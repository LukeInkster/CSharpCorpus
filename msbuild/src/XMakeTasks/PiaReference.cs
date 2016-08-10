﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

// TYPELIBATTR clashes with the one in InteropServices.
using TYPELIBATTR = System.Runtime.InteropServices.ComTypes.TYPELIBATTR;

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /*
     * Class:   PiaReference
     * 
     * COM reference wrapper class for the tlbimp tool using a PIA.
     *
     */
    internal sealed class PiaReference : ComReference
    {
        #region Constructors

        /*
         * Method:  PiaReference constructor
         * 
         */
        internal PiaReference(TaskLoggingHelper taskLoggingHelper, bool silent, ComReferenceInfo referenceInfo, string itemName)
            : base(taskLoggingHelper, silent, referenceInfo, itemName)
        {
            // do nothing
        }

        #endregion

        #region Methods

        /*
         * Method:  Resolve
         * 
         * Gets the resolved assembly path for the typelib wrapper.
         */
        internal override bool FindExistingWrapper(out ComReferenceWrapperInfo wrapperInfo, DateTime componentTimestamp)
        {
            wrapperInfo = null;

            // Let NDP do the dirty work...
            TypeLibConverter converter = new TypeLibConverter();
            string asmName, asmCodeBase;

            if (!converter.GetPrimaryInteropAssembly(ReferenceInfo.attr.guid, ReferenceInfo.attr.wMajorVerNum, ReferenceInfo.attr.wMinorVerNum, ReferenceInfo.attr.lcid,
                out asmName, out asmCodeBase))
            {
                return false;
            }

            // let's try to load the assembly to determine its path and if it's there
            try
            {
                if (asmCodeBase != null && asmCodeBase.Length > 0)
                {
                    Uri uri = new Uri(asmCodeBase);

                    // make sure the PIA can be loaded
                    Assembly assembly = Assembly.UnsafeLoadFrom(uri.LocalPath);

                    // got here? then assembly must have been loaded successfully.
                    wrapperInfo = new ComReferenceWrapperInfo();
                    wrapperInfo.path = uri.LocalPath;
                    wrapperInfo.assembly = assembly;

                    // We need to remember the original assembly name of this PIA in case it gets redirected to a newer 
                    // version and other COM components use that name to reference the PIA. assembly.FullName wouldn't
                    // work here since we'd get the redirected assembly name.
                    wrapperInfo.originalPiaName = new AssemblyNameExtension(AssemblyName.GetAssemblyName(uri.LocalPath));
                }
                else
                {
                    Assembly assembly = Assembly.Load(asmName);

                    // got here? then assembly must have been loaded successfully.
                    wrapperInfo = new ComReferenceWrapperInfo();
                    wrapperInfo.path = assembly.Location;
                    wrapperInfo.assembly = assembly;

                    // We need to remember the original assembly name of this PIA in case it gets redirected to a newer 
                    // version and other COM components use that name to reference the PIA. 
                    wrapperInfo.originalPiaName = new AssemblyNameExtension(asmName, true);
                }
            }
            catch (FileNotFoundException)
            {
                // This means that assembly file cannot be found.
                // We don't need to do anything here; wrapperInfo is not set 
                // and we'll assume that the assembly doesn't exist.
            }
            catch (BadImageFormatException)
            {
                // Similar case as above, except we should additionally warn the user that the assembly file 
                // is not really a valid assembly file.
                if (!Silent)
                {
                    Log.LogWarningWithCodeFromResources("ResolveComReference.BadAssemblyImage", asmName);
                }
            }

            // have we found the wrapper?
            if (wrapperInfo != null)
            {
                return true;
            }

            return false;
        }

        #endregion
    }
}
