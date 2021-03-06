// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Adapter
{
    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using Microsoft.VisualStudio.TestPlatform.Common.Logging;
    using Microsoft.VisualStudio.TestPlatform.CrossPlatEngine.Execution;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Adapter;
    using Microsoft.VisualStudio.TestPlatform.ObjectModel.Engine;

    /// <summary>
    /// The test execution recorder used for recording test results and test messages.
    /// </summary>
    internal class TestExecutionRecorder : TestSessionMessageLogger, ITestExecutionRecorder
    {
        private List<AttachmentSet> attachmentSets;
        private ITestRunCache testRunCache;
        private ITestCaseEventsHandler testCaseEventsHandler;

        private HashSet<Guid> testCaseEndStatusMap;

        private object testCaseEndStatusSyncObject = new object();

        /// <summary>
        /// Initializes a new instance of the <see cref="TestExecutionRecorder"/> class.
        /// </summary>
        /// <param name="testCaseEventsHandler"> The test Case Events Handler. </param>
        /// <param name="testRunCache"> The test run cache.  </param>
        public TestExecutionRecorder(ITestCaseEventsHandler testCaseEventsHandler, ITestRunCache testRunCache)
        {
            this.testRunCache = testRunCache;
            this.testCaseEventsHandler = testCaseEventsHandler;
            this.attachmentSets = new List<AttachmentSet>();

            // As a framework guideline, we should get events in this order:
            // 1. Test Case Start.
            // 2. Test Case End.
            // 3. Test Case Result.
            // If that is not that case.
            // If Test Adapters don't send the events in the above order, Test Case Results are cached till the Test Case End event is received.
            this.testCaseEndStatusMap = new HashSet<Guid>();
        }

        /// <summary>
        /// Gets the attachments received from adapters.
        /// </summary>
        internal Collection<AttachmentSet> Attachments
        {
            get
            {
                return new Collection<AttachmentSet>(this.attachmentSets);
            }
        }

        /// <summary>
        /// Notify the framework about starting of the test case. 
        /// Framework sends this event to data collectors enabled in the run. If no data collector is enabled, then the event is ignored. 
        /// </summary>
        /// <param name="testCase">test case which will be started.</param>
        public void RecordStart(TestCase testCase)
        {
            this.testRunCache.OnTestStarted(testCase);

            if (this.testCaseEventsHandler != null)
            {
                lock (this.testCaseEndStatusSyncObject)
                {
                    this.testCaseEndStatusMap.Remove(testCase.Id);
                }

                this.testCaseEventsHandler.SendTestCaseStart(testCase);
            }
        }

        /// <summary>
        /// Notify the framework about the test result.
        /// </summary>
        /// <param name="testResult">Test Result to be sent to the framework.</param>
        /// <exception cref="TestCanceledException">Exception thrown by the framework when an executor attempts to send 
        /// test result to the framework when the test(s) is canceled. </exception>  
        public void RecordResult(TestResult testResult)
        {
            if (this.testCaseEventsHandler != null)
            {
                var testCaseId = testResult.TestCase.Id;

                lock (this.testCaseEndStatusSyncObject)
                {
                    if (this.testCaseEndStatusMap.Contains(testCaseId))
                    {
                        this.testCaseEventsHandler.SendTestResult(testResult);
                    }
                }
            }

            // Test Result should always be flushed, even if datacollecter attachement is missing
            this.testRunCache.OnNewTestResult(testResult);
        }

        /// <summary>
        /// Notify the framework about completion of the test case. 
        /// Framework sends this event to data collectors enabled in the run. If no data collector is enabled, then the event is ignored. 
        /// </summary>
        /// <param name="testCase">test case which has completed.</param>
        /// <param name="outcome">outcome of the test case.</param>
        public void RecordEnd(TestCase testCase, TestOutcome outcome)
        {
            this.testRunCache.OnTestCompletion(testCase);

            if (this.testCaseEventsHandler != null)
            {
                var isTestCaseEndAlreadySent = false;

                lock (this.testCaseEndStatusSyncObject)
                {
                    isTestCaseEndAlreadySent = this.testCaseEndStatusMap.Contains(testCase.Id);

                    // Do not support multiple - testcasends for a single test case start
                    // TestCaseEnd must always be preceded by testcasestart for a given test case id
                    if (!isTestCaseEndAlreadySent)
                    {
                        this.testCaseEndStatusMap.Add(testCase.Id);

                        // Send test case end event to handler.
                        this.testCaseEventsHandler.SendTestCaseEnd(testCase, outcome);
                    }
                }
            }
        }

        /// <summary>
        /// Notify the framework about run level attachments.
        /// </summary>
        /// <param name="attachments"> The attachment sets. </param>
        public void RecordAttachments(IList<AttachmentSet> attachments)
        {
            this.attachmentSets.AddRange(attachments);
        }
    }
}