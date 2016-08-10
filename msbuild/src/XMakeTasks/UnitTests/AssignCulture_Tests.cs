﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class AssignCulture_Tests
    {
        /*
        * Method:   Basic
        *
        * Test the basic functionality.
        */
        [Fact]
        public void Basic()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Equal(1, t.AssignedFiles.Length);
            Assert.Equal(1, t.CultureNeutralAssignedFiles.Length);
            Assert.Equal("fr", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /*
         * Method:   LooksLikeCultureButIsnt
         *
         * Not everything that looks like a culture, really is.
         * Only a specific set of culture ids should be matched.
         */
        [Fact]
        public void LooksLikeCultureButIsnt()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.yy.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Equal(1, t.AssignedFiles.Length);
            Assert.Equal(1, t.CultureNeutralAssignedFiles.Length);
            Assert.Equal(String.Empty, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("MyResource.yy.resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource.yy.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /*
        * Method:   CultureAttributePrecedence
        *
        * Any pre-existing Culture attribute on the item is to be ignored
        */
        [Fact]
        public void CultureAttributePrecedence()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("Culture", "en-GB");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Equal(1, t.AssignedFiles.Length);
            Assert.Equal(1, t.CultureNeutralAssignedFiles.Length);
            Assert.Equal("fr", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /*
        * Method:   CultureAttributePrecedenceWithBogusCulture
        *
        * This is really a corner case.
        * If the incoming item has a 'Culture' attribute already, but that culture is invalid,
        * we still overwrite that culture.
        */
        [Fact]
        public void CultureAttributePrecedenceWithBogusCulture()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("Culture", "invalid");   // Bogus culture.
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Equal(1, t.AssignedFiles.Length);
            Assert.Equal(1, t.CultureNeutralAssignedFiles.Length);
            Assert.Equal("fr", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }



        /*
        * Method:   AttributeForwarding
        *
        * Make sure that attributes set on input items are forwarded to ouput items.
        * This applies to every attribute except for the one pointed to by CultureAttribute.
        */
        [Fact]
        public void AttributeForwarding()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("MyAttribute", "My Random String");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Equal(1, t.AssignedFiles.Length);
            Assert.Equal(1, t.CultureNeutralAssignedFiles.Length);
            Assert.Equal("fr", t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("My Random String", t.AssignedFiles[0].GetMetadata("MyAttribute"));
            Assert.Equal("MyResource.fr.resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }


        /*
        * Method:   NoCulture
        *
        * Test the case where an item has no embedded culture. For example,
        * "MyResource.resx"
        */
        [Fact]
        public void NoCulture()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Equal(1, t.AssignedFiles.Length);
            Assert.Equal(1, t.CultureNeutralAssignedFiles.Length);
            Assert.Equal(String.Empty, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("MyResource.resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource.resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /*
        * Method:   NoExtension
        *
        * Test the case where an item has no extension. For example "MyResource".
        */
        [Fact]
        public void NoExtension()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Equal(1, t.AssignedFiles.Length);
            Assert.Equal(1, t.CultureNeutralAssignedFiles.Length);
            Assert.Equal(String.Empty, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("MyResource", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /*
        * Method:   DoubleDot
        *
        * Test the case where an item has two dots embedded, but otherwise looks
        * like a well-formed item. For example "MyResource..resx".
        */
        [Fact]
        public void DoubleDot()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource..resx");
            t.Files = new ITaskItem[] { i };
            t.Execute();

            Assert.Equal(1, t.AssignedFiles.Length);
            Assert.Equal(1, t.CultureNeutralAssignedFiles.Length);
            Assert.Equal(String.Empty, t.AssignedFiles[0].GetMetadata("Culture"));
            Assert.Equal("MyResource..resx", t.AssignedFiles[0].ItemSpec);
            Assert.Equal("MyResource..resx", t.CultureNeutralAssignedFiles[0].ItemSpec);
        }

        /// <summary>
        /// If an item has a "DependentUpon" who's base name matches exactly, then just assume this
        /// is a resource and form that happen to have an embedded culture. That is, don't assign a 
        /// culture to these.
        /// </summary>
        [Fact]
        public void Regress283991()
        {
            AssignCulture t = new AssignCulture();
            t.BuildEngine = new MockEngine();
            ITaskItem i = new TaskItem("MyResource.fr.resx");
            i.SetMetadata("DependentUpon", "MyResourcE.fr.vb");
            t.Files = new ITaskItem[] { i };

            t.Execute();

            Assert.Equal(1, t.AssignedFiles.Length);
            Assert.Equal(0, t.AssignedFilesWithCulture.Length);
            Assert.Equal(1, t.AssignedFilesWithNoCulture.Length);
        }
    }
}



