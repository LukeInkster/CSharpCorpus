﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Shared utility methods primarily relating to communication 
// between nodes.</summary>
//-----------------------------------------------------------------------

using System;
using System.Xml;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Xml.Serialization;
using System.Security;
using System.Security.Policy;
using System.Security.Permissions;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading;

using Microsoft.Build.Shared;

namespace Microsoft.Build.Internal
{
    /// <summary>
    /// Enumeration of all possible (currently supported) types of task host context.
    /// </summary>
    internal enum TaskHostContext
    {
        /// <summary>
        /// 32-bit Intel process, using the 2.0 CLR.
        /// </summary>
        X32CLR2,

        /// <summary>
        /// 64-bit Intel process, using the 2.0 CLR.
        /// </summary>
        X64CLR2,

        /// <summary>
        /// 32-bit Intel process, using the 4.0 CLR.
        /// </summary>
        X32CLR4,

        /// <summary>
        /// 64-bit Intel process, using the 4.0 CLR.
        /// </summary>
        X64CLR4,

        /// <summary>
        /// Invalid task host context
        /// </summary>
        Invalid
    }

    /// <summary>
    /// This class contains utility methods for the MSBuild engine.
    /// </summary>
    static internal class CommunicationsUtilities
    {
        /// <summary>
        /// The timeout to connect to a node.
        /// </summary>
        private const int DefaultNodeConnectionTimeout = 900 * 1000; // 15 minutes; enough time that a dev will typically do another build in this time

        /// <summary>
        /// Flag if we have already calculated the FileVersion hashcode
        /// </summary>
        private static bool s_fileVersionChecked;

        /// <summary>
        /// A hashcode calculated from the fileversion
        /// </summary>
        private static int s_fileVersionHash;

        /// <summary>
        /// Whether to trace communications
        /// </summary>
        private static bool s_trace = String.Equals(Environment.GetEnvironmentVariable("MSBUILDDEBUGCOMM"), "1", StringComparison.Ordinal);

        /// <summary>
        /// Place to dump trace
        /// </summary>
        private static string s_debugDumpPath;

        /// <summary>
        /// Ticks at last time logged
        /// </summary>
        private static long s_lastLoggedTicks = DateTime.UtcNow.Ticks;

        /// <summary>
        /// Delegate to debug the communication utilities.
        /// </summary>
        internal delegate void LogDebugCommunications(string format, params object[] stuff);

        /// <summary>
        /// Gets or sets the node connection timeout.
        /// </summary>
        static internal int NodeConnectionTimeout
        {
            get { return GetIntegerVariableOrDefault("MSBUILDNODECONNECTIONTIMEOUT", DefaultNodeConnectionTimeout); }
        }

        /// <summary>
        /// Looks up the file version and caches the hashcode
        /// This file version hashcode is used in calculating the handshake
        /// </summary>
        private static int FileVersionHash
        {
            get
            {
                if (!s_fileVersionChecked)
                {
                    // We only hash in any complus_installroot value, not a file version.
                    // This is because in general msbuildtaskhost.exe does not load any assembly that
                    // the parent process loads, so they can't compare the version of a particular assembly.
                    // They can't compare their own versions, because if one of them is serviced, they
                    // won't match any more. The only known incompatibility is between a razzle and non-razzle
                    // parent and child. COMPLUS_Version can (and typically will) differ legitimately between
                    // them, so just check COMPLUS_InstallRoot.
                    string complusInstallRoot = Environment.GetEnvironmentVariable("COMPLUS_INSTALLROOT");

                    // We should also check the file version when COMPLUS_INSTALLROOT is null, because the protocol can change between releases.
                    // If we don't check, we'll run into issues
                    string taskhostexe = FileUtilities.ExecutingAssemblyPath;
                    string majorVersion = FileVersionInfo.GetVersionInfo(taskhostexe).FileMajorPart.ToString();

                    s_fileVersionHash = GetHandshakeHashCode(complusInstallRoot ?? majorVersion);
                    s_fileVersionChecked = true;
                }

                return s_fileVersionHash;
            }
        }

        /// <summary>
        /// Get environment block
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static unsafe extern char* GetEnvironmentStrings();

