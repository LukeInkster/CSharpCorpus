﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Loads and stores the contents of Platform.xml</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Xml;
using System.Collections.Generic;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Utilities
{
    /// <summary>
    /// Structure to represent the information contained in Platform.xml
    /// </summary>
    internal class PlatformManifest
    {
        /// <summary>
        /// Location of Platform.xml 
        /// </summary>
        private string _pathToManifest;

        /// <summary>
        /// Constructor
        /// Takes the location of Platform.xml and populates the structure with manifest data
        /// </summary>
        public PlatformManifest(string pathToManifest)
        {
            ErrorUtilities.VerifyThrowArgumentLength(pathToManifest, "pathToManifest");
            _pathToManifest = pathToManifest;
            LoadManifestFile();
        }

        /// <summary>
        /// Platform name
        /// </summary>
        public string Name
        {
            get;
            private set;
        }

        /// <summary>
        /// Platform friendly name
        /// </summary>
        public string FriendlyName
        {
            get;
            private set;
        }

        /// <summary>
        /// Platform version
        /// </summary>
        public string PlatformVersion
        {
            get;
            private set;
        }

        /// <summary>
        /// The platforms that this platform depends on.  
        /// Item1: Platform name
        /// Item2: Platform version
        /// </summary>
        public ICollection<DependentPlatform> DependentPlatforms
        {
            get;
            private set;
        }

        /// <summary>
        /// The contracts contained by this platform
        /// Item1: Contract name
        /// Item2: Contract version
        /// </summary>
        public ICollection<ApiContract> ApiContracts
        {
            get;
            private set;
        }

        /// <summary>
        /// Flag set to true if an exception occurred while reading the manifest
        /// </summary>
        public bool ReadError
        {
            get
            {
                return !String.IsNullOrEmpty(ReadErrorMessage);
            }
        }

        /// <summary>
        /// Message from exception thrown while reading manifest
        /// </summary>
        public string ReadErrorMessage
        {
            get;
            private set;
        }

        /// <summary>
        /// Load content of Platform.xml
        /// </summary>
        private void LoadManifestFile()
        {
            /*
               Platform.xml format: 

               <ApplicationPlatform name="UAP" friendlyName="Universal Application Platform" version="1.0.0.0">
                  <DependentPlatform name="UAP" version="1.0.0.0" />
                  <ContainedApiContracts>
                     <ApiContract name="UAP" version="1.0.0.0" />
                  </ContainedApiContracts>
               </ApplicationPlatform>
             */
            try
            {
                string platformManifestPath = Path.Combine(_pathToManifest, "Platform.xml");

                if (File.Exists(platformManifestPath))
                {
                    XmlDocument doc = new XmlDocument();
                    XmlReaderSettings readerSettings = new XmlReaderSettings();
                    readerSettings.DtdProcessing = DtdProcessing.Ignore;

                    using (XmlReader xmlReader = XmlReader.Create(platformManifestPath, readerSettings))
                    {
                        doc.Load(xmlReader);
                    }

                    XmlElement rootElement = null;

                    foreach (XmlNode childNode in doc.ChildNodes)
                    {
                        if (childNode.NodeType == XmlNodeType.Element &&
                            String.Equals(childNode.Name, Elements.ApplicationPlatform, StringComparison.Ordinal))
                        {
                            rootElement = (XmlElement)childNode;
                            break;
                        }
                    }

                    DependentPlatforms = new List<DependentPlatform>();
                    ApiContracts = new List<ApiContract>();

                    if (rootElement != null)
                    {
                        Name = rootElement.GetAttribute(Attributes.Name);
                        FriendlyName = rootElement.GetAttribute(Attributes.FriendlyName);
                        PlatformVersion = rootElement.GetAttribute(Attributes.Version);

                        foreach (XmlNode childNode in rootElement.ChildNodes)
                        {
                            XmlElement childElement = childNode as XmlElement;
                            if (childElement == null)
                            {
                                continue;
                            }

                            if (ApiContract.IsContainedApiContractsElement(childElement.Name))
                            {
                                ApiContract.ReadContractsElement(childElement, ApiContracts);
                            }
                            else if (String.Equals(childElement.Name, Elements.DependentPlatform, StringComparison.Ordinal))
                            {
                                DependentPlatforms.Add(new DependentPlatform(childElement.GetAttribute(Attributes.Name), childElement.GetAttribute(Attributes.Version)));
                            }
                        }
                    }
                }
                else
                {
                    this.ReadErrorMessage = ResourceUtilities.FormatResourceString("PlatformManifest.MissingPlatformXml", platformManifestPath);
                }
            }
            catch (Exception e)
            {
                if (ExceptionHandling.IsCriticalException(e))
                {
                    throw;
                }

                this.ReadErrorMessage = e.Message;
            }
        }

        /// <summary>
        /// Represents a dependency on another platform
        /// </summary>
        internal struct DependentPlatform
        {
            /// <summary>
            /// Name of the platform on which this platform depends
            /// </summary>
            internal string Name;

            /// <summary>
            /// Version of the platform on which this platform depends 
            /// </summary>
            internal string Version;

            /// <summary>
            /// Constructor
            /// </summary>
            internal DependentPlatform(string name, string version)
            {
                Name = name;
                Version = version;
            }
        }

        /// <summary>
        /// Helper class with element names in Platform.xml
        /// </summary>
        private static class Elements
        {
            /// <summary>
            /// Root element 
            /// </summary>
            public const string ApplicationPlatform = "ApplicationPlatform";

            /// <summary>
            /// Element describing a platform this platform is dependent on
            /// </summary>
            public const string DependentPlatform = "DependentPlatform";
        }

        /// <summary>
        /// Helper class with attribute names in Platform.xml
        /// </summary>
        private static class Attributes
        {
            /// <summary>
            /// Name associated with this element
            /// </summary>
            public const string Name = "name";

            /// <summary>
            /// Friendly name associated with this element
            /// </summary>
            public const string FriendlyName = "friendlyName";

            /// <summary>
            /// Version associated with this element
            /// </summary>
            public const string Version = "version";

            /// <summary>
            /// Architecture associated with this element
            /// </summary>
            public const string Architecture = "architecture";

            /// <summary>
            /// Config associated with this element
            /// </summary>
            public const string Configuration = "configuration";
        }
    }
}