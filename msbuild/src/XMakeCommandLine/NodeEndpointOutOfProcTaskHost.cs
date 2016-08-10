﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Implementation of a node endpoint for out-of-proc nodes.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO.Pipes;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Internal;

namespace Microsoft.Build.CommandLine
{
    /// <summary>
    /// This is an implementation of INodeEndpoint for the out-of-proc nodes.  It acts only as a client.
    /// </summary>
    internal class NodeEndpointOutOfProcTaskHost : NodeEndpointOutOfProcBase
    {
        #region Constructors and Factories

        /// <summary>
        /// Instantiates an endpoint to act as a client
        /// </summary>
        /// <param name="pipeName">The name of the pipe to which we should connect.</param>
        internal NodeEndpointOutOfProcTaskHost(string pipeName)
        {
            InternalConstruct(pipeName);
        }

        #endregion // Constructors and Factories

        /// <summary>
        /// Returns the host handshake for this node endpoint
        /// </summary>
        protected override long GetHostHandshake()
        {
            long hostHandshake = CommunicationsUtilities.GetTaskHostHostHandshake(CommunicationsUtilities.GetCurrentTaskHostContext());
            return hostHandshake;
        }

        /// <summary>
        /// Returns the client handshake for this node endpoint
        /// </summary>
        protected override long GetClientHandshake()
        {
            long clientHandshake = CommunicationsUtilities.GetTaskHostClientHandshake(CommunicationsUtilities.GetCurrentTaskHostContext());
            return clientHandshake;
        }
    }
}