        /// <summary>
        /// Free environment block
        /// </summary>
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static unsafe extern bool FreeEnvironmentStrings(char* pStrings);

        /// <summary>
        /// Move a block of chars
        /// </summary>
        [DllImport("kernel32.dll", EntryPoint = "RtlMoveMemory")]
        internal static unsafe extern void CopyMemory(char* destination, char* source, uint length);

        /// <summary>
        /// Retrieve the environment block.
        /// Copied from the BCL implementation to eliminate some expensive security asserts.
        /// </summary>
        internal unsafe static char[] GetEnvironmentCharArray()
        {
            char[] block = null;
            char* pStrings = null;

            try
            {
                pStrings = GetEnvironmentStrings();
                if (pStrings == null)
                {
                    throw new OutOfMemoryException();
                }

                // Format for GetEnvironmentStrings is:
                // [=HiddenVar=value\0]* [Variable=value\0]* \0
                // See the description of Environment Blocks in MSDN's
                // CreateProcess page (null-terminated array of null-terminated strings).

                // Search for terminating \0\0 (two unicode \0's).
                char* p = pStrings;
                while (!(*p == '\0' && *(p + 1) == '\0'))
                {
                    p++;
                }

                uint chars = (uint)(p - pStrings + 1);
                uint bytes = chars * sizeof(char);

                block = new char[chars];

                fixed (char* pBlock = block)
                {
                    CopyMemory(pBlock, pStrings, bytes);
                }
            }
            finally
            {
                if (pStrings != null)
                {
                    FreeEnvironmentStrings(pStrings);
                }
            }

            return block;
        }

        /// <summary>
        /// Copied from the BCL implementation to eliminate some expensive security asserts.
        /// Returns key value pairs of environment variables in a new dictionary
        /// with a case-insensitive key comparer.
        /// </summary>
        internal static Dictionary<string, string> GetEnvironmentVariables()
        {
            char[] block = GetEnvironmentCharArray();

            Dictionary<string, string> table = new Dictionary<string, string>(200, StringComparer.OrdinalIgnoreCase); // Razzle has 150 environment variables

            // Copy strings out, parsing into pairs and inserting into the table.
            // The first few environment variable entries start with an '='!
            // The current working directory of every drive (except for those drives
            // you haven't cd'ed into in your DOS window) are stored in the 
            // environment block (as =C:=pwd) and the program's exit code is 
            // as well (=ExitCode=00000000)  Skip all that start with =.
            // Read docs about Environment Blocks on MSDN's CreateProcess page.

            // Format for GetEnvironmentStrings is:
            // (=HiddenVar=value\0 | Variable=value\0)* \0
            // See the description of Environment Blocks in MSDN's
            // CreateProcess page (null-terminated array of null-terminated strings).
            // Note the =HiddenVar's aren't always at the beginning.
            for (int i = 0; i < block.Length; i++)
            {
                int startKey = i;

                // Skip to key
                // On some old OS, the environment block can be corrupted. 
                // Someline will not have '=', so we need to check for '\0'. 
                while (block[i] != '=' && block[i] != '\0')
                {
                    i++;
                }

                if (block[i] == '\0')
                {
                    continue;
                }

                // Skip over environment variables starting with '='
                if (i - startKey == 0)
                {
                    while (block[i] != 0)
                    {
                        i++;
                    }

                    continue;
                }

                string key = new string(block, startKey, i - startKey);
                i++;

                // skip over '='
                int startValue = i;

                while (block[i] != 0)
                {
                    // Read to end of this entry 
                    i++;
                }

                string value = new string(block, startValue, i - startValue);

                // skip over 0 handled by for loop's i++
                table[key] = value;
            }

            return table;
        }

        /// <summary>
        /// Updates the environment to match the provided dictionary.
        /// </summary>
        internal static void SetEnvironment(IDictionary<string, string> newEnvironment)
        {
            if (newEnvironment != null)
            {
                // First, empty out any new variables
                foreach (KeyValuePair<string, string> entry in CommunicationsUtilities.GetEnvironmentVariables())
                {
                    if (!newEnvironment.ContainsKey(entry.Key))
                    {
                        Environment.SetEnvironmentVariable(entry.Key, null);
                    }
                }

                // Then, make sure the old ones have their old values. 
                foreach (KeyValuePair<string, string> entry in newEnvironment)
                {
                    Environment.SetEnvironmentVariable(entry.Key, entry.Value);
                }
            }
        }

