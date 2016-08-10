﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.ConstrainedExecution;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;

using FILETIME = System.Runtime.InteropServices.ComTypes.FILETIME;

namespace Microsoft.Build.Shared
{
    /// <summary>
    /// Interop methods.
    /// </summary>
    internal static class NativeMethodsShared
    {
        #region Constants

        internal const uint ERROR_INSUFFICIENT_BUFFER = 0x8007007A;
        internal const uint STARTUP_LOADER_SAFEMODE = 0x10;
        internal const uint S_OK = 0x0;
        internal const uint S_FALSE = 0x1;
        internal const uint ERROR_ACCESS_DENIED = 0x5;
        internal const uint ERROR_FILE_NOT_FOUND = 0x80070002;
        internal const uint FUSION_E_PRIVATE_ASM_DISALLOWED = 0x80131044; // Tried to find unsigned assembly in GAC
        internal const uint RUNTIME_INFO_DONT_SHOW_ERROR_DIALOG = 0x40;
        internal const uint FILE_TYPE_CHAR = 0x0002;
        internal const Int32 STD_OUTPUT_HANDLE = -11;
        internal const uint RPC_S_CALLPENDING = 0x80010115;
        internal const uint E_ABORT = (uint)0x80004004;

        internal const int FILE_ATTRIBUTE_READONLY = 0x00000001;
        internal const int FILE_ATTRIBUTE_DIRECTORY = 0x00000010;
        internal const int FILE_ATTRIBUTE_REPARSE_POINT = 0x00000400;

        private const string kernel32Dll = "kernel32.dll";
        private const string mscoreeDLL = "mscoree.dll";

        internal static HandleRef NullHandleRef = new HandleRef(null, IntPtr.Zero);

        internal static IntPtr NullIntPtr = new IntPtr(0);

        // As defined in winnt.h:
        internal const ushort PROCESSOR_ARCHITECTURE_INTEL = 0;
        internal const ushort PROCESSOR_ARCHITECTURE_ARM = 5;
        internal const ushort PROCESSOR_ARCHITECTURE_IA64 = 6;
        internal const ushort PROCESSOR_ARCHITECTURE_AMD64 = 9;

        internal const uint INFINITE = 0xFFFFFFFF;
        internal const uint WAIT_ABANDONED_0 = 0x00000080;
        internal const uint WAIT_OBJECT_0 = 0x00000000;
        internal const uint WAIT_TIMEOUT = 0x00000102;

        #endregion

        #region Enums

        private enum PROCESSINFOCLASS : int
        {
            ProcessBasicInformation = 0,
            ProcessQuotaLimits,
            ProcessIoCounters,
            ProcessVmCounters,
            ProcessTimes,
            ProcessBasePriority,
            ProcessRaisePriority,
            ProcessDebugPort,
            ProcessExceptionPort,
            ProcessAccessToken,
            ProcessLdtInformation,
            ProcessLdtSize,
            ProcessDefaultHardErrorMode,
            ProcessIoPortHandlers, // Note: this is kernel mode only 
            ProcessPooledUsageAndLimits,
            ProcessWorkingSetWatch,
            ProcessUserModeIOPL,
            ProcessEnableAlignmentFaultFixup,
            ProcessPriorityClass,
            ProcessWx86Information,
            ProcessHandleCount,
            ProcessAffinityMask,
            ProcessPriorityBoost,
            MaxProcessInfoClass
        };

        private enum eDesiredAccess : int
        {
            DELETE = 0x00010000,
            READ_CONTROL = 0x00020000,
            WRITE_DAC = 0x00040000,
            WRITE_OWNER = 0x00080000,
            SYNCHRONIZE = 0x00100000,
            STANDARD_RIGHTS_ALL = 0x001F0000,

            PROCESS_TERMINATE = 0x0001,
            PROCESS_CREATE_THREAD = 0x0002,
            PROCESS_SET_SESSIONID = 0x0004,
            PROCESS_VM_OPERATION = 0x0008,
            PROCESS_VM_READ = 0x0010,
            PROCESS_VM_WRITE = 0x0020,
            PROCESS_DUP_HANDLE = 0x0040,
            PROCESS_CREATE_PROCESS = 0x0080,
            PROCESS_SET_QUOTA = 0x0100,
            PROCESS_SET_INFORMATION = 0x0200,
            PROCESS_QUERY_INFORMATION = 0x0400,
            PROCESS_ALL_ACCESS = SYNCHRONIZE | 0xFFF
        }

