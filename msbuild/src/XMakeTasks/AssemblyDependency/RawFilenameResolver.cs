﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Resolve searchpath type {RawFileName}
    /// </summary>
    internal class RawFilenameResolver : Resolver
    {
        /// <summary>
        /// Construct.
        /// </summary>
        /// <param name="searchPathElement"></param>
        /// <param name="getAssemblyName"></param>
        /// <param name="fileExists"></param>
        public RawFilenameResolver(string searchPathElement, GetAssemblyName getAssemblyName, FileExists fileExists, GetAssemblyRuntimeVersion getRuntimeVersion, Version targetedRuntimeVesion)
            : base(searchPathElement, getAssemblyName, fileExists, getRuntimeVersion, targetedRuntimeVesion, ProcessorArchitecture.None, false)
        {
        }


        /// <summary>
        /// Resolve a reference to a specific file name.
        /// </summary>
        /// <param name="assemblyName">The assemblyname of the reference.</param>
        /// <param name="rawFileNameCandidate">The reference's 'include' treated as a raw file name.</param>
        /// <param name="isPrimaryProjectReference">Whether or not this reference was directly from the project file (and therefore not a dependency)</param>
        /// <param name="wantSpecificVersion">Whether an exact version match is requested.</param>
        /// <param name="executableExtensions">Allowed executable extensions.</param>
        /// <param name="hintPath">The item's hintpath value.</param>
        /// <param name="assemblyFolderKey">Like "hklm\Vendor RegKey" as provided to a reference by the &lt;AssemblyFolderKey&gt; on the reference in the project.</param>
        /// <param name="assembliesConsideredAndRejected">Receives the list of locations that this function tried to find the assembly. May be "null".</param>
        /// <param name="foundPath">The path where the file was found.</param>
        /// <param name="userRequestedSpecificFile">Whether or not the user wanted a specific file (for example, HintPath is a request for a specific file)</param>
        /// <returns>True if the file was resolved.</returns>
        public override bool Resolve
        (
            AssemblyNameExtension assemblyName,
            string sdkName,
            string rawFileNameCandidate,
            bool isPrimaryProjectReference,
            bool wantSpecificVersion,
            string[] executableExtensions,
            string hintPath,
            string assemblyFolderKey,
            ArrayList assembliesConsideredAndRejected,

            out string foundPath,
            out bool userRequestedSpecificFile
        )
        {
            foundPath = null;
            userRequestedSpecificFile = false;

            if (rawFileNameCandidate != null)
            {
                // {RawFileName} was passed in.
                if (fileExists(rawFileNameCandidate))
                {
                    userRequestedSpecificFile = true;
                    foundPath = rawFileNameCandidate;
                    return true;
                }
                else
                {
                    if (assembliesConsideredAndRejected != null)
                    {
                        ResolutionSearchLocation considered = null;
                        considered = new ResolutionSearchLocation();
                        considered.FileNameAttempted = rawFileNameCandidate;
                        considered.SearchPath = searchPathElement;
                        considered.Reason = NoMatchReason.NotAFileNameOnDisk;
                        assembliesConsideredAndRejected.Add(considered);
                    }
                }
            }


            return false;
        }
    }
}