        /// <summary>
        /// Given a base handshake, generates the real handshake based on e.g. elevation level.  
        /// Client handshake required for comparison purposes only.  Returns the update handshake.  
        /// </summary>
        internal static long GenerateHostHandshakeFromBase(long baseHandshake, long clientHandshake)
        {
            // If we are running in elevated privs, we will only accept a handshake from an elevated process as well.
            WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());

            // Both the client and the host will calculate this separately, and the idea is that if they come out the same
            // then we can be sufficiently confident that the other side has the same elevation level as us.  This is complementary
            // to the username check which is also done on connection.
            if (principal.IsInRole(WindowsBuiltInRole.Administrator))
            {
                unchecked
                {
                    baseHandshake = baseHandshake ^ 0x5c5c5c5c5c5c5c5c + Process.GetCurrentProcess().SessionId;
                }

                if ((baseHandshake & 0x00FFFFFFFFFFFFFF) == clientHandshake)
                {
                    baseHandshake = ~baseHandshake;
                }
            }

            // Mask out the first byte. That's because old
            // builds used a single, non zero initial byte,
            // and we don't want to risk communicating with them
            return baseHandshake & 0x00FFFFFFFFFFFFFF;
        }

        /// <summary>
        /// Magic number sent by the host to the client during the handshake.
        /// Derived from the binary timestamp to avoid mixing binary versions.
        /// </summary>
        internal static long GetTaskHostHostHandshake(TaskHostContext hostContext)
        {
            long baseHandshake = GenerateHostHandshakeFromBase(GetBaseHandshakeForContext(hostContext), GetTaskHostClientHandshake(hostContext));
            return baseHandshake;
        }

        /// <summary>
        /// Magic number sent by the client to the host during the handshake.
        /// Munged version of the host handshake.
        /// </summary>
        internal static long GetTaskHostClientHandshake(TaskHostContext hostContext)
        {
            // Mask out the first byte. That's because old
            // builds used a single, non zero initial byte,
            // and we don't want to risk communicating with them
            long clientHandshake = ((GetBaseHandshakeForContext(hostContext) ^ Int64.MaxValue) & 0x00FFFFFFFFFFFFFF);
            return clientHandshake;
        }

        /// <summary>
        /// Extension method to write a series of bytes to a stream
        /// </summary>
        internal static void WriteLongForHandshake(this PipeStream stream, long value)
        {
            byte[] bytes = BitConverter.GetBytes(value);

            // We want to read the long and send it from left to right (this means big endian)
            // if we are little endian we need to reverse the array to keep the left to right reading
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            ErrorUtilities.VerifyThrow(bytes.Length == 8, "Long should be 8 bytes");

            stream.Write(bytes, 0, bytes.Length);
        }

        /// <summary>
        /// Extension method to read a series of bytes from a stream
        /// </summary>
        internal static long ReadLongForHandshake(this PipeStream stream)
        {
            return stream.ReadLongForHandshake((byte[])null, 0);
        }

        /// <summary>
        /// Extension method to read a series of bytes from a stream.
        /// If specified, leading byte matches one in the supplied array if any, returns rejection byte and throws IOException.
        /// </summary>
        internal static long ReadLongForHandshake(this PipeStream stream, byte[] leadingBytesToReject, byte rejectionByteToReturn)
        {
            byte[] bytes = new byte[8];

            for (int i = 0; i < bytes.Length; i++)
            {
                int read = stream.ReadByte();

                if (read == -1)
                {
                    // We've unexpectly reached end of stream.
                    // We are now in a bad state, disconnect on our end
                    throw new IOException(String.Format(CultureInfo.InvariantCulture, "Unexpected end of stream while reading for handshake"));
                }

                if (i == 0 && leadingBytesToReject != null)
                {
                    foreach (byte reject in leadingBytesToReject)
                    {
                        if (read == reject)
                        {
                            stream.WriteByte(rejectionByteToReturn); // disconnect the host

                            throw new IOException(String.Format(CultureInfo.InvariantCulture, "Client: rejected old host. Received byte {0} but this matched a byte to reject.", bytes[i]));  // disconnect and quit
                        }
                    }
                }

                bytes[i] = Convert.ToByte(read);
            }

            long result;

            try
            {
                // We want to read the long and send it from left to right (this means big endian)
                // If we are little endian the stream has already been reversed by the sender, we need to reverse it again to get the original number
                if (BitConverter.IsLittleEndian)
                {
                    Array.Reverse(bytes);
                }

                result = BitConverter.ToInt64(bytes, 0 /* start index */);
            }
            catch (ArgumentException ex)
            {
                throw new IOException(String.Format(CultureInfo.InvariantCulture, "Failed to convert the handshake to big-endian. {0}", ex.Message));
            }

            return result;
        }

