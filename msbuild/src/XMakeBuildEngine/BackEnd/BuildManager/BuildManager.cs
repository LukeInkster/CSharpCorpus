﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Implementation of the Build Manager.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Logging;
using Microsoft.Build.Shared;
using ForwardingLoggerRecord = Microsoft.Build.Logging.ForwardingLoggerRecord;
using LoggerDescription = Microsoft.Build.Logging.LoggerDescription;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// This class is the public entry point for executing builds.
    /// </summary>
    [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Refactoring at the end of Beta1 is not appropriate.")]
    public class BuildManager : INodePacketHandler, IBuildComponentHost, IDisposable
    {
        /// <summary>
        /// The object used for thread-safe synchronization of static members.
        /// </summary>
        private static readonly Object s_staticSyncLock = new Object();

        /// <summary>
        /// The object used for thread-safe synchronization of BuildManager shared data and the Scheduler.
        /// </summary>
        private readonly Object _syncLock = new Object();

        /// <summary>
        /// The singleton instance for the BuildManager.
        /// </summary>
        static private BuildManager s_singletonInstance;

        /// <summary>
        /// The next build id;
        /// </summary>
        static private int s_nextBuildId;

        /// <summary>
        /// The next build request configuration ID to use.
        /// These must be unique across build managers, as they
        /// are used as part of cache file names, for example.
        /// </summary>
        private static int s_nextBuildRequestConfigurationId;

        /// <summary>
        /// The cache for build request configurations.
        /// </summary>
        private IConfigCache _configCache;

        /// <summary>
        /// The cache for build results.
        /// </summary>
        private IResultsCache _resultsCache;

        /// <summary>
        /// The object responsible for creating and managing nodes.
        /// </summary>
        private INodeManager _nodeManager;

        /// <summary>
        /// The object responsible for creating and managing task host nodes.
        /// </summary>
        private INodeManager _taskHostNodeManager;

        /// <summary>
        /// The object which determines which projects to build, and where.
        /// </summary>
        private IScheduler _scheduler;

        /// <summary>
        /// The node configuration to use for spawning new nodes.
        /// </summary>
        private NodeConfiguration _nodeConfiguration;

        /// <summary>
        /// Any exception which occurs on a logging thread will go here.
        /// </summary>
        private Exception _threadException;

        /// <summary>
        /// Set of active nodes in the system.
        /// </summary>
        private HashSet<NGen<int>> _activeNodes;

        /// <summary>
        /// Event signalled when all nodes have shutdown.
        /// </summary>
        private AutoResetEvent _noNodesActiveEvent;

        /// <summary>
        /// Mapping of nodes to the configurations they know about.
        /// </summary>
        private Dictionary<NGen<int>, HashSet<NGen<int>>> _nodeIdToKnownConfigurations;

        /// <summary>
        /// Flag indicating if we are currently shutting down.  When set, we stop processing packets other than NodeShutdown.
        /// </summary>
        private bool _shuttingDown = false;

        /// <summary>
        /// The current state of the BuildManager.
        /// </summary>
        private BuildManagerState _buildManagerState;

        /// <summary>
        /// The name given to this BuildManager as the component host.
        /// </summary>
        private string _hostName;

        /// <summary>
        /// The parameters with which the build was started.
        /// </summary>
        private BuildParameters _buildParameters;

        /// <summary>
        /// The current pending and active submissions.
        /// </summary>
        /// <remarks>
        /// { submissionId, BuildSubmission }
        /// </remarks>
        private Dictionary<int, BuildSubmission> _buildSubmissions;

        /// <summary>
        /// Event signalled when all build submissions are complete.
        /// </summary>
        private AutoResetEvent _noActiveSubmissionsEvent;

        /// <summary>
        /// The overall success of the build.
        /// </summary>
        private bool _overallBuildSuccess;

        /// <summary>
        /// The next build submission id.
        /// </summary>
        private int _nextBuildSubmissionId;

        /// <summary>
        /// Mapping of unnamed project instances to the file names assigned to them.
        /// </summary>
        private Dictionary<ProjectInstance, string> _unnamedProjectInstanceToNames;

        /// <summary>
        /// The next ID to assign to a project which has no name.
        /// </summary>
        private int _nextUnnamedProjectId;

        /// <summary>
        /// The build component factories.
        /// </summary>
        private BuildComponentFactoryCollection _componentFactories;

        /// <summary>
        /// Mapping of submission IDs to their first project started events.
        /// </summary>
        private Dictionary<int, BuildEventArgs> _projectStartedEvents;

        /// <summary>
        /// Whether a cache has been provided by a project instance, meaning
        /// we've acquired at least one build submission that included a project instance.
        /// Once that has happened, we use the provided one, rather than our default.
        /// </summary>
        private bool _acquiredProjectRootElementCacheFromProjectInstance;

        /// <summary>
        /// The project started event handler
        /// </summary>
        private ProjectStartedEventHandler _projectStartedEventHandler;

        /// <summary>
        /// The project finished event handler
        /// </summary>
        private ProjectFinishedEventHandler _projectFinishedEventHandler;

        /// <summary>
        /// The logging exception event handler
        /// </summary>
        private LoggingExceptionDelegate _loggingThreadExceptionEventHandler;

        /// <summary>
        /// Legacy threading semantic data associated with this build manager.
        /// </summary>
        private LegacyThreadingData _legacyThreadingData;

        /// <summary>
        /// The worker queue.
        /// </summary>
        private ActionBlock<Action> _workQueue;

        /// <summary>
        /// Flag indicating we have disposed. 
        /// </summary>
        private bool _disposed = false;

        /// <summary>
        /// Creates a new unnamed build manager.
        /// Normally there is only one build manager in a process, and it is the default build manager.
        /// Access it with <see cref="BuildManager.DefaultBuildManager"/>
        /// </summary>
        public BuildManager()
            : this("Unnamed")
        {
        }

        /// <summary>
        /// Creates a new build manager with an arbitrary distinct name.
        /// Normally there is only one build manager in a process, and it is the default build manager.
        /// Access it with <see cref="BuildManager.DefaultBuildManager"/>
        /// </summary>
        public BuildManager(string hostName)
        {
            ErrorUtilities.VerifyThrowArgumentNull(hostName, "hostName");
            _hostName = hostName;
            _buildManagerState = BuildManagerState.Idle;
            _buildSubmissions = new Dictionary<int, BuildSubmission>();
            _noActiveSubmissionsEvent = new AutoResetEvent(true);
            _activeNodes = new HashSet<NGen<int>>();
            _noNodesActiveEvent = new AutoResetEvent(true);
            _nodeIdToKnownConfigurations = new Dictionary<NGen<int>, HashSet<NGen<int>>>();
            _unnamedProjectInstanceToNames = new Dictionary<ProjectInstance, string>();
            _nextUnnamedProjectId = 1;
            _componentFactories = new BuildComponentFactoryCollection(this);
            _componentFactories.RegisterDefaultFactories();
            _projectStartedEvents = new Dictionary<int, BuildEventArgs>();

            _projectStartedEventHandler = new ProjectStartedEventHandler(OnProjectStarted);
            _projectFinishedEventHandler = new ProjectFinishedEventHandler(OnProjectFinished);
            _loggingThreadExceptionEventHandler = new LoggingExceptionDelegate(OnThreadException);
            _legacyThreadingData = new LegacyThreadingData();
        }

        /// <summary>
        /// Finalizer
        /// </summary>
        ~BuildManager()
        {
            Dispose(false /* disposing */);
        }

        /// <summary>
        /// Enumeration describing the current state of the build manager.
        /// </summary>
        private enum BuildManagerState
        {
            /// <summary>
            /// This is the default state.  <see cref="BuildManager.BeginBuild"/> may be called in this state.  All other methods raise InvalidOperationException
            /// </summary>
            Idle,

            /// <summary>
            /// This is the state the BuildManager is in after <see cref="BuildManager.BeginBuild"/> has been called but before <see cref="BuildManager.EndBuild"/> has been called.
            /// <see cref="BuildManager.PendBuildRequest"/>, <see cref="BuildManager.BuildRequest"/> and <see cref="BuildManager.EndBuild"/> may be called in this state.
            /// </summary>
            Building,

            /// <summary>
            /// This is the state the BuildManager is in after <see cref="BuildManager.EndBuild"/> has been called but before all existing submissions have completed.
            /// </summary>
            WaitingForBuildToComplete
        }

        /// <summary>
        /// Gets the singleton instance of the Build Manager.
        /// </summary>
        public static BuildManager DefaultBuildManager
        {
            get
            {
                if (s_singletonInstance == null)
                {
                    lock (s_staticSyncLock)
                    {
                        if (s_singletonInstance == null)
                        {
                            s_singletonInstance = new BuildManager("Default");
                        }
                    }
                }

                return s_singletonInstance;
            }
        }

        /// <summary>
        /// Retrieves the logging service associated with a particular build
        /// </summary>
        /// <returns>The logging service.</returns>
        ILoggingService IBuildComponentHost.LoggingService
        {
            get
            {
                return _componentFactories.GetComponent(BuildComponentType.LoggingService) as ILoggingService;
            }
        }

        /// <summary>
        /// Retrieves the name of the component host.
        /// </summary>
        string IBuildComponentHost.Name
        {
            get
            {
                return _hostName;
            }
        }

        /// <summary>
        /// Retrieves the build parameters associated with this build.
        /// </summary>
        /// <returns>The build parameters.</returns>
        BuildParameters IBuildComponentHost.BuildParameters
        {
            get
            {
                return _buildParameters;
            }
        }

        /// <summary>
        /// Retrieves the LegacyThreadingData associated with a particular build manager
        /// </summary>
        LegacyThreadingData IBuildComponentHost.LegacyThreadingData
        {
            get
            {
                return _legacyThreadingData;
            }
        }

        /// <summary>
        /// Prepares the BuildManager to receive build requests.
        /// </summary>
        /// <param name="parameters">The build parameters.  May be null.</param>
        /// <exception cref="InvalidOperationException">Thrown if a build is already in progress.</exception>
        public void BeginBuild(BuildParameters parameters)
        {
            lock (_syncLock)
            {
                // Check for build in progress.
                RequireState(BuildManagerState.Idle, "BuildInProgress");

                if (BuildParameters.DumpOpportunisticInternStats)
                {
                    OpportunisticIntern.EnableStatisticsGathering();
                }

                _overallBuildSuccess = true;

                // Clone off the build parameters.
                if (parameters != null)
                {
                    _buildParameters = parameters.Clone();
                }
                else
                {
                    _buildParameters = new BuildParameters();
                }

                // Initialize additional build parameters.
                _buildParameters.BuildId = GetNextBuildId();

                // Initialize components.
                _nodeManager = ((IBuildComponentHost)this).GetComponent(BuildComponentType.NodeManager) as INodeManager;
                _taskHostNodeManager = ((IBuildComponentHost)this).GetComponent(BuildComponentType.TaskHostNodeManager) as INodeManager;
                _scheduler = ((IBuildComponentHost)this).GetComponent(BuildComponentType.Scheduler) as IScheduler;
                _configCache = ((IBuildComponentHost)this).GetComponent(BuildComponentType.ConfigCache) as IConfigCache;
                _resultsCache = ((IBuildComponentHost)this).GetComponent(BuildComponentType.ResultsCache) as IResultsCache;

                _nodeManager.RegisterPacketHandler(NodePacketType.BuildRequestBlocker, BuildRequestBlocker.FactoryForDeserialization, this);
                _nodeManager.RegisterPacketHandler(NodePacketType.BuildRequestConfiguration, BuildRequestConfiguration.FactoryForDeserialization, this);
                _nodeManager.RegisterPacketHandler(NodePacketType.BuildRequestConfigurationResponse, BuildRequestConfigurationResponse.FactoryForDeserialization, this);
                _nodeManager.RegisterPacketHandler(NodePacketType.BuildResult, BuildResult.FactoryForDeserialization, this);
                _nodeManager.RegisterPacketHandler(NodePacketType.NodeShutdown, NodeShutdown.FactoryForDeserialization, this);

                if (_buildParameters.ResetCaches || _configCache.IsConfigCacheSizeLargerThanThreshold())
                {
                    ResetCaches();
                }
                else
                {
                    List<int> configurationsCleared = _configCache.ClearNonExplicitlyLoadedConfigurations();

                    if (configurationsCleared != null)
                    {
                        foreach (int configurationId in configurationsCleared)
                        {
                            _resultsCache.ClearResultsForConfiguration(configurationId);
                        }
                    }

                    foreach (var config in _configCache)
                    {
                        config.ResultsNodeId = Scheduler.InvalidNodeId;
                    }

                    _buildParameters.ProjectRootElementCache.DiscardImplicitReferences();
                }

                // Set up the logging service.
                ILoggingService loggingService = CreateLoggingService(_buildParameters.Loggers, _buildParameters.ForwardingLoggers);

                _nodeManager.RegisterPacketHandler(NodePacketType.LogMessage, LogMessagePacket.FactoryForDeserialization, loggingService as INodePacketHandler);
                try
                {
                    loggingService.LogBuildStarted();
                }
                catch (Exception)
                {
                    ShutdownLoggingService(loggingService);
                    throw;
                }

                if (_threadException != null)
                {
                    ShutdownLoggingService(loggingService);

                    // Unfortunately this will reset the callstack
                    throw _threadException;
                }

                if (_workQueue == null)
                {
                    _workQueue = new ActionBlock<Action>(action => ProcessWorkQueue(action));
                }

                _buildManagerState = BuildManagerState.Building;

                _noActiveSubmissionsEvent.Set();
                _noNodesActiveEvent.Set();
            }
        }

        /// <summary>
        /// Cancels all outstanding submissions asynchronously.
        /// </summary>
        public void CancelAllSubmissions()
        {
            CultureInfo parentThreadCulture = _buildParameters != null ? _buildParameters.Culture : Thread.CurrentThread.CurrentCulture;
            CultureInfo parentThreadUICulture = _buildParameters != null ? _buildParameters.UICulture : Thread.CurrentThread.CurrentUICulture;

            WaitCallback callback = new WaitCallback(
            delegate (object state)
            {
                lock (_syncLock)
                {
                    if (_shuttingDown)
                    {
                        return;
                    }

                    // If we are Idle, obviously there is nothing to cancel.  If we are waiting for the build to end, then presumably all requests have already completed
                    // and there is nothing left to cancel.  Putting this here eliminates the possibility of us racing with EndBuild to access the nodeManager before
                    // EndBuild sets it to null.
                    if (_buildManagerState != BuildManagerState.Building)
                    {
                        return;
                    }

                    _overallBuildSuccess = false;

                    foreach (BuildSubmission submission in _buildSubmissions.Values)
                    {
                        if (submission.BuildRequest != null)
                        {
                            BuildResult result = new BuildResult(submission.BuildRequest, new BuildAbortedException());
                            _resultsCache.AddResult(result);
                            submission.CompleteResults(result);
                        }
                    }

                    ShutdownConnectedNodesAsync(true /* abort */);
                    CheckForActiveNodesAndCleanUpSubmissions();
                }
            });

            ThreadPoolExtensions.QueueThreadPoolWorkItemWithCulture(callback, parentThreadCulture, parentThreadUICulture);
        }

        /// <summary>
        /// Clears out all of the cached information.
        /// </summary>
        public void ResetCaches()
        {
            lock (_syncLock)
            {
                ErrorIfState(BuildManagerState.WaitingForBuildToComplete, "WaitingForEndOfBuild");
                ErrorIfState(BuildManagerState.Building, "BuildInProgress");

                _configCache = ((IBuildComponentHost)this).GetComponent(BuildComponentType.ConfigCache) as IConfigCache;
                _resultsCache = ((IBuildComponentHost)this).GetComponent(BuildComponentType.ResultsCache) as IResultsCache;
                _resultsCache.ClearResults();

                // This call clears out the directory.
                _configCache.ClearConfigurations();

                if (_buildParameters != null)
                {
                    _buildParameters.ProjectRootElementCache.DiscardImplicitReferences();
                }
            }
        }

        /// <summary>
        /// This methods requests the BuildManager to find a matching ProjectInstance in its cache of previously-built projects.
        /// If none exist, a new instance will be created from the specified project.
        /// </summary>
        /// <param name="project">The Project for which an instance should be retrieved.</param>
        /// <returns>The instance.</returns>
        public ProjectInstance GetProjectInstanceForBuild(Project project)
        {
            lock (_syncLock)
            {
                _configCache = ((IBuildComponentHost)this).GetComponent(BuildComponentType.ConfigCache) as IConfigCache;
                BuildRequestConfiguration configuration = _configCache.GetMatchingConfiguration(
                    new ConfigurationMetadata(project),
                    (config, loadProject) => CreateConfiguration(project, config),
                    loadProject: true);
                ErrorUtilities.VerifyThrow(configuration.Project != null, "Configuration should have been loaded.");
                return configuration.Project;
            }
        }

        /// <summary>
        /// Submits a build request to the current build but does not start it immediately.  Allows the user to
        /// perform asynchronous execution or access the submission ID prior to executing the request.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if StartBuild has not been called or if EndBuild has been called.</exception>
        public BuildSubmission PendBuildRequest(BuildRequestData requestData)
        {
            lock (_syncLock)
            {
                ErrorUtilities.VerifyThrowArgumentNull(requestData, "requestData");
                ErrorIfState(BuildManagerState.WaitingForBuildToComplete, "WaitingForEndOfBuild");
                ErrorIfState(BuildManagerState.Idle, "NoBuildInProgress");
                VerifyStateInternal(BuildManagerState.Building);

                BuildSubmission newSubmission = new BuildSubmission(this, GetNextSubmissionId(), requestData, _buildParameters.LegacyThreadingSemantics);
                _buildSubmissions.Add(newSubmission.SubmissionId, newSubmission);
                _noActiveSubmissionsEvent.Reset();
                return newSubmission;
            }
        }

        /// <summary>
        /// Convenience method. Submits a build request and blocks until the results are available.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if StartBuild has not been called or if EndBuild has been called.</exception>
        public BuildResult BuildRequest(BuildRequestData requestData)
        {
            BuildSubmission submission = PendBuildRequest(requestData);
            return submission.Execute();
        }

        /// <summary>
        /// Signals that no more build requests are expected (or allowed) and the BuildManager may clean up.
        /// </summary>
        /// <remarks>
        /// This call blocks until all currently pending requests are complete.
        /// </remarks>
        /// <exception cref="InvalidOperationException">Thrown if there is no build in progress.</exception>
        public void EndBuild()
        {
            lock (_syncLock)
            {
                ErrorIfState(BuildManagerState.WaitingForBuildToComplete, "WaitingForEndOfBuild");
                ErrorIfState(BuildManagerState.Idle, "NoBuildInProgress");
                VerifyStateInternal(BuildManagerState.Building);

                // If there are any submissions which never started, remove them now.
                List<BuildSubmission> submissionsToCheck = new List<BuildSubmission>(_buildSubmissions.Values);
                foreach (BuildSubmission submission in submissionsToCheck)
                {
                    CheckSubmissionCompletenessAndRemove(submission);
                }

                _buildManagerState = BuildManagerState.WaitingForBuildToComplete;
            }

            ILoggingService loggingService = ((IBuildComponentHost)this).LoggingService;

            try
            {
                _noActiveSubmissionsEvent.WaitOne();
                ShutdownConnectedNodesAsync(false /* normal termination */);
                _noNodesActiveEvent.WaitOne();

                // Wait for all of the actions in the work queue to drain.  Wait() could throw here if there was an unhandled exception 
                // in the work queue, but the top level exception handler there should catch everything and have forwarded it to the 
                // OnThreadException method in this class already.
                _workQueue.Complete();

                ErrorUtilities.VerifyThrow(_buildSubmissions.Count == 0, "All submissions not yet complete.");
                ErrorUtilities.VerifyThrow(_activeNodes.Count == 0, "All nodes not yet shut down.");

                if (loggingService != null)
                {
                    loggingService.LogBuildFinished(_overallBuildSuccess);
                }

#if DEBUG
                if (_projectStartedEvents.Count != 0)
                {
                    bool allMismatchedProjectStartedEventsDueToLoggerErrors = true;

                    foreach (var projectStartedEvent in _projectStartedEvents)
                    {
                        BuildResult result = _resultsCache.GetResultsForConfiguration(projectStartedEvent.Value.BuildEventContext.ProjectInstanceId);

                        // It's valid to have a mismatched project started event IFF that particular 
                        // project had some sort of unhandled exception.  If there is no result, we 
                        // can't tell for sure one way or the other, so err on the side of throwing 
                        // the assert, but if there is a result, make sure that it actually has an 
                        // exception attached. 
                        if (result == null || result.Exception == null)
                        {
                            allMismatchedProjectStartedEventsDueToLoggerErrors = false;
                            break;
                        }
                    }

                    Debug.Assert(allMismatchedProjectStartedEventsDueToLoggerErrors, "There was a mismatched project started event not caused by an exception result");
                }
#endif 
            }
            finally
            {
                try
                {
                    ShutdownLoggingService(loggingService);
                }
                finally
                {
                    if (_buildParameters.LegacyThreadingSemantics == true)
                    {
                        _legacyThreadingData.MainThreadSubmissionId = -1;
                    }

                    Reset();
                    _buildManagerState = BuildManagerState.Idle;

                    if (_threadException != null)
                    {
                        // Unfortunately this will reset the callstack
                        throw _threadException;
                    }

                    if (BuildParameters.DumpOpportunisticInternStats)
                    {
                        OpportunisticIntern.ReportStatistics();
                    }
                }
            }
        }

        /// <summary>
        /// Convenience method.  Submits a lone build request and blocks until results are available.
        /// </summary>
        /// <exception cref="InvalidOperationException">Thrown if a build is already in progress.</exception>
        public BuildResult Build(BuildParameters parameters, BuildRequestData requestData)
        {
            BuildResult result;
            BeginBuild(parameters);
            try
            {
                result = BuildRequest(requestData);
                if (result.Exception == null && _threadException != null)
                {
                    result.Exception = _threadException;
                    _threadException = null;
                }
            }
            finally
            {
                EndBuild();
            }

            return result;
        }

        /// <summary>
        /// Shuts down all idle MSBuild nodes on the machine
        /// </summary>
        public void ShutdownAllNodes()
        {
            if (null == _nodeManager)
            {
                _nodeManager = ((IBuildComponentHost)this).GetComponent(BuildComponentType.NodeManager) as INodeManager;
            }

            _nodeManager.ShutdownAllNodes();
        }

        /// <summary>
        /// Dispose of the build manager. 
        /// </summary>
        public void Dispose()
        {
            Dispose(true /* disposing */);
            GC.SuppressFinalize(this);
        }

        #region INodePacketHandler Members

        /// <summary>
        /// This method is invoked by the NodePacketRouter when a packet is received and is intended for
        /// this recipient.
        /// </summary>
        /// <param name="node">The node from which the packet was received.</param>
        /// <param name="packet">The packet.</param>
        void INodePacketHandler.PacketReceived(int node, INodePacket packet)
        {
            _workQueue.Post(() => this.ProcessPacket(node, packet));
        }

        #endregion

        #region IBuildComponentHost Members

        /// <summary>
        /// Registers a factory which will be used to create the necessary components of the build
        /// system.
        /// </summary>
        /// <param name="componentType">The type which is created by this factory.</param>
        /// <param name="factory">The factory to be registered.</param>
        /// <remarks>
        /// It is not necessary to register any factories.  If no factory is registered for a specific kind
        /// of object, the system will use the default factory.
        /// </remarks>
        void IBuildComponentHost.RegisterFactory(BuildComponentType componentType, BuildComponentFactoryDelegate factory)
        {
            _componentFactories.ReplaceFactory(componentType, factory);
        }

        /// <summary>
        /// Gets an instance of the specified component type from the host.
        /// </summary>
        /// <param name="type">The component type to be retrieved</param>
        /// <returns>The component</returns>
        IBuildComponent IBuildComponentHost.GetComponent(BuildComponentType type)
        {
            return _componentFactories.GetComponent(type);
        }

        #endregion

        /// <summary>
        /// This method adds the request in the specified submission to the set of requests being handled by the scheduler.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1031:DoNotCatchGeneralExceptionTypes", Justification = "Standard ExpectedException pattern used")]
        [SuppressMessage("Microsoft.Maintainability", "CA1506:AvoidExcessiveClassCoupling", Justification = "Complex class might need refactoring to separate scheduling elements from submission elements.")]
        internal void ExecuteSubmission(BuildSubmission submission, bool allowMainThreadBuild)
        {
            ErrorUtilities.VerifyThrowArgumentNull(submission, "submission");
            ErrorUtilities.VerifyThrow(!submission.IsCompleted, "Submission already complete.");

            ProjectInstance projectInstance = submission.BuildRequestData.ProjectInstance;
            if (projectInstance != null)
            {
                if (_acquiredProjectRootElementCacheFromProjectInstance)
                {
                    ErrorUtilities.VerifyThrowArgument(_buildParameters.ProjectRootElementCache == projectInstance.ProjectRootElementCache, "OM_BuildSubmissionsMultipleProjectCollections");
                }
                else
                {
                    _buildParameters.ProjectRootElementCache = projectInstance.ProjectRootElementCache;
                    _acquiredProjectRootElementCacheFromProjectInstance = true;
                }
            }
            else if (_buildParameters.ProjectRootElementCache == null)
            {
                // Create our own cache; if we subsequently get a build submission with a project instance attached,
                // we'll dump our cache and use that one.
                _buildParameters.ProjectRootElementCache = new ProjectRootElementCache(false /* do not automatically reload from disk */);
            }

            VerifyStateInternal(BuildManagerState.Building);

            try
            {
                // If we have an unnamed project, assign it a temporary name.
                if (String.IsNullOrEmpty(submission.BuildRequestData.ProjectFullPath))
                {
                    ErrorUtilities.VerifyThrow(submission.BuildRequestData.ProjectInstance != null, "Unexpected null path for a submission with no ProjectInstance.");

                    string tempName;

                    // If we have already named this instance when it was submitted previously during this build, use the same
                    // name so that we get the same configuration (and thus don't cause it to rebuild.)
                    if (!_unnamedProjectInstanceToNames.TryGetValue(submission.BuildRequestData.ProjectInstance, out tempName))
                    {
                        tempName = "Unnamed_" + _nextUnnamedProjectId++;
                        _unnamedProjectInstanceToNames[submission.BuildRequestData.ProjectInstance] = tempName;
                    }

                    submission.BuildRequestData.ProjectFullPath = Path.Combine(submission.BuildRequestData.ProjectInstance.GetProperty(ReservedPropertyNames.projectDirectory).EvaluatedValue, tempName);
                }

                // Create/Retrieve a configuration for each request
                BuildRequestConfiguration buildRequestConfiguration = new BuildRequestConfiguration(submission.BuildRequestData, _buildParameters.DefaultToolsVersion, _buildParameters.GetToolset);
                BuildRequestConfiguration matchingConfiguration = _configCache.GetMatchingConfiguration(buildRequestConfiguration);
                BuildRequestConfiguration newConfiguration = ResolveConfiguration(buildRequestConfiguration, matchingConfiguration, (submission.BuildRequestData.Flags & BuildRequestDataFlags.ReplaceExistingProjectInstance) == BuildRequestDataFlags.ReplaceExistingProjectInstance);

                newConfiguration.ExplicitlyLoaded = true;

                // Now create the build request
                submission.BuildRequest = new BuildRequest(
                    submission.SubmissionId,
                    Microsoft.Build.BackEnd.BuildRequest.InvalidNodeRequestId,
                    newConfiguration.ConfigurationId,
                    submission.BuildRequestData.TargetNames,
                    submission.BuildRequestData.HostServices,
                    BuildEventContext.Invalid,
                    null,
                    submission.BuildRequestData.Flags);

                if (_shuttingDown)
                {
                    // We were already canceled!
                    BuildResult result = new BuildResult(submission.BuildRequest, new BuildAbortedException());
                    submission.CompleteResults(result);
                    submission.CompleteLogging(true);
                    CheckSubmissionCompletenessAndRemove(submission);
                    return;
                }

                // Submit the build request.
                BuildRequestBlocker blocker = new BuildRequestBlocker(-1, new string[0], new BuildRequest[] { submission.BuildRequest });
                _workQueue.Post(() =>
                {
                    try
                    {
                        IssueRequestToScheduler(submission, allowMainThreadBuild, blocker);
                    }
                    catch (BuildAbortedException bae)
                    {
                        // We were canceled before we got issued by the work queue.
                        BuildResult result = new BuildResult(submission.BuildRequest, bae);
                        submission.CompleteResults(result);
                        submission.CompleteLogging(true);
                        CheckSubmissionCompletenessAndRemove(submission);
                    }
                    catch (Exception ex)
                    {
                        if (ExceptionHandling.IsCriticalException(ex))
                        {
                            throw;
                        }

                        HandleExecuteSubmissionException(submission, ex);
                    }
                });
            }
            catch (Exception ex)
            {
                if (ExceptionHandling.IsCriticalException(ex))
                {
                    throw;
                }

                HandleExecuteSubmissionException(submission, ex);
                throw;
            }
        }

        /// <summary>
        /// Creates the traversal and metaproject instances necessary to represent the solution and populates new configurations with them.
        /// </summary>
        internal void LoadSolutionIntoConfiguration(BuildRequestConfiguration config, BuildEventContext buildEventContext)
        {
            if (config.IsLoaded)
            {
                // We've already processed it, nothing to do.
                return;
            }

            ErrorUtilities.VerifyThrow(FileUtilities.IsSolutionFilename(config.ProjectFullPath), "{0} is not a solution", config.ProjectFullPath);
            ProjectInstance[] instances = ProjectInstance.LoadSolutionForBuild(config.ProjectFullPath, config.Properties, config.ExplicitToolsVersionSpecified ? config.ToolsVersion : null, _buildParameters, ((IBuildComponentHost)this).LoggingService, buildEventContext, false /* loaded by solution parser*/);

            // The first instance is the traversal project, which goes into this configuration
            config.Project = instances[0];

            // The remaining instances are the metaprojects which describe the dependencies for each project as well as how to invoke the project itself.
            for (int i = 1; i < instances.Length; i++)
            {
                // Create new configurations for each of these if they don't already exist.  That could happen if there are multiple
                // solutions in this build which refer to the same project, in which case we want them to refer to the same
                // metaproject as well.
                BuildRequestConfiguration newConfig = new BuildRequestConfiguration(GetNewConfigurationId(), instances[i]);
                newConfig.ExplicitlyLoaded = config.ExplicitlyLoaded;
                if (_configCache.GetMatchingConfiguration(newConfig) == null)
                {
                    _configCache.AddConfiguration(newConfig);
                }
            }
        }

        /// <summary>
        /// Gets the next build id.
        /// </summary>
        private static int GetNextBuildId()
        {
            return Interlocked.Increment(ref s_nextBuildId);
        }

        /// <summary>
        /// Creates and optionally populates a new configuration.
        /// </summary>
        private BuildRequestConfiguration CreateConfiguration(Project project, BuildRequestConfiguration existingConfiguration)
        {
            ProjectInstance newInstance = project.CreateProjectInstance();

            if (existingConfiguration == null)
            {
                existingConfiguration = new BuildRequestConfiguration(GetNewConfigurationId(), new BuildRequestData(newInstance, new string[] { }), null /* use the instance's tools version */, null /* shouldn't need to get toolsets because ProjectInstance's ToolsVersion overrides */);
            }
            else
            {
                existingConfiguration.Project = newInstance;
            }

            return existingConfiguration;
        }

        /// <summary>
        /// Processes the next action in the work queue.
        /// </summary>
        /// <param name="action">The action to be processed.</param>
        private void ProcessWorkQueue(Action action)
        {
            try
            {
                var oldCulture = Thread.CurrentThread.CurrentCulture;
                var oldUICulture = Thread.CurrentThread.CurrentUICulture;

                try
                {
                    if (Thread.CurrentThread.CurrentCulture != _buildParameters.Culture)
                    {
                        Thread.CurrentThread.CurrentCulture = _buildParameters.Culture;
                    }

                    if (Thread.CurrentThread.CurrentUICulture != _buildParameters.UICulture)
                    {
                        Thread.CurrentThread.CurrentUICulture = _buildParameters.UICulture;
                    }

                    action();
                }
                catch (Exception ex)
                {
                    // These need to go to the main thread exception handler.  We can't rethrow here because that will just silently stop the 
                    // action block.  Instead, send them over to the main handler for the BuildManager.
                    this.OnThreadException(ex);
                }
                finally
                {
                    // Set the culture back to the original one so that if something else reuses this thread then it will not have a culture which it was not expecting.
                    if (Thread.CurrentThread.CurrentCulture != oldCulture)
                    {
                        Thread.CurrentThread.CurrentCulture = oldCulture;
                    }

                    if (Thread.CurrentThread.CurrentUICulture != oldUICulture)
                    {
                        Thread.CurrentThread.CurrentUICulture = oldUICulture;
                    }
                }
            }
            catch (Exception e)
            {
                // On the off chance we get an exception from our exception handler (oh, the irony!), we want to know about it (and still not kill this block
                // which could lead to a somewhat mysterious hang.)
                ExceptionHandling.DumpExceptionToFile(e);
            }
        }

        /// <summary>
        /// Processes a packet
        /// </summary>
        private void ProcessPacket(int node, INodePacket packet)
        {
            lock (_syncLock)
            {
                if (_shuttingDown && packet.Type != NodePacketType.NodeShutdown)
                {
                    // Console.WriteLine("Discarding packet {0} from node {1} because we are shutting down.", packet.Type, node);
                    return;
                }

                switch (packet.Type)
                {
                    case NodePacketType.BuildRequestBlocker:
                        BuildRequestBlocker blocker = ExpectPacketType<BuildRequestBlocker>(packet, NodePacketType.BuildRequestBlocker);
                        HandleNewRequest(node, blocker);
                        break;

                    case NodePacketType.BuildRequestConfiguration:
                        BuildRequestConfiguration requestConfiguration = ExpectPacketType<BuildRequestConfiguration>(packet, NodePacketType.BuildRequestConfiguration);
                        HandleConfigurationRequest(node, requestConfiguration);
                        break;

                    case NodePacketType.BuildResult:
                        BuildResult result = ExpectPacketType<BuildResult>(packet, NodePacketType.BuildResult);
                        HandleResult(node, result);
                        break;

                    case NodePacketType.NodeShutdown:
                        // Remove the node from the list of active nodes.  When they are all done, we have shut down fully                                        
                        NodeShutdown shutdownPacket = ExpectPacketType<NodeShutdown>(packet, NodePacketType.NodeShutdown);
                        HandleNodeShutdown(node, shutdownPacket);
                        break;

                    default:
                        ErrorUtilities.ThrowInternalError("Unexpected packet received by BuildManager: {0}", packet.Type);
                        break;
                }
            }
        }

        /// <summary>
        /// Deals with exceptions that may be thrown as a result of ExecuteSubmission.
        /// </summary>
        private void HandleExecuteSubmissionException(BuildSubmission submission, Exception ex)
        {
            InvalidProjectFileException projectException = ex as InvalidProjectFileException;

            if (projectException != null)
            {
                if (projectException.HasBeenLogged != true)
                {
                    BuildEventContext buildEventContext = new BuildEventContext(submission.SubmissionId, 1, BuildEventContext.InvalidProjectInstanceId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidTaskId);
                    ((IBuildComponentHost)this).LoggingService.LogInvalidProjectFileError(buildEventContext, projectException);
                    projectException.HasBeenLogged = true;
                }
            }

            // BuildRequest may be null if the submission fails early on.
            if (submission.BuildRequest != null)
            {
                BuildResult result = new BuildResult(submission.BuildRequest, ex);
                submission.CompleteResults(result);
                submission.CompleteLogging(true);
            }

            _overallBuildSuccess = false;
            CheckSubmissionCompletenessAndRemove(submission);
        }

        /// <summary>
        /// Sends the request to the scheduler with optional legacy threading semantics behavior.
        /// </summary>
        private void IssueRequestToScheduler(BuildSubmission submission, bool allowMainThreadBuild, BuildRequestBlocker blocker)
        {
            bool resetMainThreadOnFailure = false;
            try
            {
                lock (_syncLock)
                {
                    if (_shuttingDown)
                    {
                        throw new BuildAbortedException();
                    }

                    if (allowMainThreadBuild && _buildParameters.LegacyThreadingSemantics)
                    {
                        if (_legacyThreadingData.MainThreadSubmissionId == -1)
                        {
                            resetMainThreadOnFailure = true;
                            _legacyThreadingData.MainThreadSubmissionId = submission.SubmissionId;
                        }
                    }

                    HandleNewRequest(Scheduler.VirtualNode, blocker);
                }
            }
            catch (Exception ex)
            {
                if (ExceptionHandling.IsCriticalException(ex))
                {
                    throw;
                }

                InvalidProjectFileException projectException = ex as InvalidProjectFileException;
                if (projectException != null)
                {
                    if (projectException.HasBeenLogged != true)
                    {
                        BuildEventContext projectBuildEventContext = new BuildEventContext(submission.SubmissionId, 1, BuildEventContext.InvalidProjectInstanceId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidTaskId);
                        ((IBuildComponentHost)this).LoggingService.LogInvalidProjectFileError(projectBuildEventContext, projectException);
                        projectException.HasBeenLogged = true;
                    }
                }
                else if ((ex is BuildAbortedException) || ExceptionHandling.NotExpectedException(ex))
                {
                    throw;
                }

                if (resetMainThreadOnFailure)
                {
                    _legacyThreadingData.MainThreadSubmissionId = -1;
                }

                if (projectException == null)
                {
                    BuildEventContext buildEventContext = new BuildEventContext(submission.SubmissionId, 1, BuildEventContext.InvalidProjectInstanceId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidTaskId);
                    ((IBuildComponentHost)this).LoggingService.LogFatalBuildError(buildEventContext, ex, new BuildEventFileInfo(submission.BuildRequestData.ProjectFullPath));
                }

                submission.CompleteLogging(true);
                ReportResultsToSubmission(new BuildResult(submission.BuildRequest, ex));
                _overallBuildSuccess = false;
            }
        }

        /// <summary>
        /// Asks the nodeManager to tell the currently connected nodes to shut down and sets a flag preventing all non-shutdown-related packets from
        /// being processed.
        /// </summary>
        private void ShutdownConnectedNodesAsync(bool abort)
        {
            _shuttingDown = true;

            // If we are aborting, we will NOT reuse the nodes because their state may be compromised by attempts to shut down while the build is in-progress.
            _nodeManager.ShutdownConnectedNodes(abort ? false : _buildParameters.EnableNodeReuse);

            // if we are aborting, the task host will hear about it in time through the task building infrastructure; 
            // so only shut down the task host nodes if we're shutting down tidily (in which case, it is assumed that all
            // tasks are finished building and thus that there's no risk of a race between the two shutdown pathways).  
            if (!abort)
            {
                _taskHostNodeManager.ShutdownConnectedNodes(_buildParameters.EnableNodeReuse);
            }
        }

        /// <summary>
        /// Retrieves the next build submission id.
        /// </summary>
        private int GetNextSubmissionId()
        {
            return _nextBuildSubmissionId++;
        }

        /// <summary>
        /// Errors if the BuildManager is in the specified state.
        /// </summary>
        private void ErrorIfState(BuildManagerState disallowedState, string exceptionResouorce)
        {
            if (_buildManagerState == disallowedState)
            {
                ErrorUtilities.ThrowInvalidOperation(exceptionResouorce);
            }
        }

        /// <summary>
        /// Verifies the BuildManager is in the required state, and throws a <see cref="System.InvalidOperationException"/> if it is not.
        /// </summary>
        private void RequireState(BuildManagerState requiredState, string exceptionResouorce)
        {
            ErrorUtilities.VerifyThrowInvalidOperation(_buildManagerState == requiredState, exceptionResouorce);
        }

        /// <summary>
        /// Verifies the BuildManager is in the required state, and throws a <see cref="System.InvalidOperationException"/> if it is not.
        /// </summary>
        private void VerifyStateInternal(BuildManagerState requiredState)
        {
            if (_buildManagerState != requiredState)
            {
                ErrorUtilities.ThrowInternalError("Expected state {0}, actual state {1}", requiredState, _buildManagerState);
            }
        }

        /// <summary>
        /// Method called to reset the state of the system after a build.
        /// </summary>
        private void Reset()
        {
            _nodeManager.UnregisterPacketHandler(NodePacketType.BuildRequestBlocker);
            _nodeManager.UnregisterPacketHandler(NodePacketType.BuildRequestConfiguration);
            _nodeManager.UnregisterPacketHandler(NodePacketType.BuildRequestConfigurationResponse);
            _nodeManager.UnregisterPacketHandler(NodePacketType.BuildResult);
            _nodeManager.UnregisterPacketHandler(NodePacketType.NodeShutdown);
            _nodeManager.ClearPerBuildState();
            _nodeManager = null;

            _shuttingDown = false;
            _nodeConfiguration = null;
            _buildSubmissions.Clear();
            _scheduler.Reset();
            _scheduler = null;
            _workQueue = null;
            _acquiredProjectRootElementCacheFromProjectInstance = false;

            _unnamedProjectInstanceToNames.Clear();
            _projectStartedEvents.Clear();
            _nodeIdToKnownConfigurations.Clear();
            _nextUnnamedProjectId = 1;

            if (_configCache != null)
            {
                foreach (BuildRequestConfiguration config in _configCache)
                {
                    config.ActivelyBuildingTargets.Clear();
                }
            }

            if (Environment.GetEnvironmentVariable("MSBUILDCLEARXMLCACHEONBUILDMANAGER") == "1")
            {
                // Optionally clear out the cache. This has the advantage of releasing memory,
                // but the disadvantage of causing the next build to repeat the load and parse.
                // We'll experiment here and ship with the best default.
                _buildParameters.ProjectRootElementCache.Clear();
            }
        }

        /// <summary>
        /// Returns a new, valid configuration id.
        /// </summary>
        private int GetNewConfigurationId()
        {
            int newId = Interlocked.Increment(ref s_nextBuildRequestConfigurationId);

            if (_scheduler != null)
            {
                // Minimum configuration id is always the lowest valid configuration id available, so increment after returning.
                while (newId <= _scheduler.MinimumAssignableConfigurationId) // Currently this minimum is one
                {
                    newId = Interlocked.Increment(ref s_nextBuildRequestConfigurationId);
                }
            }

            return newId;
        }

        /// <summary>
        /// Finds a matching configuration in the cache and returns it, or stores the configuration passed in.
        /// </summary>
        private BuildRequestConfiguration ResolveConfiguration(BuildRequestConfiguration unresolvedConfiguration, BuildRequestConfiguration matchingConfigurationFromCache, bool replaceProjectInstance)
        {
            BuildRequestConfiguration resolvedConfiguration = matchingConfigurationFromCache ?? _configCache.GetMatchingConfiguration(unresolvedConfiguration);
            if (resolvedConfiguration == null)
            {
                int newConfigurationId = _scheduler.GetConfigurationIdFromPlan(unresolvedConfiguration.ProjectFullPath);
                if (_configCache.HasConfiguration(newConfigurationId) || (newConfigurationId == BuildRequestConfiguration.InvalidConfigurationId))
                {
                    // There is already a configuration like this one or one didn't exist in a plan, so generate a new ID.
                    newConfigurationId = GetNewConfigurationId();
                }

                resolvedConfiguration = unresolvedConfiguration.ShallowCloneWithNewId(newConfigurationId);
                _configCache.AddConfiguration(resolvedConfiguration);
            }
            else if (replaceProjectInstance && unresolvedConfiguration.Project != null)
            {
                resolvedConfiguration.Project = unresolvedConfiguration.Project;
                _resultsCache.ClearResultsForConfiguration(resolvedConfiguration.ConfigurationId);
            }
            else if (unresolvedConfiguration.Project != null && resolvedConfiguration.Project != null && !Object.ReferenceEquals(unresolvedConfiguration.Project, resolvedConfiguration.Project))
            {
                // The user passed in a different instance than the one we already had.  Throw away any corresponding results.
                _resultsCache.ClearResultsForConfiguration(resolvedConfiguration.ConfigurationId);
                resolvedConfiguration.Project = unresolvedConfiguration.Project;
            }

            return resolvedConfiguration;
        }

        /// <summary>
        /// Handles a new request coming from a node.
        /// </summary>
        private void HandleNewRequest(int node, BuildRequestBlocker blocker)
        {
            // If we received any solution files, populate their configurations now.          
            if (blocker.BuildRequests != null)
            {
                foreach (BuildRequest request in blocker.BuildRequests)
                {
                    BuildRequestConfiguration config = _configCache[request.ConfigurationId];
                    if (FileUtilities.IsSolutionFilename(config.ProjectFullPath))
                    {
                        try
                        {
                            LoadSolutionIntoConfiguration(config, request.BuildEventContext);
                        }
                        catch (InvalidProjectFileException e)
                        {
                            // Throw the error in the cache.  The Scheduler will pick it up and return the results correctly.
                            _resultsCache.AddResult(new BuildResult(request, e));
                            if (node == Scheduler.VirtualNode)
                            {
                                throw;
                            }
                        }
                    }
                }
            }

            IEnumerable<ScheduleResponse> response = _scheduler.ReportRequestBlocked(node, blocker);
            PerformSchedulingActions(response);
        }

        /// <summary>
        /// Handles a configuration request coming from a node.
        /// </summary>
        private void HandleConfigurationRequest(int node, BuildRequestConfiguration unresolvedConfiguration)
        {
            BuildRequestConfiguration resolvedConfiguration = ResolveConfiguration(unresolvedConfiguration, null, false);

            BuildRequestConfigurationResponse response = new BuildRequestConfigurationResponse(unresolvedConfiguration.ConfigurationId, resolvedConfiguration.ConfigurationId, resolvedConfiguration.ResultsNodeId);

            HashSet<NGen<int>> configurationsOnNode = null;
            if (!_nodeIdToKnownConfigurations.TryGetValue(node, out configurationsOnNode))
            {
                configurationsOnNode = new HashSet<NGen<int>>();
                _nodeIdToKnownConfigurations[node] = configurationsOnNode;
            }

            configurationsOnNode.Add(resolvedConfiguration.ConfigurationId);

            _nodeManager.SendData(node, response);
        }

        /// <summary>
        /// Handles a build result coming from a node.
        /// </summary>
        private void HandleResult(int node, BuildResult result)
        {
            // Update cache with the default and initial targets, as needed.
            BuildRequestConfiguration configuration = _configCache[result.ConfigurationId];
            if (result.DefaultTargets != null)
            {
                // If the result has Default and Initial targets, we populate the configuration cache with them if it
                // doesn't already have entries.  This can happen if we created a configuration based on a request from
                // an external node, but hadn't yet received a result since we may not have loaded the Project locally 
                // and thus wouldn't know what the default and initial targets were.
                if (configuration.ProjectDefaultTargets == null)
                {
                    configuration.ProjectDefaultTargets = result.DefaultTargets;
                }

                if (configuration.ProjectInitialTargets == null)
                {
                    configuration.ProjectInitialTargets = result.InitialTargets;
                }
            }

            IEnumerable<ScheduleResponse> response = _scheduler.ReportResult(node, result);
            PerformSchedulingActions(response);
        }

        /// <summary>
        /// Handles the NodeShutdown packet
        /// </summary>
        private void HandleNodeShutdown(int node, NodeShutdown shutdownPacket)
        {
            _shuttingDown = true;
            ErrorUtilities.VerifyThrow(_activeNodes.Contains(node), "Unexpected shutdown from node {0} which shouldn't exist.", node);
            _activeNodes.Remove(node);

            if (shutdownPacket.Reason != NodeShutdownReason.Requested)
            {
                if (shutdownPacket.Reason == NodeShutdownReason.ConnectionFailed)
                {
                    ILoggingService loggingService = ((IBuildComponentHost)this).GetComponent(BuildComponentType.LoggingService) as ILoggingService;
                    foreach (BuildSubmission submission in _buildSubmissions.Values)
                    {
                        BuildEventContext buildEventContext = new BuildEventContext(submission.SubmissionId, BuildEventContext.InvalidNodeId, BuildEventContext.InvalidProjectInstanceId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidTaskId);
                        loggingService.LogError(buildEventContext, new BuildEventFileInfo(String.Empty) /* no project file */, "ChildExitedPrematurely", node);
                    }
                }
                else if (shutdownPacket.Reason == NodeShutdownReason.Error && _buildSubmissions.Values.Count == 0)
                {
                    // We have no submissions to attach any exceptions to, lets just log it here.
                    if (shutdownPacket.Exception != null)
                    {
                        ILoggingService loggingService = ((IBuildComponentHost)this).GetComponent(BuildComponentType.LoggingService) as ILoggingService;
                        loggingService.LogError(BuildEventContext.Invalid, new BuildEventFileInfo(String.Empty) /* no project file */, "ChildExitedPrematurely", shutdownPacket.Exception.ToString());
                        OnThreadException(shutdownPacket.Exception);
                    }
                }

                _nodeManager.ShutdownConnectedNodes(_buildParameters.EnableNodeReuse);
                _taskHostNodeManager.ShutdownConnectedNodes(_buildParameters.EnableNodeReuse);

                foreach (BuildSubmission submission in _buildSubmissions.Values)
                {
                    // The submission has not started
                    if (submission.BuildRequest == null)
                    {
                        continue;
                    }

                    _resultsCache.AddResult(new BuildResult(submission.BuildRequest, shutdownPacket.Exception ?? new BuildAbortedException()));
                }

                _scheduler.ReportBuildAborted(node);
            }

            CheckForActiveNodesAndCleanUpSubmissions();
        }

        /// <summary>
        /// If there are no more active nodes, cleans up any remaining submissions.
        /// </summary>
        /// <remarks>
        /// Must only be called from within the sync lock.
        /// </remarks>
        private void CheckForActiveNodesAndCleanUpSubmissions()
        {
            if (_activeNodes.Count == 0)
            {
                List<BuildSubmission> submissions = new List<BuildSubmission>(_buildSubmissions.Values);
                foreach (BuildSubmission submission in submissions)
                {
                    // The submission has not started do not add it to the results cache
                    if (submission.BuildRequest == null)
                    {
                        continue;
                    }

                    // UNDONE: (stability) It might be best to trigger the logging service to shut down here,
                    //         since the full build is complete.  This would allow us to ensure all logging messages have been
                    //         drained and all submissions can complete their logging requirements.
                    BuildResult result = _resultsCache.GetResultsForConfiguration(submission.BuildRequest.ConfigurationId);
                    if (result == null)
                    {
                        // If we had no results, the build aborted before we had a chance to generate any.
                        result = new BuildResult(submission.BuildRequest, new BuildAbortedException());
                    }

                    submission.CompleteResults(result);

                    // If we never received a project started event, consider logging complete anyhow, since the nodes have
                    // shut down.
                    submission.CompleteLogging(waitForLoggingThread: false);

                    _overallBuildSuccess = _overallBuildSuccess && (submission.BuildResult.OverallResult == BuildResultCode.Success);
                    CheckSubmissionCompletenessAndRemove(submission);
                }

                _noNodesActiveEvent.Set();
            }
        }

        /// <summary>
        /// Carries out the actions specified by the scheduler.
        /// </summary>
        private void PerformSchedulingActions(IEnumerable<ScheduleResponse> responses)
        {
            foreach (ScheduleResponse response in responses)
            {
                switch (response.Action)
                {
                    case ScheduleActionType.NoAction:
                        break;

                    case ScheduleActionType.SubmissionComplete:
                        if (_buildParameters.DetailedSummary)
                        {
                            _scheduler.WriteDetailedSummary(response.BuildResult.SubmissionId);
                        }

                        ReportResultsToSubmission(response.BuildResult);
                        break;

                    case ScheduleActionType.CircularDependency:
                    case ScheduleActionType.ResumeExecution:
                    case ScheduleActionType.ReportResults:
                        _nodeManager.SendData(response.NodeId, response.Unblocker);
                        break;

                    case ScheduleActionType.CreateNode:
                        List<NodeInfo> newNodes = new List<NodeInfo>();

                        for (int i = 0; i < response.NumberOfNodesToCreate; i++)
                        {
                            NodeInfo createdNode = _nodeManager.CreateNode(GetNodeConfiguration(), response.RequiredNodeType);

                            if (null != createdNode)
                            {
                                _noNodesActiveEvent.Reset();
                                _activeNodes.Add(createdNode.NodeId);
                                newNodes.Add(createdNode);
                                ErrorUtilities.VerifyThrow(_activeNodes.Count != 0, "Still 0 nodes after asking for a new node.  Build cannot proceed.");
                            }
                            else
                            {
                                BuildEventContext buildEventContext = new BuildEventContext(0, Scheduler.VirtualNode, BuildEventContext.InvalidProjectInstanceId, BuildEventContext.InvalidProjectContextId, BuildEventContext.InvalidTargetId, BuildEventContext.InvalidTaskId);
                                ((IBuildComponentHost)this).LoggingService.LogError(buildEventContext, new BuildEventFileInfo(String.Empty), "UnableToCreateNode", response.RequiredNodeType.ToString("G"));

                                throw new BuildAbortedException(ResourceUtilities.FormatResourceString("UnableToCreateNode", response.RequiredNodeType.ToString("G")));
                            }
                        }

                        IEnumerable<ScheduleResponse> newResponses = _scheduler.ReportNodesCreated(newNodes);
                        PerformSchedulingActions(newResponses);

                        break;

                    case ScheduleActionType.Schedule:
                    case ScheduleActionType.ScheduleWithConfiguration:
                        if (response.Action == ScheduleActionType.ScheduleWithConfiguration)
                        {
                            // Only actually send the configuration if the node doesn't know about it.  The scheduler only keeps track
                            // of which nodes have had configurations specifically assigned to them for building.  However, a node may
                            // have created a configuration based on a build request it needs to wait on.  In this
                            // case we need not send the configuration since it will already have been mapped earlier.
                            HashSet<NGen<int>> configurationsOnNode = null;
                            if (!_nodeIdToKnownConfigurations.TryGetValue(response.NodeId, out configurationsOnNode) ||
                               !configurationsOnNode.Contains(response.BuildRequest.ConfigurationId))
                            {
                                IConfigCache configCache = _componentFactories.GetComponent(BuildComponentType.ConfigCache) as IConfigCache;
                                _nodeManager.SendData(response.NodeId, configCache[response.BuildRequest.ConfigurationId]);
                            }
                        }

                        _nodeManager.SendData(response.NodeId, response.BuildRequest);
                        break;

                    default:
                        ErrorUtilities.ThrowInternalError("Scheduling action {0} not handled.", response.Action);
                        break;
                }
            }
        }

        /// <summary>
        /// Completes a submission using the specified overall results.
        /// </summary>
        private void ReportResultsToSubmission(BuildResult result)
        {
            lock (_syncLock)
            {
                // The build submission has not already been completed.
                if (_buildSubmissions.ContainsKey(result.SubmissionId))
                {
                    BuildSubmission submission = _buildSubmissions[result.SubmissionId];
                    submission.CompleteResults(result);

                    // If the request failed because we caught an exception from the loggers, we can assume we will receive no more logging messages for
                    // this submission, therefore set the logging as complete. IntrnalLoggerExceptions are unhandled exceptions from the logger. If the logger author does
                    // not handle an exception the eventsource wraps all exceptions (except a logging exception) into an internal logging exception.
                    // These exceptions will have their stack logged on the commandline as an unexpected failure. If a logger author wants the logger
                    // to fail gracefully then can catch an exception and log a LoggerException. This has the same effect of stopping the build but it logs only
                    // the exception error message rather than the whole stack trace.
                    if (result.Exception is InternalLoggerException || result.Exception is LoggerException || result.Exception is InvalidOperationException)
                    {
                        submission.CompleteLogging(false /* waitForLoggingThread */);
                    }

                    _overallBuildSuccess = _overallBuildSuccess && (_buildSubmissions[result.SubmissionId].BuildResult.OverallResult == BuildResultCode.Success);

                    CheckSubmissionCompletenessAndRemove(submission);
                }
            }
        }

        /// <summary>
        /// Determines if the submission is fully completed.
        /// </summary>
        private void CheckSubmissionCompletenessAndRemove(BuildSubmission submission)
        {
            lock (_syncLock)
            {
                // If the submission has completed or never started, remove it.
                if (submission.IsCompleted || submission.BuildRequest == null)
                {
                    _buildSubmissions.Remove(submission.SubmissionId);
                }

                if (_buildSubmissions.Count == 0)
                {
                    _noActiveSubmissionsEvent.Set();
                }
            }
        }

        /// <summary>
        /// Retrieves the configuration structure for a node.
        /// </summary>
        private NodeConfiguration GetNodeConfiguration()
        {
            if (null == _nodeConfiguration)
            {
                // Get the remote loggers                
                ILoggingService loggingService = ((IBuildComponentHost)this).GetComponent(BuildComponentType.LoggingService) as ILoggingService;
                List<LoggerDescription> remoteLoggers = new List<LoggerDescription>(loggingService.LoggerDescriptions);

                _nodeConfiguration = new NodeConfiguration
                (
                -1, /* must be assigned by the NodeManager */
                _buildParameters,
                remoteLoggers.ToArray(),
                AppDomain.CurrentDomain.SetupInformation
                );
            }

            return _nodeConfiguration;
        }

        /// <summary>
        /// Handler for thread exceptions (logging thread, communications thread).  This handler will only get called if the exception did not previously
        /// get handled by a node exception handlers (for instance because the build is complete for the node.)  In this case we
        /// get the exception and will put it into the OverallBuildResult so that the host can see what happened.
        /// </summary>
        private void OnThreadException(Exception e)
        {
            lock (_syncLock)
            {
                if (_threadException == null)
                {
                    _threadException = e;
                    List<BuildSubmission> submissions = new List<BuildSubmission>(_buildSubmissions.Values);
                    foreach (BuildSubmission submission in submissions)
                    {
                        // Submission has not started
                        if (submission.BuildRequest == null)
                        {
                            continue;
                        }

                        submission.CompleteLogging(false);

                        // Attach the exception to this submission if it does not already have an exception associated with it
                        if (submission.BuildResult == null || (submission.BuildResult.Exception == null))
                        {
                            if (submission.BuildResult == null)
                            {
                                submission.BuildResult = new BuildResult(submission.BuildRequest, e);
                            }
                            else
                            {
                                submission.BuildResult.Exception = _threadException;
                            }
                        }

                        CheckSubmissionCompletenessAndRemove(submission);
                    }
                }
            }
        }

        /// <summary>
        /// Raised when a project finished logging message has been processed.
        /// </summary>
        private void OnProjectFinished(object sender, ProjectFinishedEventArgs e)
        {
            lock (_syncLock)
            {
                BuildEventArgs originalArgs;
                if (_projectStartedEvents.TryGetValue(e.BuildEventContext.SubmissionId, out originalArgs))
                {
                    if (originalArgs.BuildEventContext.Equals(e.BuildEventContext))
                    {
                        BuildSubmission submission;
                        _projectStartedEvents.Remove(e.BuildEventContext.SubmissionId);
                        if (_buildSubmissions.TryGetValue(e.BuildEventContext.SubmissionId, out submission))
                        {
                            submission.CompleteLogging(false);
                            CheckSubmissionCompletenessAndRemove(submission);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Raised when a project started logging message is about to be processed.
        /// </summary>
        private void OnProjectStarted(object sender, ProjectStartedEventArgs e)
        {
            if (!_projectStartedEvents.ContainsKey(e.BuildEventContext.SubmissionId))
            {
                _projectStartedEvents[e.BuildEventContext.SubmissionId] = e;
            }
        }

        /// <summary>
        /// Creates a logging service around the specified set of loggers.
        /// </summary>
        private ILoggingService CreateLoggingService(IEnumerable<ILogger> loggers, IEnumerable<ForwardingLoggerRecord> forwardingLoggers)
        {
            int cpuCount = _buildParameters.MaxNodeCount;

            LoggerMode loggerMode = (cpuCount == 1 && _buildParameters.UseSynchronousLogging) ? LoggerMode.Synchronous : LoggerMode.Asynchronous;

            ILoggingService loggingService = (ILoggingService)Microsoft.Build.BackEnd.Logging.LoggingService.CreateLoggingService(loggerMode, 1 /*This logging service is used for the build manager and the inproc node, therefore it should have the first nodeId*/);

            ((IBuildComponent)loggingService).InitializeComponent(this);
            _componentFactories.ReplaceFactory(BuildComponentType.LoggingService, loggingService as IBuildComponent);

            _threadException = null;
            loggingService.OnLoggingThreadException += _loggingThreadExceptionEventHandler;
            loggingService.OnProjectStarted += _projectStartedEventHandler;
            loggingService.OnProjectFinished += _projectFinishedEventHandler;

            try
            {
                if (loggers != null)
                {
                    foreach (ILogger logger in loggers)
                    {
                        loggingService.RegisterLogger(logger);
                    }
                }

                if (loggingService.Loggers.Count == 0)
                {
                    // We need to register SOME logger if we don't have any. This ensures the out of proc nodes will still send us message,
                    // ensuring we receive project started and finished events.
                    Assembly engineAssembly = Assembly.GetAssembly(typeof(ProjectCollection));
                    LoggerDescription forwardingLoggerDescription = new LoggerDescription(
                        loggerClassName: typeof(ConfigurableForwardingLogger).FullName,
                        loggerAssemblyName: typeof(ConfigurableForwardingLogger).Assembly.GetName().FullName,
                        loggerAssemblyFile: null,
                        loggerSwitchParameters: "PROJECTSTARTEDEVENT;PROJECTFINISHEDEVENT",
                        verbosity: LoggerVerbosity.Quiet);

                    ForwardingLoggerRecord[] forwardingLogger = { new ForwardingLoggerRecord(new NullLogger(), forwardingLoggerDescription) };
                    forwardingLoggers = forwardingLoggers == null ? forwardingLogger : forwardingLoggers.Concat(forwardingLogger);
                }

                if (forwardingLoggers != null)
                {
                    foreach (ForwardingLoggerRecord forwardingLoggerRecord in forwardingLoggers)
                    {
                        loggingService.RegisterDistributedLogger(forwardingLoggerRecord.CentralLogger, forwardingLoggerRecord.ForwardingLoggerDescription);
                    }
                }
            }
            catch (Exception ex)
            {
                if (ExceptionHandling.IsCriticalException(ex))
                {
                    throw;
                }

                if (loggingService != null)
                {
                    ShutdownLoggingService(loggingService);
                }

                throw;
            }

            return loggingService;
        }

        /// <summary>
        /// Ensures that the packet type matches the expected type
        /// </summary>
        /// <typeparam name="I">The instance-type of packet being expected</typeparam>
        private I ExpectPacketType<I>(INodePacket packet, NodePacketType expectedType) where I : class, INodePacket
        {
            I castPacket = packet as I;

            // PERF: Not using VerifyThrow here to avoid boxing of expectedType.
            if (castPacket == null)
            {
                ErrorUtilities.ThrowInternalError("Incorrect packet type: {0} should have been {1}", packet.Type, expectedType);
            }

            return castPacket;
        }

        /// <summary>
        ///  Shutdown the logging service
        /// </summary>
        private void ShutdownLoggingService(ILoggingService loggingService)
        {
            try
            {
                if (loggingService != null)
                {
                    loggingService.OnLoggingThreadException -= _loggingThreadExceptionEventHandler;
                    loggingService.OnProjectFinished -= _projectFinishedEventHandler;
                    loggingService.OnProjectStarted -= _projectStartedEventHandler;
                    _componentFactories.ShutdownComponent(BuildComponentType.LoggingService);
                }
            }
            finally
            {
                // Even if an exception is thrown, we want to make sure we null out the logging service so that 
                // we don't try to shut it down again in some other cleanup code. 
                _componentFactories.ReplaceFactory(BuildComponentType.LoggingService, (IBuildComponent)null);
            }
        }

        /// <summary>
        /// Dispose implementation
        /// </summary>
        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    lock (_syncLock)
                    {
                        // We should always have finished cleaning up before calling Dispose.
                        RequireState(BuildManagerState.Idle, "ShouldNotDisposeWhenBuildManagerActive");

                        if (_componentFactories != null)
                        {
                            _componentFactories.ShutdownComponents();
                        }

                        if (_workQueue != null)
                        {
                            _workQueue.Complete();
                            _workQueue = null;
                        }

                        if (_noActiveSubmissionsEvent != null)
                        {
                            _noActiveSubmissionsEvent.Dispose();
                            _noActiveSubmissionsEvent = null;
                        }

                        if (_noNodesActiveEvent != null)
                        {
                            _noNodesActiveEvent.Dispose();
                            _noNodesActiveEvent = null;
                        }

                        if (Object.ReferenceEquals(this, BuildManager.s_singletonInstance))
                        {
                            BuildManager.s_singletonInstance = null;
                        }

                        _disposed = true;
                    }
                }
            }
        }

        /// <summary>
        /// The logger registered to the logging service when no other one is.
        /// </summary>
        private class NullLogger : ILogger
        {
            #region ILogger Members

            /// <summary>
            /// The logger verbosity.
            /// </summary>
            public LoggerVerbosity Verbosity
            {
                get
                {
                    return LoggerVerbosity.Normal;
                }

                set
                {
                }
            }

            /// <summary>
            /// The logger parameters.
            /// </summary>
            public string Parameters
            {
                get
                {
                    return String.Empty;
                }

                set
                {
                }
            }

            /// <summary>
            /// Initialize.
            /// </summary>
            public void Initialize(IEventSource eventSource)
            {
            }

            /// <summary>
            /// Shutdown.
            /// </summary>
            public void Shutdown()
            {
            }

            #endregion
        }
    }
}
