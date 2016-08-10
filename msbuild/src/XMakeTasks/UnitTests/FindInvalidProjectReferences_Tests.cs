﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for the task that resolves an FindInvalidProjectReferences to a full path on disk</summary>
//-----------------------------------------------------------------------

using System;
using System.IO;
using System.Reflection;
using System.Collections;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;
using Microsoft.Build.Tasks;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using System.Diagnostics;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class FindInvalidProjectReferences_Tests
    {
        /// <summary>
        /// Verify FindInvalidProjectReferences for several target platform monikers
        /// </summary>
        [Fact]
        public void VerifyFindInvalidProjectReferences()
        {
            // Create the engine.
            MockEngine engine = new MockEngine();

            FindInvalidProjectReferences t = new FindInvalidProjectReferences();
            t.TargetPlatformVersion = "8.0";
            t.TargetPlatformIdentifier = "Windows";
            Dictionary<string, string> proj1 = new Dictionary<string, string>();
            proj1["TargetPlatformMoniker"] = "Windows, Version=7.0";

            Dictionary<string, string> proj2 = new Dictionary<string, string>();
            proj2["TargetPlatformMoniker"] = "Windows, Version=8.0";

            Dictionary<string, string> proj3 = new Dictionary<string, string>();
            proj3["TargetPlatformMoniker"] = "Windows, Version=8.1";

            Dictionary<string, string> proj4 = new Dictionary<string, string>();
            proj4["TargetPlatformMoniker"] = "Windows, Version=8.2";

            t.ProjectReferences = new TaskItem[] { new TaskItem("proj1.proj", proj1), new TaskItem("proj2.proj", proj2), new TaskItem("proj3.proj", proj3), new TaskItem("proj4.proj", proj4) };
            t.BuildEngine = engine;
            bool succeeded = t.Execute();
            Assert.True(succeeded);

            string warning1 = ResourceUtilities.FormatResourceString("FindInvalidProjectReferences.WarnWhenVersionIsIncompatible", "Windows", "8.0", "proj1.proj", "Windows, Version=7.0");
            engine.AssertLogDoesntContain(warning1);

            string warning2 = ResourceUtilities.FormatResourceString("FindInvalidProjectReferences.WarnWhenVersionIsIncompatible", "Windows", "8.0", "proj2.proj", "Windows, Version=8.0");
            engine.AssertLogDoesntContain(warning2);

            string warning3 = ResourceUtilities.FormatResourceString("FindInvalidProjectReferences.WarnWhenVersionIsIncompatible", "Windows", "8.0", "proj3.proj", "Windows, Version=8.1");
            engine.AssertLogContains(warning3);

            string warning4 = ResourceUtilities.FormatResourceString("FindInvalidProjectReferences.WarnWhenVersionIsIncompatible", "Windows", "8.0", "proj4.proj", "Windows, Version=8.2");
            engine.AssertLogContains(warning4);

            Assert.Equal(t.InvalidReferences.Length, 2);
            Assert.Equal(t.InvalidReferences[0].ItemSpec, "proj3.proj");
            Assert.Equal(t.InvalidReferences[1].ItemSpec, "proj4.proj");
        }
    }
}