        /// <summary>
        /// Given the appropriate information, return the equivalent TaskHostContext.  
        /// </summary>
        internal static TaskHostContext GetTaskHostContext(IDictionary<string, string> taskHostParameters)
        {
            ErrorUtilities.VerifyThrow(taskHostParameters.ContainsKey(XMakeAttributes.runtime), "Should always have an explicit runtime when we call this method.");
            ErrorUtilities.VerifyThrow(taskHostParameters.ContainsKey(XMakeAttributes.architecture), "Should always have an explicit architecture when we call this method.");

            string runtime = taskHostParameters[XMakeAttributes.runtime];
            string architecture = taskHostParameters[XMakeAttributes.architecture];

            bool is64BitProcess = false;
            int clrVersion = 0;

            if (architecture.Equals(XMakeAttributes.MSBuildArchitectureValues.x64, StringComparison.OrdinalIgnoreCase))
            {
                is64BitProcess = true;
            }
            else if (architecture.Equals(XMakeAttributes.MSBuildArchitectureValues.x86, StringComparison.OrdinalIgnoreCase))
            {
                is64BitProcess = false;
            }
            else
            {
                ErrorUtilities.ThrowInternalError("Should always have an explicit architecture when calling this method");
            }

            if (runtime.Equals(XMakeAttributes.MSBuildRuntimeValues.clr4, StringComparison.OrdinalIgnoreCase))
            {
                clrVersion = 4;
            }
            else if (runtime.Equals(XMakeAttributes.MSBuildRuntimeValues.clr2, StringComparison.OrdinalIgnoreCase))
            {
                clrVersion = 2;
            }
            else
            {
                ErrorUtilities.ThrowInternalError("Should always have an explicit runtime when calling this method");
            }

            TaskHostContext hostContext = GetTaskHostContext(is64BitProcess, clrVersion);
            return hostContext;
        }

        /// <summary>
        /// Given the appropriate information, return the equivalent TaskHostContext.  
        /// </summary>
        internal static TaskHostContext GetTaskHostContext(bool is64BitProcess, int clrVersion)
        {
            TaskHostContext hostContext = TaskHostContext.Invalid;
            switch (clrVersion)
            {
                case 2:
                    hostContext = is64BitProcess ? TaskHostContext.X64CLR2 : TaskHostContext.X32CLR2;
                    break;
                case 4:
                    hostContext = is64BitProcess ? TaskHostContext.X64CLR4 : TaskHostContext.X32CLR4;
                    break;
                default:
                    ErrorUtilities.ThrowInternalErrorUnreachable();
                    hostContext = TaskHostContext.Invalid;
                    break;
            }

            return hostContext;
        }

        /// <summary>
        /// Returns the TaskHostContext corresponding to this process
        /// </summary>
        internal static TaskHostContext GetCurrentTaskHostContext()
        {
            // We know that whichever assembly is executing this code -- whether it's MSBuildTaskHost.exe or 
            // Microsoft.Build.dll -- is of the version of the CLR that this process is running.  So grab
            // the version of mscorlib currently in use and call that good enough.  
            Version mscorlibVersion = typeof(bool).Assembly.GetName().Version;

            string currentMSBuildArchitecture = XMakeAttributes.GetCurrentMSBuildArchitecture();
            TaskHostContext hostContext = GetTaskHostContext(currentMSBuildArchitecture.Equals(XMakeAttributes.MSBuildArchitectureValues.x64), mscorlibVersion.Major);

            return hostContext;
        }

