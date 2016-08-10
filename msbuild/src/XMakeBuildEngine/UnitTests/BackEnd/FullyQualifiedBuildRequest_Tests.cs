﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Microsoft.Build.Collections;
using Microsoft.Build.Execution;
using Microsoft.Build.Evaluation;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class FullyQualifiedBuildRequest_Tests
    {
        [Fact]
        public void TestConstructorGood()
        {
            BuildRequestData data1 = new BuildRequestData("foo", new Dictionary<string, string>(), "tools", new string[0], null);
            FullyQualifiedBuildRequest request = new FullyQualifiedBuildRequest(new BuildRequestConfiguration(data1, "2.0"), new string[1] { "foo" }, true);

            request = new FullyQualifiedBuildRequest(new BuildRequestConfiguration(data1, "2.0"), new string[0] { }, true);

            BuildRequestData data3 = new BuildRequestData("foo", new Dictionary<string, string>(), "tools", new string[0], null);
            request = new FullyQualifiedBuildRequest(new BuildRequestConfiguration(data1, "2.0"), new string[0] { }, false);
        }

        [Fact]
        public void TestConstructorBad1()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                FullyQualifiedBuildRequest request = new FullyQualifiedBuildRequest(null, new string[1] { "foo" }, true);
            }
           );
        }
        [Fact]
        public void TestConstructorBad2()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                FullyQualifiedBuildRequest request = new FullyQualifiedBuildRequest(new BuildRequestConfiguration(new BuildRequestData("foo", new Dictionary<string, string>(), "tools", new string[0], null), "2.0"), null, true);
            }
           );
        }
        [Fact]
        public void TestProperties()
        {
            BuildRequestData data = new BuildRequestData("foo", new Dictionary<string, string>(), "tools", new string[0], null);
            BuildRequestConfiguration config = new BuildRequestConfiguration(data, "2.0");
            FullyQualifiedBuildRequest request = new FullyQualifiedBuildRequest(config, new string[1] { "foo" }, true);
            Assert.Equal(request.Config, config);
            Assert.Equal(request.Targets.Length, 1);
            Assert.Equal(request.Targets[0], "foo");
            Assert.Equal(request.ResultsNeeded, true);
        }
    }
}