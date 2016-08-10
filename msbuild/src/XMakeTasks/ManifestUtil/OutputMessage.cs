﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Utilities;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Microsoft.Build.Tasks.Deployment.ManifestUtilities
{
    /// <summary>
    /// Specifies the type of output message as either an error, warning, or informational.
    /// </summary>
    [ComVisible(false)]
    public enum OutputMessageType
    {
        /// <summary>
        /// Indicates an informational message.
        /// </summary>
        Info,
        /// <summary>
        /// Indicates a warning.
        /// </summary>
        Warning,
        /// <summary>
        /// Indicates an error.
        /// </summary>
        Error
    }

    /// <summary>
    /// Describes an error, warning, or informational output message for the manifest generator.
    /// </summary>
    [ComVisible(false)]
    public sealed class OutputMessage
    {
        private readonly string[] _arguments;
        private readonly string _name;
        private readonly string _text;
        private readonly OutputMessageType _type;

        internal OutputMessage(OutputMessageType type, string name, string text, params string[] arguments)
        {
            if (name == null) throw new ArgumentNullException("name");
            if (arguments == null) throw new ArgumentNullException("arguments");
            _type = type;
            _name = name;
            _arguments = arguments;
            _text = text;
        }

        /// <summary>
        /// Returns a string array of arguments for the message.
        /// </summary>
        /// <returns></returns>
        public string[] GetArguments() { return _arguments; }

        /// <summary>
        /// Specifies an identifier for the message.
        /// </summary>
        public string Name { get { return _name; } }

        /// <summary>
        /// Contains the text of the message.
        /// </summary>
        public string Text { get { return _text; } }

        /// <summary>
        /// Indicates whether the message is an error, warning, or informational message.
        /// </summary>
        public OutputMessageType Type { get { return _type; } }
    }

    /// <summary>
    /// Provides a collection for output messages.
    /// </summary>
    [ComVisible(false)]
    public sealed class OutputMessageCollection : IEnumerable
    {
        private readonly System.Resources.ResourceManager _taskResources = Microsoft.Build.Shared.AssemblyResources.PrimaryResources;
        private readonly List<OutputMessage> _list = new List<OutputMessage>();
        private int _errorCount = 0;
        private int _warningCount = 0;

        internal OutputMessageCollection()
        {
        }

        /// <summary>
        /// Gets the element at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the entry to get.</param>
        /// <returns>The file reference instance.</returns>
        public OutputMessage this[int index]
        {
            get { return (OutputMessage)_list[index]; }
        }

        internal void AddErrorMessage(string taskResourceName, params string[] arguments)
        {
            ++_errorCount;
            string taskText = _taskResources.GetString(taskResourceName);
            if (!String.IsNullOrEmpty(taskText))
                taskText = String.Format(CultureInfo.CurrentCulture, taskText, arguments);
            _list.Add(new OutputMessage(OutputMessageType.Error, taskResourceName, taskText, arguments));
        }

        internal void AddWarningMessage(string taskResourceName, params string[] arguments)
        {
            ++_warningCount;
            string taskText = _taskResources.GetString(taskResourceName);
            if (!String.IsNullOrEmpty(taskText))
                taskText = String.Format(CultureInfo.CurrentCulture, taskText, arguments);
            _list.Add(new OutputMessage(OutputMessageType.Warning, taskResourceName, taskText, arguments));
        }

        /// <summary>
        /// Removes all objects from the collection.
        /// </summary>
        public void Clear()
        {
            _list.Clear();
            _errorCount = 0;
            _warningCount = 0;
        }

        /// <summary>
        /// Gets the number of error messages in the collecction.
        /// </summary>
        public int ErrorCount
        {
            get
            {
                return _errorCount;
            }
        }

        /// <summary>
        /// Returns an enumerator that can iterate through the collection.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public IEnumerator GetEnumerator()
        {
            return _list.GetEnumerator();
        }

        internal bool LogTaskMessages(Task task)
        {
            foreach (OutputMessage message in _list)
            {
                switch (message.Type)
                {
                    case OutputMessageType.Warning:
                        task.Log.LogWarningWithCodeFromResources(message.Name, message.GetArguments());
                        break;
                    case OutputMessageType.Error:
                        task.Log.LogErrorWithCodeFromResources(message.Name, message.GetArguments());
                        break;
                }
            }
            return _errorCount <= 0;
        }

        /// <summary>
        /// Gets the number of warning messages in the collecction.
        /// </summary>
        public int WarningCount
        {
            get
            {
                return _warningCount;
            }
        }
    }
}
