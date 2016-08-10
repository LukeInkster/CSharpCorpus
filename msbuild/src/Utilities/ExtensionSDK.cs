﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Internal representation of the extension SDK</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Structure to represent an extension sdk
    /// </summary>
    internal class ExtensionSDK
    {
        /// <summary>
        /// Path to the platform sdk may be null if not a platform sdk.
        /// </summary>
        private string _path;

        /// <summary>
        /// Extension SDK moniker
        /// </summary>
        private string _sdkMoniker;

        /// <summary>
        /// SDK version
        /// </summary>
        private Version _sdkVersion;

        /// <summary>
        /// SDK identifier
        /// </summary>
        private string _sdkIdentifier;

        /// <summary>
        /// Object containing the properties in the SDK manifest
        /// </summary>
        private SDKManifest _manifest = null;

        /// <summary>
        /// Caches minimum Visual Studio version from the manifest
        /// </summary>
        private Version _minVSVersion = null;

        /// <summary>
        /// Caches max platform version from the manifest
        /// </summary>
        private Version _maxPlatformVersion = null;

        /// <summary>
        /// Constructor
        /// </summary>
        public ExtensionSDK(string extensionSdkMoniker, string extensionSdkPath)
        {
            _sdkMoniker = extensionSdkMoniker;
            _path = extensionSdkPath;
        }

        /// <summary>
        /// SDK version from the moniker
        /// </summary>
        public Version Version
        {
            get
            {
                if (_sdkVersion == null)
                {
                    ParseMoniker(_sdkMoniker);
                }

                return _sdkVersion;
            }
        }

        /// <summary>
        /// SDK identifier from the moniker
        /// </summary>
        public string Identifier
        {
            get
            {
                if (_sdkIdentifier == null)
                {
                    ParseMoniker(_sdkMoniker);
                }

                return _sdkIdentifier;
            }
        }

        /// <summary>
        /// The type of the SDK.
        /// </summary>
        public SDKType SDKType
        {
            get
            {
                return Manifest.SDKType;
            }
        }

        /// <summary>
        /// Minimum Visual Studio version from SDKManifest.xml
        /// </summary>
        public Version MinVSVersion
        {
            get
            {
                if (_minVSVersion == null && Manifest.MinVSVersion != null)
                {
                    if (!Version.TryParse(Manifest.MinVSVersion, out _minVSVersion))
                    {
                        _minVSVersion = null;
                    }
                }

                return _minVSVersion;
            }
        }

        /// <summary>
        /// Maximum platform version from SDKManifest.xml
        /// </summary>
        public Version MaxPlatformVersion
        {
            get
            {
                if (_maxPlatformVersion == null && Manifest.MaxPlatformVersion != null)
                {
                    if (!Version.TryParse(Manifest.MaxPlatformVersion, out _maxPlatformVersion))
                    {
                        _maxPlatformVersion = null;
                    }
                }

                return _maxPlatformVersion;
            }
        }

        /// <summary>
        /// Api contracts from the SDKManifest, if any
        /// </summary>
        public ICollection<ApiContract> ApiContracts
        {
            get
            {
                return Manifest.ApiContracts;
            }
        }

        /// <summary>
        /// Reference to the manifest object
        /// Makes sure manifest is instantiated only once
        /// </summary>
        private SDKManifest Manifest
        {
            get
            {
                if (_manifest == null)
                {
                    // Load manifest from disk the first time it is needed
                    _manifest = new SDKManifest(_path);
                }

                return _manifest;
            }
        }

        /// <summary>
        /// Parse SDK moniker
        /// </summary>
        internal void ParseMoniker(string moniker)
        {
            string[] properties = moniker.Split(',');

            foreach (string property in properties)
            {
                string[] words = property.Split('=');

                if (words[0].Trim().StartsWith("Version", StringComparison.OrdinalIgnoreCase))
                {
                    Version ver = null;
                    if (words.Length > 1 && System.Version.TryParse(words[1], out ver))
                    {
                        _sdkVersion = ver;
                    }
                }
                else
                {
                    _sdkIdentifier = words[0];
                }
            }
        }
    }
}