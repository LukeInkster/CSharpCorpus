﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// <copyright file="GetSDKReferenceFiles_Tests.cs" company="Microsoft">
// </copyright>
// <summary>Tests for the task that extracts the list of reference assemblies from the SDK</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests.GetSDKReferenceFiles_Tests
{
    public class FakeSdkStructure : IDisposable
    {
        private readonly string _fakeSDKStructureRoot;
        public string SdkDirectory { get; }
        public string SdkDirectory2 { get; }

        public FakeSdkStructure()
        {
            string sdkDirectory;
            _fakeSDKStructureRoot = CreateFakeSDKReferenceAssemblyDirectory1(out sdkDirectory);
            SdkDirectory = sdkDirectory;

            string sdkDirectory2;
            CreateFakeSDKReferenceAssemblyDirectory2(out sdkDirectory2);
            SdkDirectory2 = sdkDirectory2;
        }

        public void Dispose()
        {
            if (FileUtilities.DirectoryExistsNoThrow(_fakeSDKStructureRoot))
            {
                FileUtilities.DeleteDirectoryNoThrow(_fakeSDKStructureRoot, true);
            }
        }

        /// <summary>
        /// Create a fake sdk structure on disk
        /// </summary>
        private static string CreateFakeSDKReferenceAssemblyDirectory1(out string sdkDirectory)
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "FakeSDKForReferenceAssemblies");
            sdkDirectory = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\ExtensionSDKs\\SDkWithManifest\\2.0\\");
            string referenceAssemblyDirectoryConfigx86 = Path.Combine(sdkDirectory, "References\\Retail\\X86");
            string referenceAssemblyDirectoryConfigx64 = Path.Combine(sdkDirectory, "References\\Retail\\X64");
            string referenceAssemblyDirectoryConfigNeutral = Path.Combine(sdkDirectory, "References\\Retail\\Neutral");
            string referenceAssemblyDirectoryCommonConfigNeutral = Path.Combine(sdkDirectory, "References\\CommonConfiguration\\Neutral");
            string referenceAssemblyDirectoryCommonConfigX86 = Path.Combine(sdkDirectory, "References\\CommonConfiguration\\X86");
            string referenceAssemblyDirectoryCommonConfigX64 = Path.Combine(sdkDirectory, "References\\CommonConfiguration\\X64");

            string redistDirectoryConfigx86 = Path.Combine(sdkDirectory, "Redist\\Retail\\X86");
            string redistDirectoryConfigx64 = Path.Combine(sdkDirectory, "Redist\\Retail\\X64");
            string redistDirectoryConfigNeutral = Path.Combine(sdkDirectory, "Redist\\Retail\\Neutral");
            string redistDirectoryCommonConfigNeutral = Path.Combine(sdkDirectory, "Redist\\CommonConfiguration\\Neutral");
            string redistDirectoryCommonConfigX86 = Path.Combine(sdkDirectory, "Redist\\CommonConfiguration\\X86");
            string redistDirectoryCommonConfigX64 = Path.Combine(sdkDirectory, "Redist\\CommonConfiguration\\X64");

            string designTimeDirectoryConfigx86 = Path.Combine(sdkDirectory, "DesignTime\\Retail\\X86");
            string designTimeDirectoryConfigNeutral = Path.Combine(sdkDirectory, "DesignTime\\Retail\\Neutral");
            string designTimeDirectoryCommonConfigNeutral = Path.Combine(sdkDirectory, "DesignTime\\CommonConfiguration\\Neutral");
            string designTimeDirectoryCommonConfigX86 = Path.Combine(sdkDirectory, "DesignTime\\CommonConfiguration\\X86");

            Directory.CreateDirectory(testDirectoryRoot);
            Directory.CreateDirectory(sdkDirectory);

            Directory.CreateDirectory(referenceAssemblyDirectoryConfigx86);
            Directory.CreateDirectory(referenceAssemblyDirectoryConfigx64);
            Directory.CreateDirectory(referenceAssemblyDirectoryConfigNeutral);
            Directory.CreateDirectory(referenceAssemblyDirectoryCommonConfigNeutral);
            Directory.CreateDirectory(referenceAssemblyDirectoryCommonConfigX86);
            Directory.CreateDirectory(referenceAssemblyDirectoryCommonConfigX64);

            Directory.CreateDirectory(redistDirectoryConfigx86);
            Directory.CreateDirectory(redistDirectoryConfigx64);
            Directory.CreateDirectory(redistDirectoryConfigNeutral);
            Directory.CreateDirectory(Path.Combine(redistDirectoryConfigNeutral, "ASubDirectory\\TwoDeep"));
            Directory.CreateDirectory(redistDirectoryCommonConfigNeutral);
            Directory.CreateDirectory(redistDirectoryCommonConfigX86);
            Directory.CreateDirectory(redistDirectoryCommonConfigX64);

            Directory.CreateDirectory(designTimeDirectoryConfigx86);
            Directory.CreateDirectory(designTimeDirectoryConfigNeutral);
            Directory.CreateDirectory(designTimeDirectoryCommonConfigNeutral);
            Directory.CreateDirectory(designTimeDirectoryCommonConfigX86);

            string testWinMD = Path.Combine(referenceAssemblyDirectoryConfigx86, "A.winmd");
            string testWinMD64 = Path.Combine(referenceAssemblyDirectoryConfigx64, "A.winmd");
            string testWinMDNeutral = Path.Combine(referenceAssemblyDirectoryConfigNeutral, "B.winmd");
            string testWinMDNeutralWinXML = Path.Combine(referenceAssemblyDirectoryConfigNeutral, "B.xml");
            string testWinMDCommonConfigurationx86 = Path.Combine(referenceAssemblyDirectoryCommonConfigX86, "C.winmd");
            string testWinMDCommonConfigurationx64 = Path.Combine(referenceAssemblyDirectoryCommonConfigX64, "C.winmd");
            string testWinMDCommonConfigurationNeutral = Path.Combine(referenceAssemblyDirectoryCommonConfigNeutral, "D.winmd");
            string testWinMDCommonConfigurationNeutralDupe = Path.Combine(referenceAssemblyDirectoryCommonConfigNeutral, "A.winmd");

            string testRA = Path.Combine(referenceAssemblyDirectoryConfigx86, "E.dll");
            string testRA64 = Path.Combine(referenceAssemblyDirectoryConfigx64, "E.dll");
            string testRANeutral = Path.Combine(referenceAssemblyDirectoryConfigNeutral, "F.dll");
            string testRACommonConfigurationx86 = Path.Combine(referenceAssemblyDirectoryCommonConfigX86, "G.dll");
            string testRACommonConfigurationx64 = Path.Combine(referenceAssemblyDirectoryCommonConfigX64, "G.dll");
            string testRACommonConfigurationNeutral = Path.Combine(referenceAssemblyDirectoryCommonConfigNeutral, "H.dll");
            // Make duplicate of winmd but change to dll extenson so that we can make sure that we eliminate duplicate file names.
            string testRACommonConfigurationNeutralDupe = Path.Combine(referenceAssemblyDirectoryCommonConfigNeutral, "A.dll");

            string redist = Path.Combine(redistDirectoryConfigx86, "A.dll");
            string redist64 = Path.Combine(redistDirectoryConfigx64, "A.dll");
            string redistNeutral = Path.Combine(redistDirectoryConfigNeutral, "ASubDirectory\\TwoDeep\\B.dll");
            string redistNeutralPri = Path.Combine(redistDirectoryConfigNeutral, "B.pri");
            string redistCommonConfigurationx86 = Path.Combine(redistDirectoryCommonConfigX86, "C.dll");
            string redistCommonConfigurationx64 = Path.Combine(redistDirectoryCommonConfigX64, "C.dll");
            string redistCommonConfigurationNeutral = Path.Combine(redistDirectoryCommonConfigNeutral, "D.dll");
            string redistCommonConfigurationNeutralDupe = Path.Combine(redistDirectoryCommonConfigNeutral, "A.dll");


            File.WriteAllText(testWinMDNeutralWinXML, "TestXml");
            File.WriteAllText(testWinMD, "TestWinmd");
            File.WriteAllText(testWinMD64, "TestWinmd");
            File.WriteAllText(testWinMDNeutral, "TestWinmd");
            File.WriteAllText(testWinMDCommonConfigurationNeutral, "TestWinmd");
            File.WriteAllText(testWinMDCommonConfigurationx86, "TestWinmd");
            File.WriteAllText(testWinMDCommonConfigurationx64, "TestWinmd");
            File.WriteAllText(testWinMDCommonConfigurationNeutralDupe, "TestWinmd");
            File.WriteAllText(testRA, "TestWinmd");
            File.WriteAllText(testRA64, "TestWinmd");
            File.WriteAllText(testRANeutral, "TestWinmd");
            File.WriteAllText(testRACommonConfigurationNeutral, "TestWinmd");
            File.WriteAllText(testRACommonConfigurationx86, "TestWinmd");
            File.WriteAllText(testRACommonConfigurationx64, "TestWinmd");
            File.WriteAllText(testRACommonConfigurationNeutralDupe, "TestWinmd");

            File.WriteAllText(redist, "TestWinmd");
            File.WriteAllText(redist64, "TestWinmd");
            File.WriteAllText(redistNeutral, "TestWinmd");
            File.WriteAllText(redistNeutralPri, "TestWinmd");
            File.WriteAllText(redistCommonConfigurationNeutral, "TestWinmd");
            File.WriteAllText(redistCommonConfigurationx86, "TestWinmd");
            File.WriteAllText(redistCommonConfigurationx64, "TestWinmd");
            File.WriteAllText(redistCommonConfigurationNeutralDupe, "TestWinmd");

            return testDirectoryRoot;
        }

        /// <summary>
        /// Create a fake sdk structure on disk
        /// </summary>
        private static string CreateFakeSDKReferenceAssemblyDirectory2(out string sdkDirectory)
        {
            string testDirectoryRoot = Path.Combine(Path.GetTempPath(), "FakeSDKForReferenceAssemblies");
            sdkDirectory = Path.Combine(testDirectoryRoot, "MyPlatform\\8.0\\ExtensionSDKs\\AnotherSDK\\2.0\\");
            string referenceAssemblyDirectoryConfigx86 = Path.Combine(sdkDirectory, "References\\Retail\\X86");
            string redistDirectoryConfigx86 = Path.Combine(sdkDirectory, "Redist\\Retail\\X86");

            Directory.CreateDirectory(testDirectoryRoot);
            Directory.CreateDirectory(sdkDirectory);

            Directory.CreateDirectory(referenceAssemblyDirectoryConfigx86);
            Directory.CreateDirectory(redistDirectoryConfigx86);

            string testWinMD = Path.Combine(referenceAssemblyDirectoryConfigx86, "B.winmd");
            string redist = Path.Combine(redistDirectoryConfigx86, "B.pri");
            string redist2 = Path.Combine(redistDirectoryConfigx86, "B.dll");

            File.WriteAllText(testWinMD, "TestWinmd");
            File.WriteAllText(redist, "TestWinmd");
            File.WriteAllText(redist2, "TestWinmd");

            return testDirectoryRoot;
        }
    }


    /// <summary>
    /// Test the expansion of sdk reference assemblies.
    /// </summary>
    public class GetSDKReferenceFilesTestFixture : IDisposable, IClassFixture<FakeSdkStructure>
    {
        private readonly string _sdkDirectory;
        private readonly string _sdkDirectory2;
        private readonly MockEngine.GetStringDelegate _resourceDelegate;
        private readonly GetAssemblyName _getAssemblyName = GetAssemblyName;
        private readonly GetAssemblyRuntimeVersion _getAssemblyRuntimeVersion = GetImageRuntimeVersion;
        private readonly string _cacheDirectory = Path.Combine(Path.GetTempPath(), "GetSDKReferenceFiles");

        public GetSDKReferenceFilesTestFixture(FakeSdkStructure fakeSdkStructure)
        {
            _sdkDirectory = fakeSdkStructure.SdkDirectory;
            _sdkDirectory2 = fakeSdkStructure.SdkDirectory2;
            _resourceDelegate = AssemblyResources.GetString;

            if (FileUtilities.DirectoryExistsNoThrow(_cacheDirectory))
            {
                FileUtilities.DeleteDirectoryNoThrow(_cacheDirectory, true);
            }

            Directory.CreateDirectory(_cacheDirectory);
        }

        public void Dispose()
        {
            if (FileUtilities.DirectoryExistsNoThrow(_cacheDirectory))
            {
                FileUtilities.DeleteDirectoryNoThrow(_cacheDirectory, true);
            }
        }

        /// <summary>
        /// Make sure there are no outputs if no resolved sdk files are passed in.
        /// </summary>
        [Fact]
        public void PassReferenceWithNoReferenceDirectory()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            ITaskItem item = new TaskItem("C:\\SDKDoesNotExist");
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            t.CacheFileFolderPath = _cacheDirectory;

            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(0, t.References.Length);
            Assert.Equal(0, t.RedistFiles.Length);
        }


        private delegate IList<string> GetSDKFolders(string sdkRoot);
        private delegate IList<string> GetSDKFolders2(string sdkRoot, string configuration, string architecture);

        /// <summary>
        /// Make sure we get the correct folder list when asking for it.
        /// </summary>
        [Fact]
        public void GetSDKReferenceFolders()
        {
            GetSDKFolders getReferenceFolders = new GetSDKFolders(ToolLocationHelper.GetSDKReferenceFolders);
            GetSDKFolders2 getReferenceFolders2 = new GetSDKFolders2(ToolLocationHelper.GetSDKReferenceFolders);

            VerifySDKFolders(getReferenceFolders, getReferenceFolders2, "References", _sdkDirectory);
        }

        private static void VerifySDKFolders(GetSDKFolders singleParamDelegate, GetSDKFolders2 multiParamDelegate, string folderName, string sdkDirectory)
        {
            IList<string> sdkFolders = singleParamDelegate(sdkDirectory);
            Assert.Equal(2, sdkFolders.Count);

            Assert.True(sdkFolders[0].Equals(Path.Combine(sdkDirectory, folderName + "\\Retail\\Neutral\\")));
            Assert.True(sdkFolders[1].Equals(Path.Combine(sdkDirectory, folderName + "\\CommonConfiguration\\Neutral\\")));

            sdkFolders = multiParamDelegate(sdkDirectory, "Retail", "Neutral");
            Assert.Equal(2, sdkFolders.Count);

            Assert.True(sdkFolders[0].Equals(Path.Combine(sdkDirectory, folderName + "\\Retail\\Neutral\\")));
            Assert.True(sdkFolders[1].Equals(Path.Combine(sdkDirectory, folderName + "\\CommonConfiguration\\Neutral\\")));

            sdkFolders = multiParamDelegate(sdkDirectory, "Retail", "X86");
            Assert.Equal(4, sdkFolders.Count);

            Assert.True(sdkFolders[0].Equals(Path.Combine(sdkDirectory, folderName + "\\Retail\\X86\\")));
            Assert.True(sdkFolders[1].Equals(Path.Combine(sdkDirectory, folderName + "\\Retail\\Neutral\\")));
            Assert.True(sdkFolders[2].Equals(Path.Combine(sdkDirectory, folderName + "\\CommonConfiguration\\X86\\")));
            Assert.True(sdkFolders[3].Equals(Path.Combine(sdkDirectory, folderName + "\\CommonConfiguration\\Neutral\\")));
        }

        /// <summary>
        /// Make sure we get the correct folder list when asking for it.
        /// </summary>
        [Fact]
        public void GetSDKRedistFolders()
        {
            GetSDKFolders getRedistFolders = new GetSDKFolders(ToolLocationHelper.GetSDKRedistFolders);
            GetSDKFolders2 getRedistFolders2 = new GetSDKFolders2(ToolLocationHelper.GetSDKRedistFolders);

            VerifySDKFolders(getRedistFolders, getRedistFolders2, "Redist", _sdkDirectory);
        }

        /// <summary>
        /// Make sure we get the correct folder list when asking for it.
        /// </summary>
        [Fact]
        public void GetSDKDesignTimeFolders()
        {
            GetSDKFolders getDesignTimeFolders = new GetSDKFolders(ToolLocationHelper.GetSDKDesignTimeFolders);
            GetSDKFolders2 getDesignTimeFolders2 = new GetSDKFolders2(ToolLocationHelper.GetSDKDesignTimeFolders);

            VerifySDKFolders(getDesignTimeFolders, getDesignTimeFolders2, "DesignTime", _sdkDirectory);
        }

        /// <summary>
        /// Make sure there are no outputs if an sdk which does not exist is passed in.
        /// </summary>
        [Fact]
        public void PassNoSDKReferences()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);

            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(0, t.References.Length);
            Assert.Equal(0, t.RedistFiles.Length);
        }

        /// <summary>
        /// Make sure there are no outputs if expand sdks is not true.
        /// </summary>
        [Fact]
        public void PassReferenceWithExpandFalse()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "false");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(0, t.References.Length);
            Assert.Equal(0, t.RedistFiles.Length);
        }

        /// <summary>
        /// Make sure there are no redist outputs if CopyRedist is false
        /// </summary>
        [Fact]
        public void PassReferenceWithCopyRedistFalse()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("ExpandReferenceAssemblies", "false");
            item.SetMetadata("CopyRedist", "false");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(0, t.References.Length);
            Assert.Equal(0, t.RedistFiles.Length);
        }

        /// <summary>
        /// Verify we get the correct set of reference assemblies and copy local files when the CopyLocal flag is true
        /// </summary>
        [Fact]
        public void GetReferenceAssembliesWhenExpandTrueCopyLocalTrue()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;
            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("CopyLocalExpandedReferenceAssemblies", "true");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(9, t.CopyLocalFiles.Length);
            Assert.Equal(8, t.References.Length);

            string winmd = Path.Combine(_sdkDirectory, "References\\Retail\\X86\\A.winmd");

            Assert.True(t.References[0].ItemSpec.Equals(winmd, StringComparison.OrdinalIgnoreCase));
            Assert.True(Path.GetFileName(t.References[0].ItemSpec).Equals("A.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("ImageRuntime").Equals("WindowsRuntime 1.0;CLR V2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("FusionName").Equals("A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("WinMDFile").Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("WinMDFileType").Equals("Managed", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("CopyLocal").Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));

            Assert.True(Path.GetFileName(t.References[4].ItemSpec).Equals("E.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("ImageRuntime").Equals("CLR V2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("FusionName").Equals("E, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("WinMDFile").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.References[4].GetMetadata("WinMDFileType").Length);
            Assert.True(t.References[4].GetMetadata("CopyLocal").Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));

            Assert.True(Path.GetFileName(t.CopyLocalFiles[0].ItemSpec).Equals("A.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.CopyLocalFiles[0].GetMetadata("ImageRuntime").Equals("WindowsRuntime 1.0;CLR V2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.CopyLocalFiles[0].GetMetadata("FusionName").Equals("A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.CopyLocalFiles[0].GetMetadata("WinMDFile").Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.CopyLocalFiles[0].GetMetadata("WinMDFileType").Equals("Managed", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.CopyLocalFiles[0].GetMetadata("CopyLocal").Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.CopyLocalFiles[0].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.CopyLocalFiles[0].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));

            Assert.True(Path.GetFileName(t.CopyLocalFiles[5].ItemSpec).Equals("E.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.CopyLocalFiles[5].GetMetadata("ImageRuntime").Equals("CLR V2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.CopyLocalFiles[5].GetMetadata("FusionName").Equals("E, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.CopyLocalFiles[5].GetMetadata("WinMDFile").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.CopyLocalFiles[5].GetMetadata("WinMDFileType").Length);
            Assert.True(t.CopyLocalFiles[5].GetMetadata("CopyLocal").Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.CopyLocalFiles[5].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.CopyLocalFiles[5].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));

            Assert.True(Path.GetFileName(t.CopyLocalFiles[2].ItemSpec).Equals("B.xml", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify reference is not processed by GetSDKReferenceFiles when "ReferenceOnly" metadata is set.
        /// </summary>
        [Fact]
        public void VerifyNoCopyWhenReferenceOnlyIsTrue()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item1 = new TaskItem(_sdkDirectory);
            item1.SetMetadata("ExpandReferenceAssemblies", "true");
            item1.SetMetadata("TargetedSDKConfiguration", "Retail");
            item1.SetMetadata("TargetedSDKArchitecture", "x86");
            item1.SetMetadata("CopyLocalExpandedReferenceAssemblies", "false");
            item1.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            ITaskItem item2 = new TaskItem(_sdkDirectory);
            item2.SetMetadata("ExpandReferenceAssemblies", "true");
            item2.SetMetadata("TargetedSDKConfiguration", "Retail");
            item2.SetMetadata("TargetedSDKArchitecture", "x86");
            item2.SetMetadata("CopyLocalExpandedReferenceAssemblies", "false");
            item2.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");
            item2.SetMetadata("RuntimeReferenceOnly", "true");

            // Process both regular and runtime-only references
            t.ResolvedSDKReferences = new ITaskItem[] { item1, item2 };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);

            Assert.Equal(8, t.References.Length);

            // Process regular references
            t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            t.ResolvedSDKReferences = new ITaskItem[] { item1 };
            success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);

            Assert.Equal(8, t.References.Length);

            // Process runtime-only references
            t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            t.ResolvedSDKReferences = new ITaskItem[] { item2 };
            success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);

            Assert.Equal(0, t.References.Length);
        }

        /// <summary>
        /// Verify we get the correct set of reference assemblies and copy local files when the CopyLocal flag is false
        /// </summary>
        [Fact]
        public void GetReferenceAssembliesWhenExpandTrueCopyLocalFalse()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("CopyLocalExpandedReferenceAssemblies", "false");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(8, t.References.Length);

            Assert.True(Path.GetFileName(t.References[0].ItemSpec).Equals("A.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("ImageRuntime").Equals("WindowsRuntime 1.0;CLR V2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("FusionName").Equals("A, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("WinMDFile").Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("WinMDFileType").Equals("Managed", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("CopyLocal").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));

            Assert.True(Path.GetFileName(t.References[1].ItemSpec).Equals("B.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[1].GetMetadata("ImageRuntime").Equals("WindowsRuntime 1.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[1].GetMetadata("FusionName").Equals("B, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[1].GetMetadata("WinMDFile").Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[1].GetMetadata("WinMDFileType").Equals("Native", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[1].GetMetadata("CopyLocal").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[1].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[1].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));

            Assert.True(Path.GetFileName(t.References[4].ItemSpec).Equals("E.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("ImageRuntime").Equals("CLR V2.0.50727", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("FusionName").Equals("E, Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("WinMDFile").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.References[4].GetMetadata("WinMDFileType").Length);
            Assert.True(t.References[4].GetMetadata("CopyLocal").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify that different cache files are created and used correctly for assemblies with the same identity but with files in different directories
        /// Also verifies that when 
        /// </summary>
        [Fact]
        public void VerifyCacheFileNames()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            ITaskItem[] references1 = t.References;

            // Verify the task created a cache file
            string sdkIdentity = item.GetMetadata("OriginalItemSpec");
            string sdkRoot = item.ItemSpec;
            string cacheFile = sdkIdentity + ",Set=" + FileUtilities.GetHexHash(sdkIdentity) + "-" + FileUtilities.GetHexHash(sdkRoot) + ",Hash=*.dat";
            Thread.Sleep(100);
            string[] existingCacheFiles = Directory.GetFiles(_cacheDirectory, cacheFile);
            Assert.Equal(1, existingCacheFiles.Length);

            GetSDKReferenceFiles t2 = new GetSDKReferenceFiles();
            t2.BuildEngine = engine;
            t2.CacheFileFolderPath = _cacheDirectory;

            // Same SDK with different path
            ITaskItem item2 = new TaskItem(_sdkDirectory2);
            item2.SetMetadata("ExpandReferenceAssemblies", "true");
            item2.SetMetadata("TargetedSDKConfiguration", "Retail");
            item2.SetMetadata("TargetedSDKArchitecture", "x86");
            item2.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t2.ResolvedSDKReferences = new ITaskItem[] { item2 };
            bool success2 = t2.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            ITaskItem[] references2 = t2.References;
            Assert.True(success2);

            // References from the two builds should not overlap, otherwise the cache files are being misused
            foreach (var ref2 in references2)
            {
                Assert.Equal(0, references1.Count(i => i.ItemSpec.Equals(ref2.ItemSpec, StringComparison.InvariantCultureIgnoreCase)));
            }

            Thread.Sleep(100);
            string sdkIdentity2 = item.GetMetadata("OriginalItemSpec");
            string sdkRoot2 = item.ItemSpec;
            string cacheFile2 = sdkIdentity2 + ",Set=" + FileUtilities.GetHexHash(sdkIdentity2) + "-" + FileUtilities.GetHexHash(sdkRoot2) + ",Hash=*.dat";
            string[] existingCacheFiles2 = Directory.GetFiles(_cacheDirectory, cacheFile);
            Assert.Equal(1, existingCacheFiles2.Length);

            // There should have two cache files with the same prefix and first hash
            Thread.Sleep(100);
            string[] allCacheFiles = Directory.GetFiles(_cacheDirectory, sdkIdentity2 + ",Set=" + FileUtilities.GetHexHash(sdkIdentity2) + "*");
            Assert.Equal(2, allCacheFiles.Length);
        }

        /// <summary>
        /// Verify the correct reference files are found and that by default we do log the reference files 
        /// added.
        /// </summary>
        [Fact]
        public void VerifyReferencesLogged()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(8, t.References.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[0].ItemSpec.Replace(t.References[0].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[1].ItemSpec.Replace(t.References[1].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[2].ItemSpec.Replace(t.References[2].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[3].ItemSpec.Replace(t.References[3].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[4].ItemSpec.Replace(t.References[4].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[5].ItemSpec.Replace(t.References[5].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[6].ItemSpec.Replace(t.References[6].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[7].ItemSpec.Replace(t.References[7].GetMetadata("SDKRootPath"), String.Empty));

            Assert.True(Path.GetFileName(t.References[0].ItemSpec).Equals("A.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("WinMDFile").Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("WinMDFileType").Equals("Managed", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("CopyLocal").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));

            Assert.True(Path.GetFileName(t.References[4].ItemSpec).Equals("E.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("WinMDFile").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.References[4].GetMetadata("WinMDFileType").Length);
            Assert.True(t.References[4].GetMetadata("CopyLocal").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify the correct reference files are found and that by default we do log the reference files 
        /// added.
        /// </summary>
        [Fact]
        public void VerifyReferencesLoggedFilterOutWinmd()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            t.ReferenceExtensions = new string[] { ".dll" };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(5, t.References.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[0].ItemSpec.Replace(t.References[0].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[1].ItemSpec.Replace(t.References[1].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[2].ItemSpec.Replace(t.References[2].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[3].ItemSpec.Replace(t.References[3].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[4].ItemSpec.Replace(t.References[4].GetMetadata("SDKRootPath"), String.Empty));

            Assert.True(Path.GetFileName(t.References[0].ItemSpec).Equals("A.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("WinMDFile").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.References[0].GetMetadata("WinMDFileType").Length);
            Assert.True(t.References[0].GetMetadata("CopyLocal").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));

            Assert.True(Path.GetFileName(t.References[4].ItemSpec).Equals("h.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("WinMDFile").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.References[4].GetMetadata("WinMDFileType").Length);
            Assert.True(t.References[4].GetMetadata("CopyLocal").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify we log an error if no configuration is on the sdk reference
        /// </summary>
        [Fact]
        public void LogErrorWhenNoConfiguration()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "");
            item.SetMetadata("TargetedSDKArchitecture", "amd64");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.False(success);
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.CannotHaveEmptyTargetConfiguration", _sdkDirectory);
        }

        /// <summary>
        /// Verify we log an error if no configuration is on the sdk reference
        /// </summary>
        [Fact]
        public void LogErrorWhenNoArchitecture()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Debug");
            item.SetMetadata("TargetedSDKArchitecture", "");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.False(success);
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.CannotHaveEmptyTargetArchitecture", _sdkDirectory);
        }


        /// <summary>
        /// Verify the correct reference files are found and that by default we do log the reference files 
        /// added.
        /// </summary>
        [Fact]
        public void VerifyReferencesLoggedAmd64()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "amd64");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(8, t.References.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[0].ItemSpec.Replace(t.References[0].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[1].ItemSpec.Replace(t.References[1].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[2].ItemSpec.Replace(t.References[2].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[3].ItemSpec.Replace(t.References[3].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[4].ItemSpec.Replace(t.References[4].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[5].ItemSpec.Replace(t.References[5].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[6].ItemSpec.Replace(t.References[6].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[7].ItemSpec.Replace(t.References[7].GetMetadata("SDKRootPath"), String.Empty));

            Assert.True(t.References[0].ItemSpec.IndexOf("x64", StringComparison.OrdinalIgnoreCase) > -1);
            Assert.True(Path.GetFileName(t.References[0].ItemSpec).Equals("A.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("ReferenceGrouping").Equals("SDKWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.References[0].GetMetadata("ReferenceGroupingDisplayName").Length);
            Assert.True(t.References[0].GetMetadata("WinMDFile").Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("WinMDFileType").Equals("Managed", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("CopyLocal").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));

            Assert.True(Path.GetFileName(t.References[4].ItemSpec).Equals("E.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("WinMDFile").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.References[4].GetMetadata("WinMDFileType").Length);
            Assert.True(t.References[4].GetMetadata("CopyLocal").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify the correct reference files are found and that by default we do log the reference files 
        /// added.
        /// </summary>
        [Fact]
        public void VerifyReferencesLoggedX64()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x64");
            item.SetMetadata("DisplayName", "SDKWithManifestDisplayName");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(8, t.References.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[0].ItemSpec.Replace(t.References[0].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[1].ItemSpec.Replace(t.References[1].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[2].ItemSpec.Replace(t.References[2].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[3].ItemSpec.Replace(t.References[3].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[4].ItemSpec.Replace(t.References[4].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[5].ItemSpec.Replace(t.References[5].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[6].ItemSpec.Replace(t.References[6].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[7].ItemSpec.Replace(t.References[7].GetMetadata("SDKRootPath"), String.Empty));

            Assert.True(t.References[0].ItemSpec.IndexOf("x64", StringComparison.OrdinalIgnoreCase) > -1);
            Assert.True(Path.GetFileName(t.References[0].ItemSpec).Equals("A.winmd", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("WinMDFile").Equals("true", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("ReferenceGrouping").Equals("SDKWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("WinMDFileType").Equals("Managed", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("ReferenceGroupingDisplayName").Equals("SDKWithManifestDisplayName", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("CopyLocal").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[0].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));

            Assert.True(Path.GetFileName(t.References[4].ItemSpec).Equals("E.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("WinMDFile").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.References[4].GetMetadata("WinMDFileType").Length);
            Assert.True(t.References[4].GetMetadata("CopyLocal").Equals("false", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.References[4].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Verify the correct reference files are found and that if we do not want to log them we can set a property to do so.
        /// </summary>
        [Fact]
        public void VerifyLogReferencesFalse()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            t.LogReferencesList = false;
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(8, t.References.Length);

            engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[0].ItemSpec.Replace(t.References[0].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[1].ItemSpec.Replace(t.References[1].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[2].ItemSpec.Replace(t.References[2].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[3].ItemSpec.Replace(t.References[3].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[4].ItemSpec.Replace(t.References[4].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[5].ItemSpec.Replace(t.References[5].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[6].ItemSpec.Replace(t.References[6].GetMetadata("SDKRootPath"), String.Empty));
            engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingReference", t.References[7].ItemSpec.Replace(t.References[7].GetMetadata("SDKRootPath"), String.Empty));
        }

        /// <summary>
        /// Verify the correct redist files are found and that by default we do not log the redist files 
        /// added.
        /// </summary>
        [Fact]
        public void VerifyRedistFilesLogRedistFalse()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("CopyRedist", "true");
            item.SetMetadata("CopyRedistToSubDirectory", "Super");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            t.LogRedistFilesList = false;
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(8, t.References.Length);
            Assert.Equal(5, t.RedistFiles.Length);

            engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[0].ItemSpec.Replace(t.RedistFiles[0].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[0].GetMetadata("TargetPath"));
            engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[1].ItemSpec.Replace(t.RedistFiles[1].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[1].GetMetadata("TargetPath"));
            engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[2].ItemSpec.Replace(t.RedistFiles[2].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[2].GetMetadata("TargetPath"));
            engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[3].ItemSpec.Replace(t.RedistFiles[3].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[3].GetMetadata("TargetPath"));
            engine.AssertLogDoesntContainMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[4].ItemSpec.Replace(t.RedistFiles[4].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[4].GetMetadata("TargetPath"));

            Assert.True(Path.GetFileName(t.RedistFiles[0].ItemSpec).Equals("A.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[0].GetMetadata("TargetPath").Equals("Super\\A.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[0].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[0].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.RedistFiles[0].GetMetadata("Root").Length);

            Assert.True(Path.GetFileName(t.RedistFiles[1].ItemSpec).Equals("B.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[1].GetMetadata("TargetPath").Equals("Super\\ASubDirectory\\TwoDeep\\B.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[1].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[1].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.RedistFiles[1].GetMetadata("Root").Length);

            Assert.True(Path.GetFileName(t.RedistFiles[2].ItemSpec).Equals("B.PRI", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[2].GetMetadata("TargetPath").Equals("Super\\B.PRI", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[2].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[2].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[2].GetMetadata("Root").Equals("Super", StringComparison.OrdinalIgnoreCase));

            Assert.True(Path.GetFileName(t.RedistFiles[3].ItemSpec).Equals("C.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[3].GetMetadata("TargetPath").Equals("Super\\C.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[3].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[3].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.RedistFiles[3].GetMetadata("Root").Length);

            Assert.True(Path.GetFileName(t.RedistFiles[4].ItemSpec).Equals("D.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[4].GetMetadata("TargetPath").Equals("Super\\D.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[4].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[4].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.RedistFiles[4].GetMetadata("Root").Length);
        }

        /// <summary>
        /// Verify the correct redist files are found and that by default we do not log the redist files 
        /// added.
        /// </summary>
        [Fact]
        public void VerifyRedistFilesLogRedistTrue()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("CopyRedist", "true");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(5, t.RedistFiles.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[0].ItemSpec.Replace(t.RedistFiles[0].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[0].GetMetadata("TargetPath"));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[1].ItemSpec.Replace(t.RedistFiles[1].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[1].GetMetadata("TargetPath"));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[2].ItemSpec.Replace(t.RedistFiles[2].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[2].GetMetadata("TargetPath"));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[3].ItemSpec.Replace(t.RedistFiles[3].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[3].GetMetadata("TargetPath"));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[4].ItemSpec.Replace(t.RedistFiles[4].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[4].GetMetadata("TargetPath"));
        }

        /// <summary>
        /// Verify the correct redist files are found and that by default we do not log the redist files 
        /// added.
        /// </summary>
        [Fact]
        public void VerifyRedistFilesLogRedistTrueX64()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x64");
            item.SetMetadata("CopyRedist", "true");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(5, t.RedistFiles.Length);

            Assert.True(t.RedistFiles[0].ItemSpec.IndexOf("x64", StringComparison.OrdinalIgnoreCase) > -1);
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[0].ItemSpec.Replace(t.RedistFiles[0].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[0].GetMetadata("TargetPath"));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[1].ItemSpec.Replace(t.RedistFiles[1].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[1].GetMetadata("TargetPath"));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[2].ItemSpec.Replace(t.RedistFiles[2].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[2].GetMetadata("TargetPath"));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[3].ItemSpec.Replace(t.RedistFiles[3].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[3].GetMetadata("TargetPath"));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[4].ItemSpec.Replace(t.RedistFiles[4].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[4].GetMetadata("TargetPath"));
        }

        /// <summary>
        /// Verify the correct redist files are found and that by default we do not log the redist files 
        /// added.
        /// </summary>
        [Fact]
        public void VerifyRedistFilesLogRedistTrueAmd64()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "amd64");
            item.SetMetadata("CopyRedist", "true");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(5, t.RedistFiles.Length);

            Assert.True(t.RedistFiles[0].ItemSpec.IndexOf("x64", StringComparison.OrdinalIgnoreCase) > -1);
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[0].ItemSpec.Replace(t.RedistFiles[0].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[0].GetMetadata("TargetPath"));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[1].ItemSpec.Replace(t.RedistFiles[1].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[1].GetMetadata("TargetPath"));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[2].ItemSpec.Replace(t.RedistFiles[2].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[2].GetMetadata("TargetPath"));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[3].ItemSpec.Replace(t.RedistFiles[3].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[3].GetMetadata("TargetPath"));
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.AddingRedistFile", t.RedistFiles[4].ItemSpec.Replace(t.RedistFiles[4].GetMetadata("SDKRootPath"), String.Empty), t.RedistFiles[4].GetMetadata("TargetPath"));
        }

        /// <summary>
        /// Make sure by default conflicts between references are logged as a comment if they are within the sdk itself
        /// </summary>
        [Fact]
        public void LogNoWarningForReferenceConflictWithinSDK()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("CopyRedist", "false");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(8, t.References.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.ConflictReferenceSameSDK", "SDKWithManifest, Version=2.0", "References\\Retail\\X86\\A.winmd", "References\\CommonConfiguration\\Neutral\\A.dll");
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.ConflictReferenceSameSDK", "SDKWithManifest, Version=2.0", "References\\Retail\\X86\\A.winmd", "References\\CommonConfiguration\\Neutral\\A.winmd");
            Assert.Equal(0, engine.Warnings);
        }

        /// <summary>
        /// Make sure that if the LogReferenceConflictsWithinSDKAsWarning is set log a warning for conflicts within an SDK for references.
        /// </summary>
        [Fact]
        public void LogWarningForReferenceConflictWithinSDK()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("CopyRedist", "false");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            t.LogReferenceConflictWithinSDKAsWarning = true;
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(8, t.References.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.ConflictReferenceSameSDK", "SDKWithManifest, Version=2.0", "References\\Retail\\X86\\A.winmd", "References\\CommonConfiguration\\Neutral\\A.dll");
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.ConflictReferenceSameSDK", "SDKWithManifest, Version=2.0", "References\\Retail\\X86\\A.winmd", "References\\CommonConfiguration\\Neutral\\A.winmd");
            Assert.Equal(2, engine.Warnings);
        }

        /// <summary>
        /// Make sure by default conflicts between references are logged as a comment if they are within the sdk itself
        /// </summary>
        [Fact]
        public void LogNoWarningForRedistConflictWithinSDK()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "false");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("CopyRedist", "true");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(5, t.RedistFiles.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.ConflictRedistSameSDK", "A.dll", "SDKWithManifest, Version=2.0", "Redist\\Retail\\X86\\A.dll", "Redist\\CommonConfiguration\\Neutral\\A.dll");
            Assert.Equal(0, engine.Warnings);
        }

        /// <summary>
        /// Make sure that if the LogRedistConflictsWithinSDKAsWarning is set log a warning for conflicts within an SDK for redist files.
        /// </summary>
        [Fact]
        public void LogWarningForRedistConflictWithinSDK()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "false");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("CopyRedist", "true");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item };
            t.LogRedistConflictWithinSDKAsWarning = true;
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);
            Assert.True(success);
            Assert.Equal(5, t.RedistFiles.Length);

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.ConflictRedistSameSDK", "A.dll", "SDKWithManifest, Version=2.0", "Redist\\Retail\\X86\\A.dll", "Redist\\CommonConfiguration\\Neutral\\A.dll");
            Assert.Equal(1, engine.Warnings);
        }

        /// <summary>
        /// Verify if there are conflicts between references or redist files between sdks that we log a warning by default.
        /// </summary>
        [Fact]
        public void LogReferenceAndRedistConflictBetweenSdks()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("CopyRedist", "true");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            ITaskItem item2 = new TaskItem(_sdkDirectory2);
            item2.SetMetadata("ExpandReferenceAssemblies", "true");
            item2.SetMetadata("TargetedSDKConfiguration", "Retail");
            item2.SetMetadata("TargetedSDKArchitecture", "x86");
            item2.SetMetadata("CopyRedist", "true");
            item2.SetMetadata("OriginalItemSpec", "AnotherSDK, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item, item2 };
            t.LogReferencesList = false;
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);

            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(8, t.References.Length);
            Assert.Equal(6, t.RedistFiles.Length);
            Assert.Equal(2, engine.Warnings);

            string redistWinner = Path.Combine(_sdkDirectory, "Redist\\Retail\\Neutral\\B.pri");
            string redistVictim = Path.Combine(_sdkDirectory2, "Redist\\Retail\\X86\\B.pri");
            string referenceWinner = Path.Combine(_sdkDirectory, "References\\Retail\\Neutral\\B.WinMD");
            string referenceVictim = Path.Combine(_sdkDirectory2, "References\\Retail\\X86\\B.WinMD");

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.ConflictRedistDifferentSDK", "B.PRI", "SDKWithManifest, Version=2.0", "AnotherSDK, Version=2.0", redistWinner, redistVictim);
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.ConflictReferenceDifferentSDK", "SDKWithManifest, Version=2.0", "AnotherSDK, Version=2.0", referenceWinner, referenceVictim);
        }


        /// <summary>
        /// If a user create a target path that causes a conflict between two sdks then we want to warn
        /// </summary>
        [Fact]
        public void LogReferenceAndRedistConflictBetweenSdksDueToCustomTargetPath()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("CopyRedist", "true");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            ITaskItem item2 = new TaskItem(_sdkDirectory2);
            item2.SetMetadata("ExpandReferenceAssemblies", "true");
            item2.SetMetadata("TargetedSDKConfiguration", "Retail");
            item2.SetMetadata("TargetedSDKArchitecture", "x86");
            item2.SetMetadata("CopyRedist", "true");
            item2.SetMetadata("OriginalItemSpec", "AnotherSDK, Version=2.0");
            item2.SetMetadata("CopyRedistToSubDirectory", "ASubDirectory\\TwoDeep");

            t.ResolvedSDKReferences = new ITaskItem[] { item, item2 };
            t.LogReferencesList = false;
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);

            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(8, t.References.Length);
            Assert.Equal(6, t.RedistFiles.Length);
            Assert.Equal(2, engine.Warnings);

            string redistWinner = Path.Combine(_sdkDirectory, "Redist\\Retail\\Neutral\\ASubDirectory\\TwoDeep\\B.dll");
            string redistVictim = Path.Combine(_sdkDirectory2, "Redist\\Retail\\X86\\B.dll");

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.ConflictRedistDifferentSDK", "ASUBDIRECTORY\\TWODEEP\\B.DLL", "SDKWithManifest, Version=2.0", "AnotherSDK, Version=2.0", redistWinner, redistVictim);
        }

        /// <summary>
        /// Verify if there are conflicts between references or redist files between sdks that we do not log a warning if a certain property (LogxxxConflictBetweenSDKsAsWarning is set to false.
        /// </summary>
        [Fact]
        public void LogReferenceAndRedistConflictBetweenSdksNowarning()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "true");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("CopyRedist", "true");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            ITaskItem item2 = new TaskItem(_sdkDirectory2);
            item2.SetMetadata("ExpandReferenceAssemblies", "true");
            item2.SetMetadata("TargetedSDKConfiguration", "Retail");
            item2.SetMetadata("TargetedSDKArchitecture", "x86");
            item2.SetMetadata("CopyRedist", "true");
            item2.SetMetadata("OriginalItemSpec", "AnotherSDK, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item, item2 };
            t.LogReferencesList = false;
            t.LogReferenceConflictBetweenSDKsAsWarning = false;
            t.LogRedistConflictBetweenSDKsAsWarning = false;
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);

            Assert.True(success);
            Assert.Equal(0, t.CopyLocalFiles.Length);
            Assert.Equal(8, t.References.Length);
            Assert.Equal(6, t.RedistFiles.Length);
            Assert.Equal(0, engine.Warnings);


            string redistWinner = Path.Combine(_sdkDirectory, "Redist\\Retail\\Neutral\\B.pri");
            string redistVictim = Path.Combine(_sdkDirectory2, "Redist\\Retail\\X86\\B.pri");
            string referenceWinner = Path.Combine(_sdkDirectory, "References\\Retail\\Neutral\\B.WinMD");
            string referenceVictim = Path.Combine(_sdkDirectory2, "References\\Retail\\X86\\B.WinMD");

            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.ConflictRedistDifferentSDK", "B.PRI", "SDKWithManifest, Version=2.0", "AnotherSDK, Version=2.0", redistWinner, redistVictim);
            engine.AssertLogContainsMessageFromResource(_resourceDelegate, "GetSDKReferenceFiles.ConflictReferenceDifferentSDK", "SDKWithManifest, Version=2.0", "AnotherSDK, Version=2.0", referenceWinner, referenceVictim);
        }

        /// <summary>
        /// If there are conflicting redist files between two sdks but their target paths are different then we should copy both to the appx
        /// </summary>
        [Fact]
        public void TwoSDKSConflictRedistButDifferentTargetPaths()
        {
            MockEngine engine = new MockEngine();
            GetSDKReferenceFiles t = new GetSDKReferenceFiles();
            t.BuildEngine = engine;
            t.CacheFileFolderPath = _cacheDirectory;

            ITaskItem item = new TaskItem(_sdkDirectory);
            item.SetMetadata("ExpandReferenceAssemblies", "false");
            item.SetMetadata("TargetedSDKConfiguration", "Retail");
            item.SetMetadata("TargetedSDKArchitecture", "x86");
            item.SetMetadata("CopyRedistToSubDirectory", "SDK1");
            item.SetMetadata("CopyRedist", "true");
            item.SetMetadata("OriginalItemSpec", "SDKWithManifest, Version=2.0");

            ITaskItem item2 = new TaskItem(_sdkDirectory2);
            item2.SetMetadata("ExpandReferenceAssemblies", "false");
            item2.SetMetadata("TargetedSDKConfiguration", "Retail");
            item2.SetMetadata("TargetedSDKArchitecture", "x86");
            item2.SetMetadata("CopyRedistToSubDirectory", "SDK2");
            item2.SetMetadata("CopyRedist", "true");
            item2.SetMetadata("OriginalItemSpec", "AnotherSDK, Version=2.0");

            t.ResolvedSDKReferences = new ITaskItem[] { item, item2 };
            t.LogReferencesList = false;
            bool success = t.Execute(_getAssemblyName, _getAssemblyRuntimeVersion, FileUtilities.FileExistsNoThrow);

            Assert.True(success);
            Assert.Equal(7, t.RedistFiles.Length);
            Assert.Equal(0, engine.Warnings);

            Assert.True(Path.GetFileName(t.RedistFiles[0].ItemSpec).Equals("A.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[0].GetMetadata("TargetPath").Equals("SDK1\\A.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[0].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[0].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.RedistFiles[0].GetMetadata("Root").Length);

            Assert.True(Path.GetFileName(t.RedistFiles[1].ItemSpec).Equals("B.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[1].GetMetadata("TargetPath").Equals("SDK2\\B.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[1].GetMetadata("OriginalItemSpec").Equals("AnotherSDK, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[1].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.RedistFiles[1].GetMetadata("Root").Length);

            Assert.True(Path.GetFileName(t.RedistFiles[2].ItemSpec).Equals("B.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[2].GetMetadata("TargetPath").Equals("SDK1\\ASubDirectory\\TwoDeep\\B.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[2].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[2].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.RedistFiles[2].GetMetadata("Root").Length);

            Assert.True(Path.GetFileName(t.RedistFiles[3].ItemSpec).Equals("B.pri", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[3].GetMetadata("TargetPath").Equals("SDK2\\B.Pri", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[3].GetMetadata("OriginalItemSpec").Equals("AnotherSDK, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[3].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[3].GetMetadata("Root").Equals("SDK2", StringComparison.OrdinalIgnoreCase));

            Assert.True(Path.GetFileName(t.RedistFiles[4].ItemSpec).Equals("B.PRI", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[4].GetMetadata("TargetPath").Equals("SDK1\\B.PRI", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[4].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[4].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[4].GetMetadata("Root").Equals("SDK1", StringComparison.OrdinalIgnoreCase));

            Assert.True(Path.GetFileName(t.RedistFiles[5].ItemSpec).Equals("C.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[5].GetMetadata("TargetPath").Equals("SDK1\\C.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[5].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[5].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.RedistFiles[5].GetMetadata("Root").Length);

            Assert.True(Path.GetFileName(t.RedistFiles[6].ItemSpec).Equals("D.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[6].GetMetadata("TargetPath").Equals("SDK1\\D.dll", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[6].GetMetadata("OriginalItemSpec").Equals("SDkWithManifest, Version=2.0", StringComparison.OrdinalIgnoreCase));
            Assert.True(t.RedistFiles[6].GetMetadata("ResolvedFrom").Equals("GetSDKReferenceFiles", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(0, t.RedistFiles[6].GetMetadata("Root").Length);
        }

        private static AssemblyNameExtension GetAssemblyName(string path)
        {
            if (Path.GetFileName(path).Equals("C.winmd", StringComparison.OrdinalIgnoreCase))
            {
                throw new BadImageFormatException();
            }

            if (Path.GetExtension(path).Equals(".winmd", StringComparison.OrdinalIgnoreCase) || Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                return new AssemblyNameExtension(fileName + ", Version=2.0.0.0, Culture=Neutral, PublicKeyToken=null");
            }

            return null;
        }

        private static string GetImageRuntimeVersion(string path)
        {
            if (Path.GetFileName(path).Equals("A.winmd", StringComparison.OrdinalIgnoreCase))
            {
                return "WindowsRuntime 1.0;CLR V2.0.50727";
            }
            if (Path.GetExtension(path).Equals(".winmd", StringComparison.OrdinalIgnoreCase))
            {
                return "WindowsRuntime 1.0";
            }

            if (Path.GetExtension(path).Equals(".dll", StringComparison.OrdinalIgnoreCase))
            {
                return "CLR V2.0.50727";
            }

            return null;
        }
    }
}
