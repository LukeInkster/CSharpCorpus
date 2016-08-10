﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for Project that involve some reference to internal code</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.IO;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Shared;

using InternalUtilities = Microsoft.Build.Internal.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests.Definition
{
    /// <summary>
    /// Tests some manipulations of Project and ProjectCollection that require dealing with internal data. 
    /// </summary>
    public class Project_Internal_Tests
    {
        /// <summary>
        /// Set default tools version; subsequent projects should use it 
        /// </summary>
        [Fact]
        public void SetDefaultToolsVersion()
        {
            string oldValue = Environment.GetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION");

            try
            {
                // In the new world of figuring out the ToolsVersion to use, we completely ignore the default
                // ToolsVersion in the ProjectCollection.  However, this test explicitly depends on modifying 
                // that, so we need to turn the new defaulting behavior off in order to verify that this still works.  
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", "1");
                InternalUtilities.RefreshInternalEnvironmentValues();

                ProjectCollection collection = new ProjectCollection();
                collection.AddToolset(new Toolset("x", @"c:\y", collection, null));

                collection.DefaultToolsVersion = "x";

                Assert.Equal("x", collection.DefaultToolsVersion);

                string content = @"
                    <Project xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'/>
                    </Project>
                ";

                Project project = new Project(XmlReader.Create(new StringReader(content)), null, null, collection);

                Assert.Equal(project.ToolsVersion, "x");
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", oldValue);
                InternalUtilities.RefreshInternalEnvironmentValues();
            }
        }

        /// <summary>
        /// If the ToolsVersion in the project file is bogus, we'll default to the current ToolsVersion and successfully 
        /// load it.  Make sure we can RE-load it, too, and successfully pick up the correct copy of the loaded project. 
        /// 
        /// ... Make sure we can do this even if we're not using the "always default everything to current anyway" codepath. 
        /// </summary>
        [Fact]
        public void ReloadProjectWithInvalidToolsVersionInFile()
        {
            string oldValue = Environment.GetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION");

            try
            {
                // In the new world of figuring out the ToolsVersion to use, we completely ignore the default
                // ToolsVersion in the ProjectCollection.  However, this test explicitly depends on modifying 
                // that, so we need to turn the new defaulting behavior off in order to verify that this still works.  
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", "1");
                InternalUtilities.RefreshInternalEnvironmentValues();

                string content = @"
                    <Project ToolsVersion='bogus' xmlns='http://schemas.microsoft.com/developer/msbuild/2003' >
                        <Target Name='t'/>
                    </Project>
                ";

                Project project = new Project(XmlReader.Create(new StringReader(content)));
                project.FullPath = "c:\\123.proj";

                Project project2 = ProjectCollection.GlobalProjectCollection.LoadProject("c:\\123.proj", null, null);

                Assert.True(Object.ReferenceEquals(project, project2));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", oldValue);
                InternalUtilities.RefreshInternalEnvironmentValues();
            }
        }

        /// <summary>
        /// Project.ToolsVersion should be set to ToolsVersion evaluated with,
        /// even if it is subsequently changed on the XML (without reevaluation)
        /// </summary>
        [Fact]
        public void ProjectToolsVersion20Present()
        {
            if (FrameworkLocationHelper.PathToDotNetFrameworkV20 == null)
            {
                // "Requires 2.0 to be installed"
                return;
            }

            string oldValue = Environment.GetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION");

            try
            {
                // In the new world of figuring out the ToolsVersion to use, we completely ignore what 
                // is written in the project file.  However, this test explicitly depends on effectively
                // modifying the "project file" (through the construction model OM), so we need to turn 
                // that behavior off in order to verify that it still works.  
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", "1");
                InternalUtilities.RefreshInternalEnvironmentValues();

                Project project = new Project();
                project.Xml.ToolsVersion = "2.0";
                project.ReevaluateIfNecessary();

                Assert.Equal("2.0", project.ToolsVersion);

                project.Xml.ToolsVersion = "4.0";

                Assert.Equal("2.0", project.ToolsVersion);
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", oldValue);
                InternalUtilities.RefreshInternalEnvironmentValues();
            }
        }

        /// <summary>
        /// $(MSBuildToolsVersion) should be set to ToolsVersion evaluated with,
        /// even if it is subsequently changed on the XML (without reevaluation)
        /// </summary>
        [Fact]
        public void MSBuildToolsVersionProperty()
        {
            if (FrameworkLocationHelper.PathToDotNetFrameworkV20 == null)
            {
                // "Requires 2.0 to be installed"
                return;
            }

            string oldValue = Environment.GetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION");

            try
            {
                // In the new world of figuring out the ToolsVersion to use, we completely ignore what 
                // is written in the project file.  However, this test explicitly depends on effectively
                // modifying the "project file" (through the construction model OM), so we need to turn 
                // that behavior off in order to verify that it still works.  
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", "1");
                InternalUtilities.RefreshInternalEnvironmentValues();

                Project project = new Project();
                project.Xml.ToolsVersion = "2.0";
                project.ReevaluateIfNecessary();

                Assert.Equal("2.0", project.GetPropertyValue("msbuildtoolsversion"));

                project.Xml.ToolsVersion = "4.0";
                Assert.Equal("2.0", project.GetPropertyValue("msbuildtoolsversion"));

                project.ReevaluateIfNecessary();

                Assert.Equal("4.0", project.GetPropertyValue("msbuildtoolsversion"));
            }
            finally
            {
                Environment.SetEnvironmentVariable("MSBUILDLEGACYDEFAULTTOOLSVERSION", oldValue);
                InternalUtilities.RefreshInternalEnvironmentValues();
            }
        }
    }
}
