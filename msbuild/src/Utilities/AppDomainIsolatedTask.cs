﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Resources;
using System.Security;
using System.Security.Permissions;
using System.Runtime.InteropServices;

using Microsoft.Build.Framework;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// This class provides the same functionality as the Task class, but derives from MarshalByRefObject so that it can be
    /// instantiated in its own app domain.
    /// </summary>
    [LoadInSeparateAppDomain]
    public abstract class AppDomainIsolatedTask : MarshalByRefObject, ITask
    {
        #region Constructors

        /// <summary>
        /// Default (family) constructor.
        /// </summary>
        protected AppDomainIsolatedTask()
        {
            _log = new TaskLoggingHelper(this);
        }

        /// <summary>
        /// This (family) constructor allows derived task classes to register their resources.
        /// </summary>
        /// <param name="taskResources">The task resources.</param>
        protected AppDomainIsolatedTask(ResourceManager taskResources)
            : this()
        {
            _log.TaskResources = taskResources;
        }

        /// <summary>
        /// This (family) constructor allows derived task classes to register their resources, as well as provide a prefix for
        /// composing help keywords from string resource names. If the prefix is an empty string, then string resource names will
        /// be used verbatim as help keywords. For an example of how the prefix is used, see the
        /// <see cref="TaskLoggingHelper.LogErrorWithCodeFromResources(string, object[])"/> method.
        /// </summary>
        /// <param name="taskResources">The task resources.</param>
        /// <param name="helpKeywordPrefix">The help keyword prefix.</param>
        protected AppDomainIsolatedTask(ResourceManager taskResources, string helpKeywordPrefix)
            : this(taskResources)
        {
            _log.HelpKeywordPrefix = helpKeywordPrefix;
        }

        #endregion

        #region Properties

        /// <summary>
        /// The build engine automatically sets this property to allow tasks to call back into it.
        /// </summary>
        /// <value>The build engine interface available to tasks.</value>
        public IBuildEngine BuildEngine
        {
            get
            {
                return _buildEngine;
            }

            set
            {
                _buildEngine = value;
            }
        }

        // callback interface on the build engine
        private IBuildEngine _buildEngine;

        /// <summary>
        /// The build engine sets this property if the host IDE has associated a host object with this particular task.
        /// </summary>
        /// <value>The host object instance (can be null).</value>
        public ITaskHost HostObject
        {
            get
            {
                return _hostObject;
            }

            set
            {
                _hostObject = value;
            }
        }

        // Optional host object that might be used by certain IDE-aware tasks.
        private ITaskHost _hostObject;

        /// <summary>
        /// Gets an instance of a TaskLoggingHelper class containing task logging methods.
        /// </summary>
        /// <value>The logging helper object.</value>
        public TaskLoggingHelper Log
        {
            get
            {
                return _log;
            }
        }

        // the logging helper
        private TaskLoggingHelper _log;

        /// <summary>
        /// Gets or sets the task's culture-specific resources. Derived classes should register their resources either during
        /// construction, or via this property, if they have localized strings.
        /// </summary>
        /// <value>The task's resources (can be null).</value>
        protected ResourceManager TaskResources
        {
            get
            {
                return Log.TaskResources;
            }

            set
            {
                Log.TaskResources = value;
            }
        }

        /// <summary>
        /// Gets or sets the prefix used to compose help keywords from string resource names. If a task does not have help
        /// keywords associated with its messages, it can ignore this property or set it to null. If the prefix is set to an empty
        /// string, then string resource names will be used verbatim as help keywords. For an example of how this prefix is used,
        /// see the <see cref="TaskLoggingHelper.LogErrorWithCodeFromResources(string, object[])"/> method.
        /// </summary>
        /// <value>The help keyword prefix string (can be null).</value>
        protected string HelpKeywordPrefix
        {
            get
            {
                return Log.HelpKeywordPrefix;
            }

            set
            {
                Log.HelpKeywordPrefix = value;
            }
        }

        #endregion

        #region Methods

        /// <summary>
        /// Must be implemented by derived class.
        /// </summary>
        /// <returns>true, if successful</returns>
        public abstract bool Execute();

        /// <summary>
        /// Overridden to give tasks deriving from this class infinite lease time. Otherwise we end up with a limited
        /// lease (5 minutes I think) and task instances can expire if they take long time processing.
        /// </summary>
        [SecurityCritical]
        public override object InitializeLifetimeService()
        {
            // null means infinite lease time
            return null;
        }

        #endregion
    }
}
