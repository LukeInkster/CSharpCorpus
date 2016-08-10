﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Class implementing INodeProvider for out-of-proc nodes.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Permissions;

using Microsoft.Build.Shared;
using Microsoft.Build.Framework;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Internal;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// The provider for out-of-proc nodes.  This manages the lifetime of external MSBuild.exe processes
    /// which act as child nodes for the build system.
    /// </summary>
    internal class NodeProviderOutOfProc : NodeProviderOutOfProcBase, INodeProvider
    {
        /// <summary>
        /// A mapping of all the nodes managed by this provider.
        /// </summary>
        private Dictionary<int, NodeContext> _nodeContexts;

        /// <summary>
        /// Constructor.
        /// </summary>
        private NodeProviderOutOfProc()
        {
        }

        #region INodeProvider Members

        /// <summary>
        /// Returns the node provider type.
        /// </summary>
        public NodeProviderType ProviderType
        {
            [DebuggerStepThrough]
            get
            { return NodeProviderType.OutOfProc; }
        }

        /// <summary>
        /// Returns the number of available nodes.
        /// </summary>
        public int AvailableNodes
        {
            get
            {
                return ComponentHost.BuildParameters.MaxNodeCount - _nodeContexts.Count;
            }
        }

        /// <summary>
        /// Magic number sent by the host to the client during the handshake.
        /// Derived from the binary timestamp to avoid mixing binary versions,
        /// Is64BitProcess to avoid mixing bitness, and enableNodeReuse to
        /// ensure that a /nr:false build doesn't reuse clients left over from
        /// a prior /nr:true build.
        /// </summary>
        /// <param name="enableNodeReuse">Is reuse of build nodes allowed?</param>
        internal static long GetHostHandshake(bool enableNodeReuse)
        {
            long baseHandshake = Constants.AssemblyTimestamp;

            baseHandshake = baseHandshake*17 + Environment.Is64BitProcess.GetHashCode();

            baseHandshake = baseHandshake*17 + enableNodeReuse.GetHashCode();

            return CommunicationsUtilities.GenerateHostHandshakeFromBase(baseHandshake, GetClientHandshake());
        }

        /// <summary>
        /// Magic number sent by the client to the host during the handshake.
        /// Munged version of the host handshake.
        /// </summary>
        internal static long GetClientHandshake()
        {
            // Mask out the first byte. That's because old
            // builds used a single, non zero initial byte,
            // and we don't want to risk communicating with them
            return (Constants.AssemblyTimestamp ^ Int64.MaxValue) & 0x00FFFFFFFFFFFFFF;
        }

        /// <summary>
        /// Instantiates a new MSBuild process acting as a child node.
        /// </summary>
        public bool CreateNode(int nodeId, INodePacketFactory factory, NodeConfiguration configuration)
        {
            ErrorUtilities.VerifyThrowArgumentNull(factory, "factory");

            if (_nodeContexts.Count == ComponentHost.BuildParameters.MaxNodeCount)
            {
                ErrorUtilities.ThrowInternalError("All allowable nodes already created ({0}).", _nodeContexts.Count);
                return false;
            }

            // Start the new process.  We pass in a node mode with a node number of 1, to indicate that we 
            // want to start up just a standard MSBuild out-of-proc node.
            // Note: We need to always pass /nodeReuse to ensure the value for /nodeReuse from msbuild.rsp
            // (next to msbuild.exe) is ignored.
            string commandLineArgs = ComponentHost.BuildParameters.EnableNodeReuse ? 
                "/nologo /nodemode:1 /nodeReuse:true" : 
                "/nologo /nodemode:1 /nodeReuse:false";

            // Make it here.
            CommunicationsUtilities.Trace("Starting to acquire a new or existing node to establish node ID {0}...", nodeId);
            NodeContext context = GetNode(null, commandLineArgs, nodeId, factory, NodeProviderOutOfProc.GetHostHandshake(ComponentHost.BuildParameters.EnableNodeReuse), NodeProviderOutOfProc.GetClientHandshake(), NodeContextTerminated);

            if (null != context)
            {
                _nodeContexts[nodeId] = context;

                // Start the asynchronous read.
                context.BeginAsyncPacketRead();

                // Configure the node.
                context.SendData(configuration);

                return true;
            }

            throw new BuildAbortedException(ResourceUtilities.FormatResourceString("CouldNotConnectToMSBuildExe", ComponentHost.BuildParameters.NodeExeLocation));
        }

        /// <summary>
        /// Sends data to the specified node.
        /// </summary>
        /// <param name="nodeId">The node to which data shall be sent.</param>
        /// <param name="packet">The packet to send.</param>
        public void SendData(int nodeId, INodePacket packet)
        {
            ErrorUtilities.VerifyThrow(_nodeContexts.ContainsKey(nodeId), "Invalid node id specified: {0}.", nodeId);

            SendData(_nodeContexts[nodeId], packet);
        }

        /// <summary>
        /// Shuts down all of the connected managed nodes.
        /// </summary>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        public void ShutdownConnectedNodes(bool enableReuse)
        {
            // Send the build completion message to the nodes, causing them to shutdown or reset.
            List<NodeContext> contextsToShutDown;

            lock (_nodeContexts)
            {
                contextsToShutDown = new List<NodeContext>(_nodeContexts.Values);
            }

            ShutdownConnectedNodes(contextsToShutDown, enableReuse);
        }

        /// <summary>
        /// Shuts down all of the managed nodes permanently.
        /// </summary>
        public void ShutdownAllNodes()
        {
            ShutdownAllNodes(NodeProviderOutOfProc.GetHostHandshake(ComponentHost.BuildParameters.EnableNodeReuse), NodeProviderOutOfProc.GetClientHandshake(), NodeContextTerminated);
        }

        #endregion

        #region IBuildComponent Members

        /// <summary>
        /// Initializes the component.
        /// </summary>
        /// <param name="host">The component host.</param>
        public void InitializeComponent(IBuildComponentHost host)
        {
            this.ComponentHost = host;
            _nodeContexts = new Dictionary<int, NodeContext>();
        }

        /// <summary>
        /// Shuts down the component
        /// </summary>
        public void ShutdownComponent()
        {
        }

        #endregion

        /// <summary>
        /// Static factory for component creation.
        /// </summary>
        static internal IBuildComponent CreateComponent(BuildComponentType componentType)
        {
            ErrorUtilities.VerifyThrow(componentType == BuildComponentType.OutOfProcNodeProvider, "Factory cannot create components of type {0}", componentType);
            return new NodeProviderOutOfProc();
        }

        /// <summary>
        /// Method called when a context terminates.
        /// </summary>
        private void NodeContextTerminated(int nodeId)
        {
            lock (_nodeContexts)
            {
                _nodeContexts.Remove(nodeId);
            }
        }
    }
}
