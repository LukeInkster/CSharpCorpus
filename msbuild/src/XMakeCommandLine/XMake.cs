﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Configuration;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading;
using System.Xml;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
#if (!STANDALONEBUILD)
using Microsoft.Internal.Performance;
#endif
#if MSBUILDENABLEVSPROFILING 
using Microsoft.VisualStudio.Profiler;
#endif

using FileLogger = Microsoft.Build.Logging.FileLogger;
using ConsoleLogger = Microsoft.Build.Logging.ConsoleLogger;
using LoggerDescription = Microsoft.Build.Logging.LoggerDescription;
using ForwardingLoggerRecord = Microsoft.Build.Logging.ForwardingLoggerRecord;
using System.Runtime.CompilerServices;

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// This class implements the MSBuild.exe command-line application. It processes
    /// command-line arguments and invokes the build engine.
    /// </summary>
    static public class MSBuildApp
    {
        /// <summary>
        /// Enumeration of the various ways in which the MSBuild.exe application can exit.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "shipped already")]
        public enum ExitType
        {
            /// <summary>
            /// The application executed successfully.
            /// </summary>
            Success,
            /// <summary>
            /// There was a syntax error in a command line argument.
            /// </summary>
            SwitchError,
            /// <summary>
            /// A command line argument was not valid.
            /// </summary>
            InitializationError,
            /// <summary>
            /// The build failed.
            /// </summary>
            BuildError,
            /// <summary>
            /// A logger aborted the build.
            /// </summary>
            LoggerAbort,
            /// <summary>
            /// A logger failed unexpectedly.
            /// </summary>
            LoggerFailure,
            /// <summary>
            /// The build stopped unexpectedly, for example,
            /// because a child died or hung.
            /// </summary>
            Unexpected
        }

        /// <summary>
        /// Whether the static constructor ran successfully.
        /// </summary>
        private static bool s_initialized;

        /// <summary>
        /// The object used to synchronize access to shared build state
        /// </summary>
        private static Object s_buildLock = new Object();

        /// <summary>
        /// The currently active build, if any.
        /// </summary>
        private static BuildSubmission s_activeBuild;

        /// <summary>
        /// Event signalled when the build is complete.
        /// </summary>
        private static ManualResetEvent s_buildComplete = new ManualResetEvent(false);

        /// <summary>
        /// Event signalled when the cancel method is complete.
        /// </summary>
        private static ManualResetEvent s_cancelComplete = new ManualResetEvent(true);

        /// <summary>
        /// Set to 1 when the cancel method has been invoked.
        /// Never reset to false: subsequent hits of Ctrl-C should do nothing
        /// </summary>
        private static int s_receivedCancel;

        /// <summary>
        /// Static constructor
        /// </summary>
        static MSBuildApp()
        {
            try
            {
                ////////////////////////////////////////////////////////////////////////////////
                //  Only initialize static fields here, not inline!                           //
                //  This forces the type to initialize in this static constructor and thus    //
                //  any configuration file exceptions can be caught here.                     //
                ////////////////////////////////////////////////////////////////////////////////
                s_exePath = Path.GetDirectoryName(FileUtilities.ExecutingAssemblyPath);

                s_initialized = true;
            }
            catch (TypeInitializationException ex)
            {
                if (ex.InnerException == null || ex.InnerException.GetType() != typeof(ConfigurationErrorsException))
                {
                    throw;
                }
                HandleConfigurationException(ex);
            }
            catch (ConfigurationException ex)
            {
                HandleConfigurationException(ex);
            }
        }

        /// <summary>
        /// Static no-op method to force static constructor to run and initialize resources.
        /// This is useful for unit tests.
        /// </summary>
        internal static void Initialize()
        {
        }

        /// <summary>
        /// Dump any exceptions reading the configuration file, nicely
        /// </summary>
        private static void HandleConfigurationException(Exception ex)
        {
            // Error reading the configuration file - eg., unexpected element
            // Since we expect users to edit it to add toolsets, this is not unreasonable to expect
            StringBuilder builder = new StringBuilder();

            Exception exception = ex;
            do
            {
                string message = exception.Message.TrimEnd();

                builder.Append(message);

                // One of the exceptions is missing a period!
                if (message[message.Length - 1] != '.')
                {
                    builder.Append(".");
                }
                builder.Append(" ");

                exception = exception.InnerException;
            }
            while (exception != null);

            Console.WriteLine(ResourceUtilities.FormatResourceString("InvalidConfigurationFile", builder.ToString()));

            s_initialized = false;
        }

        /// <summary>
        /// This is the entry point for the application.
        /// </summary>
        /// <remarks>
        /// MSBuild no longer runs any arbitrary code (tasks or loggers) on the main thread, so it never needs the
        /// main thread to be in an STA. Accordingly, to avoid ambiguity, we explicitly use the [MTAThread] attribute.
        /// This doesn't actually do any work unless COM interop occurs for some reason.
        /// </remarks>
        /// <returns>0 on success, 1 on failure</returns>
        [MTAThread]
        public static int Main()
        {
            if (Environment.GetEnvironmentVariable("MSBUILDDUMPPROCESSCOUNTERS") == "1")
            {
                DumpCounters(true /* initialize only */);
            }

            // return 0 on success, non-zero on failure
            int exitCode = ((s_initialized && Execute(Environment.CommandLine) == ExitType.Success) ? 0 : 1);

            if (Environment.GetEnvironmentVariable("MSBUILDDUMPPROCESSCOUNTERS") == "1")
            {
                DumpCounters(false /* log to console */);
            }

            return exitCode;
        }

        /// <summary>
        /// Append output file with elapsedTime
        /// </summary>
        /// <comments>
        /// This is a non-supported feature to facilitate timing multiple runs
        /// </comments>
        static private void AppendOutputFile(string path, Int64 elapsedTime)
        {
            if (!File.Exists(path))
            {
                using (StreamWriter sw = File.CreateText(path))
                {
                    sw.WriteLine(elapsedTime);
                }
            }
            else
            {
                using (StreamWriter sw = File.AppendText(path))
                {
                    sw.WriteLine(elapsedTime);
                }
            }
        }

        /// <summary>
        /// Dump process counters in parseable format.
        /// These can't be gotten after the process ends, so log them here.
        /// These are for the current process only: remote nodes are not counted.
        /// </summary>
        /// <comments>
        /// Because some of these counters give bogus results or are poorly defined,
        /// we only dump counters if an undocumented environment variable is set.
        /// Also, the strings are not localized.
        /// Before execution, this is called with initialize only, causing counters to get called with NextValue() to
        /// initialize them.
        /// </comments>         
        private static void DumpCounters(bool initializeOnly)
        {
            Process currentProcess = Process.GetCurrentProcess();

            if (!initializeOnly)
            {
                Console.WriteLine("\n{0}{1}{0}", new String('=', 41 - "Process".Length / 2), "Process");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Peak Working Set", currentProcess.PeakWorkingSet64, "bytes");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Peak Paged Memory", currentProcess.PeakPagedMemorySize64, "bytes"); // Not very useful one
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Peak Virtual Memory", currentProcess.PeakVirtualMemorySize64, "bytes"); // Not very useful one
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Peak Privileged Processor Time", currentProcess.PrivilegedProcessorTime.TotalMilliseconds, "ms");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Peak User Processor Time", currentProcess.UserProcessorTime.TotalMilliseconds, "ms");
                Console.WriteLine("||{0,50}|{1,20:N0}|{2,8}|", "Peak Total Processor Time", currentProcess.TotalProcessorTime.TotalMilliseconds, "ms");

                Console.WriteLine("{0}{0}", new String('=', 41));
            }

            // Now some Windows performance counters

            // First get the instance name of this process, in order to look them up.
            // Generally, the instance names, such as "msbuild" and "msbuild#2" are non deterministic; we want this process.
            // Don't use the "ID Process" counter out of the "Process" category, as it doesn't use the same naming scheme
            // as the .NET counters. However, the "Process ID" counter out of the ".NET CLR Memory" category apparently uses
            // the same scheme as the other .NET categories.            
            string currentInstance = null;
            PerformanceCounterCategory processCategory = new PerformanceCounterCategory("Process");
            foreach (string instance in processCategory.GetInstanceNames())
            {
                using (PerformanceCounter counter = new PerformanceCounter(".NET CLR Memory", "Process ID", instance, true))
                {
                    try
                    {
                        if ((int)counter.RawValue == currentProcess.Id)
                        {
                            currentInstance = instance;
                            break;
                        }
                    }
                    catch (InvalidOperationException) // Instance 'WmiApSrv' does not exist in the specified Category. (??)
                    {
                    }
                }
            }

            foreach (PerformanceCounterCategory category in PerformanceCounterCategory.GetCategories())
            {
                DumpAllInCategory(currentInstance, category, initializeOnly);
            }
        }

        /// <summary>
        /// Dumps all counters in the category
        /// </summary>        
        private static void DumpAllInCategory(string currentInstance, PerformanceCounterCategory category, bool initializeOnly)
        {
            if (category.CategoryName.IndexOf("remoting", StringComparison.OrdinalIgnoreCase) != -1) // not interesting
            {
                return;
            }

            PerformanceCounter[] counters;
            try
            {
                counters = category.GetCounters(currentInstance);
            }
            catch (InvalidOperationException)
            {
                // This is a system-wide category, ignore those
                return;
            }

            if (!initializeOnly)
            {
                Console.WriteLine("\n{0}{1}{0}", new String('=', 41 - category.CategoryName.Length / 2), category.CategoryName);
            }

            foreach (PerformanceCounter counter in counters)
            {
                DumpCounter(counter, initializeOnly);
            }

            if (!initializeOnly)
            {
                Console.WriteLine("{0}{0}", new String('=', 41));
            }
        }

        /// <summary>
        /// Dumps one counter
        /// </summary>          
        private static void DumpCounter(PerformanceCounter counter, bool initializeOnly)
        {
            try
            {
                if (counter.CounterName.IndexOf("not displayed", StringComparison.OrdinalIgnoreCase) != -1)
                {
                    return;
                }

                float value = counter.NextValue();

                if (!initializeOnly)
                {
                    string friendlyCounterType = GetFriendlyCounterType(counter.CounterType, counter.CounterName);
                    string valueFormat;

                    // At least some (such as % in GC; maybe all) "%" counters are already multiplied by 100. So we don't do that here.

                    // Show decimal places if meaningful
                    valueFormat = value < 10 ? "{0,20:N2}" : "{0,20:N0}";

                    string valueString = String.Format(CultureInfo.CurrentCulture, valueFormat, value);

                    Console.WriteLine("||{0,50}|{1}|{2,8}|", counter.CounterName, valueString, friendlyCounterType);
                }
            }
            catch (InvalidOperationException) // Instance 'WmiApSrv' does not exist in the specified Category. (??)
            {
            }
        }

        /// <summary>
        /// Gets a friendly representation of the counter units
        /// </summary>        
        private static string GetFriendlyCounterType(PerformanceCounterType type, string name)
        {
            if (name.IndexOf("bytes", StringComparison.OrdinalIgnoreCase) != -1)
            {
                return "bytes";
            }

            if (name.IndexOf("threads", StringComparison.OrdinalIgnoreCase) != -1)
            {
                return "threads";
            }

            switch (type)
            {
                case PerformanceCounterType.ElapsedTime:
                case PerformanceCounterType.AverageTimer32:
                    return "s";

                case PerformanceCounterType.Timer100Ns:
                case PerformanceCounterType.Timer100NsInverse:
                    return "100ns";

                case PerformanceCounterType.SampleCounter:
                case PerformanceCounterType.AverageCount64:
                case PerformanceCounterType.NumberOfItems32:
                case PerformanceCounterType.NumberOfItems64:
                case PerformanceCounterType.NumberOfItemsHEX32:
                case PerformanceCounterType.NumberOfItemsHEX64:
                case PerformanceCounterType.RateOfCountsPerSecond32:
                case PerformanceCounterType.RateOfCountsPerSecond64:
                case PerformanceCounterType.CountPerTimeInterval32:
                case PerformanceCounterType.CountPerTimeInterval64:
                case PerformanceCounterType.CounterTimer:
                case PerformanceCounterType.CounterTimerInverse:
                case PerformanceCounterType.CounterMultiTimer:
                case PerformanceCounterType.CounterMultiTimerInverse:
                case PerformanceCounterType.CounterDelta32:
                case PerformanceCounterType.CounterDelta64:
                    return "#";

                case PerformanceCounterType.CounterMultiTimer100Ns:
                case PerformanceCounterType.CounterMultiTimer100NsInverse:
                case PerformanceCounterType.RawFraction:
                case PerformanceCounterType.SampleFraction:
                    return "%";

                case PerformanceCounterType.AverageBase:
                case PerformanceCounterType.RawBase:
                case PerformanceCounterType.SampleBase:
                case PerformanceCounterType.CounterMultiBase:
                default:
                    return "?";
            }
        }

        /// <summary>
        /// Orchestrates the execution of the application, and is also responsible
        /// for top-level error handling.
        /// </summary>
        /// <param name="commandLine">The command line to process. The first argument
        /// on the command line is assumed to be the name/path of the executable, and
        /// is ignored.</param>
        /// <returns>A value of type ExitType that indicates whether the build succeeded,
        /// or the manner in which it failed.</returns>
        public static ExitType Execute(string commandLine)
        {
            // Indicate to the engine that it can toss extraneous file content
            // when it loads microsoft.*.targets. We can't do this in the general case,
            // because tasks in the build can (and occasionally do) load MSBuild format files
            // with our OM and modify and save them. They'll never do this for Microsoft.*.targets, though,
            // and those form the great majority of our unnecessary memory use.
            Environment.SetEnvironmentVariable("MSBuildLoadMicrosoftTargetsReadOnly", "true");

            string debugFlag = Environment.GetEnvironmentVariable("MSBUILDDEBUGONSTART");
            if (debugFlag == "1")
            {
                Debugger.Launch();
            }
            else if (debugFlag == "2")
            {
                // Sometimes easier to attach rather than deal with JIT prompt
                Console.ReadLine();
            }

            ErrorUtilities.VerifyThrowArgumentLength(commandLine, "commandLine");

            ExitType exitType = ExitType.Success;

            ConsoleCancelEventHandler cancelHandler = new ConsoleCancelEventHandler(Console_CancelKeyPress);
            try
            {
#if (!STANDALONEBUILD)
                // Enable CodeMarkers for MSBuild.exe
                CodeMarkers.Instance.InitPerformanceDll(CodeMarkerApp.MSBUILDPERF, @"Software\Microsoft\MSBuild\4.0");
#endif
#if MSBUILDENABLEVSPROFILING 
                string startMSBuildExe = String.Format(CultureInfo.CurrentCulture, "Running MSBuild.exe with command line {0}", commandLine);
                DataCollection.CommentMarkProfile(8800, startMSBuildExe);
#endif
                Console.CancelKeyPress += cancelHandler;

                // check the operating system the code is running on
                VerifyThrowSupportedOS();

                // Setup the console UI.
                SetConsoleUI();

                // reset the application state for this new build
                ResetBuildState();

                // process the detected command line switches -- gather build information, take action on non-build switches, and
                // check for non-trivial errors
                string projectFile = null;
                string[] targets = { };
                string toolsVersion = null;
                Dictionary<string, string> globalProperties = null;
                ILogger[] loggers = { };
                LoggerVerbosity verbosity = LoggerVerbosity.Normal;
                List<DistributedLoggerRecord> distributedLoggerRecords = null;
                bool needToValidateProject = false;
                string schemaFile = null;
                int cpuCount = 1;
                bool enableNodeReuse = true;
                TextWriter preprocessWriter = null;
                bool debugger = false;
                bool detailedSummary = false;

                CommandLineSwitches switchesFromAutoResponseFile;
                CommandLineSwitches switchesNotFromAutoResponseFile;
                GatherAllSwitches(commandLine, out switchesFromAutoResponseFile, out switchesNotFromAutoResponseFile);

                if (ProcessCommandLineSwitches(
                        switchesFromAutoResponseFile,
                        switchesNotFromAutoResponseFile,
                        ref projectFile,
                        ref targets,
                        ref toolsVersion,
                        ref globalProperties,
                        ref loggers,
                        ref verbosity,
                        ref distributedLoggerRecords,
                        ref needToValidateProject,
                        ref schemaFile,
                        ref cpuCount,
                        ref enableNodeReuse,
                        ref preprocessWriter,
                        ref debugger,
                        ref detailedSummary,
                        recursing: false
                        ))
                {
                    // Unfortunately /m isn't the default, and we are not yet brave enough to make it the default.
                    // However we want to give a hint to anyone who is building single proc without realizing it that there
                    // is a better way.
                    if (cpuCount == 1 && FileUtilities.IsSolutionFilename(projectFile) && verbosity > LoggerVerbosity.Minimal)
                    {
                        Console.WriteLine(ResourceUtilities.FormatResourceString("PossiblyOmittedMaxCPUSwitch"));
                    }

                    if (preprocessWriter != null || debugger)
                    {
                        // Indicate to the engine that it can NOT toss extraneous file content: we want to 
                        // see that in preprocessing/debugging
                        Environment.SetEnvironmentVariable("MSBUILDLOADALLFILESASWRITEABLE", "1");
                    }

                    DateTime t1 = DateTime.Now;

#if !STANDALONEBUILD
                    if (Environment.GetEnvironmentVariable("MSBUILDOLDOM") != "1")
#endif
                    {
                        // if everything checks out, and sufficient information is available to start building
                        if (!BuildProject(projectFile, targets, toolsVersion, globalProperties, loggers, verbosity, distributedLoggerRecords.ToArray(), needToValidateProject, schemaFile, cpuCount, enableNodeReuse, preprocessWriter, debugger, detailedSummary))
                        {
                            exitType = ExitType.BuildError;
                        }
                    }
#if !STANDALONEBUILD
                    else
                    {
                        exitType = OldOMBuildProject(exitType, projectFile, targets, toolsVersion, globalProperties, loggers, verbosity, needToValidateProject, schemaFile, cpuCount);
                    }
#endif
                    DateTime t2 = DateTime.Now;

                    TimeSpan elapsedTime = t2.Subtract(t1);

                    string timerOutputFilename = Environment.GetEnvironmentVariable("MSBUILDTIMEROUTPUTS");

                    if (!String.IsNullOrEmpty(timerOutputFilename))
                    {
                        AppendOutputFile(timerOutputFilename, elapsedTime.Milliseconds);
                    }
                }
                else
                {
                    // if there was no need to start the build e.g. because /help was triggered
                    // do nothing
                }
            }
            /**********************************************************************************************************************
             * WARNING: Do NOT add any more catch blocks below! Exceptions should be caught as close to their point of origin as
             * possible, and converted into one of the known exceptions. The code that causes an exception best understands the
             * reason for the exception, and only that code can provide the proper error message. We do NOT want to display
             * messages from unknown exceptions, because those messages are most likely neither localized, nor composed in the
             * canonical form with the correct prefix.
             *********************************************************************************************************************/
            // handle switch errors
            catch (CommandLineSwitchException e)
            {
                Console.WriteLine(e.Message);
                Console.WriteLine();
                // prompt user to display help for proper switch usage
                ShowHelpPrompt();

                exitType = ExitType.SwitchError;
            }
            // handle configuration exceptions: problems reading toolset information from msbuild.exe.config or the registry
            catch (InvalidToolsetDefinitionException e)
            {
                // Brief prefix to indicate that it's a configuration failure, and provide the "error" indication
                Console.WriteLine(ResourceUtilities.FormatResourceString("ConfigurationFailurePrefixNoErrorCode", e.ErrorCode, e.Message));

                exitType = ExitType.InitializationError;
            }
            // handle initialization failures
            catch (InitializationException e)
            {
                Console.WriteLine(e.Message);

                exitType = ExitType.InitializationError;
            }
            // handle polite logger failures: don't dump the stack or trigger watson for these
            catch (LoggerException e)
            {
                // display the localized message from the outer exception in canonical format
                if (null != e.ErrorCode)
                {
                    // Brief prefix to indicate that it's a logger failure, and provide the "error" indication
                    Console.WriteLine(ResourceUtilities.FormatResourceString("LoggerFailurePrefixNoErrorCode", e.ErrorCode, e.Message));
                }
                else
                {
                    // Brief prefix to indicate that it's a logger failure, adding a generic error code to make sure
                    // there's something for the user to look up in the documentation
                    Console.WriteLine(ResourceUtilities.FormatResourceString("LoggerFailurePrefixWithErrorCode", e.Message));
                }

                if (null != e.InnerException)
                {
                    // write out exception details -- don't bother triggering Watson, because most of these exceptions will be coming
                    // from buggy loggers written by users
                    Console.WriteLine(e.InnerException.ToString());
                }

                exitType = ExitType.LoggerAbort;
            }
            // handle logger failures (logger bugs)
            catch (InternalLoggerException e)
            {
                if (!e.InitializationException)
                {
                    // display the localized message from the outer exception in canonical format
                    Console.WriteLine("MSBUILD : error " + e.ErrorCode + ": " + e.Message);
#if DEBUG
                    Console.WriteLine("This is an unhandled exception from a logger -- PLEASE OPEN A BUG AGAINST THE LOGGER OWNER.");
#endif
                    // write out exception details -- don't bother triggering Watson, because most of these exceptions will be coming
                    // from buggy loggers written by users
                    Console.WriteLine(e.InnerException.ToString());

                    exitType = ExitType.LoggerFailure;
                }
                else
                {
                    Console.WriteLine("MSBUILD : error " + e.ErrorCode + ": " + e.Message +
                        (e.InnerException != null ? " " + e.InnerException.Message : ""));
                    exitType = ExitType.InitializationError;
                }
            }
            catch (BuildAbortedException e)
            {
                Console.WriteLine("MSBUILD : error " + e.ErrorCode + ": " + e.Message +
                                (e.InnerException != null ? " " + e.InnerException.Message : String.Empty));

                exitType = ExitType.Unexpected;
            }
            // handle fatal errors
            catch (Exception e)
            {
                // display a generic localized message for the user
                Console.WriteLine("{0}\r\n{1}", AssemblyResources.GetString("FatalError"), e.ToString());
#if DEBUG
                Console.WriteLine("This is an unhandled exception in MSBuild Engine -- PLEASE OPEN A BUG AGAINST THE MSBUILD TEAM.\r\n{0}", e.ToString());
#endif
                // rethrow, in case Watson is enabled on the machine -- if not, the CLR will write out exception details
                // allow the build lab to set an env var to avoid jamming the build
                if (Environment.GetEnvironmentVariable("MSBUILDDONOTLAUNCHDEBUGGER") != "1")
                {
                    throw;
                }
            }
            finally
            {
                s_buildComplete.Set();
                Console.CancelKeyPress -= cancelHandler;

                // Wait for any pending cancel, so that we get any remaining messages
                s_cancelComplete.WaitOne();
#if (!STANDALONEBUILD)
                // Turn off codemarkers
                CodeMarkers.Instance.UninitializePerformanceDLL(CodeMarkerApp.MSBUILDPERF);
#endif
            }
            /**********************************************************************************************************************
             * WARNING: Do NOT add any more catch blocks above!
             *********************************************************************************************************************/

            return exitType;
        }