        /// <summary>
        /// Flags for CoWaitForMultipleHandles
        /// </summary>
        [Flags]
        public enum COWAIT_FLAGS : int
        {
            /// <summary>
            /// Exit when a handle is signaled.
            /// </summary>
            COWAIT_NONE = 0,

            /// <summary>
            /// Exit when all handles are signaled AND a message is received.
            /// </summary>
            COWAIT_WAITALL = 0x00000001,

            /// <summary>
            /// Exit when an RPC call is serviced.
            /// </summary>
            COWAIT_ALERTABLE = 0x00000002
        }

        #endregion

        #region Structs

        /// <summary>
        /// Structure that contain information about the system on which we are running
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal struct SYSTEM_INFO
        {
            // This is a union of a DWORD and a struct containing 2 WORDs.
            internal ushort wProcessorArchitecture;
            internal ushort wReserved;

            internal uint dwPageSize;
            internal IntPtr lpMinimumApplicationAddress;
            internal IntPtr lpMaximumApplicationAddress;
            internal IntPtr dwActiveProcessorMask;
            internal uint dwNumberOfProcessors;
            internal uint dwProcessorType;
            internal uint dwAllocationGranularity;
            internal ushort wProcessorLevel;
            internal ushort wProcessorRevision;
        }


        /// <summary>
        /// Wrap the intptr returned by OpenProcess in a safe handle.
        /// </summary>
        internal class SafeProcessHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            // Create a SafeHandle, informing the base class
            // that this SafeHandle instance "owns" the handle,
            // and therefore SafeHandle should call
            // our ReleaseHandle method when the SafeHandle
            // is no longer in use
            private SafeProcessHandle() : base(true)
            {
            }

            protected override bool ReleaseHandle()
            {
                return CloseHandle(handle);
            }

            [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
            [DllImport("KERNEL32.DLL")]
            private static extern bool CloseHandle(IntPtr hObject);
        }

        /// <summary>
        /// Contains information about the current state of both physical and virtual memory, including extended memory
        /// </summary>
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
        internal class MemoryStatus
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="T:MemoryStatus"/> class.
            /// </summary>
            public MemoryStatus()
            {
#if (CLR2COMPATIBILITY)
            _length = (uint)Marshal.SizeOf(typeof(NativeMethodsShared.MemoryStatus));
#else
            _length = (uint)Marshal.SizeOf<NativeMethodsShared.MemoryStatus>();
#endif
            }

            /// <summary>
            /// Size of the structure, in bytes. You must set this member before calling GlobalMemoryStatusEx. 
            /// </summary>
            private uint _length;

            /// <summary>
            /// Number between 0 and 100 that specifies the approximate percentage of physical 
            /// memory that is in use (0 indicates no memory use and 100 indicates full memory use). 
            /// </summary>
            public uint MemoryLoad;

            /// <summary>
            /// Total size of physical memory, in bytes.
            /// </summary>
            public ulong TotalPhysical;

            /// <summary>
            /// Size of physical memory available, in bytes. 
            /// </summary>
            public ulong AvailablePhysical;

            /// <summary>
            /// Size of the committed memory limit, in bytes. This is physical memory plus the 
            /// size of the page file, minus a small overhead. 
            /// </summary>
            public ulong TotalPageFile;

            /// <summary>
            /// Size of available memory to commit, in bytes. The limit is ullTotalPageFile. 
            /// </summary>
            public ulong AvailablePageFile;

            /// <summary>
            /// Total size of the user mode portion of the virtual address space of the calling process, in bytes. 
            /// </summary>
            public ulong TotalVirtual;

            /// <summary>
            /// Size of unreserved and uncommitted memory in the user mode portion of the virtual 
            /// address space of the calling process, in bytes. 
            /// </summary>
            public ulong AvailableVirtual;

