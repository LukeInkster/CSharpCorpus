﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Resources;
using System.Text;
using System.Text.RegularExpressions;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Lifetime;
using System.Runtime.Remoting;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

#if BUILD_ENGINE
namespace Microsoft.Build.BackEnd
#else
namespace Microsoft.Build.Utilities
#endif
{
    /// <summary>
    /// Helper logging class - contains all the logging methods used by tasks.
    /// A TaskLoggingHelper object is passed to every task by MSBuild. For tasks that derive
    /// from the Task class, it is provided in the Log property.
    /// This class is thread safe: tasks can log from any threads.
    /// </summary>
#if BUILD_ENGINE
    internal
#else
    public
#endif
 class TaskLoggingHelper : MarshalByRefObject
    {
        #region Constructors

        /// <summary>
        /// public constructor
        /// </summary>
        /// <param name="taskInstance">task containing an instance of this class</param>
        public TaskLoggingHelper(ITask taskInstance)
        {
            ErrorUtilities.VerifyThrowArgumentNull(taskInstance, "taskInstance");
            _taskInstance = taskInstance;
            _taskName = taskInstance.GetType().Name;
        }

        /// <summary>
        /// Public constructor which can be used by task factories to assist them in logging messages.
        /// </summary>
        /// <param name="taskInstance">task containing an instance of this class</param>
        public TaskLoggingHelper(IBuildEngine buildEngine, string taskName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(buildEngine, "buildEngine");
            ErrorUtilities.VerifyThrowArgumentLength(taskName, "taskName");
            _taskName = taskName;
            _buildEngine = buildEngine;
        }

        #endregion

        #region Properties

        /// <summary>
        /// A client sponsor is a class
        /// which will respond to a lease renewal request and will
        /// increase the lease time allowing the object to stay in memory
        /// </summary>
        private ClientSponsor _sponsor;

        // We have to pass an instance of ITask to BuildEngine, and since we call into the engine from this class we
        // need to store the actual task instance.
        private ITask _taskInstance;

        /// <summary>
        /// Object to make this class thread-safe.
        /// </summary>
        private Object _locker = new Object();

        /// <summary>
        /// Gets the name of the parent task.
        /// </summary>
        /// <value>Task name string.</value>
        protected string TaskName
        {
            get
            {
                return _taskName;
            }
        }

        // the name of the parent task
        private string _taskName;

        /// <summary>
        /// Gets the upper-case version of the parent task's name.
        /// </summary>
        /// <value>Upper-case task name string.</value>
        private string TaskNameUpperCase
        {
            get
            {
                if (_taskNameUpperCase == null)
                {
                    // NOTE: use the current thread culture, because this string will be displayed to the user
                    _taskNameUpperCase = TaskName.ToUpper(CultureInfo.CurrentCulture);
                }

                return _taskNameUpperCase;
            }
        }

        // the upper-case version of the parent task's name (for logging purposes)
        private string _taskNameUpperCase;

        /// <summary>
        /// The build engine we are going to log against
        /// </summary>
        private IBuildEngine _buildEngine;

        /// <summary>
        /// Shortcut property for getting our build engine - we retrieve it from the task instance
        /// </summary>
        protected IBuildEngine BuildEngine
        {
            get
            {
                // If the task instance does not equal null then use its build engine because 
                // the task instances build engine can be changed for example during tests. This changing of the engine on the same task object is not expected to happen
                // during normal operation.
                if (_taskInstance != null)
                {
                    return _taskInstance.BuildEngine;
                }

                return _buildEngine;
            }
        }

        /// <summary>
        /// Used to load culture-specific resources. Derived classes should register their resources either during construction, or
        /// via this property, if they have localized strings.
        /// </summary>
        public ResourceManager TaskResources
        {
            get
            {
                return _taskResources;
            }

            set
            {
                _taskResources = value;
            }
        }

        // UI resources (including strings) used by the logging methods
        private ResourceManager _taskResources;

        /// <summary>
        /// Gets or sets the prefix used to compose help keywords from string resource names.
        /// </summary>
        /// <value>The help keyword prefix string.</value>
        public string HelpKeywordPrefix
        {
            get
            {
                return _helpKeywordPrefix;
            }

            set
            {
                _helpKeywordPrefix = value;
            }
        }

        // the prefix for composing help keywords
        private string _helpKeywordPrefix;

        /// <summary>
        /// Has the task logged any errors through this logging helper object?
        /// </summary>
        public bool HasLoggedErrors
        {
            get
            {
                return _hasLoggedErrors;
            }
        }

        // Has the task logged any errors through this logging helper object?
        private bool _hasLoggedErrors = false;

        #endregion

        #region Utility methods

        /// <summary>
        /// Extracts the message code (if any) prefixed to the given message string. Message code prefixes must match the
        /// following .NET regular expression in order to be recognized: <c>^\s*[A-Za-z]+\d+:\s*</c>
        /// Thread safe.
        /// </summary>
        /// <example>
        /// If this method is given the string "MYTASK1001: This is an error message.", it will return "MYTASK1001" for the
        /// message code, and "This is an error message." for the message.
        /// </example>
        /// <param name="message">The message to parse.</param>
        /// <param name="messageWithoutCodePrefix">The message with the code prefix removed (if any).</param>
        /// <returns>The message code extracted from the prefix, or null if there was no code.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <c>message</c> is null.</exception>
        public string ExtractMessageCode(string message, out string messageWithoutCodePrefix)
        {
            ErrorUtilities.VerifyThrowArgumentNull(message, "message");

            string code;
            messageWithoutCodePrefix = ResourceUtilities.ExtractMessageCode(false /* any code */, message, out code);

            return code;
        }

        /// <summary>
        /// Loads the specified resource string and optionally formats it using the given arguments. The current thread's culture
        /// is used for formatting.
        /// 
        /// Requires the owner task to have registered its resources either via the Task (or TaskMarshalByRef) base
        /// class constructor, or the Task.TaskResources (or AppDomainIsolatedTask.TaskResources) property.
        /// 
        /// Thread safe.
        /// </summary>
        /// <param name="resourceName">The name of the string resource to load.</param>
        /// <param name="args">Optional arguments for formatting the loaded string.</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <c>resourceName</c> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the string resource indicated by <c>resourceName</c> does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the <c>TaskResources</c> property of the owner task is not set.</exception>
        virtual public string FormatResourceString(string resourceName, params object[] args)
        {
            ErrorUtilities.VerifyThrowArgumentNull(resourceName, "resourceName");
            ErrorUtilities.VerifyThrowInvalidOperation(TaskResources != null, "Shared.TaskResourcesNotRegistered", TaskName);

            string resourceString = TaskResources.GetString(resourceName, CultureInfo.CurrentUICulture);

            ErrorUtilities.VerifyThrowArgument(resourceString != null, "Shared.TaskResourceNotFound", resourceName, TaskName);

            return FormatString(resourceString, args);
        }

        /// <summary>
        /// Formats the given string using the variable arguments passed in. The current thread's culture is used for formatting.
        /// Thread safe.
        /// </summary>
        /// <param name="unformatted">The string to format.</param>
        /// <param name="args">Arguments for formatting.</param>
        /// <returns>The formatted string.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <c>unformatted</c> is null.</exception>
        virtual public string FormatString(string unformatted, params object[] args)
        {
            ErrorUtilities.VerifyThrowArgumentNull(unformatted, "unformatted");

            return ResourceUtilities.FormatString(unformatted, args);
        }

        /// <summary>
        /// Get the message from resource in task library.
        /// Thread safe.
        /// </summary>
        /// <param name="resourceName">The resource name.</param>
        /// <returns>The message from resource.</returns>
        virtual public string GetResourceMessage(string resourceName)
        {
            string resourceString = FormatResourceString(resourceName, null);
            return resourceString;
        }
        #endregion

        #region Message logging methods

        /// <summary>
        /// Logs a message using the specified string.
        /// Thread safe.
        /// </summary>
        /// <param name="message">The message string.</param>
        /// <param name="messageArgs">Optional arguments for formatting the message string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>message</c> is null.</exception>
        public void LogMessage(string message, params object[] messageArgs)
        {
            // This API is poorly designed, because parameters misordered like LogMessage(message, MessageImportance.High)
            // will use this overload, ignore the importance and accidentally format the string.
            // Can't change it now as it's shipped, but this debug assert will help catch callers doing this.
            // Debug only because it is in theory legitimate to pass importance as a string format parameter.
            Debug.Assert(messageArgs == null || messageArgs.Length == 0 || messageArgs[0].GetType() != typeof(MessageImportance), "Did you call the wrong overload?");

            LogMessage(MessageImportance.Normal, message, messageArgs);
        }

        /// <summary>
        /// Logs a message of the given importance using the specified string.
        /// Thread safe.
        /// </summary>
        /// <remarks>
        /// Take care to order the parameters correctly or the other overload will be called inadvertently.
        /// </remarks>
        /// <param name="importance">The importance level of the message.</param>
        /// <param name="message">The message string.</param>
        /// <param name="messageArgs">Optional arguments for formatting the message string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>message</c> is null.</exception>
        public void LogMessage(MessageImportance importance, string message, params object[] messageArgs)
        {
            // No lock needed, as BuildEngine methods from v4.5 onwards are thread safe.
            ErrorUtilities.VerifyThrowArgumentNull(message, "message");

            BuildMessageEventArgs e = new BuildMessageEventArgs
                (
                    message,                             // message
                    null,                                // help keyword
                    TaskName,                            // sender 
                    importance,                          // importance
                    DateTime.UtcNow,                     // timestamp
                    messageArgs                          // message arguments
                );

            // If BuildEngine is null, task attempted to log before it was set on it,
            // presumably in its constructor. This is not allowed, and all
            // we can do is throw.
            if (BuildEngine == null)
            {
                // Do not use Verify[...] as it would read e.Message ahead of time
                ErrorUtilities.ThrowInvalidOperation("LoggingBeforeTaskInitialization", e.Message);
            }

            BuildEngine.LogMessageEvent(e);
#if _DEBUG
            // Assert that the message does not contain an error code.  Only errors and warnings
            // should have error codes.
            string errorCode;
            ResourceUtilities.ExtractMessageCode(true /* only msbuild codes */, message, out errorCode);
            Debug.Assert(errorCode == null, errorCode, "This message contains an error code (" + errorCode + "), yet it was logged as a regular message: " + message);
#endif
        }

        /// <summary>
        /// Logs a message using the specified string and other message details.
        /// Thread safe.
        /// </summary>
        /// <param name="subcategory">Description of the warning type (can be null).</param>
        /// <param name="code">Message code (can be null)</param>
        /// <param name="helpKeyword">The help keyword for the host IDE (can be null).</param>
        /// <param name="file">The path to the file causing the message (can be null).</param>
        /// <param name="lineNumber">The line in the file causing the message (set to zero if not available).</param>
        /// <param name="columnNumber">The column in the file causing the message (set to zero if not available).</param>
        /// <param name="endLineNumber">The last line of a range of lines in the file causing the message (set to zero if not available).</param>
        /// <param name="endColumnNumber">The last column of a range of columns in the file causing the message (set to zero if not available).</param>
        /// <param name="importance">Importance of the message.</param>
        /// <param name="message">The message string.</param>
        /// <param name="messageArgs">Optional arguments for formatting the message string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>message</c> is null.</exception>
        public void LogMessage
        (
            string subcategory,
            string code,
            string helpKeyword,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            MessageImportance importance,
            string message,
            params object[] messageArgs
        )
        {
            // No lock needed, as BuildEngine methods from v4.5 onwards are thread safe.
            ErrorUtilities.VerifyThrowArgumentNull(message, "message");

            // If BuildEngine is null, task attempted to log before it was set on it,
            // presumably in its constructor. This is not allowed, and all
            // we can do is throw.
            ErrorUtilities.VerifyThrowInvalidOperation(BuildEngine != null, "LoggingBeforeTaskInitialization", message);

            // If the task has missed out all location information, add the location of the task invocation;
            // that gives the user something.
            bool fillInLocation = (String.IsNullOrEmpty(file) && (lineNumber == 0) && (columnNumber == 0));

            BuildMessageEventArgs e = new BuildMessageEventArgs
                (
                    subcategory,
                    code,
                    fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                    fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                    fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                    endLineNumber,
                    endColumnNumber,
                    message,
                    helpKeyword,
                    TaskName,
                    importance,
                    DateTime.UtcNow,
                    messageArgs
                );

            BuildEngine.LogMessageEvent(e);
        }

        /// <summary>
        /// Logs a critical message using the specified string and other message details.
        /// Thread safe.
        /// </summary>
        /// <param name="subcategory">Description of the warning type (can be null).</param>
        /// <param name="code">Message code (can be null).</param>
        /// <param name="helpKeyword">The help keyword for the host IDE (can be null).</param>
        /// <param name="file">The path to the file causing the message (can be null).</param>
        /// <param name="lineNumber">The line in the file causing the message (set to zero if not available).</param>
        /// <param name="columnNumber">The column in the file causing the message (set to zero if not available).</param>
        /// <param name="endLineNumber">The last line of a range of lines in the file causing the message (set to zero if not available).</param>
        /// <param name="endColumnNumber">The last column of a range of columns in the file causing the message (set to zero if not available).</param>
        /// <param name="message">The message string.</param>
        /// <param name="messageArgs">Optional arguments for formatting the message string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>message</c> is null.</exception>
        public void LogCriticalMessage
        (
            string subcategory,
            string code,
            string helpKeyword,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message,
            params object[] messageArgs
        )
        {
            // No lock needed, as BuildEngine methods from v4.5 onwards are thread safe.
            ErrorUtilities.VerifyThrowArgumentNull(message, "message");

            // If BuildEngine is null, task attempted to log before it was set on it,
            // presumably in its constructor. This is not allowed, and all
            // we can do is throw.
            ErrorUtilities.VerifyThrowInvalidOperation(BuildEngine != null, "LoggingBeforeTaskInitialization", message);

            // If the task has missed out all location information, add the location of the task invocation;
            // that gives the user something.
            bool fillInLocation = (String.IsNullOrEmpty(file) && (lineNumber == 0) && (columnNumber == 0));

            CriticalBuildMessageEventArgs e = new CriticalBuildMessageEventArgs
                (
                    subcategory,
                    code,
                    fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                    fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                    fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                    endLineNumber,
                    endColumnNumber,
                    message,
                    helpKeyword,
                    TaskName,
                    DateTime.UtcNow,
                    messageArgs
                );

            BuildEngine.LogMessageEvent(e);
        }

        /// <summary>
        /// Logs a message using the specified resource string.
        /// Thread safe.
        /// </summary>
        /// <param name="messageResourceName">The name of the string resource to load.</param>
        /// <param name="messageArgs">Optional arguments for formatting the loaded string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>messageResourceName</c> is null.</exception>
        public void LogMessageFromResources(string messageResourceName, params object[] messageArgs)
        {
            // No lock needed, as the logging methods are thread safe and the rest does not modify
            // global state.
            //
            // This API is poorly designed, because parameters misordered like LogMessageFromResources(messageResourceName, MessageImportance.High)
            // will use this overload, ignore the importance and accidentally format the string.
            // Can't change it now as it's shipped, but this debug assert will help catch callers doing this.
            // Debug only because it is in theory legitimate to pass importance as a string format parameter.
            Debug.Assert(messageArgs == null || messageArgs.Length == 0 || messageArgs[0].GetType() != typeof(MessageImportance), "Did you call the wrong overload?");

            LogMessageFromResources(MessageImportance.Normal, messageResourceName, messageArgs);
        }

        /// <summary>
        /// Logs a message of the given importance using the specified resource string.
        /// Thread safe.
        /// </summary>
        /// <remarks>
        /// Take care to order the parameters correctly or the other overload will be called inadvertently.
        /// </remarks>
        /// <param name="importance">The importance level of the message.</param>
        /// <param name="messageResourceName">The name of the string resource to load.</param>
        /// <param name="messageArgs">Optional arguments for formatting the loaded string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>messageResourceName</c> is null.</exception>
        public void LogMessageFromResources(MessageImportance importance, string messageResourceName, params object[] messageArgs)
        {
            // No lock needed, as the logging methods are thread safe and the rest does not modify
            // global state.
            ErrorUtilities.VerifyThrowArgumentNull(messageResourceName, "messageResourceName");

            LogMessage(importance, FormatResourceString(messageResourceName, messageArgs));
#if _DEBUG
            // Assert that the message does not contain an error code.  Only errors and warnings
            // should have error codes.
            string errorCode;
            ResourceUtilities.ExtractMessageCode(true /* only msbuild codes */, FormatResourceString(messageResourceName, messageArgs), out errorCode);
            Debug.Assert(errorCode == null, errorCode, FormatResourceString(messageResourceName, messageArgs));
#endif
        }

        #endregion

        #region ExternalProjectStarted/Finished logging methods

        /// <summary>
        /// Small helper for logging the custom ExternalProjectStarted build event
        /// Thread safe.
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword</param>
        /// <param name="projectFile">project name</param>
        /// <param name="targetNames">targets we are going to build (empty indicates default targets)</param>
        public void LogExternalProjectStarted
        (
            string message,
            string helpKeyword,
            string projectFile,
            string targetNames
        )
        {
            // No lock needed, as BuildEngine methods from v4.5 onwards are thread safe.
            ExternalProjectStartedEventArgs eps = new ExternalProjectStartedEventArgs(message, helpKeyword, TaskName, projectFile, targetNames);
            BuildEngine.LogCustomEvent(eps);
        }

        /// <summary>
        /// Small helper for logging the custom ExternalProjectFinished build event.
        /// Thread safe.
        /// </summary>
        /// <param name="message">text message</param>
        /// <param name="helpKeyword">help keyword</param>
        /// <param name="projectFile">project name</param>
        /// <param name="succeeded">true indicates project built successfully</param>
        public void LogExternalProjectFinished
        (
            string message,
            string helpKeyword,
            string projectFile,
            bool succeeded
        )
        {
            // No lock needed, as BuildEngine methods from v4.5 onwards are thread safe.
            ExternalProjectFinishedEventArgs epf = new ExternalProjectFinishedEventArgs(message, helpKeyword, TaskName, projectFile, succeeded);
            BuildEngine.LogCustomEvent(epf);
        }

        #endregion

        #region Command line logging methods

        /// <summary>
        /// Logs the command line for a task's underlying tool/executable/shell command.
        /// Thread safe.
        /// </summary>
        /// <param name="commandLine">The command line string.</param>
        public void LogCommandLine(string commandLine)
        {
            LogCommandLine(MessageImportance.Low, commandLine);
        }

        /// <summary>
        /// Logs the command line for a task's underlying tool/executable/shell
        /// command, using the given importance level.
        /// Thread safe.
        /// </summary>
        /// <param name="importance">The importance level of the command line.</param>
        /// <param name="commandLine">The command line string.</param>
        public void LogCommandLine(MessageImportance importance, string commandLine)
        {
            // No lock needed, as BuildEngine methods from v4.5 onwards are thread safe.
            ErrorUtilities.VerifyThrowArgumentNull(commandLine, "commandLine");

            TaskCommandLineEventArgs e = new TaskCommandLineEventArgs(commandLine, TaskName, importance);

            // If BuildEngine is null, the task attempted to log before it was set on it,
            // presumably in its constructor. This is not allowed, and all we can do is throw.
            if (BuildEngine == null)
            {
                // Do not use Verify[...] as it would read e.Message ahead of time
                ErrorUtilities.ThrowInvalidOperation("LoggingBeforeTaskInitialization", e.Message);
            }


            BuildEngine.LogMessageEvent(e);
        }

        #endregion

        #region Error logging methods

        /// <summary>
        /// Logs an error using the specified string.
        /// Thread safe.
        /// </summary>
        /// <param name="message">The message string.</param>
        /// <param name="messageArgs">Optional arguments for formatting the message string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>message</c> is null.</exception>
        public void LogError(string message, params object[] messageArgs)
        {
            LogError(null, null, null, null, 0, 0, 0, 0, message, messageArgs);
        }

        /// <summary>
        /// Logs an error using the specified string and other error details.
        /// Thread safe.
        /// </summary>
        /// <param name="subcategory">Description of the error type (can be null).</param>
        /// <param name="errorCode">The error code (can be null).</param>
        /// <param name="helpKeyword">The help keyword for the host IDE (can be null).</param>
        /// <param name="file">The path to the file containing the error (can be null).</param>
        /// <param name="lineNumber">The line in the file where the error occurs (set to zero if not available).</param>
        /// <param name="columnNumber">The column in the file where the error occurs (set to zero if not available).</param>
        /// <param name="endLineNumber">The last line of a range of lines in the file where the error occurs (set to zero if not available).</param>
        /// <param name="endColumnNumber">The last column of a range of columns in the file where the error occurs (set to zero if not available).</param>
        /// <param name="message">The message string.</param>
        /// <param name="messageArgs">Optional arguments for formatting the message string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>message</c> is null.</exception>
        public void LogError
        (
            string subcategory,
            string errorCode,
            string helpKeyword,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message,
            params object[] messageArgs
        )
        {
            // No lock needed, as BuildEngine methods from v4.5 onwards are thread safe.
            ErrorUtilities.VerifyThrowArgumentNull(message, "message");

            // If BuildEngine is null, task attempted to log before it was set on it,
            // presumably in its constructor. This is not allowed, and all
            // we can do is throw.
            ErrorUtilities.VerifyThrowInvalidOperation(BuildEngine != null, "LoggingBeforeTaskInitialization", message);

#if false
            // All of our errors should have an error code, so the user has something
            // to look up in the documentation. To help find errors without error codes,
            // temporarily uncomment this line and run the unit tests.
            //if (null == errorCode) File.AppendAllText("c:\\errorsWithoutCodes", message + "\n");
            // We don't have a Debug.Assert for this, because it would be triggered by <Error> and <Warning> tags.
#endif

            // If the task has missed out all location information, add the location of the task invocation;
            // that gives the user something.
            bool fillInLocation = (String.IsNullOrEmpty(file) && (lineNumber == 0) && (columnNumber == 0));

            BuildErrorEventArgs e = new BuildErrorEventArgs
                (
                    subcategory,
                    errorCode,
                    fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                    fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                    fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                    endLineNumber,
                    endColumnNumber,
                    message,
                    helpKeyword,
                    TaskName,
                    DateTime.UtcNow,
                    messageArgs
                );
            BuildEngine.LogErrorEvent(e);

            _hasLoggedErrors = true;
        }

        /// <summary>
        /// Logs an error using the specified resource string.
        /// Thread safe.
        /// </summary>
        /// <param name="messageResourceName">The name of the string resource to load.</param>
        /// <param name="messageArgs">Optional arguments for formatting the loaded string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>messageResourceName</c> is null.</exception>
        public void LogErrorFromResources(string messageResourceName, params object[] messageArgs)
        {
            LogErrorFromResources(null, null, null, null, 0, 0, 0, 0, messageResourceName, messageArgs);
        }

        /// <summary>
        /// Logs an error using the specified resource string and other error details.
        /// Thread safe.
        /// </summary>
        /// <param name="subcategoryResourceName">The name of the string resource that describes the error type (can be null).</param>
        /// <param name="errorCode">The error code (can be null).</param>
        /// <param name="helpKeyword">The help keyword for the host IDE (can be null).</param>
        /// <param name="file">The path to the file containing the error (can be null).</param>
        /// <param name="lineNumber">The line in the file where the error occurs (set to zero if not available).</param>
        /// <param name="columnNumber">The column in the file where the error occurs (set to zero if not available).</param>
        /// <param name="endLineNumber">The last line of a range of lines in the file where the error occurs (set to zero if not available).</param>
        /// <param name="endColumnNumber">The last column of a range of columns in the file where the error occurs (set to zero if not available).</param>
        /// <param name="messageResourceName">The name of the string resource containing the error message.</param>
        /// <param name="messageArgs">Optional arguments for formatting the loaded string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>messageResourceName</c> is null.</exception>
        public void LogErrorFromResources
        (
            string subcategoryResourceName,
            string errorCode,
            string helpKeyword,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string messageResourceName,
            params object[] messageArgs
        )
        {
            // No lock needed, as the logging methods are thread safe and the rest does not modify
            // global state.
            ErrorUtilities.VerifyThrowArgumentNull(messageResourceName, "messageResourceName");

            string subcategory = null;

            if (subcategoryResourceName != null)
            {
                subcategory = FormatResourceString(subcategoryResourceName);
            }

#if _DEBUG
            // If the message does have a message code, LogErrorWithCodeFromResources
            // should have been called instead, so that the errorCode field gets populated.
            // Check this only in debug, to avoid the cost of attempting to extract a
            // message code when there probably isn't one.
            string messageCode;
            string throwAwayMessageBody = ResourceUtilities.ExtractMessageCode(true /* only msbuild codes */, FormatResourceString(messageResourceName, messageArgs), out messageCode);
            Debug.Assert(messageCode == null || messageCode.Length == 0, "Called LogErrorFromResources instead of LogErrorWithCodeFromResources, but message '" + throwAwayMessageBody + "' does have an error code '" + messageCode + "'");
#endif

            LogError
            (
                subcategory,
                errorCode,
                helpKeyword,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                FormatResourceString(messageResourceName, messageArgs)
            );
        }

        /// <summary>
        /// Logs an error using the specified resource string. 
        /// If the message has an error code prefixed to it, the code is extracted and logged with the message. If a help keyword
        /// prefix has been provided, a help keyword for the host IDE is also logged with the message. The help keyword is
        /// composed by appending the string resource name to the prefix.
        /// 
        /// A task can provide a help keyword prefix either via the Task (or TaskMarshalByRef) base class constructor, or the
        /// Task.HelpKeywordPrefix (or AppDomainIsolatedTask.HelpKeywordPrefix) property.
        ///    
        /// Thread safe.
        /// </summary>
        /// <param name="messageResourceName">The name of the string resource to load.</param>
        /// <param name="messageArgs">Optional arguments for formatting the loaded string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>messageResourceName</c> is null.</exception>
        public void LogErrorWithCodeFromResources(string messageResourceName, params object[] messageArgs)
        {
            LogErrorWithCodeFromResources(null, null, 0, 0, 0, 0, messageResourceName, messageArgs);
        }

        /// <summary>
        /// Logs an error using the specified resource string and other error details. 
        /// If the message has an error code prefixed, the code is extracted and logged with the message. If a
        /// help keyword prefix has been provided, a help keyword for the host IDE is also logged with the message. The help
        /// keyword is composed by appending the error message resource string name to the prefix.
        /// 
        /// A task can provide a help keyword prefix either via the Task (or TaskMarshalByRef) base class constructor, or the
        /// Task.HelpKeywordPrefix (or AppDomainIsolatedTask.HelpKeywordPrefix) property.
        ///    
        /// Thread safe.
        /// </summary>
        /// <param name="subcategoryResourceName">The name of the string resource that describes the error type (can be null).</param>
        /// <param name="file">The path to the file containing the error (can be null).</param>
        /// <param name="lineNumber">The line in the file where the error occurs (set to zero if not available).</param>
        /// <param name="columnNumber">The column in the file where the error occurs (set to zero if not available).</param>
        /// <param name="endLineNumber">The last line of a range of lines in the file where the error occurs (set to zero if not available).</param>
        /// <param name="endColumnNumber">The last column of a range of columns in the file where the error occurs (set to zero if not available).</param>
        /// <param name="messageResourceName">The name of the string resource containing the error message.</param>
        /// <param name="messageArgs">Optional arguments for formatting the loaded string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>messageResourceName</c> is null.</exception>
        public void LogErrorWithCodeFromResources
        (
            string subcategoryResourceName,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string messageResourceName,
            params object[] messageArgs
        )
        {
            // No lock needed, as the logging methods are thread safe and the rest does not modify
            // global state.
            ErrorUtilities.VerifyThrowArgumentNull(messageResourceName, "messageResourceName");

            string subcategory = null;

            if (subcategoryResourceName != null)
            {
                subcategory = FormatResourceString(subcategoryResourceName);
            }

            string errorCode;
            string message = ResourceUtilities.ExtractMessageCode(false /* all codes */, FormatResourceString(messageResourceName, messageArgs), out errorCode);

            string helpKeyword = null;

            if (HelpKeywordPrefix != null)
            {
                helpKeyword = HelpKeywordPrefix + messageResourceName;
            }

            LogError
            (
                subcategory,
                errorCode,
                helpKeyword,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message
            );
        }

        /// <summary>
        /// Logs an error using the message from the given exception context.
        /// No callstack will be shown.
        /// Thread safe.
        /// </summary>
        /// <param name="exception">Exception to log.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>e</c> is null.</exception>
        public void LogErrorFromException(Exception exception)
        {
            LogErrorFromException(exception, false);
        }

        /// <summary>
        /// Logs an error using the message (and optionally the stack-trace) from the given exception context.
        /// Thread safe.
        /// </summary>
        /// <param name="exception">Exception to log.</param>
        /// <param name="showStackTrace">If true, callstack will be appended to message.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>exception</c> is null.</exception>
        public void LogErrorFromException(Exception exception, bool showStackTrace)
        {
            LogErrorFromException(exception, showStackTrace, false, null);
        }

        /// <summary>
        /// Logs an error using the message, and optionally the stack-trace from the given exception, and
        /// optionally inner exceptions too.
        /// Thread safe.
        /// </summary>
        /// <param name="exception">Exception to log.</param>
        /// <param name="showStackTrace">If true, callstack will be appended to message.</param>
        /// <param name="showDetail">Whether to log exception types and any inner exceptions.</param>
        /// <param name="file">File related to the exception, or null if the project file should be logged</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>exception</c> is null.</exception>
        public void LogErrorFromException(Exception exception, bool showStackTrace, bool showDetail, string file)
        {
            // No lock needed, as the logging methods are thread safe and the rest does not modify
            // global state.
            ErrorUtilities.VerifyThrowArgumentNull(exception, "exception");

            string message = null;

            if (!showDetail && (Environment.GetEnvironmentVariable("MSBUILDDIAGNOSTICS") == null)) // This env var is also used in ToolTask
            {
                message = exception.Message;

                if (showStackTrace)
                {
                    message += Environment.NewLine + exception.StackTrace;
                }
            }
            else
            {
                // The more comprehensive output, showing exception types
                // and inner exceptions
                StringBuilder builder = new StringBuilder(200);
                do
                {
                    builder.Append(exception.GetType().Name);
                    builder.Append(": ");
                    builder.AppendLine(exception.Message);
                    if (showStackTrace)
                    {
                        builder.AppendLine(exception.StackTrace);
                    }
                    exception = exception.InnerException;
                } while (exception != null);

                message = builder.ToString();
            }

            LogError(null, null, null, file, 0, 0, 0, 0, message);
        }

        #endregion

        #region Warning logging methods

        /// <summary>
        /// Logs a warning using the specified string.
        /// Thread safe.
        /// </summary>
        /// <param name="message">The message string.</param>
        /// <param name="messageArgs">Optional arguments for formatting the message string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>message</c> is null.</exception>
        public void LogWarning(string message, params object[] messageArgs)
        {
            LogWarning(null, null, null, null, 0, 0, 0, 0, message, messageArgs);
        }

        /// <summary>
        /// Logs a warning using the specified string and other warning details.
        /// Thread safe.
        /// </summary>
        /// <param name="subcategory">Description of the warning type (can be null).</param>
        /// <param name="warningCode">The warning code (can be null).</param>
        /// <param name="helpKeyword">The help keyword for the host IDE (can be null).</param>
        /// <param name="file">The path to the file causing the warning (can be null).</param>
        /// <param name="lineNumber">The line in the file causing the warning (set to zero if not available).</param>
        /// <param name="columnNumber">The column in the file causing the warning (set to zero if not available).</param>
        /// <param name="endLineNumber">The last line of a range of lines in the file causing the warning (set to zero if not available).</param>
        /// <param name="endColumnNumber">The last column of a range of columns in the file causing the warning (set to zero if not available).</param>
        /// <param name="message">The message string.</param>
        /// <param name="messageArgs">Optional arguments for formatting the message string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>message</c> is null.</exception>
        public void LogWarning
        (
            string subcategory,
            string warningCode,
            string helpKeyword,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string message,
            params object[] messageArgs
        )
        {
            // No lock needed, as BuildEngine methods from v4.5 onwards are thread safe.
            ErrorUtilities.VerifyThrowArgumentNull(message, "message");

            // If BuildEngine is null, task attempted to log before it was set on it,
            // presumably in its constructor. This is not allowed, and all
            // we can do is throw.
            ErrorUtilities.VerifyThrowInvalidOperation(BuildEngine != null, "LoggingBeforeTaskInitialization", message);

#if false
            // All of our warnings should have an error code, so the user has something
            // to look up in the documentation. To help find warnings without error codes,
            // temporarily uncomment this line and run the unit tests.
            //if (null == warningCode) File.AppendAllText("c:\\warningsWithoutCodes", message + "\n");
            // We don't have a Debug.Assert for this, because it would be triggered by <Error> and <Warning> tags.
#endif

            // If the task has missed out all location information, add the location of the task invocation;
            // that gives the user something.
            bool fillInLocation = (String.IsNullOrEmpty(file) && (lineNumber == 0) && (columnNumber == 0));

            BuildWarningEventArgs e = new BuildWarningEventArgs
                (
                    subcategory,
                    warningCode,
                    fillInLocation ? BuildEngine.ProjectFileOfTaskNode : file,
                    fillInLocation ? BuildEngine.LineNumberOfTaskNode : lineNumber,
                    fillInLocation ? BuildEngine.ColumnNumberOfTaskNode : columnNumber,
                    endLineNumber,
                    endColumnNumber,
                    message,
                    helpKeyword,
                    TaskName,
                    DateTime.UtcNow,
                    messageArgs
                );

            BuildEngine.LogWarningEvent(e);
        }

        /// <summary>
        /// Logs a warning using the specified resource string.
        /// Thread safe.
        /// </summary>
        /// <param name="messageResourceName">The name of the string resource to load.</param>
        /// <param name="messageArgs">Optional arguments for formatting the loaded string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>messageResourceName</c> is null.</exception>
        public void LogWarningFromResources(string messageResourceName, params object[] messageArgs)
        {
            LogWarningFromResources(null, null, null, null, 0, 0, 0, 0, messageResourceName, messageArgs);
        }

        /// <summary>
        /// Logs a warning using the specified resource string and other warning details.
        /// Thread safe.
        /// </summary>
        /// <param name="subcategoryResourceName">The name of the string resource that describes the warning type (can be null).</param>
        /// <param name="warningCode">The warning code (can be null).</param>
        /// <param name="helpKeyword">The help keyword for the host IDE (can be null).</param>
        /// <param name="file">The path to the file causing the warning (can be null).</param>
        /// <param name="lineNumber">The line in the file causing the warning (set to zero if not available).</param>
        /// <param name="columnNumber">The column in the file causing the warning (set to zero if not available).</param>
        /// <param name="endLineNumber">The last line of a range of lines in the file causing the warning (set to zero if not available).</param>
        /// <param name="endColumnNumber">The last column of a range of columns in the file causing the warning (set to zero if not available).</param>
        /// <param name="messageResourceName">The name of the string resource containing the warning message.</param>
        /// <param name="messageArgs">Optional arguments for formatting the loaded string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>messageResourceName</c> is null.</exception>
        public void LogWarningFromResources
        (
            string subcategoryResourceName,
            string warningCode,
            string helpKeyword,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string messageResourceName,
            params object[] messageArgs
        )
        {
            // No lock needed, as log methods are thread safe and the rest does not modify
            // global state.
            ErrorUtilities.VerifyThrowArgumentNull(messageResourceName, "messageResourceName");

            string subcategory = null;

            if (subcategoryResourceName != null)
            {
                subcategory = FormatResourceString(subcategoryResourceName);
            }

#if DEBUG
            // If the message does have a message code, LogWarningWithCodeFromResources
            // should have been called instead, so that the errorCode field gets populated.
            // Check this only in debug, to avoid the cost of attempting to extract a
            // message code when there probably isn't one.
            string messageCode;
            string throwAwayMessageBody = ResourceUtilities.ExtractMessageCode(true /* only msbuild codes */, FormatResourceString(messageResourceName, messageArgs), out messageCode);
            Debug.Assert(messageCode == null || messageCode.Length == 0, "Called LogWarningFromResources instead of LogWarningWithCodeFromResources, but message '" + throwAwayMessageBody + "' does have an error code '" + messageCode + "'");
#endif

            LogWarning
            (
                subcategory,
                warningCode,
                helpKeyword,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                FormatResourceString(messageResourceName, messageArgs)
            );
        }

        /// <summary>
        /// Logs a warning using the specified resource string. 
        /// If the message has a warning code prefixed to it, the code is extracted and logged with the message. If a help keyword
        /// prefix has been provided, a help keyword for the host IDE is also logged with the message. The help keyword is
        /// composed by appending the string resource name to the prefix.
        /// 
        /// A task can provide a help keyword prefix either via the Task (or TaskMarshalByRef) base class constructor, or the
        /// Task.HelpKeywordPrefix (or AppDomainIsolatedTask.HelpKeywordPrefix) property.
        /// 
        /// Thread safe.
        /// </summary>
        /// <param name="messageResourceName">The name of the string resource to load.</param>
        /// <param name="messageArgs">Optional arguments for formatting the loaded string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>messageResourceName</c> is null.</exception>
        public void LogWarningWithCodeFromResources(string messageResourceName, params object[] messageArgs)
        {
            LogWarningWithCodeFromResources(null, null, 0, 0, 0, 0, messageResourceName, messageArgs);
        }

        /// <summary>
        /// Logs a warning using the specified resource string and other warning details. 
        /// If the message has a warning code, the code is extracted and logged with the message.
        /// If a help keyword prefix has been provided, a help keyword for the host IDE is also logged with the message. The help
        /// keyword is composed by appending the warning message resource string name to the prefix.
        /// 
        /// A task can provide a help keyword prefix either via the Task (or TaskMarshalByRef) base class constructor, or the
        /// Task.HelpKeywordPrefix (or AppDomainIsolatedTask.HelpKeywordPrefix) property.
        /// 
        /// Thread safe.
        /// </summary>
        /// <param name="subcategoryResourceName">The name of the string resource that describes the warning type (can be null).</param>
        /// <param name="file">The path to the file causing the warning (can be null).</param>
        /// <param name="lineNumber">The line in the file causing the warning (set to zero if not available).</param>
        /// <param name="columnNumber">The column in the file causing the warning (set to zero if not available).</param>
        /// <param name="endLineNumber">The last line of a range of lines in the file causing the warning (set to zero if not available).</param>
        /// <param name="endColumnNumber">The last column of a range of columns in the file causing the warning (set to zero if not available).</param>
        /// <param name="messageResourceName">The name of the string resource containing the warning message.</param>
        /// <param name="messageArgs">Optional arguments for formatting the loaded string.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>messageResourceName</c> is null.</exception>
        public void LogWarningWithCodeFromResources
        (
            string subcategoryResourceName,
            string file,
            int lineNumber,
            int columnNumber,
            int endLineNumber,
            int endColumnNumber,
            string messageResourceName,
            params object[] messageArgs
        )
        {
            // No lock needed, as log methods are thread safe and the rest does not modify
            // global state.
            ErrorUtilities.VerifyThrowArgumentNull(messageResourceName, "messageResourceName");

            string subcategory = null;

            if (subcategoryResourceName != null)
            {
                subcategory = FormatResourceString(subcategoryResourceName);
            }

            string warningCode;
            string message = ResourceUtilities.ExtractMessageCode(false /* all codes */, FormatResourceString(messageResourceName, messageArgs), out warningCode);

            string helpKeyword = null;

            if (HelpKeywordPrefix != null)
            {
                helpKeyword = HelpKeywordPrefix + messageResourceName;
            }

            LogWarning
            (
                subcategory,
                warningCode,
                helpKeyword,
                file,
                lineNumber,
                columnNumber,
                endLineNumber,
                endColumnNumber,
                message
            );
        }

        /// <summary>
        /// Logs a warning using the message from the given exception context.
        /// Thread safe.
        /// </summary>
        /// <param name="exception">Exception to log.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>exception</c> is null.</exception>
        public void LogWarningFromException(Exception exception)
        {
            LogWarningFromException(exception, false);
        }

        /// <summary>
        /// Logs a warning using the message (and optionally the stack-trace) from the given exception context.
        /// Thread safe.
        /// </summary>
        /// <param name="exception">Exception to log.</param>
        /// <param name="showStackTrace">If true, the exception callstack is appended to the message.</param>
        /// <exception cref="ArgumentNullException">Thrown when <c>exception</c> is null.</exception>
        public void LogWarningFromException(Exception exception, bool showStackTrace)
        {
            // No lock needed, as log methods are thread safe and the rest does not modify
            // global state.
            ErrorUtilities.VerifyThrowArgumentNull(exception, "exception");

            string message = exception.Message;

            if (showStackTrace)
            {
                message += Environment.NewLine + exception.StackTrace;
            }

            LogWarning(message);
        }

        #endregion

        #region Bulk logging methods

        /// <summary>
        /// Logs errors/warnings/messages for each line of text in the given file. Errors/warnings are only logged for lines that
        /// fit a particular (canonical) format -- the remaining lines are treated as messages.
        /// Thread safe.
        /// </summary>
        /// <param name="fileName">The file to log from.</param>
        /// <returns>true, if any errors were logged</returns>
        /// <exception cref="ArgumentNullException">Thrown when <c>filename</c> is null.</exception>
        public bool LogMessagesFromFile(string fileName)
        {
            return LogMessagesFromFile(fileName, MessageImportance.Low);
        }

        /// <summary>
        /// Logs errors/warnings/messages for each line of text in the given file. Errors/warnings are only logged for lines that
        /// fit a particular (canonical) format -- the remaining lines are treated as messages.
        /// Thread safe.
        /// </summary>
        /// <param name="fileName">The file to log from.</param>
        /// <param name="messageImportance">The importance level for messages that are neither errors nor warnings.</param>
        /// <returns>true, if any errors were logged</returns>
        /// <exception cref="ArgumentNullException">Thrown when <c>filename</c> is null.</exception>
        public bool LogMessagesFromFile(string fileName, MessageImportance messageImportance)
        {
            // No lock needed, as log methods are thread safe and the rest does not modify
            // global state.
            ErrorUtilities.VerifyThrowArgumentNull(fileName, "fileName");

            bool errorsFound = false;

            // Command-line tools are generally going to emit their output using the current 
            // codepage, so that it displays correctly in the console window.  
            using (StreamReader fileStream = new StreamReader(fileName, System.Text.Encoding.Default)) // HIGHCHAR: Use ANSI for logging messages.
            {
                errorsFound = LogMessagesFromStream(fileStream, messageImportance);
            }

            return errorsFound;
        }

        /// <summary>
        /// Logs errors/warnings/messages for each line of text in the given stream. Errors/warnings are only logged for lines
        /// that fit a particular (canonical) format -- the remaining lines are treated as messages.
        /// Thread safe.
        /// </summary>
        /// <param name="stream">The stream to log from.</param>
        /// <param name="messageImportance">The importance level for messages that are neither errors nor warnings.</param>
        /// <returns>true, if any errors were logged</returns>
        /// <exception cref="ArgumentNullException">Thrown when <c>stream</c> is null.</exception>
        public bool LogMessagesFromStream(TextReader stream, MessageImportance messageImportance)
        {
            // No lock needed, as log methods are thread safe and the rest does not modify
            // global state.
            ErrorUtilities.VerifyThrowArgumentNull(stream, "stream");

            bool errorsFound = false;
            string lineOfText;

            // ReadLine() blocks until either A.) there is a complete line of text to be read from
            // the stream, or B.) the stream is closed/done/finit/gone/byebye.
            while ((lineOfText = stream.ReadLine()) != null)
            {
                errorsFound |= LogMessageFromText(lineOfText, messageImportance);
            }

            return errorsFound;
        }

        /// <summary>
        /// Logs an error/warning/message from the given line of text. Errors/warnings are only logged for lines that fit a
        /// particular (canonical) format -- all other lines are treated as messages.
        /// Thread safe.
        /// </summary>
        /// <param name="lineOfText">The line of text to log from.</param>
        /// <param name="messageImportance">The importance level for messages that are neither errors nor warnings.</param>
        /// <returns>true, if an error was logged</returns>
        /// <exception cref="ArgumentNullException">Thrown when <c>lineOfText</c> is null.</exception>
        public bool LogMessageFromText(string lineOfText, MessageImportance messageImportance)
        {
            // No lock needed, as log methods are thread safe and the rest does not modify
            // global state.
            ErrorUtilities.VerifyThrowArgumentNull(lineOfText, "lineOfText");

            bool isError = false;
            CanonicalError.Parts messageParts = CanonicalError.Parse(lineOfText);

            if (null == messageParts)
            {
                // Line was not recognized as a canonical error. Log it as a message.
                LogMessage(messageImportance, lineOfText);
            }
            else
            {
                // The message was in Canonical format.
                //  Log it as a warning or error.
                string origin = messageParts.origin;

                if ((origin == null) || (origin.Length == 0))
                {
                    // Use the task class name as the origin, if none specified.
                    origin = TaskNameUpperCase;
                }

                switch (messageParts.category)
                {
                    case CanonicalError.Parts.Category.Error:
                        {
                            LogError
                            (
                                messageParts.subcategory,
                                messageParts.code,
                                null,
                                origin,
                                messageParts.line,
                                messageParts.column,
                                messageParts.endLine,
                                messageParts.endColumn,
                                messageParts.text
                            );

                            isError = true;
                            break;
                        }

                    case CanonicalError.Parts.Category.Warning:
                        {
                            LogWarning
                            (
                                messageParts.subcategory,
                                messageParts.code,
                                null,
                                origin,
                                messageParts.line,
                                messageParts.column,
                                messageParts.endLine,
                                messageParts.endColumn,
                                messageParts.text
                            );

                            break;
                        }

                    default:
                        ErrorUtilities.VerifyThrow(false, "Impossible canonical part.");
                        break;
                }
            }

            return isError;
        }

        #endregion

        #region AppDomain Code

        /// <summary>
        /// InitializeLifetimeService is called when the remote object is activated. 
        /// This method will determine how long the lifetime for the object will be.
        /// Thread safe. However, InitializeLifetimeService and MarkAsInactive should
        /// only be called in that order, together or not at all, and no more than once.
        /// </summary>
        /// <returns>The lease object to control this object's lifetime.</returns>
        public override object InitializeLifetimeService()
        {
            lock (_locker)
            {
                // Each MarshalByRef object has a reference to the service which
                // controls how long the remote object will stay around
                ILease lease = (ILease)base.InitializeLifetimeService();

                // Set how long a lease should be initially. Once a lease expires
                // the remote object will be disconnected and it will be marked as being available
                // for garbage collection
                int initialLeaseTime = 1;

                string initialLeaseTimeFromEnvironment = Environment.GetEnvironmentVariable("MSBUILDTASKLOGGINGHELPERINITIALLEASETIME");

                if (!String.IsNullOrEmpty(initialLeaseTimeFromEnvironment))
                {
                    int leaseTimeFromEnvironment;
                    if (int.TryParse(initialLeaseTimeFromEnvironment, out leaseTimeFromEnvironment) && leaseTimeFromEnvironment > 0)
                    {
                        initialLeaseTime = leaseTimeFromEnvironment;
                    }
                }

                lease.InitialLeaseTime = TimeSpan.FromMinutes(initialLeaseTime);

                // Make a new client sponsor. A client sponsor is a class
                // which will respond to a lease renewal request and will
                // increase the lease time allowing the object to stay in memory
                _sponsor = new ClientSponsor();

                // When a new lease is requested lets make it last 1 minutes longer. 
                int leaseExtensionTime = 1;

                string leaseExtensionTimeFromEnvironment = Environment.GetEnvironmentVariable("MSBUILDTASKLOGGINGHELPERLEASEEXTENSIONTIME");
                if (!String.IsNullOrEmpty(leaseExtensionTimeFromEnvironment))
                {
                    int leaseExtensionFromEnvironment;
                    if (int.TryParse(leaseExtensionTimeFromEnvironment, out leaseExtensionFromEnvironment) && leaseExtensionFromEnvironment > 0)
                    {
                        leaseExtensionTime = leaseExtensionFromEnvironment;
                    }
                }

                _sponsor.RenewalTime = TimeSpan.FromMinutes(leaseExtensionTime);

                // Register the sponsor which will increase lease timeouts when the lease expires
                lease.Register(_sponsor);

                return lease;
            }
        }

        /// <summary>
        /// Notifies this object that its work is done.
        /// Thread safe. However, InitializeLifetimeService and MarkAsInactive should
        /// only be called in that order, together or not at all, and no more than once.
        /// </summary>
        /// <remarks>
        /// Indicates to the TaskLoggingHelper that it is no longer needed.
        /// </remarks>
        public void MarkAsInactive()
        {
            lock (_locker)
            {
                // Clear out the sponsor (who is responsible for keeping the TaskLoggingHelper remoting lease alive until the task is done)
                // this will be null if the engineproxy was never sent across an appdomain boundary.
                if (_sponsor != null)
                {
                    ILease lease = (ILease)RemotingServices.GetLifetimeService(this);

                    if (lease != null)
                    {
                        lease.Unregister(_sponsor);
                    }

                    _sponsor.Close();
                    _sponsor = null;
                }
            }
        }

        #endregion
    }
}
