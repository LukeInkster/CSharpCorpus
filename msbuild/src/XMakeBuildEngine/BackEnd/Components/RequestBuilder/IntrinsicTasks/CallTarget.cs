﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;

// This CallTarget intrinsic task replaces the one on Microsoft.Build.Tasks, which is now deprecated.

namespace Microsoft.Build.BackEnd
{
    /// <remarks>
    /// This class implements the "CallTarget" task, which invokes other targets within the same
    /// project file.  Marked RunInMTA because we do not want this task to ever be invoked explicitly
    /// on the STA if the RequestBuilder is running on another thread, as this will cause thread
    /// id validation checks to fail.
    /// </remarks>
    [RunInMTA]
    internal class CallTarget : ITask
    {
        /// <summary>
        /// The task logging helper
        /// </summary>
        private TaskLoggingHelper _logHelper = null;

        /// <summary>
        /// Default constructor.
        /// </summary>
        public CallTarget()
        {
        }

        #region Properties

        // A list of targets to build.  This is a required parameter.  If you want to build the 
        // default targets, use the <MSBuild> task and pass in Projects=$(MSBuildProjectFile).
        private string[] _targets = null;

        // outputs of all built targets
        private ArrayList _targetOutputs = new ArrayList();

        // When this is true, instead of calling the engine once to build all the targets,
        // we would call the engine once per target.  The benefit of this is that
        // if one target fails, you can still continue with the remaining targets.
        private bool _runEachTargetSeparately = false;

        // If true the cache will be checked for the result and the result will be stored if the operation 
        // is run
        private bool _useResultsCache = false;

        /// <summary>
        /// The targets to build.
        /// </summary>
        /// <value>Array of target names.</value>
        public string[] Targets
        {
            get
            {
                return _targets;
            }

            set
            {
                _targets = value;
            }
        }

        /// <summary>
        /// Outputs of the targets built in each project.
        /// </summary>
        /// <value>Array of output items.</value>
        [Output]
        public ITaskItem[] TargetOutputs
        {
            get
            {
                return (ITaskItem[])_targetOutputs.ToArray(typeof(ITaskItem));
            }
        }

        /// <summary>
        /// When this is true, instead of calling the engine once to build all the targets (for each project),
        /// we would call the engine once per target (for each project).  The benefit of this is that
        /// if one target fails, you can still continue with the remaining targets.
        /// </summary>
        public bool RunEachTargetSeparately
        {
            get
            {
                return _runEachTargetSeparately;
            }

            set
            {
                _runEachTargetSeparately = value;
            }
        }

        /// <summary>
        /// If true the cached result will be returned if present and a if MSBuild
        /// task is run its result will be cached in a scope (ProjectFileName, GlobalProperties)[TargetNames]
        /// as a list of build items
        /// </summary>
        public bool UseResultsCache
        {
            get
            {
                return _useResultsCache;
            }
            set
            {
                _useResultsCache = value;
            }
        }

        #endregion

        #region ITask Members

        public IBuildEngine BuildEngine
        {
            get;
            set;
        }

        public IBuildEngine2 BuildEngine2
        {
            get { return (IBuildEngine2)BuildEngine; }
        }

        public IBuildEngine3 BuildEngine3
        {
            get { return (IBuildEngine3)BuildEngine; }
        }

        /// <summary>
        /// The host object, from ITask
        /// </summary>
        public ITaskHost HostObject
        {
            get;
            set;
        }

        public TaskLoggingHelper Log
        {
            get
            {
                if (_logHelper == null)
                {
                    _logHelper = new TaskLoggingHelper(this);
                }

                return _logHelper;
            }
        }

        public bool Execute()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Instructs the MSBuild engine to build one or more targets in the current project.
        /// </summary>
        /// <returns>true if all targets built successfully; false if any target fails</returns>
        public Task<bool> ExecuteInternal()
        {
            // Make sure the list of targets was passed in.
            if ((Targets == null) || (Targets.Length == 0))
            {
                return Task<bool>.FromResult(true);
            }

            // This is a list of string[].  That is, each element in the list is a string[].  Each
            // string[] represents a set of target names to build.  Depending on the value 
            // of the RunEachTargetSeparately parameter, we each just call the engine to run all 
            // the targets together, or we call the engine separately for each target.
            ArrayList targetLists = MSBuild.CreateTargetLists(this.Targets, this.RunEachTargetSeparately);

            ITaskItem[] singleProject = new ITaskItem[1];
            singleProject[0] = null;
            // Build the specified targets in the current project.
            return MSBuild.ExecuteTargets
                (
                singleProject,  // project = null (current project)
                null,           // propertiesTable = null
                null,           // undefineProperties
                targetLists,    // list of targets to build
                false,          // stopOnFirstFailure = false
                false,          // rebaseOutputs = false
                this.BuildEngine3,
                this.Log,
                _targetOutputs,
                this.UseResultsCache,
                false,
                null            // toolsVersion = null
                );
        }

        #endregion
    }
}
