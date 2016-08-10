﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;

using UtilitiesDotNetFrameworkArchitecture = Microsoft.Build.Utilities.DotNetFrameworkArchitecture;
using SharedDotNetFrameworkArchitecture = Microsoft.Build.Shared.DotNetFrameworkArchitecture;
using FrameworkNameVersioning = System.Runtime.Versioning.FrameworkName;
using Microsoft.Win32;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class ToolLocationHelper_Tests
    {
        public ToolLocationHelper_Tests()
        {
            ToolLocationHelper.ClearStaticCaches();
        }

        [Fact]
        public void GetApiContractReferencesHandlesEmptyContracts()
        {
            string[] returnValue = ToolLocationHelper.GetApiContractReferences(Enumerable.Empty<ApiContract>(), String.Empty);
            Assert.Equal(0, returnValue.Length);
        }

        [Fact]
        public void GetApiContractReferencesHandlesNullContracts()
        {
            string[] returnValue = ToolLocationHelper.GetApiContractReferences(null, String.Empty);
            Assert.Equal(0, returnValue.Length);
        }

        [Fact]
        public void GetApiContractReferencesHandlesNonExistingLocation()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string[] returnValue = ToolLocationHelper.GetApiContractReferences(new ApiContract[] { new ApiContract { Name = "Foo", Version = "Bar" } }, tempDirectory);
            Assert.Equal(0, returnValue.Length);
        }

        [Fact]
        public void GetApiContractReferencesFindsWinMDs()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string referenceDirectory = Path.Combine(tempDirectory, @"References\Foo\Bar");

            try
            {
                Directory.CreateDirectory(referenceDirectory);
                File.WriteAllText(Path.Combine(referenceDirectory, "One.winmd"), "First");
                File.WriteAllText(Path.Combine(referenceDirectory, "Two.winmd"), "Second");
                File.WriteAllText(Path.Combine(referenceDirectory, "Three.winmd"), "Third");
                string[] returnValue = ToolLocationHelper.GetApiContractReferences(new ApiContract[] { new ApiContract { Name = "Foo", Version = "Bar" } }, tempDirectory);
                Assert.Equal(3, returnValue.Length);
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }

        [Fact]
        public void GatherExtensionSDKsInvalidVersionDirectory()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string sdkDirectory = Path.Combine(tempDirectory, @"Foo\Bar");

            try
            {
                Directory.CreateDirectory(sdkDirectory);
                DirectoryInfo info = new DirectoryInfo(tempDirectory);
                TargetPlatformSDK sdk = new TargetPlatformSDK("Foo", new Version(), String.Empty);
                ToolLocationHelper.GatherExtensionSDKs(info, sdk);
                Assert.Equal(0, sdk.ExtensionSDKs.Count);
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }

        [Fact]
        public void GatherExtensionSDKsNoManifest()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string sdkDirectory = Path.Combine(tempDirectory, @"Foo\1.0");

            try
            {
                Directory.CreateDirectory(sdkDirectory);
                DirectoryInfo info = new DirectoryInfo(tempDirectory);
                TargetPlatformSDK sdk = new TargetPlatformSDK("Foo", new Version(), String.Empty);
                ToolLocationHelper.GatherExtensionSDKs(info, sdk);
                Assert.Equal(0, sdk.ExtensionSDKs.Count);
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }

        [Fact]
        public void GatherExtensionSDKsEmptyManifest()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string sdkDirectory = Path.Combine(tempDirectory, @"Foo\1.0");

            try
            {
                Directory.CreateDirectory(sdkDirectory);
                File.WriteAllText(Path.Combine(sdkDirectory, "sdkManifest.xml"), "");
                DirectoryInfo info = new DirectoryInfo(tempDirectory);
                TargetPlatformSDK sdk = new TargetPlatformSDK("Foo", new Version(), String.Empty);
                ToolLocationHelper.GatherExtensionSDKs(info, sdk);
                Assert.Equal(1, sdk.ExtensionSDKs.Count);
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }

        [Fact]
        public void GatherExtensionSDKsGarbageManifest()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            string sdkDirectory = Path.Combine(tempDirectory, @"Foo\1.0");

            try
            {
                Directory.CreateDirectory(sdkDirectory);
                File.WriteAllText(Path.Combine(sdkDirectory, "sdkManifest.xml"), "Garbaggggge");
                DirectoryInfo info = new DirectoryInfo(tempDirectory);
                TargetPlatformSDK sdk = new TargetPlatformSDK("Foo", new Version(), String.Empty);
                ToolLocationHelper.GatherExtensionSDKs(info, sdk);
                Assert.Equal(1, sdk.ExtensionSDKs.Count);
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }

        /// <summary>
        /// Verify the case where we ask for a tool using a target framework version of 3.5
        /// We make sure in the fake sdk path we also create a 4.0 folder in order to make sure we do not return that when we only want the bin directory.
        /// </summary>
        [Fact]
        public void VerifyinternalGetPathToDotNetFrameworkSdkFileNot40()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "VGPTDNFSFN40");
            string temp35Directory = Path.Combine(tempDirectory, "bin");
            string temp40Directory = Path.Combine(temp35Directory, "NETFX 4.0 Tools");
            string toolPath = Path.Combine(temp35Directory, "MyTool.exe");
            string toolPath40 = Path.Combine(temp40Directory, "MyTool.exe");

            try
            {
                if (!Directory.Exists(temp35Directory))
                {
                    Directory.CreateDirectory(temp35Directory);
                }

                // Make a .NET 4.0 Tools so that we can make sure that we do not return it if we are not targeting 4.0
                if (!Directory.Exists(temp40Directory))
                {
                    Directory.CreateDirectory(temp40Directory);
                }

                // Write a tool to disk to the existence check works
                File.WriteAllText(toolPath, "Contents");
                File.WriteAllText(toolPath40, "Contents");

                string foundToolPath = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("MyTool.exe", temp35Directory, "x86");
                Assert.NotNull(foundToolPath);
                Assert.True(toolPath.Equals(foundToolPath, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }

        /// <summary>
        /// Make sure that if a unknown framework identifier with a root directory which does not exist in it is passed in then we get an empty list back out.
        /// </summary>
        [Fact]
        public void GetFrameworkIdentifiersNoReferenceAssemblies()
        {
            IList<string> installedIdentifiers = ToolLocationHelper.GetFrameworkIdentifiers("f:\\IDontExistAtAll");
            Assert.Equal(0, installedIdentifiers.Count);
        }

        /// <summary>
        /// When the root does not exist make sure nothing is returned
        /// </summary>
        [Fact]
        public void HighestVersionOfTargetFrameworkIdentifierRootDoesNotExist()
        {
            FrameworkNameVersioning highestMoniker = ToolLocationHelper.HighestVersionOfTargetFrameworkIdentifier("f:\\IDontExistAtAll", ".UnKNownFramework");
            Assert.Null(highestMoniker);
        }

        /// <summary>
        /// When the root contains no folders with versions on them make sure nothing is returned
        /// </summary>
        [Fact]
        public void HighestVersionOfTargetFrameworkIdentifierRootNoVersions()
        {
            string tempPath = Path.GetTempPath();
            string testPath = Path.Combine(tempPath, "HighestVersionOfTargetFrameworkIdentifierRootNoVersions");
            string nonVersionFolder = Path.Combine(testPath, ".UnknownFramework\\NotAVersion");

            if (!Directory.Exists(nonVersionFolder))
            {
                Directory.CreateDirectory(nonVersionFolder);
            }

            FrameworkNameVersioning highestMoniker = ToolLocationHelper.HighestVersionOfTargetFrameworkIdentifier(testPath, ".UnKNownFramework");
            Assert.Null(highestMoniker);
        }


        /// <summary>
        /// If a directory contains multiple versions make sure we pick the highest one.
        /// </summary>
        [Fact]
        public void HighestVersionOfTargetFrameworkIdentifierRootMultipleVersions()
        {
            string tempPath = Path.GetTempPath();
            string testPath = Path.Combine(tempPath, "HighestVersionOfTargetFrameworkIdentifierRootMultipleVersions");
            string folder10 = Path.Combine(testPath, ".UnknownFramework\\v1.0");
            string folder20 = Path.Combine(testPath, ".UnknownFramework\\v2.0");
            string folder40 = Path.Combine(testPath, ".UnknownFramework\\v4.0");


            if (!Directory.Exists(folder10))
            {
                Directory.CreateDirectory(folder10);
            }

            if (!Directory.Exists(folder20))
            {
                Directory.CreateDirectory(folder20);
            }

            if (!Directory.Exists(folder40))
            {
                Directory.CreateDirectory(folder40);
            }

            FrameworkNameVersioning highestMoniker = ToolLocationHelper.HighestVersionOfTargetFrameworkIdentifier(testPath, ".UnKNownFramework");
            Assert.NotNull(highestMoniker);
            Assert.Equal(4, highestMoniker.Version.Major);
        }

        /// <summary>
        /// Verify the case where we ask for a tool using a target framework version of 4.0
        /// </summary>
        [Fact]
        public void VerifyinternalGetPathToDotNetFrameworkSdkFile40()
        {
            string tempDirectory = Path.Combine(Path.GetTempPath(), "VGPTDNFSFN40");
            string temp35Directory = Path.Combine(tempDirectory, "bin");
            string temp40Directory = Path.Combine(temp35Directory, "NETFX 4.0 Tools");
            string toolPath = Path.Combine(temp35Directory, "MyTool.exe");
            string toolPath40 = Path.Combine(temp40Directory, "MyTool.exe");

            try
            {
                if (!Directory.Exists(temp35Directory))
                {
                    Directory.CreateDirectory(temp35Directory);
                }

                // Make a .NET 4.0 Tools so that we can make sure that we do not return it if we are not targeting 4.0
                if (!Directory.Exists(temp40Directory))
                {
                    Directory.CreateDirectory(temp40Directory);
                }

                // Write a tool to disk to the existence check works
                File.WriteAllText(toolPath, "Contents");
                File.WriteAllText(toolPath40, "Contents");

                string foundToolPath = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("MyTool.exe", temp40Directory, "x86");
                Assert.NotNull(foundToolPath);
                Assert.True(toolPath40.Equals(foundToolPath, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }
            }
        }

        /// <summary>
        /// Make sure if null is passed in for any of the arguments that the method returns null and does not crash.
        /// </summary>
        [Fact]
        public void VerifyinternalGetPathToDotNetFrameworkSdkFileNullPassedIn()
        {
            string foundToolPath = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("MyTool.exe", "C:\\Path", null);
            Assert.Null(foundToolPath);

            foundToolPath = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile("MyTool.exe", null, "x86");
            Assert.Null(foundToolPath);

            foundToolPath = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile(null, "c:\\path", "x86");
            Assert.Null(foundToolPath);
        }

        /*
          * Method:   FindFrameworksPathRunningThisTest
          *
          * Our FX path should be resolved as the one we're running on by default
          */
        [Fact]
        public void FindFrameworksPathRunningThisTest()
        {
            string path = FrameworkLocationHelper.FindDotNetFrameworkPath(
                Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName),
                ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Version40),
                new DirectoryExists(ToolLocationHelper_Tests.DirectoryExists),
                new GetDirectories(ToolLocationHelper_Tests.GetDirectories),
                SharedDotNetFrameworkArchitecture.Current
            );

            Assert.Equal(Path.GetDirectoryName(typeof(object).Module.FullyQualifiedName), path);
        }

        /*
         * Method:   FindFrameworksPathRunningUnderWhidbey
         *
         * Search for a whidbey when whidbey is the current version.
         */
        [Fact]
        public void FindFrameworksPathRunningUnderWhidbey()
        {
            string path = FrameworkLocationHelper.FindDotNetFrameworkPath
                (
                    @"{runtime-base}\v1.2.x86dbg",    // Simulate "Whidbey" as the current runtime.
                    "v1.2",
                    new DirectoryExists(ToolLocationHelper_Tests.DirectoryExists),
                    new GetDirectories(ToolLocationHelper_Tests.GetDirectories),
                    SharedDotNetFrameworkArchitecture.Current
                );
            Assert.Equal(@"{runtime-base}\v1.2.x86dbg", path);
        }

        /*
        * Method:   FindFrameworksPathRunningUnderOrcas
        *
        * Search for a whidbey when orcas is the current version.
        */
        [Fact]
        public void FindFrameworksPathRunningUnderOrcas()
        {
            string path = FrameworkLocationHelper.FindDotNetFrameworkPath
                (
                    @"{runtime-base}\v1.3.x86dbg",    // Simulate "Orcas" as the current runtime.
                    "v1.2",                          // But we're looking for "Whidbey"
                    new DirectoryExists(ToolLocationHelper_Tests.DirectoryExists),
                    new GetDirectories(ToolLocationHelper_Tests.GetDirectories),
                    SharedDotNetFrameworkArchitecture.Current
                );
            Assert.Equal(@"{runtime-base}\v1.2.x86fre", path);
        }

        /*
        * Method:   FindFrameworksPathRunningUnderEverett
        *
        * Search for a whidbey when orcas is the current version.
        */
        [Fact]
        public void FindFrameworksPathRunningUnderEverett()
        {
            string path = FrameworkLocationHelper.FindDotNetFrameworkPath
                (
                    @"{runtime-base}\v1.1.x86dbg",    // Simulate "Everett" as the current runtime.
                    "v1.2",                          // But we're looking for "Whidbey"
                    new DirectoryExists(ToolLocationHelper_Tests.DirectoryExists),
                    new GetDirectories(ToolLocationHelper_Tests.GetDirectories),
                    SharedDotNetFrameworkArchitecture.Current
                );

            Assert.Equal(@"{runtime-base}\v1.2.x86fre", path);
        }

        /*
        * Method:   FindPathForNonexistentFrameworks
        *
        * Trying to find a non-existent path should return null.
        */
        [Fact]
        public void FindPathForNonexistentFrameworks()
        {
            string path = FrameworkLocationHelper.FindDotNetFrameworkPath
                (
                    @"{runtime-base}\v1.1",  // Simulate "everett" as the current runtime
                    "v1.3",                 // And we're trying to find "orchas" runtime which isn't installed.
                    new DirectoryExists(ToolLocationHelper_Tests.DirectoryExists),
                    new GetDirectories(ToolLocationHelper_Tests.GetDirectories),
                    SharedDotNetFrameworkArchitecture.Current
                );

            Assert.Equal(null, path);
        }

        /*
        * Method:   FindPathForEverettThatIsntProperlyInstalled
        *
        * Trying to find a path if GetRequestedRuntimeInfo fails and useHeuristic=false should return null.
        */
        [Fact]
        public void FindPathForEverettThatIsntProperlyInstalled()
        {
            string tempPath = Path.GetTempPath();
            string fakeWhidbeyPath = Path.Combine(tempPath, "v2.0.50224");
            string fakeEverettPath = Path.Combine(tempPath, "v1.1.43225");
            Directory.CreateDirectory(fakeEverettPath);

            string path = FrameworkLocationHelper.FindDotNetFrameworkPath
                (
                    fakeWhidbeyPath,  // Simulate "whidbey" as the current runtime
                    "v1.1",                 // We're looking for "everett" 
                    new DirectoryExists(ToolLocationHelper_Tests.DirectoryExists),
                    new GetDirectories(ToolLocationHelper_Tests.GetDirectories),
                    SharedDotNetFrameworkArchitecture.Current
                );

            Directory.Delete(fakeEverettPath);
            Assert.Equal(null, path);
        }

        [Fact]
        public void ExerciseMiscToolLocationHelperMethods()
        {
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Version11), FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV11);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Version20), FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV20);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Version30), FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV30);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Version35), FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV35);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.Version40), FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV40);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkVersionFolderPrefix(TargetDotNetFrameworkVersion.VersionLatest), FrameworkLocationHelper.dotNetFrameworkVersionFolderPrefixV40);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkRootRegistryKey(TargetDotNetFrameworkVersion.VersionLatest), FrameworkLocationHelper.fullDotNetFrameworkRegistryKey);

            Assert.Equal(ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version11), FrameworkLocationHelper.PathToDotNetFrameworkV11);
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20), FrameworkLocationHelper.PathToDotNetFrameworkV20);
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version30), FrameworkLocationHelper.PathToDotNetFrameworkV30);
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35), FrameworkLocationHelper.PathToDotNetFrameworkV35);
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version40), FrameworkLocationHelper.PathToDotNetFrameworkV40);
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.VersionLatest), FrameworkLocationHelper.PathToDotNetFrameworkV40);

            Assert.Equal(
                    ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version11, UtilitiesDotNetFrameworkArchitecture.Bitness32),
                    FrameworkLocationHelper.GetPathToDotNetFrameworkV11(SharedDotNetFrameworkArchitecture.Bitness32)
                );
            Assert.Equal(
                    ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20, UtilitiesDotNetFrameworkArchitecture.Bitness32),
                    FrameworkLocationHelper.GetPathToDotNetFrameworkV20(SharedDotNetFrameworkArchitecture.Bitness32)
                );
            Assert.Equal(
                    ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version30, UtilitiesDotNetFrameworkArchitecture.Bitness32),
                    FrameworkLocationHelper.GetPathToDotNetFrameworkV30(SharedDotNetFrameworkArchitecture.Bitness32)
                );
            Assert.Equal(
                    ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35, UtilitiesDotNetFrameworkArchitecture.Bitness32),
                    FrameworkLocationHelper.GetPathToDotNetFrameworkV35(SharedDotNetFrameworkArchitecture.Bitness32)
                );

            Assert.Equal(
                    ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version40, UtilitiesDotNetFrameworkArchitecture.Bitness32),
                    FrameworkLocationHelper.GetPathToDotNetFrameworkV40(SharedDotNetFrameworkArchitecture.Bitness32)
                );
            Assert.Equal(
                    ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.VersionLatest, UtilitiesDotNetFrameworkArchitecture.Bitness32),
                    FrameworkLocationHelper.GetPathToDotNetFrameworkV40(SharedDotNetFrameworkArchitecture.Bitness32)
                );

            if (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles(x86)")))
            {
                // 64-bit machine, so we should test the 64-bit overloads as well
                Assert.Equal(
                    ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version11, UtilitiesDotNetFrameworkArchitecture.Bitness64),
                    FrameworkLocationHelper.GetPathToDotNetFrameworkV11(SharedDotNetFrameworkArchitecture.Bitness64)
                );
                Assert.Equal(
                        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version20, UtilitiesDotNetFrameworkArchitecture.Bitness64),
                        FrameworkLocationHelper.GetPathToDotNetFrameworkV20(SharedDotNetFrameworkArchitecture.Bitness64)
                    );
                Assert.Equal(
                        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version30, UtilitiesDotNetFrameworkArchitecture.Bitness64),
                        FrameworkLocationHelper.GetPathToDotNetFrameworkV30(SharedDotNetFrameworkArchitecture.Bitness64)
                    );
                Assert.Equal(
                        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version35, UtilitiesDotNetFrameworkArchitecture.Bitness64),
                        FrameworkLocationHelper.GetPathToDotNetFrameworkV35(SharedDotNetFrameworkArchitecture.Bitness64)
                    );

                Assert.Equal(
                        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.Version40, UtilitiesDotNetFrameworkArchitecture.Bitness64),
                        FrameworkLocationHelper.GetPathToDotNetFrameworkV40(SharedDotNetFrameworkArchitecture.Bitness64)
                    );
                Assert.Equal(
                        ToolLocationHelper.GetPathToDotNetFramework(TargetDotNetFrameworkVersion.VersionLatest, UtilitiesDotNetFrameworkArchitecture.Bitness64),
                        FrameworkLocationHelper.GetPathToDotNetFrameworkV40(SharedDotNetFrameworkArchitecture.Bitness64)
                    );
            }
        }

        [Fact]
        public void TestGetPathToBuildToolsFile()
        {
            string net20Path = ToolLocationHelper.GetPathToDotNetFrameworkFile("msbuild.exe", TargetDotNetFrameworkVersion.Version20);

            if (net20Path != null)
            {
                Assert.Equal(net20Path, ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", "2.0"));
            }

            string net35Path = ToolLocationHelper.GetPathToDotNetFrameworkFile("msbuild.exe", TargetDotNetFrameworkVersion.Version35);

            if (net35Path != null)
            {
                Assert.Equal(net35Path, ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", "3.5"));
            }

            Assert.Equal(
                    ToolLocationHelper.GetPathToDotNetFrameworkFile("msbuild.exe", TargetDotNetFrameworkVersion.Version40),
                    ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", "4.0")
                );

            string tv12path = Path.Combine(ProjectCollection.GlobalProjectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion).ToolsPath, "msbuild.exe");

            Assert.Equal(tv12path, ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", ObjectModelHelpers.MSBuildDefaultToolsVersion));
            Assert.Equal(tv12path, ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", ToolLocationHelper.CurrentToolsVersion));
        }

        [Fact]
        public void TestGetPathToBuildToolsFile_32Bit()
        {
            string net20Path = ToolLocationHelper.GetPathToDotNetFrameworkFile("msbuild.exe", TargetDotNetFrameworkVersion.Version20, UtilitiesDotNetFrameworkArchitecture.Bitness32);

            if (net20Path != null)
            {
                Assert.Equal(net20Path, ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", "2.0", UtilitiesDotNetFrameworkArchitecture.Bitness32));
            }

            string net35Path = ToolLocationHelper.GetPathToDotNetFrameworkFile("msbuild.exe", TargetDotNetFrameworkVersion.Version35, UtilitiesDotNetFrameworkArchitecture.Bitness32);

            if (net35Path != null)
            {
                Assert.Equal(net35Path, ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", "3.5", UtilitiesDotNetFrameworkArchitecture.Bitness32));
            }

            Assert.Equal(
                    ToolLocationHelper.GetPathToDotNetFrameworkFile("msbuild.exe", TargetDotNetFrameworkVersion.Version40, UtilitiesDotNetFrameworkArchitecture.Bitness32),
                    ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", "4.0", UtilitiesDotNetFrameworkArchitecture.Bitness32)
                );


            var toolsPath32 = ProjectCollection.GlobalProjectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion).Properties["MSBuildToolsPath32"];
            string tv12path = Path.Combine(Path.GetFullPath(toolsPath32.EvaluatedValue), "msbuild.exe");

            Assert.Equal(tv12path, ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", ObjectModelHelpers.MSBuildDefaultToolsVersion, UtilitiesDotNetFrameworkArchitecture.Bitness32));
            Assert.Equal(tv12path, ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", ToolLocationHelper.CurrentToolsVersion, UtilitiesDotNetFrameworkArchitecture.Bitness32));
        }

        [Fact]
        public void TestGetPathToBuildToolsFile_64Bit()
        {
            if (String.IsNullOrEmpty(Environment.GetEnvironmentVariable("ProgramFiles(x86)")))
            {
                // 32-bit machine, so just ignore
                return;
            }

            string net20Path = ToolLocationHelper.GetPathToDotNetFrameworkFile("msbuild.exe", TargetDotNetFrameworkVersion.Version20, UtilitiesDotNetFrameworkArchitecture.Bitness64);

            if (net20Path != null)
            {
                Assert.Equal(net20Path, ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", "2.0", UtilitiesDotNetFrameworkArchitecture.Bitness64));
            }

            string net35Path = ToolLocationHelper.GetPathToDotNetFrameworkFile("msbuild.exe", TargetDotNetFrameworkVersion.Version35, UtilitiesDotNetFrameworkArchitecture.Bitness64);

            if (net35Path != null)
            {
                Assert.Equal(net35Path, ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", "3.5", UtilitiesDotNetFrameworkArchitecture.Bitness64));
            }

            Assert.Equal(
                    ToolLocationHelper.GetPathToDotNetFrameworkFile("msbuild.exe", TargetDotNetFrameworkVersion.Version40, UtilitiesDotNetFrameworkArchitecture.Bitness64),
                    ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", "4.0", UtilitiesDotNetFrameworkArchitecture.Bitness64)
                );

            var toolsPath32 = ProjectCollection.GlobalProjectCollection.GetToolset(ObjectModelHelpers.MSBuildDefaultToolsVersion).Properties["MSBuildToolsPath32"];
            var toolsPath64 = Path.Combine(Path.GetFullPath(toolsPath32.EvaluatedValue), "amd64");
            var tv12path = Path.Combine(toolsPath64, "msbuild.exe");
            bool created = false;

            try
            {
                // When building normally, the AMD64 folder will not exist. The method we're testing will return null if the path
                // doesn't exist or msbuild.exe is not located in that path.
                if (!Directory.Exists(toolsPath64))
                {
                    Directory.CreateDirectory(toolsPath64);
                    created = true;
                    if (!File.Exists(tv12path))
                    {
                        File.WriteAllText(tv12path, string.Empty);
                    }
                }

                Assert.Equal(tv12path, ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", ObjectModelHelpers.MSBuildDefaultToolsVersion, UtilitiesDotNetFrameworkArchitecture.Bitness64));
                Assert.Equal(tv12path, ToolLocationHelper.GetPathToBuildToolsFile("msbuild.exe", ToolLocationHelper.CurrentToolsVersion, UtilitiesDotNetFrameworkArchitecture.Bitness64));
            }
            finally
            {
                if (created)
                {
                    FileUtilities.DeleteDirectoryNoThrow(toolsPath64, true);
                }
            }
        }

        [Fact]
        public void TestGetDotNetFrameworkSdkRootRegistryKey()
        {
            // Test out of range .net version.
            foreach (var vsVersion in EnumVisualStudioVersions())
            {
                ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey((TargetDotNetFrameworkVersion)99, vsVersion); });
            }

            // Test out of range visual studio version.
            foreach (var dotNetVersion in EnumDotNetFrameworkVersions())
            {
                ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(dotNetVersion, (VisualStudioVersion)99); });
            }

            foreach (var vsVersion in EnumVisualStudioVersions())
            {
                // v1.1
                Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version11, vsVersion), FrameworkLocationHelper.fullDotNetFrameworkRegistryKey);

                // v2.0
                Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version20, vsVersion), FrameworkLocationHelper.fullDotNetFrameworkRegistryKey);

                // v3.0
                ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version30, vsVersion); });

                // v3.5
                Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version35, vsVersion),
                    vsVersion == VisualStudioVersion.Version100 ? FrameworkLocationHelper.fullDotNetFrameworkSdkRegistryKeyV35OnVS10 : FrameworkLocationHelper.fullDotNetFrameworkSdkRegistryKeyV35OnVS11);
            }

            string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK70A = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v7.0A\WinSDK-NetFx40Tools-x86";
            string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK80A = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.0A\WinSDK-NetFx40Tools-x86";
            string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK81A = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.1A\WinSDK-NetFx40Tools-x86";
            string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK46 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\NETFXSDK\4.6\WinSDK-NetFx40Tools-x86";
            string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK461 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\NETFXSDK\4.6.1\WinSDK-NetFx40Tools-x86";
            string fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK462 = @"HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Microsoft SDKs\NETFXSDK\4.6.2\WinSDK-NetFx40Tools-x86";

            // v4.0
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version100), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK70A);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version110), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK80A);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version120), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK81A);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version140), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK46);

            // v4.5
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version100), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK80A);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version110), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK80A);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version120), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK81A);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version140), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK46);

            // v4.5.1
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version100); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version110); });
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version120), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK81A);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version140), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK46);

            // v4.5.2
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version452, VisualStudioVersion.Version100); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version452, VisualStudioVersion.Version110); });
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version452, VisualStudioVersion.Version120), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK81A);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version452, VisualStudioVersion.Version140), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK46);

            // v4.6
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version100); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version110); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version120); });
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version140), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK46);


            // v4.6.1
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.Version100); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.Version110); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.Version120); });
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version461, VisualStudioVersion.Version140), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK461);

            // v4.6.2
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version462, VisualStudioVersion.Version100); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version462, VisualStudioVersion.Version110); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version462, VisualStudioVersion.Version120); });
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkRootRegistryKey(TargetDotNetFrameworkVersion.Version462, VisualStudioVersion.Version150), fullDotNetFrameworkSdkRegistryPathForV4ToolsOnManagedToolsSDK462);
        }

        [Fact]
        public void TestGetDotNetFrameworkSdkInstallKeyValue()
        {
            // Test out of range .net version.
            foreach (var vsVersion in EnumVisualStudioVersions())
            {
                ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue((TargetDotNetFrameworkVersion)99, vsVersion); });
            }

            // Test out of range visual studio version.
            foreach (var dotNetVersion in EnumDotNetFrameworkVersions())
            {
                ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(dotNetVersion, (VisualStudioVersion)99); });
            }

            string InstallationFolder = "InstallationFolder";

            foreach (var vsVersion in EnumVisualStudioVersions())
            {
                // v1.1
                Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version11, vsVersion), FrameworkLocationHelper.dotNetFrameworkSdkInstallKeyValueV11);

                // v2.0
                Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version20, vsVersion), FrameworkLocationHelper.dotNetFrameworkSdkInstallKeyValueV20);

                // v3.0
                ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version30, vsVersion); });

                // v3.5
                Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version35, vsVersion), InstallationFolder);

                // v4.0
                Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version40, vsVersion), InstallationFolder);

                // v4.5
                Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version45, vsVersion), InstallationFolder);
            }

            // v4.5.1
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version100); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version110); });
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version120), InstallationFolder);
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version140), InstallationFolder);

            // v4.6
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version100); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version110); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version120); });
            Assert.Equal(ToolLocationHelper.GetDotNetFrameworkSdkInstallKeyValue(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version140), InstallationFolder);
        }

        [Fact]
        public void GetPathToDotNetFrameworkSdk()
        {
            // Test out of range .net version.
            foreach (var vsVersion in EnumVisualStudioVersions())
            {
                ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetPathToDotNetFrameworkSdk((TargetDotNetFrameworkVersion)99, vsVersion); });
            }

            // Test out of range visual studio version.
            foreach (var dotNetVersion in EnumDotNetFrameworkVersions())
            {
                ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetPathToDotNetFrameworkSdk(dotNetVersion, (VisualStudioVersion)99); });
            }

            string pathToSdk35InstallRoot = Path.Combine(FrameworkLocationHelper.programFiles32, @"Microsoft SDKs\Windows\v7.0A\");
            string pathToSdkV4InstallRootOnVS10 = Path.Combine(FrameworkLocationHelper.programFiles32, @"Microsoft SDKs\Windows\v7.0A\");
            string pathToSdkV4InstallRootOnVS11 = Path.Combine(FrameworkLocationHelper.programFiles32, @"Microsoft SDKs\Windows\v8.0A\");

            // After uninstalling the 4.5 (Dev11) SDK, the Bootstrapper folder is left behind, so we can't 
            // just check for the root folder.
            if (!Directory.Exists(Path.Combine(pathToSdkV4InstallRootOnVS11, "bin")))
            {
                // falls back to the Dev10 location (7.0A)
                pathToSdkV4InstallRootOnVS11 = pathToSdkV4InstallRootOnVS10;
            }

            string pathToSdkV4InstallRootOnVS12 = Path.Combine(FrameworkLocationHelper.programFiles32, @"Microsoft SDKs\Windows\v8.1A\");

            if (!Directory.Exists(pathToSdkV4InstallRootOnVS12))
            {
                // falls back to the Dev11 location (8.0A)
                pathToSdkV4InstallRootOnVS12 = pathToSdkV4InstallRootOnVS11;
            }

            string pathToSdkV4InstallRootOnVS14 = Path.Combine(FrameworkLocationHelper.programFiles32, @"Microsoft SDKs\Windows\v10.0A\");

            foreach (var vsVersion in EnumVisualStudioVersions())
            {
                // v1.1
                Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version11, vsVersion), FrameworkLocationHelper.PathToDotNetFrameworkSdkV11);

                // v2.0
                Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version20, vsVersion), FrameworkLocationHelper.PathToDotNetFrameworkSdkV20);

                // v3.0
                ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version30, vsVersion); });

                // v3.5
                Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version35, vsVersion), pathToSdk35InstallRoot);
            }

            // v4.0
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version100), pathToSdkV4InstallRootOnVS10);
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version110), pathToSdkV4InstallRootOnVS11);
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version120), pathToSdkV4InstallRootOnVS12);
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version140), pathToSdkV4InstallRootOnVS14);

            // v4.5
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version100), pathToSdkV4InstallRootOnVS11);
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version110), pathToSdkV4InstallRootOnVS11);
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version120), pathToSdkV4InstallRootOnVS12);
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version140), pathToSdkV4InstallRootOnVS14);

            // v4.5.1
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version100); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version110); });
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version120), pathToSdkV4InstallRootOnVS12);
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version451, VisualStudioVersion.Version140), pathToSdkV4InstallRootOnVS14);

            // v4.6
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version100); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version110); });
            ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version120); });
            Assert.Equal(ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version46, VisualStudioVersion.Version140), pathToSdkV4InstallRootOnVS14);
        }

