using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests.VersioningAndUnification.AutoUnify
{
    public sealed class StronglyNamedDependencyAutoUnify : ResolveAssemblyReferenceTestFixture
    {
        /// <summary>
        /// Return the default search paths.
        /// </summary>
        /// <value></value>
        internal new string[] DefaultPaths
        {
            get { return new string[] { @"C:\MyApp\v0.5", @"C:\MyApp\v1.0", @"C:\MyApp\v2.0", @"C:\MyApp\v3.0", @"C:\MyComponents\v0.5", @"C:\MyComponents\v1.0", @"C:\MyComponents\v2.0", @"C:\MyComponents\v3.0" }; }
        }


        /// <summary>
        /// In this case,
        /// - Two references are passed in:
        ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 1.0.0.0.
        ///   - DependsOnUnified 2.0.0.0 depends on UnifyMe 2.0.0.0.
        /// - The AutoUnify flag is set to 'true'.
        /// - Version 1.0.0.0 of UnifyMe exists.
        /// - Version 2.0.0.0 of UnifyMe exists.
        /// Expected:
        /// - There should be exactly one UnifyMe dependency returned and it should be version 2.0.0.0.
        /// Rationale:
        /// When AutoUnify is true, we need to resolve to the highest version of each particular assembly 
        /// dependency seen.
        /// </summary>
        [Fact]
        public void Exists()
        {
            // This WriteLine is a hack.  On a slow machine, the Tasks unittest fails because remoting
            // times out the object used for remoting console writes.  Adding a write in the middle of
            // keeps remoting from timing out the object.
            Console.WriteLine("Performing VersioningAndUnification.AutoUnify.StronglyNamedDependency.Exists() test");

            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new TaskItem("DependsOnUnified, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AutoUnify = true;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            AssertNoCase("UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));
            AssertNoCase(@"C:\MyComponents\v2.0\UnifyMe.dll", t.ResolvedDependencyFiles[0].ItemSpec);

            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnifiedDependency"), "UniFYme, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                );

            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "1.0.0.0", @"C:\MyApp\v1.0\DependsOnUnified.dll")
                );
        }

        /// <summary>
        /// In this case,
        /// - Two references are passed in:
        ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 1.0.0.0.
        ///   - DependsOnUnified 2.0.0.0 depends on UnifyMe 2.0.0.0.
        /// - The AutoUnify flag is set to 'true'.
        /// - Version 1.0.0.0 of UnifyMe exists.
        /// - Version 2.0.0.0 of UnifyMe exists.
        ///   - DependsOnUnified 2.0.0.0 is on the black list. 
        /// Expected:
        /// - There should be exactly one UnifyMe dependency returned and it should be version 1.0.0.0.
        /// Rationale:
        /// When AutoUnify is true, we need to resolve to the highest version of each particular assembly 
        /// dependency seen. However if the higher assembly is a dependency of an assembly in the black list it should not be considered during unification.
        /// </summary>
        [Fact]
        public void ExistsWithPrimaryReferenceOnBlackList()
        {
            string implicitRedistListContents =
                "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='DependsOnUnified' Version='2.0.0.0' Culture='neutral' PublicKeyToken='b77a5c561934e089' InGAC='false' />" +
                "</FileList >";

            string engineOnlySubset =
                "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                "</FileList >";

            string redistListPath = FileUtilities.GetTemporaryFile();
            string subsetListPath = FileUtilities.GetTemporaryFile();
            try
            {
                File.WriteAllText(redistListPath, implicitRedistListContents);
                File.WriteAllText(subsetListPath, engineOnlySubset);


                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                {
                    new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new TaskItem("DependsOnUnified, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistListPath) };
                t.InstalledAssemblySubsetTables = new TaskItem[] { new TaskItem(subsetListPath) };
                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AutoUnify = true;

                bool succeeded = Execute(t);

                Assert.True(succeeded);
                Assert.Equal(1, t.ResolvedFiles.Length); // "Expected there to only be one resolved file"
                Assert.True(t.ResolvedFiles[0].ItemSpec.Contains(@"C:\MyApp\v1.0\DependsOnUnified.dll")); // "Expected the ItemSpec of the resolved file to be the item spec of the 1.0.0.0 assembly"
                Assert.Equal(1, t.ResolvedDependencyFiles.Length); // "Expected there to be two resolved dependencies"
                AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));
                AssertNoCase(@"C:\MyComponents\v1.0\UnifyMe.dll", t.ResolvedDependencyFiles[0].ItemSpec);

                engine.AssertLogDoesntContain
                    (
                        String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnifiedDependency"), "UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089, ProcessorArchitecture=MSIL")
                    );

                engine.AssertLogDoesntContain
                    (
                        String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "1.0.0.0", @"C:\MyApp\v2.0\DependsOnUnified.dll")
                    );
            }
            finally
            {
                File.Delete(redistListPath);
                File.Delete(subsetListPath);
            }
        }


        /// <summary>
        /// In this case,
        /// - Two references are passed in:
        ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 1.0.0.0.
        ///   - DependsOnUnified 2.0.0.0 depends on UnifyMe 2.0.0.0.
        /// - The AutoUnify flag is set to 'true'.
        /// - Version 1.0.0.0 of UnifyMe exists.
        /// - Version 2.0.0.0 of UnifyMe exists.
        /// - UnifyMe 2.0.0.0 is on the black list
        /// Expected:
        /// - There should be exactly one UnifyMe dependency returned and it should be version 1.0.0.0.
        ///  Also there should be a warning about the primary reference DependsOnUnified 2.0.0.0 having a dependency which was in the black list.
        /// Rationale:
        /// When AutoUnify is true, we need to resolve to the highest version of each particular assembly 
        /// dependency seen. However if the higher assembly is a dependency of an assembly in the black list it should not be considered during unification.
        /// </summary>
        [Fact]
        public void ExistsPromotedDependencyInTheBlackList()
        {
            string implicitRedistListContents =
                "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='UniFYme' Version='2.0.0.0' Culture='neutral' PublicKeyToken='b77a5c561934e089' InGAC='false' />" +
                "</FileList >";

            string engineOnlySubset =
                "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                "</FileList >";

            string redistListPath = FileUtilities.GetTemporaryFile();
            string subsetListPath = FileUtilities.GetTemporaryFile();
            string appConfigFile = null;
            try
            {
                File.WriteAllText(redistListPath, implicitRedistListContents);
                File.WriteAllText(subsetListPath, engineOnlySubset);


                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                {
                    new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };

                // Construct the app.config.
                appConfigFile = WriteAppConfig
                    (
                        "        <dependentAssembly>\n" +
                        "            <assemblyIdentity name='UnifyMe' PublicKeyToken='b77a5c561934e089' culture='neutral' />\n" +
                        "            <bindingRedirect oldVersion='1.0.0.0' newVersion='2.0.0.0' />\n" +
                        "        </dependentAssembly>\n"
                    );

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();
                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistListPath) };
                t.InstalledAssemblySubsetTables = new TaskItem[] { new TaskItem(subsetListPath) };

                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AppConfigFile = appConfigFile;

                bool succeeded = Execute(t, false);

                Assert.True(succeeded);
                Assert.Equal(0, t.ResolvedDependencyFiles.Length);
                engine.AssertLogDoesntContain
                    (
                        String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAppConfig"), "1.0.0.0", appConfigFile, @"C:\MyApp\v1.0\DependsOnUnified.dll")
                    );
            }
            finally
            {
                File.Delete(redistListPath);
                File.Delete(subsetListPath);

                // Cleanup.
                File.Delete(appConfigFile);
            }
        }

        /// <summary>
        /// In this case,
        /// - Two references are passed in:
        ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 1.0.0.0.
        ///   - DependsOnUnified 2.0.0.0 depends on UnifyMe 2.0.0.0.
        /// - The AutoUnify flag is set to 'true'.
        /// - Version 1.0.0.0 of UnifyMe exists.
        /// - Version 2.0.0.0 of UnifyMe exists.
        ///   - UnifyMe 2.0.0.0 is on the black list because it is higher than what is in the redist list, 1.0.0.0 is also in a black list because it is not in the subset but is in the redist list.
        /// Expected:
        /// - There should be no UnifyMe dependency returned 
        /// There should be a warning indicating the primary reference DependsOnUnified 1.0.0.0 has a dependency that in the black list
        /// There should be a warning indicating the primary reference DependsOnUnified 2.0.0.0 has a dependency that in the black list
        /// </summary>
        [Fact]
        public void ExistsWithBothDependentReferenceOnBlackList()
        {
            string implicitRedistListContents =
                "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='UniFYme' Version='1.0.0.0' Culture='neutral' PublicKeyToken='b77a5c561934e089' InGAC='false' />" +
                "</FileList >";

            string engineOnlySubset =
                "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                "</FileList >";

            string redistListPath = FileUtilities.GetTemporaryFile();
            string subsetListPath = FileUtilities.GetTemporaryFile();
            try
            {
                File.WriteAllText(redistListPath, implicitRedistListContents);
                File.WriteAllText(subsetListPath, engineOnlySubset);


                // Create the engine.
                MockEngine engine = new MockEngine();

                ITaskItem[] assemblyNames = new TaskItem[]
                {
                    new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                    new TaskItem("DependsOnUnified, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                };

                // Now, pass feed resolved primary references into ResolveAssemblyReference.
                ResolveAssemblyReference t = new ResolveAssemblyReference();

                t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistListPath) };
                t.InstalledAssemblySubsetTables = new TaskItem[] { new TaskItem(subsetListPath) };
                t.BuildEngine = engine;
                t.Assemblies = assemblyNames;
                t.SearchPaths = DefaultPaths;
                t.AutoUnify = true;

                bool succeeded = Execute(t, false);

                Assert.True(succeeded);
                Assert.Equal(0, t.ResolvedFiles.Length); // "Expected there to be no resolved files"

                Assert.False(ContainsItem(t.ResolvedFiles, @"C:\MyApp\v1.0\DependsOnUnified.dll")); // "Expected the ItemSpec of the resolved file to not be the item spec of the 1.0.0.0 assembly"
                Assert.False(ContainsItem(t.ResolvedFiles, @"C:\MyApp\v2.0\DependsOnUnified.dll")); // "Expected the ItemSpec of the resolved file to not be the item spec of the 2.0.0.0 assembly"
                string stringList = ResolveAssemblyReference.GenerateSubSetName(null, new ITaskItem[] { new TaskItem(subsetListPath) });
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.FailBecauseDependentAssemblyInExclusionList", assemblyNames[0].ItemSpec, "UniFYme, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", stringList));
                engine.AssertLogContains(t.Log.FormatResourceString("ResolveAssemblyReference.DependencyReferenceOutsideOfFramework", assemblyNames[1].ItemSpec, "UniFYme, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", "2.0.0.0", "1.0.0.0"));
            }
            finally
            {
                File.Delete(redistListPath);
                File.Delete(subsetListPath);
            }
        }

        /// <summary>
        /// In this case,
        /// - Three references are passed in:
        ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 1.0.0.0.
        ///   - DependsOnUnified 2.0.0.0 depends on UnifyMe 2.0.0.0.
        ///   - DependsOnUnified 3.0.0.0 depends on UnifyMe 3.0.0.0.
        /// - The AutoUnify flag is set to 'true'.
        /// - Version 1.0.0.0 of UnifyMe exists.
        /// - Version 2.0.0.0 of UnifyMe exists.
        /// - Version 3.0.0.0 of UnifyMe exists.
        /// - Vesion 3.0.0.0 of DependsOn is on black list
        /// Expected:
        /// - There should be exactly one UnifyMe dependency returned and it should be version 2.0.0.0.
        /// - There should be messages saying that 2.0.0.0 was unified from 1.0.0.0.
        /// Rationale:
        /// AutoUnify works even when unifying multiple prior versions.
        /// </summary>
        [Fact]
        public void MultipleUnifiedFromNamesMiddlePrimaryOnBlackList()
        {
            string implicitRedistListContents =
                "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='DependsOnUnified' Version='3.0.0.0' Culture='neutral' PublicKeyToken='b77a5c561934e089' InGAC='false' />" +
                "</FileList >";

            string engineOnlySubset =
                "<FileList Redist='Microsoft-Windows-CLRCoreComp' >" +
                "<File AssemblyName='Microsoft.Build.Engine' Version='2.0.0.0' PublicKeyToken='b03f5f7f11d50a3a' Culture='Neutral' FileVersion='2.0.50727.208' InGAC='true' />" +
                "</FileList >";

            string redistListPath = FileUtilities.GetTemporaryFile();
            string subsetListPath = FileUtilities.GetTemporaryFile();
            File.WriteAllText(redistListPath, implicitRedistListContents);
            File.WriteAllText(subsetListPath, engineOnlySubset);

            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new TaskItem("DependsOnUnified, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new TaskItem("DependsOnUnified, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.InstalledAssemblyTables = new TaskItem[] { new TaskItem(redistListPath) };
            t.InstalledAssemblySubsetTables = new TaskItem[] { new TaskItem(subsetListPath) };

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };
            t.AutoUnify = true;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(2, t.ResolvedFiles.Length); // "Expected to find two resolved assemblies"
            Assert.True(ContainsItem(t.ResolvedFiles, @"C:\MyApp\v1.0\DependsOnUnified.dll")); // "Expected the ItemSpec of the resolved file to be the item spec of the 1.0.0.0 assembly"
            Assert.True(ContainsItem(t.ResolvedFiles, @"C:\MyApp\v2.0\DependsOnUnified.dll")); // "Expected the ItemSpec of the resolved file to be the item spec of the 2.0.0.0 assembly"
            AssertNoCase("UnifyMe, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));
            AssertNoCase(@"C:\MyComponents\v2.0\UnifyMe.dll", t.ResolvedDependencyFiles[0].ItemSpec);

            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnifiedDependency"), "UniFYme, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                );

            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "1.0.0.0", @"C:\MyApp\v1.0\DependsOnUnified.dll")
                );

            engine.AssertLogDoesntContain
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "2.0.0.0", @"C:\MyApp\v2.0\DependsOnUnified.dll")
                );
        }

        /// <summary>
        /// In this case,
        /// - Two references are passed in:
        ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 1.0.0.0.
        ///   - DependsOnUnified 2.0.0.0 depends on UnifyMe 2.0.0.0.
        ///   - DependsOnUnified 3.0.0.0 depends on UnifyMe 2.0.0.0.
        /// - The AutoUnify flag is set to 'true'.
        /// - Version 1.0.0.0 of UnifyMe exists.
        /// - Version 2.0.0.0 of UnifyMe exists.
        /// - Version 3.0.0.0 of UnifyMe exists.
        /// Expected:
        /// - There should be exactly one UnifyMe dependency returned and it should be version 3.0.0.0.
        /// - There should be messages saying that 3.0.0.0 was unified from 1.0.0.0 *and* 2.0.0.0.
        /// Rationale:
        /// AutoUnify works even when unifying multiple prior versions.
        /// </summary>
        [Fact]
        public void MultipleUnifiedFromNames()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new TaskItem("DependsOnUnified, Version=2.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new TaskItem("DependsOnUnified, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };

            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.TargetFrameworkDirectories = new string[] { @"c:\myfx" };
            t.AutoUnify = true;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            AssertNoCase("UnifyMe, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));
            AssertNoCase(@"C:\MyComponents\v3.0\UnifyMe.dll", t.ResolvedDependencyFiles[0].ItemSpec);

            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnifiedDependency"), "UniFYme, Version=3.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
                );

            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "1.0.0.0", @"C:\MyApp\v1.0\DependsOnUnified.dll")
                );

            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "2.0.0.0", @"C:\MyApp\v2.0\DependsOnUnified.dll")
                );
        }

        /// <summary>
        /// In this case,
        /// - Two references are passed in:
        ///   - DependsOnUnified 0.5.0.0 depends on UnifyMe 0.5.0.0.
        ///   - DependsOnUnified 1.0.0.0 depends on UnifyMe 2.0.0.0.
        /// - The AutoUnify flag is set to 'true'.
        /// - Version 0.5.0.0 of UnifyMe *does not* exist.
        /// - Version 1.0.0.0 of UnifyMe exists.
        /// Expected:
        /// - There should be exactly one UnifyMe dependency returned and it should be version 1.0.0.0.
        /// - There should be message saying that 1.0.0.0 was unified from 0.5.0.0
        /// Rationale:
        /// AutoUnify works even when unifying prior versions that don't exist on disk.
        /// </summary>
        [Fact]
        public void LowVersionDoesntExist()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            ITaskItem[] assemblyNames = new TaskItem[]
            {
                new TaskItem("DependsOnUnified, Version=0.5.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"),
                new TaskItem("DependsOnUnified, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089")
            };


            // Now, pass feed resolved primary references into ResolveAssemblyReference.
            ResolveAssemblyReference t = new ResolveAssemblyReference();

            t.BuildEngine = engine;
            t.Assemblies = assemblyNames;
            t.SearchPaths = DefaultPaths;
            t.AutoUnify = true;

            bool succeeded = Execute(t);

            Assert.True(succeeded);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            AssertNoCase("UnifyMe, Version=1.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089", t.ResolvedDependencyFiles[0].GetMetadata("FusionName"));
            engine.AssertLogContains
                (
                    String.Format(AssemblyResources.GetString("ResolveAssemblyReference.UnificationByAutoUnify"), "0.5.0.0", @"C:\MyApp\v0.5\DependsOnUnified.dll")
                );
        }
    }
}