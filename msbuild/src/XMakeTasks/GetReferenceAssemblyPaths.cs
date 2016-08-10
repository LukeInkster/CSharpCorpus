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

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Returns the reference assembly paths to the various frameworks
    /// </summary>
    public class GetReferenceAssemblyPaths : TaskExtension
    {
        #region Data
        /// <summary>
        /// Environment variable name for the override error on missing reference assembly directory.
        /// </summary>
        private const string WARNONNOREFERENCEASSEMBLYDIRECTORY = "MSBUILDWARNONNOREFERENCEASSEMBLYDIRECTORY";

        /// <summary>
        /// This is the sentinel assembly for .NET FX 3.5 SP1
        /// Used to determine if SP1 of 3.5 is installed
        /// </summary>
        private static readonly string s_NET35SP1SentinelAssemblyName = "System.Data.Entity, Version=3.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, processorArchitecture=MSIL";

        /// <summary>
        /// Cache in a static whether or not we have found the 35sp1sentinel assembly.
        /// </summary>
        private static bool? s_net35SP1SentinelAssemblyFound;

        /// <summary>
        /// Hold the reference assembly paths based on the passed in targetframeworkmoniker.
        /// </summary>
        private IList<string> _tfmPaths;

        /// <summary>
        /// Hold the reference assembly paths based on the passed in targetframeworkmoniker without considering any profile passed in.
        /// </summary>
        private IList<string> _tfmPathsNoProfile;

        /// <summary>
        /// Target framework moniker string passed into the task
        /// </summary>
        private string _targetFrameworkMoniker;

        /// <summary>
        /// The root path to use to generate the reference assemblyPaths
        /// </summary>
        private string _rootPath;

        /// <summary>
        /// By default GetReferenceAssemblyPaths performs simple checks
        /// to ensure that certain runtime frameworks are installed depending on the
        /// target framework.
        /// set bypassFrameworkInstallChecks to true in order to bypass those checks.
        /// </summary>
        private bool _bypassFrameworkInstallChecks;

        #endregion

        #region Properties

        /// <summary>
        /// Returns the path based on the passed in TargetFrameworkMoniker. If the TargetFrameworkMoniker is null or empty
        /// this path will be empty.
        /// </summary>
        [Output]
        public string[] ReferenceAssemblyPaths
        {
            get
            {
                if (_tfmPaths != null)
                {
                    string[] pathsToReturn = new string[_tfmPaths.Count];
                    _tfmPaths.CopyTo(pathsToReturn, 0);
                    return pathsToReturn;
                }
                else
                {
                    return new string[0];
                }
            }
        }

        /// <summary>
        /// Returns the path based on the passed in TargetFrameworkMoniker without considering the profile part of the moniker. If the TargetFrameworkMoniker is null or empty
        /// this path will be empty.
        /// </summary>
        [Output]
        public string[] FullFrameworkReferenceAssemblyPaths
        {
            get
            {
                if (_tfmPathsNoProfile != null)
                {
                    string[] pathsToReturn = new string[_tfmPathsNoProfile.Count];
                    _tfmPathsNoProfile.CopyTo(pathsToReturn, 0);
                    return pathsToReturn;
                }
                else
                {
                    return new string[0];
                }
            }
        }

        /// <summary>
        /// The target framework moniker to get the reference assembly paths for
        /// </summary>
        public string TargetFrameworkMoniker
        {
            get
            {
                return _targetFrameworkMoniker;
            }

            set
            {
                _targetFrameworkMoniker = value;
            }
        }

        /// <summary>
        /// The root path to use to generate the reference assembly path
        /// </summary>
        public string RootPath
        {
            get
            {
                return _rootPath;
            }

            set
            {
                _rootPath = value;
            }
        }

        /// <summary>
        /// By default GetReferenceAssemblyPaths performs simple checks
        /// to ensure that certain runtime frameworks are installed depending on the
        /// target framework.
        /// set BypassFrameworkInstallChecks to true in order to bypass those checks.
        /// </summary>        
        public bool BypassFrameworkInstallChecks
        {
            get
            {
                return _bypassFrameworkInstallChecks;
            }

            set
            {
                _bypassFrameworkInstallChecks = value;
            }
        }

        /// <summary>
        /// Gets the display name for the targetframeworkmoniker
        /// </summary>
        [Output]
        public string TargetFrameworkMonikerDisplayName
        {
            get;
            set;
        }

        #endregion

        #region ITask Members

        /// <summary>
        /// If the target framework moniker is set, generate the correct Paths.
        /// </summary>
        public override bool Execute()
        {
            FrameworkNameVersioning moniker = null;
            FrameworkNameVersioning monikerWithNoProfile = null;

            // Are we targeting a profile. 
            bool targetingProfile = false;

            try
            {
                moniker = new FrameworkNameVersioning(TargetFrameworkMoniker);
                targetingProfile = !String.IsNullOrEmpty(moniker.Profile);

                // If we are targeting a profile we need to generate a set of reference assembly paths which describe where the full framework 
                //  exists, to do so we need to get the reference assembly location without the profile as part of the moniker.
                if (targetingProfile)
                {
                    monikerWithNoProfile = new FrameworkNameVersioning(moniker.Identifier, moniker.Version);
                }

                // This is a very specific "hack" to ensure that when we're targeting certain .NET Framework versions that
                // WPF gets to rely on .NET FX 3.5 SP1 being installed on the build machine.
                // This only needs to occur when we are targeting a .NET FX prior to v4.0
                if (!_bypassFrameworkInstallChecks && moniker.Identifier.Equals(".NETFramework", StringComparison.OrdinalIgnoreCase) &&
                    moniker.Version.Major < 4)
                {
                    // We have not got a value for whether or not the 35 sentinel assembly has been found
                    if (!s_net35SP1SentinelAssemblyFound.HasValue)
                    {
                        // get an assemblyname from the string representation of the sentinel assembly name
                        AssemblyNameExtension sentinelAssemblyName = new AssemblyNameExtension(s_NET35SP1SentinelAssemblyName);

                        string path = GlobalAssemblyCache.GetLocation(sentinelAssemblyName, SystemProcessorArchitecture.MSIL, runtimeVersion => "v2.0.50727", new Version("2.0.57027"), false, new FileExists(FileUtilities.FileExistsNoThrow), GlobalAssemblyCache.pathFromFusionName, GlobalAssemblyCache.gacEnumerator, false);
                        s_net35SP1SentinelAssemblyFound = !String.IsNullOrEmpty(path);
                    }

                    // We did not find the SP1 sentinel assembly in the GAC. Therefore we must assume that SP1 isn't installed
                    if (!s_net35SP1SentinelAssemblyFound.Value)
                    {
                        Log.LogErrorWithCodeFromResources("GetReferenceAssemblyPaths.NETFX35SP1NotIntstalled", TargetFrameworkMoniker);
                    }
                }
            }
            catch (ArgumentException e)
            {
                Log.LogErrorWithCodeFromResources("GetReferenceAssemblyPaths.InvalidTargetFrameworkMoniker", TargetFrameworkMoniker, e.Message);
                return false;
            }

            try
            {
                _tfmPaths = GetPaths(_rootPath, moniker);

                if (_tfmPaths != null && _tfmPaths.Count > 0)
                {
                    TargetFrameworkMonikerDisplayName = ToolLocationHelper.GetDisplayNameForTargetFrameworkDirectory(_tfmPaths[0], moniker);
                }

                // If there is a profile get the paths without the profile.
                // There is no point in generating the full framework paths if profile path could not be found.
                if (targetingProfile && _tfmPaths != null)
                {
                    _tfmPathsNoProfile = GetPaths(_rootPath, monikerWithNoProfile);
                }

                // The path with out the profile is just the reference assembly paths.
                if (!targetingProfile)
                {
                    _tfmPathsNoProfile = _tfmPaths;
                }
            }
            catch (Exception e)
            {
                // The reason we need to do exception E here is because we are in a task and have the ability to log the message and give the user 
                // feedback as to its cause, tasks if at all possible should not have exception leave them.
                Log.LogErrorWithCodeFromResources("GetReferenceAssemblyPaths.ProblemGeneratingReferencePaths", TargetFrameworkMoniker, e.Message);

                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                _tfmPathsNoProfile = null;
                TargetFrameworkMonikerDisplayName = null;
            }

            return !Log.HasLoggedErrors;
        }

        /// <summary>
        /// Generate the set of chained reference assembly paths
        /// </summary>
        private IList<String> GetPaths(string rootPath, FrameworkNameVersioning frameworkmoniker)
        {
            IList<String> pathsToReturn = null;

            if (String.IsNullOrEmpty(rootPath))
            {
                pathsToReturn = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkmoniker);
            }
            else
            {
                pathsToReturn = ToolLocationHelper.GetPathToReferenceAssemblies(rootPath, frameworkmoniker);
            }

            // No reference assembly paths could be found, log an error so an invalid build will not be produced.
            // 1/26/16: Note this was changed from a warning to an error (see GitHub #173). Also added the escape hatch 
            // (set MSBUILDWARNONNOREFERENCEASSEMBLYDIRECTORY = 1) in case this causes issues.
            // TODO: This should be removed for Dev15
            if (pathsToReturn.Count == 0)
            {
                var warn = Environment.GetEnvironmentVariable(WARNONNOREFERENCEASSEMBLYDIRECTORY);

                if (string.Equals(warn, "1", StringComparison.Ordinal))
                {
                    Log.LogWarningWithCodeFromResources("GetReferenceAssemblyPaths.NoReferenceAssemblyDirectoryFound", frameworkmoniker.ToString());
                }
                else
                {
                    Log.LogErrorWithCodeFromResources("GetReferenceAssemblyPaths.NoReferenceAssemblyDirectoryFound", frameworkmoniker.ToString());
                }
            }

            return pathsToReturn;
        }

        #endregion
    }
}
