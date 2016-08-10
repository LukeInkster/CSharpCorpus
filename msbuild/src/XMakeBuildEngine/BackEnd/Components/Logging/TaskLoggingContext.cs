﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A logging context for tasks.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Xml;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.BackEnd.Logging
{
    /// <summary>
    /// A logging context representing a task being built.
    /// </summary>
    internal class TaskLoggingContext : BaseLoggingContext
    {
        /// <summary>
        /// The target context in which this task is being built.
        /// </summary>
        private TargetLoggingContext _targetLoggingContext;

        /// <summary>
        /// The task instance
        /// </summary>
        private ProjectTargetInstanceChild _task;

        /// <summary>
        /// The name of the task
        /// </summary>
        private string _taskName;

        /// <summary>
        /// Constructs a task logging context from a parent target context and a task node.
        /// </summary>
        internal TaskLoggingContext(TargetLoggingContext targetLoggingContext, string projectFullPath, ProjectTargetInstanceChild task)
            : base(targetLoggingContext)
        {
            _targetLoggingContext = targetLoggingContext;
            _task = task;

            ProjectTaskInstance taskInstance = task as ProjectTaskInstance;
            if (taskInstance != null)
            {
                _taskName = taskInstance.Name;
            }
            else
            {
                ProjectPropertyGroupTaskInstance propertyGroupInstance = task as ProjectPropertyGroupTaskInstance;
                if (propertyGroupInstance != null)
                {
                    _taskName = "PropertyGroup";
                }
                else
                {
                    ProjectItemGroupTaskInstance itemGroupInstance = task as ProjectItemGroupTaskInstance;
                    if (itemGroupInstance != null)
                    {
                        _taskName = "ItemGroup";
                    }
                    else
                    {
                        _taskName = "Unknown";
                    }
                }
            }

            this.BuildEventContext = LoggingService.LogTaskStarted2
                (
                targetLoggingContext.BuildEventContext,
                _taskName,
                projectFullPath,
                task.Location.File
                );
            this.IsValid = true;
        }

        /// <summary>
        /// Constructor used to support out-of-proc task host (proxy for in-proc logging service.)
        /// </summary>
        internal TaskLoggingContext(ILoggingService loggingService, BuildEventContext outOfProcContext)
            : base(loggingService, outOfProcContext, true)
        {
            this.IsValid = true;
        }

        /// <summary>
        /// Retrieves the target logging context.
        /// </summary>
        internal TargetLoggingContext TargetLoggingContext
        {
            get
            {
                return _targetLoggingContext;
            }
        }

        /// <summary>
        /// Retrieves the task node.
        /// </summary>
        internal ProjectTargetInstanceChild Task
        {
            get
            {
                return _task;
            }
        }

        /// <summary>
        /// Retrieves the task node.
        /// </summary>
        internal string TaskName
        {
            get
            {
                return _taskName;
            }
        }

        /// <summary>
        /// Log that a task has just completed
        /// </summary>
        internal void LogTaskBatchFinished(string projectFullPath, bool success)
        {
            ErrorUtilities.VerifyThrow(this.IsValid, "invalid");

            LoggingService.LogTaskFinished
                (
                BuildEventContext,
                _taskName,
                projectFullPath,
                _task.Location.File,
                success
                );
            this.IsValid = false;
        }
    }
}
