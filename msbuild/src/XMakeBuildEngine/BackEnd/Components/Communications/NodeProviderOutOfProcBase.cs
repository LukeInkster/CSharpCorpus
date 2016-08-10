﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Class containing the shared pieces of code from NodeProviderOutOfProc
// and NodeProviderOutOfProcTaskHost.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using System.Security.Principal;

using Microsoft.Build.Shared;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Internal;

using BackendNativeMethods = Microsoft.Build.BackEnd.NativeMethods;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Contains the shared pieces of code from NodeProviderOutOfProc
    /// and NodeProviderOutOfProcTaskHost.
    /// </summary>
    internal abstract class NodeProviderOutOfProcBase
    {
        /// <summary>
        /// The maximum number of bytes to write 
        /// </summary>
        private const int MaxPacketWriteSize = 1048576;

        /// <summary>
        /// The number of times to retry creating an out-of-proc node.
        /// </summary>
        private const int NodeCreationRetries = 10;

        /// <summary>
        /// The amount of time to wait for an out-of-proc node to spool up before we give up.
        /// </summary>
        private const int TimeoutForNewNodeCreation = 30000;

        /// <summary>
        /// The build component host.
        /// </summary>
        private IBuildComponentHost _componentHost;

        /// <summary>
        /// Keeps track of the processes we've already checked for nodes so we don't check them again.
        /// </summary>
        private HashSet<string> _processesToIgnore = new HashSet<string>();

        /// <summary>
        /// Delegate used to tell the node provider that a context has terminated.
        /// </summary>
        /// <param name="nodeId">The id of the node which terminated.</param>
        internal delegate void NodeContextTerminateDelegate(int nodeId);

        /// <summary>
        /// The build component host. 
        /// </summary>
        protected IBuildComponentHost ComponentHost
        {
            get { return _componentHost; }
            set { _componentHost = value; }
        }

        /// <summary>
        /// Sends data to the specified node.
        /// </summary>
        /// <param name="context">The node to which data shall be sent.</param>
        /// <param name="packet">The packet to send.</param>
        protected void SendData(NodeContext context, INodePacket packet)
        {
            ErrorUtilities.VerifyThrowArgumentNull(packet, "packet");
            context.SendData(packet);
        }

        /// <summary>
        /// Shuts down all of the connected managed nodes.
        /// </summary>
        /// <param name="contextsToShutDown">List of the contexts to be shut down</param>
        /// <param name="enableReuse">Flag indicating if nodes should prepare for reuse.</param>
        protected void ShutdownConnectedNodes(List<NodeContext> contextsToShutDown, bool enableReuse)
        {
            // Send the build completion message to the nodes, causing them to shutdown or reset.
            _processesToIgnore.Clear();

            foreach (NodeContext nodeContext in contextsToShutDown)
            {
                if (null != nodeContext)
                {
                    nodeContext.SendData(new NodeBuildComplete(enableReuse));
                }
            }
        }

        /// <summary>
        /// Shuts down all of the managed nodes permanently.
        /// </summary>
        /// <param name="hostHandshake">host handshake key</param>
        /// <param name="clientHandshake">client handshake key</param>
        /// <param name="terminateNode">Delegate used to tell the node provider that a context has terminated</param>
        protected void ShutdownAllNodes(long hostHandshake, long clientHandshake, NodeContextTerminateDelegate terminateNode)
        {
            // INodePacketFactory
            INodePacketFactory factory = new NodePacketFactory();

            // Find proper msbuild executable name
            string msbuildExeName = Environment.GetEnvironmentVariable("MSBUILD_EXE_NAME");

            if (String.IsNullOrEmpty(msbuildExeName))
            {
                msbuildExeName = "MSBuild.exe";
            }

            // Search for all instances of the msbuild process and create a list of them
            List<Process> nodeProcesses = new List<Process>(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(msbuildExeName)));

            // Find proper MSBuildTaskHost executable name
            string msbuildtaskhostExeName = NodeProviderOutOfProcTaskHost.TaskHostNameForClr2TaskHost;

            // Search for all instances of msbuildtaskhost process and add them to the process list
            nodeProcesses.AddRange(new List<Process>(Process.GetProcessesByName(Path.GetFileNameWithoutExtension(msbuildtaskhostExeName))));

            // For all processes in the list, send signal to terminate if able to connect
            foreach (Process nodeProcess in nodeProcesses)
            {
                NamedPipeClientStream nodeStream = TryConnectToProcess(nodeProcess.Id, 30/*verified to miss nodes if smaller*/, hostHandshake, clientHandshake);
                if (null != nodeStream)
                {
                    // If we're able to connect to such a process, send a packet requesting its termination
                    CommunicationsUtilities.Trace("Shutting down node with pid = {0}", nodeProcess.Id);
                    NodeContext nodeContext = new NodeContext(0, nodeProcess.Id, nodeStream, factory, terminateNode);
                    nodeContext.SendData(new NodeBuildComplete(false /* no node reuse */));
                    nodeStream.Close();
                }
            }
        }

        /// <summary>
        /// Finds or creates a child process which can act as a node.
        /// </summary>
        /// <returns>The pipe stream representing the node.</returns>
        protected NodeContext GetNode(string msbuildLocation, string commandLineArgs, int nodeId, INodePacketFactory factory, long hostHandshake, long clientHandshake, NodeContextTerminateDelegate terminateNode)
        {
            if (String.IsNullOrEmpty(msbuildLocation))
            {
                msbuildLocation = _componentHost.BuildParameters.NodeExeLocation;
            }

            if (String.IsNullOrEmpty(msbuildLocation))
            {
                string msbuildExeName = Environment.GetEnvironmentVariable("MSBUILD_EXE_NAME");

                if (!String.IsNullOrEmpty(msbuildExeName))
                {
                    // we assume that MSBUILD_EXE_NAME is, in fact, just the name.  
                    msbuildLocation = Path.Combine(msbuildExeName, ".exe");
                }
            }

            if (String.IsNullOrEmpty(msbuildLocation))
            {
                msbuildLocation = "MSBuild.exe";
            }

            string msbuildName = Path.GetFileNameWithoutExtension(msbuildLocation);

            List<Process> nodeProcesses = new List<Process>(Process.GetProcessesByName(msbuildName));

            // Trivial sort to try to prefer most recently used nodes
            nodeProcesses.Sort
                (
                delegate (Process left, Process right)
                {
                    return left.Id - right.Id;
                }

                );

            CommunicationsUtilities.Trace("Attempting to connect to each existing msbuild.exe process in turn to establish node {0}...", nodeId);
            foreach (Process nodeProcess in nodeProcesses)
            {
                if (nodeProcess.Id == Process.GetCurrentProcess().Id)
                {
                    continue;
                }

                // Get the full context of this inspection so that we can always skip this process when we have the same taskhost context
                string nodeLookupKey = GetProcessesToIgnoreKey(hostHandshake, clientHandshake, nodeProcess.Id);
                if (_processesToIgnore.Contains(nodeLookupKey))
                {
                    continue;
                }

                // We don't need to check this again
                _processesToIgnore.Add(nodeLookupKey);

                // Attempt to connect to each process in turn.
                NamedPipeClientStream nodeStream = TryConnectToProcess(nodeProcess.Id, 0 /* poll, don't wait for connections */, hostHandshake, clientHandshake);
                if (nodeStream != null)
                {
                    // Connection successful, use this node.   
                    CommunicationsUtilities.Trace("Successfully connected to existed node {0} which is PID {1}", nodeId, nodeProcess.Id);
                    return new NodeContext(nodeId, nodeProcess.Id, nodeStream, factory, terminateNode);
                }
            }

            // None of the processes we tried to connect to allowed a connection, so create a new one.
            // We try this in a loop because it is possible that there is another MSBuild multiproc
            // host process running somewhere which is also trying to create nodes right now.  It might
            // find our newly created node and connect to it before we get a chance.
            CommunicationsUtilities.Trace("Could not connect to existing process, now creating a process...");
            int retries = NodeCreationRetries;
            while (retries-- > 0)
            {
                // We will also check to see if .NET 3.5 is installed in the case where we need to launch a CLR2 OOP TaskHost.
                // Failure to detect this has been known to stall builds when Windows pops up a related dialog.
                // It's also a waste of time when we attempt several times to launch multiple MSBuildTaskHost.exe (CLR2 TaskHost)
                // nodes because we should never be able to connect in this case.
                string taskHostNameForClr2TaskHost = Path.GetFileNameWithoutExtension(NodeProviderOutOfProcTaskHost.TaskHostNameForClr2TaskHost);
                if (msbuildName.Equals(taskHostNameForClr2TaskHost, StringComparison.OrdinalIgnoreCase))
                {
                    if (FrameworkLocationHelper.GetPathToDotNetFrameworkV35(DotNetFrameworkArchitecture.Current) == null)
                    {
                        CommunicationsUtilities.Trace
                            (
                                "Failed to launch node from {0}. The required .NET Framework v3.5 is not installed or enabled. CommandLine: {1}",
                                msbuildLocation,
                                commandLineArgs
                            );

                        string nodeFailedToLaunchError = ResourceUtilities.FormatResourceString("TaskHostNodeFailedToLaunchErrorCodeNet35NotInstalled");
                        throw new NodeFailedToLaunchException(null, nodeFailedToLaunchError);
                    }
                }

                // Create the node process
                int msbuildProcessId = LaunchNode(msbuildLocation, commandLineArgs);
                _processesToIgnore.Add(GetProcessesToIgnoreKey(hostHandshake, clientHandshake, msbuildProcessId));

                // Note, when running under IMAGEFILEEXECUTIONOPTIONS registry key to debug, the process ID
                // gotten back from CreateProcess is that of the debugger, which causes this to try to connect
                // to the debugger process. Instead, use MSBUILDDEBUGONSTART=1

                // Now try to connect to it.
                NamedPipeClientStream nodeStream = TryConnectToProcess(msbuildProcessId, TimeoutForNewNodeCreation, hostHandshake, clientHandshake);
                if (nodeStream != null)
                {
                    // Connection successful, use this node.
                    CommunicationsUtilities.Trace("Successfully connected to created node {0} which is PID {1}", nodeId, msbuildProcessId);
                    return new NodeContext(nodeId, msbuildProcessId, nodeStream, factory, terminateNode);
                }
            }

            // We were unable to launch a node.
            CommunicationsUtilities.Trace("FAILED TO CONNECT TO A CHILD NODE");
            return null;
        }

        /// <summary>
        /// Generate a string from task host context and the remote process to be used as key to lookup processes we have already
        /// attempted to connect to or are already connected to
        /// </summary>
        private string GetProcessesToIgnoreKey(long hostHandshake, long clientHandshake, int nodeProcessId)
        {
            return hostHandshake.ToString(CultureInfo.InvariantCulture) + "|" + clientHandshake.ToString(CultureInfo.InvariantCulture) + "|" + nodeProcessId.ToString(CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Attempts to connect to the specified process.
        /// </summary>
        private NamedPipeClientStream TryConnectToProcess(int nodeProcessId, int timeout, long hostHandshake, long clientHandshake)
        {
            // Try and connect to the process.
            string pipeName = "MSBuild" + nodeProcessId;

            NamedPipeClientStream nodeStream = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
            CommunicationsUtilities.Trace("Attempting connect to PID {0} with pipe {1} with timeout {2} ms", nodeProcessId, pipeName, timeout);

            try
            {
                nodeStream.Connect(timeout);

                // Verify that the owner of the pipe is us.  This prevents a security hole where a remote node has
                // been faked up with ACLs that would let us attach to it.  It could then issue fake build requests back to
                // us, potentially causing us to execute builds that do harmful or unexpected things.  The pipe owner can
                // only be set to the user's own SID by a normal, unprivileged process.  The conditions where a faked up
                // remote node could set the owner to something else would also let it change owners on other objects, so
                // this would be a security flaw upstream of us.
                SecurityIdentifier identifier = WindowsIdentity.GetCurrent().Owner;
                PipeSecurity remoteSecurity = nodeStream.GetAccessControl();
                IdentityReference remoteOwner = remoteSecurity.GetOwner(typeof(SecurityIdentifier));
                if (remoteOwner != identifier)
                {
                    CommunicationsUtilities.Trace("The remote pipe owner {0} does not match {1}", remoteOwner.Value, identifier.Value);
                    throw new UnauthorizedAccessException();
                }

                CommunicationsUtilities.Trace("Writing handshake to pipe {0}", pipeName);
                nodeStream.WriteLongForHandshake(hostHandshake);

                CommunicationsUtilities.Trace("Reading handshake from pipe {0}", pipeName);
                long handshake = nodeStream.ReadLongForHandshake();

                if (handshake != clientHandshake)
                {
                    CommunicationsUtilities.Trace("Handshake failed. Received {0} from client not {1}. Probably the client is a different MSBuild build.", handshake, clientHandshake);
                    throw new InvalidOperationException();
                }

                // We got a connection.
                CommunicationsUtilities.Trace("Successfully connected to pipe {0}...!", pipeName);
                return nodeStream;
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                // Can be:
                // UnauthorizedAccessException -- Couldn't connect, might not be a node.
                // IOException -- Couldn't connect, already in use.
                // TimeoutException -- Couldn't connect, might not be a node.
                // InvalidOperationException – Couldn’t connect, probably a different build
                CommunicationsUtilities.Trace("Failed to connect to pipe {0}. {1}", pipeName, e.Message.TrimEnd());

                // If we don't close any stream, we might hang up the child
                if (nodeStream != null)
                {
                    nodeStream.Close();
                }
            }

            return null;
        }

        /// <summary>
        /// Creates a new MSBuild process
        /// </summary>
        private int LaunchNode(string msbuildLocation, string commandLineArgs)
        {
            // Should always have been set already. 
            ErrorUtilities.VerifyThrowInternalLength(msbuildLocation, "msbuildLocation");

            if (!File.Exists(msbuildLocation))
            {
                throw new BuildAbortedException(ResourceUtilities.FormatResourceString("CouldNotFindMSBuildExe", msbuildLocation));
            }

            // Repeat the executable name as the first token of the command line because the command line
            // parser logic expects it and will otherwise skip the first argument
            commandLineArgs = msbuildLocation + " " + commandLineArgs;

            BackendNativeMethods.STARTUP_INFO startInfo = new BackendNativeMethods.STARTUP_INFO();
            startInfo.cb = Marshal.SizeOf<BackendNativeMethods.STARTUP_INFO>();

            // Null out the process handles so that the parent process does not wait for the child process
            // to exit before it can exit.
            uint creationFlags = BackendNativeMethods.NORMALPRIORITYCLASS;
            startInfo.dwFlags = BackendNativeMethods.STARTFUSESTDHANDLES;

            if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("MSBUILDNODEWINDOW")))
            {
                startInfo.hStdError = BackendNativeMethods.InvalidHandle;
                startInfo.hStdInput = BackendNativeMethods.InvalidHandle;
                startInfo.hStdOutput = BackendNativeMethods.InvalidHandle;
                creationFlags = creationFlags | BackendNativeMethods.CREATENOWINDOW;
            }
            else
            {
                creationFlags = creationFlags | BackendNativeMethods.CREATE_NEW_CONSOLE;
            }

            BackendNativeMethods.SECURITY_ATTRIBUTES processSecurityAttributes = new BackendNativeMethods.SECURITY_ATTRIBUTES();
            BackendNativeMethods.SECURITY_ATTRIBUTES threadSecurityAttributes = new BackendNativeMethods.SECURITY_ATTRIBUTES();
            processSecurityAttributes.nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>();
            threadSecurityAttributes.nLength = Marshal.SizeOf<BackendNativeMethods.SECURITY_ATTRIBUTES>();

            BackendNativeMethods.PROCESS_INFORMATION processInfo = new BackendNativeMethods.PROCESS_INFORMATION();

            string appName = msbuildLocation;

            CommunicationsUtilities.Trace("Launching node from {0}", msbuildLocation);
            bool result = BackendNativeMethods.CreateProcess
                (
                    msbuildLocation,
                    commandLineArgs,
                    ref processSecurityAttributes,
                    ref threadSecurityAttributes,
                    false,
                    creationFlags,
                    BackendNativeMethods.NullPtr,
                    null,
                    ref startInfo,
                    out processInfo
                );

            if (!result)
            {
                // Creating an instance of this exception calls GetLastWin32Error and also converts it to a user-friendly string.
                System.ComponentModel.Win32Exception e = new System.ComponentModel.Win32Exception();

                CommunicationsUtilities.Trace
                    (
                        "Failed to launch node from {0}. System32 Error code {1}. Description {2}. CommandLine: {2}",
                        msbuildLocation,
                        e.NativeErrorCode.ToString(CultureInfo.InvariantCulture),
                        e.Message,
                        commandLineArgs
                    );

                throw new NodeFailedToLaunchException(e.NativeErrorCode.ToString(CultureInfo.InvariantCulture), e.Message);
            }

            CommunicationsUtilities.Trace("Successfully launched msbuild.exe node with PID {0}", processInfo.dwProcessId);
            return processInfo.dwProcessId;
        }

        /// <summary>
        /// Class which wraps up the communications infrastructure for a given node.
        /// </summary>
        internal class NodeContext
        {
            /// <summary>
            /// Whether to trace communications.
            /// Stored here as a field to avoid a function call when writing packets
            /// </summary>
            private static bool s_trace = String.Equals(Environment.GetEnvironmentVariable("MSBUILDDEBUGCOMM"), "1", StringComparison.Ordinal);

            /// <summary>
            /// The pipe used to communicate with the node.
            /// </summary>
            private NamedPipeClientStream _nodePipe;

            /// <summary>
            /// The factory used to create packets from data read off the pipe.
            /// </summary>
            private INodePacketFactory _packetFactory;

            /// <summary>
            /// The node id assigned by the node provider.
            /// </summary>
            private int _nodeId;

            /// <summary>
            /// The process id
            /// </summary>
            private int _processId;

            /// <summary>
            /// An array used to store the header byte for each packet when read.
            /// </summary>
            private byte[] _headerByte;

            /// <summary>
            /// A buffer typically big enough to handle a packet body.
            /// </summary>
            private byte[] _smallReadBuffer;

            /// <summary>
            /// Event indicating the node has terminated.
            /// </summary>
            private ManualResetEvent _nodeTerminated;

            /// <summary>
            /// Delegate called when the context terminates.
            /// </summary>
            private NodeContextTerminateDelegate _terminateDelegate;

            /// <summary>
            /// Per node read buffers
            /// </summary>
            private SharedReadBuffer _sharedReadBuffer;

            /// <summary>
            /// Constructor.
            /// </summary>
            public NodeContext(int nodeId, int processId, NamedPipeClientStream nodePipe, INodePacketFactory factory, NodeContextTerminateDelegate terminateDelegate)
            {
                _nodeId = nodeId;
                _processId = processId;
                _nodePipe = nodePipe;
                _packetFactory = factory;
                _headerByte = new byte[5]; // 1 for the packet type, 4 for the body length
                _smallReadBuffer = new byte[1000]; // 1000 was just an average seen on one profile run.
                _nodeTerminated = new ManualResetEvent(false);
                _terminateDelegate = terminateDelegate;
                _sharedReadBuffer = InterningBinaryReader.CreateSharedBuffer();
            }

            /// <summary>
            /// Starts a new asynchronous read operation for this node.
            /// </summary>
            public void BeginAsyncPacketRead()
            {
                _nodePipe.BeginRead(_headerByte, 0, _headerByte.Length, HeaderReadComplete, this);
            }

            /// <summary>
            /// Sends the specified packet to this node.
            /// </summary>
            /// <param name="packet">The packet to send.</param>
            public void SendData(INodePacket packet)
            {
                MemoryStream writeStream = new MemoryStream();
                INodePacketTranslator writeTranslator = NodePacketTranslator.GetWriteTranslator(writeStream);
                try
                {
                    writeStream.WriteByte((byte)packet.Type);

                    // Pad for the packet length
                    writeStream.Write(BitConverter.GetBytes((int)0), 0, 4);
                    packet.Translate(writeTranslator);

                    // Now plug in the real packet length
                    writeStream.Position = 1;
                    writeStream.Write(BitConverter.GetBytes((int)writeStream.Length - 5), 0, 4);

#if FALSE
                    if (trace) // Avoid method call
                    {
                        CommunicationsUtilities.Trace(nodeId, "Sending Packet of type {0} with length {1}", packet.Type.ToString(), writeStream.Length - 5);
                    }
#endif

                    for (int i = 0; i < writeStream.Length; i += MaxPacketWriteSize)
                    {
                        int lengthToWrite = Math.Min((int)writeStream.Length - i, MaxPacketWriteSize);
                        if ((int)writeStream.Length - i <= MaxPacketWriteSize)
                        {
                            // We are done, write the last bit asynchronously.  This is actually the general case for
                            // most packets in the build, and the asynchronous behavior here is desirable.
                            _nodePipe.BeginWrite(writeStream.GetBuffer(), i, lengthToWrite, PacketWriteComplete, null);
                            return;
                        }
                        else
                        {
                            // If this packet is longer that we can write in one go, then we need to break it up.  We can't
                            // return out of this function and let the rest of the system continue because another operation
                            // might want to send data immediately afterward, and that could result in overlapping writes
                            // to the pipe on different threads.
                            IAsyncResult result = _nodePipe.BeginWrite(writeStream.GetBuffer(), i, lengthToWrite, null, null);
                            _nodePipe.EndWrite(result);
                        }
                    }
                }
                catch (IOException e)
                {
                    // Do nothing here because any exception will be caught by the async read handler
                    CommunicationsUtilities.Trace(_nodeId, "EXCEPTION in SendData: {0}", e);
                }
                catch (ObjectDisposedException) // This happens if a child dies unexpectedly
                {
                    // Do nothing here because any exception will be caught by the async read handler
                }
            }

            /// <summary>
            /// Closes the node's context, disconnecting it from the node.
            /// </summary>
            public void Close()
            {
                _nodePipe.Close();
                _terminateDelegate(_nodeId);
            }

            /// <summary>
            /// Completes the asynchronous packet write to the node.
            /// </summary>
            private void PacketWriteComplete(IAsyncResult result)
            {
                try
                {
                    _nodePipe.EndWrite(result);
                }
                catch (IOException)
                {
                    // Do nothing here because any exception will be caught by the async read handler
                }
            }

            /// <summary>
            /// Callback invoked by the completion of a read of a header byte on one of the named pipes.
            /// </summary>
            private void HeaderReadComplete(IAsyncResult result)
            {
                int bytesRead;
                try
                {
                    try
                    {
                        bytesRead = _nodePipe.EndRead(result);
                    }

                    // Workaround for CLR stress bug; it sporadically calls us twice on the same async
                    // result, and EndRead will throw on the second one. Pretend the second one never happened.
                    catch (ArgumentException)
                    {
                        CommunicationsUtilities.Trace(_nodeId, "Hit CLR bug #825607: called back twice on same async result; ignoring");
                        return;
                    }

                    if (bytesRead != _headerByte.Length)
                    {
                        CommunicationsUtilities.Trace(_nodeId, "COMMUNICATIONS ERROR (HRC) Node: {0} Process: {1} Bytes Read: {2} Expected: {3}", _nodeId, _processId, bytesRead, _headerByte.Length);
                        try
                        {
                            Process childProcess = Process.GetProcessById(_processId);
                            if (childProcess == null || childProcess.HasExited)
                            {
                                CommunicationsUtilities.Trace(_nodeId, "   Child Process {0} has exited.", _processId);
                            }
                            else
                            {
                                CommunicationsUtilities.Trace(_nodeId, "   Child Process {0} is still running.", _processId);
                            }
                        }
                        catch (Exception e)
                        {
                            if (ExceptionHandling.IsCriticalException(e))
                            {
                                throw;
                            }

                            CommunicationsUtilities.Trace(_nodeId, "Unable to retrieve remote process information. {0}", e);
                        }

                        _packetFactory.RoutePacket(_nodeId, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
                        Close();
                        return;
                    }
                }
                catch (IOException e)
                {
                    CommunicationsUtilities.Trace(_nodeId, "EXCEPTION in HeaderReadComplete: {0}", e);
                    _packetFactory.RoutePacket(_nodeId, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
                    Close();
                    return;
                }

                NodePacketType packetType = (NodePacketType)_headerByte[0];
                int packetLength = BitConverter.ToInt32(_headerByte, 1);

                byte[] packetData;
                if (packetLength < _smallReadBuffer.Length)
                {
                    packetData = _smallReadBuffer;
                }
                else
                {
                    // Preallocated buffer is not large enough to hold the body. Allocate now, but don't hold it forever.
                    packetData = new byte[packetLength];
                }

                _nodePipe.BeginRead(packetData, 0, packetLength, BodyReadComplete, new Tuple<byte[], int>(packetData, packetLength));
            }

            /// <summary>
            /// Method called when the body of a packet has been read.
            /// </summary>
            private void BodyReadComplete(IAsyncResult result)
            {
                NodePacketType packetType = (NodePacketType)_headerByte[0];
                var state = (Tuple<byte[], int>)result.AsyncState;
                byte[] packetData = state.Item1;
                int packetLength = state.Item2;
                int bytesRead;

                try
                {
                    try
                    {
                        bytesRead = _nodePipe.EndRead(result);
                    }

                    // Workaround for CLR stress bug; it sporadically calls us twice on the same async
                    // result, and EndRead will throw on the second one. Pretend the second one never happened.
                    catch (ArgumentException)
                    {
                        CommunicationsUtilities.Trace(_nodeId, "Hit CLR bug #825607: called back twice on same async result; ignoring");
                        return;
                    }

                    if (bytesRead != packetLength)
                    {
                        CommunicationsUtilities.Trace(_nodeId, "Bad packet read for packet {0} - Expected {1} bytes, got {2}", packetType, packetLength, bytesRead);
                        _packetFactory.RoutePacket(_nodeId, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
                        Close();
                        return;
                    }
                }
                catch (IOException e)
                {
                    CommunicationsUtilities.Trace(_nodeId, "EXCEPTION in BodyReadComplete (Reading): {0}", e);
                    _packetFactory.RoutePacket(_nodeId, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
                    Close();
                    return;
                }

                // Read and route the packet.            
                try
                {
                    // The buffer is publicly visible so that InterningBinaryReader doesn't have to copy to an intermediate buffer.
                    // Since the buffer is publicly visible dispose right away to discourage outsiders from holding a reference to it.
                    using (var packetStream = new MemoryStream(packetData, 0, packetLength, /*writeable*/ false, /*bufferIsPubliclyVisible*/ true))
                    {
                        INodePacketTranslator readTranslator = NodePacketTranslator.GetReadTranslator(packetStream, _sharedReadBuffer);
                        _packetFactory.DeserializeAndRoutePacket(_nodeId, packetType, readTranslator);
                    }
                }
                catch (IOException e)
                {
                    CommunicationsUtilities.Trace(_nodeId, "EXCEPTION in BodyReadComplete (Routing): {0}", e);
                    _packetFactory.RoutePacket(_nodeId, new NodeShutdown(NodeShutdownReason.ConnectionFailed));
                    Close();
                    return;
                }

                if (packetType != NodePacketType.NodeShutdown)
                {
                    // Read the next packet.
                    BeginAsyncPacketRead();
                }
                else
                {
                    Close();
                }
            }
        }
    }
}