#if (!STANDALONEBUILD)
        /// <summary>
        /// Use the Orcas Engine to build the project
        /// #############################################################################################
        /// #### Segregated into another method to avoid loading the old Engine in the regular case. ####
        /// #### Do not move back in to the main code path! #############################################
        /// #############################################################################################
        ///  We have marked this method as NoInlining because we do not want Microsoft.Build.Engine.dll to be loaded unless we really execute this code path
        /// </summary>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static ExitType OldOMBuildProject(ExitType exitType, string projectFile, string[] targets, string toolsVersion, Dictionary<string, string> globalProperties, ILogger[] loggers, LoggerVerbosity verbosity, bool needToValidateProject, string schemaFile, int cpuCount)
        {
            // Log something to avoid confusion caused by errant environment variable sending us down here
            Console.WriteLine(AssemblyResources.GetString("Using35Engine"));

            Microsoft.Build.BuildEngine.BuildPropertyGroup oldGlobalProps = new Microsoft.Build.BuildEngine.BuildPropertyGroup();
            // Copy over the global properties to the old OM
            foreach (KeyValuePair<string, string> globalProp in globalProperties)
            {
                oldGlobalProps.SetProperty(globalProp.Key, globalProp.Value);
            }

            if (!BuildProjectWithOldOM(projectFile, targets, toolsVersion, oldGlobalProps, loggers, verbosity, null, needToValidateProject, schemaFile, cpuCount))
            {
                exitType = ExitType.BuildError;
            }
            return exitType;
        }
