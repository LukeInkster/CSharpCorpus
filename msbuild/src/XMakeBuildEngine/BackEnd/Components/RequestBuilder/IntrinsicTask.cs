﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Base class for intrinsic tasks.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;

using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using System.Xml;
using System.Reflection;
using System.Globalization;
using System.Collections;
using System.Diagnostics;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Microsoft.Build.BackEnd.Logging;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// A class that evaluates an ItemGroup or PropertyGroup that is within a target.
    /// </summary>
    internal abstract class IntrinsicTask
    {
        /// <summary>
        /// Initializes this base class.
        /// </summary>
        /// <param name="loggingContext">The logging context</param>
        /// <param name="projectInstance">The project instance</param>
        /// <param name="logTaskInputs">Flag to determine whether or not to log task inputs.</param>
        protected IntrinsicTask(TargetLoggingContext loggingContext, ProjectInstance projectInstance, bool logTaskInputs)
        {
            this.LoggingContext = loggingContext;
            this.Project = projectInstance;
            this.LogTaskInputs = logTaskInputs;
        }

        /// <summary>
        /// Gets or sets the logging context.
        /// </summary>
        internal TargetLoggingContext LoggingContext
        {
            get;
            private set;
        }

        /// <summary>
        /// Gets or sets the project instance.
        /// </summary>
        internal ProjectInstance Project
        {
            get;
            private set;
        }

        /// <summary>
        /// Flag to determine whether or not to log task inputs.
        /// </summary>
        protected bool LogTaskInputs
        {
            get;
            private set;
        }

        /// <summary>
        /// Factory for intrinsic tasks.
        /// </summary>
        /// <param name="taskInstance">The task instance object.</param>
        /// <param name="loggingContext">The logging context.</param>
        /// <param name="projectInstance">The project instance.</param>
        /// <returns>An instantiated intrinsic task.</returns>
        internal static IntrinsicTask InstantiateTask(ProjectTargetInstanceChild taskInstance, TargetLoggingContext loggingContext, ProjectInstance projectInstance, bool logTaskInputs)
        {
            if (taskInstance is ProjectPropertyGroupTaskInstance)
            {
                return new PropertyGroupIntrinsicTask(taskInstance as ProjectPropertyGroupTaskInstance, loggingContext, projectInstance, logTaskInputs);
            }
            else if (taskInstance is ProjectItemGroupTaskInstance)
            {
                return new ItemGroupIntrinsicTask(taskInstance as ProjectItemGroupTaskInstance, loggingContext, projectInstance, logTaskInputs);
            }
            else
            {
                ErrorUtilities.ThrowInternalError("Unhandled intrinsic task type {0}", taskInstance.GetType().BaseType);
                return null;
            }
        }

        /// <summary>
        /// Called to execute a task within a target. This method instantiates the task, sets its parameters, and executes it. 
        /// </summary>
        /// <param name="lookup">The lookup used for expansion and to receive created items and properties.</param>
        internal abstract void ExecuteTask(Lookup lookup);

        /// <summary>
        /// If value is not an empty string, adds it to list.
        /// </summary>
        /// <param name="list">The list of strings to which this should be added, if it is not empty.</param>
        /// <param name="value">The string to add.</param>
        protected static void AddIfNotEmptyString(List<string> list, string value)
        {
            if (!String.IsNullOrEmpty(value))
            {
                list.Add(value);
            }
        }
    }
}