            /// <summary>
            /// Size of unreserved and uncommitted memory in the extended portion of the virtual 
            /// address space of the calling process, in bytes. 
            /// </summary>
            public ulong AvailableExtendedVirtual;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PROCESS_BASIC_INFORMATION
        {
            public IntPtr ExitStatus;
            public IntPtr PebBaseAddress;
            public IntPtr AffinityMask;
            public IntPtr BasePriority;
            public IntPtr UniqueProcessId;
            public IntPtr InheritedFromUniqueProcessId;

            public int Size
            {
                get { return (6 * IntPtr.Size); }
            }
        };

        /// <summary>
        /// Contains information about a file or directory; used by GetFileAttributesEx.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        public struct WIN32_FILE_ATTRIBUTE_DATA
        {
            internal int fileAttributes;
            internal uint ftCreationTimeLow;
            internal uint ftCreationTimeHigh;
            internal uint ftLastAccessTimeLow;
            internal uint ftLastAccessTimeHigh;
            internal uint ftLastWriteTimeLow;
            internal uint ftLastWriteTimeHigh;
            internal uint fileSizeHigh;
            internal uint fileSizeLow;
        }

        /// <summary>
        /// Contains the security descriptor for an object and specifies whether
        /// the handle retrieved by specifying this structure is inheritable.
        /// </summary>
        [StructLayout(LayoutKind.Sequential)]
        internal class SecurityAttributes
        {
            public SecurityAttributes()
            {
#if (CLR2COMPATIBILITY)
            _nLength = (uint)Marshal.SizeOf(typeof(NativeMethodsShared.SecurityAttributes));
#else
            _nLength = (uint)Marshal.SizeOf<NativeMethodsShared.SecurityAttributes>();
#endif
            }

            private uint _nLength;

            public IntPtr lpSecurityDescriptor;

            public bool bInheritHandle;
        }

        #endregion

        #region Member data

        /// <summary>
        /// Default buffer size to use when dealing with the Windows API.
        /// </summary>
        /// <remarks>
        /// This member is intentionally not a constant because we want to allow
        /// unit tests to change it.
        /// </remarks>
        internal static int MAX_PATH = 260;

        /// <summary>
        /// OS name that can be used for the projectImportSearchPaths element
        /// for a toolset
        /// </summary>
        internal static string GetOSNameForExtensionsPath()
        {
#if XPLAT
            return IsOSX ? "osx" : (IsUnix ? "unix" : "windows");
#else
            return "windows";
#endif
        }

        #endregion

        #region Set Error Mode (copied from BCL)

        private static readonly Version s_threadErrorModeMinOsVersion = new Version(6, 1, 0x1db0);

        internal static int SetErrorMode(int newMode)
        {
            if (Environment.OSVersion.Version >= s_threadErrorModeMinOsVersion)
            {
                int num;
                SetErrorMode_Win7AndNewer(newMode, out num);
                return num;
            }
            return SetErrorMode_VistaAndOlder(newMode);
        }

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", EntryPoint = "SetThreadErrorMode", SetLastError = true)]
        private static extern bool SetErrorMode_Win7AndNewer(int newMode, out int oldMode);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", EntryPoint = "SetErrorMode", ExactSpelling = true)]
        private static extern int SetErrorMode_VistaAndOlder(int newMode);

        #endregion

        #region Wrapper methods

        /// <summary>
        /// Really truly non pumping wait.
        /// Raw IntPtrs have to be used, because the marshaller does not support arrays of SafeHandle, only
        /// single SafeHandles.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        public static extern Int32 WaitForMultipleObjects(uint handle, IntPtr[] handles, bool waitAll, uint milliseconds);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern void GetNativeSystemInfo(ref SYSTEM_INFO lpSystemInfo);

