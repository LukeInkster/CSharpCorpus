﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Collections;
using System.Collections.Specialized;
using System.Collections.Generic;
using System.Text;
using System.Resources;
using System.Threading;
using System.Reflection;
using System.Diagnostics;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// The return value from InitializeHostObject.  This enumeration defines what action the ToolTask
    /// should take next, after we've tried to initialize the host object.
    /// </summary>
    public enum HostObjectInitializationStatus
    {
        /// <summary>
        /// This means that there exists an appropriate host object for this task, it can support
        /// all of the parameters passed in, and it should be invoked to do the real work of the task.
        /// </summary>
        UseHostObjectToExecute,

        /// <summary>
        /// This means that either there is no host object available, or that the host object is 
        /// not capable of supporting all of the features required for this build.  Therefore,
        /// ToolTask should fallback to an alternate means of doing the build, such as invoking
        /// the command-line tool.
        /// </summary>
        UseAlternateToolToExecute,

        /// <summary>
        /// This means that the host object is already up-to-date, and no further action is necessary.
        /// </summary>
        NoActionReturnSuccess,

        /// <summary>
        /// This means that some of the parameters being passed into the task are invalid, and the
        /// task should fail immediately.
        /// </summary>
        NoActionReturnFailure
    }

    /// <summary>
    /// Base class used for tasks that spawn an executable. This class implements the ToolPath property which can be used to
    /// override the default path.
    /// </summary>
    /// <remarks>
    /// INTERNAL WARNING: DO NOT USE the Log property in this class! Log points to resources in the task assembly itself, and 
    /// we want to use resources from Utilities. Use LogPrivate (for private Utilities resources) and LogShared (for shared MSBuild resources)
    /// </remarks>
    public abstract class ToolTask : Task, ICancelableTask
    {
        private static bool s_preserveTempFiles = String.Equals(Environment.GetEnvironmentVariable("MSBUILDPRESERVETOOLTEMPFILES"), "1", StringComparison.Ordinal);

        #region Constructors

        /// <summary>
        /// Protected constructor 
        /// </summary>
        protected ToolTask()
        {
            _logPrivate = new TaskLoggingHelper(this);
            _logPrivate.TaskResources = AssemblyResources.PrimaryResources;
            _logPrivate.HelpKeywordPrefix = "MSBuild.";

            _logShared = new TaskLoggingHelper(this);
            _logShared.TaskResources = AssemblyResources.SharedResources;
            _logShared.HelpKeywordPrefix = "MSBuild.";

            // 5 second is the default termination timeout.
            TaskProcessTerminationTimeout = 5000;
            ToolCanceled = new ManualResetEvent(false);
        }

        /// <summary>
        /// Protected constructor 
        /// </summary>
        /// <param name="taskResources">The resource manager for task resources</param>
        protected ToolTask(ResourceManager taskResources)
            : this()
        {
            this.TaskResources = taskResources;
        }

        /// <summary>
        /// Protected constructor 
        /// </summary>
        /// <param name="taskResources">The resource manager for task resources</param>
        /// <param name="helpKeywordPrefix">The help keyword prefix for task's messages</param>
        protected ToolTask(ResourceManager taskResources, string helpKeywordPrefix)
            : this(taskResources)
        {
            this.HelpKeywordPrefix = helpKeywordPrefix;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The return code of the spawned process. If the task logged any errors, but the process 
        /// had an exit code of 0 (success), this will be set to -1.
        /// </summary>
        [Output]
        public int ExitCode
        {
            get
            {
                return _exitCode;
            }
        }

        /// <summary>
        /// When set to true, this task will yield the node when its task is executing.
        /// </summary>
        public bool YieldDuringToolExecution
        {
            get;
            set;
        }

        /// <summary>
        /// When set to true, the tool task will create a batch file for the command-line and execute that using the command-processor,
        /// rather than executing the command directly.
        /// </summary>
        public bool UseCommandProcessor
        {
            get;
            set;
        }

        /// <summary>
        /// When set to true, it passes /Q to the cmd.exe command line such that the command line does not get echo-ed on stdout
        /// </summary>
        public bool EchoOff
        {
            get;
            set;
        }

        /// <summary>
        /// A timeout to wait for a task to terminate before killing it.  In milliseconds.
        /// </summary>
        protected int TaskProcessTerminationTimeout
        {
            get;
            set;
        }

        /// <summary>
        /// Used to signal when a tool has been cancelled.
        /// </summary>
        protected ManualResetEvent ToolCanceled
        {
            get;
            private set;
        }

        private int _exitCode;

        /// <summary>
        /// This is the batch file created when UseCommandProcessor is set to true.
        /// </summary>
        private string _temporaryBatchFile;

        /// <summary>
        /// Implemented by the derived class. Returns a string which is the name of the underlying .EXE to run e.g. "resgen.exe"
        /// Only used by the ToolExe getter.
        /// </summary>
        /// <value>Name of tool.</value>
        abstract protected string ToolName { get; }

        /// <summary>
        /// Projects may set this to override a task's ToolName.
        /// Tasks may override this to prevent that.
        /// </summary>
        public virtual string ToolExe
        {
            get
            {
                if (!String.IsNullOrEmpty(_toolExe))
                {
                    // If the ToolExe has been overridden then return the value
                    return _toolExe;
                }
                else
                {
                    // We have no override, so simply delegate to ToolName
                    return this.ToolName;
                }
            }
            set
            {
                _toolExe = value;
            }
        }

        /// <summary>
        /// Project-visible property allows the user to override the path to the executable.
        /// </summary>
        /// <value>Path to tool.</value>
        public string ToolPath
        {
            set { _toolPath = value; }
            get { return _toolPath; }
        }

        /// <summary>
        /// Array of equals-separated pairs of environment
        /// variables that should be passed to the spawned executable,
        /// in addition to (or selectively overriding) the regular environment block.
        /// </summary>
        /// <remarks>
        /// Using this instead of EnvironmentOverride as that takes a StringDictionary,
        /// which cannot be set from an MSBuild project.
        /// </remarks>
        public string[] EnvironmentVariables
        {
            get;
            set;
        }

        private string _toolPath;

        /// <summary>
        /// Project visible property that allows the user to specify an amount of time after which the task executable
        /// is terminated. 
        /// </summary>
        /// <value>Time-out in milliseconds. Default is <see cref="System.Threading.Timeout.Infinite"/> (no time-out).</value>
        virtual public int Timeout
        {
            set { _timeout = value; }
            get { return _timeout; }
        }

        private int _timeout = System.Threading.Timeout.Infinite;

        /// <summary>
        /// Overridable property specifying the encoding of the response file, UTF8 by default
        /// </summary>
        virtual protected Encoding ResponseFileEncoding
        {
            get { return Encoding.UTF8; }
        }

        /// <summary>
        /// Overridable method to escape content of the response file
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "string", Justification = "Shipped this way in Dev11 Beta (go-live)")]
        virtual protected string ResponseFileEscape(string responseString)
        {
            return responseString;
        }

        /// <summary>
        /// Overridable property specifying the encoding of the captured task standard output stream
        /// </summary>
        /// <remarks>
        /// Console-based output uses the current system OEM code page by default. Note that we should not use Console.OutputEncoding
        /// here since processes we run don't really have much to do with our console window (and also Console.OutputEncoding
        /// doesn't return the OEM code page if the running application that hosts MSBuild is not a console application).
        /// </remarks>
        virtual protected Encoding StandardOutputEncoding
        {
            get { return EncodingUtilities.CurrentSystemOemEncoding; }
        }

        /// <summary>
        /// Overridable property specifying the encoding of the captured task standard error stream
        /// </summary>
        /// <remarks>
        /// Console-based output uses the current system OEM code page by default. Note that we should not use Console.OutputEncoding
        /// here since processes we run don't really have much to do with our console window (and also Console.OutputEncoding
        /// doesn't return the OEM code page if the running application that hosts MSBuild is not a console application).
        /// </remarks>
        virtual protected Encoding StandardErrorEncoding
        {
            get { return EncodingUtilities.CurrentSystemOemEncoding; }
        }

        /// <summary>
        /// Gets the Path override value.
        /// </summary>
        /// <returns>The new value for the Environment for the task.</returns>
        [Obsolete("Use EnvironmentVariables property")]
        virtual protected StringDictionary EnvironmentOverride
        {
            get { return null; }
        }

        /// <summary>
        /// Importance with which to log text from the
        /// standard error stream.
        /// </summary>
        virtual protected MessageImportance StandardErrorLoggingImportance
        {
            get { return MessageImportance.Normal; }
        }

        /// <summary>
        /// Whether this ToolTask has logged any errors
        /// </summary>
        protected virtual bool HasLoggedErrors
        {
            get
            {
                return (Log.HasLoggedErrors || LogPrivate.HasLoggedErrors || LogShared.HasLoggedErrors);
            }
        }

        /// <summary>
        /// Task Parameter: Importance with which to log text from the
        /// standard out stream.
        /// </summary>
        public string StandardOutputImportance
        {
            get
            {
                return _standardOutputImportance;
            }
            set
            {
                _standardOutputImportance = value;
            }
        }

        /// <summary>
        /// Task Parameter: Importance with which to log text from the
        /// standard error stream.
        /// </summary>
        public string StandardErrorImportance
        {
            get
            {
                return _standardErrorImportance;
            }
            set
            {
                _standardErrorImportance = value;
            }
        }

        /// <summary>
        /// Should ALL messages received on the standard error stream be logged as errors.
        /// </summary>
        public bool LogStandardErrorAsError
        {
            get
            {
                return _logStandardErrorAsError;
            }

            set
            {
                _logStandardErrorAsError = value;
            }
        }

        /// <summary>
        /// Importance with which to log text from in the
        /// standard out stream.
        /// </summary>
        virtual protected MessageImportance StandardOutputLoggingImportance
        {
            get { return MessageImportance.Low; }
        }

        /// <summary>
        /// The actual importance at which standard out messages will be logged.
        /// </summary>
        protected MessageImportance StandardOutputImportanceToUse
        {
            get { return _standardOutputImportanceToUse; }
        }

        /// <summary>
        /// The actual importance at which standard error messages will be logged.
        /// </summary>
        protected MessageImportance StandardErrorImportanceToUse
        {
            get { return _standardErrorImportanceToUse; }
        }

        #endregion

        #region Private properties

        /// <summary>
        /// Gets an instance of a private TaskLoggingHelper class containing task logging methods.
        /// This is necessary because ToolTask lives in a different assembly than the task inheriting from it
        /// and needs its own separate resources.
        /// </summary>
        /// <value>The logging helper object.</value>
        private TaskLoggingHelper LogPrivate
        {
            get
            {
                return _logPrivate;
            }
        }

        // the private logging helper
        private TaskLoggingHelper _logPrivate;

        /// <summary>
        /// Gets an instance of a shared resources TaskLoggingHelper class containing task logging methods.
        /// This is necessary because ToolTask lives in a different assembly than the task inheriting from it
        /// and needs its own separate resources.
        /// </summary>
        /// <value>The logging helper object.</value>
        private TaskLoggingHelper LogShared
        {
            get
            {
                return _logShared;
            }
        }

        // the shared resources logging helper
        private TaskLoggingHelper _logShared;

        #endregion

        #region Overridable methods

        /// <summary>
        /// Gets the fully qualified tool name. Should return ToolExe if ToolTask should search for the tool 
        /// in the system path. If ToolPath is set, this is ignored.
        /// </summary>
        /// <returns>Path string.</returns>
        abstract protected string GenerateFullPathToTool();

        /// <summary>
        /// Gets the working directory to use for the process. Should return null if ToolTask should use the
        /// current directory. 
        /// </summary>
        /// <remarks>This is a method rather than a property so that derived classes (like Exec) can choose to
        /// expose a public WorkingDirectory property, and it would be confusing to have two properties.</remarks>
        /// <returns></returns>
        virtual protected string GetWorkingDirectory()
        {
            return null;
        }

        /// <summary>
        /// Implemented in the derived class
        /// </summary>
        /// <returns>true, if successful</returns>
        protected internal virtual bool ValidateParameters()
        {
            // Default is no validation. This is useful for tools that don't need validation.
            return true;
        }

        /// <summary>
        /// Returns true if task execution is not necessary. Executed after ValidateParameters
        /// </summary>
        /// <returns></returns>
        virtual protected bool SkipTaskExecution()
        {
            return false;
        }

        /// <summary>
        /// Returns a string with those switches and other information that can go into a response file.
        /// Called after ValidateParameters and SkipTaskExecution
        /// </summary>
        /// <returns></returns>
        virtual protected string GenerateResponseFileCommands()
        {
            // Default is nothing. This is useful for tools that don't need or support response files.
            return string.Empty;
        }

        /// <summary>
        /// Returns a string with those switches and other information that can't go into a response file and
        /// must go directly onto the command line.
        /// Called after ValidateParameters and SkipTaskExecution
        /// </summary>
        /// <returns></returns>
        virtual protected string GenerateCommandLineCommands()
        {
            // Default is nothing. This is useful for tools where all the parameters can go into a response file.
            return string.Empty;
        }

        /// <summary>
        /// Returns the command line switch used by the tool executable to specify the response file.
        /// Will only be called if the task returned a non empty string from GetResponseFileCommands
        /// Called after ValidateParameters, SkipTaskExecution and GetResponseFileCommands
        /// </summary>
        /// <param name="responseFilePath">full path to the temporarily created response file</param>
        /// <returns></returns>
        virtual protected string GetResponseFileSwitch(string responseFilePath)
        {
            // by default, return @"<responseFilePath>"
            return "@\"" + responseFilePath + "\"";
        }

        /// <summary>
        /// Allows tool to handle the return code.
        /// This method will only be called with non-zero exitCode.
        /// </summary>
        /// <returns>The return value of this method will be used as the task return value</returns>
        virtual protected bool HandleTaskExecutionErrors()
        {
            Debug.Assert(_exitCode != 0, "HandleTaskExecutionErrors should only be called if there were problems executing the task");

            if (HasLoggedErrors)
            {
                // Emit a message.
                LogPrivate.LogMessageFromResources(MessageImportance.Low, "General.ToolCommandFailedNoErrorCode", _exitCode);
            }
            else
            {
                // If the tool itself did not log any errors on its own, then we log one now simply saying
                // that the tool exited with a non-zero exit code.  This way, the customer nevers sees
                // "Build failed" without at least one error being logged.
                LogPrivate.LogErrorWithCodeFromResources("ToolTask.ToolCommandFailed", ToolExe, _exitCode);
            }

            // by default, always fail the task
            return false;
        }

        /// <summary>
        /// We expect the tasks to override this method, if they support host objects. The implementation should call into the
        /// host object to perform the real work of the task. For example, for compiler tasks like Csc and Vbc, this method would
        /// call Compile() on the host object.
        /// </summary>
        /// <returns>The return value indicates success (true) or failure (false) if the host object was actually called to do the work.</returns>
        virtual protected bool CallHostObjectToExecute()
        {
            return false;
        }

        /// <summary>
        /// We expect tasks to override this method if they support host objects.  The implementation should
        /// make sure that the host object is ready to perform the real work of the task.  
        /// </summary>
        /// <returns>The return value indicates what steps to take next.  The default is to assume that there
        /// is no host object provided, and therefore we should fallback to calling the command-line tool.</returns>
        virtual protected HostObjectInitializationStatus InitializeHostObject()
        {
            return HostObjectInitializationStatus.UseAlternateToolToExecute;
        }

        /// <summary>
        /// Logs the actual command line about to be executed (or what the task wants the log to show)
        /// </summary>
        /// <param name="message">
        /// Descriptive message about what is happening - usually the command line to be executed.
        /// </param>
        virtual protected void LogToolCommand
        (
            string message
        )
        {
            // Log a descriptive message about what's happening.
            LogPrivate.LogCommandLine(MessageImportance.High, message);
        }

        /// <summary>
        /// Logs the tool name and the path from where it is being run.
        /// </summary>
        /// <param name="toolName">
        /// The tool to Log. This is the actual tool being used, ie. if ToolExe has been specified it will be used, otherwise it will be ToolName
        /// </param>
        /// <param name="pathToTool">
        /// The path from where the tool is being run.
        /// </param>
        virtual protected void LogPathToTool
        (
            string toolName,
            string pathToTool
        )
        {
            // We don't do anything here any more, as it was just duplicative and noise.
            // The method only remains for backwards compatibility - to avoid breaking tasks that override it
        }

        #endregion

        #region Methods

        /// <summary>
        /// Figures out the path to the tool (including the .exe), either by using the ToolPath
        /// parameter, or by asking the derived class to tell us where it should be located.
        /// </summary>
        /// <returns>path to the tool, or null</returns>
        private string ComputePathToTool()
        {
            string pathToTool;

            if (UseCommandProcessor)
            {
                return ToolExe;
            }

            if (ToolPath != null && ToolPath.Length > 0)
            {
                // If the project author passed in a ToolPath, always use that.
                pathToTool = Path.Combine(ToolPath, ToolExe);
            }
            else
            {
                // Otherwise, try to find the tool ourselves.
                pathToTool = GenerateFullPathToTool();

                // We have no toolpath, but we have been given an override
                // for the tool exe, fix up the path, assuming that the tool is in the same location
                if (pathToTool != null && !String.IsNullOrEmpty(_toolExe))
                {
                    string directory = Path.GetDirectoryName(pathToTool);
                    pathToTool = Path.Combine(directory, ToolExe);
                }
            }

            // only look for the file if we have a path to it. If we have just the file name, we'll 
            // look for it in the path
            if (pathToTool != null)
            {
                bool isOnlyFileName = (Path.GetFileName(pathToTool).Length == pathToTool.Length);
                if (!isOnlyFileName)
                {
                    bool isExistingFile = File.Exists(pathToTool);
                    if (!isExistingFile)
                    {
                        LogPrivate.LogErrorWithCodeFromResources("ToolTask.ToolExecutableNotFound", pathToTool);
                        return null;
                    }
                }
                else
                {
                    // if we just have the file name, search for the file on the system path
                    string actualPathToTool = NativeMethodsShared.FindOnPath(pathToTool);

                    // if we find the file
                    if (actualPathToTool != null)
                    {
                        // point to it
                        pathToTool = actualPathToTool;
                    }
                    else
                    {
                        // if we cannot find the file, we'll probably error out later on when
                        // we try to launch the tool; so do nothing for now
                    }
                }
            }

            return pathToTool;
        }

        /// <summary>
        /// Creates a temporary response file for the given command line arguments.
        /// We put as many command line arguments as we can into a response file to
        /// prevent the command line from getting too long. An overly long command
        /// line can cause the process creation to fail.
        /// </summary>
        /// <remarks>
        /// Command line arguments that cannot be put into response files, and which
        /// must appear on the command line, should not be passed to this method.
        /// </remarks>
        /// <param name="responseFileCommands">The command line arguments that need
        /// to go into the temporary response file.</param>
        /// <param name="responseFileSwitch">[out] The command line switch for using
        /// the temporary response file, or null if the response file is not needed.
        /// </param>
        /// <returns>The path to the temporary response file, or null if the response
        /// file is not needed.</returns>
        private string GetTemporaryResponseFile(string responseFileCommands, out string responseFileSwitch)
        {
            string responseFile = null;
            responseFileSwitch = null;

            // if this tool supports response files
            if (!String.IsNullOrEmpty(responseFileCommands))
            {
                // put all the parameters into a temporary response file so we don't
                // have to worry about how long the command-line is going to be

                // May throw IO-related exceptions
                responseFile = FileUtilities.GetTemporaryFile(".rsp");

                // Use the encoding specified by the overridable ResponseFileEncoding property
                using (StreamWriter responseFileStream = new StreamWriter(responseFile, false, this.ResponseFileEncoding))
                {
                    responseFileStream.Write(ResponseFileEscape(responseFileCommands));
                }

                responseFileSwitch = GetResponseFileSwitch(responseFile);
            }

            return responseFile;
        }

        /// <summary>
        /// Initializes the information required to spawn the process executing the tool.
        /// </summary>
        /// <param name="pathToTool"></param>
        /// <param name="commandLineCommands"></param>
        /// <param name="responseFileSwitch"></param>
        /// <returns>The information required to start the process.</returns>
        protected ProcessStartInfo GetProcessStartInfo
        (
            string pathToTool,
            string commandLineCommands,
            string responseFileSwitch
        )
        {
            // Build up the command line that will be spawned.
            string commandLine = commandLineCommands;

            if (!UseCommandProcessor)
            {
                if (!String.IsNullOrEmpty(responseFileSwitch))
                {
                    commandLine += " " + responseFileSwitch;
                }
            }

            // If the command is too long, it will most likely fail. The command line
            // arguments passed into any process cannot exceed 32768 characters, but
            // depending on the structure of the command (e.g. if it contains embedded
            // environment variables that will be expanded), longer commands might work,
            // or shorter commands might fail -- to play it safe, we warn at 32000.
            // NOTE: cmd.exe has a buffer limit of 8K, but we're not using cmd.exe here,
            // so we can go past 8K easily.
            if (commandLine.Length > 32000)
            {
                LogPrivate.LogWarningWithCodeFromResources("ToolTask.CommandTooLong", this.GetType().Name);
            }

            ProcessStartInfo startInfo = new ProcessStartInfo(pathToTool, commandLine);
            startInfo.CreateNoWindow = true;
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardError = true;
            startInfo.RedirectStandardOutput = true;
            // ensure the redirected streams have the encoding we want
            startInfo.StandardErrorEncoding = StandardErrorEncoding;
            startInfo.StandardOutputEncoding = StandardOutputEncoding;

            // Some applications such as xcopy.exe fail without error if there's no stdin stream.
            startInfo.RedirectStandardInput = true;

            // Generally we won't set a working directory, and it will use the current directory
            string workingDirectory = GetWorkingDirectory();
            if (null != workingDirectory)
            {
                startInfo.WorkingDirectory = workingDirectory;
            }

            // Old style environment overrides
#pragma warning disable 0618 // obsolete
            StringDictionary envOverrides = EnvironmentOverride;
            if (null != envOverrides)
            {
                foreach (DictionaryEntry entry in envOverrides)
                {
                    startInfo.EnvironmentVariables[(string)entry.Key] = (string)entry.Value;
                }
#pragma warning restore 0618
            }

            // New style environment overrides
            if (_environmentVariablePairs != null)
            {
                foreach (KeyValuePair<object, object> variable in _environmentVariablePairs)
                {
                    startInfo.EnvironmentVariables[(string)variable.Key] = (string)variable.Value;
                }
            }

            return startInfo;
        }

        /// <summary>
        /// Writes out a temporary response file and shell-executes the tool requested.  Enables concurrent
        /// logging of the output of the tool.
        /// </summary>
        /// <param name="pathToTool">The computed path to tool executable on disk</param>
        /// <param name="responseFileCommands">Command line arguments that should go into a temporary response file</param>
        /// <param name="commandLineCommands">Command line arguments that should be passed to the tool executable directly</param>
        /// <returns>exit code from the tool - if errors were logged and the tool has an exit code of zero, then we sit it to -1</returns>
        virtual protected int ExecuteTool
        (
            string pathToTool,
            string responseFileCommands,
            string commandLineCommands
        )
        {
            if (!UseCommandProcessor)
            {
                LogPathToTool(ToolExe, pathToTool);
            }

            string responseFile = null;
            Process proc = null;

            _standardErrorData = new Queue();
            _standardOutputData = new Queue();

            _standardErrorDataAvailable = new ManualResetEvent(false);
            _standardOutputDataAvailable = new ManualResetEvent(false);

            _toolExited = new ManualResetEvent(false);
            _toolTimeoutExpired = new ManualResetEvent(false);

            _eventsDisposed = false;

            try
            {
                string responseFileSwitch;
                responseFile = GetTemporaryResponseFile(responseFileCommands, out responseFileSwitch);

                // create/initialize the process to run the tool
                proc = new Process();
                proc.StartInfo = GetProcessStartInfo(pathToTool, commandLineCommands, responseFileSwitch);

                // turn on the Process.Exited event
                proc.EnableRaisingEvents = true;
                // sign up for the exit notification
                proc.Exited += new EventHandler(ReceiveExitNotification);

                // turn on async stderr notifications
                proc.ErrorDataReceived += new DataReceivedEventHandler(ReceiveStandardErrorData);
                // turn on async stdout notifications
                proc.OutputDataReceived += new DataReceivedEventHandler(ReceiveStandardOutputData);

                // if we've got this far, we expect to get an exit code from the process. If we don't
                // get one from the process, we want to use an exit code value of -1.
                _exitCode = -1;

                // Start the process
                proc.Start();

                // Close the input stream. This is done to prevent commands from
                // blocking the build waiting for input from the user.
                proc.StandardInput.Close();

                // sign up for stderr callbacks
                proc.BeginErrorReadLine();
                // sign up for stdout callbacks
                proc.BeginOutputReadLine();

                // start the time-out timer
                _toolTimer = new Timer(new TimerCallback(ReceiveTimeoutNotification));
                _toolTimer.Change(Timeout, System.Threading.Timeout.Infinite /* no periodic timeouts */);

                // deal with the various notifications
                HandleToolNotifications(proc);
            }
            finally
            {
                // Delete the temp file used for the response file.
                if (responseFile != null)
                {
                    DeleteTempFile(responseFile);
                }

                // get the exit code and release the process handle
                if (proc != null)
                {
                    try
                    {
                        _exitCode = proc.ExitCode;
                    }
                    catch (InvalidOperationException)
                    {
                        // The process was never launched successfully.
                        // Leave the exit code at -1.
                    }

                    proc.Close();
                    proc = null;
                }

                // If the tool exited cleanly, but logged errors then assign a failing exit code (-1)
                if ((_exitCode == 0) && HasLoggedErrors)
                {
                    _exitCode = -1;
                }

                // release all the OS resources
                // setting a bool to make sure tardy notification threads
                // don't try to set the event after this point
                lock (_eventCloseLock)
                {
                    _eventsDisposed = true;
                    _standardErrorDataAvailable.Close();
                    _standardOutputDataAvailable.Close();

                    _toolExited.Close();
                    _toolTimeoutExpired.Close();

                    if (_toolTimer != null)
                    {
                        _toolTimer.Dispose();
                    }
                }
            }

            return _exitCode;
        }

        /// <summary>
        /// Cancels the process executing the task by asking it to close nicely, then after a short period, forcing termination.
        /// </summary>
        public virtual void Cancel()
        {
            ToolCanceled.Set();
        }

        /// <summary>
        /// Delete temporary file. If the delete fails for some reason (e.g. file locked by anti-virus) then
        /// the call will not throw an exception. Instead a warning will be logged, but the build will not fail.
        /// </summary>
        /// <param name="filename">File to delete</param>
        protected void DeleteTempFile(string fileName)
        {
            if (s_preserveTempFiles)
            {
                Log.LogMessageFromText($"Preserving temporary file '{fileName}'", MessageImportance.Low);
                return;
            }

            try
            {
                File.Delete(fileName);
            }
            catch (Exception e) when (ExceptionHandling.IsIoRelatedException(e))
            {
                // Warn only -- occasionally temp files fail to delete because of virus checkers; we 
                // don't want the build to fail in such cases
                LogShared.LogWarningWithCodeFromResources("Shared.FailedDeletingTempFile", fileName, e.Message);
            }
        }

        /// <summary>
        /// Handles all the notifications sent while the tool is executing. The
        /// notifications can be for tool output, tool time-out, or tool completion.
        /// </summary>
        /// <remarks>
        /// The slightly convoluted use of the async stderr/stdout streams of the
        /// Process class is necessary because we want to log all our messages from
        /// the main thread, instead of from a worker or callback thread.
        /// </remarks>
        /// <param name="proc"></param>
        private void HandleToolNotifications(Process proc)
        {
            // NOTE: the ordering of this array is deliberate -- if multiple
            // notifications are sent simultaneously, we want to handle them
            // in the order specified by the array, so that we can observe the
            // following rules:
            // 1) if a tool times-out we want to abort it immediately regardless
            //    of whether its stderr/stdout queues are empty
            // 2) if a tool exits, we first want to flush its stderr/stdout queues
            // 3) if a tool exits and times-out at the same time, we want to let
            //    it exit gracefully
            WaitHandle[] notifications = new WaitHandle[]
                                            {
                                                _toolTimeoutExpired,
                                                ToolCanceled,
                                                _standardErrorDataAvailable,
                                                _standardOutputDataAvailable,
                                                _toolExited
                                            };

            bool isToolRunning = true;

            if (YieldDuringToolExecution)
            {
                BuildEngine3.Yield();
            }

            try
            {
                while (isToolRunning)
                {
                    // wait for something to happen -- we block the main thread here
                    // because we don't want to uselessly consume CPU cycles; in theory
                    // we could poll the stdout and stderr queues, but polling is not
                    // good for performance, and so we use ManualResetEvents to wake up
                    // the main thread only when necessary
                    // NOTE: the return value from WaitAny() is the array index of the
                    // notification that was sent; if multiple notifications are sent
                    // simultaneously, the return value is the index of the notification
                    // with the smallest index value of all the sent notifications
                    int notificationIndex = WaitHandle.WaitAny(notifications);

                    switch (notificationIndex)
                    {
                        // tool timed-out
                        case 0:
                        // tool was canceled
                        case 1:
                            TerminateToolProcess(proc, notificationIndex == 1);
                            _terminatedTool = true;
                            isToolRunning = false;
                            break;
                        // tool wrote to stderr (and maybe stdout also)
                        case 2:
                            LogMessagesFromStandardError();
                            // if stderr and stdout notifications were sent simultaneously, we
                            // must alternate between the queues, and not starve the stdout queue
                            LogMessagesFromStandardOutput();
                            break;

                        // tool wrote to stdout
                        case 3:
                            LogMessagesFromStandardOutput();
                            break;

                        // tool exited
                        case 4:
                            // We need to do this to guarantee the stderr/stdout streams
                            // are empty -- there seems to be no other way of telling when the
                            // process is done sending its async stderr/stdout notifications; why
                            // is the Process class sending the exit notification prematurely?
                            WaitForProcessExit(proc);

                            // flush the stderr and stdout queues to clear out the data placed
                            // in them while we were waiting for the process to exit
                            LogMessagesFromStandardError();
                            LogMessagesFromStandardOutput();
                            isToolRunning = false;
                            break;

                        default:
                            ErrorUtilities.VerifyThrow(false, "Unknown tool notification.");
                            break;
                    }
                }
            }
            finally
            {
                if (YieldDuringToolExecution)
                {
                    BuildEngine3.Reacquire();
                }
            }
        }

        /// <summary>
        /// Kills the given process that is executing the tool, because the tool's
        /// time-out period expired.
        /// </summary>
        /// <param name="proc"></param>
        private void KillToolProcessOnTimeout(Process proc, bool isBeingCancelled)
        {
            // kill the process if it's not finished yet
            if (!proc.HasExited)
            {
                if (!isBeingCancelled)
                {
                    ErrorUtilities.VerifyThrow(Timeout != System.Threading.Timeout.Infinite,
                        "A time-out value must have been specified or the task must be cancelled.");

                    LogShared.LogWarningWithCodeFromResources("Shared.KillingProcess", this.Timeout);
                }
                else
                {
                    LogShared.LogWarningWithCodeFromResources("Shared.KillingProcessByCancellation", proc.ProcessName);
                }

                try
                {
                    // issue the kill command
                    NativeMethodsShared.KillTree(proc.Id);
                }
                catch (InvalidOperationException)
                {
                    // The process already exited, which is fine,
                    // just continue.
                }

                // wait until the process finishes exiting/getting killed. 
                // We don't want to wait forever here because the task is already supposed to be dieing, we just want to give it long enough
                // to try and flush what it can and stop. If it cannot do that in a reasonable time frame then we will just ignore it.
                int timeout = 5000;
                string timeoutFromEnvironment = Environment.GetEnvironmentVariable("MSBUILDTOOLTASKCANCELPROCESSWAITTIMEOUT");
                if (timeoutFromEnvironment != null)
                {
                    int result = 0;
                    if (int.TryParse(timeoutFromEnvironment, out result) && result >= 0)
                    {
                        timeout = result;
                    }
                }

                proc.WaitForExit(timeout);
            }
        }

        /// <summary>
        /// Kills the specified process
        /// </summary>
        private void TerminateToolProcess(Process proc, bool isBeingCancelled)
        {
            if (proc != null)
            {
                if (proc.HasExited)
                {
                    return;
                }

                if (isBeingCancelled)
                {
                    try
                    {
                        proc.CancelOutputRead();
                        proc.CancelErrorRead();
                    }
                    catch (InvalidOperationException)
                    {
                        // The task possibly never started.
                    }
                }

                KillToolProcessOnTimeout(proc, isBeingCancelled);
            }
        }

        /// <summary>
        /// Confirms that the given process has really and truly exited. If the
        /// process is still finishing up, this method waits until it is done.
        /// </summary>
        /// <remarks>
        /// This method is a hack, but it needs to be called after both
        /// Process.WaitForExit() and Process.Kill().
        /// </remarks>
        /// <param name="proc"></param>
        private void WaitForProcessExit(Process proc)
        {
            proc.WaitForExit();

            // Process.WaitForExit() may return prematurely. We need to check to be sure.
            while (!proc.HasExited)
            {
                System.Threading.Thread.Sleep(50);
            }
        }

        /// <summary>
        /// Logs all the messages that the tool wrote to stderr. The messages
        /// are read out of the stderr data queue.
        /// </summary>
        private void LogMessagesFromStandardError()
        {
            LogMessagesFromStandardErrorOrOutput(_standardErrorData, _standardErrorDataAvailable, _standardErrorImportanceToUse, StandardOutputOrErrorQueueType.StandardError);
        }

        /// <summary>
        /// Logs all the messages that the tool wrote to stdout. The messages
        /// are read out of the stdout data queue.
        /// </summary>
        private void LogMessagesFromStandardOutput()
        {
            LogMessagesFromStandardErrorOrOutput(_standardOutputData, _standardOutputDataAvailable, _standardOutputImportanceToUse, StandardOutputOrErrorQueueType.StandardOutput);
        }

        /// <summary>
        /// Logs all the messages that the tool wrote to either stderr or stdout.
        /// The messages are read out of the given data queue. This method is a
        /// helper for the <see cref="LogMessagesFromStandardError"/>() and <see
        /// cref="LogMessagesFromStandardOutput"/>() methods.
        /// </summary>
        /// <param name="dataQueue"></param>
        /// <param name="dataAvailableSignal"></param>
        /// <param name="messageImportance"></param>
        private void LogMessagesFromStandardErrorOrOutput
        (
            Queue dataQueue,
            ManualResetEvent dataAvailableSignal,
            MessageImportance messageImportance,
            StandardOutputOrErrorQueueType queueType
        )
        {
            ErrorUtilities.VerifyThrow(dataQueue != null,
                "The data queue must be available.");

            // synchronize access to the queue -- this is a producer-consumer problem
            // NOTE: the synchronization problem here is actually not about the queue
            // at all -- if we only cared about reading from and writing to the queue,
            // we could use a synchronized wrapper around the queue, and things would
            // work perfectly; the synchronization problem here is actually around the
            // ManualResetEvent -- while a ManualResetEvent itself is a thread-safe
            // type, the information we infer from the state of a ManualResetEvent is
            // not thread-safe; because a ManualResetEvent does not have a ref count,
            // we cannot safely set (or reset) it outside of a synchronization block;
            // therefore instead of using synchronized queue wrappers, we just lock the
            // entire queue, empty it, and reset the ManualResetEvent before releasing
            // the lock; this also allows proper alternation between the stderr and
            // stdout queues -- otherwise we would continuously read from one queue and
            // starve the other; locking out the producer allows the consumer to
            // alternate between the queues
            lock (dataQueue.SyncRoot)
            {
                while (dataQueue.Count > 0)
                {
                    string errorOrOutMessage = dataQueue.Dequeue() as String;
                    if (!LogStandardErrorAsError || queueType == StandardOutputOrErrorQueueType.StandardOutput)
                    {
                        this.LogEventsFromTextOutput(errorOrOutMessage, messageImportance);
                    }
                    else if (LogStandardErrorAsError && queueType == StandardOutputOrErrorQueueType.StandardError)
                    {
                        Log.LogError(errorOrOutMessage);
                    }
                }

                ErrorUtilities.VerifyThrow(dataAvailableSignal != null,
                    "The signalling event must be available.");

                // the queue is empty, so reset the notification
                // NOTE: intentionally, do the reset inside the lock, because
                // ManualResetEvents don't have ref counts, and we want to make
                // sure we don't reset the notification just after the producer
                // signals it
                dataAvailableSignal.Reset();
            }
        }

        /// <summary>
        /// Calls a method on the TaskLoggingHelper to parse a single line of text to
        /// see if there are any errors or warnings in canonical format.  This can
        /// be overridden by the derived class if necessary.
        /// </summary>
        /// <param name="singleLine"></param>
        /// <param name="messageImportance"></param>
        virtual protected void LogEventsFromTextOutput
        (
            string singleLine,
            MessageImportance messageImportance
        )
        {
            Log.LogMessageFromText(singleLine, messageImportance);
        }

        /// <summary>
        /// Signals when the tool times-out. The tool timer calls this method
        /// when the time-out period on the tool expires.
        /// </summary>
        /// <remarks>This method is used as a System.Threading.TimerCallback delegate.</remarks>
        /// <param name="unused"></param>
        private void ReceiveTimeoutNotification(object unused)
        {
            ErrorUtilities.VerifyThrow(_toolTimeoutExpired != null,
                "The signalling event for tool time-out must be available.");
            lock (_eventCloseLock)
            {
                if (!_eventsDisposed)
                {
                    _toolTimeoutExpired.Set();
                }
            }
        }

        /// <summary>
        /// Signals when the tool exits. The Process object executing the tool
        /// calls this method when the tool exits.
        /// </summary>
        /// <remarks>This method is used as a System.EventHandler delegate.</remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReceiveExitNotification(object sender, EventArgs e)
        {
            ErrorUtilities.VerifyThrow(_toolExited != null,
                "The signalling event for tool exit must be available.");

            lock (_eventCloseLock)
            {
                if (!_eventsDisposed)
                {
                    _toolExited.Set();
                }
            }
        }

        /// <summary>
        /// Queues up the output from the stderr stream of the process executing
        /// the tool, and signals the availability of the data. The Process object
        /// executing the tool calls this method for every line of text that the
        /// tool writes to stderr.
        /// </summary>
        /// <remarks>This method is used as a System.Diagnostics.DataReceivedEventHandler delegate.</remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReceiveStandardErrorData(object sender, DataReceivedEventArgs e)
        {
            ReceiveStandardErrorOrOutputData(e, _standardErrorData, _standardErrorDataAvailable);
        }

        /// <summary>
        /// Queues up the output from the stdout stream of the process executing
        /// the tool, and signals the availability of the data. The Process object
        /// executing the tool calls this method for every line of text that the
        /// tool writes to stdout.
        /// </summary>
        /// <remarks>This method is used as a System.Diagnostics.DataReceivedEventHandler delegate.</remarks>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ReceiveStandardOutputData(object sender, DataReceivedEventArgs e)
        {
            ReceiveStandardErrorOrOutputData(e, _standardOutputData, _standardOutputDataAvailable);
        }

        /// <summary>
        /// Queues up the output from either the stderr or stdout stream of the
        /// process executing the tool, and signals the availability of the data.
        /// This method is a helper for the <see cref="ReceiveStandardErrorData"/>()
        /// and <see cref="ReceiveStandardOutputData"/>() methods.
        /// </summary>
        /// <param name="e"></param>
        /// <param name="dataQueue"></param>
        /// <param name="dataAvailableSignal"></param>
        private void ReceiveStandardErrorOrOutputData
        (
            DataReceivedEventArgs e,
            Queue dataQueue,
            ManualResetEvent dataAvailableSignal
        )
        {
            // NOTE: don't ignore empty string, because we need to log that
            if (e.Data != null)
            {
                ErrorUtilities.VerifyThrow(dataQueue != null,
                    "The data queue must be available.");

                // synchronize access to the queue -- this is a producer-consumer problem
                // NOTE: we lock the entire queue instead of using synchronized queue
                // wrappers, because ManualResetEvents don't have ref counts, and it's
                // difficult to discretely signal the availability of each instance of
                // data in the queue -- so instead we let the consumer lock and empty
                // the queue and reset the ManualResetEvent, before we add more data
                // into the queue, and signal the ManualResetEvent again
                lock (dataQueue.SyncRoot)
                {
                    dataQueue.Enqueue(e.Data);

                    ErrorUtilities.VerifyThrow(dataAvailableSignal != null,
                        "The signalling event must be available.");

                    // signal the availability of data
                    // NOTE: intentionally, do the signalling inside the lock, because
                    // ManualResetEvents don't have ref counts, and we want to make sure
                    // we don't signal the notification just before the consumer resets it
                    lock (_eventCloseLock)
                    {
                        if (!_eventsDisposed)
                        {
                            dataAvailableSignal.Set();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Assign the importances that will be used for stdout/stderr logging of messages from this tool task.
        /// This takes into account (1 is highest precedence):
        /// 1. the override value supplied as a task parameter.
        /// 2. those overridden by any derived class and
        /// 3. the defaults given by tooltask
        /// </summary>
        private bool AssignStandardStreamLoggingImportance()
        {
            // Gather the importance for the Standard Error stream:
            if ((_standardErrorImportance == null) || (_standardErrorImportance.Length == 0))
            {
                // If we have no task parameter override then ask the task for its default
                _standardErrorImportanceToUse = StandardErrorLoggingImportance;
            }
            else
            {
                try
                {
                    // Parse the raw importance string into a strongly typed enumeration.  
                    _standardErrorImportanceToUse = (MessageImportance)Enum.Parse(typeof(MessageImportance), _standardErrorImportance, true /* case-insensitive */);
                }
                catch (ArgumentException)
                {
                    Log.LogErrorWithCodeFromResources("Message.InvalidImportance", _standardErrorImportance);
                    return false;
                }
            }

            // Gather the importance for the Standard Output stream:
            if ((_standardOutputImportance == null) || (_standardOutputImportance.Length == 0))
            {
                // If we have no task parameter override then ask the task for its default
                _standardOutputImportanceToUse = StandardOutputLoggingImportance;
            }
            else
            {
                try
                {
                    // Parse the raw importance string into a strongly typed enumeration.  
                    _standardOutputImportanceToUse = (MessageImportance)Enum.Parse(typeof(MessageImportance), _standardOutputImportance, true /* case-insensitive */);
                }
                catch (ArgumentException)
                {
                    Log.LogErrorWithCodeFromResources("Message.InvalidImportance", _standardOutputImportance);
                    return false;
                }
            }

            return true;
        }

        #endregion

        #region ITask Members

        /// <summary>
        /// This method invokes the tool with the given parameters.
        /// </summary>
        /// <returns>true, if task executes successfully</returns>
        public override bool Execute()
        {
            // Let the tool validate its parameters. ToolTask is responsible for logging
            // useful information about what was wrong with the parameters.
            if (!ValidateParameters())
            {
                return false;
            }

            if (EnvironmentVariables != null)
            {
                _environmentVariablePairs = new List<KeyValuePair<object, object>>(EnvironmentVariables.Length);

                foreach (string entry in EnvironmentVariables)
                {
                    string[] nameValuePair = entry.Split(s_equalsSplitter, 2);

                    if (nameValuePair.Length == 1 || (nameValuePair.Length == 2 && nameValuePair[0].Length == 0))
                    {
                        LogPrivate.LogErrorWithCodeFromResources("ToolTask.InvalidEnvironmentParameter", nameValuePair[0]);
                        return false;
                    }

                    _environmentVariablePairs.Add(new KeyValuePair<object, object>((object)nameValuePair[0], (object)nameValuePair[1]));
                }
            }

            // Assign standard stream logging importances
            if (!AssignStandardStreamLoggingImportance())
            {
                return false;
            }

            try
            {
                if (SkipTaskExecution())
                {
                    // the task has said there's no command-line that we need to run, so
                    // return true to indicate this task completed successfully (without
                    // doing any actual work).
                    return true;
                }

                string commandLineCommands = GenerateCommandLineCommands();
                // If there are response file commands, then we need a response file later.
                string batchFileContents = commandLineCommands;
                string responseFileCommands = GenerateResponseFileCommands();

                if (UseCommandProcessor)
                {
                    ToolExe = "cmd.exe";

                    // Generate the temporary batch file
                    // May throw IO-related exceptions
                    _temporaryBatchFile = FileUtilities.GetTemporaryFile(".cmd");

                    File.AppendAllText(_temporaryBatchFile, commandLineCommands, EncodingUtilities.CurrentSystemOemEncoding);

                    string batchFileForCommandLine = _temporaryBatchFile;

                    // If for some crazy reason the path has a & character and a space in it
                    // then get the short path of the temp path, which should not have spaces in it
                    // and then escape the &
                    if (batchFileForCommandLine.Contains("&") && !batchFileForCommandLine.Contains("^&"))
                    {
                        batchFileForCommandLine = NativeMethodsShared.GetShortFilePath(batchFileForCommandLine);
                        batchFileForCommandLine = batchFileForCommandLine.Replace("&", "^&");
                    }

                    commandLineCommands = "/C \"" + batchFileForCommandLine + "\"";
                    if (EchoOff)
                    {
                        commandLineCommands = "/Q " + commandLineCommands;
                    }
                }

                // ensure the command line arguments string is not null
                if ((commandLineCommands == null) || (commandLineCommands.Length == 0))
                {
                    commandLineCommands = String.Empty;
                }
                // add a leading space to the command line arguments (if any) to
                // separate them from the tool path
                else
                {
                    commandLineCommands = " " + commandLineCommands;
                }

                // Initialize the host object.  At this point, the task may elect
                // to not proceed.  Compiler tasks do this for purposes of up-to-date
                // checking in the IDE.  
                HostObjectInitializationStatus nextAction = InitializeHostObject();
                if (nextAction == HostObjectInitializationStatus.NoActionReturnSuccess)
                {
                    return true;
                }
                else if (nextAction == HostObjectInitializationStatus.NoActionReturnFailure)
                {
                    _exitCode = 1;
                    return HandleTaskExecutionErrors();
                }

                string pathToTool = ComputePathToTool();
                if (pathToTool == null)
                {
                    // An appropriate error should have been logged already.
                    return false;
                }

                // Log the environment. We do this up here,
                // rather than later where the environment is set,
                // so that it appears before the command line is logged.
                bool alreadyLoggedEnvironmentHeader = false;

                // Old style environment overrides
#pragma warning disable 0618 // obsolete
                StringDictionary envOverrides = EnvironmentOverride;
                if (null != envOverrides)
                {
                    foreach (DictionaryEntry entry in envOverrides)
                    {
                        alreadyLoggedEnvironmentHeader = LogEnvironmentVariable(alreadyLoggedEnvironmentHeader, (string)entry.Key, (string)entry.Value);
                    }
#pragma warning restore 0618
                }

                // New style environment overrides
                if (_environmentVariablePairs != null)
                {
                    foreach (KeyValuePair<object, object> variable in _environmentVariablePairs)
                    {
                        alreadyLoggedEnvironmentHeader = LogEnvironmentVariable(alreadyLoggedEnvironmentHeader, (string)variable.Key, (string)variable.Value);
                    }
                }

                if (UseCommandProcessor)
                {
                    // Log that we are about to invoke the specified command.  
                    LogToolCommand(pathToTool + commandLineCommands);
                    LogToolCommand(batchFileContents);
                }
                else
                {
                    // Log that we are about to invoke the specified command.  
                    LogToolCommand(pathToTool + commandLineCommands + " " + responseFileCommands);
                }
                _exitCode = 0;

                if (nextAction == HostObjectInitializationStatus.UseHostObjectToExecute)
                {
                    // The hosting IDE passed in a host object to this task.  Give the task
                    // a chance to call this host object to do the actual work.  
                    try
                    {
                        if (!CallHostObjectToExecute())
                        {
                            _exitCode = 1;
                        }
                    }
                    catch (Exception e)
                    {
                        LogPrivate.LogErrorFromException(e);
                        return false;
                    }
                }
                else
                {
                    ErrorUtilities.VerifyThrow(nextAction == HostObjectInitializationStatus.UseAlternateToolToExecute,
                        "Invalid return status");

                    // No host object was provided, or at least not one that supports all of the
                    // switches/parameters we need.  So shell out to the command-line tool.
                    _exitCode = ExecuteTool(pathToTool, responseFileCommands, commandLineCommands);
                }

                // Raise a comment event to notify that the process completed
                if (_terminatedTool)
                {
                    return false;
                }
                else if (_exitCode != 0)
                {
                    return HandleTaskExecutionErrors();
                }
                else
                {
                    return true;
                }
            }
            catch (ArgumentException e)
            {
                if (!_terminatedTool)
                {
                    LogPrivate.LogErrorWithCodeFromResources("General.InvalidToolSwitch", ToolExe, GetErrorMessageWithDiagnosticsCheck(e));
                }
                return false;
            }
            catch (Win32Exception e)
            {
                if (!_terminatedTool)
                {
                    LogPrivate.LogErrorWithCodeFromResources("ToolTask.CouldNotStartToolExecutable", ToolExe, GetErrorMessageWithDiagnosticsCheck(e));
                }
                return false;
            }
            catch (IOException e)
            {
                if (!_terminatedTool)
                {
                    LogPrivate.LogErrorWithCodeFromResources("ToolTask.CouldNotStartToolExecutable", ToolExe, GetErrorMessageWithDiagnosticsCheck(e));
                }
                return false;
            }
            catch (UnauthorizedAccessException e)
            {
                if (!_terminatedTool)
                {
                    LogPrivate.LogErrorWithCodeFromResources("ToolTask.CouldNotStartToolExecutable", ToolExe, GetErrorMessageWithDiagnosticsCheck(e));
                }
                return false;
            }
            finally
            {
                // Clean up after ourselves.
                if (_temporaryBatchFile != null && File.Exists(_temporaryBatchFile))
                {
                    DeleteTempFile(_temporaryBatchFile);
                }
            }
        } // Execute()

        /// <summary>
        /// This method takes in an exception and if MSBuildDiagnostics is set then it will display the stack trace
        /// if it is not set only the message will be displayed, this is to fix the problem where the user was getting
        /// stack trace when a shorter message was better
        /// </summary>
        /// <returns>exception message</returns>
        private string GetErrorMessageWithDiagnosticsCheck(Exception e)
        {
            // If MSBuildDiagnostics is set show stack trace information
            if (Environment.GetEnvironmentVariable("MSBuildDiagnostics") != null)
            {
                // Includes stack trace
                return e.ToString();
            }
            else
            {
                // does not include stack trace
                return e.Message;
            }
        }

        /// <summary>
        /// Log a single environment variable that's about to be applied to the tool
        /// </summary>
        private bool LogEnvironmentVariable(bool alreadyLoggedEnvironmentHeader, string key, string value)
        {
            if (!alreadyLoggedEnvironmentHeader)
            {
                LogPrivate.LogMessageFromResources(MessageImportance.Low, "ToolTask.EnvironmentVariableHeader");
                alreadyLoggedEnvironmentHeader = true;
            }

            Log.LogMessage(MessageImportance.Low, "  {0}={1}", key, value);

            return alreadyLoggedEnvironmentHeader;
        }

        #endregion

        #region Member data

        /// <summary>
        /// An object to hold the event shutdown lock
        /// </summary>
        private object _eventCloseLock = new object();

        /// <summary>
        /// Splitter for environment variables
        /// </summary>
        private static char[] s_equalsSplitter = new char[] { '=' };

        /// <summary>
        /// Task Parameter: Override the importance at which standard out messages will be logged 
        /// </summary>
        private string _standardOutputImportance = null;

        /// <summary>
        /// Task Parameter: Override the importance at which standard error messages will be logged 
        /// </summary>
        private string _standardErrorImportance = null;

        /// <summary>
        /// Task Parameter: Should messages received on the standard error stream be logged as errros
        /// </summary>
        private bool _logStandardErrorAsError = false;

        /// <summary>
        /// The actual importance at which standard out messages will be logged 
        /// </summary>
        private MessageImportance _standardOutputImportanceToUse = MessageImportance.Low;

        /// <summary>
        /// The actual importance at which standard error messages will be logged 
        /// </summary>
        private MessageImportance _standardErrorImportanceToUse = MessageImportance.Normal;

        /// <summary>
        /// Holds the stderr output from the tool.
        /// </summary>
        /// <remarks>This collection is NOT thread-safe.</remarks>
        private Queue _standardErrorData;

        /// <summary>
        /// Holds the stdout output from the tool.
        /// </summary>
        /// <remarks>This collection is NOT thread-safe.</remarks>
        private Queue _standardOutputData;

        /// <summary>
        /// Used for signalling when the tool writes to stderr.
        /// </summary>
        private ManualResetEvent _standardErrorDataAvailable;

        /// <summary>
        /// Used for signalling when the tool writes to stdout.
        /// </summary>
        private ManualResetEvent _standardOutputDataAvailable;

        /// <summary>
        /// Used for signalling when the tool exits.
        /// </summary>
        private ManualResetEvent _toolExited;

        /// <summary>
        /// Set to true if the tool process was terminated, 
        /// either because the timeout was reached or it was canceled.
        /// </summary>
        private bool _terminatedTool;

        /// <summary>
        /// Used for signalling when the tool times-out.
        /// </summary>
        private ManualResetEvent _toolTimeoutExpired;

        /// <summary>
        /// Used for timing-out the tool.
        /// </summary>
        private Timer _toolTimer;

        /// <summary>
        /// Used to support overriding the toolExe name.
        /// </summary>
        private string _toolExe = null;

        /// <summary>
        /// Set when the events are about to be disposed, so that tardy
        /// calls on the event handlers don't try to reset a disposed event
        /// </summary>
        private bool _eventsDisposed;

        /// <summary>
        /// List of name, value pairs to be passed to the spawned tool's environment.
        /// May be null.
        /// Object is used instead of string to avoid NGen/JIT FXcop flagging.
        /// </summary>
        private List<KeyValuePair<object, object>> _environmentVariablePairs;

        /// <summary>
        /// Enumeration which indicates what kind of queue is being passed
        /// </summary>
        private enum StandardOutputOrErrorQueueType
        {
            StandardError = 0,
            StandardOutput = 1
        }

        #endregion
    }
}