#endif
        /// <summary>
        /// Handler for when CTRL-C or CTRL-BREAK is called.
        /// CTRL-BREAK means "die immediately"
        /// CTRL-C means "try to stop work and exit cleanly"
        /// </summary>
        private static void Console_CancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            if (e.SpecialKey == ConsoleSpecialKey.ControlBreak)
            {
                e.Cancel = false; // required; the process will now be terminated rudely
                return;
            }

            e.Cancel = true; // do not terminate rudely

            bool alreadyCalled = (Interlocked.CompareExchange(ref s_receivedCancel, 1, 0) == 1);

            if (alreadyCalled)
            {
                return;
            }

            Console.WriteLine(ResourceUtilities.FormatResourceString("AbortingBuild"));

            // The OS takes a lock in
            // kernel32.dll!_SetConsoleCtrlHandler, so if a task
            // waits for that lock somehow before quitting, it would hang
            // because we're in it here. One way a task can end up here is 
            // by calling Microsoft.Win32.SystemEvents.Initialize.
            // So do our work asynchronously so we can return immediately.
            // We're already on a threadpool thread anyway.
            WaitCallback callback = new WaitCallback(
            delegate (Object state)
            {
                s_cancelComplete.Reset();

                // If the build is already complete, just exit.
                // Cannot use WaitOne(int) overload because it does not exist in BCL 3.5; this is equivalent
                if (s_buildComplete.WaitOne(0, false /* do not exit context */))
                {
                    s_cancelComplete.Set();
                    return;
                }

                // If the build has already started (or already finished), we will cancel it
                // If the build has not yet started, it will cancel itself, because
                // we set alreadyCalled=1
                BuildSubmission result = null;
                lock (s_buildLock)
                {
                    result = s_activeBuild;
                }

                if (result != null)
                {
                    BuildManager.DefaultBuildManager.CancelAllSubmissions();
                    s_buildComplete.WaitOne();
                }

                s_cancelComplete.Set(); // This will release our main Execute method so we can finally exit.
            });

            ThreadPoolExtensions.QueueThreadPoolWorkItemWithCulture(callback, Thread.CurrentThread.CurrentCulture, Thread.CurrentThread.CurrentUICulture);
        }

        /// <summary>
        /// Clears out any state accumulated from previous builds, and resets
        /// member data in preparation for a new build.
        /// </summary>
        private static void ResetBuildState()
        {
            s_includedResponseFiles = new ArrayList();
            usingSwitchesFromAutoResponseFile = false;
        }

        /// <summary>
        /// The location of the application executable.
        /// </summary>
        /// <remarks>
        /// Initialized in the static constructor. See comment there.
        /// </remarks>
        private static readonly string s_exePath; // Do not initialize

        /// <summary>
        /// Name of the exe (presumably msbuild.exe)
        /// </summary>
        private static string s_exeName;

        /// <summary>
        /// Default name for the msbuild log file
        /// </summary>
        private const string msbuildLogFileName = "msbuild.log";

        /// <summary>
        /// Initializes the build engine, and starts the project building.
        /// </summary>
        /// <returns>true, if build succeeds</returns>
        [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Not going to refactor it right now")]
        internal static bool BuildProject
        (
            string projectFile,
            string[] targets,
            string toolsVersion,
            Dictionary<string, string> globalProperties,
            ILogger[] loggers,
            LoggerVerbosity verbosity,
            DistributedLoggerRecord[] distributedLoggerRecords,
            bool needToValidateProject,
            string schemaFile,
            int cpuCount,
            bool enableNodeReuse,
            TextWriter preprocessWriter,
            bool debugger,
            bool detailedSummary
        )
        {
            if (String.Equals(Path.GetExtension(projectFile), ".vcproj", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(Path.GetExtension(projectFile), ".dsp", StringComparison.OrdinalIgnoreCase))
            {
                InitializationException.Throw(ResourceUtilities.FormatResourceString("ProjectUpgradeNeededToVcxProj", projectFile), null);
            }

            bool success = false;

            ProjectCollection projectCollection = null;
            bool onlyLogCriticalEvents = false;

            try
            {
                List<ForwardingLoggerRecord> remoteLoggerRecords = new List<ForwardingLoggerRecord>();
                foreach (DistributedLoggerRecord distRecord in distributedLoggerRecords)
                {
                    remoteLoggerRecords.Add(new ForwardingLoggerRecord(distRecord.CentralLogger, distRecord.ForwardingLoggerDescription));
                }

                // Targeted perf optimization for the case where we only have our own parallel console logger, and verbosity is quiet. In such a case
                // we know we won't emit any messages except for errors and warnings, so the engine should not bother even logging them.
                // If we're using the original serial console logger we can't do this, as it shows project started/finished context
                // around errors and warnings.
                // Telling the engine to not bother logging non-critical messages means that typically it can avoid loading any resources in the successful
                // build case.
                if (loggers.Length == 1 &&
                    remoteLoggerRecords.Count == 0 &&
                    verbosity == LoggerVerbosity.Quiet &&
                    loggers[0].Parameters != null &&
                    loggers[0].Parameters.IndexOf("ENABLEMPLOGGING", StringComparison.OrdinalIgnoreCase) != -1 &&
                    loggers[0].Parameters.IndexOf("DISABLEMPLOGGING", StringComparison.OrdinalIgnoreCase) == -1 &&
                    loggers[0].Parameters.IndexOf("V=", StringComparison.OrdinalIgnoreCase) == -1 &&                // Console logger could have had a verbosity
                    loggers[0].Parameters.IndexOf("VERBOSITY=", StringComparison.OrdinalIgnoreCase) == -1)          // override with the /clp switch
                {
                    // Must be exactly the console logger, not a derived type like the file logger.
                    Type t1 = loggers[0].GetType();
                    Type t2 = typeof(ConsoleLogger);
                    if (t1 == t2)
                    {
                        onlyLogCriticalEvents = true;
                    }
                }

                // HACK HACK: this enables task parameter logging.
                // This is a hack for now to make sure the perf hit only happens
                // on diagnostic. This should be changed to pipe it through properly,
                // perhaps as part of a fuller tracing feature.
                bool logTaskInputs = verbosity == LoggerVerbosity.Diagnostic;

                if (!logTaskInputs)
                {
                    foreach (var logger in loggers)
                    {
                        if (logger.Parameters != null &&
                            (logger.Parameters.IndexOf("V=DIAG", StringComparison.OrdinalIgnoreCase) != -1 ||
                             logger.Parameters.IndexOf("VERBOSITY=DIAG", StringComparison.OrdinalIgnoreCase) != -1)
                           )
                        {
                            logTaskInputs = true;
                            break;
                        }
                    }
                }

                if (!logTaskInputs)
                {
                    foreach (var logger in distributedLoggerRecords)
                    {
                        if (logger.CentralLogger != null)
                        {
                            if (logger.CentralLogger.Parameters != null &&
                                (logger.CentralLogger.Parameters.IndexOf("V=DIAG", StringComparison.OrdinalIgnoreCase) != -1 ||
                                 logger.CentralLogger.Parameters.IndexOf("VERBOSITY=DIAG", StringComparison.OrdinalIgnoreCase) != -1)
                               )
                            {
                                logTaskInputs = true;
                                break;
                            }
                        }
                    }
                }

                projectCollection = new ProjectCollection
                        (
                        globalProperties,
                        loggers,
                        null,
                        Microsoft.Build.Evaluation.ToolsetDefinitionLocations.ConfigurationFile | Microsoft.Build.Evaluation.ToolsetDefinitionLocations.Registry,
                        cpuCount,
                        onlyLogCriticalEvents
                        );

                if (debugger)
                {
                    // Debugging is not currently fully supported so we don't want to open
                    // public API for it. Also, we want to have a way to make it work when running inside VS.
                    // So use an environment variable. The undocumented /debug switch is just an easy way to set it.
                    Environment.SetEnvironmentVariable("MSBUILDDEBUGGING", "1");
                }

                if (toolsVersion != null && !projectCollection.ContainsToolset(toolsVersion))
                {
                    ThrowInvalidToolsVersionInitializationException(projectCollection.Toolsets, toolsVersion);
                }

                // If the user has requested that the schema be validated, do that here. 
                if (needToValidateProject && !FileUtilities.IsSolutionFilename(projectFile))
                {
                    Microsoft.Build.Evaluation.Project project = projectCollection.LoadProject(projectFile, globalProperties, toolsVersion);
                    Microsoft.Build.Evaluation.Toolset toolset = projectCollection.GetToolset((toolsVersion == null) ? project.ToolsVersion : toolsVersion);

                    if (toolset == null)
                    {
                        ThrowInvalidToolsVersionInitializationException(projectCollection.Toolsets, project.ToolsVersion);
                    }

                    ProjectSchemaValidationHandler.VerifyProjectSchema(projectFile, schemaFile, toolset.ToolsPath);

                    // If there are schema validation errors, an InitializationException is thrown, so if we get here,
                    // we can safely assume that the project successfully validated. 
                    projectCollection.UnloadProject(project);
                }

                if (preprocessWriter != null && !FileUtilities.IsSolutionFilename(projectFile))
                {
                    Project project = projectCollection.LoadProject(projectFile, globalProperties, toolsVersion);

                    project.SaveLogicalProject(preprocessWriter);

                    projectCollection.UnloadProject(project);
                    success = true;
                }
                else
                {
                    BuildRequestData request = new BuildRequestData(projectFile, globalProperties, toolsVersion, targets, null);

                    BuildParameters parameters = new BuildParameters(projectCollection);

                    // By default we log synchronously to the console for compatibility with previous versions,
                    // but it is slightly slower
                    if (!String.Equals(Environment.GetEnvironmentVariable("MSBUILDLOGASYNC"), "1", StringComparison.Ordinal))
                    {
                        parameters.UseSynchronousLogging = true;
                    }

                    parameters.EnableNodeReuse = enableNodeReuse;
                    parameters.NodeExeLocation = Assembly.GetExecutingAssembly().Location;
                    parameters.MaxNodeCount = cpuCount;
                    parameters.Loggers = projectCollection.Loggers;
                    parameters.ForwardingLoggers = remoteLoggerRecords;
                    parameters.ToolsetDefinitionLocations = Microsoft.Build.Evaluation.ToolsetDefinitionLocations.ConfigurationFile | Microsoft.Build.Evaluation.ToolsetDefinitionLocations.Registry;
                    parameters.DetailedSummary = detailedSummary;
                    parameters.LogTaskInputs = logTaskInputs;

                    if (!String.IsNullOrEmpty(toolsVersion))
                    {
                        parameters.DefaultToolsVersion = toolsVersion;
                    }

                    string memoryUseLimit = Environment.GetEnvironmentVariable("MSBUILDMEMORYUSELIMIT");
                    if (!String.IsNullOrEmpty(memoryUseLimit))
                    {
                        parameters.MemoryUseLimit = Convert.ToInt32(memoryUseLimit, CultureInfo.InvariantCulture);

                        // The following ensures that when we divide the use by node count to get the per-limit amount, we always end up with a 
                        // positive value - otherwise setting it too low will result in a zero, which will enable only the default cache behavior
                        // which is not what is intended by using this environment variable.
                        if (parameters.MemoryUseLimit < parameters.MaxNodeCount)
                        {
                            parameters.MemoryUseLimit = parameters.MaxNodeCount;
                        }
                    }

                    BuildManager buildManager = BuildManager.DefaultBuildManager;

#if MSBUILDENABLEVSPROFILING 
                    DataCollection.CommentMarkProfile(8800, "Pending Build Request from MSBuild.exe");
#endif
                    BuildResult results = null;
                    buildManager.BeginBuild(parameters);
                    Exception exception = null;
                    try
                    {
                        try
                        {
                            lock (s_buildLock)
                            {
                                s_activeBuild = buildManager.PendBuildRequest(request);

                                // Even if Ctrl-C was already hit, we still pend the build request and then cancel.
                                // That's so the build does not appear to have completed successfully.
                                if (s_receivedCancel == 1)
                                {
                                    buildManager.CancelAllSubmissions();
                                }
                            }

                            results = s_activeBuild.Execute();
                        }
                        finally
                        {
                            buildManager.EndBuild();
                        }
                    }
                    catch (Exception ex)
                    {
                        exception = ex;
                        success = false;
                    }

                    if (results != null && exception == null)
                    {
                        success = results.OverallResult == BuildResultCode.Success;
                        exception = results.Exception;
                    }

                    if (exception != null)
                    {
                        success = false;

                        // InvalidProjectFileExceptions have already been logged.
                        if (exception.GetType() != typeof(InvalidProjectFileException))
                        {
                            if
                                (
                                exception.GetType() == typeof(LoggerException) ||
                                exception.GetType() == typeof(InternalLoggerException)
                                )
                            {
                                // We will rethrow this so the outer exception handler can catch it, but we don't
                                // want to log the outer exception stack here.
                                throw exception;
                            }

                            if (exception.GetType() == typeof(BuildAbortedException))
                            {
                                // this is not a bug and should not dump stack. It will already have been logged
                                // appropriately, there is no need to take any further action with it.
                            }
                            else
                            {
                                // After throwing again below the stack will be reset. Make certain we log everything we 
                                // can now
                                Console.WriteLine(AssemblyResources.GetString("FatalError"));
#if DEBUG
                                Console.WriteLine("This is an unhandled exception in MSBuild -- PLEASE OPEN A BUG AGAINST THE MSBUILD TEAM.");
#endif
                                Console.WriteLine(exception.ToString());
                                Console.WriteLine();

                                throw exception;
                            }
                        }
                    }
                }
            }
            // handle project file errors
            catch (InvalidProjectFileException ex)
            {
                // just eat the exception because it has already been logged
                ErrorUtilities.VerifyThrow(ex.HasBeenLogged, "Should have been logged");
                success = false;
            }
            finally
            {
                FileUtilities.ClearCacheDirectory();
                if (projectCollection != null)
                {
                    projectCollection.Dispose();
                }

                BuildManager.DefaultBuildManager.Dispose();
            }

            return success;
        }
#if (!STANDALONEBUILD)
        /// <summary>
        /// Initializes the build engine, and starts the project build.
        /// Uses the Whidbey/Orcas object model.
        /// #############################################################################################
        /// #### Segregated into another method to avoid loading the old Engine in the regular case. ####
        /// #### Do not move back in to the main code path! #############################################
        /// #############################################################################################
        ///  We have marked this method as NoInlining because we do not want Microsoft.Build.Engine.dll to be loaded unless we really execute this code path
        /// </summary>
        /// <returns>true, if build succeeds</returns>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool BuildProjectWithOldOM(string projectFile, string[] targets, string toolsVersion, Microsoft.Build.BuildEngine.BuildPropertyGroup propertyBag, ILogger[] loggers, LoggerVerbosity verbosity, DistributedLoggerRecord[] distributedLoggerRecords, bool needToValidateProject, string schemaFile, int cpuCount)
        {
            string msbuildLocation = Path.GetDirectoryName(Assembly.GetAssembly(typeof(MSBuildApp)).Location);
            string localNodeProviderParameters = "msbuildlocation=" + msbuildLocation; /*This assembly is the exe*/ ;

            localNodeProviderParameters += ";nodereuse=false";

            Microsoft.Build.BuildEngine.Engine engine = new Microsoft.Build.BuildEngine.Engine(propertyBag, Microsoft.Build.BuildEngine.ToolsetDefinitionLocations.ConfigurationFile | Microsoft.Build.BuildEngine.ToolsetDefinitionLocations.Registry, cpuCount, localNodeProviderParameters);
            bool success = false;

            try
            {
                foreach (ILogger logger in loggers)
                {
                    engine.RegisterLogger(logger);
                }

                // Targeted perf optimization for the case where we only have our own parallel console logger, and verbosity is quiet. In such a case
                // we know we won't emit any messages except for errors and warnings, so the engine should not bother even logging them.
                // If we're using the original serial console logger we can't do this, as it shows project started/finished context
                // around errors and warnings.
                // Telling the engine to not bother logging non-critical messages means that typically it can avoid loading any resources in the successful
                // build case.
                if (loggers.Length == 1 &&
                    verbosity == LoggerVerbosity.Quiet &&
                    loggers[0].Parameters.IndexOf("ENABLEMPLOGGING", StringComparison.OrdinalIgnoreCase) != -1 &&
                    loggers[0].Parameters.IndexOf("DISABLEMPLOGGING", StringComparison.OrdinalIgnoreCase) == -1 &&
                    loggers[0].Parameters.IndexOf("V=", StringComparison.OrdinalIgnoreCase) == -1 &&                // Console logger could have had a verbosity
                    loggers[0].Parameters.IndexOf("VERBOSITY=", StringComparison.OrdinalIgnoreCase) == -1)          // override with the /clp switch
                {
                    // Must be exactly the console logger, not a derived type like the file logger.
                    Type t1 = loggers[0].GetType();
                    Type t2 = typeof(ConsoleLogger);
                    if (t1 == t2)
                    {
                        engine.OnlyLogCriticalEvents = true;
                    }
                }

                Microsoft.Build.BuildEngine.Project project = null;

                try
                {
                    project = new Microsoft.Build.BuildEngine.Project(engine, toolsVersion);
                }
                catch (InvalidOperationException e)
                {
                    InitializationException.Throw("InvalidToolsVersionError", toolsVersion, e, false /*no stack*/);
                }

                project.IsValidated = needToValidateProject;
                project.SchemaFile = schemaFile;

                project.Load(projectFile);

                success = engine.BuildProject(project, targets);
            }
            // handle project file errors
            catch (InvalidProjectFileException)
            {
                // just eat the exception because it has already been logged
            }
            finally
            {
                // Unregister loggers and finish with engine
                engine.Shutdown();
            }
            return success;
        }
#endif
        /// <summary>
        /// Verifies that the code is running on a supported operating system.
        /// </summary>
        private static void VerifyThrowSupportedOS()
        {
            if ((Environment.OSVersion.Platform == PlatformID.Win32S) ||        // Win32S
                (Environment.OSVersion.Platform == PlatformID.Win32Windows) ||  // Windows 95, Windows 98, Windows ME
                (Environment.OSVersion.Platform == PlatformID.WinCE) ||         // Windows CE
                ((Environment.OSVersion.Platform == PlatformID.Win32NT) &&      // Windows NT 4.0 and earlier
                (Environment.OSVersion.Version.Major <= 4)))
            {
                // If we're running on any of the unsupported OS's, fail immediately.  This way,
                // we don't run into some obscure error down the line, totally confusing the user.
                InitializationException.VerifyThrow(false, "UnsupportedOS");
            }
        }

        /// <summary>
        /// MSBuild.exe need to fallback to English if it cannot print Japanese (or other language) characters
        /// </summary>
        internal static void SetConsoleUI()
        {
            Thread thisThread = Thread.CurrentThread;

            // Eliminate the complex script cultures from the language selection.
            thisThread.CurrentUICulture = CultureInfo.CurrentUICulture.GetConsoleFallbackUICulture();

            // Determine if the language can be displayed in the current console codepage, otherwise set to US English
            int codepage;

            try
            {
                codepage = System.Console.OutputEncoding.CodePage;
            }
            catch (NotSupportedException)
            {
                // Failed to get code page: some customers have hit this and we don't know why
                thisThread.CurrentUICulture = new CultureInfo("en-US");
                return;
            }

            if (
                    codepage != 65001 // 65001 is Unicode
                    &&
                    codepage != thisThread.CurrentUICulture.TextInfo.OEMCodePage
                    &&
                    codepage != thisThread.CurrentUICulture.TextInfo.ANSICodePage
                )
            {
                thisThread.CurrentUICulture = new CultureInfo("en-US");
            }
        }

        /// <summary>
        /// Gets all specified switches, from the command line, as well as all
        /// response files, including the auto-response file.
        /// </summary>
        /// <param name="commandLine"></param>
        /// <returns>Combined bag of switches.</returns>
        private static void GatherAllSwitches(string commandLine, out CommandLineSwitches switchesFromAutoResponseFile, out CommandLineSwitches switchesNotFromAutoResponseFile)
        {
            // split the command line on (unquoted) whitespace
            ArrayList commandLineArgs = QuotingUtilities.SplitUnquoted(commandLine);
            // discard the first piece, because that's the path to the executable -- the rest are args
            s_exeName = QuotingUtilities.Unquote((string)commandLineArgs[0]);

            if (!s_exeName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                s_exeName += ".exe";
            }

            commandLineArgs.RemoveAt(0);

            // parse the command line, and flag syntax errors and obvious switch errors
            switchesNotFromAutoResponseFile = new CommandLineSwitches();
            GatherCommandLineSwitches(commandLineArgs, switchesNotFromAutoResponseFile);

            // parse the auto-response file (if "/noautoresponse" is not specified), and combine those switches with the
            // switches on the command line
            switchesFromAutoResponseFile = new CommandLineSwitches();
            if (!switchesNotFromAutoResponseFile[CommandLineSwitches.ParameterlessSwitch.NoAutoResponse])
            {
                GatherAutoResponseFileSwitches(s_exePath, switchesFromAutoResponseFile);
            }
        }

        /// <summary>
        /// Coordinates the parsing of the command line. It detects switches on the command line, gathers their parameters, and
        /// flags syntax errors, and other obvious switch errors.
        /// </summary>
        /// <remarks>
        /// Internal for unit testing only.
        /// </remarks>
        internal static void GatherCommandLineSwitches(ArrayList commandLineArgs, CommandLineSwitches commandLineSwitches)
        {
            foreach (string commandLineArg in commandLineArgs)
            {
                int doubleQuotesRemovedFromArg;
                string unquotedCommandLineArg = QuotingUtilities.Unquote(commandLineArg, out doubleQuotesRemovedFromArg);

                if (unquotedCommandLineArg.Length > 0)
                {
                    // response file switch starts with @
                    if (unquotedCommandLineArg.StartsWith("@", StringComparison.Ordinal))
                    {
                        GatherResponseFileSwitch(unquotedCommandLineArg, commandLineSwitches);
                    }
                    else
                    {
                        string switchName;
                        string switchParameters;

                        // all switches should start with - or / unless a project is being specified
                        if (!unquotedCommandLineArg.StartsWith("-", StringComparison.Ordinal) && !unquotedCommandLineArg.StartsWith("/", StringComparison.Ordinal))
                        {
                            switchName = null;
                            // add a (fake) parameter indicator for later parsing
                            switchParameters = ":" + commandLineArg;
                        }
                        else
                        {
                            // check if switch has parameters (look for the : parameter indicator)
                            int switchParameterIndicator = unquotedCommandLineArg.IndexOf(':');

                            // extract the switch name and parameters -- the name is sandwiched between the switch indicator (the
                            // leading - or /) and the parameter indicator (if the switch has parameters); the parameters (if any)
                            // follow the parameter indicator
                            if (switchParameterIndicator == -1)
                            {
                                switchName = unquotedCommandLineArg.Substring(1);
                                switchParameters = String.Empty;
                            }
                            else
                            {
                                switchName = unquotedCommandLineArg.Substring(1, switchParameterIndicator - 1);
                                switchParameters = ExtractSwitchParameters(commandLineArg, unquotedCommandLineArg, doubleQuotesRemovedFromArg, switchName, switchParameterIndicator);
                            }
                        }

                        CommandLineSwitches.ParameterlessSwitch parameterlessSwitch;
                        CommandLineSwitches.ParameterizedSwitch parameterizedSwitch;
                        string duplicateSwitchErrorMessage;
                        bool multipleParametersAllowed;
                        string missingParametersErrorMessage;
                        bool unquoteParameters;

                        // Special case: for the switch "/m" or "/maxCpuCount" we wish to pretend we saw "/m:<number of cpus>"
                        // This allows a subsequent /m:n on the command line to override it.
                        // We could create a new kind of switch with optional parameters, but it's a great deal of churn for this single case. 
                        // Note that if no "/m" or "/maxCpuCount" switch -- either with or without parameters -- is present, then we still default to 1 cpu
                        // for backwards compatibility.
                        if (String.IsNullOrEmpty(switchParameters))
                        {
                            if (String.Equals(switchName, "m", StringComparison.OrdinalIgnoreCase) ||
                                String.Equals(switchName, "maxcpucount", StringComparison.OrdinalIgnoreCase))
                            {
                                int numberOfCpus = Environment.ProcessorCount;
                                switchParameters = ":" + numberOfCpus;
                            }
                        }

                        if (CommandLineSwitches.IsParameterlessSwitch(switchName, out parameterlessSwitch, out duplicateSwitchErrorMessage))
                        {
                            GatherParameterlessCommandLineSwitch(commandLineSwitches, parameterlessSwitch, switchParameters, duplicateSwitchErrorMessage, unquotedCommandLineArg);
                        }
                        else if (CommandLineSwitches.IsParameterizedSwitch(switchName, out parameterizedSwitch, out duplicateSwitchErrorMessage, out multipleParametersAllowed, out missingParametersErrorMessage, out unquoteParameters))
                        {
                            GatherParameterizedCommandLineSwitch(commandLineSwitches, parameterizedSwitch, switchParameters, duplicateSwitchErrorMessage, multipleParametersAllowed, missingParametersErrorMessage, unquoteParameters, unquotedCommandLineArg);
                        }
                        else
                        {
                            commandLineSwitches.SetUnknownSwitchError(unquotedCommandLineArg);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Extracts a switch's parameters after processing all quoting around the switch.
        /// </summary>
        /// <remarks>
        /// This method is marked "internal" for unit-testing purposes only -- ideally it should be "private".
        /// </remarks>
        /// <param name="commandLineArg"></param>
        /// <param name="unquotedCommandLineArg"></param>
        /// <param name="doubleQuotesRemovedFromArg"></param>
        /// <param name="switchName"></param>
        /// <param name="switchParameterIndicator"></param>
        /// <returns>The given switch's parameters (with interesting quoting preserved).</returns>
        internal static string ExtractSwitchParameters
        (
            string commandLineArg,
            string unquotedCommandLineArg,
            int doubleQuotesRemovedFromArg,
            string switchName,
            int switchParameterIndicator
        )
        {
            string switchParameters = null;

            // find the parameter indicator again using the quoted arg
            // NOTE: since the parameter indicator cannot be part of a switch name, quoting around it is not relevant, because a
            // parameter indicator cannot be escaped or made into a literal
            int quotedSwitchParameterIndicator = commandLineArg.IndexOf(':');

            // check if there is any quoting in the name portion of the switch
            int doubleQuotesRemovedFromSwitchIndicatorAndName;
            string unquotedSwitchIndicatorAndName = QuotingUtilities.Unquote(commandLineArg.Substring(0, quotedSwitchParameterIndicator), out doubleQuotesRemovedFromSwitchIndicatorAndName);

            ErrorUtilities.VerifyThrow(switchName == unquotedSwitchIndicatorAndName.Substring(1),
                "The switch name extracted from either the partially or completely unquoted arg should be the same.");

            ErrorUtilities.VerifyThrow(doubleQuotesRemovedFromArg >= doubleQuotesRemovedFromSwitchIndicatorAndName,
                "The name portion of the switch cannot contain more quoting than the arg itself.");

            // if quoting in the name portion of the switch was terminated
            if ((doubleQuotesRemovedFromSwitchIndicatorAndName % 2) == 0)
            {
                // get the parameters exactly as specified on the command line i.e. including quoting
                switchParameters = commandLineArg.Substring(quotedSwitchParameterIndicator);
            }
            else
            {
                // if quoting was not terminated in the name portion of the switch, and the terminal double-quote (if any)
                // terminates the switch parameters
                int terminalDoubleQuote = commandLineArg.IndexOf('"', quotedSwitchParameterIndicator + 1);
                if (((doubleQuotesRemovedFromArg - doubleQuotesRemovedFromSwitchIndicatorAndName) <= 1) &&
                    ((terminalDoubleQuote == -1) || (terminalDoubleQuote == (commandLineArg.Length - 1))))
                {
                    // then the parameters are not quoted in any interesting way, so use the unquoted parameters
                    switchParameters = unquotedCommandLineArg.Substring(switchParameterIndicator);
                }
                else
                {
                    // otherwise, use the quoted parameters, after compensating for the quoting that was started in the name
                    // portion of the switch
                    switchParameters = ":\"" + commandLineArg.Substring(quotedSwitchParameterIndicator + 1);
                }
            }

            ErrorUtilities.VerifyThrow(switchParameters != null, "We must be able to extract the switch parameters.");

            return switchParameters;
        }

        /// <summary>
        /// Used to keep track of response files to prevent them from
        /// being included multiple times (or even recursively).
        /// </summary>
        private static ArrayList s_includedResponseFiles;

        /// <summary>
        /// Called when a response file switch is detected on the command line. It loads the specified response file, and parses
        /// each line in it like a command line. It also prevents multiple (or recursive) inclusions of the same response file.
        /// </summary>
        /// <param name="unquotedCommandLineArg"></param>
        /// <param name="commandLineSwitches"></param>
        private static void GatherResponseFileSwitch(string unquotedCommandLineArg, CommandLineSwitches commandLineSwitches)
        {
            try
            {
                string responseFile = unquotedCommandLineArg.Substring(1);

                if (responseFile.Length == 0)
                {
                    commandLineSwitches.SetSwitchError("MissingResponseFileError", unquotedCommandLineArg);
                }
                else if (!File.Exists(responseFile))
                {
                    commandLineSwitches.SetParameterError("ResponseFileNotFoundError", unquotedCommandLineArg);
                }
                else
                {
                    // normalize the response file path to help catch multiple (or recursive) inclusions
                    responseFile = Path.GetFullPath(responseFile);
                    // NOTE: for network paths or mapped paths, normalization is not guaranteed to work

                    bool isRepeatedResponseFile = false;

                    foreach (string includedResponseFile in s_includedResponseFiles)
                    {
                        if (String.Compare(responseFile, includedResponseFile, StringComparison.OrdinalIgnoreCase) == 0)
                        {
                            commandLineSwitches.SetParameterError("RepeatedResponseFileError", unquotedCommandLineArg);
                            isRepeatedResponseFile = true;
                            break;
                        }
                    }

                    if (!isRepeatedResponseFile)
                    {
                        s_includedResponseFiles.Add(responseFile);

                        ArrayList argsFromResponseFile;

                        using (StreamReader responseFileContents = new StreamReader(responseFile, Encoding.Default)) // HIGHCHAR: If response files have no byte-order marks, then assume ANSI rather than ASCII.
                        {
                            argsFromResponseFile = new ArrayList();

                            while (responseFileContents.Peek() != -1)
                            {
                                // ignore leading whitespace on each line
                                string responseFileLine = responseFileContents.ReadLine().TrimStart();

                                // skip comment lines beginning with #
                                if (!responseFileLine.StartsWith("#", StringComparison.Ordinal))
                                {
                                    // treat each line of the response file like a command line i.e. args separated by whitespace
                                    argsFromResponseFile.AddRange(QuotingUtilities.SplitUnquoted(Environment.ExpandEnvironmentVariables(responseFileLine)));
                                }
                            }
                        }

                        GatherCommandLineSwitches(argsFromResponseFile, commandLineSwitches);
                    }
                }
            }
            catch (NotSupportedException e)
            {
                commandLineSwitches.SetParameterError("ReadResponseFileError", unquotedCommandLineArg, e);
            }
            catch (SecurityException e)
            {
                commandLineSwitches.SetParameterError("ReadResponseFileError", unquotedCommandLineArg, e);
            }
            catch (UnauthorizedAccessException e)
            {
                commandLineSwitches.SetParameterError("ReadResponseFileError", unquotedCommandLineArg, e);
            }
            catch (IOException e)
            {
                commandLineSwitches.SetParameterError("ReadResponseFileError", unquotedCommandLineArg, e);
            }
        }

        /// <summary>
        /// Called when a switch that doesn't take parameters is detected on the command line.
        /// </summary>
        /// <param name="commandLineSwitches"></param>
        /// <param name="parameterlessSwitch"></param>
        /// <param name="switchParameters"></param>
        /// <param name="duplicateSwitchErrorMessage"></param>
        /// <param name="unquotedCommandLineArg"></param>
        private static void GatherParameterlessCommandLineSwitch
        (
            CommandLineSwitches commandLineSwitches,
            CommandLineSwitches.ParameterlessSwitch parameterlessSwitch,
            string switchParameters,
            string duplicateSwitchErrorMessage,
            string unquotedCommandLineArg
        )
        {
            // switch should not have any parameters
            if (switchParameters.Length == 0)
            {
                // check if switch is duplicated, and if that's allowed
                if (!commandLineSwitches.IsParameterlessSwitchSet(parameterlessSwitch) ||
                    (duplicateSwitchErrorMessage == null))
                {
                    commandLineSwitches.SetParameterlessSwitch(parameterlessSwitch, unquotedCommandLineArg);
                }
                else
                {
                    commandLineSwitches.SetSwitchError(duplicateSwitchErrorMessage, unquotedCommandLineArg);
                }
            }
            else
            {
                commandLineSwitches.SetUnexpectedParametersError(unquotedCommandLineArg);
            }
        }

        /// <summary>
        /// Called when a switch that takes parameters is detected on the command line. This method flags errors and stores the
        /// switch parameters.
        /// </summary>
        /// <param name="commandLineSwitches"></param>
        /// <param name="parameterizedSwitch"></param>
        /// <param name="switchParameters"></param>
        /// <param name="duplicateSwitchErrorMessage"></param>
        /// <param name="multipleParametersAllowed"></param>
        /// <param name="missingParametersErrorMessage"></param>
        /// <param name="unquoteParameters"></param>
        /// <param name="unquotedCommandLineArg"></param>
        private static void GatherParameterizedCommandLineSwitch
        (
            CommandLineSwitches commandLineSwitches,
            CommandLineSwitches.ParameterizedSwitch parameterizedSwitch,
            string switchParameters,
            string duplicateSwitchErrorMessage,
            bool multipleParametersAllowed,
            string missingParametersErrorMessage,
            bool unquoteParameters,
            string unquotedCommandLineArg
        )
        {
            if (// switch must have parameters
                (switchParameters.Length > 1) ||
                // unless the parameters are optional
                (missingParametersErrorMessage == null))
            {
                // check if switch is duplicated, and if that's allowed
                if (!commandLineSwitches.IsParameterizedSwitchSet(parameterizedSwitch) ||
                    (duplicateSwitchErrorMessage == null))
                {
                    // skip the parameter indicator (if any)
                    if (switchParameters.Length > 0)
                    {
                        switchParameters = switchParameters.Substring(1);
                    }

                    // save the parameters after unquoting and splitting them if necessary
                    if (!commandLineSwitches.SetParameterizedSwitch(parameterizedSwitch, unquotedCommandLineArg, switchParameters, multipleParametersAllowed, unquoteParameters))
                    {
                        // if parsing revealed there were no real parameters, flag an error, unless the parameters are optional
                        if (missingParametersErrorMessage != null)
                        {
                            commandLineSwitches.SetSwitchError(missingParametersErrorMessage, unquotedCommandLineArg);
                        }
                    }
                }
                else
                {
                    commandLineSwitches.SetSwitchError(duplicateSwitchErrorMessage, unquotedCommandLineArg);
                }
            }
            else
            {
                commandLineSwitches.SetSwitchError(missingParametersErrorMessage, unquotedCommandLineArg);
            }
        }

        /// <summary>
        /// The name of the auto-response file.
        /// </summary>
        private const string autoResponseFileName = "MSBuild.rsp";

        /// <summary>
        /// Whether switches from the auto-response file are being used.
        /// </summary>
        internal static bool usingSwitchesFromAutoResponseFile = false;

        /// <summary>
        /// Parses the auto-response file (assumes the "/noautoresponse" switch is not specified on the command line), and combines the
        /// switches from the auto-response file with the switches passed in.
        /// Returns true if the response file was found.
        /// </summary>
        private static bool GatherAutoResponseFileSwitches(string path, CommandLineSwitches switchesFromAutoResponseFile)
        {
            string autoResponseFile = Path.Combine(path, autoResponseFileName);
            bool found = false;

            // if the auto-response file does not exist, only use the switches on the command line
            if (File.Exists(autoResponseFile))
            {
                found = true;
                GatherResponseFileSwitch("@" + autoResponseFile, switchesFromAutoResponseFile);

                // if the "/noautoresponse" switch was set in the auto-response file, flag an error
                if (switchesFromAutoResponseFile[CommandLineSwitches.ParameterlessSwitch.NoAutoResponse])
                {
                    switchesFromAutoResponseFile.SetSwitchError("CannotAutoDisableAutoResponseFile",
                        switchesFromAutoResponseFile.GetParameterlessSwitchCommandLineArg(CommandLineSwitches.ParameterlessSwitch.NoAutoResponse));
                }

                if (switchesFromAutoResponseFile.HaveAnySwitchesBeenSet())
                {
                    // we picked up some switches from the auto-response file
                    usingSwitchesFromAutoResponseFile = true;
                }
            }

            return found;
        }

        /// <summary>
        /// Coordinates the processing of all detected switches. It gathers information necessary to invoke the build engine, and
        /// performs deeper error checking on the switches and their parameters.
        /// </summary>
        /// <returns>true, if build can be invoked</returns>
        private static bool ProcessCommandLineSwitches
        (
            CommandLineSwitches switchesFromAutoResponseFile,
            CommandLineSwitches switchesNotFromAutoResponseFile,
            ref string projectFile,
            ref string[] targets,
            ref string toolsVersion,
            ref Dictionary<string, string> globalProperties,
            ref ILogger[] loggers,
            ref LoggerVerbosity verbosity,
            ref List<DistributedLoggerRecord> distributedLoggerRecords,
            ref bool needToValidateProject,
            ref string schemaFile,
            ref int cpuCount,
            ref bool enableNodeReuse,
            ref TextWriter preprocessWriter,
            ref bool debugger,
            ref bool detailedSummary,
            bool recursing
        )
        {
            bool invokeBuild = false;

            // combine the auto-response file switches with the command line switches in a left-to-right manner, where the
            // auto-response file switches are on the left (default options), and the command line switches are on the
            // right (overriding options) so that we consume switches in the following sequence of increasing priority:
            // (1) switches from the msbuild.rsp file/s, including recursively included response files
            // (2) switches from the command line, including recursively included response file switches inserted at the point they are declared with their "@" symbol
            CommandLineSwitches commandLineSwitches = new CommandLineSwitches();
            commandLineSwitches.Append(switchesFromAutoResponseFile);    // lowest precedence
            commandLineSwitches.Append(switchesNotFromAutoResponseFile);

            // show copyright message if nologo switch is not set
            // NOTE: we heed the nologo switch even if there are switch errors
            if (!recursing && !commandLineSwitches[CommandLineSwitches.ParameterlessSwitch.NoLogo] && !commandLineSwitches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Preprocess))
            {
                DisplayCopyrightMessage();
            }

            // if help switch is set (regardless of switch errors), show the help message and ignore the other switches
            if (commandLineSwitches[CommandLineSwitches.ParameterlessSwitch.Help])
            {
                ShowHelpMessage();
            }
            else if (commandLineSwitches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.NodeMode))
            {
                StartLocalNode(commandLineSwitches);
            }
            else
            {
                // if help switch is not set, and errors were found, abort (don't process the remaining switches)
                commandLineSwitches.ThrowErrors();

                // if version switch is set, just show the version and quit (ignore the other switches)
                if (commandLineSwitches[CommandLineSwitches.ParameterlessSwitch.Version])
                {
                    ShowVersion();
                }
                else
                {
                    // figure out what project we are building
                    projectFile = ProcessProjectSwitch(commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.Project], commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.IgnoreProjectExtensions], Directory.GetFiles);

                    if (!recursing && !commandLineSwitches[CommandLineSwitches.ParameterlessSwitch.NoAutoResponse])
                    {
                        // gather any switches from an msbuild.rsp that is next to the project or solution file itself
                        string projectDirectory = Path.GetDirectoryName(Path.GetFullPath(projectFile));

                        bool found = false;

                        // Don't look for more response files if it's only in the same place we already looked (next to the exe)
                        if (!String.Equals(projectDirectory, s_exePath, StringComparison.OrdinalIgnoreCase))
                        {
                            // this combines any found, with higher precedence, with the switches from the original auto response file switches
                            found = GatherAutoResponseFileSwitches(projectDirectory, switchesFromAutoResponseFile);
                        }

                        if (found)
                        {
                            // we presumably read in more switches, so start our switch processing all over again,
                            // so that we consume switches in the following sequence of increasing priority:
                            // (1) switches from the msbuild.rsp next to msbuild.exe, including recursively included response files
                            // (2) switches from this msbuild.rsp next to the project or solution <<--------- these we have just now merged with (1)
                            // (3) switches from the command line, including recursively included response file switches inserted at the point they are declared with their "@" symbol
                            return ProcessCommandLineSwitches(
                                                               switchesFromAutoResponseFile,
                                                               switchesNotFromAutoResponseFile,
                                                               ref projectFile,
                                                               ref targets,
                                                               ref toolsVersion,
                                                               ref globalProperties,
                                                               ref loggers,
                                                               ref verbosity,
                                                               ref distributedLoggerRecords,
                                                               ref needToValidateProject,
                                                               ref schemaFile,
                                                               ref cpuCount,
                                                               ref enableNodeReuse,
                                                               ref preprocessWriter,
                                                               ref debugger,
                                                               ref detailedSummary,
                                                               recursing: true
                                                             );
                        }
                    }

                    // figure out which targets we are building
                    targets = ProcessTargetSwitch(commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.Target]);

                    // figure out which ToolsVersion has been set on the command line
                    toolsVersion = ProcessToolsVersionSwitch(commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.ToolsVersion]);

                    // figure out which properties have been set on the command line
                    globalProperties = ProcessPropertySwitch(commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.Property]);

                    // figure out if there was a max cpu count provided
                    cpuCount = ProcessMaxCPUCountSwitch(commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.MaxCPUCount]);

                    // figure out if we shold reuse nodes
                    enableNodeReuse = ProcessNodeReuseSwitch(commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.NodeReuse]);

                    // determine what if any writer to preprocess to
                    preprocessWriter = null;
                    if (commandLineSwitches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Preprocess))
                    {
                        preprocessWriter = ProcessPreprocessSwitch(commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.Preprocess]);
                    }

