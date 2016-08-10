﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /***************************************************************************
     * 
     * Class:       MockEngine
     * 
     * In order to execute tasks, we have to pass in an Engine object, so the
     * task can log events.  It doesn't have to be the real Engine object, just
     * something that implements the IBuildEngine4 interface.  So, we mock up
     * a fake engine object here, so we're able to execute tasks from the unit tests.
     * 
     * The unit tests could have instantiated the real Engine object, but then
     * we would have had to take a reference onto the Microsoft.Build.Engine assembly, which
     * is somewhat of a no-no for task assemblies.
     * 
     **************************************************************************/
    sealed internal class MockEngine : IBuildEngine4
    {
        private bool _isRunningMultipleNodes;
        private int _messages = 0;
        private int _warnings = 0;
        private int _errors = 0;
        private string _log = "";
        private string _upperLog = null;
        private ProjectCollection _projectCollection = new ProjectCollection();
        private bool _logToConsole = false;
        private MockLogger _mockLogger = null;
        private Dictionary<object, object> _objectCashe = new Dictionary<object, object>();

        internal MockEngine() : this(false)
        {
        }

        internal int Messages
        {
            set { _messages = value; }
            get { return _messages; }
        }

        internal int Warnings
        {
            set { _warnings = value; }
            get { return _warnings; }
        }

        internal int Errors
        {
            set { _errors = value; }
            get { return _errors; }
        }

        internal MockLogger MockLogger
        {
            get { return _mockLogger; }
        }

        public MockEngine(bool logToConsole)
        {
            _mockLogger = new MockLogger();
            _logToConsole = logToConsole;
        }


        public void LogErrorEvent(BuildErrorEventArgs eventArgs)
        {
            if (eventArgs.File != null && eventArgs.File.Length > 0)
            {
                if (_logToConsole)
                    Console.Write("{0}({1},{2}): ", eventArgs.File, eventArgs.LineNumber, eventArgs.ColumnNumber);
                _log += String.Format("{0}({1},{2}): ", eventArgs.File, eventArgs.LineNumber, eventArgs.ColumnNumber);
            }

            if (_logToConsole)
                Console.Write("ERROR " + eventArgs.Code + ": ");
            _log += "ERROR " + eventArgs.Code + ": ";
            ++_errors;

            if (_logToConsole)
                Console.WriteLine(eventArgs.Message);
            _log += eventArgs.Message;
            _log += "\n";
        }

        public void LogWarningEvent(BuildWarningEventArgs eventArgs)
        {
            if (eventArgs.File != null && eventArgs.File.Length > 0)
            {
                if (_logToConsole)
                    Console.Write("{0}({1},{2}): ", eventArgs.File, eventArgs.LineNumber, eventArgs.ColumnNumber);
                _log += String.Format("{0}({1},{2}): ", eventArgs.File, eventArgs.LineNumber, eventArgs.ColumnNumber);
            }

            if (_logToConsole)
                Console.Write("WARNING " + eventArgs.Code + ": ");
            _log += "WARNING " + eventArgs.Code + ": ";
            ++_warnings;

            if (_logToConsole)
                Console.WriteLine(eventArgs.Message);
            _log += eventArgs.Message;
            _log += "\n";
        }

        public void LogCustomEvent(CustomBuildEventArgs eventArgs)
        {
            if (_logToConsole)
                Console.WriteLine(eventArgs.Message);
            _log += eventArgs.Message;
            _log += "\n";
        }

        public void LogMessageEvent(BuildMessageEventArgs eventArgs)
        {
            if (_logToConsole)
                Console.WriteLine(eventArgs.Message);
            _log += eventArgs.Message;
            _log += "\n";
            ++_messages;
        }

        public bool ContinueOnError
        {
            get
            {
                return false;
            }
        }

        public string ProjectFileOfTaskNode
        {
            get
            {
                return String.Empty;
            }
        }

        public int LineNumberOfTaskNode
        {
            get
            {
                return 0;
            }
        }

        public int ColumnNumberOfTaskNode
        {
            get
            {
                return 0;
            }
        }

        internal string Log
        {
            set { _log = value; }
            get { return _log; }
        }

        public bool IsRunningMultipleNodes
        {
            get { return _isRunningMultipleNodes; }
            set { _isRunningMultipleNodes = value; }
        }

        public bool BuildProjectFile
            (
            string projectFileName,
            string[] targetNames,
            IDictionary globalPropertiesPassedIntoTask,
            IDictionary targetOutputs
            )
        {
            ILogger[] loggers = new ILogger[2] { _mockLogger, new ConsoleLogger() };

            return this.BuildProjectFile(projectFileName, targetNames, globalPropertiesPassedIntoTask, targetOutputs, null);
        }

        public bool BuildProjectFile
            (
            string projectFileName,
            string[] targetNames,
            IDictionary globalPropertiesPassedIntoTask,
            IDictionary targetOutputs,
            string toolsVersion
            )
        {
            Dictionary<string, string> finalGlobalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            // Finally, whatever global properties were passed into the task ... those are the final winners.
            if (globalPropertiesPassedIntoTask != null)
            {
                foreach (DictionaryEntry newGlobalProperty in globalPropertiesPassedIntoTask)
                {
                    finalGlobalProperties[(string)newGlobalProperty.Key] = (string)newGlobalProperty.Value;
                }
            }

            Project project = _projectCollection.LoadProject(projectFileName, finalGlobalProperties, toolsVersion);

            ILogger[] loggers = new ILogger[2] { _mockLogger, new ConsoleLogger() };

            return project.Build(targetNames, loggers);
        }

        public bool BuildProjectFilesInParallel
        (
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IDictionary[] targetOutputsPerProject,
            string[] toolsVersion,
            bool useResultsCache,
            bool unloadProjectsOnCompletion
        )
        {
            bool includeTargetOutputs = targetOutputsPerProject != null;

            BuildEngineResult result = BuildProjectFilesInParallel(projectFileNames, targetNames, globalProperties, new List<String>[projectFileNames.Length], toolsVersion, includeTargetOutputs);

            if (includeTargetOutputs)
            {
                for (int i = 0; i < targetOutputsPerProject.Length; i++)
                {
                    if (targetOutputsPerProject[i] != null)
                    {
                        foreach (KeyValuePair<string, ITaskItem[]> output in result.TargetOutputsPerProject[i])
                        {
                            targetOutputsPerProject[i].Add(output.Key, output.Value);
                        }
                    }
                }
            }

            return result.Result;
        }

        public BuildEngineResult BuildProjectFilesInParallel
        (
            string[] projectFileNames,
            string[] targetNames,
            IDictionary[] globalProperties,
            IList<string>[] undefineProperties,
            string[] toolsVersion,
            bool returnTargetOutputs
        )
        {
            List<IDictionary<string, ITaskItem[]>> targetOutputsPerProject = null;

            ILogger[] loggers = new ILogger[2] { _mockLogger, new ConsoleLogger() };

            bool allSucceeded = true;

            if (returnTargetOutputs)
            {
                targetOutputsPerProject = new List<IDictionary<string, ITaskItem[]>>();
            }

            for (int i = 0; i < projectFileNames.Length; i++)
            {
                Dictionary<string, string> finalGlobalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (globalProperties[i] != null)
                {
                    foreach (DictionaryEntry newGlobalProperty in globalProperties[i])
                    {
                        finalGlobalProperties[(string)newGlobalProperty.Key] = (string)newGlobalProperty.Value;
                    }
                }

                ProjectInstance instance = _projectCollection.LoadProject((string)projectFileNames[i], finalGlobalProperties, null).CreateProjectInstance();

                IDictionary<string, TargetResult> targetOutputs;
                bool success = instance.Build(targetNames, loggers, out targetOutputs);

                if (targetOutputsPerProject != null)
                {
                    targetOutputsPerProject.Add(new Dictionary<string, ITaskItem[]>(StringComparer.OrdinalIgnoreCase));

                    foreach (KeyValuePair<string, TargetResult> resultEntry in targetOutputs)
                    {
                        targetOutputsPerProject[i][resultEntry.Key] = resultEntry.Value.Items;
                    }
                }

                allSucceeded = allSucceeded && success;
            }

            return new BuildEngineResult(allSucceeded, targetOutputsPerProject);
        }

        public void Yield()
        {
        }

        public void Reacquire()
        {
        }

        public bool BuildProjectFile
            (
            string projectFileName
            )
        {
            return (_projectCollection.LoadProject(projectFileName)).Build();
        }

        public bool BuildProjectFile
            (
            string projectFileName,
            string[] targetNames
            )
        {
            return (_projectCollection.LoadProject(projectFileName)).Build(targetNames);
        }

        public bool BuildProjectFile
            (
            string projectFileName,
            string targetName
            )
        {
            return (_projectCollection.LoadProject(projectFileName)).Build(targetName);
        }

        public void UnregisterAllLoggers
            (
            )
        {
            _projectCollection.UnregisterAllLoggers();
        }

        public void UnloadAllProjects
            (
            )
        {
            _projectCollection.UnloadAllProjects();
        }


        /// <summary>
        /// Assert that the mock log in the engine doesn't contain a certain message based on a resource string and some parameters
        /// </summary>
        internal void AssertLogDoesntContainMessageFromResource(GetStringDelegate getString, string resourceName, params string[] parameters)
        {
            string resource = getString(resourceName);
            string stringToSearchFor = String.Format(resource, parameters);
            AssertLogDoesntContain(stringToSearchFor);
        }

        /// <summary>
        /// Assert that the mock log in the engine contains a certain message based on a resource string and some parameters
        /// </summary>
        internal void AssertLogContainsMessageFromResource(GetStringDelegate getString, string resourceName, params string[] parameters)
        {
            string resource = getString(resourceName);
            string stringToSearchFor = String.Format(resource, parameters);
            AssertLogContains(stringToSearchFor);
        }

        /// <summary>
        /// Assert that the log file contains the given string.
        /// Case insensitive.
        /// First check if the string is in the log string. If not
        /// than make sure it is also check the MockLogger
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogContains(string contains)
        {
            if (_upperLog == null)
            {
                _upperLog = _log;
                _upperLog = _upperLog.ToUpperInvariant();
            }

            // If we do not contain this string than pass it to
            // MockLogger. Since MockLogger is also registered as
            // a logger it may have this string.
            if (!_upperLog.Contains
                (
                    contains.ToUpperInvariant()
                )
              )
            {
                Console.WriteLine(_log);
                _mockLogger.AssertLogContains(contains);
            }
        }

        /// <summary>
        /// Assert that the log doesnt contain the given string.
        /// First check if the string is in the log string. If not
        /// than make sure it is also not in the MockLogger
        /// </summary>
        /// <param name="contains"></param>
        internal void AssertLogDoesntContain(string contains)
        {
            Console.WriteLine(_log);

            if (_upperLog == null)
            {
                _upperLog = _log;
                _upperLog = _upperLog.ToUpperInvariant();
            }

            Assert.False(_upperLog.Contains
                (
                    contains.ToUpperInvariant()
                ));

            // If we do not contain this string than pass it to
            // MockLogger. Since MockLogger is also registered as
            // a logger it may have this string.
            _mockLogger.AssertLogDoesntContain
            (
                contains
            );
        }

        /// <summary>
        /// Delegate which will get the resource from the correct resource manager
        /// </summary>
        public delegate string GetStringDelegate(string resourceName);

        public object GetRegisteredTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            object obj = null;
            _objectCashe.TryGetValue(key, out obj);
            return obj;
        }

        public void RegisterTaskObject(object key, object obj, RegisteredTaskObjectLifetime lifetime, bool allowEarlyCollection)
        {
            _objectCashe[key] = obj;
        }

        public object UnregisterTaskObject(object key, RegisteredTaskObjectLifetime lifetime)
        {
            var obj = _objectCashe[key];
            _objectCashe.Remove(key);
            return obj;
        }
    }
}
