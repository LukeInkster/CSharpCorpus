﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// <summary>Get the reference assembly paths for a given target framework version / moniker.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.Collections.Generic;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;
using SystemProcessorArchitecture = System.Reflection.ProcessorArchitecture;
using System.Text.RegularExpressions;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Returns the reference assembly paths to the various frameworks
    /// </summary>
    public class FindInvalidProjectReferences : TaskExtension
    {
        #region Fields

        ///<summary>
        /// Regex for breaking up the platform moniker
        /// Example: XNA, Version=8.0
        /// </summary>
        private static readonly Regex s_platformMonikerFormat = new Regex
        (
             @"(?<PLATFORMIDENTITY>^[^,]*),\s*Version=(?<PLATFORMVERSION>.*)",
            RegexOptions.IgnoreCase
        );

        /// <summary>
        /// Reference moniker metadata
        /// </summary>
        private static readonly string s_referencePlatformMonikerMetadata = "TargetPlatformMoniker";

        /// <summary>
        /// SimpleName group
        /// </summary>
        private static readonly string s_platformSimpleNameGroup = "PLATFORMIDENTITY";

        /// <summary>
        /// Version group
        /// </summary>
        private static readonly string s_platformVersionGroup = "PLATFORMVERSION";

        #endregion

        #region Properties

        /// <summary>
        /// List of Platform monikers for each referenced project
        /// </summary>
        public ITaskItem[] ProjectReferences
        {
            get;
            set;
        }

        /// <summary>
        /// Target platform version of the current project
        /// </summary>
        [Required]
        public string TargetPlatformVersion
        {
            get;
            set;
        }

        /// <summary>
        /// Target platform identifier of the current project
        /// </summary>
        [Required]
        public string TargetPlatformIdentifier
        {
            get;
            set;
        }

        /// <summary>
        /// Invalid references to be unresolved 
        /// </summary>
        [Output]
        public ITaskItem[] InvalidReferences
        {
            get;
            private set;
        }

        #endregion

        #region ITask Members

        /// <summary>
        /// Execute the task.
        /// </summary>
        public override bool Execute()
        {
            Version targetPlatformVersionAsVersion = null;
            List<ITaskItem> invalidReferences = new List<ITaskItem>();

            Version.TryParse(TargetPlatformVersion, out targetPlatformVersionAsVersion);

            if (ProjectReferences != null)
            {
                foreach (ITaskItem item in ProjectReferences)
                {
                    string referenceIdentity = item.ItemSpec;
                    string referencePlatformMoniker = item.GetMetadata(s_referencePlatformMonikerMetadata);

                    string platform = null;
                    Version version = null;

                    // For each moniker, compare version, issue localized message if the referenced project targets 
                    // a platform with version higher than the current project and make the reference invalid by adding it to
                    // an invalid reference list output
                    if (ParseMoniker(referencePlatformMoniker, out platform, out version))
                    {
                        if (targetPlatformVersionAsVersion < version)
                        {
                            Log.LogWarningWithCodeFromResources("FindInvalidProjectReferences.WarnWhenVersionIsIncompatible", TargetPlatformIdentifier, TargetPlatformVersion, referenceIdentity, referencePlatformMoniker);
                            invalidReferences.Add(item);
                        }
                    }
                }
            }

            InvalidReferences = invalidReferences.ToArray();

            return true;
        }

        /// <summary>
        /// Take the identity and the version of a platform moniker
        /// </summary>
        private static bool ParseMoniker(string reference, out string platformIdentity, out Version platformVersion)
        {
            Match match = s_platformMonikerFormat.Match(reference);

            platformIdentity = String.Empty;
            bool parsedVersion = false;

            platformVersion = null;

            if (match.Success)
            {
                platformIdentity = match.Groups[s_platformSimpleNameGroup].Value.Trim();

                string rawVersion = match.Groups[s_platformVersionGroup].Value.Trim();
                parsedVersion = Version.TryParse(rawVersion, out platformVersion);
            }

            return platformIdentity.Length > 0 && parsedVersion;
        }

        #endregion
    }
}