        /// <summary>
        /// Get the last write time of the fullpath to a directory. If the pointed path is not a directory, or
        /// if the directory does not exist, then false is returned and fileModifiedTimeUtc is set DateTime.MinValue.
        /// </summary>
        /// <param name="fullPath">Full path to the file in the filesystem</param>
        /// <param name="fileModifiedTimeUtc">The UTC last write time for the directory</param>
        internal static bool GetLastWriteDirectoryUtcTime(string fullPath, out DateTime fileModifiedTimeUtc)
        {
            // This code was copied from the reference mananger, if there is a bug fix in that code, see if the same fix should also be made
            // there

            fileModifiedTimeUtc = DateTime.MinValue;
            WIN32_FILE_ATTRIBUTE_DATA data = new WIN32_FILE_ATTRIBUTE_DATA();
            bool success = false;

            success = GetFileAttributesEx(fullPath, 0, ref data);
            if (success)
            {
                if ((data.fileAttributes & FILE_ATTRIBUTE_DIRECTORY) != 0)
                {
                    long dt = ((long)(data.ftLastWriteTimeHigh) << 32) | ((long)data.ftLastWriteTimeLow);
                    fileModifiedTimeUtc = DateTime.FromFileTimeUtc(dt);
                }
                else
                {
                    // Path does not point to a directory
                    success = false;
                }
            }

            return success;
        }

        /// <summary>
        /// Takes the path and returns the short path
        /// </summary>
        internal static string GetShortFilePath(string path)
        {
            if (path != null)
            {
                int length = GetShortPathName(path, null, 0);
                int errorCode = Marshal.GetLastWin32Error();

                if (length > 0)
                {
                    System.Text.StringBuilder fullPathBuffer = new System.Text.StringBuilder(length);
                    length = GetShortPathName(path, fullPathBuffer, length);
                    errorCode = Marshal.GetLastWin32Error();

                    if (length > 0)
                    {
                        string fullPath = fullPathBuffer.ToString();
                        path = fullPath;
                    }
                }

                if (length == 0 && errorCode != 0)
                {
                    ThrowExceptionForErrorCode(errorCode);
                }
            }

            return path;
        }

        /// <summary>
        /// Takes the path and returns a full path
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        internal static string GetLongFilePath(string path)
        {
            if (path != null)
            {
                int length = GetLongPathName(path, null, 0);
                int errorCode = Marshal.GetLastWin32Error();

                if (length > 0)
                {
                    System.Text.StringBuilder fullPathBuffer = new System.Text.StringBuilder(length);
                    length = GetLongPathName(path, fullPathBuffer, length);
                    errorCode = Marshal.GetLastWin32Error();

                    if (length > 0)
                    {
                        string fullPath = fullPathBuffer.ToString();
                        path = fullPath;
                    }
                }

                if (length == 0 && errorCode != 0)
                {
                    ThrowExceptionForErrorCode(errorCode);
                }
            }

            return path;
        }

        /// <summary>
        /// Retrieves the current global memory status.
        /// </summary>
        internal static MemoryStatus GetMemoryStatus()
        {
            MemoryStatus status = new MemoryStatus();
            bool returnValue = NativeMethodsShared.GlobalMemoryStatusEx(status);
            if (!returnValue)
            {
                return null;
            }

            return status;
        }

        private static readonly bool UseSymlinkTimeInsteadOfTargetTime = Environment.GetEnvironmentVariable("MSBUILDUSESYMLINKTIMESTAMP") == "1";

        /// <summary>
        /// Get the last write time of the fullpath to the file. 
        /// If the file does not exist, then DateTime.MinValue is returned
        /// </summary>
        /// <param name="fullPath">Full path to the file in the filesystem</param>
        /// <returns></returns>
        internal static DateTime GetLastWriteFileUtcTime(string fullPath)
        {
            DateTime fileModifiedTime = DateTime.MinValue;

            if (UseSymlinkTimeInsteadOfTargetTime)
            {
                WIN32_FILE_ATTRIBUTE_DATA data = new WIN32_FILE_ATTRIBUTE_DATA();
                bool success = false;

                success = NativeMethodsShared.GetFileAttributesEx(fullPath, 0, ref data);
                if (success)
                {
                    long dt = ((long) (data.ftLastWriteTimeHigh) << 32) | ((long) data.ftLastWriteTimeLow);
                    fileModifiedTime = DateTime.FromFileTimeUtc(dt);
                }
            }
            else
            {
                using (SafeFileHandle handle =
                    CreateFile(fullPath, GENERIC_READ, FILE_SHARE_READ, IntPtr.Zero, OPEN_EXISTING,
                        FILE_ATTRIBUTE_NORMAL, IntPtr.Zero))
                {
                    if (!handle.IsInvalid)
                    {
                        FILETIME ftCreationTime, ftLastAccessTime, ftLastWriteTime;
                        if (!GetFileTime(handle, out ftCreationTime, out ftLastAccessTime, out ftLastWriteTime) != true)
                        {
                            long fileTime = ((long) (uint) ftLastWriteTime.dwHighDateTime) << 32 |
                                            (long) (uint) ftLastWriteTime.dwLowDateTime;
                            fileModifiedTime =
                                DateTime.FromFileTimeUtc(fileTime);
                        }
                    }
                }
            }

            return fileModifiedTime;
        }

