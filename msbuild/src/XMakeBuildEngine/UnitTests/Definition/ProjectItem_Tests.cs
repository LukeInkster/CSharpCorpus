﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for ProjectInstance internal members</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Linq;
using System.Resources;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Text;
using System.Globalization;

using Microsoft.Build.BackEnd;
using Microsoft.Build.Execution;
using Microsoft.Build.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using System.Threading;
using InvalidProjectFileException = Microsoft.Build.Exceptions.InvalidProjectFileException;
using ProjectItemFactory = Microsoft.Build.Evaluation.ProjectItem.ProjectItemFactory;
using Microsoft.Build.Construction;
using Xunit;

namespace Microsoft.Build.UnitTests.Definition
{
    /// <summary>
    /// Class containing tests for the ProjectItem and related functionality.
    /// </summary>
    public class ProjectItem_Tests
    {
        /// <summary>
        /// Make sure the the CopyFrom actually does a clone.
        /// </summary>
        [Fact]
        public void CopyFromClonesMetadata()
        {
            ProjectItem item1 = GetOneItemFromFragment(@"<i Include='i1'><m>m1</m></i>");
            ProjectItemFactory factory = new ProjectItemFactory(item1.Project, item1.Xml);
            ProjectItem item2 = factory.CreateItem(item1, item1.Project.FullPath);

            item1.SetMetadataValue("m", "m2");
            item1.SetMetadataValue("n", "n1");

            Assert.Equal(1, Helpers.MakeList(item2.Metadata).Count);
            Assert.Equal(String.Empty, item2.GetMetadataValue("n"));
            Assert.Equal(1 + 15 /* built-in metadata */, item2.MetadataCount);

            // Should still point at the same XML items
            Assert.Equal(true, Object.ReferenceEquals(item1.DirectMetadata.First().Xml, item2.DirectMetadata.First().Xml));
        }

        /// <summary>
        /// Get items of item type "i" with using the item xml fragment passed in
        /// </summary>
        private static IList<ProjectItem> GetItemsFromFragment(string fragment)
        {
            string content = String.Format
                (
                ObjectModelHelpers.CleanupFileContents(@"
                    <Project xmlns='msbuildnamespace' ToolsVersion='msbuilddefaulttoolsversion'>
                        <ItemGroup>
                            {0}
                        </ItemGroup>
                    </Project>
                "),
                 fragment
                 );

            IList<ProjectItem> items = GetItems(content);
            return items;
        }

        /// <summary>
        /// Get the item of type "i" using the item Xml fragment provided.
        /// If there is more than one, fail. 
        /// </summary>
        private static ProjectItem GetOneItemFromFragment(string fragment)
        {
            IList<ProjectItem> items = GetItemsFromFragment(fragment);

            Assert.Equal(1, items.Count);
            return items[0];
        }

        /// <summary>
        /// Get the items of type "i" in the project provided
        /// </summary>
        private static IList<ProjectItem> GetItems(string content)
        {
            ProjectRootElement projectXml = ProjectRootElement.Create(XmlReader.Create(new StringReader(content)));
            Project project = new Project(projectXml);
            IList<ProjectItem> item = Helpers.MakeList(project.GetItems("i"));

            return item;
        }
    }
}