﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.IO;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    public class SGen_Tests
    {
        internal class SGenExtension : SGen
        {
            internal string CommandLine()
            {
                return base.GenerateCommandLineCommands();
            }
        }

        [Fact]
        public void KeyFileQuotedOnCommandLineIfNecessary()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;

            // This should result in a nested, quoted parameter on
            // the command line, which ultimately looks like this:
            //
            //   /compiler:"/keyfile:\"c:\Some Folder\MyKeyFile.snk\""
            //
            sgen.KeyFile = "c:\\Some Folder\\MyKeyFile.snk";

            string commandLine = sgen.CommandLine();

            Assert.True(commandLine.IndexOf("/compiler:\"/keyfile:\\\"" + sgen.KeyFile + "\\\"\"", StringComparison.OrdinalIgnoreCase) >= 0);
        }

        [Fact]
        public void TestKeepFlagTrue()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            sgen.UseKeep = true;

            string commandLine = sgen.CommandLine();

            Assert.True(commandLine.IndexOf("/keep", StringComparison.OrdinalIgnoreCase) >= 0);
        }
        [Fact]
        public void TestKeepFlagFalse()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            sgen.UseKeep = false;

            string commandLine = sgen.CommandLine();

            Assert.True(commandLine.IndexOf("/keep", StringComparison.OrdinalIgnoreCase) < 0);
        }


        [Fact]
        public void TestInputChecks1()
        {
            MockEngine engine = new MockEngine();
            SGenExtension sgen = new SGenExtension();
            sgen.BuildEngine = engine;
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null" + Path.GetInvalidPathChars()[0];
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            // This should result in a quoted parameter...
            sgen.KeyFile = "c:\\Some Folder\\MyKeyFile.snk";
            string commandLine = sgen.CommandLine();
            Assert.Equal(1, engine.Errors);
        }

        [Fact]
        public void TestInputChecks2()
        {
            MockEngine engine = new MockEngine();
            SGenExtension sgen = new SGenExtension();
            sgen.BuildEngine = engine;
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll" + Path.GetInvalidPathChars()[0];
            sgen.ShouldGenerateSerializer = true;
            sgen.UseProxyTypes = false;
            // This should result in a quoted parameter...
            sgen.KeyFile = "c:\\Some Folder\\MyKeyFile.snk";
            string commandLine = sgen.CommandLine();
            Assert.Equal(1, engine.Errors);
        }

        [Fact]
        public void TestInputChecks3()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                MockEngine engine = new MockEngine();
                SGenExtension sgen = new SGenExtension();
                sgen.BuildEngine = engine;
                sgen.BuildAssemblyName = null;
                sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
                sgen.ShouldGenerateSerializer = true;
                sgen.UseProxyTypes = false;
                // This should result in a quoted parameter...
                sgen.KeyFile = "c:\\Some Folder\\MyKeyFile.snk";
                string commandLine = sgen.CommandLine();
            }
           );
        }
        [Fact]
        public void TestInputChecks4()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                MockEngine engine = new MockEngine();
                SGenExtension sgen = new SGenExtension();
                sgen.BuildEngine = engine;
                sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
                sgen.BuildAssemblyPath = null;
                sgen.ShouldGenerateSerializer = true;
                sgen.UseProxyTypes = false;
                // This should result in a quoted parameter...
                sgen.KeyFile = "c:\\Some Folder\\MyKeyFile.snk";

                string commandLine = sgen.CommandLine();
            }
           );
        }
        [Fact]
        public void TestInputPlatform()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.Platform = "x86";
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;

            string commandLine = sgen.CommandLine();

            Assert.True(String.Equals(commandLine, "/assembly:\"C:\\SomeFolder\\MyAsm.dll\\MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\" /compiler:/platform:x86", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void TestInputTypes()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.Types = new string[] { "System.String", "System.Boolean" };
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;

            string commandLine = sgen.CommandLine();

            Assert.True(String.Equals(commandLine, "/assembly:\"C:\\SomeFolder\\MyAsm.dll\\MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\" /type:System.String /type:System.Boolean", StringComparison.OrdinalIgnoreCase));
        }

        [Fact]
        public void TestInputEmptyTypesAndPlatform()
        {
            SGenExtension sgen = new SGenExtension();
            sgen.BuildAssemblyName = "MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null";
            sgen.BuildAssemblyPath = "C:\\SomeFolder\\MyAsm.dll";
            sgen.ShouldGenerateSerializer = true;

            string commandLine = sgen.CommandLine();

            Assert.True(String.Equals(commandLine, "/assembly:\"C:\\SomeFolder\\MyAsm.dll\\MyAsm, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null\"", StringComparison.OrdinalIgnoreCase));
        }
    }
}