#if FEATURE_MSBUILD_DEBUGGER
                    debugger = commandLineSwitches.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.Debugger);
#endif
                    detailedSummary = commandLineSwitches.IsParameterlessSwitchSet(CommandLineSwitches.ParameterlessSwitch.DetailedSummary);

                    // figure out which loggers are going to listen to build events
                    string[][] groupedFileLoggerParameters = commandLineSwitches.GetFileLoggerParameters();

                    loggers = ProcessLoggingSwitches(
                        commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.Logger],
                        commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.DistributedLogger],
                        commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.Verbosity],
                        commandLineSwitches[CommandLineSwitches.ParameterlessSwitch.NoConsoleLogger],
                        commandLineSwitches[CommandLineSwitches.ParameterlessSwitch.DistributedFileLogger],
                        commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.FileLoggerParameters], // used by DistributedFileLogger
                        commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.ConsoleLoggerParameters],
                        groupedFileLoggerParameters,
                        out distributedLoggerRecords,
                        out verbosity,
                        ref detailedSummary,
                        cpuCount
                        );

                    // If we picked up switches from the autoreponse file, let the user know. This could be a useful
                    // hint to a user that does not know that we are picking up the file automatically.
                    // Since this is going to happen often in normal use, only log it in high verbosity mode.
                    // Also, only log it to the console; logging to loggers would involve increasing the public API of
                    // the Engine, and we don't want to do that.
                    if (usingSwitchesFromAutoResponseFile && LoggerVerbosity.Diagnostic == verbosity)
                    {
                        Console.WriteLine(ResourceUtilities.FormatResourceString("PickedUpSwitchesFromAutoResponse", autoResponseFileName));
                    }

                    if (verbosity == LoggerVerbosity.Diagnostic)
                    {
                        string equivalentCommandLine = commandLineSwitches.GetEquivalentCommandLineExceptProjectFile();
                        Console.WriteLine(Path.Combine(s_exePath, s_exeName) + " " + equivalentCommandLine + " " + projectFile);
                    }

                    // figure out if the project needs to be validated against a schema
                    needToValidateProject = commandLineSwitches.IsParameterizedSwitchSet(CommandLineSwitches.ParameterizedSwitch.Validate);
                    schemaFile = ProcessValidateSwitch(commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.Validate]);
                    invokeBuild = true;
                }
            }

            ErrorUtilities.VerifyThrow(!invokeBuild || !String.IsNullOrEmpty(projectFile), "We should have a project file if we're going to build.");

            return invokeBuild;
        }

        /// <summary>
        /// Processes the node reuse switch, the user can set node reuse to true, false or not set the switch. If the switch is
        /// not set the system will check to see if the process is being run as an administrator. This check in localnode provider
        /// will determine the node reuse setting for that case.
        /// </summary>
        internal static bool ProcessNodeReuseSwitch(string[] parameters)
        {
            bool enableNodeReuse = true;

            if (Environment.GetEnvironmentVariable("MSBUILDDISABLENODEREUSE") == "1") // For example to disable node reuse in a gated checkin, without using the flag
            {
                enableNodeReuse = false;
            }

            if (parameters.Length > 0)
            {
                try
                {
                    // There does not seem to be a localizable function for this
                    enableNodeReuse = bool.Parse(parameters[parameters.Length - 1]);
                }
                catch (FormatException ex)
                {
                    CommandLineSwitchException.Throw("InvalidNodeReuseValue", parameters[parameters.Length - 1], ex.Message);
                }
                catch (ArgumentNullException ex)
                {
                    CommandLineSwitchException.Throw("InvalidNodeReuseValue", parameters[parameters.Length - 1], ex.Message);
                }
            }

            return enableNodeReuse;
        }

        /// <summary>
        /// Figure out what TextWriter we should preprocess the project file to.
        /// If no parameter is provided to the switch, the default is to output to the console.
        /// </summary>
        internal static TextWriter ProcessPreprocessSwitch(string[] parameters)
        {
            TextWriter writer = Console.Out;

            if (parameters.Length > 0)
            {
                try
                {
                    writer = new StreamWriter(parameters[parameters.Length - 1]);
                }
                catch (Exception ex) when (ExceptionHandling.IsIoRelatedException(ex))
                {
                    CommandLineSwitchException.Throw("InvalidPreprocessPath", parameters[parameters.Length - 1], ex.Message);
                }
            }

            return writer;
        }

        /// <summary>
        /// Uses the input from thinNodeMode switch to start a local node server
        /// </summary>
        /// <param name="commandLineSwitches"></param>
        private static void StartLocalNode(CommandLineSwitches commandLineSwitches)
        {
            string[] input = commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.NodeMode];
            int nodeModeNumber = 0;

            if (input.Length > 0)
            {
                try
                {
                    nodeModeNumber = int.Parse(input[0], CultureInfo.InvariantCulture);
                }
                catch (FormatException ex)
                {
                    CommandLineSwitchException.Throw("InvalidNodeNumberValue", input[0], ex.Message);
                }
                catch (OverflowException ex)
                {
                    CommandLineSwitchException.Throw("InvalidNodeNumberValue", input[0], ex.Message);
                }

                CommandLineSwitchException.VerifyThrow(nodeModeNumber >= 0, "InvalidNodeNumberValueIsNegative", input[0]);
            }