        /// <summary>
        /// Did the HRESULT succeed
        /// </summary>
        public static bool HResultSucceeded(int hr)
        {
            return (hr >= 0);
        }

        /// <summary>
        /// Did the HRESULT Fail
        /// </summary>
        public static bool HResultFailed(int hr)
        {
            return (hr < 0);
        }

        /// <summary>
        /// Given an error code, converts it to an HRESULT and throws the appropriate exception. 
        /// </summary>
        /// <param name="errorCode"></param>
        public static void ThrowExceptionForErrorCode(int errorCode)
        {
            // See ndp\clr\src\bcl\system\io\__error.cs for this code as it appears in the CLR.

            // Something really bad went wrong witht the call
            // translate the error into an exception

            // Convert the errorcode into an HRESULT (See MakeHRFromErrorCode in Win32Native.cs in
            // ndp\clr\src\bcl\microsoft\win32)
            errorCode = unchecked(((int)0x80070000) | errorCode);

            // Throw an exception as best we can
            Marshal.ThrowExceptionForHR(errorCode);
        }

        /// <summary>
        /// Looks for the given file in the system path i.e. all locations in
        /// the %PATH% environment variable.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns>The location of the file, or null if file not found.</returns>
        internal static string FindOnPath(string filename)
        {
            StringBuilder pathBuilder = new StringBuilder(MAX_PATH + 1);
            string pathToFile = null;

            // we may need to make two attempts because there's a small chance
            // the buffer may not be sized correctly the first time
            for (int i = 0; i < 2; i++)
            {
                uint result = SearchPath
                                (
                                    null /* search the system path */,
                                    filename /* look for this file */,
                                    null /* don't add an extra extension to the filename when searching */,
                                    pathBuilder.Capacity /* size of buffer */,
                                    pathBuilder /* buffer to write path into */,
                                    null /* don't want pointer to filename in the return path */
                                );

                // if the buffer is not big enough
                if (result > pathBuilder.Capacity)
                {
                    ErrorUtilities.VerifyThrow(i == 0, "We should not have to resize the buffer twice.");

                    // resize the buffer and try again
                    pathBuilder.Capacity = (int)result;
                }
                else if (result > 0)
                {
                    // file was found, so don't make another attempt
                    pathToFile = pathBuilder.ToString();
                    break;
                }
                else
                {
                    // file was not found, so quit
                    break;
                }
            }

            return pathToFile;
        }

