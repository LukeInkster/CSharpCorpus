﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A collection of loaded projects.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Xml;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Collections;
using Microsoft.Build.Construction;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Internal;

using Constants = Microsoft.Build.Internal.Constants;
using ForwardingLoggerRecord = Microsoft.Build.Logging.ForwardingLoggerRecord;
using ILoggingService = Microsoft.Build.BackEnd.Logging.ILoggingService;
using InternalLoggerException = Microsoft.Build.Exceptions.InternalLoggerException;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using LoggerMode = Microsoft.Build.BackEnd.Logging.LoggerMode;
using ObjectModel = System.Collections.ObjectModel;
using ReservedPropertyNames = Microsoft.Build.Internal.ReservedPropertyNames;
using Utilities = Microsoft.Build.Internal.Utilities;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Flags for controlling the toolset initialization.
    /// </summary>
    [Flags]
    public enum ToolsetDefinitionLocations
    {
        /// <summary>
        /// Do not read toolset information from any external location.
        /// </summary>
        None = 0,

        /// <summary>
        /// Read toolset information from the exe configuration file.
        /// </summary>
        ConfigurationFile = 1,

        /// <summary>
        /// Read toolset information from the registry (HKLM\Software\Microsoft\MSBuild\ToolsVersions).
        /// </summary>
        Registry = 2
    }

    /// <summary>
    /// This class encapsulates a set of related projects, their toolsets, a default set of global properties,
    /// and the loggers that should be used to build them.
    /// A global version of this class acts as the default ProjectCollection.
    /// Multiple ProjectCollections can exist within an appdomain. However, these must not build concurrently.
    /// </summary>
    [SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "This is a collection of projects API review has approved this")]
    public class ProjectCollection : IToolsetProvider, IBuildComponent, IDisposable
    {
        /// <summary>
        /// The object to synchronize with when accessing certain fields.
        /// </summary>
        private readonly object _locker = new object();

        /// <summary>
        /// The global singleton project collection used as a default for otherwise
        /// unassociated projects.
        /// </summary>
        private static ProjectCollection s_globalProjectCollection;

        /// <summary>
        /// Gets the file version of the file in which the Engine assembly lies.
        /// </summary>
        /// <remarks>
        /// This is the Windows file version (specifically the value of the ProductVersion
        /// resource), not necessarily the assembly version.
        /// If you want the assembly version, use Constants.AssemblyVersion.
        /// </remarks>
        private static Version s_engineVersion;

        /// <summary>
        /// The projects loaded into this collection.
        /// </summary>
        private LoadedProjectCollection _loadedProjects;

        /// <summary>
        /// The component host for this collection.
        /// </summary>
        private IBuildComponentHost _host;

        /// <summary>
        /// Single logging service used for all builds of projects in this project collection
        /// </summary>
        private ILoggingService _loggingService;

        /// <summary>
        /// Any object exposing host services.
        /// May be null.
        /// </summary>
        private HostServices _hostServices;

        /// <summary>
        /// The locations where we look for toolsets.
        /// </summary>
        private ToolsetDefinitionLocations _toolsetDefinitionLocations;

        /// <summary>
        /// A mapping of tools versions to Toolsets, which contain the public Toolsets.
        /// This is the collection we use internally.
        /// </summary>
        private Dictionary<string, Toolset> _toolsets;

        /// <summary>
        /// The default global properties.
        /// </summary>
        private PropertyDictionary<ProjectPropertyInstance> _globalProperties;

        /// <summary>
        /// The properties representing the environment.
        /// </summary>
        private PropertyDictionary<ProjectPropertyInstance> _environmentProperties;

        /// <summary>
        /// The default tools version obtained by examining all of the toolsets.
        /// </summary>
        private string _defaultToolsVersion;

        /// <summary>
        /// A counter incremented every time the toolsets change which would necessitate a re-evaluation of
        /// associated projects.
        /// </summary>
        private int _toolsetsVersion;

        /// <summary>
        /// This is the default value used by newly created projects for whether or not the building
        /// of projects is enabled.  This is for security purposes in case a host wants to closely
        /// control which projects it allows to run targets/tasks.
        /// </summary>
        private bool _isBuildEnabled = true;

        /// <summary>
        /// We may only wish to log crtitical events, record that fact so we can apply it to build requests
        /// </summary>
        private bool _onlyLogCriticalEvents;

        /// <summary>
        /// Whether reevaluation is temporarily disabled on projects in this collection.
        /// This is useful when the host expects to make a number of reads and writes 
        /// to projects, and wants to temporarily sacrifice correctness for performance.
        /// </summary>
        private bool _skipEvaluation;

        /// <summary>
        /// Whether <see cref="Project.MarkDirty()">MarkDirty()</see> is temporarily disabled on
        /// projects in this collection.
        /// This allows, for example, global properties to be set without projects getting
        /// marked dirty for reevaluation as a consequence.
        /// </summary>
        private bool _disableMarkDirty;

        /// <summary>
        /// The maximum number of nodes which can be started during the build
        /// </summary>
        private int _maxNodeCount;

        /// <summary>
        /// The cache of project root elements associated with this project collection.
        /// Each is associated with a specific project collection for two reasons:
        /// - To help protect one project collection from any XML edits through another one:
        /// until a reload from disk - when it's ready to accept changes - it won't see the edits;
        /// - So that the owner of this project collection can force the XML to be loaded again
        /// from disk, by doing <see cref="UnloadAllProjects"/>.
        /// </summary>
        private ProjectRootElementCache _projectRootElementCache;

        /// <summary>
        /// Hook up last minute dumping of any exceptions bringing down the process
        /// </summary>
        static ProjectCollection()
        {
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(ExceptionHandling.UnhandledExceptionHandler);
        }

        /// <summary>
        /// Instantiates a project collection with no global properties or loggers that reads toolset 
        /// information from the configuration file and registry.
        /// </summary>
        public ProjectCollection()
            : this((IDictionary<string, string>)null)
        {
        }

        /// <summary>
        /// Instantiates a project collection using toolsets from the specified locations,
        /// and no global properties or loggers.
        /// May throw InvalidToolsetDefinitionException.
        /// </summary>
        /// <param name="toolsetLocations">The locations from which to load toolsets.</param>
        public ProjectCollection(ToolsetDefinitionLocations toolsetLocations)
            : this(null, null, toolsetLocations)
        {
        }

        /// <summary>
        /// Instantiates a project collection with specified global properties, no loggers,
        /// and that reads toolset information from the configuration file and registry.
        /// May throw InvalidToolsetDefinitionException.
        /// </summary>
        /// <param name="globalProperties">The default global properties to use. May be null.</param>
        public ProjectCollection(IDictionary<string, string> globalProperties)
            : this(globalProperties, null, ToolsetDefinitionLocations.ConfigurationFile | ToolsetDefinitionLocations.Registry)
        {
        }

        /// <summary>
        /// Instantiates a project collection with specified global properties and loggers and using the
        /// specified toolset locations.
        /// May throw InvalidToolsetDefinitionException.
        /// </summary>
        /// <param name="globalProperties">The default global properties to use. May be null.</param>
        /// <param name="loggers">The loggers to register. May be null.</param>
        /// <param name="toolsetDefinitionLocations">The locations from which to load toolsets.</param>
        public ProjectCollection(IDictionary<string, string> globalProperties, IEnumerable<ILogger> loggers, ToolsetDefinitionLocations toolsetDefinitionLocations)
            : this(globalProperties, loggers, null, toolsetDefinitionLocations, 1 /* node count */, false /* do not only log critical events */)
        {
        }

        /// <summary>
        /// Instantiates a project collection with specified global properties and loggers and using the
        /// specified toolset locations, node count, and setting of onlyLogCriticalEvents.
        /// Global properties and loggers may be null.
        /// Throws InvalidProjectFileException if any of the global properties are reserved.
        /// May throw InvalidToolsetDefinitionException.
        /// </summary>
        /// <param name="globalProperties">The default global properties to use. May be null.</param>
        /// <param name="loggers">The loggers to register. May be null and specified to any build instead.</param>
        /// <param name="remoteLoggers">Any remote loggers to register. May be null and specified to any build instead.</param>
        /// <param name="toolsetDefinitionLocations">The locations from which to load toolsets.</param>
        /// <param name="maxNodeCount">The maximum number of nodes to use for building.</param>
        /// <param name="onlyLogCriticalEvents">If set to true, only critical events will be logged.</param>
        public ProjectCollection(IDictionary<string, string> globalProperties, IEnumerable<ILogger> loggers, IEnumerable<ForwardingLoggerRecord> remoteLoggers, ToolsetDefinitionLocations toolsetDefinitionLocations, int maxNodeCount, bool onlyLogCriticalEvents)
        {
            _loadedProjects = new LoadedProjectCollection();
            _toolsetDefinitionLocations = toolsetDefinitionLocations;
            this.MaxNodeCount = maxNodeCount;
            this.ProjectRootElementCache = new ProjectRootElementCache(false /* do not automatically reload changed files from disk */);
            this.OnlyLogCriticalEvents = onlyLogCriticalEvents;

            try
            {
                CreateLoggingService(maxNodeCount, onlyLogCriticalEvents);

                RegisterLoggers(loggers);
                RegisterForwardingLoggers(remoteLoggers);

                if (globalProperties != null)
                {
                    _globalProperties = new PropertyDictionary<ProjectPropertyInstance>(globalProperties.Count);

                    foreach (KeyValuePair<string, string> pair in globalProperties)
                    {
                        try
                        {
                            _globalProperties.Set(ProjectPropertyInstance.Create(pair.Key, pair.Value));
                        }
                        catch (ArgumentException ex)
                        {
                            // Reserved or invalid property name
                            try
                            {
                                ProjectErrorUtilities.ThrowInvalidProject(ElementLocation.Create("MSBUILD"), "InvalidProperty", ex.Message);
                            }
                            catch (InvalidProjectFileException ex2)
                            {
                                BuildEventContext buildEventContext = new BuildEventContext(0 /* node ID */, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId);
                                LoggingService.LogInvalidProjectFileError(buildEventContext, ex2);
                                throw;
                            }
                        }
                    }
                }
                else
                {
                    _globalProperties = new PropertyDictionary<ProjectPropertyInstance>();
                }

                InitializeToolsetCollection();
            }
            catch (Exception)
            {
                ShutDownLoggingService();
                throw;
            }

            ProjectRootElementCache.ProjectRootElementAddedHandler += new Evaluation.ProjectRootElementCache.ProjectRootElementCacheAddEntryHandler(ProjectRootElementCache_ProjectRootElementAddedHandler);
            ProjectRootElementCache.ProjectRootElementDirtied += ProjectRootElementCache_ProjectRootElementDirtiedHandler;
            ProjectRootElementCache.ProjectDirtied += ProjectRootElementCache_ProjectDirtiedHandler;
        }

        /// <summary>
        /// Handler to receive which project got added to the project collection.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "This has been API reviewed")]
        public delegate void ProjectAddedEventHandler(object sender, ProjectAddedToProjectCollectionEventArgs e);

        /// <summary>
        /// Event that is fired when a project is added to the ProjectRootElementCache of this project collection.
        /// </summary>
        public event ProjectAddedEventHandler ProjectAdded;

        /// <summary>
        /// Raised when state is changed on this instance.
        /// </summary>
        /// <remarks>
        /// This event is NOT raised for changes in individual projects.
        /// </remarks>
        public event EventHandler<ProjectCollectionChangedEventArgs> ProjectCollectionChanged;

        /// <summary>
        /// Raised when a <see cref="ProjectRootElement"/> contained by this instance is changed.
        /// </summary>
        /// <remarks>
        /// This event is NOT raised for changes to global properties, or any other change that doesn't actually dirty the XML.
        /// </remarks>
        public event EventHandler<ProjectXmlChangedEventArgs> ProjectXmlChanged;

        /// <summary>
        /// Raised when a <see cref="Project"/> contained by this instance is directly changed.
        /// </summary>
        /// <remarks>
        /// This event is NOT raised for direct project XML changes via the construction model.
        /// </remarks>
        public event EventHandler<ProjectChangedEventArgs> ProjectChanged;

        /// <summary>
        /// Retrieves the global project collection object.
        /// This is a singleton project collection with no global properties or loggers that reads toolset 
        /// information from the configuration file and registry.
        /// May throw InvalidToolsetDefinitionException.
        /// Thread safe.
        /// </summary>
        public static ProjectCollection GlobalProjectCollection
        {
            get
            {
                if (s_globalProjectCollection == null)
                {
                    // Take care to ensure that there is never more than one value observed
                    // from this property even in the case of race conditions while lazily initializing.
                    var local = new ProjectCollection();
                    Interlocked.CompareExchange(ref s_globalProjectCollection, local, null);
                }

                return s_globalProjectCollection;
            }
        }

        /// <summary>
        /// Gets the file version of the file in which the Engine assembly lies.
        /// </summary>
        /// <remarks>
        /// This is the Windows file version (specifically the value of the FileVersion
        /// resource), not necessarily the assembly version.
        /// If you want the assembly version, use Constants.AssemblyVersion.
        /// This is not the <see cref="ToolsetsVersion">ToolsetCollectionVersion</see>.
        /// </remarks>
        public static Version Version
        {
            get
            {
                if (s_engineVersion == null)
                {
                    // Get the file version from the currently executing assembly.
                    // Use .CodeBase instead of .Location, because .Location doesn't
                    // work when Microsoft.Build.dll has been shadow-copied, for example
                    // in scenarios where NUnit is loading Microsoft.Build.
                    s_engineVersion = new Version(FileVersionInfo.GetVersionInfo(FileUtilities.ExecutingAssemblyPath).FileVersion);
                }

                return s_engineVersion;
            }
        }

        /// <summary>
        /// The default tools version of this project collection. Projects use this tools version if they
        /// aren't otherwise told what tools version to use.
        /// This value is gotten from the .exe.config file, or else in the registry, 
        /// or if neither specify a default tools version then it is hard-coded to the tools version "2.0".
        /// Setter throws InvalidOperationException if a toolset with the provided tools version has not been defined.
        /// Always defined.
        /// </summary>
        public string DefaultToolsVersion
        {
            get
            {
                lock (_locker)
                {
                    ErrorUtilities.VerifyThrow(_defaultToolsVersion != null, "Should have a default");
                    return _defaultToolsVersion;
                }
            }

            set
            {
                ProjectCollectionChangedEventArgs eventArgs = null;
                lock (_locker)
                {
                    ErrorUtilities.VerifyThrowArgumentLength(value, "DefaultToolsVersion");

                    if (!_toolsets.ContainsKey(value))
                    {
                        string toolsVersionList = Microsoft.Build.Internal.Utilities.CreateToolsVersionListString(Toolsets);
                        ErrorUtilities.ThrowInvalidOperation("UnrecognizedToolsVersion", value, toolsVersionList);
                    }

                    if (_defaultToolsVersion != value)
                    {
                        _defaultToolsVersion = value;

                        eventArgs = new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.DefaultToolsVersion);
                    }
                }

                OnProjectCollectionChangedIfNonNull(eventArgs);
            }
        }

        /// <summary>
        /// Returns default global properties for all projects in this collection.
        /// Read-only dead dictionary.
        /// </summary>
        /// <remarks>
        /// This is the publicly exposed getter, that translates into a read-only dead IDictionary&lt;string, string&gt;.
        /// 
        /// To be consistent with Project, setting and removing global properties is done with 
        /// <see cref="SetGlobalProperty">SetGlobalProperty</see> and <see cref="RemoveGlobalProperty">RemoveGlobalProperty</see>.
        /// </remarks>
        public IDictionary<string, string> GlobalProperties
        {
            get
            {
                lock (_locker)
                {
                    if (_globalProperties.Count == 0)
                    {
                        return ReadOnlyEmptyDictionary<string, string>.Instance;
                    }

                    Dictionary<string, string> dictionary = new Dictionary<string, string>(_globalProperties.Count, MSBuildNameIgnoreCaseComparer.Default);

                    foreach (ProjectPropertyInstance property in _globalProperties)
                    {
                        dictionary[property.Name] = ((IProperty)property).EvaluatedValueEscaped;
                    }

                    return new ObjectModel.ReadOnlyDictionary<string, string>(dictionary);
                }
            }
        }

        /// <summary>
        /// All the projects currently loaded into this collection.
        /// Each has a unique combination of path, global properties, and tools version.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1721:PropertyNamesShouldNotMatchGetMethods", Justification = "This is a reasonable choice. API review approved")]
        public ICollection<Project> LoadedProjects
        {
            get
            {
                lock (_locker)
                {
                    return new List<Project>(_loadedProjects);
                }
            }
        }

        /// <summary>
        /// Number of projects currently loaded into this collection.
        /// </summary>
        public int Count
        {
            get
            {
                lock (_locker)
                {
                    return _loadedProjects.Count;
                }
            }
        }

        /// <summary>
        /// Loggers that all contained projects will use for their builds.
        /// Loggers are added with the <see cref="RegisterLogger"/>.
        /// UNDONE: Currently they cannot be removed.
        /// Returns an empty collection if there are no loggers.
        /// </summary>
        public ICollection<ILogger> Loggers
        {
            [DebuggerStepThrough]
            get
            {
                lock (_locker)
                {
                    return (_loggingService.Loggers == null) ?
                    (ICollection<ILogger>)ReadOnlyEmptyCollection<ILogger>.Instance :
                    new List<ILogger>(_loggingService.Loggers);
                }
            }
        }

        /// <summary>
        /// Returns the toolsets this ProjectCollection knows about.
        /// </summary>
        /// <comments>
        /// ValueCollection is already read-only
        /// </comments>
        public ICollection<Toolset> Toolsets
        {
            get
            {
                lock (_locker)
                {
                    return new List<Toolset>(_toolsets.Values);
                }
            }
        }

        /// <summary>
        /// Returns the locations used to find the toolsets.
        /// </summary>
        public ToolsetDefinitionLocations ToolsetLocations
        {
            [DebuggerStepThrough]
            get
            {
                lock (_locker)
                {
                    return _toolsetDefinitionLocations;
                }
            }
        }

        /// <summary>
        /// This is the default value used by newly created projects for whether or not the building
        /// of projects is enabled.  This is for security purposes in case a host wants to closely
        /// control which projects it allows to run targets/tasks.
        /// </summary>
        public bool IsBuildEnabled
        {
            [DebuggerStepThrough]
            get
            {
                lock (_locker)
                {
                    return _isBuildEnabled;
                }
            }

            [DebuggerStepThrough]
            set
            {
                ProjectCollectionChangedEventArgs eventArgs = null;
                lock (_locker)
                {
                    if (_isBuildEnabled != value)
                    {
                        _isBuildEnabled = value;

                        eventArgs = new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.IsBuildEnabled);
                    }
                }

                OnProjectCollectionChangedIfNonNull(eventArgs);
            }
        }

        /// <summary>
        /// When true, only log critical events such as warnings and errors. Has to be in here for API compat
        /// </summary>
        public bool OnlyLogCriticalEvents
        {
            get
            {
                lock (_locker)
                {
                    return _onlyLogCriticalEvents;
                }
            }

            set
            {
                ProjectCollectionChangedEventArgs eventArgs = null;
                lock (_locker)
                {
                    if (_onlyLogCriticalEvents != value)
                    {
                        _onlyLogCriticalEvents = value;

                        eventArgs = new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.OnlyLogCriticalEvents);
                    }
                }

                OnProjectCollectionChangedIfNonNull(eventArgs);
            }
        }

        /// <summary>
        /// Object exposing host services to tasks during builds of projects
        /// contained by this project collection.
        /// By default, <see cref="HostServices">HostServices</see> is used.
        /// May be set to null, but the getter will create a new instance in that case.
        /// </summary>
        public HostServices HostServices
        {
            get
            {
                lock (_locker)
                {
                    if (_hostServices == null)
                    {
                        _hostServices = new HostServices();
                    }

                    return _hostServices;
                }
            }

            set
            {
                ProjectCollectionChangedEventArgs eventArgs = null;
                lock (_locker)
                {
                    if (_hostServices != value)
                    {
                        _hostServices = value;
                        eventArgs = new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.HostServices);
                    }
                }

                OnProjectCollectionChangedIfNonNull(eventArgs);
            }
        }

        /// <summary>
        /// Whether reevaluation is temporarily disabled on projects in this collection.
        /// This is useful when the host expects to make a number of reads and writes 
        /// to projects, and wants to temporarily sacrifice correctness for performance.
        /// </summary>
        public bool SkipEvaluation
        {
            get
            {
                lock (_locker)
                {
                    return _skipEvaluation;
                }
            }

            set
            {
                ProjectCollectionChangedEventArgs eventArgs = null;
                lock (_locker)
                {
                    if (_skipEvaluation != value)
                    {
                        _skipEvaluation = value;

                        eventArgs = new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.SkipEvaluation);
                    }
                }

                OnProjectCollectionChangedIfNonNull(eventArgs);
            }
        }

        /// <summary>
        /// Whether <see cref="Project.MarkDirty()">MarkDirty()</see> is temporarily disabled on
        /// projects in this collection.
        /// This allows, for example, global properties to be set without projects getting
        /// marked dirty for reevaluation as a consequence.
        /// </summary>
        public bool DisableMarkDirty
        {
            get
            {
                lock (_locker)
                {
                    return _disableMarkDirty;
                }
            }

            set
            {
                ProjectCollectionChangedEventArgs eventArgs = null;
                lock (_locker)
                {
                    if (_disableMarkDirty != value)
                    {
                        _disableMarkDirty = value;

                        eventArgs = new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.DisableMarkDirty);
                    }
                }

                OnProjectCollectionChangedIfNonNull(eventArgs);
            }
        }

        /// <summary>
        /// Logging service that should be used for project load and for builds
        /// </summary>
        internal ILoggingService LoggingService
        {
            [DebuggerStepThrough]
            get
            {
                lock (_locker)
                {
                    return _loggingService;
                }
            }
        }

        /// <summary>
        /// Gets default global properties for all projects in this collection.
        /// Dead copy.
        /// </summary>
        internal PropertyDictionary<ProjectPropertyInstance> GlobalPropertiesCollection
        {
            [DebuggerStepThrough]
            get
            {
                lock (_locker)
                {
                    var clone = new PropertyDictionary<ProjectPropertyInstance>();

                    foreach (var property in _globalProperties)
                    {
                        clone.Set(property.DeepClone());
                    }

                    return clone;
                }
            }
        }

        /// <summary>
        /// Returns the property dictionary containing the properties representing the environment.
        /// </summary>
        internal PropertyDictionary<ProjectPropertyInstance> EnvironmentProperties
        {
            get
            {
                lock (_locker)
                {
                    // Retrieves the environment properties.
                    // This is only done once, when the project collection is created. Any subsequent
                    // environment changes will be ignored. Child nodes will be passed this set
                    // of properties in their build parameters.
                    if (null == _environmentProperties)
                    {
                        _environmentProperties = Microsoft.Build.Internal.Utilities.GetEnvironmentProperties();
                    }

                    return new PropertyDictionary<ProjectPropertyInstance>(_environmentProperties);
                }
            }
        }

        /// <summary>
        /// Returns the internal version for this object's state.
        /// Updated when toolsets change, indicating all contained projects are potentially invalid.
        /// </summary>
        internal int ToolsetsVersion
        {
            [DebuggerStepThrough]
            get
            {
                lock (_locker)
                {
                    return _toolsetsVersion;
                }
            }
        }

        /// <summary>
        /// The maximum number of nodes which can be started during the build
        /// </summary>
        internal int MaxNodeCount
        {
            get
            {
                lock (_locker)
                {
                    return _maxNodeCount;
                }
            }

            set
            {
                lock (_locker)
                {
                    _maxNodeCount = value;
                }
            }
        }

        /// <summary>
        /// The cache of project root elements associated with this project collection.
        /// Each is associated with a specific project collection for two reasons:
        /// - To help protect one project collection from any XML edits through another one:
        /// until a reload from disk - when it's ready to accept changes - it won't see the edits;
        /// - So that the owner of this project collection can force the XML to be loaded again
        /// from disk, by doing <see cref="UnloadAllProjects"/>.
        /// </summary>
        internal ProjectRootElementCache ProjectRootElementCache
        {
            get
            {
                // no locks required because this field is only set in the constructor.
                return _projectRootElementCache;
            }

            private set
            {
                // no locks required because this field is only set in the constructor.
                _projectRootElementCache = value;
            }
        }

        /// <summary>
        /// Escape a string using MSBuild escaping format. For example, "%3b" for ";".
        /// Only characters that are especially significant to MSBuild parsing are escaped.
        /// Callers can use this method to make a string safe to be parsed to other methods
        /// that would otherwise expand it; or to make a string safe to be written to a project file.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "string", Justification = "Public API that has shipped")]
        public static string Escape(string unescapedString)
        {
            return EscapingUtilities.Escape(unescapedString);
        }

        /// <summary>
        /// Unescape a string using MSBuild escaping format. For example, "%3b" for ";".
        /// All escaped characters are unescaped.
        /// </summary>
        [SuppressMessage("Microsoft.Naming", "CA1720:IdentifiersShouldNotContainTypeNames", MessageId = "string", Justification = "Public API that has shipped")]
        public static string Unescape(string escapedString)
        {
            return EscapingUtilities.UnescapeAll(escapedString);
        }

        /// <summary>
        /// Returns true if there is a toolset defined for the specified 
        /// tools version, otherwise false.
        /// </summary>
        public bool ContainsToolset(string toolsVersion)
        {
            lock (_locker)
            {
                bool result = GetToolset(toolsVersion) != null;

                return result;
            }
        }

        /// <summary>
        /// Add a new toolset.
        /// Replaces any existing toolset with the same tools version.
        /// </summary>
        public void AddToolset(Toolset toolset)
        {
            lock (_locker)
            {
                ErrorUtilities.VerifyThrowArgumentNull(toolset, "toolset");

                _toolsets[toolset.ToolsVersion] = toolset;

                _toolsetsVersion++;
            }

            OnProjectCollectionChanged(new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.Toolsets));
        }

        /// <summary>
        /// Remove a toolset.
        /// Returns true if it was present, otherwise false.
        /// </summary>
        public bool RemoveToolset(string toolsVersion)
        {
            bool changed;
            lock (_locker)
            {
                changed = RemoveToolsetInternal(toolsVersion);
            }

            if (changed)
            {
                OnProjectCollectionChanged(new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.Toolsets));
            }

            return changed;
        }

        /// <summary>
        /// Removes all toolsets.
        /// </summary>
        public void RemoveAllToolsets()
        {
            bool changed = false;
            lock (_locker)
            {
                List<Toolset> toolsets = new List<Toolset>(Toolsets);

                foreach (Toolset toolset in toolsets)
                {
                    changed |= RemoveToolsetInternal(toolset.ToolsVersion);
                }
            }

            if (changed)
            {
                OnProjectCollectionChanged(new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.Toolsets));
            }
        }

        /// <summary>
        /// Get the toolset with the specified tools version.
        /// If it is not present, returns null.
        /// </summary>
        public Toolset GetToolset(string toolsVersion)
        {
            lock (_locker)
            {
                ErrorUtilities.VerifyThrowArgumentLength(toolsVersion, "toolsVersion");

                Toolset toolset;
                _toolsets.TryGetValue(toolsVersion, out toolset);

                return toolset;
            }
        }

        /// <summary>
        /// Figure out what ToolsVersion to use to actually build the project with. 
        /// </summary>
        /// <param name="explicitToolsVersion">The user-specified ToolsVersion (through e.g. /tv: on the command line). May be null</param>
        /// <param name="toolsVersionFromProject">The ToolsVersion from the project file. May be null</param>
        /// <returns>The ToolsVersion we should use to build this project.  Should never be null.</returns>
        public string GetEffectiveToolsVersion(string explicitToolsVersion, string toolsVersionFromProject)
        {
            return Utilities.GenerateToolsVersionToUse(explicitToolsVersion, toolsVersionFromProject, GetToolset, DefaultToolsVersion);
        }

        /// <summary>
        /// Returns any and all loaded projects with the provided path.
        /// There may be more than one, if they are distinguished by global properties
        /// and/or tools version.
        /// </summary>
        public ICollection<Project> GetLoadedProjects(string fullPath)
        {
            lock (_locker)
            {
                var loaded = new List<Project>(_loadedProjects.GetMatchingProjectsIfAny(fullPath));

                return loaded;
            }
        }

        /// <summary>
        /// Loads a project with the specified filename, using the collection's global properties and tools version.
        /// If a matching project is already loaded, it will be returned, otherwise a new project will be loaded.
        /// </summary>
        /// <param name="fileName">The project file to load</param>
        /// <returns>A loaded project.</returns>
        public Project LoadProject(string fileName)
        {
            return LoadProject(fileName, null);
        }

        /// <summary>
        /// Loads a project with the specified filename and tools version, using the collection's global properties.
        /// If a matching project is already loaded, it will be returned, otherwise a new project will be loaded.
        /// </summary>
        /// <param name="fileName">The project file to load</param>
        /// <param name="toolsVersion">The tools version to use. May be null.</param>
        /// <returns>A loaded project.</returns>
        public Project LoadProject(string fileName, string toolsVersion)
        {
            return LoadProject(fileName, null /* use project collection's global properties */, toolsVersion);
        }

        /// <summary>
        /// Loads a project with the specified filename, tools version and global properties.
        /// If a matching project is already loaded, it will be returned, otherwise a new project will be loaded.
        /// </summary>
        /// <param name="fileName">The project file to load</param>
        /// <param name="globalProperties">The global properties to use. May be null, in which case the containing project collection's global properties will be used.</param>
        /// <param name="toolsVersion">The tools version. May be null.</param>
        /// <returns>A loaded project.</returns>
        public Project LoadProject(string fileName, IDictionary<string, string> globalProperties, string toolsVersion)
        {
            lock (_locker)
            {
                ErrorUtilities.VerifyThrowArgumentLength(fileName, "fileName");
                BuildEventContext buildEventContext = new BuildEventContext(0 /* node ID */, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTaskId);

                if (globalProperties == null)
                {
                    globalProperties = this.GlobalProperties;
                }
                else
                {
                    // We need to update the set of global properties to merge in the ProjectCollection global properties --
                    // otherwise we might end up declaring "not matching" a project that actually does ... and then throw
                    // an exception when we go to actually add the newly created project to the ProjectCollection. 
                    // BUT remember that project global properties win -- don't override a property that already exists.
                    foreach (KeyValuePair<string, string> globalProperty in this.GlobalProperties)
                    {
                        if (!globalProperties.ContainsKey(globalProperty.Key))
                        {
                            globalProperties.Add(globalProperty);
                        }
                    }
                }

                // We do not control the current directory at this point, but assume that if we were
                // passed a relative path, the caller assumes we will prepend the current directory.
                fileName = FileUtilities.NormalizePath(fileName);
                string toolsVersionFromProject = null;

                if (toolsVersion == null)
                {
                    // Load the project XML to get any ToolsVersion attribute. 
                    // If there isn't already an equivalent project loaded, the real load we'll do will be satisfied from the cache.
                    // If there is already an equivalent project loaded, we'll never need this XML -- but it'll already 
                    // have been loaded by that project so it will have been satisfied from the ProjectRootElementCache.
                    // Either way, no time wasted.
                    try
                    {
                        ProjectRootElement xml = ProjectRootElement.OpenProjectOrSolution(fileName, globalProperties, toolsVersion, LoggingService, ProjectRootElementCache, buildEventContext, true /*explicitlyloaded*/);
                        toolsVersionFromProject = (xml.ToolsVersion.Length > 0) ? xml.ToolsVersion : DefaultToolsVersion;
                    }
                    catch (InvalidProjectFileException ex)
                    {
                        LoggingService.LogInvalidProjectFileError(buildEventContext, ex);
                        throw;
                    }
                }

                string effectiveToolsVersion = Utilities.GenerateToolsVersionToUse(toolsVersion, toolsVersionFromProject, GetToolset, DefaultToolsVersion);
                Project project = _loadedProjects.GetMatchingProjectIfAny(fileName, globalProperties, effectiveToolsVersion);

                if (project == null)
                {
                    // The Project constructor adds itself to our collection,
                    // it is not done by us
                    project = new Project(fileName, globalProperties, effectiveToolsVersion, this);
                }

                return project;
            }
        }

        /// <summary>
        /// Loads a project with the specified reader, using the collection's global properties and tools version.
        /// The project will be added to this project collection when it is named.
        /// </summary>
        /// <param name="xmlReader">Xml reader to read project from</param>
        /// <returns>A loaded project.</returns>
        public Project LoadProject(XmlReader xmlReader)
        {
            return LoadProject(xmlReader, null);
        }

        /// <summary>
        /// Loads a project with the specified reader and tools version, using the collection's global properties.
        /// The project will be added to this project collection when it is named.
        /// </summary>
        /// <param name="xmlReader">Xml reader to read project from</param>
        /// <param name="toolsVersion">The tools version to use. May be null.</param>
        /// <returns>A loaded project.</returns>
        public Project LoadProject(XmlReader xmlReader, string toolsVersion)
        {
            return LoadProject(xmlReader, null /* use project collection's global properties */, toolsVersion);
        }

        /// <summary>
        /// Loads a project with the specified reader, tools version and global properties.
        /// The project will be added to this project collection when it is named.
        /// </summary>
        /// <param name="xmlReader">Xml reader to read project from</param>
        /// <param name="globalProperties">The global properties to use. May be null in which case the containing project collection's global properties will be used.</param>
        /// <param name="toolsVersion">The tools version. May be null.</param>
        /// <returns>A loaded project.</returns>
        public Project LoadProject(XmlReader xmlReader, IDictionary<string, string> globalProperties, string toolsVersion)
        {
            return new Project(xmlReader, globalProperties, toolsVersion, this);
        }

        /// <summary>
        /// Adds a logger to the collection of loggers used for builds of projects in this collection.
        /// If the logger object is already in the collection, does nothing.
        /// </summary>
        public void RegisterLogger(ILogger logger)
        {
            lock (_locker)
            {
                RegisterLoggerInternal(logger);
            }

            OnProjectCollectionChanged(new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.Loggers));
        }

        /// <summary>
        /// Adds some loggers to the collection of loggers used for builds of projects in this collection.
        /// If any logger object is already in the collection, does nothing for that logger.
        /// May be null.
        /// </summary>
        public void RegisterLoggers(IEnumerable<ILogger> loggers)
        {
            bool changed = false;
            if (loggers != null)
            {
                lock (_locker)
                {
                    foreach (ILogger logger in loggers)
                    {
                        RegisterLoggerInternal(logger);
                        changed = true;
                    }
                }
            }

            if (changed)
            {
                OnProjectCollectionChanged(new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.Loggers));
            }
        }

        /// <summary>
        /// Adds some remote loggers to the collection of remote loggers used for builds of projects in this collection.
        /// May be null.
        /// </summary>
        public void RegisterForwardingLoggers(IEnumerable<ForwardingLoggerRecord> remoteLoggers)
        {
            lock (_locker)
            {
                if (remoteLoggers != null)
                {
                    foreach (ForwardingLoggerRecord remoteLoggerRecord in remoteLoggers)
                    {
                        _loggingService.RegisterDistributedLogger(new ReusableLogger(remoteLoggerRecord.CentralLogger), remoteLoggerRecord.ForwardingLoggerDescription);
                    }
                }
            }

            OnProjectCollectionChanged(new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.Loggers));
        }

        /// <summary>
        /// Removes all loggers from the collection of loggers used for builds of projects in this collection.
        /// </summary>
        public void UnregisterAllLoggers()
        {
            lock (_locker)
            {
                _loggingService.UnregisterAllLoggers();

                // UNDONE: Logging service should not shut down when all loggers are unregistered.
                // VS unregisters all loggers on the same project collection often. To workaround this, we have to create it again now!
                CreateLoggingService(MaxNodeCount, OnlyLogCriticalEvents);
            }

            OnProjectCollectionChanged(new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.Loggers));
        }

        /// <summary>
        /// Unloads the specific project specified.
        /// Host should call this when they are completely done with the project.
        /// If project was not already loaded, throws InvalidOperationException.
        /// </summary>
        public void UnloadProject(Project project)
        {
            lock (_locker)
            {
                bool existed = _loadedProjects.RemoveProject(project);

                ErrorUtilities.VerifyThrowInvalidOperation(existed, "OM_ProjectWasNotLoaded");

                project.Zombify();

                // If we've removed the last entry for the given project full path
                // then unregister any and all host objects for that project
                if (_hostServices != null && _loadedProjects.GetMatchingProjectsIfAny(project.FullPath).Count == 0)
                {
                    _hostServices.UnregisterProject(project.FullPath);
                }

                // Release our own cache's strong references to try to help
                // free memory. These may be the last references to the ProjectRootElements
                // in the cache, so the cache shouldn't hold strong references to them of its own.
                ProjectRootElementCache.DiscardStrongReferences();

                // Aggressively release any strings from all the contributing documents.
                // It's fine if we cache less (by now we likely did a lot of loading and got the benefits)
                // If we don't do this, we could be releasing the last reference to a 
                // ProjectRootElement, causing it to fall out of the weak cache leaving its strings and XML
                // behind in the string cache.
                project.Xml.XmlDocument.ClearAnyCachedStrings();

                foreach (var import in project.Imports)
                {
                    import.ImportedProject.XmlDocument.ClearAnyCachedStrings();
                }
            }
        }

        /// <summary>
        /// Unloads a project XML root element from the weak cache.
        /// </summary>
        /// <param name="projectRootElement">The project XML root element to unload.</param>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the project XML root element to unload is still in use by a loaded project or its imports.
        /// </exception>
        /// <remarks>
        /// This method is useful for the case where the host knows that all projects using this XML element
        /// are unloaded, and desires to discard any unsaved changes.
        /// </remarks>
        public void UnloadProject(ProjectRootElement projectRootElement)
        {
            lock (_locker)
            {
                ErrorUtilities.VerifyThrowArgumentNull(projectRootElement, "projectRootElement");

                Project conflictingProject = LoadedProjects.FirstOrDefault(project => project.UsesProjectRootElement(projectRootElement));

                if (conflictingProject != null)
                {
                    ErrorUtilities.ThrowInvalidOperation("OM_ProjectXmlCannotBeUnloadedDueToLoadedProjects", projectRootElement.FullPath, conflictingProject.FullPath);
                }

                projectRootElement.XmlDocument.ClearAnyCachedStrings();
                ProjectRootElementCache.DiscardAnyWeakReference(projectRootElement);
            }
        }

        /// <summary>
        /// Unloads all the projects contained by this ProjectCollection.
        /// Host should call this when they are completely done with all the projects.
        /// </summary>
        public void UnloadAllProjects()
        {
            lock (_locker)
            {
                foreach (Project project in _loadedProjects)
                {
                    project.Zombify();

                    // We're removing every entry from the project collection
                    // so unregister any and all host objects for each project
                    if (_hostServices != null)
                    {
                        _hostServices.UnregisterProject(project.FullPath);
                    }
                }

                _loadedProjects.RemoveAllProjects();

                ProjectRootElementCache.Clear();
            }
        }

        /// <summary>
        /// Get any global property on the project collection that has the specified name,
        /// otherwise returns null.
        /// </summary>
        public ProjectPropertyInstance GetGlobalProperty(string name)
        {
            lock (_locker)
            {
                return _globalProperties[name];
            }
        }

        /// <summary>
        /// Set a global property at the collection-level,
        /// and on all projects in the project collection.
        /// </summary>
        public void SetGlobalProperty(string name, string value)
        {
            ProjectCollectionChangedEventArgs eventArgs = null;
            lock (_locker)
            {
                ProjectPropertyInstance propertyInGlobalProperties = _globalProperties.GetProperty(name);
                bool changed = propertyInGlobalProperties == null || (!String.Equals(((IValued)propertyInGlobalProperties).EscapedValue, value, StringComparison.OrdinalIgnoreCase));

                if (changed)
                {
                    _globalProperties.Set(ProjectPropertyInstance.Create(name, value));
                    eventArgs = new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.GlobalProperties);
                }

                // Copy LoadedProjectCollection as modifying a project's global properties will cause it to re-add
                List<Project> projects = new List<Project>(_loadedProjects);
                foreach (Project project in projects)
                {
                    project.SetGlobalProperty(name, value);
                }
            }

            OnProjectCollectionChangedIfNonNull(eventArgs);
        }

        /// <summary>
        /// Removes a global property from the collection-level set of global properties,
        /// and all projects in the project collection.
        /// If it was on this project collection, returns true.
        /// </summary>
        public bool RemoveGlobalProperty(string name)
        {
            bool set;
            lock (_locker)
            {
                set = _globalProperties.Remove(name);

                // Copy LoadedProjectCollection as modifying a project's global properties will cause it to re-add
                List<Project> projects = new List<Project>(_loadedProjects);
                foreach (Project project in projects)
                {
                    project.RemoveGlobalProperty(name);
                }
            }

            OnProjectCollectionChanged(new ProjectCollectionChangedEventArgs(ProjectCollectionChangedState.GlobalProperties));

            return set;
        }

        /// <summary>
        /// Called when a host is completely done with the project collection.
        /// UNDONE: This is a hack to make sure the logging thread shuts down if the build used the loggingservice
        /// off the ProjectCollection. After CTP we need to rationalize this and see if we can remove the logging service from
        /// the project collection entirely so this isn't necessary.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #region IBuildComponent Members

        /// <summary>
        /// Initializes the component with the component host.
        /// </summary>
        /// <param name="host">The component host.</param>
        void IBuildComponent.InitializeComponent(IBuildComponentHost host)
        {
            _host = host;
        }

        /// <summary>
        /// Shuts down the component.
        /// </summary>
        void IBuildComponent.ShutdownComponent()
        {
            _host = null;
        }

        #endregion

        /// <summary>
        /// Unloads a project XML root element from the cache entirely, if it is not
        /// in use by project loaded into this collection.
        /// Returns true if it was unloaded successfully, or was not already loaded.
        /// Returns false if it was not unloaded because it was still in use by a loaded <see cref="Project"/>.
        /// </summary>
        /// <param name="projectRootElement">The project XML root element to unload.</param>
        public bool TryUnloadProject(ProjectRootElement projectRootElement)
        {
            lock (_locker)
            {
                ErrorUtilities.VerifyThrowArgumentNull(projectRootElement, "projectRootElement");

                ProjectRootElementCache.DiscardStrongReferences();

                Project conflictingProject = LoadedProjects.FirstOrDefault(project => project.UsesProjectRootElement(projectRootElement));

                if (conflictingProject == null)
                {
                    ProjectRootElementCache.DiscardAnyWeakReference(projectRootElement);
                    projectRootElement.XmlDocument.ClearAnyCachedStrings();
                    return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Called by a Project object to load itself into this collection.
        /// If the project was already loaded under a different name, it is unloaded.
        /// Stores the project in the list of loaded projects if it has a name.
        /// Does not store the project if it has no name because it has not been saved to disk yet.
        /// If the project previously had a name, but was not in the collection already, throws InvalidOperationException.
        /// If the project was not previously in the collection, sets the collection's global properties on it.
        /// </summary>
        internal void OnAfterRenameLoadedProject(string oldFullPathIfAny, Project project)
        {
            lock (_locker)
            {
                if (project.FullPath == null)
                {
                    return;
                }

                if (oldFullPathIfAny != null)
                {
                    bool existed = _loadedProjects.RemoveProject(oldFullPathIfAny, project);

                    ErrorUtilities.VerifyThrowInvalidOperation(existed, "OM_ProjectWasNotLoaded");
                }

                // The only time this ever gets called with a null full path is when the project is first being 
                // constructed.  The mere fact that this method is being called means that this project will belong 
                // to this project collection.  As such, it has already had all necessary global properties applied 
                // when being constructed -- we don't need to do anything special here. 
                // If we did add global properties here, we would just end up either duplicating work or possibly 
                // wiping out global properties set on the project meant to override the ProjectCollection copies. 
                _loadedProjects.AddProject(project);

                if (_hostServices != null)
                {
                    HostServices.OnRenameProject(oldFullPathIfAny, project.FullPath);
                }
            }
        }

        /// <summary>
        /// Called after a loaded project's global properties are changed, so we can update
        /// our loaded project table.
        /// Project need not already be in the project collection yet, but it can't be in another one.
        /// </summary>
        /// <remarks>
        /// We have to remove and re-add so that there's an error if there's already an equivalent
        /// project loaded.
        /// </remarks>
        internal void AfterUpdateLoadedProjectGlobalProperties(Project project)
        {
            lock (_locker)
            {
                ErrorUtilities.VerifyThrowInvalidOperation(Object.ReferenceEquals(project.ProjectCollection, this), "OM_IncorrectObjectAssociation", "Project", "ProjectCollection");

                if (project.FullPath == null)
                {
                    return;
                }

                bool existed = _loadedProjects.RemoveProject(project);

                if (existed)
                {
                    _loadedProjects.AddProject(project);
                }
            }
        }

        /// <summary>
        /// Following standard framework guideline dispose pattern.
        /// Shut down logging service if the project collection owns one, in order
        /// to shut down the logger thread and loggers.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources..</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                ShutDownLoggingService();
                Tracing.Dump();
            }
        }

        /// <summary>
        /// Remove a toolset and does not raise events. The caller should have acquired a lock on this method's behalf.
        /// </summary>
        /// <param name="toolsVersion">The toolset to remove.</param>
        /// <returns><c>true</c> if the toolset was found and removed; <c>false</c> otherwise.</returns>
        private bool RemoveToolsetInternal(string toolsVersion)
        {
            ErrorUtilities.VerifyThrowArgumentLength(toolsVersion, "toolsVersion");
            Debug.Assert(Monitor.IsEntered(_locker));

            if (!_toolsets.ContainsKey(toolsVersion))
            {
                return false;
            }

            _toolsets.Remove(toolsVersion);

            _toolsetsVersion++;

            return true;
        }

        /// <summary>
        /// Adds a logger to the collection of loggers used for builds of projects in this collection.
        /// If the logger object is already in the collection, does nothing.
        /// </summary>
        private void RegisterLoggerInternal(ILogger logger)
        {
            ErrorUtilities.VerifyThrowArgumentNull(logger, "logger");
            Debug.Assert(Monitor.IsEntered(_locker));
            _loggingService.RegisterLogger(new ReusableLogger(logger));
        }

        /// <summary>
        /// Handler which is called when a project is added to the RootElementCache of this project collection. We then fire an event indicating that a project was added to the collection itself.
        /// </summary>
        private void ProjectRootElementCache_ProjectRootElementAddedHandler(object sender, ProjectRootElementCache.ProjectRootElementCacheAddEntryEventArgs e)
        {
            if (ProjectAdded != null)
            {
                ProjectAdded(this, new ProjectAddedToProjectCollectionEventArgs(e.RootElement));
            }
        }

        /// <summary>
        /// Handler which is called when a project that is part of this collection is dirtied. We then fire an event indicating that a project has been dirtied.
        /// </summary>
        private void ProjectRootElementCache_ProjectRootElementDirtiedHandler(object sender, ProjectXmlChangedEventArgs e)
        {
            OnProjectXmlChanged(e);
        }

        /// <summary>
        /// Handler which is called when a project is dirtied.
        /// </summary>
        private void ProjectRootElementCache_ProjectDirtiedHandler(object sender, ProjectChangedEventArgs e)
        {
            OnProjectChanged(e);
        }

        /// <summary>
        /// Raises the <see cref="ProjectXmlChanged"/> event.
        /// </summary>
        /// <param name="e">The event arguments that indicate ProjectRootElement-specific details.</param>
        private void OnProjectXmlChanged(ProjectXmlChangedEventArgs e)
        {
            var projectXmlChanged = this.ProjectXmlChanged;
            if (projectXmlChanged != null)
            {
                projectXmlChanged(this, e);
            }
        }

        /// <summary>
        /// Raises the <see cref="ProjectChanged"/> event.
        /// </summary>
        /// <param name="e">The event arguments that indicate Project-specific details.</param>
        private void OnProjectChanged(ProjectChangedEventArgs e)
        {
            var projectChanged = this.ProjectChanged;
            if (projectChanged != null)
            {
                projectChanged(this, e);
            }
        }

        /// <summary>
        /// Raises the <see cref="ProjectCollectionChanged"/> event.
        /// </summary>
        /// <param name="e">The event arguments that indicate details on what changed on the collection.</param>
        private void OnProjectCollectionChanged(ProjectCollectionChangedEventArgs e)
        {
            Debug.Assert(!Monitor.IsEntered(_locker), "We should never raise events while holding a private lock.");
            var projectCollectionChanged = this.ProjectCollectionChanged;
            if (projectCollectionChanged != null)
            {
                projectCollectionChanged(this, e);
            }
        }

        /// <summary>
        /// Raises the <see cref="ProjectCollectionChanged"/> event if the args parameter is non-null.
        /// </summary>
        /// <param name="e">The event arguments that indicate details on what changed on the collection.</param>
        private void OnProjectCollectionChangedIfNonNull(ProjectCollectionChangedEventArgs e)
        {
            if (e != null)
            {
                OnProjectCollectionChanged(e);
            }
        }

        /// <summary>
        /// Shutdown the logging service
        /// </summary>
        private void ShutDownLoggingService()
        {
            if (_loggingService != null)
            {
                try
                {
                    ((IBuildComponent)LoggingService).ShutdownComponent();
                }
                catch (LoggerException)
                {
                    throw;
                }
                catch (InternalLoggerException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    // According to Framework Guidelines, Dispose methods should never throw except in dire circumstances.
                    // However if we throw at all, its a bug. Throw InternalErrorException to emphasize that.
                    ErrorUtilities.ThrowInternalError("Throwing from logger shutdown", ex);
                    throw;
                }

                _loggingService = null;
            }
        }

        /// <summary>
        /// Create a new logging service
        /// </summary>
        private void CreateLoggingService(int maxCPUCount, bool onlyLogCriticalEvents)
        {
            _loggingService = Microsoft.Build.BackEnd.Logging.LoggingService.CreateLoggingService(LoggerMode.Synchronous, 0 /*Evaluation can be done as if it was on node "0"*/);
            _loggingService.MaxCPUCount = maxCPUCount;
            _loggingService.OnlyLogCriticalEvents = onlyLogCriticalEvents;
        }

        /// <summary>
        /// Reset the toolsets using the provided toolset reader, used by unit tests
        /// </summary>
        internal void ResetToolsetsForTests(ToolsetConfigurationReader configurationReaderForTestsOnly)
        {
            InitializeToolsetCollection(configReader:configurationReaderForTestsOnly);
        }

        /// <summary>
        /// Reset the toolsets using the provided toolset reader, used by unit tests
        /// <summary>
        internal void ResetToolsetsForTests(ToolsetRegistryReader registryReaderForTestsOnly)
        {
            InitializeToolsetCollection(registryReader:registryReaderForTestsOnly);
        }

        /// <summary>
        /// Populate Toolsets with a dictionary of (toolset version, Toolset) 
        /// using information from the registry and config file, if any.  
        /// </summary>
        private void InitializeToolsetCollection(
                ToolsetRegistryReader registryReader = null,
                ToolsetConfigurationReader configReader = null)
        {
            _toolsets = new Dictionary<string, Toolset>(StringComparer.OrdinalIgnoreCase);

            // We only want our local toolset (as defined in MSBuild.exe.config) when we're operating locally...
            _defaultToolsVersion = ToolsetReader.ReadAllToolsets(_toolsets,
                    registryReader,
                    configReader,
                    EnvironmentProperties, _globalProperties, _toolsetDefinitionLocations);

            _toolsetsVersion++;
        }

        /// <summary>
        /// Event to provide information about what project just got added to the project collection.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1034:NestedTypesShouldNotBeVisible", Justification = "This has been API reviewed")]
        public class ProjectAddedToProjectCollectionEventArgs : EventArgs
        {
            /// <summary>
            /// Root element which was added to the project collection.
            /// </summary>
            private ProjectRootElement _rootElement;

            /// <summary>
            /// The root element which was added to the project collection.
            /// </summary>
            public ProjectAddedToProjectCollectionEventArgs(ProjectRootElement element)
            {
                _rootElement = element;
            }

            /// <summary>
            /// Root element which was added to the project collection.
            /// </summary>
            public ProjectRootElement ProjectRootElement
            {
                get { return _rootElement; }
            }
        }

        /// <summary>
        /// The ReusableLogger wraps a logger and allows it to be used for both design-time and build-time.  It internally swaps
        /// between the design-time and build-time event sources in response to Initialize and Shutdown events.
        /// </summary>
        internal class ReusableLogger : INodeLogger, IEventSource
        {
            /// <summary>
            /// The logger we are wrapping.
            /// </summary>
            private ILogger _originalLogger;

            /// <summary>
            /// The design-time event source
            /// </summary>
            private IEventSource _designTimeEventSource;

            /// <summary>
            /// The build-time event source
            /// </summary>
            private IEventSource _buildTimeEventSource;

            /// <summary>
            /// The Any event handler
            /// </summary>
            private AnyEventHandler _anyEventHandler;

            /// <summary>
            /// The BuildFinished event handler
            /// </summary>
            private BuildFinishedEventHandler _buildFinishedEventHandler;

            /// <summary>
            /// The BuildStarted event handler
            /// </summary>
            private BuildStartedEventHandler _buildStartedEventHandler;

            /// <summary>
            /// The Custom event handler
            /// </summary>
            private CustomBuildEventHandler _customBuildEventHandler;

            /// <summary>
            /// The Error event handler
            /// </summary>
            private BuildErrorEventHandler _buildErrorEventHandler;

            /// <summary>
            /// The Message event handler
            /// </summary>
            private BuildMessageEventHandler _buildMessageEventHandler;

            /// <summary>
            /// The ProjectFinished event handler
            /// </summary>
            private ProjectFinishedEventHandler _projectFinishedEventHandler;

            /// <summary>
            /// The ProjectStarted event handler
            /// </summary>
            private ProjectStartedEventHandler _projectStartedEventHandler;

            /// <summary>
            /// The Status event handler
            /// </summary>
            private BuildStatusEventHandler _buildStatusEventHandler;

            /// <summary>
            /// The TargetFinished event handler
            /// </summary>
            private TargetFinishedEventHandler _targetFinishedEventHandler;

            /// <summary>
            /// The TargetStarted event handler
            /// </summary>
            private TargetStartedEventHandler _targetStartedEventHandler;

            /// <summary>
            /// The TaskFinished event handler
            /// </summary>
            private TaskFinishedEventHandler _taskFinishedEventHandler;

            /// <summary>
            /// The TaskStarted event handler
            /// </summary>
            private TaskStartedEventHandler _taskStartedEventHandler;

            /// <summary>
            /// The Warning event handler
            /// </summary>
            private BuildWarningEventHandler _buildWarningEventHandler;

            /// <summary>
            /// Constructor.
            /// </summary>
            public ReusableLogger(ILogger originalLogger)
            {
                ErrorUtilities.VerifyThrowArgumentNull(originalLogger, "originalLogger");
                _originalLogger = originalLogger;
            }

            #region IEventSource Members

            /// <summary>
            /// The Message logging event
            /// </summary>
            public event BuildMessageEventHandler MessageRaised;

            /// <summary>
            /// The Error logging event
            /// </summary>
            public event BuildErrorEventHandler ErrorRaised;

            /// <summary>
            /// The Warning logging event
            /// </summary>
            public event BuildWarningEventHandler WarningRaised;

            /// <summary>
            /// The BuildStarted logging event
            /// </summary>
            public event BuildStartedEventHandler BuildStarted;

            /// <summary>
            /// The BuildFinished logging event
            /// </summary>
            public event BuildFinishedEventHandler BuildFinished;

            /// <summary>
            /// The ProjectStarted logging event
            /// </summary>
            public event ProjectStartedEventHandler ProjectStarted;

            /// <summary>
            /// The ProjectFinished logging event
            /// </summary>
            public event ProjectFinishedEventHandler ProjectFinished;

            /// <summary>
            /// The TargetStarted logging event
            /// </summary>
            public event TargetStartedEventHandler TargetStarted;

            /// <summary>
            /// The TargetFinished logging event
            /// </summary>
            public event TargetFinishedEventHandler TargetFinished;

            /// <summary>
            /// The TashStarted logging event
            /// </summary>
            public event TaskStartedEventHandler TaskStarted;

            /// <summary>
            /// The TaskFinished logging event
            /// </summary>
            public event TaskFinishedEventHandler TaskFinished;

            /// <summary>
            /// The Custom logging event
            /// </summary>
            public event CustomBuildEventHandler CustomEventRaised;

            /// <summary>
            /// The Status logging event
            /// </summary>
            public event BuildStatusEventHandler StatusEventRaised;

            /// <summary>
            /// The Any logging event
            /// </summary>
            public event AnyEventHandler AnyEventRaised;

            #endregion

            #region ILogger Members

            /// <summary>
            /// The logger verbosity
            /// </summary>
            public LoggerVerbosity Verbosity
            {
                get
                {
                    return _originalLogger.Verbosity;
                }

                set
                {
                    _originalLogger.Verbosity = value;
                }
            }

            /// <summary>
            /// The logger parameters
            /// </summary>
            public string Parameters
            {
                get
                {
                    return _originalLogger.Parameters;
                }

                set
                {
                    _originalLogger.Parameters = value;
                }
            }

            /// <summary>
            /// If we haven't yet been initialized, we register for design time events and initialize the logger we are holding.
            /// If we are in design-time mode
            /// </summary>
            public void Initialize(IEventSource eventSource, int nodeCount)
            {
                if (_designTimeEventSource == null)
                {
                    _designTimeEventSource = eventSource;
                    RegisterForEvents(_designTimeEventSource);

                    if (_originalLogger is INodeLogger)
                    {
                        ((INodeLogger)_originalLogger).Initialize(this, nodeCount);
                    }
                    else
                    {
                        _originalLogger.Initialize(this);
                    }
                }
                else
                {
                    ErrorUtilities.VerifyThrow(_buildTimeEventSource == null, "Already registered for build-time.");
                    _buildTimeEventSource = eventSource;
                    UnregisterForEvents(_designTimeEventSource);
                    RegisterForEvents(_buildTimeEventSource);
                }
            }

            /// <summary>
            /// If we haven't yet been initialized, we register for design time events and initialize the logger we are holding.
            /// If we are in design-time mode
            /// </summary>
            public void Initialize(IEventSource eventSource)
            {
                Initialize(eventSource, 1);
            }

            /// <summary>
            /// If we are in build-time mode, we unregister for build-time events and re-register for design-time events.
            /// If we are in design-time mode, we unregister for design-time events and shut down the logger we are holding.
            /// </summary>
            public void Shutdown()
            {
                if (_buildTimeEventSource != null)
                {
                    UnregisterForEvents(_buildTimeEventSource);
                    RegisterForEvents(_designTimeEventSource);
                    _buildTimeEventSource = null;
                }
                else
                {
                    ErrorUtilities.VerifyThrow(_designTimeEventSource != null, "Already unregistered for design-time.");
                    UnregisterForEvents(_designTimeEventSource);
                    _originalLogger.Shutdown();
                }
            }

            #endregion

            /// <summary>
            /// Registers for all of the events on the specified event source.
            /// </summary>
            private void RegisterForEvents(IEventSource eventSource)
            {
                // Create the handlers.
                _anyEventHandler = new AnyEventHandler(AnyEventRaisedHandler);
                _buildFinishedEventHandler = new BuildFinishedEventHandler(BuildFinishedHandler);
                _buildStartedEventHandler = new BuildStartedEventHandler(BuildStartedHandler);
                _customBuildEventHandler = new CustomBuildEventHandler(CustomEventRaisedHandler);
                _buildErrorEventHandler = new BuildErrorEventHandler(ErrorRaisedHandler);
                _buildMessageEventHandler = new BuildMessageEventHandler(MessageRaisedHandler);
                _projectFinishedEventHandler = new ProjectFinishedEventHandler(ProjectFinishedHandler);
                _projectStartedEventHandler = new ProjectStartedEventHandler(ProjectStartedHandler);
                _buildStatusEventHandler = new BuildStatusEventHandler(StatusEventRaisedHandler);
                _targetFinishedEventHandler = new TargetFinishedEventHandler(TargetFinishedHandler);
                _targetStartedEventHandler = new TargetStartedEventHandler(TargetStartedHandler);
                _taskFinishedEventHandler = new TaskFinishedEventHandler(TaskFinishedHandler);
                _taskStartedEventHandler = new TaskStartedEventHandler(TaskStartedHandler);
                _buildWarningEventHandler = new BuildWarningEventHandler(WarningRaisedHandler);

                // Register for the events.
                eventSource.AnyEventRaised += _anyEventHandler;
                eventSource.BuildFinished += _buildFinishedEventHandler;
                eventSource.BuildStarted += _buildStartedEventHandler;
                eventSource.CustomEventRaised += _customBuildEventHandler;
                eventSource.ErrorRaised += _buildErrorEventHandler;
                eventSource.MessageRaised += _buildMessageEventHandler;
                eventSource.ProjectFinished += _projectFinishedEventHandler;
                eventSource.ProjectStarted += _projectStartedEventHandler;
                eventSource.StatusEventRaised += _buildStatusEventHandler;
                eventSource.TargetFinished += _targetFinishedEventHandler;
                eventSource.TargetStarted += _targetStartedEventHandler;
                eventSource.TaskFinished += _taskFinishedEventHandler;
                eventSource.TaskStarted += _taskStartedEventHandler;
                eventSource.WarningRaised += _buildWarningEventHandler;
            }

            /// <summary>
            /// Unregisters for all events on the specified event source.
            /// </summary>
            private void UnregisterForEvents(IEventSource eventSource)
            {
                // Unregister for the events.
                eventSource.AnyEventRaised -= _anyEventHandler;
                eventSource.BuildFinished -= _buildFinishedEventHandler;
                eventSource.BuildStarted -= _buildStartedEventHandler;
                eventSource.CustomEventRaised -= _customBuildEventHandler;
                eventSource.ErrorRaised -= _buildErrorEventHandler;
                eventSource.MessageRaised -= _buildMessageEventHandler;
                eventSource.ProjectFinished -= _projectFinishedEventHandler;
                eventSource.ProjectStarted -= _projectStartedEventHandler;
                eventSource.StatusEventRaised -= _buildStatusEventHandler;
                eventSource.TargetFinished -= _targetFinishedEventHandler;
                eventSource.TargetStarted -= _targetStartedEventHandler;
                eventSource.TaskFinished -= _taskFinishedEventHandler;
                eventSource.TaskStarted -= _taskStartedEventHandler;
                eventSource.WarningRaised -= _buildWarningEventHandler;

                // Null out the handlers.
                _anyEventHandler = null;
                _buildFinishedEventHandler = null;
                _buildStartedEventHandler = null;
                _customBuildEventHandler = null;
                _buildErrorEventHandler = null;
                _buildMessageEventHandler = null;
                _projectFinishedEventHandler = null;
                _projectStartedEventHandler = null;
                _buildStatusEventHandler = null;
                _targetFinishedEventHandler = null;
                _targetStartedEventHandler = null;
                _taskFinishedEventHandler = null;
                _taskStartedEventHandler = null;
                _buildWarningEventHandler = null;
            }

            /// <summary>
            /// Handler for Warning events.
            /// </summary>
            private void WarningRaisedHandler(object sender, BuildWarningEventArgs e)
            {
                if (WarningRaised != null)
                {
                    WarningRaised(sender, e);
                }
            }

            /// <summary>
            /// Handler for TaskStartedevents.
            /// </summary>
            private void TaskStartedHandler(object sender, TaskStartedEventArgs e)
            {
                if (TaskStarted != null)
                {
                    TaskStarted(sender, e);
                }
            }

            /// <summary>
            /// Handler for TaskFinished events.
            /// </summary>
            private void TaskFinishedHandler(object sender, TaskFinishedEventArgs e)
            {
                if (TaskFinished != null)
                {
                    TaskFinished(sender, e);
                }
            }

            /// <summary>
            /// Handler for TargetStarted events.
            /// </summary>
            private void TargetStartedHandler(object sender, TargetStartedEventArgs e)
            {
                if (TargetStarted != null)
                {
                    TargetStarted(sender, e);
                }
            }

            /// <summary>
            /// Handler for TargetFinished events.
            /// </summary>
            private void TargetFinishedHandler(object sender, TargetFinishedEventArgs e)
            {
                if (TargetFinished != null)
                {
                    TargetFinished(sender, e);
                }
            }

            /// <summary>
            /// Handler for Status events.
            /// </summary>
            private void StatusEventRaisedHandler(object sender, BuildStatusEventArgs e)
            {
                if (StatusEventRaised != null)
                {
                    StatusEventRaised(sender, e);
                }
            }

            /// <summary>
            /// Handler for ProjectStarted events.
            /// </summary>
            private void ProjectStartedHandler(object sender, ProjectStartedEventArgs e)
            {
                if (ProjectStarted != null)
                {
                    ProjectStarted(sender, e);
                }
            }

            /// <summary>
            /// Handler for ProjectFinished events.
            /// </summary>
            private void ProjectFinishedHandler(object sender, ProjectFinishedEventArgs e)
            {
                if (ProjectFinished != null)
                {
                    ProjectFinished(sender, e);
                }
            }

            /// <summary>
            /// Handler for Message events.
            /// </summary>
            private void MessageRaisedHandler(object sender, BuildMessageEventArgs e)
            {
                if (MessageRaised != null)
                {
                    MessageRaised(sender, e);
                }
            }

            /// <summary>
            /// Handler for Error events.
            /// </summary>
            private void ErrorRaisedHandler(object sender, BuildErrorEventArgs e)
            {
                if (ErrorRaised != null)
                {
                    ErrorRaised(sender, e);
                }
            }

            /// <summary>
            /// Handler for Custom events.
            /// </summary>
            private void CustomEventRaisedHandler(object sender, CustomBuildEventArgs e)
            {
                if (CustomEventRaised != null)
                {
                    CustomEventRaised(sender, e);
                }
            }

            /// <summary>
            /// Handler for BuildStarted events.
            /// </summary>
            private void BuildStartedHandler(object sender, BuildStartedEventArgs e)
            {
                if (BuildStarted != null)
                {
                    BuildStarted(sender, e);
                }
            }

            /// <summary>
            /// Handler for BuildFinished events.
            /// </summary>
            private void BuildFinishedHandler(object sender, BuildFinishedEventArgs e)
            {
                if (BuildFinished != null)
                {
                    BuildFinished(sender, e);
                }
            }

            /// <summary>
            /// Handler for Any events.
            /// </summary>
            private void AnyEventRaisedHandler(object sender, BuildEventArgs e)
            {
                if (AnyEventRaised != null)
                {
                    AnyEventRaised(sender, e);
                }
            }
        }

        /// <summary>
        /// Holder for the projects loaded into this collection.
        /// </summary>
        private class LoadedProjectCollection : IEnumerable<Project>
        {
            /// <summary>
            /// The collection of all projects already loaded into this collection.
            /// Key is the full path to the project, value is a list of projects with that path, each
            /// with different global properties and/or tools version.
            /// </summary>
            /// <remarks>
            /// If hosts tend to load lots of projects with the same path, the value will have to be 
            /// changed to a more efficient type of collection.
            ///
            /// Lock on this object. Concurrent load must be thread safe.
            /// Not using ConcurrentDictionary because some of the add/update
            /// semantics would get convoluted.
            /// </remarks>
            private Dictionary<string, List<Project>> _loadedProjects = new Dictionary<string, List<Project>>(StringComparer.OrdinalIgnoreCase);

            /// <summary>
            /// Count of loaded projects
            /// </summary>
            private int _count;

            /// <summary>
            /// Constructor
            /// </summary>
            internal LoadedProjectCollection()
            {
            }

            /// <summary>
            /// Returns the number of projects currently loaded
            /// </summary>
            internal int Count
            {
                get
                {
                    lock (_loadedProjects)
                    {
                        return _count;
                    }
                }
            }

            /// <summary>
            /// Enumerate all the projects
            /// </summary>
            public IEnumerator<Project> GetEnumerator()
            {
                lock (_loadedProjects)
                {
                    var projects = new List<Project>();

                    foreach (List<Project> projectList in _loadedProjects.Values)
                    {
                        foreach (Project project in projectList)
                        {
                            projects.Add(project);
                        }
                    }

                    return projects.GetEnumerator();
                }
            }

            /// <summary>
            /// Enumerate all the projects.
            /// </summary>
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            /// <summary>
            /// Get all projects with the provided path.
            /// Returns an empty list if there are none.
            /// </summary>
            internal IList<Project> GetMatchingProjectsIfAny(string fullPath)
            {
                lock (_loadedProjects)
                {
                    List<Project> candidates;

                    _loadedProjects.TryGetValue(fullPath, out candidates);

                    return (candidates == null) ? (IList<Project>)ReadOnlyEmptyList<Project>.Instance : candidates;
                }
            }

            /// <summary>
            /// Returns the project in the collection matching the path, global properties, and tools version provided.
            /// There can be no more than one match.
            /// If none is found, returns null.
            /// </summary>
            internal Project GetMatchingProjectIfAny(string fullPath, IDictionary<string, string> globalProperties, string toolsVersion)
            {
                lock (_loadedProjects)
                {
                    List<Project> candidates;
                    if (_loadedProjects.TryGetValue(fullPath, out candidates))
                    {
                        foreach (Project candidate in candidates)
                        {
                            if (HasEquivalentGlobalPropertiesAndToolsVersion(candidate, globalProperties, toolsVersion))
                            {
                                return candidate;
                            }
                        }
                    }

                    return null;
                }
            }

            /// <summary>
            /// Adds the provided project to the collection.
            /// If there is already an equivalent project, throws InvalidOperationException.
            /// </summary>
            internal void AddProject(Project project)
            {
                lock (_loadedProjects)
                {
                    List<Project> projectList;
                    if (!_loadedProjects.TryGetValue(project.FullPath, out projectList))
                    {
                        projectList = new List<Project>();
                        _loadedProjects.Add(project.FullPath, projectList);
                    }

                    foreach (Project existing in projectList)
                    {
                        if (HasEquivalentGlobalPropertiesAndToolsVersion(existing, project.GlobalProperties, project.ToolsVersion))
                        {
                            ErrorUtilities.ThrowInvalidOperation("OM_MatchingProjectAlreadyInCollection", existing.FullPath);
                        }
                    }

                    projectList.Add(project);
                    _count++;
                }
            }

            /// <summary>
            /// Removes the provided project from the collection.
            /// If project was not loaded, returns false.
            /// </summary>
            internal bool RemoveProject(Project project)
            {
                return RemoveProject(project.FullPath, project);
            }

            /// <summary>
            /// Removes a project, using the specified full path to use as the key to find it.
            /// This is specified separately in case the project was previously stored under a different path.
            /// </summary>
            internal bool RemoveProject(string projectFullPath, Project project)
            {
                lock (_loadedProjects)
                {
                    List<Project> projectList;
                    if (!_loadedProjects.TryGetValue(projectFullPath, out projectList))
                    {
                        return false;
                    }

                    bool result = projectList.Remove(project);

                    if (result)
                    {
                        _count--;

                        if (projectList.Count == 0)
                        {
                            _loadedProjects.Remove(projectFullPath);
                        }
                    }

                    return result;
                }
            }

            /// <summary>
            /// Removes all projects from the collection.
            /// </summary>
            internal void RemoveAllProjects()
            {
                lock (_loadedProjects)
                {
                    _loadedProjects = new Dictionary<string, List<Project>>(StringComparer.OrdinalIgnoreCase);
                    _count = 0;
                }
            }

            /// <summary>
            /// Returns true if the global properties and tools version provided are equivalent to
            /// those in the provided project, otherwise false.
            /// </summary>
            private bool HasEquivalentGlobalPropertiesAndToolsVersion(Project project, IDictionary<string, string> globalProperties, string toolsVersion)
            {
                if (!String.Equals(project.ToolsVersion, toolsVersion, StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (project.GlobalProperties.Count != globalProperties.Count)
                {
                    return false;
                }

                foreach (KeyValuePair<string, string> leftProperty in project.GlobalProperties)
                {
                    string rightValue;
                    if (!globalProperties.TryGetValue(leftProperty.Key, out rightValue))
                    {
                        return false;
                    }

                    if (!String.Equals(leftProperty.Value, rightValue, StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