#if !STANDALONEBUILD
            if (!commandLineSwitches[CommandLineSwitches.ParameterlessSwitch.OldOM])
#endif
            {
                bool restart = true;
                while (restart)
                {
                    Exception nodeException = null;
                    NodeEngineShutdownReason shutdownReason = NodeEngineShutdownReason.Error;
                    // normal OOP node case
                    if (nodeModeNumber == 1)
                    {
                        OutOfProcNode node = new OutOfProcNode();
                        shutdownReason = node.Run(ProcessNodeReuseSwitch(commandLineSwitches[CommandLineSwitches.ParameterizedSwitch.NodeReuse]), out nodeException);
                        FileUtilities.ClearCacheDirectory();
                    }
                    else if (nodeModeNumber == 2)
                    {
                        OutOfProcTaskHostNode node = new OutOfProcTaskHostNode();
                        shutdownReason = node.Run(out nodeException);
                    }
                    else
                    {
                        CommandLineSwitchException.Throw("InvalidNodeNumberValue", input[0]);
                    }


                    if (shutdownReason == NodeEngineShutdownReason.Error)
                    {
                        Debug.WriteLine("An error has happened, throwing an exception");
                        throw nodeException;
                    }

                    if (shutdownReason != NodeEngineShutdownReason.BuildCompleteReuse)
                    {
                        restart = false;
                    }
                }
            }
