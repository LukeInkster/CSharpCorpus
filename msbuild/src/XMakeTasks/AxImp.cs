﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>ToolTask that wraps AxImp.exe, which generates Windows forms wrappers for ActiveX controls.</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Resources;
using System.Reflection;
using System.Diagnostics;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Main class for the COM reference resolution task
    /// </summary>
    public sealed partial class ResolveComReference
    {
        /// <summary>
        /// Defines the "AxImp" MSBuild task, which enables using AxImp.exe 
        /// to generate Windows Forms wrappers for ActiveX controls.
        /// </summary>
        internal class AxImp : AxTlbBaseTask
        {
            #region Properties
            /*
        Microsoft (R) .NET ActiveX Control to Windows Forms Assembly Generator
        [Microsoft .Net Framework, Version 4.0.10719.0]
        Copyright (c) Microsoft Corporation.  All rights reserved.


        Generates a Windows Forms Control that wraps ActiveX controls defined in the given OcxName.

        Usage:
           AxImp OcxName [Options]
        Options:
           /out:FileName            File name of assembly to be produced
           /publickey:FileName      File containing strong name public key
           /keyfile:FileName        File containing strong name key pair
           /keycontainer:FileName   Key container holding strong name key pair
           /delaysign               Force strong name delay signing
                                    Used with /keyfile, /keycontainer or /publickey
           /source                  Generate C# source code for Windows Forms wrapper
           /rcw:FileName            Assembly to use for Runtime Callable Wrapper rather than generating new one.
                                    Multiple instances may be specified. Current directory is used for relative paths.
           /nologo                  Prevents AxImp from displaying logo
           /silent                  Prevents AxImp from displaying success message
           /verbose                 Displays extra information
           /? or /help              Display this usage message
     */

            /// <summary>
            /// .ocx File the ActiveX controls being wrapped are defined in.
            /// </summary>
            public string ActiveXControlName
            {
                get { return (string)Bag["ActiveXControlName"]; }
                set { Bag["ActiveXControlName"] = value; }
            }

            /// <summary>
            /// If true, will generate C# source code for the Windows Forms wrapper.
            /// </summary>
            public bool GenerateSource
            {
                get { return GetBoolParameterWithDefault("GenerateSource", false); }
                set { Bag["GenerateSource"] = value; }
            }

            /// <summary>
            /// If true, suppresses displaying the logo
            /// </summary>
            public bool NoLogo
            {
                get { return GetBoolParameterWithDefault("NoLogo", false); }
                set { Bag["NoLogo"] = value; }
            }

            /// <summary>
            /// File name of assembly to be produced.
            /// </summary>
            public string OutputAssembly
            {
                get { return (string)Bag["OutputAssembly"]; }
                set { Bag["OutputAssembly"] = value; }
            }

            /// <summary>
            /// Name of assembly to use as a RuntimeCallableWrapper instead of generating one.
            /// </summary>
            public string RuntimeCallableWrapperAssembly
            {
                get { return (string)Bag["RuntimeCallableWrapperAssembly"]; }
                set { Bag["RuntimeCallableWrapperAssembly"] = value; }
            }

            /// <summary>
            /// If true, prevents AxImp from displaying success message.
            /// </summary>
            public bool Silent
            {
                get { return GetBoolParameterWithDefault("Silent", false); }
                set { Bag["Silent"] = value; }
            }

            /// <summary>
            /// If true, AxImp prints more information.
            /// </summary>
            public bool Verbose
            {
                get { return GetBoolParameterWithDefault("Verbose", false); }
                set { Bag["Verbose"] = value; }
            }

            #endregion // Properties

            #region ToolTask Members

            /// <summary>
            /// Returns the name of the tool to execute
            /// </summary>
            protected override string ToolName
            {
                get { return "AxImp.exe"; }
            }

            /// <summary>
            /// Fills the provided CommandLineBuilderExtension with all the command line options used when
            /// executing this tool
            /// </summary>
            /// <param name="commandLine">Gets filled with command line commands</param>
            protected internal override void AddCommandLineCommands(CommandLineBuilderExtension commandLine)
            {
                // .ocx file being imported
                commandLine.AppendFileNameIfNotNull(ActiveXControlName);

                // options
                commandLine.AppendWhenTrue("/nologo", Bag, "NoLogo");
                commandLine.AppendSwitchIfNotNull("/out:", OutputAssembly);
                commandLine.AppendSwitchIfNotNull("/rcw:", RuntimeCallableWrapperAssembly);
                commandLine.AppendWhenTrue("/silent", Bag, "Silent");
                commandLine.AppendWhenTrue("/source", Bag, "GenerateSource");
                commandLine.AppendWhenTrue("/verbose", Bag, "Verbose");

                base.AddCommandLineCommands(commandLine);
            }

            /// <summary>
            /// Validates the parameters passed to the task
            /// </summary>
            /// <returns>True if parameters are valid</returns>
            protected override bool ValidateParameters()
            {
                // Verify that we were actually passed a .ocx to import
                if (String.IsNullOrEmpty(ActiveXControlName))
                {
                    Log.LogErrorWithCodeFromResources("AxImp.NoInputFileSpecified");
                    return false;
                }

                return base.ValidateParameters();
            }

            #endregion // ToolTask Members
        }
    }
}