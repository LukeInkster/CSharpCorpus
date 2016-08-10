﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Text;
using System.Resources;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Base class for task that determines the appropriate manifest resource name to 
    /// assign to a given resx or other resource.
    /// </summary>
    public class CreateCSharpManifestResourceName : CreateManifestResourceName
    {
        /// <summary>
        /// Utility function for creating a C#-style manifest name from 
        /// a resource name. 
        /// </summary>
        /// <param name="fileName">The file name of the dependent (usually a .resx)</param>
        /// <param name="linkFileName">The file name of the dependent (usually a .resx)</param>
        /// <param name="rootNamespace">The root namespace (usually from the project file). May be null</param>
        /// <param name="dependentUponFileName">The file name of the parent of this dependency (usually a .cs file). May be null</param>
        /// <param name="binaryStream">File contents binary stream, may be null</param>
        /// <returns>Returns the manifest name</returns>
        override protected string CreateManifestName
        (
            string fileName,
            string linkFileName,
            string rootNamespace,
            string dependentUponFileName,
            Stream binaryStream
        )
        {
            ITaskItem item = null;
            string culture = null;
            if (fileName != null && itemSpecToTaskitem.TryGetValue(fileName, out item))
            {
                culture = item.GetMetadata("Culture");
            }

            /*
                Actual implementation is in a static method called CreateManifestNameImpl.
                The reason is that CreateManifestName can't be static because it is an 
                override of a method declared in the base class, but its convenient 
                to expose a static version anyway for unittesting purposes.
            */
            return CreateCSharpManifestResourceName.CreateManifestNameImpl
            (
                fileName,
                linkFileName,
                PrependCultureAsDirectory,
                rootNamespace,
                dependentUponFileName,
                culture,
                binaryStream,
                this.Log
            );
        }

        /// <summary>
        /// Utility function for creating a C#-style manifest name from 
        /// a resource name. Note that this function attempts to emulate the
        /// Everret implementation of this code which can be found by searching for
        /// ComputeNonWFCResourceName() or ComputeWFCResourceName() in
        /// \vsproject\langproj\langbldmgrsite.cpp
        /// </summary>
        /// <param name="fileName">The file name of the dependent (usually a .resx)</param>
        /// <param name="linkFileName">The file name of the dependent (usually a .resx)</param>
        /// <param name="rootNamespace">The root namespace (usually from the project file). May be null</param>
        /// <param name="prependCultureAsDirectory">should the culture name be prepended to the manifest name as a path</param>
        /// <param name="dependentUponFileName">The file name of the parent of this dependency (usually a .cs file). May be null</param>
        /// <param name="culture">The override culture of this resource, if any</param>
        /// <param name="binaryStream">File contents binary stream, may be null</param>
        /// <param name="log">Task's TaskLoggingHelper, for logging warnings or errors</param>
        /// <returns>Returns the manifest name</returns>
        internal static string CreateManifestNameImpl
        (
            string fileName,
            string linkFileName,
            bool prependCultureAsDirectory, // true by default
            string rootNamespace, // May be null
            string dependentUponFileName, // May be null
            string culture, // may be null 
            Stream binaryStream, // File contents binary stream, may be null
            TaskLoggingHelper log
        )
        {
            // Use the link file name if there is one, otherwise, fall back to file name.
            string embeddedFileName = linkFileName;
            if (embeddedFileName == null || embeddedFileName.Length == 0)
            {
                embeddedFileName = fileName;
            }

            Culture.ItemCultureInfo info = Culture.GetItemCultureInfo(embeddedFileName, dependentUponFileName);

            // If the item has a culture override, respect that. 
            if (!String.IsNullOrEmpty(culture))
            {
                info.culture = culture;
            }

            StringBuilder manifestName = new StringBuilder();
            if (binaryStream != null)
            {
                // Resource depends on a form. Now, get the form's class name fully 
                // qualified with a namespace.
                ExtractedClassName result = CSharpParserUtilities.GetFirstClassNameFullyQualified(binaryStream);

                if (result.IsInsideConditionalBlock && log != null)
                {
                    log.LogWarningWithCodeFromResources("CreateManifestResourceName.DefinitionFoundWithinConditionalDirective", dependentUponFileName, embeddedFileName);
                }

                if (result.Name != null && result.Name.Length > 0)
                {
                    manifestName.Append(result.Name);

                    // Append the culture if there is one.        
                    if (info.culture != null && info.culture.Length > 0)
                    {
                        manifestName.Append(".").Append(info.culture);
                    }
                }
            }

            // If there's no manifest name at this point, then fall back to using the
            // RootNamespace+Filename_with_slashes_converted_to_dots         
            if (manifestName.Length == 0)
            {
                // If Rootnamespace was null, then it wasn't set from the project resourceFile.
                // Empty namespaces are allowed.
                if ((rootNamespace != null) && (rootNamespace.Length > 0))
                {
                    manifestName.Append(rootNamespace).Append(".");
                }

                // Replace spaces in the directory name with underscores. Needed for compatibility with Everett.
                // Note that spaces in the file name itself are preserved.
                string everettCompatibleDirectoryName = CreateManifestResourceName.MakeValidEverettIdentifier(Path.GetDirectoryName(info.cultureNeutralFilename));

                // only strip extension for .resx and .restext files

                string sourceExtension = Path.GetExtension(info.cultureNeutralFilename);
                if (
                        (0 == String.Compare(sourceExtension, ".resx", StringComparison.OrdinalIgnoreCase))
                        ||
                        (0 == String.Compare(sourceExtension, ".restext", StringComparison.OrdinalIgnoreCase))
                        ||
                        (0 == String.Compare(sourceExtension, ".resources", StringComparison.OrdinalIgnoreCase))
                    )
                {
                    manifestName.Append(Path.Combine(everettCompatibleDirectoryName, Path.GetFileNameWithoutExtension(info.cultureNeutralFilename)));

                    // Replace all '\' with '.'
                    manifestName.Replace(Path.DirectorySeparatorChar, '.');
                    manifestName.Replace(Path.AltDirectorySeparatorChar, '.');

                    // Append the culture if there is one.        
                    if (info.culture != null && info.culture.Length > 0)
                    {
                        manifestName.Append(".").Append(info.culture);
                    }

                    // If the original extension was .resources, add it back
                    if (String.Equals(sourceExtension, ".resources", StringComparison.OrdinalIgnoreCase))
                    {
                        manifestName.Append(sourceExtension);
                    }
                }
                else
                {
                    manifestName.Append(Path.Combine(everettCompatibleDirectoryName, Path.GetFileName(info.cultureNeutralFilename)));

                    // Replace all '\' with '.'
                    manifestName.Replace(Path.DirectorySeparatorChar, '.');
                    manifestName.Replace(Path.AltDirectorySeparatorChar, '.');

                    if (prependCultureAsDirectory)
                    {
                        // Prepend the culture as a subdirectory if there is one.        
                        if (info.culture != null && info.culture.Length > 0)
                        {
                            manifestName.Insert(0, Path.DirectorySeparatorChar);
                            manifestName.Insert(0, info.culture);
                        }
                    }
                }
            }

            return manifestName.ToString();
        }

        /// <summary>
        /// Return 'true' if this is a C# source file.
        /// </summary>
        /// <param name="fileName">Name of the candidate source file.</param>
        /// <returns>True, if this is a validate source file.</returns>
        override protected bool IsSourceFile(string fileName)
        {
            string extension = Path.GetExtension(fileName);
            return (String.Compare(extension, ".cs", StringComparison.OrdinalIgnoreCase) == 0);
        }
    }
}
