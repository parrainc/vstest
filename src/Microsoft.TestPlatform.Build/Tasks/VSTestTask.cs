﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TestPlatform.Build.Tasks
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using Microsoft.Build.Framework;
    using Microsoft.Build.Utilities;
    using Microsoft.TestPlatform.Build.Resources;
    using Microsoft.TestPlatform.Build.Utils;
    using Trace;

    public class VSTestTask : Task, ICancelableTask
    {
        // The process which is invoking vstest.console
        private VSTestForwardingApp vsTestForwardingApp;

        private const string vsTestAppName = "vstest.console.dll";

        public string TestFileFullPath
        {
            get;
            set;
        }

        public string VSTestSetting
        {
            get;
            set;
        }

        public string[] VSTestTestAdapterPath
        {
            get;
            set;
        }

        public string VSTestFramework
        {
            get;
            set;
        }

        public string VSTestPlatform
        {
            get;
            set;
        }

        public string VSTestTestCaseFilter
        {
            get;
            set;
        }
        public string[] VSTestLogger
        {
            get;
            set;
        }

        public string VSTestListTests
        {
            get;
            set;
        }

        public string VSTestDiag
        {
            get;
            set;
        }

        public string[] VSTestCLIRunSettings
        {
            get;
            set;
        }

        [Required]
        public string VSTestConsolePath
        {
            get;
            set;
        }

        public string VSTestResultsDirectory
        {
            get;
            set;
        }

        public string VSTestVerbosity
        {
            get;
            set;
        }

        public string[] VSTestCollect
        {
            get;
            set;
        }

        public string VSTestBlame
        {
            get;
            set;
        }

        public string VSTestTraceDataCollectorDirectoryPath
        {
            get;
            set;
        }

        public override bool Execute()
        {
            var traceEnabledValue = Environment.GetEnvironmentVariable("VSTEST_BUILD_TRACE");
            Tracing.traceEnabled = !string.IsNullOrEmpty(traceEnabledValue) && traceEnabledValue.Equals("1", StringComparison.OrdinalIgnoreCase);

            vsTestForwardingApp = new VSTestForwardingApp(this.VSTestConsolePath, this.CreateArgument());
            if (!string.IsNullOrEmpty(this.VSTestFramework))
            {
                Console.WriteLine(Resources.TestRunningSummary, this.TestFileFullPath, this.VSTestFramework);
            }

            return vsTestForwardingApp.Execute() == 0;
        }

        public void Cancel()
        {
            Tracing.Trace("VSTest: Killing the process...");
            vsTestForwardingApp.Cancel();
        }

        internal IEnumerable<string> CreateArgument()
        {
            var isConsoleLoggerEnabled = true;
            var isCollectCodeCoverageEnabled = false;
            var isRunSettingsEnabled = false;
            var allArgs = new List<string>();

            // TODO log arguments in task
            if (!string.IsNullOrEmpty(this.VSTestSetting))
            {
                isRunSettingsEnabled = true;
                allArgs.Add("--settings:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(this.VSTestSetting));
            }

            if (this.VSTestTestAdapterPath != null && this.VSTestTestAdapterPath.Length > 0)
            {
                foreach (var arg in this.VSTestTestAdapterPath)
                {
                    allArgs.Add("--testAdapterPath:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(arg));
                }
            }

            if (!string.IsNullOrEmpty(this.VSTestFramework))
            {
                allArgs.Add("--framework:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(this.VSTestFramework));
            }

            // vstest.console only support x86 and x64 for argument platform
            if (!string.IsNullOrEmpty(this.VSTestPlatform) && !this.VSTestPlatform.Contains("AnyCPU"))
            {
                allArgs.Add("--platform:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(this.VSTestPlatform));
            }

            if (!string.IsNullOrEmpty(this.VSTestTestCaseFilter))
            {
                allArgs.Add("--testCaseFilter:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(this.VSTestTestCaseFilter));
            }

            if (this.VSTestLogger != null && this.VSTestLogger.Length > 0)
            {
                foreach (var arg in this.VSTestLogger)
                {
                    allArgs.Add("--logger:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(arg));

                    if (arg.StartsWith("console", StringComparison.OrdinalIgnoreCase))
                    {
                        isConsoleLoggerEnabled = false;
                    }
                }
            }

            if (!string.IsNullOrEmpty(this.VSTestResultsDirectory))
            {
                allArgs.Add("--resultsDirectory:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(this.VSTestResultsDirectory));
            }

            if (!string.IsNullOrEmpty(this.VSTestListTests))
            {
                allArgs.Add("--listTests");
            }

            if (!string.IsNullOrEmpty(this.VSTestDiag))
            {
                allArgs.Add("--Diag:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(this.VSTestDiag));
            }

            if (string.IsNullOrEmpty(this.TestFileFullPath))
            {
                this.Log.LogError("Test file path cannot be empty or null.");
            }
            else
            {
                allArgs.Add(ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(this.TestFileFullPath));
            }

            // Console logger was not specified by user, but verbosity was, hence add default console logger with verbosity as specified
            if (!string.IsNullOrWhiteSpace(this.VSTestVerbosity) && isConsoleLoggerEnabled)
            {
                var normalTestLogging = new List<string>() { "n", "normal", "d", "detailed", "diag", "diagnostic" };
                var quietTestLogging = new List<string>() { "q", "quiet" };

                string vsTestVerbosity = "minimal";
                if (normalTestLogging.Contains(this.VSTestVerbosity))
                {
                    vsTestVerbosity = "normal";
                }
                else if (quietTestLogging.Contains(this.VSTestVerbosity))
                {
                    vsTestVerbosity = "quiet";
                }

                allArgs.Add("--logger:Console;Verbosity=" + vsTestVerbosity);
            }

            if (this.VSTestCLIRunSettings != null && this.VSTestCLIRunSettings.Length > 0)
            {
                allArgs.Add("--");
                foreach (var arg in this.VSTestCLIRunSettings)
                {
                    allArgs.Add(ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(arg));
                }
            }

            if (this.VSTestCollect != null && this.VSTestCollect.Length > 0)
            {
                foreach (var arg in this.VSTestCollect)
                {
                    if (arg.Equals("Code Coverage", StringComparison.OrdinalIgnoreCase))
                    {
                        isCollectCodeCoverageEnabled = true;
                    }
                    allArgs.Add("--collect:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(arg));
                }
            }

            if (isCollectCodeCoverageEnabled || isRunSettingsEnabled)
            {

                // Pass TraceDataCollector path to vstest.console as TestAdapterPath if --collect "Code Coverage"
                // or --settings (User can enable code coverage from runsettings) option given.
                // Not parsing the runsettings for two reason:
                //    1. To keep no knowledge of runsettings structure in VSTestTask.
                //    2. Impact of adding adapter path always is minimal. (worst case: loads additional data collector assembly in datacollector process.)
                // This is required due to currently trace datacollector not ships with dotnet sdk, can be remove once we have
                // go code coverage x-plat.
                if (!string.IsNullOrEmpty(this.VSTestTraceDataCollectorDirectoryPath))
                {
                    allArgs.Add("--testAdapterPath:" + ArgumentEscaper.HandleEscapeSequenceInArgForProcessStart(this.VSTestTraceDataCollectorDirectoryPath));
                }
            }

            if (!string.IsNullOrEmpty(this.VSTestBlame))
            {
                allArgs.Add("--Blame");
            }

            return allArgs;
        }
    }
}