#pragma warning disable 618 //The test below tests a deprecated API. We disable the warning for obsolete methods for this particular test

        [Fact]
        public void GetPathToWindowsSdk()
        {
            // Test out of range .net version.
            foreach (var vsVersion in EnumVisualStudioVersions())
            {
                ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetPathToWindowsSdk((TargetDotNetFrameworkVersion)99, vsVersion); });
            }

            string pathToWindowsSdkV80 = GetRegistryValueHelper(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.0", "InstallationFolder");
            string pathToWindowsSdkV81 = GetRegistryValueHelper(RegistryHive.LocalMachine, RegistryView.Registry32, @"SOFTWARE\Microsoft\Microsoft SDKs\Windows\v8.1", "InstallationFolder");

            foreach (var vsVersion in EnumVisualStudioVersions().Concat(new[] { (VisualStudioVersion)99 }))
            {
                // v1.1, v2.0, v3.0, v3.5, v4.0
                foreach (var dotNetVersion in EnumDotNetFrameworkVersions().Where(v => v <= TargetDotNetFrameworkVersion.Version40))
                {
                    ObjectModelHelpers.AssertThrows(typeof(ArgumentException), delegate { ToolLocationHelper.GetPathToWindowsSdk(dotNetVersion, vsVersion); });
                }

                // v4.5
                Assert.Equal(ToolLocationHelper.GetPathToWindowsSdk(TargetDotNetFrameworkVersion.Version45, vsVersion), pathToWindowsSdkV80);

                // v4.5.1
                Assert.Equal(ToolLocationHelper.GetPathToWindowsSdk(TargetDotNetFrameworkVersion.Version451, vsVersion), pathToWindowsSdkV81);

                // v4.6
                Assert.Equal(ToolLocationHelper.GetPathToWindowsSdk(TargetDotNetFrameworkVersion.Version46, vsVersion), pathToWindowsSdkV81);
            }
        }