        /// <summary>
        /// Kills the specified process by id and all of its children recursively.
        /// </summary>
        internal static void KillTree(int processIdToKill)
        {
            // Note that GetProcessById does *NOT* internally hold on to the process handle. 
            // Only when you create the process using the Process object
            // does the Process object retain the original handle.

            Process thisProcess = null;
            try
            {
                thisProcess = Process.GetProcessById(processIdToKill);
            }
            catch (ArgumentException)
            {
                // The process has already died for some reason.  So shrug and assume that any child processes 
                // have all also either died or are in the process of doing so. 
                return;
            }

            try
            {
                DateTime myStartTime = thisProcess.StartTime;

                // Grab the process handle.  We want to keep this open for the duration of the function so that
                // it cannot be reused while we are running. 
                SafeProcessHandle hProcess = OpenProcess(eDesiredAccess.PROCESS_QUERY_INFORMATION, false, processIdToKill);
                if (hProcess.IsInvalid)
                {
                    return;
                }

                try
                {
                    try
                    {
                        // Kill this process, so that no further children can be created.
                        thisProcess.Kill();
                    }
                    catch (Win32Exception e)
                    {
                        // Access denied is potentially expected -- it happens when the process that 
                        // we're attempting to kill is already dead.  So just ignore in that case. 
                        if (e.NativeErrorCode != ERROR_ACCESS_DENIED)
                        {
                            throw;
                        }
                    }

                    // Now enumerate our children.  Children of this process are any process which has this process id as its parent
                    // and which also started after this process did.
                    List<KeyValuePair<int, SafeProcessHandle>> children = GetChildProcessIds(processIdToKill, myStartTime);

                    try
                    {
                        foreach (KeyValuePair<int, SafeProcessHandle> childProcessInfo in children)
                        {
                            KillTree(childProcessInfo.Key);
                        }
                    }
                    finally
                    {
                        foreach (KeyValuePair<int, SafeProcessHandle> childProcessInfo in children)
                        {
                            childProcessInfo.Value.Dispose();
                        }
                    }
                }
                finally
                {
                    // Release the handle.  After this point no more children of this process exist and this process has also exited.
                    hProcess.Dispose();
                }
            }
            finally
            {
                thisProcess.Dispose();
            }
        }

        /// <summary>
        /// Returns the parent process id for the specified process.
        /// Returns zero if it cannot be gotten for some reason.
        /// </summary>
        internal static int GetParentProcessId(int processId)
        {
            int ParentID = 0;
            SafeProcessHandle hProcess = OpenProcess(eDesiredAccess.PROCESS_QUERY_INFORMATION, false, processId);

            if (!hProcess.IsInvalid)
            {
                try
                {
                    // UNDONE: NtQueryInformationProcess will fail if we are not elevated and other process is. Advice is to change to use ToolHelp32 API's
                    // For now just return zero and worst case we will not kill some children.
                    PROCESS_BASIC_INFORMATION pbi = new PROCESS_BASIC_INFORMATION();
                    int pSize = 0;

                    if (0 == NtQueryInformationProcess(hProcess, PROCESSINFOCLASS.ProcessBasicInformation, ref pbi, pbi.Size, ref pSize))
                    {
                        ParentID = (int)pbi.InheritedFromUniqueProcessId;
                    }
                }
                finally
                {
                    hProcess.Dispose();
                }
            }

            return (ParentID);
        }

