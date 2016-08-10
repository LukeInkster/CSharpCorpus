﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Runtime.InteropServices;
using Microsoft.Build.Framework;

namespace Microsoft.Build.Tasks.Hosting
{
    /*
     * Interface:       ICscHostObject2
     *
     * Defines an interface for the Csc task to communicate with the IDE.  In particular,
     * the Csc task will delegate the actual compilation to the IDE, rather than shelling
     * out to the command-line compilers.
     *
     */
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    [ComVisible(true)]
    [Guid("D6D4E228-259A-4076-B5D0-0627338BCC10")]
    public interface ICscHostObject2 : ICscHostObject
    {
        bool SetWin32Manifest(string win32Manifest);
    }
}