#pragma warning restore 618

        private static string s_verifyToolsetAndToolLocationHelperProjectCommonContent = @"
                                    string currentInstallFolderLocation = null;

                                    using (RegistryKey baseKey = Registry.LocalMachine.OpenSubKey(""SOFTWARE\\Microsoft\\Microsoft SDKs\\Windows""))
                                    {
                                        if (baseKey != null)
                                        {
                                            object keyValue = baseKey.GetValue(""CurrentInstallFolder"");

                                            if (keyValue != null)
                                            {
                                                currentInstallFolderLocation = keyValue.ToString();
                                            }
                                        }
                                    }

                                    string sdk35ToolsPath = Sdk35ToolsPath == null ? Sdk35ToolsPath : Path.GetFullPath(Sdk35ToolsPath);
                                    string sdk40ToolsPath = Sdk40ToolsPath == null ? Sdk40ToolsPath : Path.GetFullPath(Sdk40ToolsPath);
                                    pathTo35Sdk = pathTo35Sdk == null ? pathTo35Sdk : Path.GetFullPath(pathTo35Sdk);
                                    pathTo40Sdk = pathTo40Sdk == null ? pathTo40Sdk : Path.GetFullPath(pathTo40Sdk);
                                    string currentInstall35Location = null;
                                    string currentInstall40Location = null;

                                    if (currentInstallFolderLocation != null)
                                    {
                                        currentInstall35Location = Path.GetFullPath(Path.Combine(currentInstallFolderLocation, ""bin\\""));
                                        currentInstall40Location = Path.GetFullPath(Path.Combine(currentInstallFolderLocation, ""bin\\NetFX 4.0 Tools\\""));
                                    }

                                    Log.LogMessage(MessageImportance.High, ""SDK35ToolsPath           = {0}"", Sdk35ToolsPath);
                                    Log.LogMessage(MessageImportance.High, ""SDK40ToolsPath           = {0}"", Sdk40ToolsPath);
                                    Log.LogMessage(MessageImportance.High, ""pathTo35Sdk              = {0}"", pathTo35Sdk);
                                    Log.LogMessage(MessageImportance.High, ""pathTo40Sdk              = {0}"", pathTo40Sdk);
                                    Log.LogMessage(MessageImportance.High, ""currentInstall35Location = {0}"", currentInstall35Location);
                                    Log.LogMessage(MessageImportance.High, ""currentInstall40Location = {0}"", currentInstall40Location);

                                    if (!String.Equals(sdk35ToolsPath, pathTo35Sdk, StringComparison.OrdinalIgnoreCase) && 
                                        (currentInstall35Location != null &&  /* this will be null on win8 express since 35 tools and this registry key will not be written, for vsultimate it is written*/      
                                        !String.Equals(currentInstall35Location, pathTo35Sdk, StringComparison.OrdinalIgnoreCase))
                                       )
                                    {
                                        Log.LogError(""Sdk35ToolsPath is incorrect! Registry: {0}  ToolLocationHelper: {1}  CurrentInstallFolder: {2}"", sdk35ToolsPath, pathTo35Sdk, currentInstall35Location);
                                    }

                                    if (!String.Equals(sdk40ToolsPath, pathTo40Sdk, StringComparison.OrdinalIgnoreCase) && 
                                        (currentInstall40Location != null &&  /* this will be null on win8 express since 35 tools and this registry key will not be written, for vsultimate it is written*/      
                                        !String.Equals(currentInstall40Location, pathTo40Sdk, StringComparison.OrdinalIgnoreCase))
                                       )
                                    {
                                        Log.LogError(""Sdk40ToolsPath is incorrect! Registry: {0}  ToolLocationHelper: {1}  CurrentInstallFolder: {2}"", sdk40ToolsPath, pathTo40Sdk, currentInstall40Location);
                                    }
  ";

        [Fact]
        public void VerifyToolsetAndToolLocationHelperAgree()
        {
            string projectContents = ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                        <UsingTask TaskName='VerifySdkPaths' TaskFactory='CodeTaskFactory' AssemblyFile='$(MSBuildToolsPath)\Microsoft.Build.Tasks.Core.dll' >
                         <ParameterGroup>     
                             <Sdk35ToolsPath />
                             <Sdk40ToolsPath />
                             <WindowsSDK80Path />
                          </ParameterGroup>
                            <Task>
                                <Using Namespace='Microsoft.Win32'/>
                                <Code>
                                <![CDATA[
                                    string pathTo35Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile(""gacutil.exe"", TargetDotNetFrameworkVersion.Version35);
                                    if (!String.IsNullOrEmpty(pathTo35Sdk))
                                    {
                                        pathTo35Sdk = Path.GetDirectoryName(pathTo35Sdk) + ""\\"";
                                    }

                                    string pathTo40Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdkFile(""gacutil.exe"", TargetDotNetFrameworkVersion.VersionLatest); 

                                    if (!String.IsNullOrEmpty(pathTo40Sdk))
                                    {
                                        pathTo40Sdk = Path.GetDirectoryName(pathTo40Sdk) + ""\\"";
                                    }

                                    string pathTo81WinSDK = ToolLocationHelper.GetPathToWindowsSdk(TargetDotNetFrameworkVersion.VersionLatest, VisualStudioVersion.VersionLatest);" +
                                    s_verifyToolsetAndToolLocationHelperProjectCommonContent +
                                  @"if (!String.Equals(WindowsSDK80Path, pathTo81WinSDK, StringComparison.OrdinalIgnoreCase))
                                    {
                                        Log.LogError(""WindowsSDK80Path is incorrect! Registry: {0}  ToolLocationHelper: {1}"", WindowsSDK80Path, pathTo81WinSDK);
                                    }

                                    return !Log.HasLoggedErrors;
                                ]]>
                              </Code>
                            </Task>
                        </UsingTask>
                        <Target Name='Build'>
                            <VerifySdkPaths Sdk35ToolsPath='$(Sdk35ToolsPath)' Sdk40ToolsPath='$(Sdk40ToolsPath)' WindowsSDK80Path='$(WindowsSDK80Path)' />
                        </Target>
                    </Project>");

            ILogger logger = new MockLogger();
            ProjectCollection collection = new ProjectCollection();
            Project p = ObjectModelHelpers.CreateInMemoryProject(collection, projectContents, logger);

            bool success = p.Build(logger);

            Assert.True(success); // "Build Failed.  See Std Out for details."
        }

        [Fact]
        public void VerifyToolsetAndToolLocationHelperAgreeWhenVisualStudioVersionIsEmpty()
        {
            string projectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' ToolsVersion='4.0'>
                        <UsingTask TaskName='VerifySdkPaths' TaskFactory='CodeTaskFactory' AssemblyName='Microsoft.Build.Tasks.v4.0, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' >
                         <ParameterGroup>     
                             <Sdk35ToolsPath />
                             <Sdk40ToolsPath />
                             <WindowsSDK80Path />
                          </ParameterGroup>
                            <Task>
                                <Using Namespace='Microsoft.Win32'/>
                                <Code>
                                <![CDATA[
                                    string pathTo35Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version35);
                                    string pathTo40Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40);

                                    pathTo35Sdk = pathTo35Sdk == null ? pathTo35Sdk : Path.Combine(pathTo35Sdk, ""bin\\"");
                                    pathTo40Sdk = pathTo40Sdk == null ? pathTo40Sdk : Path.Combine(pathTo40Sdk, ""bin\\NetFX 4.0 Tools\\"");" +
                                    s_verifyToolsetAndToolLocationHelperProjectCommonContent +
                                  @"return !Log.HasLoggedErrors;
                                ]]>
                              </Code>
                            </Task>
                        </UsingTask>
                        <Target Name='Build'>
                            <VerifySdkPaths Sdk35ToolsPath='$(Sdk35ToolsPath)' Sdk40ToolsPath='$(Sdk40ToolsPath)' WindowsSDK80Path='$(WindowsSDK80Path)' />
                        </Target>
                    </Project>";

            ILogger logger = new MockLogger();

            ProjectCollection collection = new ProjectCollection();
            Project p = ObjectModelHelpers.CreateInMemoryProject(collection, projectContents, logger, "4.0");

            bool success = p.Build(logger);

            Assert.True(success); // "Build Failed.  See Std Out for details."
        }

        [Fact]
        public void VerifyToolsetAndToolLocationHelperAgreeWhenVisualStudioVersionIs10()
        {
            string projectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' ToolsVersion='4.0'>
                        <UsingTask TaskName='VerifySdkPaths' TaskFactory='CodeTaskFactory' AssemblyName='Microsoft.Build.Tasks.v4.0, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' >
                         <ParameterGroup>     
                             <Sdk35ToolsPath />
                             <Sdk40ToolsPath />
                             <WindowsSDK80Path />
                          </ParameterGroup>
                            <Task>
                                <Using Namespace='Microsoft.Win32'/>
                                <Code>
                                <![CDATA[
                                    string pathTo35Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.Version100);
                                    string pathTo40Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version100);

                                    pathTo35Sdk = pathTo35Sdk == null ? pathTo35Sdk : Path.Combine(pathTo35Sdk, ""bin\\"");
                                    pathTo40Sdk = pathTo40Sdk == null ? pathTo40Sdk : Path.Combine(pathTo40Sdk, ""bin\\NetFX 4.0 Tools\\"");" +
                                    s_verifyToolsetAndToolLocationHelperProjectCommonContent +
                                  @"return !Log.HasLoggedErrors;
                                ]]>
                              </Code>
                            </Task>
                        </UsingTask>
                        <Target Name='Build'>
                            <VerifySdkPaths Sdk35ToolsPath='$(Sdk35ToolsPath)' Sdk40ToolsPath='$(Sdk40ToolsPath)' WindowsSDK80Path='$(WindowsSDK80Path)' />
                        </Target>
                    </Project>";

            ILogger logger = new MockLogger();
            IDictionary<string, string> globalProperties = new Dictionary<string, string>();
            globalProperties.Add("VisualStudioVersion", "10.0");

            ProjectCollection collection = new ProjectCollection(globalProperties);
            Project p = ObjectModelHelpers.CreateInMemoryProject(collection, projectContents, logger, "4.0");

            bool success = p.Build(logger);

            Assert.True(success); // "Build Failed.  See Std Out for details."
        }

        [Fact]
        public void VerifyToolsetAndToolLocationHelperAgreeWhenVisualStudioVersionIs11()
        {
            string projectContents = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' ToolsVersion='4.0'>
                        <UsingTask TaskName='VerifySdkPaths' TaskFactory='CodeTaskFactory' AssemblyName='Microsoft.Build.Tasks.v4.0, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a' >
                         <ParameterGroup>     
                             <Sdk35ToolsPath />
                             <Sdk40ToolsPath />
                             <WindowsSDK80Path />
                          </ParameterGroup>
                            <Task>
                                <Using Namespace='Microsoft.Win32'/>
                                <Code>
                                <![CDATA[
                                    string pathTo35Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version35, VisualStudioVersion.Version110);
                                    string pathTo40Sdk = ToolLocationHelper.GetPathToDotNetFrameworkSdk(TargetDotNetFrameworkVersion.Version40, VisualStudioVersion.Version110);
                                    string pathTo80WinSDK = ToolLocationHelper.GetPathToWindowsSdk(TargetDotNetFrameworkVersion.Version45, VisualStudioVersion.Version110);

                                    pathTo35Sdk = pathTo35Sdk == null ? pathTo35Sdk : Path.Combine(pathTo35Sdk, ""bin\\"");
                                    pathTo40Sdk = pathTo40Sdk == null ? pathTo40Sdk : Path.Combine(pathTo40Sdk, ""bin\\NetFX 4.0 Tools\\"");" +
                                    s_verifyToolsetAndToolLocationHelperProjectCommonContent +
                                   @"if (String.IsNullOrEmpty(WindowsSDK80Path))
                                    {
                                        Log.LogWarning(""WindowsSDK80Path is empty, which is technically not correct, but we're letting it slide for now because the OTG build won't have the updated registry for a while.  Make sure we don't see this warning on PURITs runs, though!"");
                                    }
                                    else if (!String.Equals(WindowsSDK80Path, pathTo80WinSDK, StringComparison.OrdinalIgnoreCase))
                                    {
                                        Log.LogError(""WindowsSDK80Path is incorrect! Registry: {0}  ToolLocationHelper: {1}"", WindowsSDK80Path, pathTo80WinSDK);
                                    }

                                    return !Log.HasLoggedErrors;
                                ]]>
                             </Code>
                            </Task>
                        </UsingTask>
                        <Target Name='Build'>
                            <VerifySdkPaths Sdk35ToolsPath='$(Sdk35ToolsPath)' Sdk40ToolsPath='$(Sdk40ToolsPath)' WindowsSDK80Path='$(WindowsSDK80Path)' />
                        </Target>
                    </Project>";

            ILogger logger = new MockLogger();
            IDictionary<string, string> globalProperties = new Dictionary<string, string>();
            globalProperties.Add("VisualStudioVersion", "11.0");

            ProjectCollection collection = new ProjectCollection(globalProperties);
            Project p = ObjectModelHelpers.CreateInMemoryProject(collection, projectContents, logger, "4.0");

            bool success = p.Build(logger);

            Assert.True(success); // "Build Failed.  See Std Out for details."
        }

        #region GenerateReferenceAssemblyPath
        [Fact]
        public void GenerateReferencAssemblyPathAllElements()
        {
            string targetFrameworkRootPath = "c:\\Program Files\\Reference Assemblies\\Microsoft\\Framework";
            string targetFrameworkIdentifier = "Compact Framework";
            Version targetFrameworkVersion = new Version("1.0");
            string targetFrameworkProfile = "PocketPC";

            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning(targetFrameworkIdentifier, targetFrameworkVersion, targetFrameworkProfile);

            string expectedPath = Path.Combine(targetFrameworkRootPath, targetFrameworkIdentifier);
            expectedPath = Path.Combine(expectedPath, "v" + targetFrameworkVersion.ToString());
            expectedPath = Path.Combine(expectedPath, "Profile");
            expectedPath = Path.Combine(expectedPath, targetFrameworkProfile);

            string path = FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
            Assert.True(String.Equals(expectedPath, path, StringComparison.InvariantCultureIgnoreCase));
        }

        [Fact]
        public void GenerateReferencAssemblyPathNoProfile()
        {
            string targetFrameworkRootPath = "c:\\Program Files\\Reference Assemblies\\Microsoft\\Framework";
            string targetFrameworkIdentifier = "Compact Framework";
            Version targetFrameworkVersion = new Version("1.0");
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning(targetFrameworkIdentifier, targetFrameworkVersion, String.Empty);
            string expectedPath = Path.Combine(targetFrameworkRootPath, targetFrameworkIdentifier);
            expectedPath = Path.Combine(expectedPath, "v" + targetFrameworkVersion.ToString());

            string path = FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
            Assert.True(String.Equals(expectedPath, path, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Make sure if the profile has invalid chars which would be used as part of path generation that we get an InvalidOperationException
        /// which indicates there was a problem generating the reference assembly path.
        /// </summary>
        [Fact]
        public void GenerateReferencAssemblyInvalidProfile()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                string targetFrameworkRootPath = "c:\\Program Files\\Reference Assemblies\\Microsoft\\Framework";
                string targetFrameworkIdentifier = "Compact Framework";
                Version targetFrameworkVersion = new Version("1.0");
                string targetFrameworkProfile = "PocketPC" + new String(Path.GetInvalidFileNameChars());

                FrameworkNameVersioning frameworkName = new FrameworkNameVersioning(targetFrameworkIdentifier, targetFrameworkVersion, targetFrameworkProfile);

                string path = FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
            }
           );
        }
        /// <summary>
        /// Make sure if the identifier has invalid chars which would be used as part of path generation that we get an InvalidOperationException
        /// which indicates there was a problem generating the reference assembly path.
        /// </summary>
        [Fact]
        public void GenerateReferencAssemblyInvalidIdentifier()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                string targetFrameworkRootPath = "c:\\Program Files\\Reference Assemblies\\Microsoft\\Framework";
                string targetFrameworkIdentifier = "Compact Framework" + new String(Path.GetInvalidFileNameChars());
                Version targetFrameworkVersion = new Version("1.0");
                string targetFrameworkProfile = "PocketPC";

                FrameworkNameVersioning frameworkName = new FrameworkNameVersioning(targetFrameworkIdentifier, targetFrameworkVersion, targetFrameworkProfile);

                string path = FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
            }
           );
        }
        /// <summary>
        /// Make sure if the moniker and the root make a too long path that an InvalidOperationException is raised
        /// which indicates there was a problem generating the reference assembly path.
        /// </summary>
        [Fact]
        public void GenerateReferencAssemblyPathTooLong()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                string pathTooLong = new String('a', 500);

                string targetFrameworkRootPath = "c:\\Program Files\\Reference Assemblies\\Microsoft\\Framework";
                string targetFrameworkIdentifier = "Compact Framework" + pathTooLong;
                Version targetFrameworkVersion = new Version("1.0");
                string targetFrameworkProfile = "PocketPC";

                FrameworkNameVersioning frameworkName = new FrameworkNameVersioning(targetFrameworkIdentifier, targetFrameworkVersion, targetFrameworkProfile);

                string path = FrameworkLocationHelper.GenerateReferenceAssemblyPath(targetFrameworkRootPath, frameworkName);
            }
           );
        }
        #endregion

        #region ChainReferenceAssemblyPath

        /// <summary>
        /// Verify the chaining method returns a null if there is no redist list file for the framework we are trying to chaing with. This is ok because the lack of a redist list file means we
        /// do not have anything to chain with.
        /// </summary>
        [Fact]
        public void ChainReferenceAssembliesRedistExistsNoRedistList()
        {
            string path = ToolLocationHelper.ChainReferenceAssemblyPath(@"PathDoesNotExistSoICannotChain");
            Assert.Null(path); // " Expected the path to be null when the path to the FrameworkList.xml does not exist"
        }

        /// <summary>
        /// Verify we do not hang, crash, go on forever if there is a circular reference with the include frameworks. What should happen is 
        /// we should notice that we have already chained to a given framework and not try and chain with it again.
        /// </summary>
        [Fact]
        public void ChainReferenceAssembliesRedistExistsCircularRefernce()
        {
            string redistString41 = "<FileList Redist='Random' IncludeFramework='v4.0'>" +
                                     "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                  "</FileList >";

            string redistString40 = "<FileList Redist='Random'>" +
                                       "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                    "</FileList >";

            string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistExistsChain");

            string redist41Directory = Path.Combine(tempDirectory, "v4.1\\RedistList\\");
            string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
            string redist40Directory = Path.Combine(tempDirectory, "v4.0\\RedistList\\");
            string redist40 = Path.Combine(redist40Directory, "FrameworkList.xml");
            try
            {
                Directory.CreateDirectory(redist41Directory);
                Directory.CreateDirectory(redist40Directory);
                File.WriteAllText(redist40, redistString40);
                File.WriteAllText(redist41, redistString41);

                string path = ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.1"));

                string expectedChainedPath = Path.Combine(tempDirectory, "v4.0");
                Assert.True(String.Equals(path, expectedChainedPath, StringComparison.InvariantCultureIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(redist40Directory))
                {
                    Directory.Delete(redist40Directory, true);
                }

                if (Directory.Exists(redist41Directory))
                {
                    Directory.Delete(redist41Directory, true);
                }
            }
        }

        /// <summary>
        /// Verify the case where there is no Inclded framework attribute, there should be no errors and we should continue on as if there were no further framework chained with the current one
        /// </summary>
        [Fact]
        public void ChainReferenceAssembliesRedistExistsNoInclude()
        {
            string redistString41 = "<FileList Redist='Random'>" +
                                        "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                     "</FileList >";

            string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistExistsNoInclude");

            string redist41Directory = Path.Combine(tempDirectory, "v4.1\\RedistList\\");
            string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
            try
            {
                Directory.CreateDirectory(redist41Directory);
                File.WriteAllText(redist41, redistString41);
                string path = ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.1"));
                Assert.Equal(path, String.Empty); // "Expected the path to be empty"
            }
            finally
            {
                if (Directory.Exists(redist41Directory))
                {
                    Directory.Delete(redist41Directory, true);
                }
            }
        }

        /// <summary>
        /// Verify the case where the include framework is empty, this is ok, we should error but should just continue on as if there was no chaining of the redist list file.
        /// </summary>
        [Fact]
        public void ChainReferenceAssembliesRedistExistsEmptyInclude()
        {
            string redistString41 = "<FileList Redist='Random' IncludeFramework=''>" +
                                        "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                     "</FileList >";

            string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistExistsNoInclude");

            string redist41Directory = Path.Combine(tempDirectory, "v4.1\\RedistList\\");
            string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
            try
            {
                Directory.CreateDirectory(redist41Directory);
                File.WriteAllText(redist41, redistString41);
                string path = ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.1"));
                Assert.Equal(path, String.Empty); // "Expected the path to be empty"
            }
            finally
            {
                if (Directory.Exists(redist41Directory))
                {
                    Directory.Delete(redist41Directory, true);
                }
            }
        }

        /// <summary>
        /// Verify the case where the redist is a valid xml file but does not have the FileListElement, this is to make sure we do not crash or get an exception if the FileList element cannot be found
        /// </summary>
        [Fact]
        public void ChainReferenceAssembliesRedistExistsNoFileList()
        {
            string redistString41 = "<FileListNOT Redist='Random'>" +
                                        "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                     "</FileListNOT >";

            string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistExistsNoFileList");

            string redist41Directory = Path.Combine(tempDirectory, "v4.1\\RedistList\\");
            string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
            try
            {
                Directory.CreateDirectory(redist41Directory);
                File.WriteAllText(redist41, redistString41);
                string path = ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.1"));
                Assert.Equal(path, String.Empty); // "Expected the path to be empty"
            }
            finally
            {
                if (Directory.Exists(redist41Directory))
                {
                    Directory.Delete(redist41Directory, true);
                }
            }
        }

        /// <summary>
        /// Make sure we get the correct exception when there is no xml in the redist list file
        /// </summary>
        [Fact]
        public void ChainReferenceAssembliesRedistExistsBadFile()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                string redistString40 = "GARBAGE";
                string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistExistsBadFile");

                string redist40Directory = Path.Combine(tempDirectory, "v4.0\\RedistList\\");
                string redist40 = Path.Combine(redist40Directory, "FrameworkList.xml");
                try
                {
                    Directory.CreateDirectory(redist40Directory);
                    File.WriteAllText(redist40, redistString40);

                    string path = ToolLocationHelper.ChainReferenceAssemblyPath(Path.Combine(tempDirectory, "v4.0"));
                    Assert.Null(path); // "Expected the path to be null"
                }
                finally
                {
                    if (Directory.Exists(redist40Directory))
                    {
                        Directory.Delete(redist40Directory, true);
                    }
                }
            }
           );
        }
        /// <summary>
        /// Make sure we get the correct exception when the xml file points to an included framwork which does not exist.
        /// </summary>
        [Fact]
        public void ChainReferenceAssembliesRedistPointsToInvalidInclude()
        {
            string redistString41 = "<FileList Redist='Random' IncludeFramework='IDontExist'>" +
                                              "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                           "</FileList>";

            string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistPointsToInvalidInclude");

            string redist41Directory = Path.Combine(tempDirectory, "v4.1\\RedistList\\");
            string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
            string tempDirectoryPath = Path.Combine(tempDirectory, "v4.1");
            try
            {
                Directory.CreateDirectory(redist41Directory);
                File.WriteAllText(redist41, redistString41);

                string path = ToolLocationHelper.ChainReferenceAssemblyPath(tempDirectoryPath);
                Assert.Null(path);
            }
            finally
            {
                if (Directory.Exists(redist41Directory))
                {
                    Directory.Delete(redist41Directory, true);
                }
            }
        }

        /// <summary>
        /// Make sure we get the correct exception when the xml file points to an included framwork which has invalid path chars.
        /// </summary>
        [Fact]
        public void ChainReferenceAssembliesRedistInvalidPathChars()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                char[] invalidFileNameChars = Path.GetInvalidFileNameChars();

                string redistString41 = "<FileList Redist='Random' IncludeFramework='" + new string(invalidFileNameChars) + "'>" +
                                                  "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                               "</FileList>";

                string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistInvalidPathChars");

                string redist41Directory = Path.Combine(tempDirectory, "v4.1\\RedistList\\");
                string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
                string tempDirectoryPath = Path.Combine(tempDirectory, "v4.1");
                try
                {
                    Directory.CreateDirectory(redist41Directory);
                    File.WriteAllText(redist41, redistString41);

                    string path = ToolLocationHelper.ChainReferenceAssemblyPath(tempDirectoryPath);
                }
                finally
                {
                    if (Directory.Exists(redist41Directory))
                    {
                        Directory.Delete(redist41Directory, true);
                    }
                }
            }
           );
        }
        /// <summary>
        /// Make sure we get the correct exception when the xml file points to an included framwork which has invalid path chars.
        /// </summary>
        [Fact]
        public void ChainReferenceAssembliesRedistPathTooLong()
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                string tooLong = new String('a', 500);
                string redistString41 = "<FileList Redist='Random' IncludeFramework='" + tooLong + "'>" +
                                                  "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                               "</FileList>";

                string tempDirectory = Path.Combine(Path.GetTempPath(), "ChainReferenceAssembliesRedistPathTooLong");

                string redist41Directory = Path.Combine(tempDirectory, "v4.1\\RedistList\\");
                string redist41 = Path.Combine(redist41Directory, "FrameworkList.xml");
                string tempDirectoryPath = Path.Combine(tempDirectory, "v4.1");
                try
                {
                    Directory.CreateDirectory(redist41Directory);
                    File.WriteAllText(redist41, redistString41);

                    string path = ToolLocationHelper.ChainReferenceAssemblyPath(tempDirectoryPath);
                }
                finally
                {
                    if (Directory.Exists(redist41Directory))
                    {
                        Directory.Delete(redist41Directory, true);
                    }
                }
            }
           );
        }
        #endregion

        #region GetReferenceAssemblyPathWithRootPath

        /// <summary>
        /// Verify the case where we are chaining redist lists and they are properly formatted
        /// </summary>
        [Fact]
        public void GetPathToReferenceAssembliesWithRootGoodWithChain()
        {
            string redistString41 = "<FileList Redist='Random' IncludeFramework='v4.0'>" +
                                     "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                  "</FileList >";

            string redistString40 = "<FileList Redist='Random' IncludeFramework='v3.9'>" +
                                       "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                    "</FileList >";

            string redistString39 = "<FileList Redist='Random'>" +
                                         "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                      "</FileList >";

            string tempDirectory = Path.Combine(Path.GetTempPath(), "GetPathToReferenceAssembliesWithRootGoodWithChain");

            string framework41Directory = Path.Combine(tempDirectory, "MyFramework\\v4.1\\");
            string framework41redistDirectory = Path.Combine(framework41Directory, "RedistList");
            string framework41RedistList = Path.Combine(framework41redistDirectory, "FrameworkList.xml");

            string framework40Directory = Path.Combine(tempDirectory, "MyFramework\\v4.0\\");
            string framework40redistDirectory = Path.Combine(framework40Directory, "RedistList");
            string framework40RedistList = Path.Combine(framework40redistDirectory, "FrameworkList.xml");

            string framework39Directory = Path.Combine(tempDirectory, "MyFramework\\v3.9\\");
            string framework39redistDirectory = Path.Combine(framework39Directory, "RedistList");
            string framework39RedistList = Path.Combine(framework39redistDirectory, "FrameworkList.xml");


            try
            {
                Directory.CreateDirectory(framework41redistDirectory);
                Directory.CreateDirectory(framework40redistDirectory);
                Directory.CreateDirectory(framework39redistDirectory);

                File.WriteAllText(framework39RedistList, redistString39);
                File.WriteAllText(framework40RedistList, redistString40);
                File.WriteAllText(framework41RedistList, redistString41);


                FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("MyFramework", new Version("4.1"));
                IList<string> directories = ToolLocationHelper.GetPathToReferenceAssemblies(tempDirectory, frameworkName);

                Assert.Equal(3, directories.Count); // "Expected the method to return three paths."
                Assert.True(String.Equals(directories[0], framework41Directory, StringComparison.OrdinalIgnoreCase), "Expected first entry to be first in chain but it was" + directories[0]);
                Assert.True(String.Equals(directories[1], framework40Directory, StringComparison.OrdinalIgnoreCase), "Expected first entry to be second in chain but it was" + directories[1]);
                Assert.True(String.Equals(directories[2], framework39Directory, StringComparison.OrdinalIgnoreCase), "Expected first entry to be third in chain but it was" + directories[2]);
            }
            finally
            {
                if (Directory.Exists(framework41Directory))
                {
                    Directory.Delete(framework41Directory, true);
                }

                if (Directory.Exists(framework40Directory))
                {
                    Directory.Delete(framework40Directory, true);
                }

                if (Directory.Exists(framework39Directory))
                {
                    Directory.Delete(framework39Directory, true);
                }
            }
        }

        /// <summary>
        /// Verify the correct display name returned
        /// </summary>
        [Fact]
        public void DisplayNameGeneration()
        {
            string redistString40 = "<FileList Redist='Random' Name='MyFramework 4.0' >" +
                                       "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                    "</FileList >";

            string redistString39 = "<FileList Redist='Random'>" +
                                         "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                      "</FileList >";

            string tempDirectory = Path.Combine(Path.GetTempPath(), "DisplayNameGeneration");

            string framework40Directory = Path.Combine(tempDirectory, "MyFramework\\v4.0\\");
            string framework40redistDirectory = Path.Combine(framework40Directory, "RedistList");
            string framework40RedistList = Path.Combine(framework40redistDirectory, "FrameworkList.xml");

            string framework39Directory = Path.Combine(tempDirectory, "MyFramework\\v3.9\\Profile\\Client");
            string framework39redistDirectory = Path.Combine(framework39Directory, "RedistList");
            string framework39RedistList = Path.Combine(framework39redistDirectory, "FrameworkList.xml");

            try
            {
                Directory.CreateDirectory(framework40redistDirectory);
                Directory.CreateDirectory(framework39redistDirectory);

                File.WriteAllText(framework39RedistList, redistString39);
                File.WriteAllText(framework40RedistList, redistString40);

                FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("MyFramework", new Version("4.0"));
                string displayName40 = ToolLocationHelper.GetDisplayNameForTargetFrameworkDirectory(framework40Directory, frameworkName);

                frameworkName = new FrameworkNameVersioning("MyFramework", new Version("3.9"), "Client");
                string displayName39 = ToolLocationHelper.GetDisplayNameForTargetFrameworkDirectory(framework39Directory, frameworkName);
                Assert.True(displayName40.Equals("MyFramework 4.0", StringComparison.OrdinalIgnoreCase));
                Assert.True(displayName39.Equals("MyFramework v3.9 Client", StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                if (Directory.Exists(framework40Directory))
                {
                    Directory.Delete(framework40Directory, true);
                }

                if (Directory.Exists(framework39Directory))
                {
                    Directory.Delete(framework39Directory, true);
                }
            }
        }


        /// <summary>
        /// Make sure we do not crach if there is a circular reference in the redist lists, we should only have a path in our reference assembly list once.
        /// 
        /// </summary>
        [Fact]
        public void GetPathToReferenceAssembliesWithRootCircularReference()
        {
            string redistString41 = "<FileList Redist='Random' IncludeFramework='v4.0'>" +
                                     "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                  "</FileList >";

            string redistString40 = "<FileList Redist='Random' IncludeFramework='v4.1'>" +
                                       "<File AssemblyName='System' Version='4.0.0.0' PublicKeyToken='b77a5c561934e089' Culture='neutral' ProcessorArchitecture='MSIL' FileVersion='4.0.0.0' InGAC='false' />" +
                                    "</FileList >";

            string tempDirectory = Path.Combine(Path.GetTempPath(), "GetPathToReferenceAssembliesWithRootGoodWithChain");

            string framework41Directory = Path.Combine(tempDirectory, "MyFramework\\v4.1\\");
            string framework41redistDirectory = Path.Combine(framework41Directory, "RedistList");
            string framework41RedistList = Path.Combine(framework41redistDirectory, "FrameworkList.xml");

            string framework40Directory = Path.Combine(tempDirectory, "MyFramework\\v4.0\\");
            string framework40redistDirectory = Path.Combine(framework40Directory, "RedistList");
            string framework40RedistList = Path.Combine(framework40redistDirectory, "FrameworkList.xml");

            try
            {
                Directory.CreateDirectory(framework41redistDirectory);
                Directory.CreateDirectory(framework40redistDirectory);

                File.WriteAllText(framework40RedistList, redistString40);
                File.WriteAllText(framework41RedistList, redistString41);


                FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("MyFramework", new Version("4.1"));
                IList<string> directories = ToolLocationHelper.GetPathToReferenceAssemblies(tempDirectory, frameworkName);

                Assert.Equal(2, directories.Count); // "Expected the method to return two paths."
                Assert.True(String.Equals(directories[0], framework41Directory, StringComparison.OrdinalIgnoreCase), "Expected first entry to be first in chain but it was" + directories[0]);
                Assert.True(String.Equals(directories[1], framework40Directory, StringComparison.OrdinalIgnoreCase), "Expected first entry to be second in chain but it was" + directories[1]);
            }
            finally
            {
                if (Directory.Exists(framework41Directory))
                {
                    Directory.Delete(framework41Directory, true);
                }

                if (Directory.Exists(framework40Directory))
                {
                    Directory.Delete(framework40Directory, true);
                }
            }
        }


        /// <summary>
        /// Test the case where the root path is a string but the framework name is null. 
        /// We should expect the correct argument null exception
        /// </summary>
        [Fact]
        public void GetPathToReferenceAssembliesNullFrameworkName()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ToolLocationHelper.GetPathToReferenceAssemblies("Not Null String", (FrameworkNameVersioning)null);
            }
           );
        }
        /// <summary>
        /// Make sure we get the correct exception when both parameters are null
        /// </summary>
        [Fact]
        public void GetPathToReferenceAssembliesNullArgumentNameandFrameworkName()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ToolLocationHelper.GetPathToReferenceAssemblies(null, (FrameworkNameVersioning)null);
            }
           );
        }
        /// <summary>
        /// Make sure we get the correct exception when the root is null but the frameworkname is not null
        /// </summary>
        [Fact]
        public void GetPathToReferenceAssembliesNullArgumentGoodFrameworkNameNullRoot()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Ident", new Version("2.0"));
                ToolLocationHelper.GetPathToReferenceAssemblies(null, frameworkName);
            }
           );
        }
        /// <summary>
        /// Make sure we get the correct exception when the root is null but the frameworkname is not null
        /// With no framework name we cannot generate the path
        /// </summary>
        [Fact]
        public void GetPathToReferenceAssembliesNullArgumentGoodFrameworkNameEmptyRoot()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Ident", new Version("2.0"));
                ToolLocationHelper.GetPathToReferenceAssemblies(String.Empty, frameworkName);
            }
           );
        }
        /// <summary>
        /// Make sure we get the correct exception when the root is null but the frameworkname is not empty to make sure we cover the different input cases
        /// With no root we cannot properly generate the path.
        /// </summary>
        [Fact]
        public void GetPathToReferenceAssembliesNullArgumentGoodFrameworkNameEmptyRoot2()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Ident", new Version("2.0"));
                ToolLocationHelper.GetPathToReferenceAssemblies(String.Empty, frameworkName);
            }
           );
        }
        #endregion

        #region GetReferenceAssemblyPathWithDefaultRoot

        /// <summary>
        /// Test the case where the method which only takes in a FrameworkName will throw an exception when 
        /// the input is null since a null framework name is not useful
        /// </summary>
        [Fact]
        public void GetPathToReferenceAssembliesDefaultLocationNullFrameworkName()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ToolLocationHelper.GetPathToReferenceAssemblies((FrameworkNameVersioning)null);
            }
           );
        }
        /// <summary>
        /// Verify the method correctly returns the 4.5 reference assembly location information if .net 4.5 and 
        /// its corresponding reference assemblies are installed.
        /// If they are not installed, the test should be ignored.
        /// </summary>
        [Fact]
        public void GetPathToReferenceAssembliesDefaultLocation45()
        {
            FrameworkNameVersioning frameworkName = null;
            IList<string> directories = null;
            if (ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version45) != null)
            {
                frameworkName = new FrameworkNameVersioning(".NETFramework", new Version("4.5"));
                directories = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName);
                Assert.Equal(1, directories.Count); // "Expected the method to return one path."

                string referenceAssemblyPath = ToolLocationHelper.GetPathToDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion.Version45);
                Assert.True(String.Equals(directories[0], referenceAssemblyPath, StringComparison.OrdinalIgnoreCase), "Expected referenceassembly directory to be " + referenceAssemblyPath + " but it was " + directories[0]);
            }
            // else
            // "Ignored because v4.5 did not seem to be installed"
        }

        /// <summary>
        /// Test the case where the framework requested does not exist. Since we do an existence check before returning the path this non existent path should return an empty list
        /// </summary>
        [Fact]
        public void GetPathToReferenceAssembliesDefaultLocation99()
        {
            string targetFrameworkRootPath = Path.Combine(System.Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Reference Assemblies\\Microsoft\\Framework");
            string targetFrameworkIdentifier = ".Net Framework";
            Version targetFrameworkVersion = new Version("99.99");

            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning(targetFrameworkIdentifier, targetFrameworkVersion, String.Empty);

            IList<string> directories = ToolLocationHelper.GetPathToReferenceAssemblies(frameworkName);
            Assert.Equal(0, directories.Count); // "Expected the method to return no paths."
        }

        /// <summary>
        /// Make sure we choose the correct path for program files based on the environment variables
        /// </summary>
        [Fact]
        public void TestGenerateProgramFiles32()
        {
            string programFilesX86Original = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            try
            {
                Environment.SetEnvironmentVariable("ProgramFiles(x86)", null);
                string result = FrameworkLocationHelper.GenerateProgramFiles32();
                Assert.Equal(programFiles, result, true); // "Expected to use program files but used program files x86"

                Environment.SetEnvironmentVariable("ProgramFiles(x86)", String.Empty);

                result = FrameworkLocationHelper.GenerateProgramFiles32();
                Assert.Equal(programFiles, result, true); // "Expected to use program files but used program files x86"
            }
            finally
            {
                Environment.SetEnvironmentVariable("ProgramFiles(x86)", programFilesX86Original);
            }
        }

        /// <summary>
        /// Verify we get the correct reference assembly path out of the framework location helper
        /// </summary>
        [Fact]
        public void TestGeneratedReferenceAssemblyPath()
        {
            string programFiles32 = FrameworkLocationHelper.GenerateProgramFiles32();
            string referenceAssemblyRoot = FrameworkLocationHelper.GenerateProgramFilesReferenceAssemblyRoot();
            string pathToCombineWith = "Reference Assemblies\\Microsoft\\Framework";
            string combinedPath = Path.Combine(programFiles32, pathToCombineWith);
            string fullPath = Path.GetFullPath(combinedPath);

            Assert.True(referenceAssemblyRoot.Equals(fullPath, StringComparison.OrdinalIgnoreCase), String.Format("Expected the path to be '{0}' but it was '{1}'", fullPath, referenceAssemblyRoot));
        }


        #endregion

        #region HandleLegacyFrameworks

        /// <summary>
        /// Verify when 20 is simulated to be installed that the method returns the 2.0 directory
        /// </summary>
        [Fact]
        public void LegacyFramework20Good()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("2.0"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();
            legacyHelper.DotNet20Installed = true;

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(1, list.Count);
            Assert.Equal(LegacyFrameworkTestHelper.DotNet20FrameworkPath, list[0]);
        }

        /// <summary>
        /// Verify when 20 is simulated to not be installed that the method returns an empty list
        /// </summary>
        [Fact]
        public void LegacyFramework20NotInstalled()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("2.0"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(0, list.Count);
        }

        /// <summary>
        /// Verify when 30 is simulated to be installed that the method returns the 3.0 directory
        /// </summary>
        [Fact]
        public void LegacyFramework30Good()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.0"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();
            legacyHelper.DotNetReferenceAssemblies30Installed = true;
            legacyHelper.DotNet30Installed = true;
            legacyHelper.DotNet20Installed = true;

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(3, list.Count);
            Assert.Equal(LegacyFrameworkTestHelper.DotNet30ReferenceAssemblyPath, list[0]);
            Assert.Equal(LegacyFrameworkTestHelper.DotNet30FrameworkPath, list[1]);
            Assert.Equal(LegacyFrameworkTestHelper.DotNet20FrameworkPath, list[2]);
        }

        /// <summary>
        /// Verify when 30 is simulated to not be installed that the method returns an empty list
        /// </summary>
        [Fact]
        public void LegacyFramework30NotInstalled()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.0"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();
            legacyHelper.DotNetReferenceAssemblies30Installed = true;

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(0, list.Count);
        }

        /// <summary>
        /// Verify when the 30 reference assemblies are simulated to not be installed that the method returns an empty list
        /// </summary>
        [Fact]
        public void LegacyFramework30ReferenceAssembliesNotInstalled()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.0"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();
            legacyHelper.DotNet30Installed = true;

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(0, list.Count);
        }

        /// <summary>
        /// Verify when 30 is installed but 2.0 is not installed that we only get one of the paths back.
        /// </summary>
        [Fact]
        public void LegacyFramework30WithNo20Installed()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.0"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();
            legacyHelper.DotNet30Installed = true;
            legacyHelper.DotNetReferenceAssemblies30Installed = true;
            // Note no 2.0 installed

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(2, list.Count);
            Assert.True(list[0].Equals(LegacyFrameworkTestHelper.DotNet30ReferenceAssemblyPath, StringComparison.OrdinalIgnoreCase));
            Assert.True(list[1].Equals(LegacyFrameworkTestHelper.DotNet30FrameworkPath, StringComparison.OrdinalIgnoreCase));
        }


        /// <summary>
        /// Verify when 35 is simulated to be installed that the method returns the 3.5 directory
        /// </summary>
        [Fact]
        public void LegacyFramework35Good()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.5"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();
            legacyHelper.DotNetReferenceAssemblies35Installed = true;
            legacyHelper.DotNetReferenceAssemblies30Installed = true;
            legacyHelper.DotNet30Installed = true;
            legacyHelper.DotNet35Installed = true;
            legacyHelper.DotNet20Installed = true;

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(5, list.Count);
            Assert.Equal(LegacyFrameworkTestHelper.DotNet35ReferenceAssemblyPath, list[0]);
            Assert.Equal(LegacyFrameworkTestHelper.DotNet35FrameworkPath, list[1]);
            Assert.Equal(LegacyFrameworkTestHelper.DotNet30ReferenceAssemblyPath, list[2]);
            Assert.Equal(LegacyFrameworkTestHelper.DotNet30FrameworkPath, list[3]);
            Assert.Equal(LegacyFrameworkTestHelper.DotNet20FrameworkPath, list[4]);
        }

        /// <summary>
        /// Verify when 35 is simulated to not be installed that the method returns an empty list
        /// </summary>
        [Fact]
        public void LegacyFramework35NotInstalled()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.5"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();
            legacyHelper.DotNetReferenceAssemblies35Installed = true;

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(0, list.Count);
        }


        /// <summary>
        /// Verify when 35 reference asssemblie are simulated to not be installed that the method returns an empty list
        /// </summary>
        [Fact]
        public void LegacyFramework35ReferenceAssembliesNotInstalled()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.5"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();
            legacyHelper.DotNet35Installed = true;

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(0, list.Count);
        }

        /// <summary>
        /// Make sure when we are targeting .net framework 3.5 and are on a 64 bit machine we get the correct framework path.
        ///  
        /// We are on a 64 bit machine
        /// Targeting .net framework 3.5
        /// 
        /// 1) Target platform is x86. We expect to get the 32 bit framework directory
        /// 2) Target platform is x64, we expect to get the 64 bit framework directory
        /// 3) Target platform is Itanium, we expect to get the 64 bit framework directory
        /// 3) Target platform is some other value (AnyCpu, or anything else)  expect the framework directory for the "current" bitness of the process we are running under.
        /// 
        /// </summary>
        [Fact]
        public void GetPathToStandardLibraries64Bit35()
        {
            string frameworkDirectory2032bit = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(SharedDotNetFrameworkArchitecture.Bitness32);
            string frameworkDirectory2064bit = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(SharedDotNetFrameworkArchitecture.Bitness64);
            string frameworkDirectory20Current = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(SharedDotNetFrameworkArchitecture.Current);

            if (!Environment.Is64BitOperatingSystem)
            {
                // "Not 64 bit OS "
                return;
            }

            if (String.IsNullOrEmpty(frameworkDirectory2032bit) || String.IsNullOrEmpty(frameworkDirectory2064bit) || String.IsNullOrEmpty(frameworkDirectory20Current))
            {
                // ".Net 2.0 not installed: checked current {0} :: 64 bit :: {1} :: 32 bit {2}", frameworkDirectory20Current, frameworkDirectory2064bit, frameworkDirectory2032bit
                return;
            }

            string pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", String.Empty, "x86");
            Assert.True(frameworkDirectory2032bit.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", frameworkDirectory2032bit, pathToFramework));

            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", String.Empty, "x64");
            Assert.True(frameworkDirectory2064bit.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", frameworkDirectory2064bit, pathToFramework));

            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", String.Empty, "itanium");
            Assert.True(frameworkDirectory2064bit.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", frameworkDirectory2064bit, pathToFramework));

            if (!Environment.Is64BitProcess)
            {
                pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", String.Empty, "RandomPlatform");
                Assert.True(frameworkDirectory2032bit.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", frameworkDirectory2032bit, pathToFramework));
            }
            else
            {
                pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", String.Empty, "RandomPlatform");
                Assert.True(frameworkDirectory2064bit.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", frameworkDirectory2064bit, pathToFramework));
            }
        }

        /// <summary>
        /// Make sure when we are targeting .net framework 3.5 and are on a 64 bit machine we get the correct framework path.
        ///  
        /// We are on a 64 bit machine
        /// Targeting .net framework 4.0
        /// 
        /// We expect to always get the same path which is returned by GetPathToReferenceAssemblies.
        /// </summary>
        [Fact]
        public void GetPathToStandardLibraries64Bit40()
        {
            IList<string> referencePaths = ToolLocationHelper.GetPathToReferenceAssemblies(new FrameworkNameVersioning(".NETFramework", new Version("4.0")));

            if (!Environment.Is64BitOperatingSystem)
            {
                // "Not 64 bit OS "
                return;
            }

            if (referencePaths.Count == 0)
            {
                // ".Net 4.0 not installed"
                return;
            }

            string pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", String.Empty, "x86");
            string dotNet40Path = FileUtilities.EnsureNoTrailingSlash(referencePaths[0]);
            Assert.True(dotNet40Path.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", dotNet40Path, pathToFramework));

            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", String.Empty, "x64");
            Assert.True(dotNet40Path.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", dotNet40Path, pathToFramework));

            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", String.Empty, "itanium");
            Assert.True(dotNet40Path.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", dotNet40Path, pathToFramework));

            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", String.Empty, "RandomPlatform");
            Assert.True(dotNet40Path.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", dotNet40Path, pathToFramework));
        }

        /// <summary>
        /// Make sure when we are targeting .net framework 3.5 and are on a 32 bit machine we get the correct framework path.
        ///  
        /// We are on a 32 bit machine
        /// Targeting .net framework 3.5
        /// 
        /// 1) Target platform is x86. We expect to get the 32 bit framework directory
        /// 2) Target platform is x64, we expect to get the 32 bit framework directory
        /// 3) Target platform is Itanium, we expect to get the 32 bit framework directory
        /// 3) Target platform is some other value (AnyCpu, or anything else)  expect the framework directory for the "current" bitness of the process we are running under. In the 
        ///    case of the unit test this should be the 32 bit framework directory.
        /// 
        /// </summary>
        [Fact]
        public void GetPathToStandardLibraries32Bit35()
        {
            string frameworkDirectory2032bit = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(SharedDotNetFrameworkArchitecture.Bitness32);
            string frameworkDirectory20Current = FrameworkLocationHelper.GetPathToDotNetFrameworkV20(SharedDotNetFrameworkArchitecture.Current);

            if (Environment.Is64BitOperatingSystem)
            {
                // "Is a 64 bit OS "
                return;
            }

            if (String.IsNullOrEmpty(frameworkDirectory2032bit) || String.IsNullOrEmpty(frameworkDirectory20Current))
            {
                // ".Net 2.0 not installed: checked current {0} :: 32 bit {2}", frameworkDirectory20Current, frameworkDirectory2032bit
                return;
            }

            string pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", String.Empty, "x86");
            Assert.True(frameworkDirectory2032bit.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", frameworkDirectory2032bit, pathToFramework));

            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", String.Empty, "x64");
            Assert.True(frameworkDirectory2032bit.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", frameworkDirectory2032bit, pathToFramework));

            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", String.Empty, "itanium");
            Assert.True(frameworkDirectory2032bit.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", frameworkDirectory2032bit, pathToFramework));

            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v3.5", String.Empty, "RandomPlatform");
            Assert.True(frameworkDirectory2032bit.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", frameworkDirectory2032bit, pathToFramework));
        }

        /// <summary>
        /// Make sure when we are targeting .net framework 4.0 and are on a 32 bit machine we get the correct framework path.
        ///  
        /// We are on a 32 bit machine
        /// Targeting .net framework 4.0
        /// 
        /// We expect to always get the same path which is returned by GetPathToReferenceAssemblies.
        /// </summary>
        [Fact]
        public void GetPathToStandardLibraries32Bit40()
        {
            IList<string> referencePaths = ToolLocationHelper.GetPathToReferenceAssemblies(new FrameworkNameVersioning(".NETFramework", new Version("4.0")));

            if (Environment.Is64BitOperatingSystem)
            {
                // "Is 64 bit OS "
                return;
            }

            if (referencePaths.Count == 0)
            {
                // ".Net 4.0 not installed"
                return;
            }

            string pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", String.Empty, "x86");
            string dotNet40Path = FileUtilities.EnsureNoTrailingSlash(referencePaths[0]);
            Assert.True(dotNet40Path.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", dotNet40Path, pathToFramework));

            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", String.Empty, "x64");
            Assert.True(dotNet40Path.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", dotNet40Path, pathToFramework));

            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", String.Empty, "itanium");
            Assert.True(dotNet40Path.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", dotNet40Path, pathToFramework));

            pathToFramework = ToolLocationHelper.GetPathToStandardLibraries(".NetFramework", "v4.0", String.Empty, "RandomPlatform");
            Assert.True(dotNet40Path.Equals(pathToFramework, StringComparison.OrdinalIgnoreCase), String.Format("Expected {0} but got {1}", dotNet40Path, pathToFramework));
        }

        /// <summary>
        /// Verify when 35 is installed but 2.0 is not installed we to find 3.5 and 3.0 but no 2.0 because it does not exist.
        /// </summary>
        [Fact]
        public void LegacyFramework35WithNo20Installed()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.5"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();
            legacyHelper.DotNetReferenceAssemblies35Installed = true;
            legacyHelper.DotNetReferenceAssemblies30Installed = true;
            legacyHelper.DotNet35Installed = true;
            legacyHelper.DotNet30Installed = true;
            // Note no 2.0 installed

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(4, list.Count);
            Assert.True(list[0].Equals(LegacyFrameworkTestHelper.DotNet35ReferenceAssemblyPath, StringComparison.OrdinalIgnoreCase));
            Assert.True(list[1].Equals(LegacyFrameworkTestHelper.DotNet35FrameworkPath, StringComparison.OrdinalIgnoreCase));
            Assert.True(list[2].Equals(LegacyFrameworkTestHelper.DotNet30ReferenceAssemblyPath, StringComparison.OrdinalIgnoreCase));
            Assert.True(list[3].Equals(LegacyFrameworkTestHelper.DotNet30FrameworkPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when 35 is installed but 3.0 is not installed we expect not to find 3.0 or 2.0.
        /// </summary>
        [Fact]
        public void LegacyFramework35WithNo30Installed()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("3.5"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();
            legacyHelper.DotNetReferenceAssemblies35Installed = true;
            legacyHelper.DotNet35Installed = true;
            legacyHelper.DotNet20Installed = true;
            // Note no 3.0

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(2, list.Count);
            Assert.True(list[0].Equals(LegacyFrameworkTestHelper.DotNet35ReferenceAssemblyPath, StringComparison.OrdinalIgnoreCase));
            Assert.True(list[1].Equals(LegacyFrameworkTestHelper.DotNet35FrameworkPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify when 40 is simulated to not be installed that the method returns an empty list
        /// </summary>
        [Fact]
        public void LegacyFramework40NotInstalled()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("4.0"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(0, list.Count);
        }

        /// <summary>
        /// Verify when 40 reference assemblies are installed but the dot net framework is not, in this case we return empty indicating .net 4.0 is not properly installed
        /// </summary>
        [Fact]
        public void LegacyFramework40DotNetFrameworkDirectoryNotInstalled()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("4.0"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();
            legacyHelper.DotNetReferenceAssemblies40Installed = true;

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(0, list.Count);
        }

        /// <summary>
        /// Verify when 40 reference assemblies are installed but the dot net framework is not we only get one of the paths back, this is because right now the assemblies are not in the right location
        /// </summary>
        [Fact]
        public void LegacyFramework40DotNetReferenceAssemblyDirectoryNotInstalled()
        {
            FrameworkNameVersioning frameworkName = new FrameworkNameVersioning("Anything", new Version("4.0"));
            LegacyFrameworkTestHelper legacyHelper = new LegacyFrameworkTestHelper();
            legacyHelper.DotNet40Installed = true;

            IList<string> list = ToolLocationHelper.HandleLegacyDotNetFrameworkReferenceAssemblyPaths(legacyHelper.GetDotNetVersionToPathDelegate, legacyHelper.GetDotNetReferenceAssemblyDelegate, frameworkName);
            Assert.Equal(0, list.Count);
        }
        #endregion

        /// <summary>
        /// Verify we can an argument exception if we try and pass a empty registry root
        /// </summary>
        [Fact]
        public void GetAssemblyFoldersExInfoTestEmptyRegistryRoot()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ToolLocationHelper.GetAssemblyFoldersExInfo("", "v3.0", "AssemblyFoldersEx", null, null, System.Reflection.ProcessorArchitecture.MSIL);
            }
           );
        }
        /// <summary>
        /// Verify we can an argumentNull exception if we try and pass a null registry root
        /// </summary>
        [Fact]
        public void GetAssemblyFoldersExInfoListTestNullRegistryRoot()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ToolLocationHelper.GetAssemblyFoldersExInfo(null, "v3.0", "AssemblyFoldersEx", null, null, System.Reflection.ProcessorArchitecture.MSIL);
            }
           );
        }
        /// <summary>
        /// Verify we can an argument exception if we try and pass a empty registry suffix
        /// </summary>
        [Fact]
        public void GetAssemblyFoldersExInfoTestEmptyRegistrySuffix()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ToolLocationHelper.GetAssemblyFoldersExInfo(@"SOFTWARE\Microsoft\.UnitTest", "v3.0", "", null, null, System.Reflection.ProcessorArchitecture.MSIL);
            }
           );
        }
        /// <summary>
        /// Verify we can an argumentNull exception if we try and pass a null registry suffix
        /// </summary>
        [Fact]
        public void GetAssemblyFoldersExInfoTestNullRegistrySuffix()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ToolLocationHelper.GetAssemblyFoldersExInfo(@"SOFTWARE\Microsoft\.UnitTest", "v3.0", null, null, null, System.Reflection.ProcessorArchitecture.MSIL);
            }
           );
        }
        /// <summary>
        /// Verify we can an argument exception if we try and pass a empty registry suffix
        /// </summary>
        [Fact]
        public void GetAssemblyFoldersExInfoTestEmptyTargetRuntime()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                ToolLocationHelper.GetAssemblyFoldersExInfo(@"SOFTWARE\Microsoft\.UnitTest", "", "AssemblyFoldersEx", null, null, System.Reflection.ProcessorArchitecture.MSIL);
            }
           );
        }
        /// <summary>
        /// Verify we can an argumentNull exception if we try and pass a null target runtime version
        /// </summary>
        [Fact]
        public void GetAssemblyFoldersExInfoTestNullTargetRuntimeVersion()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ToolLocationHelper.GetAssemblyFoldersExInfo(@"SOFTWARE\Microsoft\.UnitTest", null, "AssemblyFoldersEx", null, null, System.Reflection.ProcessorArchitecture.MSIL);
            }
           );
        }
        /// <summary>
        /// Verify we can get a list of directories out of the public API.
        /// </summary>
        [Fact]
        public void GetAssemblyFoldersExInfoTest()
        {
            SetupAssemblyFoldersExTestConditionRegistryKey();
            IList<AssemblyFoldersExInfo> directories = null;
            try
            {
                directories = ToolLocationHelper.GetAssemblyFoldersExInfo(@"SOFTWARE\Microsoft\.UnitTest", "v3.0", "AssemblyFoldersEx", null, null, System.Reflection.ProcessorArchitecture.MSIL);
            }
            finally
            {
                RemoveAssemblyFoldersExTestConditionRegistryKey();
            }
            Assert.NotNull(directories);
            Assert.Equal(2, directories.Count);
            Assert.True(@"C:\V1Control2".Equals(directories[0].DirectoryPath, StringComparison.OrdinalIgnoreCase));
            Assert.True(@"C:\V1Control".Equals(directories[1].DirectoryPath, StringComparison.OrdinalIgnoreCase));
        }

        private void SetupAssemblyFoldersExTestConditionRegistryKey()
        {
            RegistryKey baseKey = Registry.CurrentUser;
            baseKey.DeleteSubKeyTree(@"SOFTWARE\Microsoft\.UnitTest", false);
            RegistryKey folderKey = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\.UnitTest\v2.0.3600\AssemblyFoldersEx\Component1");
            folderKey.SetValue("", @"C:\V1Control");

            RegistryKey servicePackKey = baseKey.CreateSubKey(@"SOFTWARE\Microsoft\.UnitTest\v2.0.3600\AssemblyFoldersEx\Component2");
            servicePackKey.SetValue("", @"C:\V1Control2");
        }

        private void RemoveAssemblyFoldersExTestConditionRegistryKey()
        {
            RegistryKey baseKey = Registry.CurrentUser;
            try
            {
                baseKey.DeleteSubKeyTree(@"SOFTWARE\Microsoft\.UnitTest\v2.0.3600\AssemblyFoldersEx\Component1");
                baseKey.DeleteSubKeyTree(@"SOFTWARE\Microsoft\.UnitTest\v2.0.3600\AssemblyFoldersEx\Component2");
            }
            catch (Exception)
            {
            }
        }

        /*
        * Method:   GetDirectories
        *
        * Delegate method simulates a file system for testing location methods.
        */
        private static string[] GetDirectories(string path, string pattern)
        {
            if (path == "{runtime-base}" && pattern == "v1.2*")
            {
                return new string[] { @"{runtime-base}\v1.2.30617", @"{runtime-base}\v1.2.x86dbg", @"{runtime-base}\v1.2.x86fre" };
            }
            return new string[0];
        }

        /*
        * Method:   GetDirectories35
        *
        * Delegate method simulates a file system for testing location methods.
        */
        private static string[] GetDirectories35(string path, string pattern)
        {
            return new string[] { @"{runtime-base}\v3.5.12333", @"{runtime-base}\v3.5", @"{runtime-base}\v3.5.23455" };
        }

        /// <summary>
        /// Delegate method simulates a file system for testing location methods. 
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        private static bool DirectoryExists(string path)
        {
            return path.Contains("{runtime-base}") || Directory.Exists(path);
        }

        private static string GetRegistryValueHelper(RegistryHive hive, RegistryView view, string subKeyPath, string name)
        {
            using (var key = RegistryHelper.OpenBaseKey(hive, view))
            using (var subKey = key.OpenSubKey(subKeyPath))
            {
                if (subKey != null)
                {
                    return (string)subKey.GetValue(name);
                }
            }

            return null;
        }

        private static IEnumerable<VisualStudioVersion> EnumVisualStudioVersions()
        {
            for (VisualStudioVersion vsVersion = VisualStudioVersion.Version100; vsVersion <= VisualStudioVersion.VersionLatest; ++vsVersion)
            {
                yield return vsVersion;
            }
        }

        private static IEnumerable<TargetDotNetFrameworkVersion> EnumDotNetFrameworkVersions()
        {
            for (TargetDotNetFrameworkVersion dotNetVersion = TargetDotNetFrameworkVersion.Version11; dotNetVersion <= TargetDotNetFrameworkVersion.VersionLatest; ++dotNetVersion)
            {
                yield return dotNetVersion;
            }
        }

        /// <summary>
        /// This class will provide delegates and properties to allow differen combinations of ToolLocationHelper GetDotNetFrameworkPaths and GetReferenceAssemblyPaths to be simulated.
        /// </summary>
        internal class LegacyFrameworkTestHelper
        {
            /// <summary>
            /// Paths which simulate the fact that the frameworks are installed including their reference assemblies
            /// </summary>
            internal const string DotNet40ReferenceAssemblyPath = "C:\\Program Files\\Reference Assemblies\\Framework\\V4.0";
            internal const string DotNet35ReferenceAssemblyPath = "C:\\Program Files\\Reference Assemblies\\Framework\\V3.5";
            internal const string DotNet30ReferenceAssemblyPath = "C:\\Program Files\\Reference Assemblies\\Framework\\V3.0";
            internal const string DotNet20FrameworkPath = "C:\\Microsoft\\.Net Framework\\V2.0.57027";
            internal const string DotNet30FrameworkPath = "C:\\Microsoft\\.Net Framework\\V3.0";
            internal const string DotNet35FrameworkPath = "C:\\Microsoft\\.Net Framework\\V3.5";
            internal const string DotNet40FrameworkPath = "C:\\Microsoft\\.Net Framework\\V4.0";

            /// <summary>
            /// Should the delegate respond with a path or null when asked for Version20 on the delegate which gets the DotNetFrameworkPath
            /// </summary>
            internal bool DotNet20Installed
            {
                get;
                set;
            }

            /// <summary>
            /// Should the delegate respond with a path or null when asked for Version30 on the delegate which gets the DotNetFrameworkPath
            /// </summary>
            internal bool DotNet30Installed
            {
                get;
                set;
            }

            /// <summary>
            /// Should the delegate respond with a path or null when asked for Version35 on the delegate which gets the DotNetFrameworkPath
            /// </summary>
            internal bool DotNet35Installed
            {
                get;
                set;
            }

            /// <summary>
            /// Should the delegate respond with a path or null when asked for Version40 on the delegate which gets the DotNetFrameworkPath
            /// </summary>
            internal bool DotNet40Installed
            {
                get;
                set;
            }

            /// <summary>
            /// Should the delegate respond with a path or null when asked for Version40 on the delegate which gets the DotNetReferenceAssembliesPath is called
            /// </summary>
            internal bool DotNetReferenceAssemblies40Installed
            {
                get;
                set;
            }

            /// <summary>
            /// Should the delegate respond with a path or null when asked for Version35 on the delegate which gets the DotNetReferenceAssembliesPath is called
            /// </summary>
            internal bool DotNetReferenceAssemblies35Installed
            {
                get;
                set;
            }

            /// <summary>
            /// Should the delegate respond with a path or null when asked for Version30 on the delegate which gets the DotNetReferenceAssembliesPath is called
            /// </summary>
            internal bool DotNetReferenceAssemblies30Installed
            {
                get;
                set;
            }

            /// <summary>
            /// Return a delegate which will return a path or null depending on whether or not frameworks and their reference assembly paths are being simulated as being installed
            /// </summary>
            internal ToolLocationHelper.VersionToPath GetDotNetVersionToPathDelegate
            {
                get
                {
                    return new ToolLocationHelper.VersionToPath(GetDotNetFramework);
                }
            }

            /// <summary>
            /// Return a delegate which will return a path or null depending on whether or not frameworks and their reference assembly paths are being simulated as being installed
            /// </summary>
            internal ToolLocationHelper.VersionToPath GetDotNetReferenceAssemblyDelegate
            {
                get
                {
                    return new ToolLocationHelper.VersionToPath(GetDotNetFrameworkReferenceAssemblies);
                }
            }

            /// <summary>
            /// Return a path to the .net framework reference assemblies if the boolean property said we should return one. 
            /// Return null if we should not fake the fact that the framework reference assemblies are installed
            /// </summary>
            internal string GetDotNetFrameworkReferenceAssemblies(TargetDotNetFrameworkVersion version)
            {
                if (version == TargetDotNetFrameworkVersion.Version40)
                {
                    if (DotNetReferenceAssemblies40Installed)
                    {
                        return DotNet40ReferenceAssemblyPath;
                    }
                    else
                    {
                        return null;
                    }
                }

                if (version == TargetDotNetFrameworkVersion.Version35)
                {
                    if (DotNetReferenceAssemblies35Installed)
                    {
                        return DotNet35ReferenceAssemblyPath;
                    }
                    else
                    {
                        return null;
                    }
                }

                if (version == TargetDotNetFrameworkVersion.Version30)
                {
                    if (DotNetReferenceAssemblies30Installed)
                    {
                        return DotNet30ReferenceAssemblyPath;
                    }
                    else
                    {
                        return null;
                    }
                }

                return null;
            }

            /// <summary>
            /// Return a path to the .net framework if the boolean property said we should return one. 
            /// Return null if we should not fake the fact that the framework is installed
            /// </summary>
            internal string GetDotNetFramework(TargetDotNetFrameworkVersion version)
            {
                if (version == TargetDotNetFrameworkVersion.Version20)
                {
                    if (DotNet20Installed)
                    {
                        return DotNet20FrameworkPath;
                    }
                    else
                    {
                        return null;
                    }
                }

                if (version == TargetDotNetFrameworkVersion.Version30)
                {
                    if (DotNet30Installed)
                    {
                        return DotNet30FrameworkPath;
                    }
                    else
                    {
                        return null;
                    }
                }

                if (version == TargetDotNetFrameworkVersion.Version35)
                {
                    if (DotNet35Installed)
                    {
                        return DotNet35FrameworkPath;
                    }
                    else
                    {
                        return null;
                    }
                }

                if (version == TargetDotNetFrameworkVersion.Version40)
                {
                    if (DotNet40Installed)
                    {
                        return DotNet40FrameworkPath;
                    }
                    else
                    {
                        return null;
                    }
                }

                return null;
            }
        }
    }

    /// <summary>
    /// Verify the toolLocation helper method that enumerates the disk and registry to get the list of installed SDKs.
    /// </summary>
    public class GetPlatformExtensionSDKLocationsTestFixture : IDisposable
    {
        // Create delegates to mock the registry for the registry portion of the test.
        private Microsoft.Build.Shared.OpenBaseKey _openBaseKey = new Microsoft.Build.Shared.OpenBaseKey(GetBaseKey);
        internal Microsoft.Build.Shared.GetRegistrySubKeyNames getRegistrySubKeyNames = new Microsoft.Build.Shared.GetRegistrySubKeyNames(GetRegistrySubKeyNames);
        internal Microsoft.Build.Shared.GetRegistrySubKeyDefaultValue getRegistrySubKeyDefaultValue;

        // Path to the fake SDk directory structure created under the temp directory.
        private string _fakeStructureRoot = null;
        private string _fakeStructureRoot2 = null;

        public GetPlatformExtensionSDKLocationsTestFixture()
        {
            getRegistrySubKeyDefaultValue = new Microsoft.Build.Shared.GetRegistrySubKeyDefaultValue(GetRegistrySubKeyDefaultValue);

            _fakeStructureRoot = MakeFakeSDKStructure();
            _fakeStructureRoot2 = MakeFakeSDKStructure2();
        }

        #region TestMethods

        public void Dispose()
        {
            if (_fakeStructureRoot != null)
            {
                if (FileUtilities.DirectoryExistsNoThrow(_fakeStructureRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(_fakeStructureRoot, true);
                }
            }

            if (_fakeStructureRoot2 != null)
            {
                if (FileUtilities.DirectoryExistsNoThrow(_fakeStructureRoot2))
                {
                    FileUtilities.DeleteDirectoryNoThrow(_fakeStructureRoot2, true);
                }
            }
        }

        /// <summary>
        /// Pass empty and null target platform identifier and target platform version string to make sure we get the correct exceptions out.
        /// </summary>
        [Fact]
        public void PassEmptyAndNullTPM()
        {
            VerifyExceptionOnEmptyOrNullPlatformAttributes(String.Empty, new Version("1.0"));
            VerifyExceptionOnEmptyOrNullPlatformAttributes(null, new Version("1.0"));
            VerifyExceptionOnEmptyOrNullPlatformAttributes(null, null);
            VerifyExceptionOnEmptyOrNullPlatformAttributes("Windows", null);
        }

        /// <summary>
        /// Verify that we get argument exceptions where different combinations of identifier and version are passed in.
        /// </summary>
        private static void VerifyExceptionOnEmptyOrNullPlatformAttributes(string identifier, Version version)
        {

            Assert.ThrowsAny<ArgumentException>(
                () => ToolLocationHelper.GetPlatformExtensionSDKLocations(identifier, version));

            Assert.ThrowsAny<ArgumentException>(
                () => ToolLocationHelper.GetPlatformSDKLocation(identifier, version));
        }

        /// <summary>
        /// Verify we can get a list of extension sdks out of the API
        /// </summary>
        [Fact]
        public void TestGetExtensionSDKLocations()
        {
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");

                // Identifier does not exist
                IDictionary<string, string> sdks = ToolLocationHelper.GetPlatformExtensionSDKLocations(new string[] { _fakeStructureRoot }, null, "FOO", new Version(1, 0));
                Assert.Equal(0, sdks.Count);

                // Identifier exists
                sdks = ToolLocationHelper.GetPlatformExtensionSDKLocations(new string[] { _fakeStructureRoot }, null, "MyPlatform", new Version(3, 0));
                Assert.True(sdks.ContainsKey("MyAssembly, Version=1.0"));
                Assert.Equal(1, sdks.Count);

                // Targeting version higher than exists, however since we are using a russian doll model for extension sdks we will return ones in lower versions of the targeted platform.
                sdks = ToolLocationHelper.GetPlatformExtensionSDKLocations(new string[] { _fakeStructureRoot }, null, "MyPlatform", new Version(4, 0));
                Assert.True(sdks.ContainsKey("MyAssembly, Version=1.0"));
                Assert.True(sdks["MyAssembly, Version=1.0"].Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\3.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));
                Assert.True(sdks.ContainsKey("AnotherAssembly, Version=1.0"));
                Assert.True(sdks["AnotherAssembly, Version=1.0"].Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\4.0\\ExtensionSDKs\\AnotherAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));
                Assert.Equal(2, sdks.Count);

                // Identifier exists but no extensions are in sdks this version or lower
                sdks = ToolLocationHelper.GetPlatformExtensionSDKLocations(new string[] { _fakeStructureRoot }, null, "MyPlatform", new Version(1, 0));
                Assert.Equal(0, sdks.Count);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
            }
        }

        /// <summary>
        /// Verify we can get a single extension sdk location out of the API
        /// </summary>
        [Fact]
        public void TestGetExtensionSDKLocation()
        {
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");

                // Identifier does not exist
                IDictionary<string, string> sdks = ToolLocationHelper.GetPlatformExtensionSDKLocations(new string[] { _fakeStructureRoot }, null, "FOO", new Version(1, 0));
                Assert.Equal(0, sdks.Count);

                // Identifier exists
                string path = ToolLocationHelper.GetPlatformExtensionSDKLocation("MyAssembly, Version=1.0", "MyPlatform", new Version(3, 0), new string[] { _fakeStructureRoot }, null);
                Assert.True(path.Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\3.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));

                // Identifier exists in lower version
                path = ToolLocationHelper.GetPlatformExtensionSDKLocation("MyAssembly, Version=1.0", "MyPlatform", new Version(4, 0), new string[] { _fakeStructureRoot }, null);
                Assert.True(path.Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\3.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));

                // Identifier does not exist
                path = ToolLocationHelper.GetPlatformExtensionSDKLocation("Something, Version=1.0", "MyPlatform", new Version(4, 0), new string[] { _fakeStructureRoot }, null);
                Assert.Equal(0, path.Length);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
            }
        }

        /// <summary>
        /// Verify we do not get any resolved paths when we pass in a root which is too long
        /// 
        /// </summary>
        [Fact]
        public void ResolveFromDirectoryPathTooLong()
        {
            Assert.Throws<PathTooLongException>(() =>
            {
                // Try a path too long, which does not exist
                string tooLongPath = "C:\\" + new String('g', 1800);
                List<string> paths = new List<string>() { tooLongPath };
                Dictionary<TargetPlatformSDK, TargetPlatformSDK> targetPlatform = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();

                ToolLocationHelper.GatherSDKListFromDirectory(paths, targetPlatform);
            }
           );
        }
        /// <summary>
        /// Verify we get no resolved paths when we pass in a root with invalid chars
        /// </summary>
        [Fact]
        public void ResolveFromDirectoryInvalidChar()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                Dictionary<TargetPlatformSDK, TargetPlatformSDK> targetPlatform = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();

                // Try a path with invalid chars which does not exist
                string directoryWithInvalidChars = "c:\\<>?";
                List<string> paths = new List<string>() { directoryWithInvalidChars };
                ToolLocationHelper.GatherSDKListFromDirectory(paths, targetPlatform);
                Assert.Equal(0, targetPlatform.Count);
            }
           );
        }
        /// <summary>
        /// Verify we get no resolved paths when we pass in a path which does not exist.
        /// 
        /// </summary>
        [Fact]
        public void ResolveFromDirectoryNotExist()
        {
            Dictionary<TargetPlatformSDK, TargetPlatformSDK> targetPlatform = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();

            // Try a regular path which does not exist.
            string normalDirectory = "c:\\SDKPath";
            List<string> paths = new List<string>() { normalDirectory };
            ToolLocationHelper.GatherSDKListFromDirectory(paths, targetPlatform);
            Assert.Equal(0, targetPlatform.Count);
        }

        [Fact]
        public void VerifySDKManifestWithNullOrEmptyParameter()
        {
            Assert.Throws<ArgumentNullException>(() =>
                new SDKManifest(null));

            Assert.Throws<ArgumentException>(() =>
                new SDKManifest(""));
        }

        /// <summary>
        /// Verify SDKManifest defaults values for MaxPlatformVersion, MinOSVersion, MaxOSVersion when these are not
        /// present in the manifest and the SDK is a framework extension SDK
        /// </summary>
        [Fact]
        public void VerifyFrameworkSdkWithOldManifest()
        {
            string tmpRootDirectory = Path.GetTempPath();
            string frameworkPathPattern = @"Microsoft SDKs\Windows\v8.0\ExtensionSDKs\MyFramework";
            string frameworkPathPattern2 = @"ExtensionSDKs\MyFramework";

            string frameworkPath = Path.Combine(tmpRootDirectory, frameworkPathPattern);
            string manifestFile = Path.Combine(frameworkPath, "sdkManifest.xml");

            string frameworkPath2 = Path.Combine(tmpRootDirectory, frameworkPathPattern2);
            string manifestFile2 = Path.Combine(frameworkPath, "sdkManifest.xml");

            try
            {
                Directory.CreateDirectory(frameworkPath);
                Directory.CreateDirectory(frameworkPath2);

                // This is a framework SDK with specified values, no default ones are used
                string manifestExtensionSDK = @"
                <FileList
	                DisplayName = ""My SDK""
	                ProductFamilyName = ""UnitTest SDKs""
	                FrameworkIdentity = ""Name=MySDK.10.Debug, MinVersion=1.0.0.0""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK""
                    MaxPlatformVersion = ""9.0""
                    MinOSVersion = ""6.2.3""
                    MaxOSVersionTested = ""6.2.2"">

                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";

                File.WriteAllText(manifestFile, manifestExtensionSDK);
                SDKManifest sdkManifest = new SDKManifest(frameworkPath);

                Assert.True(sdkManifest.FrameworkIdentities != null && sdkManifest.FrameworkIdentities.Count > 0);
                Assert.Equal(sdkManifest.MaxPlatformVersion, "9.0");
                Assert.Equal(sdkManifest.MinOSVersion, "6.2.3");
                Assert.Equal(sdkManifest.MaxOSVersionTested, "6.2.2");

                // This is a framework SDK and the values default b/c they are not in the manifest
                string manifestExtensionSDK2 = @"
                <FileList
	                DisplayName = ""My SDK""
	                ProductFamilyName = ""UnitTest SDKs""
	                FrameworkIdentity = ""Name=MySDK.10.Debug, MinVersion=1.0.0.0""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK"">


                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";

                File.WriteAllText(manifestFile, manifestExtensionSDK2);
                SDKManifest sdkManifest2 = new SDKManifest(frameworkPath);

                Assert.True(sdkManifest.FrameworkIdentities != null && sdkManifest.FrameworkIdentities.Count > 0);
                Assert.Equal(sdkManifest2.MaxPlatformVersion, "8.0");
                Assert.Equal(sdkManifest2.MinOSVersion, "6.2.1");
                Assert.Equal(sdkManifest2.MaxOSVersionTested, "6.2.1");

                // This is not a framework SDK because it does not have FrameworkIdentity set
                string manifestExtensionSDK3 = @"
                <FileList
	                DisplayName = ""My SDK""
	                ProductFamilyName = ""UnitTest SDKs""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK"">


                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";

                File.WriteAllText(manifestFile, manifestExtensionSDK3);
                SDKManifest sdkManifest3 = new SDKManifest(frameworkPath);

                Assert.Equal(sdkManifest3.FrameworkIdentity, null);
                Assert.Equal(sdkManifest3.MaxPlatformVersion, null);
                Assert.Equal(sdkManifest3.MinOSVersion, null);
                Assert.Equal(sdkManifest3.MaxOSVersionTested, null);

                // This is not a framework SDK because of its location
                string manifestExtensionSDK4 = @"
                <FileList
	                DisplayName = ""My SDK""
	                ProductFamilyName = ""UnitTest SDKs""
	                FrameworkIdentity = ""Name=MySDK.10.Debug, MinVersion=1.0.0.0""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK""


                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";

                File.WriteAllText(manifestFile2, manifestExtensionSDK4);
                SDKManifest sdkManifest4 = new SDKManifest(frameworkPath2);

                Assert.Equal(sdkManifest4.FrameworkIdentity, null);
                Assert.Equal(sdkManifest4.MaxPlatformVersion, null);
                Assert.Equal(sdkManifest4.MinOSVersion, null);
                Assert.Equal(sdkManifest4.MaxOSVersionTested, null);

                // This is a framework SDK with partially specified values, some default values are used
                string manifestExtensionSDK5 = @"
                <FileList
	                DisplayName = ""My SDK""
	                ProductFamilyName = ""UnitTest SDKs""
	                FrameworkIdentity = ""Name=MySDK.10.Debug, MinVersion=1.0.0.0""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK""
                    MaxOSVersionTested = ""6.2.2"">

                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";

                File.WriteAllText(manifestFile, manifestExtensionSDK5);
                SDKManifest sdkManifest5 = new SDKManifest(frameworkPath);

                Assert.True(sdkManifest5.FrameworkIdentities != null && sdkManifest5.FrameworkIdentities.Count > 0);
                Assert.Equal(sdkManifest5.MaxPlatformVersion, "8.0");
                Assert.Equal(sdkManifest5.MinOSVersion, "6.2.1");
                Assert.Equal(sdkManifest5.MaxOSVersionTested, "6.2.2");
            }
            finally
            {
                Directory.Delete(frameworkPath, true /* for recursive deletion */);
                Directory.Delete(frameworkPath2, true /* for recursive deletion */);
            }
        }
        /// <summary>
        /// Verify that SDKManifest properties map correctly to properties in SDKManifest.xml.
        /// </summary>
        [Fact]
        public void VerifySDKManifest()
        {
            string manifestPath = Path.Combine(Path.GetTempPath(), "ManifestTmp");

            try
            {
                Directory.CreateDirectory(manifestPath);

                string manifestFile = Path.Combine(manifestPath, "sdkManifest.xml");


                string manifestPlatformSDK = @"
                <FileList
                    DisplayName = ""Windows""
                    PlatformIdentity = ""Windows, version=8.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""11.0""
                    MinOSVersion = ""6.2.1""
                    MaxOSVersionTested = ""6.2.1""
                    UnsupportedDowntarget = ""Windows, version=8.0"">

                <File Reference = ""Windows"">
	                <ToolboxItems VSCategory = ""Toolbox.Default""/>
                </File>
                </FileList>";

                File.WriteAllText(manifestFile, manifestPlatformSDK);
                SDKManifest sdkManifest = new SDKManifest(manifestPath);

                Assert.Equal(sdkManifest.AppxLocations, null);
                Assert.Equal(sdkManifest.CopyRedistToSubDirectory, null);
                Assert.Equal(sdkManifest.DependsOnSDK, null);
                Assert.Equal(sdkManifest.DisplayName, "Windows");
                Assert.Equal(sdkManifest.FrameworkIdentities, null);
                Assert.Equal(sdkManifest.FrameworkIdentity, null);
                Assert.Equal(sdkManifest.MaxPlatformVersion, null);
                Assert.Equal(sdkManifest.MinVSVersion, "11.0");
                Assert.Equal(sdkManifest.MinOSVersion, "6.2.1");
                Assert.Equal(sdkManifest.PlatformIdentity, "Windows, version=8.0");
                Assert.Equal(sdkManifest.ProductFamilyName, null);
                Assert.Equal(sdkManifest.SDKType, SDKType.Unspecified);
                Assert.Equal(sdkManifest.SupportedArchitectures, null);
                Assert.Equal(sdkManifest.SupportPrefer32Bit, null);
                Assert.Equal(sdkManifest.SupportsMultipleVersions, MultipleVersionSupport.Allow);
                Assert.Equal(sdkManifest.ReadError, false);

                string manifestExtensionSDK = @"
                <FileList
                    DisplayName = ""My SDK""
                    ProductFamilyName = ""UnitTest SDKs""
                    FrameworkIdentity-Debug = ""Name=MySDK.10.Debug, MinVersion=1.0.0.0""
                    FrameworkIdentity-Retail = ""Name=MySDK.10, MinVersion=1.0.0.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""11.0""
                    AppliesTo = ""WindowsAppContainer + WindowsXAML""
                    SupportPrefer32Bit = ""True""
                    SupportedArchitectures = ""x86;x64;ARM""
                    SupportsMultipleVersions = ""Error""
                    AppX-Debug-x86 = "".\AppX\Debug\x86\Microsoft.MySDK.x86.Debug.1.0.appx""
                    AppX-Debug-x64 = "".\AppX\Debug\x64\Microsoft.MySDK.x64.Debug.1.0.appx""
                    AppX-Debug-ARM = "".\AppX\Debug\ARM\Microsoft.MySDK.ARM.Debug.1.0.appx""
                    AppX-Retail-x86 = "".\AppX\Retail\x86\Microsoft.MySDK.x86.1.0.appx""
                    AppX-Retail-x64 = "".\AppX\Retail\x64\Microsoft.MySDK.x64.1.0.appx""
                    AppX-Retail-ARM = "".\AppX\Retail\ARM\Microsoft.MySDK.ARM.1.0.appx"" 
                    CopyRedistToSubDirectory = "".""
                    DependsOn = ""SDKB, version=2.0""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK""
                    MaxPlatformVersion = ""8.0""
                    MinOSVersion = ""6.2.1""
                    MaxOSVersionTested = ""6.2.3"">

                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";


                File.WriteAllText(manifestFile, manifestExtensionSDK);
                sdkManifest = new SDKManifest(manifestPath);

                Assert.True(sdkManifest.AppxLocations.ContainsKey("AppX-Debug-x86"));
                Assert.True(sdkManifest.AppxLocations.ContainsKey("AppX-Debug-x64"));
                Assert.True(sdkManifest.AppxLocations.ContainsKey("AppX-Debug-ARM"));

                Assert.True(sdkManifest.AppxLocations.ContainsKey("AppX-Retail-x86"));
                Assert.True(sdkManifest.AppxLocations.ContainsKey("AppX-Retail-x64"));
                Assert.True(sdkManifest.AppxLocations.ContainsKey("AppX-Retail-ARM"));

                Assert.Equal(sdkManifest.AppxLocations["AppX-Debug-x86"], ".\\AppX\\Debug\\x86\\Microsoft.MySDK.x86.Debug.1.0.appx");
                Assert.Equal(sdkManifest.AppxLocations["AppX-Debug-x64"], ".\\AppX\\Debug\\x64\\Microsoft.MySDK.x64.Debug.1.0.appx");
                Assert.Equal(sdkManifest.AppxLocations["AppX-Debug-ARM"], ".\\AppX\\Debug\\ARM\\Microsoft.MySDK.ARM.Debug.1.0.appx");

                Assert.Equal(sdkManifest.AppxLocations["AppX-Retail-x86"], ".\\AppX\\Retail\\x86\\Microsoft.MySDK.x86.1.0.appx");
                Assert.Equal(sdkManifest.AppxLocations["AppX-Retail-x64"], ".\\AppX\\Retail\\x64\\Microsoft.MySDK.x64.1.0.appx");
                Assert.Equal(sdkManifest.AppxLocations["AppX-Retail-ARM"], ".\\AppX\\Retail\\ARM\\Microsoft.MySDK.ARM.1.0.appx");

                Assert.Equal(sdkManifest.CopyRedistToSubDirectory, ".");
                Assert.Equal(sdkManifest.DependsOnSDK, "SDKB, version=2.0");
                Assert.Equal(sdkManifest.DisplayName, "My SDK");

                Assert.True(sdkManifest.FrameworkIdentities.ContainsKey("FrameworkIdentity-Debug"));
                Assert.True(sdkManifest.FrameworkIdentities.ContainsKey("FrameworkIdentity-Retail"));

                Assert.Equal(sdkManifest.FrameworkIdentities["FrameworkIdentity-Debug"], "Name=MySDK.10.Debug, MinVersion=1.0.0.0");
                Assert.Equal(sdkManifest.FrameworkIdentities["FrameworkIdentity-Retail"], "Name=MySDK.10, MinVersion=1.0.0.0");

                Assert.Equal(sdkManifest.FrameworkIdentity, null);
                Assert.Equal(sdkManifest.MaxPlatformVersion, "8.0");
                Assert.Equal(sdkManifest.MinVSVersion, "11.0");
                Assert.Equal(sdkManifest.MinOSVersion, "6.2.1");
                Assert.Equal(sdkManifest.MaxOSVersionTested, "6.2.3");
                Assert.Equal(sdkManifest.PlatformIdentity, null);
                Assert.Equal(sdkManifest.ProductFamilyName, "UnitTest SDKs");
                Assert.Equal(sdkManifest.SDKType, SDKType.Unspecified);
                Assert.Equal(sdkManifest.SupportedArchitectures, "x86;x64;ARM");
                Assert.Equal(sdkManifest.SupportPrefer32Bit, "True");
                Assert.Equal(sdkManifest.SupportsMultipleVersions, MultipleVersionSupport.Error);
                Assert.Equal(sdkManifest.MoreInfo, "http://msdn.microsoft.com/MySDK");
                Assert.Equal(sdkManifest.ReadError, false);

                File.WriteAllText(manifestFile, "Hello");
                sdkManifest = new SDKManifest(manifestPath);

                Assert.Equal(sdkManifest.ReadError, true);
            }
            finally
            {
                Directory.Delete(manifestPath, true /* for recursive deletion */);
            }
        }

        /// <summary>
        /// Verify ExtensionSDK
        /// </summary>
        [Fact]
        public void VerifyExtensionSDK()
        {
            string manifestPath = Path.Combine(Path.GetTempPath(), "ManifestTmp");

            try
            {
                Directory.CreateDirectory(manifestPath);

                string manifestFile = Path.Combine(manifestPath, "sdkManifest.xml");

                string manifestExtensionSDK = @"
                <FileList
	                DisplayName = ""My SDK""
	                ProductFamilyName = ""UnitTest SDKs""
	                FrameworkIdentity-Debug = ""Name=MySDK.10.Debug, MinVersion=1.0.0.0""
                    FrameworkIdentity-Retail = ""Name=MySDK.10, MinVersion=1.0.0.0""
	                TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
	                MinVSVersion = ""11.0""
                    AppliesTo = ""WindowsAppContainer + WindowsXAML""
	                SupportPrefer32Bit = ""True""
	                SupportedArchitectures = ""x86;x64;ARM""
	                SupportsMultipleVersions = ""Error""
	                AppX-Debug-x86 = "".\AppX\Debug\x86\Microsoft.MySDK.x86.Debug.1.0.appx""
	                AppX-Debug-x64 = "".\AppX\Debug\x64\Microsoft.MySDK.x64.Debug.1.0.appx""
	                AppX-Debug-ARM = "".\AppX\Debug\ARM\Microsoft.MySDK.ARM.Debug.1.0.appx""
	                AppX-Retail-x86 = "".\AppX\Retail\x86\Microsoft.MySDK.x86.1.0.appx""
	                AppX-Retail-x64 = "".\AppX\Retail\x64\Microsoft.MySDK.x64.1.0.appx""
	                AppX-Retail-ARM = "".\AppX\Retail\ARM\Microsoft.MySDK.ARM.1.0.appx"" 
                    CopyRedistToSubDirectory = "".""
                    DependsOn = ""SDKB, version=2.0""
                    MoreInfo = ""http://msdn.microsoft.com/MySDK""
                    MaxPlatformVersion = ""8.0""
                    MinOSVersion = ""6.2.1""
                    MaxOSVersionTested = ""6.2.1"">

                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                        <Registration Type = ""Flipper"" Implementation = ""XNASprintFlipperImpl.dll"" />
                        <Registration Type = ""Flexer"" Implementation = ""XNASprintFlexerImpl.dll"" />
                        <ToolboxItems VSCategory = ""Toolbox.Default"" />
                    </File>
                </FileList>";

                File.WriteAllText(manifestFile, manifestExtensionSDK);
                ExtensionSDK extensionSDK = new ExtensionSDK(String.Format("CppUnitTestFramework, Version={0}", ObjectModelHelpers.MSBuildDefaultToolsVersion), manifestPath);

                Assert.Equal(extensionSDK.Identifier, "CppUnitTestFramework");
                Assert.Equal(extensionSDK.MaxPlatformVersion, new Version("8.0"));
                Assert.Equal(extensionSDK.MinVSVersion, new Version("11.0"));
                Assert.Equal(extensionSDK.Version, new Version(ObjectModelHelpers.MSBuildDefaultToolsVersion));
            }
            finally
            {
                Directory.Delete(manifestPath, true /* for recursive deletion */);
            }
        }

        /// <summary>
        /// Verify Platform SDKs are filtered correctly
        /// </summary>
        [Fact]
        public void VerifyFilterPlatformSdks()
        {
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "True");

                IList<TargetPlatformSDK> sdkList = ToolLocationHelper.GetTargetPlatformSdks(new string[] { _fakeStructureRoot }, null);
                IList<TargetPlatformSDK> filteredSdkList = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, new Version(6, 2, 5), new Version(12, 0));
                IList<TargetPlatformSDK> filteredSdkList1 = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, new Version(6, 2, 1), new Version(10, 0));
                IList<TargetPlatformSDK> filteredSdkList2 = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, new Version(6, 2, 3), new Version(10, 0));
                IList<TargetPlatformSDK> filteredSdkList3 = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, new Version(6, 2, 3), new Version(11, 0));

                // Filter based only on OS version
                IList<TargetPlatformSDK> filteredSdkList4 = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, new Version(6, 2, 3), null);

                // Filter based only on VS version
                IList<TargetPlatformSDK> filteredSdkList5 = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, null, new Version(10, 0));

                // Pass both versions as null. Don't filter anything
                IList<TargetPlatformSDK> filteredSdkList6 = ToolLocationHelper.FilterTargetPlatformSdks(sdkList, null, null);

                Assert.Equal(sdkList.Count, 7);
                Assert.Equal(filteredSdkList.Count, 7);
                Assert.Equal(filteredSdkList1.Count, 2);
                Assert.Equal(filteredSdkList2.Count, 3);
                Assert.Equal(filteredSdkList3.Count, 4);
                Assert.Equal(filteredSdkList4.Count, 5);
                Assert.Equal(filteredSdkList5.Count, 5);
                Assert.Equal(filteredSdkList6.Count, 7);

                Assert.Equal(filteredSdkList2[0].TargetPlatformIdentifier, "MyPlatform");
                Assert.Equal(filteredSdkList2[2].TargetPlatformVersion, new Version(3, 0));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
            }
        }

        /// <summary>
        /// Verify Extension SDKs are filtered correctly
        /// </summary>
        [Fact]
        public void VerifyFilterPlatformExtensionSdks()
        {
            // Create fake directory tree
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "True");

                IDictionary<string, string> extensionSDKs = ToolLocationHelper.GetPlatformExtensionSDKLocations(new string[] { _fakeStructureRoot }, null, "MyPlatform", new Version(4, 0));
                IDictionary<string, string> filteredExtensionSDKs1 = ToolLocationHelper.FilterPlatformExtensionSDKs(new Version(8, 0), extensionSDKs);
                IDictionary<string, string> filteredExtensionSDKs2 = ToolLocationHelper.FilterPlatformExtensionSDKs(new Version(9, 0), extensionSDKs);
                IDictionary<string, string> filteredExtensionSDKs3 = ToolLocationHelper.FilterPlatformExtensionSDKs(new Version(10, 0), extensionSDKs);

                Assert.Equal(filteredExtensionSDKs1.Count, 2);
                Assert.Equal(filteredExtensionSDKs2.Count, 1);
                Assert.Equal(filteredExtensionSDKs3.Count, 0);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
            }
        }

        /// <summary>
        /// Verify that the GetPlatformExtensionSDKLocation method can be correctly called during evaluation time as a msbuild function.
        /// </summary>
        [Fact]
        public void VerifyGetInstalledSDKLocations()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "VerifyGetInstalledSDKLocations");
            string platformDirectory = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\");
            string sdkDirectory = Path.Combine(platformDirectory, "ExtensionSDKs\\SDkWithManifest\\2.0\\");

            string tempProjectContents = ObjectModelHelpers.CleanupFileContents(@"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier> 
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                    <SDKLocation1>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=2.0','MyPlatform','8.0'))</SDKLocation1>
                    <SDKLocation2>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=V2.0','MyPlatform','8.0'))</SDKLocation2>
                    <SDKLocation3>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('MyPlatform','8.0'))</SDKLocation3>                 
                    <SDKName>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKDisplayName('MyPlatform','8.0'))</SDKName>
                 </PropertyGroup>

                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", testDirectoryRoot);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectoryRoot);
                Directory.CreateDirectory(sdkDirectory);
                File.WriteAllText(Path.Combine(platformDirectory, "sdkManifest.xml"), "HI");
                File.WriteAllText(Path.Combine(sdkDirectory, "sdkManifest.xml"), "HI");
                string testProjectFile = Path.Combine(testDirectoryRoot, "testproject.csproj");

                File.WriteAllText(testProjectFile, tempProjectContents);

                MockLogger logger = new MockLogger();

                ProjectCollection pc = new ProjectCollection();
                Project project = pc.LoadProject(testProjectFile);
                string propertyValue1 = project.GetPropertyValue("SDKLocation1");
                string propertyValue2 = project.GetPropertyValue("SDKLocation2");
                string propertyValue3 = project.GetPropertyValue("SDKLocation3");
                string sdkName = project.GetPropertyValue("SDKName");

                Assert.True(propertyValue1.Equals(sdkDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.Equal(0, propertyValue2.Length);
                Assert.True(propertyValue3.Equals(platformDirectory, StringComparison.OrdinalIgnoreCase));

                // No displayname set in the SDK manifest, so it mocks one up
                Assert.Equal("MyPlatform 8.0", sdkName);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDSDKREFERENCEDIRECTORY", null);
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Verify that the GetPlatformExtensionSDKLocation method can be correctly called during evaluation time as a msbuild function.
        /// </summary>
        [Fact]
        public void VerifyGetInstalledSDKLocations2()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "VerifyGetInstalledSDKLocations2");
            string platformDirectory = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\");
            string sdkDirectory = Path.Combine(platformDirectory, "ExtensionSDKs\\SDkWithManifest\\2.0\\");

            string tempProjectContents = ObjectModelHelpers.CleanupFileContents(@"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier> 
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>" +
                   @"<SDKDirectoryRoot>" + testDirectoryRoot + "</SDKDirectoryRoot>" +
                    @"<SDKLocation1>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=2.0','MyPlatform','8.0', '$(SDKDirectoryRoot)',''))</SDKLocation1>
                      <SDKLocation2>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=V2.0','MyPlatform','8.0', '$(SDKDirectoryRoot)',''))</SDKLocation2>                 
                      <SDKLocation3>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('MyPlatform','8.0', '$(SDKDirectoryRoot)',''))</SDKLocation3>
                      <SDKName>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKDisplayName('MyPlatform','8.0', '$(SDKDirectoryRoot)', ''))</SDKName>
                 </PropertyGroup>

                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

            string platformSDKManifestContents = @"<FileList
                    DisplayName = ""My cool platform SDK!"">
                </FileList>";

            try
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectoryRoot);
                Directory.CreateDirectory(sdkDirectory);
                File.WriteAllText(Path.Combine(platformDirectory, "sdkManifest.xml"), platformSDKManifestContents);
                File.WriteAllText(Path.Combine(sdkDirectory, "sdkManifest.xml"), "HI");
                string testProjectFile = Path.Combine(testDirectoryRoot, "testproject.csproj");

                File.WriteAllText(testProjectFile, tempProjectContents);

                MockLogger logger = new MockLogger();

                ProjectCollection pc = new ProjectCollection();
                Project project = pc.LoadProject(testProjectFile);
                string propertyValue1 = project.GetPropertyValue("SDKLocation1");
                string propertyValue2 = project.GetPropertyValue("SDKLocation2");
                string propertyValue3 = project.GetPropertyValue("SDKLocation3");
                string sdkName = project.GetPropertyValue("SDKName");

                Assert.True(propertyValue1.Equals(sdkDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(propertyValue3.Equals(platformDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.Equal(0, propertyValue2.Length);
                Assert.Equal("My cool platform SDK!", sdkName);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }


        /// <summary>
        /// Setup some fake entries in the registry and verify we get the correct sdk from there.
        /// </summary>
        [Fact]
        public void VerifyGetInstalledSDKLocations3()
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "VerifyGetInstalledSDKLocations3");
            string platformDirectory = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\");
            string sdkDirectory = Path.Combine(platformDirectory, "ExtensionSDKs\\SDkWithManifest\\2.0\\");

            string tempProjectContents = ObjectModelHelpers.CleanupFileContents(@"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier> 
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                    <SDKRegistryRoot>SOFTWARE\Microsoft\VerifyGetInstalledSDKLocations3</SDKRegistryRoot>
                    <SDKDiskRoot>Somewhere</SDKDiskRoot>
                    <SDKLocation1>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=2.0','MyPlatform','8.0', '$(SDKDirectoryRoot)','$(SDKRegistryRoot)'))</SDKLocation1>
                    <SDKLocation2>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformExtensionSDKLocation('SDkWithManifest, Version=V2.0','MyPlatform','8.0', '$(SDKDirectoryRoot)','$(SDKRegistryRoot)'))</SDKLocation2>                 
                    <SDKLocation3>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('MyPlatform','8.0', '$(SDKDirectoryRoot)','$(SDKRegistryRoot)'))</SDKLocation3> 
                    <SDKName>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKDisplayName('MyPlatform','8.0', '$(SDKDirectoryRoot)', '$(SDKRegistryRoot)'))</SDKName>
                 </PropertyGroup>
                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

            string platformSDKManifestContents = @"<FileList
                    DisplayName = ""MyPlatform from the registry""
                    PlatformIdentity = ""MyPlatform, version=8.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""12.0""
                    MinOSVersion = ""6.2.1""
                    MaxOSVersionTested = ""6.2.1""
                    UnsupportedDowntarget = ""MyPlatform, version=8.0"">

                <File Reference = ""Windows"">
	                <ToolboxItems VSCategory = ""Toolbox.Default""/>
                </File>
            </FileList>";

            string registryKey = @"SOFTWARE\Microsoft\VerifyGetInstalledSDKLocations3\";
            RegistryKey baseKey = Registry.CurrentUser;

            try
            {
                RegistryKey folderKey = baseKey.CreateSubKey(registryKey + @"\MyPlatform\v8.0\ExtensionSDKS\SDKWithManifest\2.0");
                folderKey.SetValue("", Path.Combine(testDirectoryRoot, sdkDirectory));

                folderKey = baseKey.CreateSubKey(registryKey + @"\MyPlatform\v8.0");
                folderKey.SetValue("", Path.Combine(testDirectoryRoot, platformDirectory));


                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectoryRoot);
                Directory.CreateDirectory(sdkDirectory);
                File.WriteAllText(Path.Combine(sdkDirectory, "sdkManifest.xml"), "HI");
                File.WriteAllText(Path.Combine(platformDirectory, "sdkManifest.xml"), platformSDKManifestContents);

                string testProjectFile = Path.Combine(testDirectoryRoot, "testproject.csproj");

                File.WriteAllText(testProjectFile, tempProjectContents);

                MockLogger logger = new MockLogger();

                ProjectCollection pc = new ProjectCollection();
                Project project = pc.LoadProject(testProjectFile);
                string propertyValue1 = project.GetPropertyValue("SDKLocation1");
                string propertyValue2 = project.GetPropertyValue("SDKLocation2");
                string propertyValue3 = project.GetPropertyValue("SDKLocation3");
                string sdkName = project.GetPropertyValue("SDKName");

                Assert.True(propertyValue1.Equals(sdkDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(propertyValue3.Equals(platformDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.Equal(0, propertyValue2.Length);
                Assert.Equal("MyPlatform from the registry", sdkName);
            }
            finally
            {
                try
                {
                    baseKey.DeleteSubKeyTree(registryKey);
                }
                catch (Exception)
                {
                }
                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Verify based on a fake directory structure with some good directories and some invalid ones at each level that we 
        /// get the expected set out.
        /// </summary>
        [Fact]
        public void ResolveSDKFromDirectory()
        {
            Dictionary<string, string> extensionSDKs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            List<string> paths = new List<string> { _fakeStructureRoot, _fakeStructureRoot2 };
            Dictionary<TargetPlatformSDK, TargetPlatformSDK> targetPlatforms = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();

            ToolLocationHelper.GatherSDKListFromDirectory(paths, targetPlatforms);

            TargetPlatformSDK key = new TargetPlatformSDK("Windows", new Version("1.0"), null);
            Assert.Equal(2, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=1.0"));
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "Windows\\1.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].Equals(Path.Combine(_fakeStructureRoot, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=2.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=2.0"].Equals(Path.Combine(_fakeStructureRoot, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\2.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("Windows", new Version("2.0"), null);
            Assert.Equal(2, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "Windows\\2.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=3.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=3.0"].Equals(Path.Combine(_fakeStructureRoot, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\3.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=4.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=4.0"].Equals(Path.Combine(_fakeStructureRoot2, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\4.0\\"), StringComparison.OrdinalIgnoreCase));

            // Windows kits special case is only in registry
            key = new TargetPlatformSDK("MyPlatform", new Version("6.0"), null);
            Assert.False(targetPlatforms.ContainsKey(key));

            key = new TargetPlatformSDK("MyPlatform", new Version("4.0"), null);
            Assert.Null(targetPlatforms[key].Path);
            Assert.Equal(1, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("AnotherAssembly, Version=1.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["AnotherAssembly, Version=1.0"].Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\4.0\\ExtensionSDKs\\AnotherAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("MyPlatform", new Version("3.0"), null);
            Assert.Equal(1, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\3.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=1.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\3.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("MyPlatform", new Version("2.0"), null);
            Assert.Equal(1, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\2.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=1.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\2.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("MyPlatform", new Version("1.0"), null);
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\1.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, targetPlatforms[key].ExtensionSDKs.Count);

            key = new TargetPlatformSDK("MyPlatform", new Version("8.0"), null);
            Assert.Equal(Path.Combine(_fakeStructureRoot, "MyPlatform\\8.0\\"), targetPlatforms[key].Path);
            Assert.Equal(0, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.Equal(3, targetPlatforms[key].Platforms.Count);
            Assert.True(targetPlatforms[key].ContainsPlatform("PlatformAssembly", "0.1.2.3"));
            Assert.Equal(Path.Combine(_fakeStructureRoot, "MyPlatform\\8.0\\Platforms\\PlatformAssembly\\0.1.2.3\\"), targetPlatforms[key].Platforms["PlatformAssembly, Version=0.1.2.3"]);
            Assert.True(targetPlatforms[key].ContainsPlatform("PlatformAssembly", "1.2.3.0"));
            Assert.True(targetPlatforms[key].ContainsPlatform("Sparkle", "3.3.3.3"));

            key = new TargetPlatformSDK("MyPlatform", new Version("9.0"), null);
            Assert.Equal(Path.Combine(_fakeStructureRoot, "MyPlatform\\9.0\\"), targetPlatforms[key].Path);
            Assert.Equal(0, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.Equal(1, targetPlatforms[key].Platforms.Count);
            Assert.True(targetPlatforms[key].ContainsPlatform("PlatformAssembly", "0.1.2.3"));
            Assert.Equal(Path.Combine(_fakeStructureRoot, "MyPlatform\\9.0\\Platforms\\PlatformAssembly\\0.1.2.3\\"), targetPlatforms[key].Platforms["PlatformAssembly, Version=0.1.2.3"]);
        }

        /// <summary>
        /// Verify based on a fake directory structure with some good directories and some invalid ones at each level that we 
        /// get the expected set out.
        /// </summary>
        [Fact]
        public void ResolveSDKFromRegistry()
        {
            Dictionary<TargetPlatformSDK, TargetPlatformSDK> targetPlatforms = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();
            ToolLocationHelper.GatherSDKsFromRegistryImpl(targetPlatforms, "Software\\Microsoft\\MicrosoftSDks", RegistryView.Registry32, RegistryHive.CurrentUser, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, _openBaseKey, new FileExists(File.Exists));
            ToolLocationHelper.GatherSDKsFromRegistryImpl(targetPlatforms, "Software\\Microsoft\\MicrosoftSDks", RegistryView.Registry32, RegistryHive.LocalMachine, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, _openBaseKey, new FileExists(File.Exists));

            TargetPlatformSDK key = new TargetPlatformSDK("Windows", new Version("1.0"), null);
            Assert.Equal(2, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "Windows\\1.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=1.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].Equals(Path.Combine(_fakeStructureRoot, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=2.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=2.0"].Equals(Path.Combine(_fakeStructureRoot, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\2.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("Windows", new Version("2.0"), null);
            Assert.Equal(1, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "Windows\\2.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=3.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=3.0"].Equals(Path.Combine(_fakeStructureRoot, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\3.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("MyPlatform", new Version("5.0"), null);
            Assert.True(targetPlatforms.ContainsKey(key));
            Assert.Null(targetPlatforms[key].Path);

            key = new TargetPlatformSDK("MyPlatform", new Version("6.0"), null);
            Assert.True(targetPlatforms.ContainsKey(key));
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "Windows Kits\\6.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("MyPlatform", new Version("4.0"), null);
            Assert.Equal(1, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "SomeOtherPlace\\MyPlatformOtherLocation\\4.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=1.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].Equals(Path.Combine(_fakeStructureRoot, "SomeOtherPlace\\MyPlatformOtherLocation\\4.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("MyPlatform", new Version("9.0"), null);
            Assert.Equal(Path.Combine(_fakeStructureRoot, "MyPlatform\\9.0\\"), targetPlatforms[key].Path);
            Assert.Equal(0, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.Equal(1, targetPlatforms[key].Platforms.Count);
            Assert.True(targetPlatforms[key].ContainsPlatform("PlatformAssembly", "0.1.2.3"));
            Assert.Equal(Path.Combine(_fakeStructureRoot, "MyPlatform\\9.0\\Platforms\\PlatformAssembly\\0.1.2.3\\"), targetPlatforms[key].Platforms["PlatformAssembly, Version=0.1.2.3"]);
        }

        /// <summary>
        /// Verify based on a fake directory structure with some good directories and some invalid ones at each level that we 
        /// get the expected set out. Make sure that when we resolve from both the disk and registry that there are no duplicates
        /// and make sure we get the expected results.
        /// </summary>
        [Fact]
        public void ResolveSDKFromRegistryAndDisk()
        {
            Dictionary<TargetPlatformSDK, TargetPlatformSDK> targetPlatforms = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();

            List<string> paths = new List<string>() { _fakeStructureRoot };

            ToolLocationHelper.GatherSDKListFromDirectory(paths, targetPlatforms);
            ToolLocationHelper.GatherSDKsFromRegistryImpl(targetPlatforms, "Software\\Microsoft\\MicrosoftSDks", RegistryView.Registry32, RegistryHive.CurrentUser, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, _openBaseKey, new FileExists(File.Exists));
            ToolLocationHelper.GatherSDKsFromRegistryImpl(targetPlatforms, "Software\\Microsoft\\MicrosoftSDks", RegistryView.Registry32, RegistryHive.LocalMachine, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, _openBaseKey, new FileExists(File.Exists));

            TargetPlatformSDK key = new TargetPlatformSDK("Windows", new Version("1.0"), null);
            Assert.Equal(2, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "Windows\\1.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=1.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].Equals(Path.Combine(_fakeStructureRoot, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=2.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=2.0"].Equals(Path.Combine(_fakeStructureRoot, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\2.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("Windows", new Version("2.0"), null);
            Assert.Equal(1, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "Windows\\2.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=3.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=3.0"].Equals(Path.Combine(_fakeStructureRoot, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\3.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("MyPlatform", new Version("6.0"), null);
            Assert.True(targetPlatforms.ContainsKey(key));
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "Windows Kits\\6.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("MyPlatform", new Version("5.0"), null);
            Assert.True(targetPlatforms.ContainsKey(key));
            Assert.Equal(0, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.Null(targetPlatforms[key].Path);

            key = new TargetPlatformSDK("MyPlatform", new Version("4.0"), null);
            Assert.Equal(2, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "SomeOtherPlace\\MyPlatformOtherLocation\\4.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=1.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].Equals(Path.Combine(_fakeStructureRoot, "SomeOtherPlace\\MyPlatformOtherLocation\\4.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("AnotherAssembly, Version=1.0"));
            Assert.True(targetPlatforms[key].ExtensionSDKs["AnotherAssembly, Version=1.0"].Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\4.0\\ExtensionSDKs\\AnotherAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("MyPlatform", new Version("3.0"), null);
            Assert.Equal(1, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=1.0"));
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\3.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\3.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("MyPlatform", new Version("2.0"), null);
            Assert.Equal(1, targetPlatforms[key].ExtensionSDKs.Count);
            Assert.True(targetPlatforms[key].ExtensionSDKs.ContainsKey("MyAssembly, Version=1.0"));
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\2.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.True(targetPlatforms[key].ExtensionSDKs["MyAssembly, Version=1.0"].Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\2.0\\ExtensionSDKs\\MyAssembly\\1.0\\"), StringComparison.OrdinalIgnoreCase));

            key = new TargetPlatformSDK("MyPlatform", new Version("1.0"), null);
            Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\1.0\\"), StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, targetPlatforms[key].ExtensionSDKs.Count);
        }

        /// <summary>
        /// Make sure if the sdk identifier is null we get an ArgumentNullException because without specifying the
        /// sdk identifier we can't get any platforms back.
        /// </summary>
        [Fact]
        public void GetPlatformsForSDKNullSDKIdentifier()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ToolLocationHelper.GetPlatformsForSDK(null, new Version("1.0"));
            }
           );
        }
        /// <summary>
        /// Make sure if the sdk version is null we get an ArgumentNullException because without specifying the
        /// sdk version we can't get any platforms back.
        /// </summary>
        [Fact]
        public void GetPlatformsForSDKNullSDKVersion()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                ToolLocationHelper.GetPlatformsForSDK("AnySDK", null);
            }
           );
        }
        /// <summary>
        /// Verify that when there are no sdks with target platforms installed, our list of platforms is empty
        /// to make sure we are not getting platforms from somewhere else.
        /// </summary>
        [Fact]
        public void GetPlatformsForSDKWithNoInstalledTargetPlatforms()
        {
            IEnumerable<string> platforms = ToolLocationHelper.GetPlatformsForSDK("AnySDK", new Version("1.0"), new string[0], "");
            Assert.Equal(false, platforms.Any<string>());
        }

        /// <summary>
        /// Verify that the list of platforms returned is exactly as we expect when we have platforms
        /// installed and we pass in a matching sdk identifier and version number for one of the
        /// installed platforms.
        /// </summary>
        [Fact]
        public void GetPlatformsForSDKWithMatchingInstalledTargetPlatforms()
        {
            IEnumerable<string> myPlatforms = ToolLocationHelper.GetPlatformsForSDK("MyPlatform", new Version("8.0"), new string[] { _fakeStructureRoot }, null);
            Assert.True(myPlatforms.Contains<string>("Sparkle, Version=3.3.3.3"));
            Assert.True(myPlatforms.Contains<string>("PlatformAssembly, Version=0.1.2.3"));
            Assert.True(myPlatforms.Contains<string>("PlatformAssembly, Version=1.2.3.0"));
            Assert.Equal(3, myPlatforms.Count<string>());
        }

        /// <summary>
        /// Verify that the list of platforms is empty if we ask for an sdk that is not installed.
        /// </summary>
        [Fact]
        public void GetPlatformsForSDKWithInstalledTargetPlatformsNoMatch()
        {
            IEnumerable<string> platforms = ToolLocationHelper.GetPlatformsForSDK("DoesNotExistPlatform", new Version("0.0.0.0"), new string[] { _fakeStructureRoot }, null);
            Assert.Equal(false, platforms.Any<string>());
        }

        /// <summary>
        /// Verify that the list of platforms is empty if we ask for a valid sdk identifier but
        /// a version number that isn't installed.
        /// </summary>
        [Fact]
        public void GetPlatformsForSDKWithMatchingPlatformNotMatchingVersion()
        {
            IEnumerable<string> platforms = ToolLocationHelper.GetPlatformsForSDK("MyPlatform", new Version("0.0.0.0"), new string[] { _fakeStructureRoot }, null);
            Assert.Equal(false, platforms.Any<string>());
        }

        /// <summary>
        /// Verify that if we pass in an sdk identifier and version for an installed legacy platform sdk
        /// that the list of platforms is empty because it has no platforms.
        /// </summary>
        [Fact]
        public void GetPlatformsForSDKForLegacyPlatformSDK()
        {
            IEnumerable<string> platforms = ToolLocationHelper.GetPlatformsForSDK("Windows", new Version("8.0"), new string[] { _fakeStructureRoot }, null);
            Assert.Equal(false, platforms.Any<string>());
        }

        /// <summary>
        /// Verify based on a fake directory structure with some good directories and some invalid ones at each level that we 
        /// get the expected set out. Make sure that when we resolve from both the disk and registry that there are no duplicates
        /// and make sure we get the expected results.
        /// </summary>
        [Fact]
        public void GetALLTargetPlatformSDKs()
        {
            try
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", "true");
                var sdks = ToolLocationHelper.GetTargetPlatformSdks(new string[] { _fakeStructureRoot }, null);

                Dictionary<TargetPlatformSDK, TargetPlatformSDK> targetPlatforms = new Dictionary<TargetPlatformSDK, TargetPlatformSDK>();
                foreach (TargetPlatformSDK sdk in sdks)
                {
                    targetPlatforms.Add(sdk, sdk);
                }

                TargetPlatformSDK key = new TargetPlatformSDK("Windows", new Version("1.0"), null);
                Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "Windows\\1.0\\"), StringComparison.OrdinalIgnoreCase));

                key = new TargetPlatformSDK("Windows", new Version("2.0"), null);
                Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "Windows\\2.0\\"), StringComparison.OrdinalIgnoreCase));

                key = new TargetPlatformSDK("MyPlatform", new Version("3.0"), null);
                Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\3.0\\"), StringComparison.OrdinalIgnoreCase));

                key = new TargetPlatformSDK("MyPlatform", new Version("2.0"), null);
                Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\2.0\\"), StringComparison.OrdinalIgnoreCase));

                key = new TargetPlatformSDK("MyPlatform", new Version("1.0"), null);
                Assert.True(targetPlatforms[key].Path.Equals(Path.Combine(_fakeStructureRoot, "MyPlatform\\1.0\\"), StringComparison.OrdinalIgnoreCase));

                key = new TargetPlatformSDK("MyPlatform", new Version("5.0"), null);
                Assert.False(targetPlatforms.ContainsKey(key));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDDISABLEREGISTRYFORSDKLOOKUP", null);
            }
        }

        /// <summary>
        /// Verify that the GetPlatformSDKPropsFileLocation method can be correctly called for pre-OneCore SDKs during evaluation time as a msbuild function.
        /// </summary>
        [Fact]
        public void VerifyGetPreOneCoreSDKPropsLocation()
        {
            // This is the mockup layout for SDKs before One Core SDK.
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "VerifyGetPreOneCoreSDKPropsLocation");
            string platformDirectory = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\");
            string propsDirectory = Path.Combine(platformDirectory, "DesignTime\\CommonConfiguration\\Neutral");

            string tempProjectContents = ObjectModelHelpers.CleanupFileContents(@"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier> 
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                    <SDKRegistryRoot>SOFTWARE\Microsoft\VerifyGetPlatformSDKPropsLocation</SDKRegistryRoot>
                    <SDKDiskRoot>Somewhere</SDKDiskRoot>
                    <PlatformSDKLocation>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('MyPlatform', '8.0', '$(SDKDirectoryRoot)', '$(SDKRegistryRoot)'))</PlatformSDKLocation> 
                    <PropsLocation>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKPropsFileLocation('',' ','MyPlatform',' ','8.0', '$(SDKDirectoryRoot)', '$(SDKRegistryRoot)'))</PropsLocation>
                 </PropertyGroup>
                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

            string registryKey = @"SOFTWARE\Microsoft\VerifyGetPlatformSDKPropsLocation\";
            RegistryKey baseKey = Registry.CurrentUser;

            try
            {
                using (RegistryKey platformKey = baseKey.CreateSubKey(registryKey + @"\MyPlatform\v8.0"))
                {
                    platformKey.SetValue("InstallationFolder", platformDirectory);
                }

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectoryRoot);
                Directory.CreateDirectory(propsDirectory);

                File.WriteAllText(Path.Combine(platformDirectory, "sdkManifest.xml"), "Test");

                Project project = ObjectModelHelpers.CreateInMemoryProject(new ProjectCollection(), tempProjectContents, null);

                string propertyValue = project.GetPropertyValue("PlatformSDKLocation");
                string propsLocation = project.GetPropertyValue("PropsLocation");

                Assert.True(propertyValue.Equals(platformDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(propsLocation.Equals(propsDirectory, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                try
                {
                    baseKey.DeleteSubKeyTree(registryKey);
                }

                catch (Exception)
                {
                }

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Verify that the GetPlatformSDKPropsFileLocation method can be correctly called for OneCore SDK during evaluation time as a msbuild function.
        /// </summary>
        [Fact]
        public void VerifyGetOneCoreSDKPropsLocation()
        {
            // This is the mockup layout for One Core SDK. 
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "VerifyGetOneCoreSDKPropsLocation");
            string platformDirectory = Path.Combine(testDirectoryRoot, "OneCoreSDK\\1.0\\");
            string propsDirectory = Path.Combine(platformDirectory, "DesignTime\\CommonConfiguration\\Neutral\\MyPlatform\\0.8.0.0");
            string platformDirectory2 = Path.Combine(platformDirectory, "Platforms", "MyPlatform", "0.8.0.0");

            string tempProjectContents = ObjectModelHelpers.CleanupFileContents(@"
             <Project DefaultTargets=""ExpandSDKReferenceAssemblies"" ToolsVersion=""msbuilddefaulttoolsversion"" xmlns=""msbuildnamespace"">
                 <PropertyGroup>
                    <TargetPlatformIdentifier>MyPlatform</TargetPlatformIdentifier> 
                    <TargetPlatformVersion>8.0</TargetPlatformVersion>
                    <SDKRegistryRoot>SOFTWARE\Microsoft\VerifyGetOneCoreSDKPropsLocation</SDKRegistryRoot>
                    <SDKDiskRoot>Somewhere</SDKDiskRoot>
                    <PlatformSDKLocation>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKLocation('OneCoreSDK', '1.0', '', '$(SDKRegistryRoot)'))</PlatformSDKLocation> 
                    <PropsLocation>$([Microsoft.Build.Utilities.ToolLocationHelper]::GetPlatformSDKPropsFileLocation('OneCoreSDK','1.0','MyPlatform',' ','0.8.0.0', '', '$(SDKRegistryRoot)'))</PropsLocation>
                 </PropertyGroup>
                 <Import Project=""$(MSBuildBinPath)\Microsoft.Common.targets""/>
              </Project>");

            string registryKey = @"SOFTWARE\Microsoft\VerifyGetOneCoreSDKPropsLocation\";
            RegistryKey baseKey = Registry.CurrentUser;

            try
            {
                using (RegistryKey platformKey = baseKey.CreateSubKey(registryKey + @"\OneCoreSDK\1.0"))
                {
                    platformKey.SetValue("InstallationFolder", platformDirectory);
                }

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }

                Directory.CreateDirectory(testDirectoryRoot);
                Directory.CreateDirectory(propsDirectory);
                Directory.CreateDirectory(platformDirectory2);

                File.WriteAllText(Path.Combine(platformDirectory, "sdkManifest.xml"), "Test");
                File.WriteAllText(Path.Combine(platformDirectory2, "Platform.xml"), "Test");

                Project project = ObjectModelHelpers.CreateInMemoryProject(new ProjectCollection(), tempProjectContents, null);

                string propertyValue = project.GetPropertyValue("PlatformSDKLocation");
                string propsLocation = project.GetPropertyValue("PropsLocation");

                Assert.True(propertyValue.Equals(platformDirectory, StringComparison.OrdinalIgnoreCase));
                Assert.True(propsLocation.Equals(propsDirectory, StringComparison.OrdinalIgnoreCase));
            }
            finally
            {
                try
                {
                    baseKey.DeleteSubKeyTree(registryKey);
                }

                catch (Exception)
                {
                }

                if (Directory.Exists(testDirectoryRoot))
                {
                    FileUtilities.DeleteDirectoryNoThrow(testDirectoryRoot, true);
                }
            }
        }

        /// <summary>
        /// Make a fake SDK structure on disk for testing.
        /// </summary>
        private static string MakeFakeSDKStructure()
        {
            string manifestPlatformSDK1 = @"
                <FileList
                    DisplayName = ""Windows""
                    PlatformIdentity = ""Windows, version=8.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""12.0""
                    MinOSVersion = ""6.2.1""
                    MaxOSVersionTested = ""6.2.1""
                    UnsupportedDowntarget = ""Windows, version=8.0"">

                <File Reference = ""Windows"">
	                <ToolboxItems VSCategory = ""Toolbox.Default""/>
                </File>
                </FileList>";

            string manifestPlatformSDK2 = @"
                <FileList
                    DisplayName = ""Windows""
                    PlatformIdentity = ""Windows, version=8.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""11.0""
                    MinOSVersion = ""6.2.2""
                    MaxOSVersionTested = ""6.2.1""
                    UnsupportedDowntarget = ""Windows, version=8.0"">

                <File Reference = ""Windows"">
	                <ToolboxItems VSCategory = ""Toolbox.Default""/>
                </File>
                </FileList>";

            string manifestPlatformSDK3 = @"
                <FileList
                    DisplayName = ""Windows""
                    PlatformIdentity = ""Windows, version=8.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""10.0""
                    MinOSVersion = ""6.2.3""
                    MaxOSVersionTested = ""6.2.1""
                    UnsupportedDowntarget = ""Windows, version=8.0"">

                <File Reference = ""Windows"">
	                <ToolboxItems VSCategory = ""Toolbox.Default""/>
                </File>
                </FileList>";

            string manifestPlatformSDK4 = @"
                <FileList
                    DisplayName = ""Windows""
                    PlatformIdentity = ""Windows, version=8.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""9.0""
                    MinOSVersion = ""6.2.4""
                    MaxOSVersionTested = ""6.2.1""
                    UnsupportedDowntarget = ""Windows, version=8.0"">

                <File Reference = ""Windows"">
	                <ToolboxItems VSCategory = ""Toolbox.Default""/>
                </File>
                </FileList>";

            string manifestPlatformSDK5 = @"
                <FileList
                    DisplayName = ""Windows""
                    PlatformIdentity = ""Windows, version=8.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""8.0""
                    MinOSVersion = ""6.2.5""
                    MaxOSVersionTested = ""6.2.1""
                    UnsupportedDowntarget = ""Windows, version=8.0"">

                <File Reference = ""Windows"">
	                <ToolboxItems VSCategory = ""Toolbox.Default""/>
                </File>
                </FileList>";

            string manifestPlatformSDK6 = @"
                <FileList
                    DisplayName = ""Windows""
                    PlatformIdentity = ""Windows, version=8.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MaxOSVersionTested = ""6.2.1""
                    UnsupportedDowntarget = ""Windows, version=8.0"">

                <File Reference = ""Windows"">
	                <ToolboxItems VSCategory = ""Toolbox.Default""/>
                </File>
                </FileList>";

            string manifestPlatformSDK7 = @"
                <FileList
                    DisplayName = ""Windows""
                    PlatformIdentity = ""Windows, version=8.0""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""8""
                    MinOSVersion = ""Blah""
                    MaxOSVersionTested = ""6.2.1""
                    UnsupportedDowntarget = ""Windows, version=8.0"">

                <File Reference = ""Windows"">
                    <ToolboxItems VSCategory = ""Toolbox.Default""/>
                </File>
                </FileList>";

            string manifestExtensionSDK1 = @"
                <FileList
                    DisplayName = ""ExtensionSDK2""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""11.0""
                    MaxPlatformVersion = ""8.0""
                    MinOSVersion = ""6.2.1""
                    MaxOSVersionTested = ""6.2.1"">

                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                    </File>
                </FileList>";

            string manifestExtensionSDK2 = @"
                <FileList
                    DisplayName = ""ExtensionSDK2""
                    TargetFramework = "".NETCore, version=v4.5; .NETFramework, version=v4.5""
                    MinVSVersion = ""11.0""
                    MaxPlatformVersion = ""9.0""
                    MinOSVersion = ""6.2.1""
                    MaxOSVersionTested = ""6.2.1"">

                    <File Reference = ""MySDK.Sprint.winmd"" Implementation = ""XNASprintImpl.dll"">
                    </File>
                </FileList>";

            string tempPath = Path.Combine(Path.GetTempPath(), "FakeSDKDirectory");
            try
            {
                // Good
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\1.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\2.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\3.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "SomeOtherPlace\\MyPlatformOtherLocation\\4.0\\ExtensionSDKs\\MyAssembly\\1.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "WindowsKits\\6.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\5.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\4.0\\ExtensionSDKs\\AnotherAssembly\\1.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\3.0\\ExtensionSDKs\\MyAssembly\\1.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\2.0\\ExtensionSDKs\\MyAssembly\\1.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\1.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\8.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\8.0\\Platforms\\PlatformAssembly\\0.1.2.3"));
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\8.0\\Platforms\\PlatformAssembly\\1.2.3.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\8.0\\Platforms\\Sparkle\\3.3.3.3"));
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\9.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\9.0\\Platforms\\PlatformAssembly\\0.1.2.3"));
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\9.0\\PlatformAssembly\\Sparkle"));
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\9.0\\Platforms\\PlatformAssembly\\Sparkle"));

                File.WriteAllText(Path.Combine(tempPath, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\1.0", "sdkmanifest.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\2.0", "sdkmanifest.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\3.0", "sdkmanifest.xml"), "Hello");

                File.WriteAllText(Path.Combine(tempPath, "SomeOtherPlace\\MyPlatformOtherLocation\\4.0", "sdkmanifest.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "SomeOtherPlace\\MyPlatformOtherLocation\\4.0\\ExtensionSDKs\\MyAssembly\\1.0", "sdkmanifest.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "Windows\\1.0", "sdkmanifest.xml"), manifestPlatformSDK1);
                File.WriteAllText(Path.Combine(tempPath, "Windows\\2.0", "sdkmanifest.xml"), manifestPlatformSDK2);
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\4.0\\ExtensionSDKs\\AnotherAssembly\\1.0", "sdkmanifest.xml"), manifestExtensionSDK2);
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\3.0", "sdkmanifest.xml"), manifestPlatformSDK3);
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\2.0", "sdkmanifest.xml"), manifestPlatformSDK4);
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\3.0\\ExtensionSDKs\\MyAssembly\\1.0", "sdkmanifest.xml"), manifestExtensionSDK1);
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\2.0\\ExtensionSDKs\\MyAssembly\\1.0", "sdkmanifest.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\1.0", "sdkmanifest.xml"), manifestPlatformSDK5);

                // Contains a couple of sub-platforms
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\8.0", "sdkmanifest.xml"), manifestPlatformSDK6);
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\8.0\\Platforms\\PlatformAssembly\\0.1.2.3", "Platform.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\8.0\\Platforms\\PlatformAssembly\\1.2.3.0", "Platform.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\8.0\\Platforms\\Sparkle\\3.3.3.3", "Platform.xml"), "Hello");

                // Contains invalid sub-platforms as well as valid ones
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\9.0", "sdkmanifest.xml"), manifestPlatformSDK7);
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\9.0\\Platforms\\PlatformAssembly\\0.1.2.3", "Platform.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\9.0\\PlatformAssembly\\Sparkle", "Platform.xml"), "Hello"); // not under the Platforms directory
                File.WriteAllText(Path.Combine(tempPath, "MyPlatform\\9.0\\Platforms\\PlatformAssembly\\Sparkle", "Platform.xml"), "Hello"); // bad version
                Directory.CreateDirectory(Path.Combine(tempPath, "MyPlatform\\9.0\\Platforms\\Sparkle\\3.3.3.3")); // no platform.xml

                //Bad because of v in the sdk version
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\v1.0\\ExtensionSDKs\\AnotherAssembly\\v1.1"));

                //Bad because no extensionsdks directory under the platform version
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\v3.0\\"));

                // Bad because the directory under the identifier is not a version
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\NotAVersion\\"));

                // Bad because the directory under the identifier is not a version
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\NotAVersion\\ExtensionSDKs\\Assembly\\1.0"));
            }
            catch (Exception)
            {
                FileUtilities.DeleteDirectoryNoThrow(tempPath, true);
                return null;
            }

            return tempPath;
        }

        /// <summary>
        /// Make a fake SDK structure on disk for testing.
        /// </summary>
        private static string MakeFakeSDKStructure2()
        {
            string tempPath = Path.Combine(Path.GetTempPath(), "FakeSDKDirectory2");
            try
            {
                // Good
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\1.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\2.0"));
                Directory.CreateDirectory(Path.Combine(tempPath, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\4.0"));

                File.WriteAllText(Path.Combine(tempPath, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\1.0", "sdkmanifest.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\2.0", "sdkmanifest.xml"), "Hello");
                File.WriteAllText(Path.Combine(tempPath, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\4.0", "sdkmanifest.xml"), "Hello");
            }
            catch (Exception)
            {
                FileUtilities.DeleteDirectoryNoThrow(tempPath, true);
                return null;
            }

            return tempPath;
        }
        #endregion

        #region HelperMethods

        /// <summary>
        /// Simplified registry access delegate. Given a baseKey and a subKey, get all of the subkey
        /// names.
        /// </summary>
        /// <param name="baseKey">The base registry key.</param>
        /// <param name="subKey">The subkey</param>
        /// <returns>An enumeration of strings.</returns>
        private static IEnumerable<string> GetRegistrySubKeyNames(RegistryKey baseKey, string subKey)
        {
            if (baseKey == Registry.CurrentUser)
            {
                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "Windows", "MyPlatform" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "v1.0", "1.0" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v1.0\ExtensionSDKs", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "MyAssembly" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\1.0\ExtensionSDKs", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "MyAssembly" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v1.0\ExtensionSDKs\MyAssembly", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "v1.1", "1.0", "2.0", "3.0" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\1.0\ExtensionSDKs\MyAssembly", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "2.0" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "4.0", "5.0", "6.0", "9.0" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\4.0\ExtensionSDKs", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "MyAssembly" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\5.0\ExtensionSDKs", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { String.Empty };
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\4.0\ExtensionSDKs\MyAssembly", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "1.0" };
                }
            }

            if (baseKey == Registry.LocalMachine)
            {
                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "Windows" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "v2.0" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v2.0\ExtensionSDKs", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "MyAssembly" };
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v2.0\ExtensionSDKs\MyAssembly", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return new string[] { "3.0" };
                }
            }

            return new string[] { };
        }

        /// <summary>
        /// Simplified registry access delegate. Given a baseKey and subKey, get the default value
        /// of the subKey.
        /// </summary>
        /// <param name="baseKey">The base registry key.</param>
        /// <param name="subKey">The subkey</param>
        /// <returns>A string containing the default value.</returns>
        private string GetRegistrySubKeyDefaultValue(RegistryKey baseKey, string subKey)
        {
            if (baseKey == Registry.CurrentUser)
            {
                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v1.0\ExtensionSDKs\MyAssembly\1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return Path.Combine(_fakeStructureRoot, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\1.0");
                }
                // This has a v in the sdk version and should not be found but we need a real path incase it is so it will show up in the returned list and fail the test.
                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v1.0\ExtensionSDKs\MyAssembly\v1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return Path.Combine(_fakeStructureRoot, "Windows\\v1.0\\ExtensionSDKs\\MyAssembly\\1.0");
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\1.0\ExtensionSDKs\MyAssembly\2.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return Path.Combine(_fakeStructureRoot, "Windows\\1.0\\ExtensionSDKs\\MyAssembly\\2.0");
                }

                // This has a set of bad char in the returned directory so it should not be allowed.
                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v1.0\ExtensionSDKs\MyAssembly\3.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return _fakeStructureRoot + @"\Windows\1.0\ExtensionSDKs\MyAssembly\<>?/";
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\5.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return Path.Combine(_fakeStructureRoot, "MyPlatform\\5.0");
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\4.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return Path.Combine(_fakeStructureRoot, "SomeOtherPlace\\MyPlatformOtherLocation\\4.0");
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\6.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return Path.Combine(_fakeStructureRoot, "Windows Kits\\6.0");
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\9.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return Path.Combine(_fakeStructureRoot, "MyPlatform\\9.0");
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\MyPlatform\4.0\ExtensionSDKs\MyAssembly\1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return Path.Combine(_fakeStructureRoot, "SomeOtherPlace\\MyPlatformOtherLocation\\4.0\\ExtensionSDKs\\MyAssembly\\1.0");
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v1.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return Path.Combine(_fakeStructureRoot, "Windows\\1.0");
                }
            }

            if (baseKey == Registry.LocalMachine)
            {
                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v2.0\ExtensionSDKs\MyAssembly\3.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return Path.Combine(_fakeStructureRoot, "Windows\\2.0\\ExtensionSDKs\\MyAssembly\\3.0");
                }

                if (String.Compare(subKey, @"Software\Microsoft\MicrosoftSDKs\Windows\v2.0", StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return Path.Combine(_fakeStructureRoot, "Windows\\2.0");
                }
            }

            return null;
        }

        /// <summary>
        /// Registry access delegate. Given a hive and a view, return the registry base key.
        /// </summary>
        private static RegistryKey GetBaseKey(RegistryHive hive, RegistryView view)
        {
            if (hive == RegistryHive.CurrentUser)
            {
                return Registry.CurrentUser;
            }
            else if (hive == RegistryHive.LocalMachine)
            {
                return Registry.LocalMachine;
            }

            return null;
        }

        #endregion
    }
}
