using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests.ResolveAssemblyReference_Tests
{
    /// <summary>
    /// Unit test the cases where we need to determine if the target framework is greater than the current target framework through the use of the target framework attribute
    /// </summary>
    public sealed class VerifyTargetFrameworkAttribute : ResolveAssemblyReferenceTestFixture
    {
        /// <summary>
        /// Verify there are no warnings if the target framework identifier passed to rar and the target framework identifier in the dll do not match.
        /// </summary>
        [Fact]
        public void FrameworksDoNotMatch()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnFoo4Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "BAR, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "BAR";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\DependsOnFoo4Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework but we are a lower version. With a primary reference in the project.
        /// </summary>
        [Fact]
        public void LowerVersionSameFrameworkDirect()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnFoo35Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=v4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\DependsOnFoo35Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework and the same version and a direct reference
        /// </summary>
        [Fact]
        public void SameVersionSameFrameworkDirect()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("DependsOnFoo4Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\DependsOnFoo4Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if the reference was built for a higher framework but specific version is true
        /// </summary>
        [Fact]
        public void HigherVersionButSpecificVersionDirect()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("DependsOnFoo45Framework, Version=4.5.0.0, PublicKeyToken=null, Culture=Neutral");
            item.SetMetadata("SpecificVersion", "true");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\DependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework but we are a lower version.
        /// </summary>
        [Fact]
        public void LowerVersionSameFrameworkInDirect()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("IndirectDependsOnFoo35Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=v4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\IndirectDependsOnFoo35Framework.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Frameworks\DependsOnFoo35Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework and the same version.
        /// </summary>
        [Fact]
        public void SameVersionSameFrameworkInDirect()
        {
            MockEngine e = new MockEngine();

            ITaskItem[] items = new ITaskItem[]
            {
                new TaskItem("IndirectDependsOnFoo4Framework"),
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\IndirectDependsOnFoo4Framework.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Frameworks\DependsOnFoo4Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if it is the same framework and a higher version but specific version is true.
        /// </summary>
        [Fact]
        public void HigherVersionButSpecificVersionInDirect()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("IndirectDependsOnFoo45Framework, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral");
            item.SetMetadata("SpecificVersion", "true");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\IndirectDependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Frameworks\DependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are warnings if there is an indirect reference to a dll that is higher that what the current target framework is.
        /// </summary>
        [Fact]
        public void HigherVersionInDirect()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("IndirectDependsOnFoo45Framework, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t, false);

            Assert.Equal(1, e.Warnings); // "One warning expected in this scenario."
            e.AssertLogContains("MSB3275");
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(0, t.ResolvedFiles.Length);
            Assert.Equal(0, t.ResolvedDependencyFiles.Length);
        }

        /// <summary>
        /// Verify there are no warnings if there is an indirect reference to a dll that is higher that what the current target framework is but IgnoreFrameworkAttributeVersionMismatch is true.
        /// </summary>
        [Fact]
        public void HigherVersionInDirectIgnoreMismatch()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("IndirectDependsOnFoo45Framework, Version=0.0.0.0, PublicKeyToken=null, Culture=Neutral");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            t.IgnoreTargetFrameworkAttributeVersionMismatch = true;
            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(1, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\IndirectDependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
            Assert.True(ContainsItem(t.ResolvedDependencyFiles, @"c:\Frameworks\DependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
        }

        /// <summary>
        /// Verify there are no warnings if there is a direct reference to a dll that is higher that what the current target framework is but the property IgnoreFrameworkAttributeVersionMismatch is true.
        /// </summary>
        [Fact]
        public void HigherVersionDirectIgnoreMismatch()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("DependsOnFoo45Framework");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            t.IgnoreTargetFrameworkAttributeVersionMismatch = true;

            Execute(t);

            Assert.Equal(0, e.Warnings); // "No warnings expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\DependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
        }


        /// <summary>
        /// Verify there are warnings if there is a direct reference to a dll that is higher that what the current target framework is.
        /// </summary>
        [Fact]
        public void HigherVersionDirect()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("DependsOnFoo45Framework");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Execute(t, false);

            Assert.Equal(1, e.Warnings); // "One warning expected in this scenario."
            e.AssertLogContains("MSB3274");
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(0, t.ResolvedFiles.Length);
            Assert.Equal(0, t.ResolvedDependencyFiles.Length);
        }

        /// <summary>
        /// Verify there are no warnings if there is a direct reference to a dll that is higher that what the current target framework is but 
        /// find dependencies is false. This is because we do not want to add an extra read for this attribute during the project load phase. 
        /// which has dependencies set to false.  A regular build or design time build has this set to true so we do the correct check.
        /// </summary>
        [Fact]
        public void HigherVersionDirectDependenciesFalse()
        {
            MockEngine e = new MockEngine();

            TaskItem item = new TaskItem("DependsOnFoo45Framework");

            ITaskItem[] items = new ITaskItem[]
            {
                item
            };

            ResolveAssemblyReference t = new ResolveAssemblyReference();
            t.BuildEngine = e;
            t.Assemblies = items;
            t.FindDependencies = false;
            t.TargetFrameworkMoniker = "Foo, Version=4.0";
            t.TargetFrameworkMonikerDisplayName = "Foo";
            t.SearchPaths = new string[] { @"c:\Frameworks\" };
            Assert.True(t.Execute(fileExists, directoryExists, getDirectories, getAssemblyName, getAssemblyMetadata, getRegistrySubKeyNames, getRegistrySubKeyDefaultValue, getLastWriteTime, getRuntimeVersion, openBaseKey, checkIfAssemblyIsInGac, isWinMDFile, readMachineTypeFromPEHeader));


            Assert.Equal(0, e.Warnings); // "No warning expected in this scenario."
            Assert.Equal(0, e.Errors); // "No errors expected in this scenario."
            Assert.Equal(1, t.ResolvedFiles.Length);
            Assert.Equal(0, t.ResolvedDependencyFiles.Length);
            Assert.True(ContainsItem(t.ResolvedFiles, @"c:\Frameworks\DependsOnFoo45Framework.dll")); // "Expected to find assembly, but didn't."
        }
    }
}