        /// <summary>
        /// Returns an array of all the immediate child processes by id.
        /// NOTE: The IntPtr in the tuple is the handle of the child process.  CloseHandle MUST be called on this.
        /// </summary>
        internal static List<KeyValuePair<int, SafeProcessHandle>> GetChildProcessIds(int parentProcessId, DateTime parentStartTime)
        {
            List<KeyValuePair<int, SafeProcessHandle>> myChildren = new List<KeyValuePair<int, SafeProcessHandle>>();

            foreach (Process possibleChildProcess in Process.GetProcesses())
            {
                using (possibleChildProcess)
                {
                    // Hold the child process handle open so that children cannot die and restart with a different parent after we've started looking at it.
                    // This way, any handle we pass back is guaranteed to be one of our actual children.
                    SafeProcessHandle childHandle = OpenProcess(eDesiredAccess.PROCESS_QUERY_INFORMATION, false, possibleChildProcess.Id);
                    if (childHandle.IsInvalid)
                    {
                        continue;
                    }

                    bool keepHandle = false;
                    try
                    {
                        if (possibleChildProcess.StartTime > parentStartTime)
                        {
                            int childParentProcessId = GetParentProcessId(possibleChildProcess.Id);
                            if (childParentProcessId != 0)
                            {
                                if (parentProcessId == childParentProcessId)
                                {
                                    // Add this one 
                                    myChildren.Add(new KeyValuePair<int, SafeProcessHandle>(possibleChildProcess.Id, childHandle));
                                    keepHandle = true;
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (!keepHandle)
                        {
                            childHandle.Dispose();
                        }
                    }
                }
            }

            return myChildren;
        }

        /// <summary>
        /// Internal, optimized GetCurrentDirectory implementation that simply delegates to the native method
        /// </summary>
        /// <returns></returns>
        internal static string GetCurrentDirectory()
        {
            StringBuilder sb = new StringBuilder(MAX_PATH);
            int pathLength = GetCurrentDirectory(MAX_PATH, sb);

            if (pathLength > 0)
            {
                return sb.ToString();
            }
            else
            {
                return null;
            }
        }

        #endregion

        #region PInvoke

        /// <summary>
        /// Gets the current OEM code page which is used by console apps 
        /// (as opposed to the Windows/ANSI code page used by the normal people)
        /// Basically for each ANSI code page (set in Regional settings) there's a corresponding OEM code page 
        /// that needs to be used for instance when writing to batch files
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport(kernel32Dll)]
        internal static extern int GetOEMCP();

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool GetFileAttributesEx(String name, int fileInfoLevel, ref WIN32_FILE_ATTRIBUTE_DATA lpFileInformation);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport(kernel32Dll, SetLastError = true, CharSet = CharSet.Unicode)]
        private static extern uint SearchPath
        (
            string path,
            string fileName,
            string extension,
            int numBufferChars,
            [Out] StringBuilder buffer,
            int[] filePart
        );

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", PreserveSig = true, SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool FreeLibrary([In] IntPtr module);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", PreserveSig = true, BestFitMapping = false, ThrowOnUnmappableChar = true, CharSet = CharSet.Ansi, SetLastError = true)]
        internal static extern IntPtr GetProcAddress(IntPtr module, string procName);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, PreserveSig = true, SetLastError = true)]
        internal static extern IntPtr LoadLibrary(string fileName);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport(mscoreeDLL, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern uint GetRequestedRuntimeInfo(String pExe,
                                                String pwszVersion,
                                                String pConfigurationFile,
                                                uint startupFlags,
                                                uint runtimeInfoFlags,
                                                [Out] StringBuilder pDirectory,
                                                int dwDirectory,
                                                out uint dwDirectoryLength,
                                                [Out] StringBuilder pVersion,
                                                int cchBuffer,
                                                out uint dwlength);

        /// <summary>
        /// Gets the fully qualified filename of the currently executing .exe
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport(kernel32Dll, SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int GetModuleFileName(HandleRef hModule, [Out] StringBuilder buffer, int length);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll")]
        internal static extern IntPtr GetStdHandle(int nStdHandle);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll")]
        internal static extern uint GetFileType(IntPtr hFile);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api", Justification = "Using unmanaged equivalent for performance reasons")]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static extern int GetCurrentDirectory(int nBufferLength, [Out] StringBuilder lpBuffer);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [SuppressMessage("Microsoft.Usage", "CA2205:UseManagedEquivalentsOfWin32Api", Justification = "Using unmanaged equivalent for performance reasons")]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        [return: MarshalAs(UnmanagedType.Bool)]
        internal static extern bool SetCurrentDirectory(string path);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
        internal static unsafe extern int GetFullPathName(string target, int bufferLength, char* buffer, IntPtr mustBeZero);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("KERNEL32.DLL")]
        private static extern SafeProcessHandle OpenProcess(eDesiredAccess dwDesiredAccess, [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle, int dwProcessId);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("NTDLL.DLL")]
        private static extern int NtQueryInformationProcess(SafeProcessHandle hProcess, PROCESSINFOCLASS pic, ref PROCESS_BASIC_INFORMATION pbi, int cb, ref int pSize);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [return: MarshalAs(UnmanagedType.Bool)]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        private static extern bool GlobalMemoryStatusEx([In, Out] MemoryStatus lpBuffer);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, BestFitMapping = false)]
        internal static extern int GetShortPathName(string path, [Out] System.Text.StringBuilder fullpath, [In] int length);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, BestFitMapping = false)]
        internal static extern int GetLongPathName([In] string path, [Out] System.Text.StringBuilder fullpath, [In] int length);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool CreatePipe(out SafeFileHandle hReadPipe, out SafeFileHandle hWritePipe, SecurityAttributes lpPipeAttributes, int nSize);

        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        internal static extern bool ReadFile(SafeFileHandle hFile, byte[] lpBuffer, uint nNumberOfBytesToRead, out uint lpNumberOfBytesRead, IntPtr lpOverlapped);

        /// <summary>
        /// CoWaitForMultipleHandles allows us to wait in an STA apartment and still service RPC requests from other threads.
        /// VS needs this in order to allow the in-proc compilers to properly initialize, since they will make calls from the
        /// build thread which the main thread (blocked on BuildSubmission.Execute) must service.
        /// </summary>
        [SuppressMessage("Microsoft.Design", "CA1060:MovePInvokesToNativeMethodsClass", Justification = "Class name is NativeMethodsShared for increased clarity")]
        [DllImport("ole32.dll")]
        public static extern int CoWaitForMultipleHandles(COWAIT_FLAGS dwFlags, int dwTimeout, int cHandles, [MarshalAs(UnmanagedType.LPArray)] IntPtr[] pHandles, out int pdwIndex);

        internal const uint GENERIC_READ = 0x80000000;
        internal const uint FILE_SHARE_READ = 0x1;
        internal const uint FILE_ATTRIBUTE_NORMAL = 0x80;
        internal const uint FILE_FLAG_OPEN_REPARSE_POINT = 0x00200000;
        internal const uint OPEN_EXISTING = 3;

        [DllImport("kernel32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall,
            SetLastError = true)]
        internal static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile
            );

        [DllImport("kernel32.dll", SetLastError = true)]
        internal static extern bool GetFileTime(
            SafeFileHandle hFile,
            out FILETIME lpCreationTime,
            out FILETIME lpLastAccessTime,
            out FILETIME lpLastWriteTime
            );

        #endregion

        #region Extensions

        /// <summary>
        /// Waits while pumping APC messages.  This is important if the waiting thread is an STA thread which is potentially
        /// servicing COM calls from other threads.
        /// </summary>
        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle", Scope = "member", Target = "Microsoft.Build.Shared.NativeMethodsShared.#MsgWaitOne(System.Threading.WaitHandle,System.Int32)", Justification = "This is necessary and it has been used for a long time. No need to change it now.")]
        internal static bool MsgWaitOne(this WaitHandle handle)
        {
            return handle.MsgWaitOne(Timeout.Infinite);
        }

        /// <summary>
        /// Waits while pumping APC messages.  This is important if the waiting thread is an STA thread which is potentially
        /// servicing COM calls from other threads.
        /// </summary>
        internal static bool MsgWaitOne(this WaitHandle handle, TimeSpan timeout)
        {
            return MsgWaitOne(handle, (int)timeout.TotalMilliseconds);
        }

        /// <summary>
        /// Waits while pumping APC messages.  This is important if the waiting thread is an STA thread which is potentially
        /// servicing COM calls from other threads.
        /// </summary>
        [SuppressMessage("Microsoft.Reliability", "CA2001:AvoidCallingProblematicMethods", MessageId = "System.Runtime.InteropServices.SafeHandle.DangerousGetHandle", Justification = "Necessary to avoid pumping")]
        internal static bool MsgWaitOne(this WaitHandle handle, int timeout)
        {
            // CoWaitForMultipleHandles allows us to wait in an STA apartment and still service RPC requests from other threads.
            // VS needs this in order to allow the in-proc compilers to properly initialize, since they will make calls from the
            // build thread which the main thread (blocked on BuildSubmission.Execute) must service.
            int waitIndex;
            int returnValue = CoWaitForMultipleHandles(COWAIT_FLAGS.COWAIT_NONE, timeout, 1, new IntPtr[] { handle.SafeWaitHandle.DangerousGetHandle() }, out waitIndex);
            ErrorUtilities.VerifyThrow(returnValue == 0 || ((uint)returnValue == RPC_S_CALLPENDING && timeout != Timeout.Infinite), "Received {0} from CoWaitForMultipleHandles, but expected 0 (S_OK)", returnValue);
            return returnValue == 0;
        }

#endregion
    }
}
