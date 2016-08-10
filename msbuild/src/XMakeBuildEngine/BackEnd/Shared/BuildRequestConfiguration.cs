﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A configuration for a build request.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Microsoft.Build.Construction;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using Microsoft.Build.Execution;
using Microsoft.Build.Collections;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using System.Diagnostics;
using System.IO;
using System.Xml;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// A build request configuration represents all of the data necessary to know which project to build
    /// and the environment in which it should be built.
    /// </summary>
    internal class BuildRequestConfiguration : IEquatable<BuildRequestConfiguration>,
                                               INodePacket
    {
        /// <summary>
        /// The invalid configuration id
        /// </summary>
        public const int InvalidConfigurationId = 0;

        #region Static State

        /// <summary>
        /// This is the ID of the configuration as set by the generator of the configuration.  When
        /// a node generates a configuration, this is set to a negative number.  The Build Manager will
        /// generate positive IDs
        /// </summary>
        private int _configId;

        /// <summary>
        /// The full path to the project to build.
        /// </summary>
        private string _projectFullPath;

        /// <summary>
        /// The tools version specified for the configuration.
        /// Always specified.
        /// May have originated from a /tv switch, or an MSBuild task,
        /// or a Project tag, or the default.
        /// </summary>
        private string _toolsVersion;

        /// <summary>
        /// Whether the tools version was set by the /tv switch or passed in through an msbuild callback
        /// directly or indirectly.
        /// </summary>
        private bool _explicitToolsVersionSpecified;

        /// <summary>
        /// The set of global properties which should be used when building this project.
        /// </summary>
        private PropertyDictionary<ProjectPropertyInstance> _globalProperties;

        /// <summary>
        /// Flag indicating if the project in this configuration is a traversal
        /// </summary>
        private bool? _isTraversalProject;

        /// <summary>
        /// Synchronization object.  Currently this just prevents us from caching and uncaching at the
        /// same time, causing a race condition.  This class is not made 100% threadsafe by the presence
        /// and current usage of this lock.
        /// </summary>
        private Object _syncLock = new Object();

        #endregion

        #region Build State

        /// <summary>
        /// The project object, representing the project to be built.
        /// </summary>
        private ProjectInstance _project;

        /// <summary>
        /// The state of a project instance which has been transferred from one node to another.
        /// </summary>
        private ProjectInstance _transferredState;

        /// <summary>
        /// The project instance properties we should transfer.
        /// </summary>
        private List<ProjectPropertyInstance> _transferredProperties;

        /// <summary>
        /// The initial targets for the project
        /// </summary>
        private List<string> _projectInitialTargets;

        /// <summary>
        /// The default targets for the project
        /// </summary>
        private List<string> _projectDefaultTargets;

        /// <summary>
        /// This is the lookup representing the current project items and properties 'state'.
        /// </summary>
        private Lookup _baseLookup;

        /// <summary>
        /// This is the set of targets which are currently building but which have not yet completed.
        /// { targetName -> globalRequestId }
        /// </summary>
        private Dictionary<string, int> _activelyBuildingTargets;

        /// <summary>
        /// The node where this configuration's master results are stored.
        /// </summary>
        private int _resultsNodeId = Scheduler.InvalidNodeId;

        ///<summary>
        /// Holds a snapshot of the environment at the time we blocked.
        /// </summary>
        private Dictionary<string, string> _savedEnvironmentVariables;

        /// <summary>
        /// Holds a snapshot of the current working directory at the time we blocked.
        /// </summary>
        private string _savedCurrentDirectory;

        #endregion

        /// <summary>
        /// Initializes a configuration from a BuildRequestData structure.  Used by the BuildManager.
        /// Figures out the correct tools version to use, falling back to the provided default if necessary.
        /// May throw InvalidProjectFileException.
        /// </summary>
        /// <param name="data">The data containing the configuration information.</param>
        /// <param name="defaultToolsVersion">The default ToolsVersion to use as a fallback</param>
        /// <param name="getToolset">Callback used to get a Toolset based on a ToolsVersion</param>
        internal BuildRequestConfiguration(BuildRequestData data, string defaultToolsVersion, Utilities.GetToolset getToolset = null)
            : this(0, data, defaultToolsVersion, getToolset)
        {
        }

        /// <summary>
        /// Initializes a configuration from a BuildRequestData structure.  Used by the BuildManager.
        /// Figures out the correct tools version to use, falling back to the provided default if necessary.
        /// May throw InvalidProjectFileException.
        /// </summary>
        /// <param name="configId">The configuration ID to assign to this new configuration.</param>
        /// <param name="data">The data containing the configuration information.</param>
        /// <param name="defaultToolsVersion">The default ToolsVersion to use as a fallback</param>
        /// <param name="getToolset">Callback used to get a Toolset based on a ToolsVersion</param>
        internal BuildRequestConfiguration(int configId, BuildRequestData data, string defaultToolsVersion, Utilities.GetToolset getToolset = null)
        {
            ErrorUtilities.VerifyThrowArgumentNull(data, "data");
            ErrorUtilities.VerifyThrowInternalLength(data.ProjectFullPath, "data.ProjectFullPath");

            _configId = configId;
            _projectFullPath = data.ProjectFullPath;
            _explicitToolsVersionSpecified = data.ExplicitToolsVersionSpecified;
            _toolsVersion = ResolveToolsVersion(data, defaultToolsVersion, getToolset);
            _globalProperties = data.GlobalPropertiesDictionary;

            // The following information only exists when the request is populated with an existing project.
            if (data.ProjectInstance != null)
            {
                _project = data.ProjectInstance;
                _projectInitialTargets = data.ProjectInstance.InitialTargets;
                _projectDefaultTargets = data.ProjectInstance.DefaultTargets;
                if (data.PropertiesToTransfer != null)
                {
                    _transferredProperties = new List<ProjectPropertyInstance>();
                    foreach (var name in data.PropertiesToTransfer)
                    {
                        _transferredProperties.Add(data.ProjectInstance.GetProperty(name));
                    }
                }

                this.IsCacheable = false;
            }
            else
            {
                this.IsCacheable = true;
            }
        }

        /// <summary>
        /// Creates a new BuildRequestConfiguration based on an existing project instance.
        /// Used by the BuildManager to populate configurations from a solution.
        /// </summary>
        /// <param name="configId">The configuration id</param>
        /// <param name="instance">The project instance.</param>
        internal BuildRequestConfiguration(int configId, ProjectInstance instance)
        {
            ErrorUtilities.VerifyThrowArgumentNull(instance, "instance");

            _configId = configId;
            _projectFullPath = instance.FullPath;
            _explicitToolsVersionSpecified = instance.ExplicitToolsVersionSpecified;
            _toolsVersion = instance.ToolsVersion;
            _globalProperties = instance.GlobalPropertiesDictionary;

            _project = instance;
            _projectInitialTargets = instance.InitialTargets;
            _projectDefaultTargets = instance.DefaultTargets;
            this.IsCacheable = false;
        }

        /// <summary>
        /// Creates a new configuration which is a clone of the old one but with a new id.
        /// </summary>
        private BuildRequestConfiguration(int configId, BuildRequestConfiguration other)
        {
            ErrorUtilities.VerifyThrow(configId != 0, "Configuration ID must not be zero when using this constructor.");
            ErrorUtilities.VerifyThrowArgumentNull(other, "other");
            ErrorUtilities.VerifyThrow(other._transferredState == null, "Unexpected transferred state still set on other configuration.");

            _project = other._project;
            _transferredProperties = other._transferredProperties;
            _projectDefaultTargets = other._projectDefaultTargets;
            _projectInitialTargets = other._projectInitialTargets;
            _projectFullPath = other._projectFullPath;
            _toolsVersion = other._toolsVersion;
            _explicitToolsVersionSpecified = other._explicitToolsVersionSpecified;
            _globalProperties = other._globalProperties;
            this.IsCacheable = other.IsCacheable;
            _configId = configId;
        }

        /// <summary>
        /// Private constructor for deserialization
        /// </summary>
        private BuildRequestConfiguration(INodePacketTranslator translator)
        {
            Translate(translator);
        }

        /// <summary>
        /// Flag indicating whether the configuration is allowed to cache.  This does not mean that the configuration will
        /// actually cache - there are several criteria which must for that.
        /// </summary>
        public bool IsCacheable
        {
            get;
            set;
        }

        /// <summary>
        /// When reset caches is false we need to only keep around the configurations which are being asked for during the design time build.
        /// Other configurations need to be cleared. If this configuration is marked as ExplicitlyLoadedConfiguration then it should not be cleared when 
        /// Reset Caches is false.
        /// </summary>
        public bool ExplicitlyLoaded
        {
            get;
            set;
        }

        /// <summary>
        /// Flag indicating whether or not the configuration is actually building.
        /// </summary>
        public bool IsActivelyBuilding
        {
            get
            {
                return (_activelyBuildingTargets != null) && (_activelyBuildingTargets.Count > 0);
            }
        }

        /// <summary>
        /// Flag indicating whether or not the configuration has been loaded before.
        /// </summary>
        public bool IsLoaded
        {
            get { return _project != null; }
        }

        /// <summary>
        /// Flag indicating if the configuration is cached or not.
        /// </summary>
        public bool IsCached
        {
            get;
            private set;
        }

        /// <summary>
        /// Flag indicating if this configuration represents a traversal project.  Traversal projects
        /// are projects which typically do little or no work themselves, but have references to other
        /// projects (and thus are used to find more work.)  The scheduler can treat these differently
        /// in order to fill its work queue with other options for scheduling.
        /// </summary>
        public bool IsTraversal
        {
            get
            {
                if (!_isTraversalProject.HasValue)
                {
                    if (String.Equals(Path.GetFileName(ProjectFullPath), "dirs.proj", StringComparison.OrdinalIgnoreCase))
                    {
                        // dirs.proj are assumed to be traversals
                        _isTraversalProject = true;
                    }
                    else if (FileUtilities.IsMetaprojectFilename(ProjectFullPath))
                    {
                        // Metaprojects generated by the SolutionProjectGenerator are traversals.  They have no 
                        // on-disk representation - they are ProjectInstances which exist only in memory.
                        _isTraversalProject = true;
                    }
                    else if (FileUtilities.IsSolutionFilename(ProjectFullPath))
                    {
                        // Solution files are considered to be traversals.
                        _isTraversalProject = true;
                    }
                    else
                    {
                        _isTraversalProject = false;
                    }
                }

                return _isTraversalProject.Value;
            }
        }

        /// <summary>
        /// Returns true if this configuration was generated on a node and has not yet been resolved.
        /// </summary>
        public bool WasGeneratedByNode
        {
            [DebuggerStepThrough]
            get
            { return _configId < 0; }
        }

        /// <summary>
        /// Sets or returns the configuration id
        /// </summary>
        public int ConfigurationId
        {
            [DebuggerStepThrough]
            get
            {
                return _configId;
            }

            [DebuggerStepThrough]
            set
            {
                ErrorUtilities.VerifyThrow((_configId == 0) || (WasGeneratedByNode && (value > 0)), "Configuration ID must be zero, or it must be less than zero and the new config must be greater than zero.  It was {0}, the new value was {1}.", _configId, value);
                _configId = value;
            }
        }

        /// <summary>
        /// Returns the filename of the project to build.
        /// </summary>
        public string ProjectFullPath
        {
            [DebuggerStepThrough]
            get
            { return _projectFullPath; }
        }

        /// <summary>
        /// The tools version specified for the configuration.
        /// Always specified.
        /// May have originated from a /tv switch, or an MSBuild task,
        /// or a Project tag, or the default.
        /// </summary>
        public string ToolsVersion
        {
            [DebuggerStepThrough]
            get
            { return _toolsVersion; }
        }

        /// <summary>
        /// Returns the global properties to use to build this project.
        /// </summary>
        public PropertyDictionary<ProjectPropertyInstance> Properties
        {
            [DebuggerStepThrough]
            get
            { return _globalProperties; }
        }

        /// <summary>
        /// Sets or returns the project to build.
        /// </summary>
        public ProjectInstance Project
        {
            [DebuggerStepThrough]
            get
            {
                ErrorUtilities.VerifyThrow(!IsCached, "We shouldn't be accessing the ProjectInstance when the configuration is cached.");
                return _project;
            }

            [DebuggerStepThrough]
            set
            {
                ErrorUtilities.VerifyThrow(value != null, "Cannot set null project.");
                _project = value;
                _baseLookup = null;

                // Clear these out so the other accessors don't complain.  We don't want to generally enable resetting these fields.
                _projectDefaultTargets = null;
                _projectInitialTargets = null;
                ProjectDefaultTargets = _project.DefaultTargets;
                ProjectInitialTargets = _project.InitialTargets;

                if (IsCached)
                {
                    ClearCacheFile();
                    IsCached = false;
                }

                // If we have transferred the state of a project previously, then we need to assume its items and properties.
                if (_transferredState != null)
                {
                    ErrorUtilities.VerifyThrow(_transferredProperties == null, "Shouldn't be transferring entire state of ProjectInstance when transferredProperties is not null.");
                    _project.UpdateStateFrom(_transferredState);
                    _transferredState = null;
                }

                // If we have just requested a limited transfer of properties, do that.
                if (_transferredProperties != null)
                {
                    foreach (var property in _transferredProperties)
                    {
                        _project.SetProperty(property.Name, ((IProperty)property).EvaluatedValueEscaped);
                    }

                    _transferredProperties = null;
                }
            }
        }

        /// <summary>
        /// Returns true if the default and initial targets have been resolved.
        /// </summary>
        public bool HasTargetsResolved
        {
            get { return ProjectInitialTargets != null && ProjectDefaultTargets != null; }
        }

        /// <summary>
        /// Gets the initial targets for the project
        /// </summary>
        public List<string> ProjectInitialTargets
        {
            [DebuggerStepThrough]
            get
            {
                return _projectInitialTargets;
            }

            [DebuggerStepThrough]
            set
            {
                ErrorUtilities.VerifyThrow(_projectInitialTargets == null, "Initial targets cannot be reset once they have been set.");
                _projectInitialTargets = value;
            }
        }

        /// <summary>
        /// Gets the default targets for the project
        /// </summary>
        public List<string> ProjectDefaultTargets
        {
            [DebuggerStepThrough]
            get
            {
                return _projectDefaultTargets;
            }

            [DebuggerStepThrough]
            set
            {
                ErrorUtilities.VerifyThrow(_projectDefaultTargets == null, "Default targets cannot be reset once they have been set.");
                _projectDefaultTargets = value;
            }
        }

        /// <summary>
        /// Returns the node packet type
        /// </summary>
        public NodePacketType Type
        {
            [DebuggerStepThrough]
            get
            { return NodePacketType.BuildRequestConfiguration; }
        }

        /// <summary>
        /// Returns the lookup which collects all items and properties during the run of this project.
        /// </summary>
        public Lookup BaseLookup
        {
            get
            {
                ErrorUtilities.VerifyThrow(!IsCached, "Configuration is cached, we shouldn't be accessing the lookup.");

                if (null == _baseLookup)
                {
                    _baseLookup = new Lookup(Project.ItemsToBuildWith, Project.PropertiesToBuildWith, Project.InitialGlobalsForDebugging);
                }

                return _baseLookup;
            }
        }

        /// <summary>
        /// Retrieves the set of targets currently building, mapped to the request id building them.
        /// </summary>
        public Dictionary<string, int> ActivelyBuildingTargets
        {
            get
            {
                if (null == _activelyBuildingTargets)
                {
                    _activelyBuildingTargets = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                }

                return _activelyBuildingTargets;
            }
        }

        /// <summary>
        /// Holds a snapshot of the environment at the time we blocked.
        /// </summary>
        public Dictionary<string, string> SavedEnvironmentVariables
        {
            get
            {
                return _savedEnvironmentVariables;
            }

            set
            {
                _savedEnvironmentVariables = value;
            }
        }

        /// <summary>
        /// Holds a snapshot of the current working directory at the time we blocked.
        /// </summary>
        public string SavedCurrentDirectory
        {
            get
            {
                return _savedCurrentDirectory;
            }

            set
            {
                _savedCurrentDirectory = value;
            }
        }

        /// <summary>
        /// Whether the tools version was set by the /tv switch or passed in through an msbuild callback
        /// directly or indirectly.
        /// </summary>
        public bool ExplicitToolsVersionSpecified
        {
            [DebuggerStepThrough]
            get
            { return _explicitToolsVersionSpecified; }
        }

        /// <summary>
        /// Gets or sets the node on which this configuration's results are stored.
        /// </summary>
        internal int ResultsNodeId
        {
            get
            {
                return _resultsNodeId;
            }

            set
            {
                _resultsNodeId = value;
            }
        }

        /// <summary>
        /// Implementation of the equality operator.
        /// </summary>
        /// <param name="left">The left hand argument</param>
        /// <param name="right">The right hand argument</param>
        /// <returns>True if the objects are equivalent, false otherwise.</returns>
        public static bool operator ==(BuildRequestConfiguration left, BuildRequestConfiguration right)
        {
            if (Object.ReferenceEquals(left, null))
            {
                if (Object.ReferenceEquals(right, null))
                {
                    return true;
                }
                else
                {
                    return false;
                }
            }
            else
            {
                if (Object.ReferenceEquals(right, null))
                {
                    return false;
                }
                else
                {
                    return left.InternalEquals(right);
                }
            }
        }

        /// <summary>
        /// Implementation of the inequality operator.
        /// </summary>
        /// <param name="left">The left-hand argument</param>
        /// <param name="right">The right-hand argument</param>
        /// <returns>True if the objects are not equivalent, false otherwise.</returns>
        public static bool operator !=(BuildRequestConfiguration left, BuildRequestConfiguration right)
        {
            return !(left == right);
        }

        /// <summary>
        /// Requests that the configuration be cached to disk.
        /// </summary>
        public void CacheIfPossible()
        {
            lock (_syncLock)
            {
                if (IsActivelyBuilding || IsCached || !IsLoaded || !IsCacheable)
                {
                    return;
                }

                lock (_project)
                {
                    if (IsCacheable)
                    {
                        INodePacketTranslator translator = GetConfigurationTranslator(TranslationDirection.WriteToStream);

                        try
                        {
                            _project.Cache(translator);
                            _baseLookup = null;

                            IsCached = true;
                        }
                        finally
                        {
                            translator.Writer.BaseStream.Close();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Retrieves the configuration data from the cache.
        /// </summary>
        public void RetrieveFromCache()
        {
            lock (_syncLock)
            {
                if (!IsLoaded)
                {
                    return;
                }

                if (!IsCached)
                {
                    return;
                }

                INodePacketTranslator translator = GetConfigurationTranslator(TranslationDirection.ReadFromStream);
                try
                {
                    _project.RetrieveFromCache(translator);

                    IsCached = false;
                }
                finally
                {
                    translator.Reader.BaseStream.Close();
                }
            }
        }

        /// <summary>
        /// Gets the list of targets which are used to build the specified request, including all initial and applicable default targets
        /// </summary>
        /// <param name="request">The request </param>
        /// <returns>An array of t</returns>
        public List<string> GetTargetsUsedToBuildRequest(BuildRequest request)
        {
            ErrorUtilities.VerifyThrow(request.ConfigurationId == ConfigurationId, "Request does not match configuration.");
            ErrorUtilities.VerifyThrow(_projectInitialTargets != null, "Initial targets have not been set.");
            ErrorUtilities.VerifyThrow(_projectDefaultTargets != null, "Default targets have not been set.");

            List<string> initialTargets = _projectInitialTargets;
            List<string> nonInitialTargets = (request.Targets.Count == 0) ? _projectDefaultTargets : request.Targets;

            List<string> allTargets = new List<string>(initialTargets.Count + nonInitialTargets.Count);

            allTargets.AddRange(initialTargets);
            allTargets.AddRange(nonInitialTargets);

            return allTargets;
        }

        /// <summary>
        /// Returns the list of targets that are AfterTargets (or AfterTargets of the AfterTargets) 
        /// of the entrypoint targets.  
        /// </summary>
        public List<string> GetAfterTargetsForDefaultTargets(BuildRequest request)
        {
            // We may not have a project available.  In which case, return nothing -- we simply don't have 
            // enough information to figure out what the correct answer is.
            if (!this.IsCached && this.Project != null)
            {
                HashSet<string> afterTargetsFound = new HashSet<string>();

                Queue<string> targetsToCheckForAfterTargets = new Queue<string>((request.Targets.Count == 0) ? this.ProjectDefaultTargets : request.Targets);

                while (targetsToCheckForAfterTargets.Count > 0)
                {
                    string targetToCheck = targetsToCheckForAfterTargets.Dequeue();

                    IList<TargetSpecification> targetsWhichRunAfter = this.Project.GetTargetsWhichRunAfter(targetToCheck);

                    foreach (TargetSpecification targetWhichRunsAfter in targetsWhichRunAfter)
                    {
                        if (afterTargetsFound.Add(targetWhichRunsAfter.TargetName))
                        {
                            // If it's already in there, we've already looked into it so no need to do so again.  Otherwise, add it 
                            // to the list to check.
                            targetsToCheckForAfterTargets.Enqueue(targetWhichRunsAfter.TargetName);
                        }
                    }
                }

                return new List<string>(afterTargetsFound);
            }

            return null;
        }

        /// <summary>
        /// This override is used to provide a hash code for storage in dictionaries and the like.
        /// </summary>
        /// <remarks>
        /// If two objects are Equal, they must have the same hash code, for dictionaries to work correctly.
        /// Two configurations are Equal if their global properties are equivalent, not necessary reference equals.
        /// So only include filename and tools version in the hashcode.
        /// </remarks>
        /// <returns>A hash code</returns>
        public override int GetHashCode()
        {
            return StringComparer.OrdinalIgnoreCase.GetHashCode(_projectFullPath) ^ StringComparer.OrdinalIgnoreCase.GetHashCode(_toolsVersion);
        }

        /// <summary>
        /// Returns a string representation of the object
        /// </summary>
        /// <returns>String representation of the object</returns>
        public override string ToString()
        {
            return String.Format(CultureInfo.CurrentCulture, "{0} {1} {2} {3}", _configId, _projectFullPath, _toolsVersion, _globalProperties);
        }

        /// <summary>
        /// Determines object equality
        /// </summary>
        /// <param name="obj">The object to compare with</param>
        /// <returns>True if they contain the same data, false otherwise</returns>
        public override bool Equals(object obj)
        {
            if (Object.ReferenceEquals(obj, null))
            {
                return false;
            }

            if (this.GetType() != obj.GetType())
            {
                return false;
            }

            return InternalEquals((BuildRequestConfiguration)obj);
        }

        #region IEquatable<BuildRequestConfiguration> Members

        /// <summary>
        /// Equality of the configuration is the product of the equality of its members.
        /// </summary>
        /// <param name="other">The other configuration to which we will compare ourselves.</param>
        /// <returns>True if equal, false otherwise.</returns>
        public bool Equals(BuildRequestConfiguration other)
        {
            if (Object.ReferenceEquals(other, null))
            {
                return false;
            }

            return InternalEquals(other);
        }

        #endregion

        #region INodePacket Members

        /// <summary>
        /// Reads or writes the packet to the serializer.
        /// </summary>
        public void Translate(INodePacketTranslator translator)
        {
            if (translator.Mode == TranslationDirection.WriteToStream && _transferredProperties == null)
            {
                // When writing, we will transfer the state of any loaded project instance if we aren't transferring a limited subset.
                _transferredState = _project;
            }

            translator.Translate(ref _configId);
            translator.Translate(ref _projectFullPath);
            translator.Translate(ref _transferredState, ProjectInstance.FactoryForDeserialization);
            translator.Translate(ref _transferredProperties, ProjectPropertyInstance.FactoryForDeserialization);
            translator.Translate(ref _resultsNodeId);
            translator.Translate(ref _toolsVersion);
            translator.Translate(ref _explicitToolsVersionSpecified);
            translator.TranslateDictionary<PropertyDictionary<ProjectPropertyInstance>, ProjectPropertyInstance>(ref _globalProperties, ProjectPropertyInstance.FactoryForDeserialization);
            translator.Translate(ref _savedCurrentDirectory);
            translator.TranslateDictionary(ref _savedEnvironmentVariables, StringComparer.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Factory for serialization.
        /// </summary>
        static internal INodePacket FactoryForDeserialization(INodePacketTranslator translator)
        {
            return new BuildRequestConfiguration(translator);
        }

        #endregion

        /// <summary>
        /// Applies the state from the specified instance to the loaded instance.  This overwrites the items and properties.
        /// </summary>
        /// <remarks>
        /// Used when we transfer results and state from a previous node to the current one.
        /// </remarks>
        internal void ApplyTransferredState(ProjectInstance instance)
        {
            if (instance != null)
            {
                _project.UpdateStateFrom(instance);
            }
        }

        /// <summary>
        /// Gets the name of the cache file for this configuration.
        /// </summary>
        internal string GetCacheFile()
        {
            string filename = Path.Combine(FileUtilities.GetCacheDirectory(), String.Format(CultureInfo.InvariantCulture, "Configuration{0}.cache", _configId));
            return filename;
        }

        /// <summary>
        /// Deletes the cache file
        /// </summary>
        internal void ClearCacheFile()
        {
            string cacheFile = GetCacheFile();
            if (File.Exists(cacheFile))
            {
                FileUtilities.DeleteNoThrow(cacheFile);
            }
        }

        /// <summary>
        /// Clones this BuildRequestConfiguration but sets a new configuration id.
        /// </summary>
        internal BuildRequestConfiguration ShallowCloneWithNewId(int newId)
        {
            return new BuildRequestConfiguration(newId, this);
        }

        /// <summary>
        /// Compares this object with another for equality
        /// </summary>
        /// <param name="other">The object with which to compare this one.</param>
        /// <returns>True if the objects contain the same data, false otherwise.</returns>
        private bool InternalEquals(BuildRequestConfiguration other)
        {
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            if ((other.WasGeneratedByNode == WasGeneratedByNode) &&
                (other._configId != 0) &&
                (_configId != 0))
            {
                return _configId == other._configId;
            }
            else
            {
                return _projectFullPath.Equals(other._projectFullPath, StringComparison.OrdinalIgnoreCase) &&
                       _toolsVersion.Equals(other._toolsVersion, StringComparison.OrdinalIgnoreCase) &&
                       _globalProperties.Equals(other._globalProperties);
            }
        }

        /// <summary>
        /// Determines what the real tools version is.
        /// </summary>
        private string ResolveToolsVersion(BuildRequestData data, string defaultToolsVersion, Utilities.GetToolset getToolset)
        {
            if (data.ExplicitToolsVersionSpecified)
            {
                return data.ExplicitlySpecifiedToolsVersion;
            }

            // None was specified by the call, fall back to the project's ToolsVersion attribute
            if (data.ProjectInstance != null)
            {
                return data.ProjectInstance.Toolset.ToolsVersion;
            }
            else if (FileUtilities.IsVCProjFilename(data.ProjectFullPath))
            {
                ProjectFileErrorUtilities.ThrowInvalidProjectFile(new BuildEventFileInfo(data.ProjectFullPath), "ProjectUpgradeNeededToVcxProj", data.ProjectFullPath);
            }
            else if (!FileUtilities.IsSolutionFilename(data.ProjectFullPath))
            {
                // If the file does not exist, it's a failure of the host, possibly msbuild.exe; so it's an ArgumentException here
                ErrorUtilities.VerifyThrowArgument(File.Exists(data.ProjectFullPath), "ProjectFileNotFound", data.ProjectFullPath);

                string toolsVersionFromFile = null;

                // We use an XmlTextReader to sniff, rather than simply loading a ProjectRootElement into the cache, because
                // quite likely this won't be the node on which the request will be built, so we'd be loading the ProjectRootElement
                // on this node unnecessarily.
                toolsVersionFromFile = XmlUtilities.SniffAttributeValueFromXmlFile(ProjectFullPath, XMakeAttributes.project, XMakeAttributes.toolsVersion);

                // Instead of just using the ToolsVersion from the file, though, ask our "source of truth" what the ToolsVersion 
                // we should use is.  This takes into account the various environment variables that can affect ToolsVersion, etc., 
                // to make it more likely that the ToolsVersion we come up with is going to be the one actually being used by the 
                // project at build time.  
                string toolsVersionToUse = Utilities.GenerateToolsVersionToUse
                    (
                        data.ExplicitlySpecifiedToolsVersion,
                        toolsVersionFromFile,
                        getToolset,
                        defaultToolsVersion
                    );

                return toolsVersionToUse;
            }

            // Couldn't find out the right ToolsVersion any other way, so just return the default. 
            return defaultToolsVersion;
        }

        /// <summary>
        /// Gets the translator for this configuration.
        /// </summary>
        private INodePacketTranslator GetConfigurationTranslator(TranslationDirection direction)
        {
            string cacheFile = GetCacheFile();
            try
            {
                if (direction == TranslationDirection.WriteToStream)
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(cacheFile));
                    return NodePacketTranslator.GetWriteTranslator(File.Create(cacheFile));
                }
                else
                {
                    // Not using sharedReadBuffer because this is not a memory stream and so the buffer won't be used anyway.
                    return NodePacketTranslator.GetReadTranslator(File.OpenRead(cacheFile), null);
                }
            }
            catch (Exception e)
            {
                if (e is DirectoryNotFoundException || e is UnauthorizedAccessException)
                {
                    ErrorUtilities.ThrowInvalidOperation("CacheFileInaccessible", cacheFile, e);
                }

                // UNREACHABLE
                throw;
            }
        }
    }
}
