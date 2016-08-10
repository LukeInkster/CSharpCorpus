﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Base class for the implementation of a node endpoint for out-of-proc nodes.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Internal;
using Microsoft.Build.Shared;
using System.Security;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Security.Permissions;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// This is an implementation of INodeEndpoint for the out-of-proc nodes.  It acts only as a client.
    /// </summary>
    internal abstract class NodeEndpointOutOfProcBase : INodeEndpoint
    {
        #region Private Data

        /// <summary>
        /// The amount of time to wait for the client to connect to the host.
        /// </summary>
        private const int ClientConnectTimeout = 60000;

        /// <summary>
        /// The size of the buffers to use for named pipes
        /// </summary>
        private const int PipeBufferSize = 131072;

        /// <summary>
        /// Flag indicating if we should debug communications or not.
        /// </summary>
        private bool _debugCommunications = false;

        /// <summary>
        /// The current communication status of the node.
        /// </summary>
        private LinkStatus _status;

        /// <summary>
        /// The pipe client used by the nodes.
        /// </summary>
        private NamedPipeServerStream _pipeServer;

        // The following private data fields are used only when the endpoint is in ASYNCHRONOUS mode.

        /// <summary>
        /// Object used as a lock source for the async data
        /// </summary>
        private object _asyncDataMonitor;

        /// <summary>
        /// Set when a packet is available in the packet queue
        /// </summary>      
        private AutoResetEvent _packetAvailable;

        /// <summary>
        /// Set when the asynchronous packet pump should terminate
        /// </summary>
        private AutoResetEvent _terminatePacketPump;

        /// <summary>
        /// The thread which runs the asynchronous packet pump
        /// </summary>
        private Thread _packetPump;

        /// <summary>
        /// The factory used to create and route packets.
        /// </summary>
        private INodePacketFactory _packetFactory;

        /// <summary>
        /// The asynchronous packet queue.  
        /// </summary>
        /// <remarks>
        /// Operations on this queue must be synchronized since it is accessible by multiple threads.
        /// Use a lock on the packetQueue itself.
        /// </remarks>
        private Queue<INodePacket> _packetQueue;

        /// <summary>
        /// Per-node shared read buffer.
        /// </summary>
        private SharedReadBuffer _sharedReadBuffer;

        #endregion

        #region INodeEndpoint Events

        /// <summary>
        /// Raised when the link status has changed.
        /// </summary>
        public event LinkStatusChangedDelegate OnLinkStatusChanged;

        #endregion

        #region INodeEndpoint Properties

        /// <summary>
        /// Returns the link status of this node.
        /// </summary>
        public LinkStatus LinkStatus
        {
            get { return _status; }
        }

        #endregion

        #region Properties

        #endregion

        #region INodeEndpoint Methods

        /// <summary>
        /// Causes this endpoint to wait for the remote endpoint to connect
        /// </summary>
        /// <param name="factory">The factory used to create packets.</param>
        public void Listen(INodePacketFactory factory)
        {
            ErrorUtilities.VerifyThrow(_status == LinkStatus.Inactive, "Link not inactive.  Status is {0}", _status);
            ErrorUtilities.VerifyThrowArgumentNull(factory, "factory");
            _packetFactory = factory;

            InitializeAsyncPacketThread();
        }

        /// <summary>
        /// Causes this node to connect to the matched endpoint.  
        /// </summary>
        /// <param name="factory">The factory used to create packets.</param>
        public void Connect(INodePacketFactory factory)
        {
            ErrorUtilities.ThrowInternalError("Connect() not valid on the out of proc endpoint.");
        }

        /// <summary>
        /// Shuts down the link
        /// </summary>
        public void Disconnect()
        {
            InternalDisconnect();
        }

        /// <summary>
        /// Sends data to the peer endpoint.
        /// </summary>
        /// <param name="packet">The packet to send.</param>
        public void SendData(INodePacket packet)
        {
            // PERF: Set up a priority system so logging packets are sent only when all other packet types have been sent.
            if (_status == LinkStatus.Active)
            {
                EnqueuePacket(packet);
            }
        }

        #endregion

        #region Construction

        /// <summary>
        /// Instantiates an endpoint to act as a client
        /// </summary>
        /// <param name="pipeName">The name of the pipe to which we should connect.</param>
        internal void InternalConstruct(string pipeName)
        {
            ErrorUtilities.VerifyThrowArgumentLength(pipeName, "pipeName");

            _debugCommunications = (Environment.GetEnvironmentVariable("MSBUILDDEBUGCOMM") == "1");

            _status = LinkStatus.Inactive;
            _asyncDataMonitor = new object();
            _sharedReadBuffer = InterningBinaryReader.CreateSharedBuffer();

            SecurityIdentifier identifier = WindowsIdentity.GetCurrent().Owner;
            PipeSecurity security = new PipeSecurity();

            // Restrict access to just this account.  We set the owner specifically here, and on the
            // pipe client side they will check the owner against this one - they must have identical
            // SIDs or the client will reject this server.  This is used to avoid attacks where a
            // hacked server creates a less restricted pipe in an attempt to lure us into using it and 
            // then sending build requests to the real pipe client (which is the MSBuild Build Manager.)
            PipeAccessRule rule = new PipeAccessRule(identifier, PipeAccessRights.ReadWrite, AccessControlType.Allow);
            security.AddAccessRule(rule);
            security.SetOwner(identifier);

            _pipeServer = new NamedPipeServerStream
                (
                pipeName,
                PipeDirection.InOut,
                1, // Only allow one connection at a time.
                PipeTransmissionMode.Byte,
                PipeOptions.Asynchronous | PipeOptions.WriteThrough,
                PipeBufferSize, // Default input buffer
                PipeBufferSize, // Default output buffer
                security,
                HandleInheritability.None
                );
        }

        #endregion

        /// <summary>
        /// Returns the host handshake for this node endpoint
        /// </summary>
        protected abstract long GetHostHandshake();

        /// <summary>
        /// Returns the client handshake for this node endpoint
        /// </summary>
        protected abstract long GetClientHandshake();

        /// <summary>
        /// Updates the current link status if it has changed and notifies any registered delegates.
        /// </summary>
        /// <param name="newStatus">The status the node should now be in.</param>
        protected void ChangeLinkStatus(LinkStatus newStatus)
        {
            ErrorUtilities.VerifyThrow(_status != newStatus, "Attempting to change status to existing status {0}.", _status);
            CommunicationsUtilities.Trace("Changing link status from {0} to {1}", _status.ToString(), newStatus.ToString());
            _status = newStatus;
            RaiseLinkStatusChanged(_status);
        }

        /// <summary>
        /// Invokes the OnLinkStatusChanged event in a thread-safe manner.
        /// </summary>
        /// <param name="newStatus">The new status of the endpoint link.</param>
        private void RaiseLinkStatusChanged(LinkStatus newStatus)
        {
            if (null != OnLinkStatusChanged)
            {
                LinkStatusChangedDelegate linkStatusDelegate = (LinkStatusChangedDelegate)OnLinkStatusChanged.Clone();
                linkStatusDelegate(this, newStatus);
            }
        }

        #region Private Methods

        /// <summary>
        /// This does the actual work of changing the status and shutting down any threads we may have for
        /// disconnection.
        /// </summary>
        private void InternalDisconnect()
        {
            ErrorUtilities.VerifyThrow(_packetPump.ManagedThreadId != Thread.CurrentThread.ManagedThreadId, "Can't join on the same thread.");
            _terminatePacketPump.Set();
            _packetPump.Join();
            _terminatePacketPump.Close();
            _pipeServer.Close();
            _packetPump = null;
            ChangeLinkStatus(LinkStatus.Inactive);
        }

        #region Asynchronous Mode Methods

        /// <summary>
        /// Adds a packet to the packet queue when asynchronous mode is enabled.
        /// </summary>
        /// <param name="packet">The packet to be transmitted.</param>
        private void EnqueuePacket(INodePacket packet)
        {
            ErrorUtilities.VerifyThrowArgumentNull(packet, "packet");
            ErrorUtilities.VerifyThrow(null != _packetQueue, "packetQueue is null");
            ErrorUtilities.VerifyThrow(null != _packetAvailable, "packetAvailable is null");

            lock (_packetQueue)
            {
                _packetQueue.Enqueue(packet);
                _packetAvailable.Set();
            }
        }

        /// <summary>
        /// Initializes the packet pump thread and the supporting events as well as the packet queue.
        /// </summary>
        private void InitializeAsyncPacketThread()
        {
            lock (_asyncDataMonitor)
            {
                _packetPump = new Thread(PacketPumpProc);
                _packetPump.IsBackground = true;
                _packetPump.Name = "OutOfProc Endpoint Packet Pump";
                _packetAvailable = new AutoResetEvent(false);
                _terminatePacketPump = new AutoResetEvent(false);
                _packetQueue = new Queue<INodePacket>();
                _packetPump.Start();
            }
        }

        /// <summary>
        /// This method handles the asynchronous message pump.  It waits for messages to show up on the queue
        /// and calls FireDataAvailable for each such packet.  It will terminate when the terminate event is
        /// set.
        /// </summary>
        private void PacketPumpProc()
        {
            NamedPipeServerStream localPipeServer = _pipeServer;
            AutoResetEvent localPacketAvailable = _packetAvailable;
            AutoResetEvent localTerminatePacketPump = _terminatePacketPump;
            Queue<INodePacket> localPacketQueue = _packetQueue;

            DateTime originalWaitStartTime = DateTime.UtcNow;
            bool gotValidConnection = false;
            while (!gotValidConnection)
            {
                DateTime restartWaitTime = DateTime.UtcNow;

                // We only wait to wait the difference between now and the last original start time, in case we have multiple hosts attempting
                // to attach.  This prevents each attempt from resetting the timer.
                TimeSpan usedWaitTime = restartWaitTime - originalWaitStartTime;
                int waitTimeRemaining = Math.Max(0, CommunicationsUtilities.NodeConnectionTimeout - (int)usedWaitTime.TotalMilliseconds);

                try
                {
                    // Wait for a connection
                    IAsyncResult resultForConnection = localPipeServer.BeginWaitForConnection(null, null);
                    CommunicationsUtilities.Trace("Waiting for connection {0} ms...", waitTimeRemaining);

                    bool connected = resultForConnection.AsyncWaitHandle.WaitOne(waitTimeRemaining, false);
                    if (!connected)
                    {
                        CommunicationsUtilities.Trace("Connection timed out waiting a host to contact us.  Exiting comm thread.");
                        ChangeLinkStatus(LinkStatus.ConnectionFailed);
                        return;
                    }

                    CommunicationsUtilities.Trace("Parent started connecting. Reading handshake from parent");
                    localPipeServer.EndWaitForConnection(resultForConnection);

                    // The handshake protocol is a simple long exchange.  The host sends us a long, and we
                    // respond with another long.  Once the handshake is complete, both sides can be assured the
                    // other is ready to accept data.
                    // To avoid mixing client and server builds, the long is the MSBuild binary timestamp.

                    // Compatibility issue here.
                    // Previous builds of MSBuild 4.0 would exchange just a byte.
                    // Host would send either 0x5F or 0x60 depending on whether it was the toolset or not respectively.
                    // Client would return either 0xF5 or 0x06 respectively.
                    // Therefore an old host on a machine with new clients running will hang, 
                    // sending a byte and waiting for a byte until it eventually times out;
                    // because the new client will want 7 more bytes before it returns anything.
                    // The other way around is not a problem, because the old client would immediately return the (wrong)
                    // byte on receiving the first byte of the long sent by the new host, and the new host would disconnect.
                    // To avoid the hang, special case here:
                    // Make sure our handshakes always start with 00.
                    // If we received ONLY one byte AND it's 0x5F or 0x60, return 0xFF (it doesn't matter what as long as
                    // it will cause the host to reject us; new hosts expect 00 and old hosts expect F5 or 06).
                    try
                    {
                        long handshake = localPipeServer.ReadLongForHandshake(/* reject these leads */ new byte[] { 0x5F, 0x60 }, 0xFF /* this will disconnect the host; it expects leading 00 or F5 or 06 */);
                        WindowsIdentity currentIdentity = WindowsIdentity.GetCurrent();
                        string remoteUserName = localPipeServer.GetImpersonationUserName();

                        if (handshake != GetHostHandshake())
                        {
                            CommunicationsUtilities.Trace("Handshake failed. Received {0} from host not {1}. Probably the host is a different MSBuild build.", handshake, GetHostHandshake());
                            localPipeServer.Disconnect();
                            continue;
                        }

                        // We will only talk to a host that was started by the same user as us.  Even though the pipe access is set to only allow this user, we want to ensure they
                        // haven't attempted to change those permissions out from under us.  This ensures that the only way they can truly gain access is to be impersonating the
                        // user we were started by.
                        WindowsIdentity clientIdentity = null;
                        localPipeServer.RunAsClient(delegate () { clientIdentity = WindowsIdentity.GetCurrent(true); });

                        if (clientIdentity == null || !String.Equals(clientIdentity.Name, currentIdentity.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            CommunicationsUtilities.Trace("Handshake failed. Host user is {0} but we were created by {1}.", (clientIdentity == null) ? "<unknown>" : clientIdentity.Name, currentIdentity.Name);
                            localPipeServer.Disconnect();
                            continue;
                        }
                    }
                    catch (IOException e)
                    {
                        // We will get here when:
                        // 1. The host (OOP main node) connects to us, it immediately checks for user privileges
                        //    and if they don't match it disconnects immediately leaving us still trying to read the blank handshake
                        // 2. The host is too old sending us bits we automatically reject in the handshake
                        CommunicationsUtilities.Trace("Client connection failed but we will wait for another connection. Exception: {0}", e.Message);
                        if (localPipeServer.IsConnected)
                        {
                            localPipeServer.Disconnect();
                        }

                        continue;
                    }

                    gotValidConnection = true;
                }
                catch (Exception e)
                {
                    if (ExceptionHandling.IsCriticalException(e))
                    {
                        throw;
                    }

                    CommunicationsUtilities.Trace("Client connection failed.  Exiting comm thread. {0}", e);
                    if (localPipeServer.IsConnected)
                    {
                        localPipeServer.Disconnect();
                    }

                    ExceptionHandling.DumpExceptionToFile(e);
                    ChangeLinkStatus(LinkStatus.Failed);
                    return;
                }
            }

            CommunicationsUtilities.Trace("Writing handshake to parent");
            localPipeServer.WriteLongForHandshake(GetClientHandshake());
            ChangeLinkStatus(LinkStatus.Active);

            // Ordering of the wait handles is important.  The first signalled wait handle in the array 
            // will be returned by WaitAny if multiple wait handles are signalled.  We prefer to have the
            // terminate event triggered so that we cannot get into a situation where packets are being
            // spammed to the endpoint and it never gets an opportunity to shutdown.
            CommunicationsUtilities.Trace("Entering read loop.");
            byte[] headerByte = new byte[5];
            IAsyncResult result = localPipeServer.BeginRead(headerByte, 0, headerByte.Length, null, null);

            bool exitLoop = false;
            do
            {
                // Ordering is important.  We want packetAvailable to supercede terminate otherwise we will not properly wait for all
                // packets to be sent by other threads which are shutting down, such as the logging thread.
                WaitHandle[] handles = new WaitHandle[] { result.AsyncWaitHandle, localPacketAvailable, localTerminatePacketPump };

                int waitId = WaitHandle.WaitAny(handles);
                switch (waitId)
                {
                    case 0:
                        {
                            int bytesRead = 0;
                            try
                            {
                                bytesRead = localPipeServer.EndRead(result);
                            }
                            catch (Exception e)
                            {
                                // Lost communications.  Abort (but allow node reuse)
                                CommunicationsUtilities.Trace("Exception reading from server.  {0}", e);
                                ExceptionHandling.DumpExceptionToFile(e);
                                ChangeLinkStatus(LinkStatus.Inactive);
                                exitLoop = true;
                                break;
                            }

                            if (bytesRead != headerByte.Length)
                            {
                                // Incomplete read.  Abort.
                                if (bytesRead == 0)
                                {
                                    CommunicationsUtilities.Trace("Parent disconnected abruptly");
                                }
                                else
                                {
                                    CommunicationsUtilities.Trace("Incomplete header read from server.  {0} of {1} bytes read", bytesRead, headerByte.Length);
                                }

                                ChangeLinkStatus(LinkStatus.Failed);
                                exitLoop = true;
                                break;
                            }

                            NodePacketType packetType = (NodePacketType)Enum.ToObject(typeof(NodePacketType), headerByte[0]);
                            int packetLength = BitConverter.ToInt32(headerByte, 1);

                            try
                            {
                                _packetFactory.DeserializeAndRoutePacket(0, packetType, NodePacketTranslator.GetReadTranslator(localPipeServer, _sharedReadBuffer));
                            }
                            catch (Exception e)
                            {
                                // Error while deserializing or handling packet.  Abort.
                                CommunicationsUtilities.Trace("Exception while deserializing packet {0}: {1}", packetType, e);
                                ExceptionHandling.DumpExceptionToFile(e);
                                ChangeLinkStatus(LinkStatus.Failed);
                                exitLoop = true;
                                break;
                            }

                            result = localPipeServer.BeginRead(headerByte, 0, headerByte.Length, null, null);
                        }

                        break;

                    case 1:
                    case 2:
                        try
                        {
                            int packetCount = localPacketQueue.Count;

                            // Write out all the queued packets.
                            while (packetCount > 0)
                            {
                                INodePacket packet;
                                lock (_packetQueue)
                                {
                                    packet = localPacketQueue.Dequeue();
                                }

                                MemoryStream packetStream = new MemoryStream();
                                INodePacketTranslator writeTranslator = NodePacketTranslator.GetWriteTranslator(packetStream);

                                packetStream.WriteByte((byte)packet.Type);

                                // Pad for packet length
                                packetStream.Write(BitConverter.GetBytes((int)0), 0, 4);

                                // Reset the position in the write buffer.
                                packet.Translate(writeTranslator);

                                // Now write in the actual packet length
                                packetStream.Position = 1;
                                packetStream.Write(BitConverter.GetBytes((int)packetStream.Length - 5), 0, 4);

                                localPipeServer.Write(packetStream.GetBuffer(), 0, (int)packetStream.Length);

                                packetCount--;
                            }
                        }
                        catch (Exception e)
                        {
                            // Error while deserializing or handling packet.  Abort.
                            CommunicationsUtilities.Trace("Exception while serializing packets: {0}", e);
                            ExceptionHandling.DumpExceptionToFile(e);
                            ChangeLinkStatus(LinkStatus.Failed);
                            exitLoop = true;
                            break;
                        }

                        if (waitId == 2)
                        {
                            CommunicationsUtilities.Trace("Disconnecting voluntarily");
                            ChangeLinkStatus(LinkStatus.Failed);
                            exitLoop = true;
                        }

                        break;

                    default:
                        ErrorUtilities.ThrowInternalError("waitId {0} out of range.", waitId);
                        break;
                }
            }
            while (!exitLoop);

            CommunicationsUtilities.Trace("Ending read loop");

            try
            {
                if (localPipeServer.IsConnected)
                {
                    localPipeServer.WaitForPipeDrain();
                    localPipeServer.Disconnect();
                }
            }
            catch (Exception)
            {
                // We don't really care if Disconnect somehow fails, but it gives us a chance to do the right thing.
            }
        }

        #endregion

        #endregion
    }
}