#if !STANDALONEBUILD
            else
            {
                StartLocalNodeOldOM(nodeModeNumber);
            }
#endif
        }

#if !STANDALONEBUILD
        /// <summary>
        /// Start an old-OM local node
        /// </summary>
        /// <remarks>
        /// #############################################################################################
        /// #### Segregated into another method to avoid loading the old Engine in the regular case. ####
        /// #### Do not move back in to the main code path! #############################################
        /// #############################################################################################
        ///  We have marked this method as NoInlining because we do not want Microsoft.Build.Engine.dll to be loaded unless we really execute this code path
        /// </remarks>
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void StartLocalNodeOldOM(int nodeNumber)
        {
            Microsoft.Build.BuildEngine.LocalNode.StartLocalNodeServer(nodeNumber);
        }
#endif

        /// <summary>
        /// Process the /m: switch giving the CPU count
        /// </summary>
        /// <remarks>
        /// Internal for unit testing only
        /// </remarks>
        internal static int ProcessMaxCPUCountSwitch(string[] parameters)
        {
            int cpuCount = 1;

            if (parameters.Length > 0)
            {
                try
                {
                    cpuCount = int.Parse(parameters[parameters.Length - 1], CultureInfo.InvariantCulture);
                }
                catch (FormatException ex)
                {
                    CommandLineSwitchException.Throw("InvalidMaxCPUCountValue", parameters[parameters.Length - 1], ex.Message);
                }
                catch (OverflowException ex)
                {
                    CommandLineSwitchException.Throw("InvalidMaxCPUCountValue", parameters[parameters.Length - 1], ex.Message);
                }

                CommandLineSwitchException.VerifyThrow(cpuCount > 0 && cpuCount <= 1024, "InvalidMaxCPUCountValueOutsideRange", parameters[parameters.Length - 1]);
            }

            return cpuCount;
        }

        /// <summary>
        /// Figures out what project to build.
        /// Throws if it cannot figure it out.
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns>The project filename/path.</returns>
        /// Internal for testing purposes
        internal static string ProcessProjectSwitch
                               (
                                 string[] parameters,
                                 string[] projectsExtensionsToIgnore,
                                 DirectoryGetFiles getFiles
                               )
        {
            ErrorUtilities.VerifyThrow(parameters.Length <= 1, "It should not be possible to specify more than 1 project at a time.");
            string projectFile = null;
            // We need to look in the current directory for a project file...
            if (parameters.Length == 0)
            {
                // Get all files in the current directory that have a proj-like extension
                string[] potentialProjectFiles = getFiles(".", "*.*proj");
                // Get all files in the current directory that have a sln extension
                string[] potentialSolutionFiles = getFiles(".", "*.sln");

                List<string> extensionsToIgnore = new List<string>();
                if (projectsExtensionsToIgnore != null)
                {
                    extensionsToIgnore.AddRange(projectsExtensionsToIgnore);
                }

                if (potentialProjectFiles != null)
                {
                    foreach (string s in potentialProjectFiles)
                    {
                        if (s.EndsWith("~", true, CultureInfo.CurrentCulture))
                        {
                            extensionsToIgnore.Add(Path.GetExtension(s));
                        }
                    }
                }


                if (potentialSolutionFiles != null)
                {
                    foreach (string s in potentialSolutionFiles)
                    {
                        if (!FileUtilities.IsSolutionFilename(s))
                        {
                            extensionsToIgnore.Add(Path.GetExtension(s));
                        }
                    }
                }

                Dictionary<string, object> extensionsToIgnoreDictionary = ValidateExtensions(extensionsToIgnore.ToArray());

                // Remove projects that are in the projectExtensionsToIgnore List
                // If we have no extensions to ignore we can skip removing any extensions
                if (extensionsToIgnoreDictionary.Count > 0)
                {
                    // No point removing extensions if we have no project files
                    if (potentialProjectFiles != null && potentialProjectFiles.Length > 0)
                    {
                        potentialProjectFiles = RemoveFilesWithExtensionsToIgnore(potentialProjectFiles, extensionsToIgnoreDictionary);
                    }

                    // No point removing extensions if we have no solutions
                    if (potentialSolutionFiles != null && potentialSolutionFiles.Length > 0)
                    {
                        potentialSolutionFiles = RemoveFilesWithExtensionsToIgnore(potentialSolutionFiles, extensionsToIgnoreDictionary);
                    }
                }
                // If there is exactly 1 project file and exactly 1 solution file
                if ((potentialProjectFiles.Length == 1) && (potentialSolutionFiles.Length == 1))
                {
                    // Grab the name of both project and solution without extensions
                    string solutionName = Path.GetFileNameWithoutExtension(potentialSolutionFiles[0]);
                    string projectName = Path.GetFileNameWithoutExtension(potentialProjectFiles[0]);
                    // Compare the names and error if they are not identical
                    InitializationException.VerifyThrow(String.Compare(solutionName, projectName, StringComparison.OrdinalIgnoreCase) == 0, "AmbiguousProjectError");
                }
                // If there is more than one solution file in the current directory we have no idea which one to use
                else if (potentialSolutionFiles.Length > 1)
                {
                    InitializationException.VerifyThrow(false, "AmbiguousProjectError");
                }
                // If there is more than one project file in the current directory we may be able to figure it out
                else if (potentialProjectFiles.Length > 1)
                {
                    // We have more than one project, it is ambiguous at the moment
                    bool isAmbiguousProject = true;

                    // If there are exactly two projects and one of them is a .proj use that one and ignore the other
                    if (potentialProjectFiles.Length == 2)
                    {
                        string firstPotentialProjectExtension = Path.GetExtension(potentialProjectFiles[0]);
                        string secondPotentialProjectExtension = Path.GetExtension(potentialProjectFiles[1]);

                        // If the two projects have the same extension we can't decide which one to pick
                        if (String.Compare(firstPotentialProjectExtension, secondPotentialProjectExtension, StringComparison.OrdinalIgnoreCase) != 0)
                        {
                            // Check to see if the first project is the proj, if it is use it
                            if (String.Compare(firstPotentialProjectExtension, ".proj", StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                potentialProjectFiles = new string[] { potentialProjectFiles[0] };
                                // We have made a decision
                                isAmbiguousProject = false;
                            }
                            // If the first project is not the proj check to see if the second one is the proj, if so use it
                            else if (String.Compare(secondPotentialProjectExtension, ".proj", StringComparison.OrdinalIgnoreCase) == 0)
                            {
                                potentialProjectFiles = new string[] { potentialProjectFiles[1] };
                                // We have made a decision
                                isAmbiguousProject = false;
                            }
                        }
                    }
                    InitializationException.VerifyThrow(!isAmbiguousProject, "AmbiguousProjectError");
                }
                // if there are no project or solution files in the directory, we can't build
                else if ((potentialProjectFiles.Length == 0) &&
                         (potentialSolutionFiles.Length == 0))
                {
                    InitializationException.VerifyThrow(false, "MissingProjectError");
                }

                // We are down to only one project or solution.
                // If only 1 solution build the solution.  If only 1 project build the project
                // If 1 solution and 1 project and they are of the same name build the solution
                projectFile = (potentialSolutionFiles.Length == 1) ? potentialSolutionFiles[0] : potentialProjectFiles[0];
            }
            else
            {
                InitializationException.VerifyThrow(File.Exists(parameters[0]), "ProjectNotFoundError", parameters[0]);
                projectFile = parameters[0];
            }

            return projectFile;
        }

        /// <summary>
        ///  This method takes in a list of file name extensions to ignore. It will then validate the extensions
        ///  to make sure they start with a period, have atleast one character after the period and do not contain
        ///  any invalid path chars or wild cards
        /// </summary>
        /// <param name="projectsExtensionsToIgnore"></param>
        /// <returns></returns>
        private static Dictionary<string, object> ValidateExtensions(string[] projectsExtensionsToIgnore)
        {
            // Dictionary to contain the list of files which match the extensions to ignore. We are using a dictionary for fast lookups of extensions to ignore
            Dictionary<string, object> extensionsToIgnoreDictionary = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            // Go through each of the extensions to ignore and add them as a key in the dictionary
            if (projectsExtensionsToIgnore != null && projectsExtensionsToIgnore.Length > 0)
            {
                string extension = null;

                foreach (string extensionToIgnore in projectsExtensionsToIgnore)
                {
                    // Use the path get extension method to figure out if there is an extension in the passed 
                    // in extension to ignore. Will return null or empty if there is no extension in the string
                    try
                    {
                        // Use GetExtension to parse the extension from the extensionToIgnore
                        extension = Path.GetExtension(extensionToIgnore);
                    }
                    catch (ArgumentException)
                    {
                        // There has been an invalid char in the extensionToIgnore
                        InitializationException.Throw("InvalidExtensionToIgnore", extensionToIgnore, null, false);
                    }

                    // if null or empty is returned that means that there was not extension able to be parsed from the string
                    InitializationException.VerifyThrow(!string.IsNullOrEmpty(extension), "InvalidExtensionToIgnore", extensionToIgnore);

                    // There has to be more than a . passed in as the extension
                    InitializationException.VerifyThrow(extension.Length >= 2, "InvalidExtensionToIgnore", extensionToIgnore);

                    // The parsed extension does not match the passed in extension, this means that there were
                    // some other chars before the last extension
                    if (string.Compare(extension, extensionToIgnore, StringComparison.OrdinalIgnoreCase) != 0)
                    {
                        InitializationException.Throw("InvalidExtensionToIgnore", extensionToIgnore, null, false);
                    }

                    // Make sure that no wild cards are in the string because for now we dont allow wild card extensions
                    if (extensionToIgnore.IndexOfAny(s_wildcards) > -1)
                    {
                        InitializationException.Throw("InvalidExtensionToIgnore", extensionToIgnore, null, false);
                    };
                    if (!extensionsToIgnoreDictionary.ContainsKey(extensionToIgnore))
                    {
                        extensionsToIgnoreDictionary.Add(extensionToIgnore, null);
                    }
                }
            }
            return extensionsToIgnoreDictionary;
        }

        /// <summary>
        /// Removes filenames from the given list whose extensions are on the list of extensions to ignore
        /// </summary>
        /// <param name="potentialProjectOrSolutionFiles">A list of project or solution file names</param>
        /// <param name="extensionsToIgnore">A list of extensions to ignore</param>
        /// <returns>Array of project or solution files names which do not have an extension to be ignored </returns>
        private static string[] RemoveFilesWithExtensionsToIgnore
                                (
                                    string[] potentialProjectOrSolutionFiles,
                                    Dictionary<string, object> extensionsToIgnoreDictionary
                                )
        {
            // If we got to this method we should have to possible projects or solutions and some extensions to ignore
            ErrorUtilities.VerifyThrow(((potentialProjectOrSolutionFiles != null) && (potentialProjectOrSolutionFiles.Length > 0)), "There should be some potential project or solution files");
            ErrorUtilities.VerifyThrow(((extensionsToIgnoreDictionary != null) && (extensionsToIgnoreDictionary.Count > 0)), "There should be some extensions to Ignore");

            List<string> filesToKeep = new List<string>();
            foreach (string projectOrSolutionFile in potentialProjectOrSolutionFiles)
            {
                string extension = Path.GetExtension(projectOrSolutionFile);
                // Check to see if the file extension of the project is in our ignore list. If not keep the file
                if (!extensionsToIgnoreDictionary.ContainsKey(extension))
                {
                    filesToKeep.Add(projectOrSolutionFile);
                }
            }
            return filesToKeep.ToArray();
        }

        /// <summary>
        /// Figures out which targets are to be built.
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns>List of target names.</returns>
        private static string[] ProcessTargetSwitch(string[] parameters)
        {
            return parameters;
        }

        /// <summary>
        /// The = sign is used to pair properties with their values on the command line.
        /// </summary>
        private static readonly char[] s_propertyValueSeparator = { '=' };

        /// <summary>
        /// This is a set of wildcard chars which can cause a file extension to be invalid 
        /// </summary>
        private static readonly char[] s_wildcards = { '*', '?' };

        /// <summary>
        /// Determines which ToolsVersion was specified on the command line.  If more than
        /// one ToolsVersion was specified, we honor only the final ToolsVersion.
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns></returns>
        private static string ProcessToolsVersionSwitch(string[] parameters)
        {
            if (parameters.Length > 0)
            {
                // We don't do any validation on the value of the ToolsVersion here, since we don't 
                // know what a valid value looks like.  The engine will take care of this later.
                return parameters[parameters.Length - 1];
            }

            return null;
        }

        /// <summary>
        /// Figures out which properties were set on the command line.
        /// Internal for unit testing.
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns>BuildProperty bag.</returns>
        internal static Dictionary<string, string> ProcessPropertySwitch(string[] parameters)
        {
            Dictionary<string, string> globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (string parameter in parameters)
            {
                // split each <prop>=<value> string into 2 pieces, breaking on the first = that is found
                string[] parameterSections = parameter.Split(s_propertyValueSeparator, 2);

                Debug.Assert((parameterSections.Length >= 1) && (parameterSections.Length <= 2),
                    "String.Split() will return at least one string, and no more than two.");

                // check that the property name is not blank, and the property has a value
                CommandLineSwitchException.VerifyThrow((parameterSections[0].Length > 0) && (parameterSections.Length == 2),
                    "InvalidPropertyError", parameter);

                // Validation of whether the property has a reserved name will occur when
                // we start to build: and it will be logged then, too.
                globalProperties[parameterSections[0]] = parameterSections[1];
            }

            return globalProperties;
        }

        /// <summary>
        /// Instantiates the loggers that are going to listen to events from this build.
        /// </summary>
        /// <returns>List of loggers.</returns>
        private static ILogger[] ProcessLoggingSwitches
        (
            string[] loggerSwitchParameters,
            string[] distributedLoggerSwitchParameters,
            string[] verbositySwitchParameters,
            bool noConsoleLogger,
            bool distributedFileLogger,
            string[] fileLoggerParameters,
            string[] consoleLoggerParameters,
            string[][] groupedFileLoggerParameters,
            out List<DistributedLoggerRecord> distributedLoggerRecords,
            out LoggerVerbosity verbosity,
            ref bool detailedSummary,
            int cpuCount
        )
        {
            // if verbosity level is not specified, use the default
            verbosity = LoggerVerbosity.Normal;

            if (verbositySwitchParameters.Length > 0)
            {
                // Read the last verbosity switch found
                verbosity = ProcessVerbositySwitch(verbositySwitchParameters[verbositySwitchParameters.Length - 1]);
            }

            ArrayList loggers = ProcessLoggerSwitch(loggerSwitchParameters, verbosity);

            // Add any loggers which have been specified on the commandline
            distributedLoggerRecords = ProcessDistributedLoggerSwitch(distributedLoggerSwitchParameters, verbosity);

            ProcessConsoleLoggerSwitch(noConsoleLogger, consoleLoggerParameters, distributedLoggerRecords, verbosity, cpuCount, loggers);

            ProcessDistributedFileLogger(distributedFileLogger, fileLoggerParameters, distributedLoggerRecords, loggers, cpuCount);

            ProcessFileLoggers(groupedFileLoggerParameters, distributedLoggerRecords, verbosity, cpuCount, loggers);

            if (verbosity == LoggerVerbosity.Diagnostic)
            {
                detailedSummary = true;
            }

            return (ILogger[])loggers.ToArray(typeof(ILogger));
        }

        /// <summary>
        /// Parameters for a particular logger may be passed in fragments that we have to aggregate: for example,
        ///   /flp:foo=bar;baz=biz /flp:boz=bez becomes "foo=bar;baz=biz;boz=bez"
        /// We are going to aggregate the LoggerParameters into one LoggerParameters string
        /// to do this we must first trim off the ; from the start and the end of the strings as 
        /// this would interfere with the use of string.Join by possibly having ;; at the beginning or end of a
        /// logger parameter
        /// </summary>
        internal static string AggregateParameters(string anyPrefixingParameter, string[] parametersToAggregate)
        {
            for (int i = 0; i < parametersToAggregate.Length; i++)
            {
                parametersToAggregate[i] = parametersToAggregate[i].Trim(';');
            }

            // Join the logger parameters into one string seperated by semicolons
            string result = anyPrefixingParameter ?? String.Empty;

            result += String.Join(";", parametersToAggregate);

            return result;
        }

        /// <summary>
        /// Add a file logger with the appropriate parameters to the loggers list for each
        /// non-empty set of file logger parameters provided.
        /// </summary>
        private static void ProcessFileLoggers(string[][] groupedFileLoggerParameters, List<DistributedLoggerRecord> distributedLoggerRecords, LoggerVerbosity verbosity, int cpuCount, ArrayList loggers)
        {
            for (int i = 0; i < groupedFileLoggerParameters.Length; i++)
            {
                // If we had no, say, "/fl5" then continue; we may have a "/fl6" and so on
                if (groupedFileLoggerParameters[i] == null) continue;

                string fileParameters = "SHOWPROJECTFILE=TRUE;";
                // Use a default log file name of "msbuild.log", "msbuild1.log", "msbuild2.log", etc; put this first on the parameter
                // list so that any supplied log file parameter will override it 
                if (i == 0)
                {
                    fileParameters += "logfile=msbuild.log;";
                }
                else
                {
                    fileParameters += "logfile=msbuild" + i + ".log;";
                }

                if (groupedFileLoggerParameters[i].Length > 0)
                {
                    // Join the file logger parameters into one string seperated by semicolons
                    fileParameters = AggregateParameters(fileParameters, groupedFileLoggerParameters[i]);
                }

                FileLogger fileLogger = new FileLogger();
                // Set to detailed by default, can be overidden by fileLoggerParameters
                LoggerVerbosity defaultFileLoggerVerbosity = LoggerVerbosity.Detailed;
                fileLogger.Verbosity = defaultFileLoggerVerbosity;

                if (cpuCount == 1)
                {
                    // We've decided to use the MP logger even in single proc mode.
                    // Switch it on here, rather than in the logger, so that other hosts that use
                    // the existing ConsoleLogger don't see the behavior change in single proc.
                    fileLogger.Parameters = "ENABLEMPLOGGING;" + fileParameters;
                    loggers.Add(fileLogger);
                }
                else
                {
                    fileLogger.Parameters = fileParameters;

                    // For performance, register this logger using the forwarding logger mechanism, rather than as an old-style
                    // central logger.
                    DistributedLoggerRecord forwardingLoggerRecord = CreateForwardingLoggerRecord(fileLogger, fileParameters, defaultFileLoggerVerbosity);
                    distributedLoggerRecords.Add(forwardingLoggerRecord);
                }
            }
        }

        /// <summary>
        /// Process the noconsole switch and attach or not attach the correct console loggers
        /// </summary>
        internal static void ProcessConsoleLoggerSwitch
        (
            bool noConsoleLogger,
            string[] consoleLoggerParameters,
            List<DistributedLoggerRecord> distributedLoggerRecords,
            LoggerVerbosity verbosity,
            int cpuCount,
            ArrayList loggers
        )
        {
            // the console logger is always active, unless specifically disabled
            if (!noConsoleLogger)
            {
                // A central logger will be created for single proc and multiproc 
                ConsoleLogger logger = new ConsoleLogger(verbosity);
                string consoleParameters = "SHOWPROJECTFILE=TRUE;";

                if ((consoleLoggerParameters != null) && (consoleLoggerParameters.Length > 0))
                {
                    consoleParameters = AggregateParameters(consoleParameters, consoleLoggerParameters);
                }

                if (cpuCount == 1)
                {
                    // We've decided to use the MP logger even in single proc mode.
                    // Switch it on here, rather than in the logger, so that other hosts that use
                    // the existing ConsoleLogger don't see the behavior change in single proc.
                    logger.Parameters = "ENABLEMPLOGGING;" + consoleParameters;
                    loggers.Add(logger);
                }
                else
                {
                    logger.Parameters = consoleParameters;

                    // For performance, register this logger using the forwarding logger mechanism, rather than as an old-style
                    // central logger.
                    DistributedLoggerRecord forwardingLoggerRecord = CreateForwardingLoggerRecord(logger, consoleParameters, verbosity);
                    distributedLoggerRecords.Add(forwardingLoggerRecord);
                }
            }
        }

        /// <summary>
        /// Returns a DistributedLoggerRecord containing this logger and a ConfigurableForwardingLogger. 
        /// Looks at the logger's parameters for any verbosity parameter in order to make sure it is setting up the ConfigurableForwardingLogger
        /// with the verbosity level that the logger will actually use.
        /// </summary>
        private static DistributedLoggerRecord CreateForwardingLoggerRecord(ILogger logger, string loggerParameters, LoggerVerbosity defaultVerbosity)
        {
            string verbosityParameter = ExtractAnyLoggerParameter(loggerParameters, "verbosity", "v");

            string verbosityValue = ExtractAnyParameterValue(verbosityParameter);

            LoggerVerbosity effectiveVerbosity = defaultVerbosity;
            if (!String.IsNullOrEmpty(verbosityValue))
            {
                effectiveVerbosity = ProcessVerbositySwitch(verbosityValue);
            }

            //Gets the currently loaded assembly in which the specified class is defined
            Assembly engineAssembly = Assembly.GetAssembly(typeof(ProjectCollection));
            string loggerClassName = "Microsoft.Build.Logging.ConfigurableForwardingLogger";
            string loggerAssemblyName = engineAssembly.GetName().FullName;
            LoggerDescription forwardingLoggerDescription = new LoggerDescription(loggerClassName, loggerAssemblyName, null, loggerParameters, effectiveVerbosity);
            DistributedLoggerRecord distributedLoggerRecord = new DistributedLoggerRecord(logger, forwardingLoggerDescription);

            return distributedLoggerRecord;
        }

        /// <summary>
        /// Process the file logger switches and attach the correct file loggers. Internal for testing
        /// </summary>
        internal static void ProcessDistributedFileLogger
        (
            bool distributedFileLogger,
            string[] fileLoggerParameters,
            List<DistributedLoggerRecord> distributedLoggerRecords,
            ArrayList loggers,
            int cpuCount
        )
        {
            if (distributedFileLogger)
            {
                string fileParameters = string.Empty;
                if ((fileLoggerParameters != null) && (fileLoggerParameters.Length > 0))
                {
                    // Join the file logger parameters into one string seperated by semicolons
                    fileParameters = AggregateParameters(null, fileLoggerParameters);
                }

                // Check to see if the logfile parameter has been set, if not set it to the current directory
                string logFileParameter = ExtractAnyLoggerParameter(fileParameters, "logfile");

                string logFileName = ExtractAnyParameterValue(logFileParameter);

                try
                {
                    // If the path is not an absolute path set the path to the current directory of the exe combined with the relative path
                    // If the string is empty then send it through as the distributed file logger WILL deal with EMPTY logfile paths
                    if (!String.IsNullOrEmpty(logFileName) && !Path.IsPathRooted(logFileName))
                    {
                        fileParameters = fileParameters.Replace(logFileParameter, "logFile=" + Path.Combine(Directory.GetCurrentDirectory(), logFileName));
                    }
                }
                catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
                {
                    throw new LoggerException(e.Message, e);
                }

                if (String.IsNullOrEmpty(logFileName))
                {
                    // If the string is not empty and it does not end in a ;, we need to add a ; to seperate what is in the parameter from the logfile
                    // if the string is empty, no ; is needed because logfile is the only parameter which will be passed in
                    if (!String.IsNullOrEmpty(fileParameters) && !fileParameters.EndsWith(";", StringComparison.OrdinalIgnoreCase))
                    {
                        fileParameters += ";";
                    }

                    fileParameters += "logFile=" + Path.Combine(Directory.GetCurrentDirectory(), msbuildLogFileName);
                }

                //Gets the currently loaded assembly in which the specified class is defined
                Assembly engineAssembly = Assembly.GetAssembly(typeof(ProjectCollection));
                string loggerClassName = "Microsoft.Build.Logging.DistributedFileLogger";
                string loggerAssemblyName = engineAssembly.GetName().FullName;
                // Node the verbosity parameter is not used by the Distributed file logger so changing it here has no effect. It must be changed in the distributed file logger
                LoggerDescription forwardingLoggerDescription = new LoggerDescription(loggerClassName, loggerAssemblyName, null, fileParameters, LoggerVerbosity.Detailed);
                // Use the null as the central Logger, this will cause the engine to instantiate the NullCentralLogger, this logger will throw an exception if anything except for the buildstarted and buildFinished events are sent
                DistributedLoggerRecord distributedLoggerRecord = new DistributedLoggerRecord(null, forwardingLoggerDescription);
                distributedLoggerRecords.Add(distributedLoggerRecord);
            }
        }

        /// <summary>
        /// Given a string of aggregated parameters, such as "foo=bar;baz;biz=boz" and a list of parameter names,
        /// such as "biz", tries to find and return the LAST matching parameter, such as "biz=boz"
        /// </summary>
        internal static string ExtractAnyLoggerParameter(string parameters, params string[] parameterNames)
        {
            string[] nameValues = parameters.Split(';');
            string result = null;

            foreach (string nameValue in nameValues)
            {
                foreach (string name in parameterNames)
                {
                    bool found = nameValue.StartsWith(name + "=", StringComparison.OrdinalIgnoreCase) ||   // Parameters with value, such as "logfile=foo.txt"
                                 String.Equals(name, nameValue, StringComparison.OrdinalIgnoreCase);       // Parameters without value, such as "append"

                    if (found)
                    {
                        result = nameValue;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Given a parameter, such as "foo=bar", tries to find and return
        /// the value part, ie "bar"; otherwise returns null.
        /// </summary>
        private static string ExtractAnyParameterValue(string parameter)
        {
            string value = null;

            if (!String.IsNullOrEmpty(parameter))
            {
                string[] nameValuePair = parameter.Split('=');

                value = (nameValuePair.Length > 1) ? nameValuePair[1] : null;
            }

            return value;
        }

        /// <summary>
        /// Figures out what verbosity level to assign to loggers.
        /// </summary>
        /// <remarks>
        /// Internal for unit testing only
        /// </remarks> 
        /// <param name="parameters"></param>
        /// <returns>The logger verbosity level.</returns>
        internal static LoggerVerbosity ProcessVerbositySwitch(string value)
        {
            LoggerVerbosity verbosity = LoggerVerbosity.Normal;

            if (String.Equals(value, "q", StringComparison.OrdinalIgnoreCase) ||
                String.Equals(value, "quiet", StringComparison.OrdinalIgnoreCase))
            {
                verbosity = LoggerVerbosity.Quiet;
            }
            else if (String.Equals(value, "m", StringComparison.OrdinalIgnoreCase) ||
                     String.Equals(value, "minimal", StringComparison.OrdinalIgnoreCase))
            {
                verbosity = LoggerVerbosity.Minimal;
            }
            else if (String.Equals(value, "n", StringComparison.OrdinalIgnoreCase) ||
                     String.Equals(value, "normal", StringComparison.OrdinalIgnoreCase))
            {
                verbosity = LoggerVerbosity.Normal;
            }
            else if (String.Equals(value, "d", StringComparison.OrdinalIgnoreCase) ||
                     String.Equals(value, "detailed", StringComparison.OrdinalIgnoreCase))
            {
                verbosity = LoggerVerbosity.Detailed;
            }
            else if (String.Equals(value, "diag", StringComparison.OrdinalIgnoreCase) ||
                     String.Equals(value, "diagnostic", StringComparison.OrdinalIgnoreCase))
            {
                verbosity = LoggerVerbosity.Diagnostic;
            }
            else
            {
                CommandLineSwitchException.Throw("InvalidVerbosityError", value);
            }

            return verbosity;
        }

        /// <summary>
        /// Figures out which additional loggers are going to listen to build events.
        /// </summary>
        /// <returns>List of loggers.</returns>
        private static ArrayList ProcessLoggerSwitch(string[] parameters, LoggerVerbosity verbosity)
        {
            ArrayList loggers = new ArrayList();

            foreach (string parameter in parameters)
            {
                string unquotedParameter = QuotingUtilities.Unquote(parameter);

                LoggerDescription loggerDescription = ParseLoggingParameter(parameter, unquotedParameter, verbosity);

                loggers.Add(CreateAndConfigureLogger(loggerDescription, verbosity, unquotedParameter));
            }

            return loggers;
        }

        /// <summary>
        /// Parses command line arguments describing the distributed loggers
        /// </summary>
        /// <returns>List of distributed logger records</returns>
        private static List<DistributedLoggerRecord> ProcessDistributedLoggerSwitch(string[] parameters, LoggerVerbosity verbosity)
        {
            List<DistributedLoggerRecord> distributedLoggers = new List<DistributedLoggerRecord>();

            foreach (string parameter in parameters)
            {
                // split each <central logger>|<node logger> string into two pieces, breaking on the first | that is found
                int emptySplits; // ignored
                ArrayList loggerSpec = QuotingUtilities.SplitUnquoted(parameter, 2, true /* keep empty splits */, false /* keep quotes */, out emptySplits, '*');

                ErrorUtilities.VerifyThrow((loggerSpec.Count >= 1) && (loggerSpec.Count <= 2),
                    "SplitUnquoted() must return at least one string, and no more than two.");

                string unquotedParameter = QuotingUtilities.Unquote((string)loggerSpec[0]);
                LoggerDescription centralLoggerDescription =
                    ParseLoggingParameter((string)loggerSpec[0], unquotedParameter, verbosity);

                ILogger centralLogger = CreateAndConfigureLogger(centralLoggerDescription, verbosity, unquotedParameter);

                // By default if no forwarding logger description is specified the same logger is used for both functions
                LoggerDescription forwardingLoggerDescription = centralLoggerDescription;

                if (loggerSpec.Count > 1)
                {
                    unquotedParameter = QuotingUtilities.Unquote((string)loggerSpec[1]);
                    forwardingLoggerDescription = ParseLoggingParameter((string)loggerSpec[1], unquotedParameter, verbosity);
                }

                DistributedLoggerRecord distributedLoggerRecord =
                    new DistributedLoggerRecord(centralLogger, forwardingLoggerDescription);

                distributedLoggers.Add(distributedLoggerRecord);
            }

            return distributedLoggers;
        }

        /// <summary>
        /// Parse a command line logger argument into a LoggerDescription structure
        /// </summary>
        /// <param name="parameter">the command line string</param>
        /// <returns></returns>
        private static LoggerDescription ParseLoggingParameter(string parameter, string unquotedParameter, LoggerVerbosity verbosity)
        {
            ArrayList loggerSpec;
            string loggerClassName;
            string loggerAssemblyName;
            string loggerAssemblyFile;
            string loggerParameters = null;

            int emptySplits; // ignored

            // split each <logger type>;<logger parameters> string into two pieces, breaking on the first ; that is found
            loggerSpec = QuotingUtilities.SplitUnquoted(parameter, 2, true /* keep empty splits */, false /* keep quotes */, out emptySplits, ';');

            ErrorUtilities.VerifyThrow((loggerSpec.Count >= 1) && (loggerSpec.Count <= 2),
                "SplitUnquoted() must return at least one string, and no more than two.");

            // check that the logger is specified
            CommandLineSwitchException.VerifyThrow(((string)loggerSpec[0]).Length > 0,
                "InvalidLoggerError", unquotedParameter);

            // extract logger parameters if present
            if (loggerSpec.Count == 2)
            {
                loggerParameters = QuotingUtilities.Unquote((string)loggerSpec[1]);
            }

            // split each <logger class>,<logger assembly> string into two pieces, breaking on the first , that is found
            ArrayList loggerTypeSpec = QuotingUtilities.SplitUnquoted((string)loggerSpec[0], 2, true /* keep empty splits */, false /* keep quotes */, out emptySplits, ',');

            ErrorUtilities.VerifyThrow((loggerTypeSpec.Count >= 1) && (loggerTypeSpec.Count <= 2),
                "SplitUnquoted() must return at least one string, and no more than two.");


            string loggerAssemblySpec;

            // if the logger class and assembly are both specified
            if (loggerTypeSpec.Count == 2)
            {
                loggerClassName = QuotingUtilities.Unquote((string)loggerTypeSpec[0]);
                loggerAssemblySpec = QuotingUtilities.Unquote((string)loggerTypeSpec[1]);
            }
            else
            {
                loggerClassName = String.Empty;
                loggerAssemblySpec = QuotingUtilities.Unquote((string)loggerTypeSpec[0]);
            }

            CommandLineSwitchException.VerifyThrow(loggerAssemblySpec.Length > 0,
                "InvalidLoggerError", unquotedParameter);

            loggerAssemblyName = null;
            loggerAssemblyFile = null;

            // DDB Bug msbuild.exe -Logger:FileLogger,Microsoft.Build.Engine fails due to moved engine file.
            // Only add strong naming if the assembly is a non-strong named 'Microsoft.Build.Engine' (i.e, no additional characteristics)
            // Concat full Strong Assembly to match v4.0
            if (String.Compare(loggerAssemblySpec, "Microsoft.Build.Engine", StringComparison.OrdinalIgnoreCase) == 0)
            {
                loggerAssemblySpec = "Microsoft.Build.Engine,Version=4.0.0.0,Culture=neutral,PublicKeyToken=b03f5f7f11d50a3a";
            }

            // figure out whether the assembly's identity (strong/weak name), or its filename/path is provided
            if (File.Exists(loggerAssemblySpec))
            {
                loggerAssemblyFile = loggerAssemblySpec;
            }
            else
            {
                loggerAssemblyName = loggerAssemblySpec;
            }

            return new LoggerDescription(loggerClassName, loggerAssemblyName, loggerAssemblyFile, loggerParameters, verbosity);
        }

        /// <summary>
        /// Loads a logger from its assembly, instantiates it, and handles errors.
        /// </summary>
        /// <returns>Instantiated logger.</returns>
        private static ILogger CreateAndConfigureLogger
        (
            LoggerDescription loggerDescription,
            LoggerVerbosity verbosity,
            string unquotedParameter
        )
        {
            ILogger logger = null;

            try
            {
                logger = loggerDescription.CreateLogger();

                if (logger == null)
                {
                    InitializationException.VerifyThrow(logger != null, "LoggerNotFoundError", unquotedParameter);
                }
            }
            catch (IOException e)
            {
                InitializationException.Throw("LoggerCreationError", unquotedParameter, e, false);
            }
            catch (BadImageFormatException e)
            {
                InitializationException.Throw("LoggerCreationError", unquotedParameter, e, false);
            }
            catch (SecurityException e)
            {
                InitializationException.Throw("LoggerCreationError", unquotedParameter, e, false);
            }
            catch (ReflectionTypeLoadException e)
            {
                InitializationException.Throw("LoggerCreationError", unquotedParameter, e, false);
            }
            catch (MemberAccessException e)
            {
                InitializationException.Throw("LoggerCreationError", unquotedParameter, e, false);
            }
            catch (TargetInvocationException e)
            {
                InitializationException.Throw("LoggerFatalError", unquotedParameter, e.InnerException, true);
            }

            // Configure the logger by setting the verbosity level and parameters
            try
            {
                // set its verbosity level
                logger.Verbosity = verbosity;

                // set the logger parameters (if any)
                if (loggerDescription.LoggerSwitchParameters != null)
                {
                    logger.Parameters = loggerDescription.LoggerSwitchParameters;
                }
            }

            catch (LoggerException)
            {
                // Logger failed politely during parameter/verbosity setting
                throw;
            }
            catch (Exception e)
            {
                InitializationException.Throw("LoggerFatalError", unquotedParameter, e, true);
            }

            return logger;
        }

        /// <summary>
        /// Figures out if the project needs to be validated against a schema.
        /// </summary>
        /// <param name="parameters"></param>
        /// <returns>The schema to validate against, or null.</returns>
        private static string ProcessValidateSwitch(string[] parameters)
        {
            string schemaFile = null;

            foreach (string parameter in parameters)
            {
                InitializationException.VerifyThrow(schemaFile == null, "MultipleSchemasError", parameter);
                InitializationException.VerifyThrow(File.Exists(parameter), "SchemaNotFoundError", parameter);

                schemaFile = Path.Combine(Directory.GetCurrentDirectory(), parameter);
            }

            return schemaFile;
        }

        /// <summary>
        /// Given an invalid ToolsVersion string and the collection of valid toolsets, 
        /// throws an InitializationException with the appropriate message.
        /// </summary>
        private static void ThrowInvalidToolsVersionInitializationException(IEnumerable<Toolset> toolsets, string toolsVersion)
        {
            string toolsVersionList = String.Empty;
            foreach (Toolset toolset in toolsets)
            {
                toolsVersionList += "\"" + toolset.ToolsVersion + "\", ";
            }

            // Remove trailing comma and space
            if (toolsVersionList.Length > 0)
            {
                toolsVersionList = toolsVersionList.Substring(0, toolsVersionList.Length - 2);
            }

            string message = ResourceUtilities.FormatResourceString
                (
                "UnrecognizedToolsVersion",
                toolsVersion,
                toolsVersionList
                );
            message = ResourceUtilities.FormatResourceString("InvalidToolsVersionError", message);

            InitializationException.Throw(message, toolsVersion);
        }

        /// <summary>
        /// Displays the application copyright message/logo.
        /// </summary>
        private static void DisplayCopyrightMessage()
        {
            Console.WriteLine(ResourceUtilities.FormatResourceString("CopyrightMessage", ProjectCollection.Version.ToString()));
        }

        /// <summary>
        /// Displays the help message that explains switch usage and syntax.
        /// </summary>
        private static void ShowHelpMessage()
        {
            // NOTE: the help message is broken into pieces because localization
            // prefers it that way -- see VSW #482758 "Entire command line help
            // message is stored in a single resource"
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_1_Syntax"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_2_Description"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_3_SwitchesHeader"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_9_TargetSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_10_PropertySwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_17_MaximumCPUSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_23_ToolsVersionSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_12_VerbositySwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_13_ConsoleLoggerParametersSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_14_NoConsoleLoggerSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_20_FileLoggerSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_22_FileLoggerParametersSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_18_DistributedLoggerSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_21_DistributedFileLoggerSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_11_LoggerSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_15_ValidateSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_19_IgnoreProjectExtensionsSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_24_NodeReuse"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_25_PreprocessSwitch"));

            Console.WriteLine(AssemblyResources.GetString("HelpMessage_26_DetailedSummarySwitch"));

            if (CommandLineSwitches.IsParameterlessSwitch("debug"))
            {
                Console.WriteLine(AssemblyResources.GetString("HelpMessage_27_DebuggerSwitch"));
            }

            Console.WriteLine(AssemblyResources.GetString("HelpMessage_7_ResponseFile"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_8_NoAutoResponseSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_5_NoLogoSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_6_VersionSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_4_HelpSwitch"));
            Console.WriteLine(AssemblyResources.GetString("HelpMessage_16_Examples"));
        }

        /// <summary>
        /// Displays a message prompting the user to look up help.
        /// </summary>
        private static void ShowHelpPrompt()
        {
            Console.WriteLine(AssemblyResources.GetString("HelpPrompt"));
        }

        /// <summary>
        /// Displays the build engine's version number.
        /// </summary>
        private static void ShowVersion()
        {
            Console.Write(ProjectCollection.Version.ToString());
        }
    }
}
