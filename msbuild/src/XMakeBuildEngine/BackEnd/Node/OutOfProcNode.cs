﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Class implementing an out-of-proc node.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Globalization;
using System.Threading;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Build.BackEnd;
using Microsoft.Build.BackEnd.Logging;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Internal;
using Microsoft.Build.BackEnd.Components.Caching;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// This class represents an implementation of INode for out-of-proc nodes.
    /// </summary>
    public class OutOfProcNode : INode, IBuildComponentHost, INodePacketFactory, INodePacketHandler
    {
        /// <summary>
        /// Whether the current appdomain has an out of proc node.
        /// For diagnostics.
        /// </summary>
        private static bool s_isOutOfProcNode;

        /// <summary>
        /// The one and only project root element cache to be used for the build
        /// on this out of proc node.
        /// </summary>
        private static ProjectRootElementCache s_projectRootElementCache;

        /// <summary>
        /// The endpoint used to talk to the host.
        /// </summary>
        private NodeEndpointOutOfProc _nodeEndpoint;

        /// <summary>
        /// The saved environment for the process.
        /// </summary>
        private IDictionary<string, string> _savedEnvironment;

        /// <summary>
        /// The component factories.
        /// </summary>
        private BuildComponentFactoryCollection _componentFactories;

        /// <summary>
        /// The build system parameters.
        /// </summary>
        private BuildParameters _buildParameters;

        /// <summary>
        /// The logging service.
        /// </summary>
        private ILoggingService _loggingService;

        /// <summary>
        /// The node logging context.
        /// </summary>
        private NodeLoggingContext _loggingContext;

        /// <summary>
        /// The global config cache.
        /// </summary>
        private IConfigCache _globalConfigCache;

        /// <summary>
        /// The global node manager
        /// </summary>
        private INodeManager _taskHostNodeManager;

        /// <summary>
        /// The build request engine.
        /// </summary>
        private IBuildRequestEngine _buildRequestEngine;

        /// <summary>
        /// The packet factory.
        /// </summary>
        private NodePacketFactory _packetFactory;

        /// <summary>
        /// The current node configuration
        /// </summary>
        private NodeConfiguration _currentConfiguration;

        /// <summary>
        /// The queue of packets we have received but which have not yet been processed.
        /// </summary>
        private Queue<INodePacket> _receivedPackets;

        /// <summary>
        /// The event which is set when we receive packets.
        /// </summary>
        private AutoResetEvent _packetReceivedEvent;

        /// <summary>
        /// The event which is set when we should shut down.
        /// </summary>
        private ManualResetEvent _shutdownEvent;

        /// <summary>
        /// The reason we are shutting down.
        /// </summary>
        private NodeEngineShutdownReason _shutdownReason;

        /// <summary>
        /// The exception, if any, which caused shutdown.
        /// </summary>
        private Exception _shutdownException;

        /// <summary>
        /// Flag indicating if we should debug communications or not.
        /// </summary>
        private bool _debugCommunications = false;

        /// <summary>
        /// Data for the use of LegacyThreading semantics.
        /// </summary>
        private LegacyThreadingData _legacyThreadingData;

        /// <summary>
        /// Constructor.
        /// </summary>
        public OutOfProcNode()
        {
            s_isOutOfProcNode = true;

            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(ExceptionHandling.UnhandledExceptionHandler);

            _debugCommunications = (Environment.GetEnvironmentVariable("MSBUILDDEBUGCOMM") == "1");

            _receivedPackets = new Queue<INodePacket>();
            _packetReceivedEvent = new AutoResetEvent(false);
            _shutdownEvent = new ManualResetEvent(false);
            _legacyThreadingData = new LegacyThreadingData();

            _componentFactories = new BuildComponentFactoryCollection(this);
            _componentFactories.RegisterDefaultFactories();
            _packetFactory = new NodePacketFactory();

            _buildRequestEngine = (this as IBuildComponentHost).GetComponent(BuildComponentType.RequestEngine) as IBuildRequestEngine;
            _globalConfigCache = (this as IBuildComponentHost).GetComponent(BuildComponentType.ConfigCache) as IConfigCache;
            _taskHostNodeManager = (this as IBuildComponentHost).GetComponent(BuildComponentType.TaskHostNodeManager) as INodeManager;

            if (s_projectRootElementCache == null)
            {
                s_projectRootElementCache = new ProjectRootElementCache(true /* automatically reload any changes from disk */);
            }

            _buildRequestEngine.OnEngineException += new EngineExceptionDelegate(OnEngineException);
            _buildRequestEngine.OnNewConfigurationRequest += new NewConfigurationRequestDelegate(OnNewConfigurationRequest);
            _buildRequestEngine.OnRequestBlocked += new RequestBlockedDelegate(OnNewRequest);
            _buildRequestEngine.OnRequestComplete += new RequestCompleteDelegate(OnRequestComplete);

            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.BuildRequest, BuildRequest.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.BuildRequestConfiguration, BuildRequestConfiguration.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.BuildRequestConfigurationResponse, BuildRequestConfigurationResponse.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.BuildRequestUnblocker, BuildRequestUnblocker.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.NodeConfiguration, NodeConfiguration.FactoryForDeserialization, this);
            (this as INodePacketFactory).RegisterPacketHandler(NodePacketType.NodeBuildComplete, NodeBuildComplete.FactoryForDeserialization, this);
        }

        /// <summary>
        /// Get the logging service for a build.
        /// </summary>
        /// <returns>The logging service.</returns>
        ILoggingService IBuildComponentHost.LoggingService
        {
            get
            {
                return _loggingService;
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
        /// Retrieves the name of this component host.
        /// </summary>
        string IBuildComponentHost.Name
        {
            get
            {
                return "OutOfProc";
            }
        }

        /// <summary>
        /// Retrieves the build parameters for the current build.
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
        /// Whether the current appdomain has an out of proc node.
        /// </summary>
        internal static bool IsOutOfProcNode
        {
            get { return s_isOutOfProcNode; }
        }

        #region INode Members

        /// <summary>
        /// Starts up the node and processes messages until the node is requested to shut down.
        /// Assumes no node reuse.
        /// </summary>
        /// <param name="shutdownException">The exception which caused shutdown, if any.</param>
        /// <returns>The reason for shutting down.</returns>
        public NodeEngineShutdownReason Run(out Exception shutdownException)
        {
            return Run(false, out shutdownException);
        }


        /// <summary>
        /// Starts up the node and processes messages until the node is requested to shut down.
        /// </summary>
        /// <param name="enableReuse">Whether this node is eligible for reuse later.</param>
        /// <param name="shutdownException">The exception which caused shutdown, if any.</param>
        /// <returns>The reason for shutting down.</returns>
        public NodeEngineShutdownReason Run(bool enableReuse, out Exception shutdownException)
        {
            // Console.WriteLine("Run called at {0}", DateTime.Now);
            string pipeName = "MSBuild" + Process.GetCurrentProcess().Id;

            _nodeEndpoint = new NodeEndpointOutOfProc(pipeName, this, enableReuse);
            _nodeEndpoint.OnLinkStatusChanged += new LinkStatusChangedDelegate(OnLinkStatusChanged);
            _nodeEndpoint.Listen(this);

            WaitHandle[] waitHandles = new WaitHandle[] { _shutdownEvent, _packetReceivedEvent };

            // Get the current directory before doing any work. We need this so we can restore the directory when the node shutsdown.
            while (true)
            {
                int index = WaitHandle.WaitAny(waitHandles);
                switch (index)
                {
                    case 0:
                        NodeEngineShutdownReason shutdownReason = HandleShutdown(out shutdownException);
                        return shutdownReason;

                    case 1:
                        INodePacket packet = null;

                        int packetCount = _receivedPackets.Count;

                        while (packetCount > 0)
                        {
                            lock (_receivedPackets)
                            {
                                if (_receivedPackets.Count > 0)
                                {
                                    packet = _receivedPackets.Dequeue();
                                }
                                else
                                {
                                    break;
                                }
                            }

                            if (packet != null)
                            {
                                HandlePacket(packet);
                            }
                        }

                        break;
                }
            }

            // UNREACHABLE
        }

        #endregion

        #region IBuildComponentHost Members

        /// <summary>
        /// Registers a factory with the component host.
        /// </summary>
        /// <param name="factoryType">The factory type to register.</param>
        /// <param name="factory">The factory method.</param>
        void IBuildComponentHost.RegisterFactory(BuildComponentType factoryType, BuildComponentFactoryDelegate factory)
        {
            _componentFactories.ReplaceFactory(factoryType, factory);
        }

        /// <summary>
        /// Get a component from the host.
        /// </summary>
        /// <param name="type">The component type to get.</param>
        /// <returns>The component.</returns>
        IBuildComponent IBuildComponentHost.GetComponent(BuildComponentType type)
        {
            return _componentFactories.GetComponent(type);
        }

        #endregion

        #region INodePacketFactory Members

        /// <summary>
        /// Registers a packet handler.
        /// </summary>
        /// <param name="packetType">The packet type for which the handler should be registered.</param>
        /// <param name="factory">The factory used to create packets.</param>
        /// <param name="handler">The handler for the packets.</param>
        void INodePacketFactory.RegisterPacketHandler(NodePacketType packetType, NodePacketFactoryMethod factory, INodePacketHandler handler)
        {
            _packetFactory.RegisterPacketHandler(packetType, factory, handler);
        }

        /// <summary>
        /// Unregisters a packet handler.
        /// </summary>
        /// <param name="packetType">The type of packet for which the handler should be unregistered.</param>
        void INodePacketFactory.UnregisterPacketHandler(NodePacketType packetType)
        {
            _packetFactory.UnregisterPacketHandler(packetType);
        }

        /// <summary>
        /// Deserializes and routes a packer to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node from which the packet was received.</param>
        /// <param name="packetType">The packet type.</param>
        /// <param name="translator">The translator to use as a source for packet data.</param>
        void INodePacketFactory.DeserializeAndRoutePacket(int nodeId, NodePacketType packetType, INodePacketTranslator translator)
        {
            _packetFactory.DeserializeAndRoutePacket(nodeId, packetType, translator);
        }

        /// <summary>
        /// Routes a packet to the appropriate handler.
        /// </summary>
        /// <param name="nodeId">The node id from which the packet was received.</param>
        /// <param name="packet">The packet to route.</param>
        void INodePacketFactory.RoutePacket(int nodeId, INodePacket packet)
        {
            _packetFactory.RoutePacket(nodeId, packet);
        }

        #endregion

        #region INodePacketHandler Members

        /// <summary>
        /// Called when a packet has been received.
        /// </summary>
        /// <param name="node">The node from which the packet was received.</param>
        /// <param name="packet">The packet.</param>
        void INodePacketHandler.PacketReceived(int node, INodePacket packet)
        {
            lock (_receivedPackets)
            {
                _receivedPackets.Enqueue(packet);
                _packetReceivedEvent.Set();
            }
        }

        #endregion

        /// <summary>
        /// Event handler for the BuildEngine's OnRequestComplete event.
        /// </summary>
        private void OnRequestComplete(BuildRequest request, BuildResult result)
        {
            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                _nodeEndpoint.SendData(result);
            }
        }

        /// <summary>
        /// Event handler for the BuildEngine's OnNewRequest event.
        /// </summary>
        private void OnNewRequest(BuildRequestBlocker blocker)
        {
            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                _nodeEndpoint.SendData(blocker);
            }
        }

        /// <summary>
        /// Event handler for the BuildEngine's OnNewConfigurationRequest event.
        /// </summary>
        private void OnNewConfigurationRequest(BuildRequestConfiguration config)
        {
            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                _nodeEndpoint.SendData(config);
            }
        }

        /// <summary>
        /// Event handler for the LoggingService's OnLoggingThreadException event.
        /// </summary>
        private void OnLoggingThreadException(Exception e)
        {
            OnEngineException(e);
        }

        /// <summary>
        /// Event handler for the BuildEngine's OnEngineException event.
        /// </summary>
        private void OnEngineException(Exception e)
        {
            _shutdownException = e;
            _shutdownReason = NodeEngineShutdownReason.Error;
            _shutdownEvent.Set();
        }

        /// <summary>
        /// Perform necessary actions to shut down the node.
        /// </summary>
        private NodeEngineShutdownReason HandleShutdown(out Exception exception)
        {
            if (_debugCommunications)
            {
                using (StreamWriter writer = File.CreateText(String.Format(CultureInfo.CurrentCulture, Path.Combine(Path.GetTempPath(), @"MSBuild_NodeShutdown_{0}.txt"), Process.GetCurrentProcess().Id)))
                {
                    writer.WriteLine("Node shutting down with reason {0} and exception: {1}", _shutdownReason, _shutdownException);
                }
            }

            // Clean up the engine
            if (null != _buildRequestEngine && _buildRequestEngine.Status != BuildRequestEngineStatus.Uninitialized)
            {
                _buildRequestEngine.CleanupForBuild();

                if (_shutdownReason == NodeEngineShutdownReason.BuildCompleteReuse)
                {
                    ((IBuildComponent)_buildRequestEngine).ShutdownComponent();
                }
            }

            // Dispose of any build registered objects
            IRegisteredTaskObjectCache objectCache = (IRegisteredTaskObjectCache)(_componentFactories.GetComponent(BuildComponentType.RegisteredTaskObjectCache));
            objectCache.DisposeCacheObjects(RegisteredTaskObjectLifetime.Build);

            if (_shutdownReason != NodeEngineShutdownReason.BuildCompleteReuse)
            {
                // Dispose of any node registered objects.
                ((IBuildComponent)objectCache).ShutdownComponent();
            }

            // Shutdown any Out Of Proc Nodes Created
            _taskHostNodeManager.ShutdownConnectedNodes(_shutdownReason == NodeEngineShutdownReason.BuildCompleteReuse);

            // Restore the original current directory.
            NativeMethodsShared.SetCurrentDirectory(Environment.SystemDirectory);

            // Restore the original environment.
            // If the node was never configured, this will be null.
            if (_savedEnvironment != null)
            {
                foreach (KeyValuePair<string, string> entry in CommunicationsUtilities.GetEnvironmentVariables())
                {
                    if (!_savedEnvironment.ContainsKey(entry.Key))
                    {
                        Environment.SetEnvironmentVariable(entry.Key, null);
                    }
                }

                foreach (KeyValuePair<string, string> entry in _savedEnvironment)
                {
                    Environment.SetEnvironmentVariable(entry.Key, entry.Value);
                }
            }

            try
            {
                // Shut down logging, which will cause all queued logging messages to be sent.
                if (null != _loggingContext && null != _loggingService)
                {
                    _loggingContext.LogBuildFinished(true);
                    ((IBuildComponent)_loggingService).ShutdownComponent();
                }
            }
            finally
            {
                // Shut down logging, which will cause all queued logging messages to be sent.
                if (null != _loggingContext && null != _loggingService)
                {
                    _loggingContext.LoggingService.OnLoggingThreadException -= new LoggingExceptionDelegate(OnLoggingThreadException);
                    _loggingContext = null;
                }

                exception = _shutdownException;

                if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
                {
                    // Notify the BuildManager that we are done.
                    _nodeEndpoint.SendData(new NodeShutdown(_shutdownReason == NodeEngineShutdownReason.Error ? NodeShutdownReason.Error : NodeShutdownReason.Requested, exception));

                    // Flush all packets to the pipe and close it down.  This blocks until the shutdown is complete.
                    _nodeEndpoint.OnLinkStatusChanged -= new LinkStatusChangedDelegate(OnLinkStatusChanged);
                }

                _nodeEndpoint.Disconnect();
                CleanupCaches();
            }

            return _shutdownReason;
        }

        /// <summary>
        /// Clears all the caches used during the build.
        /// </summary>
        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.GC.Collect", Justification = "Required because when calling this method, we want the memory back NOW.")]
        private void CleanupCaches()
        {
            IConfigCache configCache = _componentFactories.GetComponent(BuildComponentType.ConfigCache) as IConfigCache;
            if (null != configCache)
            {
                configCache.ClearConfigurations();
            }

            IResultsCache resultsCache = _componentFactories.GetComponent(BuildComponentType.ResultsCache) as IResultsCache;
            if (null != resultsCache)
            {
                resultsCache.ClearResults();
            }

            if (Environment.GetEnvironmentVariable("MSBUILDCLEARXMLCACHEONCHILDNODES") == "1")
            {
                // Optionally clear out the cache. This has the advantage of releasing memory,
                // but the disadvantage of causing the next build to repeat the load and parse.
                // We'll experiment here and ship with the best default.
                s_projectRootElementCache = null;
            }

            // Since we aren't going to be doing any more work, lets clean up all our memory usage.
            GC.Collect();
        }

        /// <summary>
        /// Event handler for the node endpoint's LinkStatusChanged event.
        /// </summary>
        private void OnLinkStatusChanged(INodeEndpoint endpoint, LinkStatus status)
        {
            switch (status)
            {
                case LinkStatus.ConnectionFailed:
                case LinkStatus.Failed:
                    _shutdownReason = NodeEngineShutdownReason.ConnectionFailed;
                    _shutdownEvent.Set();
                    break;

                case LinkStatus.Inactive:
                    break;

                default:
                    break;
            }
        }

        /// <summary>
        /// Callback for logging packets to be sent.
        /// </summary>
        private void SendLoggingPacket(INodePacket packet)
        {
            if (_nodeEndpoint.LinkStatus == LinkStatus.Active)
            {
                _nodeEndpoint.SendData(packet);
            }
        }

        /// <summary>
        /// Dispatches the packet to the correct handler.
        /// </summary>
        private void HandlePacket(INodePacket packet)
        {
            // Console.WriteLine("Handling packet {0} at {1}", packet.Type, DateTime.Now);
            switch (packet.Type)
            {
                case NodePacketType.BuildRequest:
                    HandleBuildRequest(packet as BuildRequest);
                    break;

                case NodePacketType.BuildRequestConfiguration:
                    HandleBuildRequestConfiguration(packet as BuildRequestConfiguration);
                    break;

                case NodePacketType.BuildRequestConfigurationResponse:
                    HandleBuildRequestConfigurationResponse(packet as BuildRequestConfigurationResponse);
                    break;

                case NodePacketType.BuildRequestUnblocker:
                    HandleBuildRequestUnblocker(packet as BuildRequestUnblocker);
                    break;

                case NodePacketType.NodeConfiguration:
                    HandleNodeConfiguration(packet as NodeConfiguration);
                    break;

                case NodePacketType.NodeBuildComplete:
                    HandleNodeBuildComplete(packet as NodeBuildComplete);
                    break;
            }
        }

        /// <summary>
        /// Handles the BuildRequest packet.
        /// </summary>
        private void HandleBuildRequest(BuildRequest request)
        {
            _buildRequestEngine.SubmitBuildRequest(request);
        }

        /// <summary>
        /// Handles the BuildRequestConfiguration packet.
        /// </summary>
        private void HandleBuildRequestConfiguration(BuildRequestConfiguration configuration)
        {
            _globalConfigCache.AddConfiguration(configuration);
        }

        /// <summary>
        /// Handles the BuildRequestConfigurationResponse packet.
        /// </summary>
        private void HandleBuildRequestConfigurationResponse(BuildRequestConfigurationResponse response)
        {
            _buildRequestEngine.ReportConfigurationResponse(response);
        }

        /// <summary>
        /// Handles the BuildResult packet.
        /// </summary>
        private void HandleBuildRequestUnblocker(BuildRequestUnblocker unblocker)
        {
            _buildRequestEngine.UnblockBuildRequest(unblocker);
        }

        /// <summary>
        /// Handles the NodeConfiguration packet.
        /// </summary>
        private void HandleNodeConfiguration(NodeConfiguration configuration)
        {
            // Grab the system parameters.
            _buildParameters = configuration.BuildParameters;

            _buildParameters.ProjectRootElementCache = s_projectRootElementCache;

            // Snapshot the current environment
            _savedEnvironment = CommunicationsUtilities.GetEnvironmentVariables();

            // Change to the startup directory
            try
            {
                NativeMethodsShared.SetCurrentDirectory(BuildParameters.StartupDirectory);
            }
            catch (DirectoryNotFoundException)
            {
                // Somehow the startup directory vanished. This can happen if build was started from a USB Key and it was removed.
                NativeMethodsShared.SetCurrentDirectory(Environment.SystemDirectory);
            }

            // Replicate the environment.  First, unset any environment variables set by the previous configuration.
            if (_currentConfiguration != null)
            {
                foreach (string key in _currentConfiguration.BuildParameters.BuildProcessEnvironment.Keys)
                {
                    Environment.SetEnvironmentVariable(key, null);
                }
            }

            // Now set the new environment
            foreach (KeyValuePair<string, string> environmentPair in _buildParameters.BuildProcessEnvironment)
            {
                Environment.SetEnvironmentVariable(environmentPair.Key, environmentPair.Value);
            }

            // We want to make sure the global project collection has the toolsets which were defined on the parent
            // so that any custom toolsets defined can be picked up by tasks who may use the global project collection but are 
            // executed on the child node.
            ICollection<Toolset> parentToolSets = _buildParameters.ToolsetProvider.Toolsets;
            if (parentToolSets != null)
            {
                ProjectCollection.GlobalProjectCollection.RemoveAllToolsets();

                foreach (Toolset toolSet in parentToolSets)
                {
                    ProjectCollection.GlobalProjectCollection.AddToolset(toolSet);
                }
            }

            // Set the culture.
            Thread.CurrentThread.CurrentCulture = _buildParameters.Culture;
            Thread.CurrentThread.CurrentUICulture = _buildParameters.UICulture;

            // Get the node ID.
            _buildParameters.NodeId = configuration.NodeId;
            _buildParameters.IsOutOfProc = true;

            // And the AppDomainSetup
            _buildParameters.AppDomainSetup = configuration.AppDomainSetup;

            // Set up the logging service.
            LoggingServiceFactory loggingServiceFactory = new LoggingServiceFactory(LoggerMode.Asynchronous, configuration.NodeId);
            _componentFactories.ReplaceFactory(BuildComponentType.LoggingService, loggingServiceFactory.CreateInstance);

            _loggingService = _componentFactories.GetComponent(BuildComponentType.LoggingService) as ILoggingService;

            BuildEventArgTransportSink sink = new BuildEventArgTransportSink(SendLoggingPacket);

            _shutdownException = null;

            try
            {
                // If there are no node loggers to initialize dont do anything
                if (configuration.LoggerDescriptions != null && configuration.LoggerDescriptions.Length > 0)
                {
                    _loggingService.InitializeNodeLoggers(configuration.LoggerDescriptions, sink, configuration.NodeId);
                }
            }
            catch (Exception ex)
            {
                if (ExceptionHandling.IsCriticalException(ex))
                {
                    throw;
                }

                OnEngineException(ex);
            }

            _loggingService.OnLoggingThreadException += new LoggingExceptionDelegate(OnLoggingThreadException);

            string forwardPropertiesFromChild = Environment.GetEnvironmentVariable("MSBUILDFORWARDPROPERTIESFROMCHILD");
            string[] propertyListToSerialize = null;

            // Get a list of properties which should be serialized
            if (!String.IsNullOrEmpty(forwardPropertiesFromChild))
            {
                propertyListToSerialize = forwardPropertiesFromChild.Split(new char[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            }

            _loggingService.PropertiesToSerialize = propertyListToSerialize;
            _loggingService.RunningOnRemoteNode = true;

            string forwardAllProperties = Environment.GetEnvironmentVariable("MSBUILDFORWARDALLPROPERTIESFROMCHILD");
            if (String.Equals(forwardAllProperties, "1", StringComparison.OrdinalIgnoreCase) || _buildParameters.LogInitialPropertiesAndItems)
            {
                _loggingService.SerializeAllProperties = true;
            }
            else
            {
                _loggingService.SerializeAllProperties = false;
            }

            // Now prep the buildRequestEngine for the build.
            _loggingContext = new NodeLoggingContext(_loggingService, configuration.NodeId, false /* inProcNode */);

            if (_shutdownException != null)
            {
                Exception exception;
                HandleShutdown(out exception);
                throw exception;
            }

            _buildRequestEngine.InitializeForBuild(_loggingContext);

            // Finally store off this configuration packet.
            _currentConfiguration = configuration;
        }

        /// <summary>
        /// Handles the NodeBuildComplete packet.
        /// </summary>
        private void HandleNodeBuildComplete(NodeBuildComplete buildComplete)
        {
            _shutdownReason = buildComplete.PrepareForReuse ? NodeEngineShutdownReason.BuildCompleteReuse : NodeEngineShutdownReason.BuildComplete;
            _shutdownEvent.Set();
        }
    }
}