        /// <summary>
        /// Gets the value of an integer environment variable, or returns the default if none is set or it cannot be converted.
        /// </summary>
        internal static int GetIntegerVariableOrDefault(string environmentVariable, int defaultValue)
        {
            string environmentValue = Environment.GetEnvironmentVariable(environmentVariable);
            if (String.IsNullOrEmpty(environmentValue))
            {
                return defaultValue;
            }

            int localDefaultValue;
            if (Int32.TryParse(environmentValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out localDefaultValue))
            {
                defaultValue = localDefaultValue;
            }

            return defaultValue;
        }

        /// <summary>
        /// Writes trace information to a log file
        /// </summary>
        internal static void Trace(string format, params object[] args)
        {
            Trace(/* nodeId */ -1, format, args);
        }

        /// <summary>
        /// Writes trace information to a log file
        /// </summary>
        internal static void Trace(int nodeId, string format, params object[] args)
        {
            if (s_trace)
            {
                if (s_debugDumpPath == null)
                {
                    s_debugDumpPath = Environment.GetEnvironmentVariable("MSBUILDDEBUGPATH");

                    if (String.IsNullOrEmpty(s_debugDumpPath))
                    {
                        s_debugDumpPath = Path.GetTempPath();
                    }
                    else
                    {
                        Directory.CreateDirectory(s_debugDumpPath);
                    }
                }

                try
                {
                    string fileName = @"MSBuild_CommTrace_PID_{0}";
                    if (nodeId != -1)
                    {
                        fileName += "_node_" + nodeId;
                    }

                    fileName += ".txt";

                    using (StreamWriter file = new StreamWriter(String.Format(CultureInfo.CurrentCulture, Path.Combine(s_debugDumpPath, fileName), Process.GetCurrentProcess().Id, nodeId), true))
                    {
                        string message = String.Format(CultureInfo.CurrentCulture, format, args);
                        long now = DateTime.UtcNow.Ticks;
                        float millisecondsSinceLastLog = (float)((now - s_lastLoggedTicks) / 10000L);
                        s_lastLoggedTicks = now;
                        file.WriteLine("{0} (TID {1}) {2,15} +{3,10}ms: {4}", Thread.CurrentThread.Name, Thread.CurrentThread.ManagedThreadId, now, millisecondsSinceLastLog, message);
                    }
                }
                catch (IOException)
                {
                    // Ignore
                }
            }
        }

        /// <summary>
        /// Add the task host context to this handshake, to make sure that task hosts with different contexts 
        /// will have different handshakes.  Shift it into the upper 32-bits to avoid running into the 
        /// session ID.
        /// </summary>
        /// <param name="hostContext">TaskHostContext</param>
        /// <returns>Base Handshake</returns>
        private static long GetBaseHandshakeForContext(TaskHostContext hostContext)
        {
            long baseHandshake = ((long)hostContext << 40) | ((long)FileVersionHash << 8);
            return baseHandshake;
        }

        /// <summary>
        /// Gets a hash code for this string.  If strings A and B are such that A.Equals(B), then
        /// they will return the same hash code.
        /// This is as implemented in CLR String.GetHashCode() [ndp\clr\src\BCL\system\String.cs]
        /// but stripped out architecture specific defines
        /// that causes the hashcode to be different and this causes problem in cross-architecture handshaking
        /// </summary>
        private static int GetHandshakeHashCode(string fileVersion)
        {
            unsafe
            {
                fixed (char* src = fileVersion)
                {
                    int hash1 = (5381 << 16) + 5381;
                    int hash2 = hash1;

                    int* pint = (int*)src;
                    int len = fileVersion.Length;
                    while (len > 0)
                    {
                        hash1 = ((hash1 << 5) + hash1 + (hash1 >> 27)) ^ pint[0];
                        if (len <= 2)
                        {
                            break;
                        }

                        hash2 = ((hash2 << 5) + hash2 + (hash2 >> 27)) ^ pint[1];
                        pint += 2;
                        len -= 4;
                    }

                    return hash1 + (hash2 * 1566083941);
                }
            }
        }
    }
}
