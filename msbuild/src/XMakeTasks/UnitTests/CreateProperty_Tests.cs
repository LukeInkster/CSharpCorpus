﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Reflection;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Framework;
using Microsoft.Build.Tasks;
using Microsoft.Build.Utilities;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    sealed public class CreateProperty_Tests : IDisposable
    {
        public CreateProperty_Tests()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        public void Dispose()
        {
            ProjectCollection.GlobalProjectCollection.UnloadAllProjects();
        }

        /// <summary>
        /// Make sure that I can use the CreateProperty task to blank out a property value.
        /// </summary>
        [Fact]
        public void CreateBlankProperty()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"

                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <PropertyGroup>
                            <NumberOfProcessors>Twenty</NumberOfProcessors>
                        </PropertyGroup>

                        <Target Name=`Build`>
                            <CreateProperty Value=``>
                                <Output PropertyName=`NumberOfProcessors` TaskParameter=`Value`/>
                            </CreateProperty>
                            <Message Text=`NumberOfProcessors='$(NumberOfProcessors)'`/>
                        </Target>
                    </Project>

                ");

            logger.AssertLogContains("NumberOfProcessors=''");
        }

        /// <summary>
        /// Make sure that I can use the CreateProperty task to create a property
        /// that has a parseable semicolon in it.
        /// </summary>
        [Fact]
        public void CreatePropertyWithSemicolon()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"

                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <CreateProperty Value=`Clean ; Build`>
                                <Output PropertyName=`TargetsToRunLaterProperty` TaskParameter=`Value`/>
                            </CreateProperty>
                            <Message Text=`TargetsToRunLaterProperty = $(TargetsToRunLaterProperty)`/>
                            <CreateItem Include=`$(TargetsToRunLaterProperty)`>
                                <Output ItemName=`TargetsToRunLaterItem` TaskParameter=`Include`/>
                            </CreateItem>
                            <Message Text=`TargetsToRunLaterItem = @(TargetsToRunLaterItem,'----')`/>
                        </Target>
                    </Project>

                ");

            logger.AssertLogContains("TargetsToRunLaterProperty = Clean;Build");
            logger.AssertLogContains("TargetsToRunLaterItem = Clean----Build");
        }

        /// <summary>
        /// Make sure that I can use the CreateProperty task to create a property
        /// that has a literal semicolon in it.
        /// </summary>
        [Fact]
        public void CreatePropertyWithLiteralSemicolon()
        {
            MockLogger logger = ObjectModelHelpers.BuildProjectExpectSuccess(@"

                    <Project ToolsVersion=`msbuilddefaulttoolsversion` xmlns=`msbuildnamespace`>
                        <Target Name=`Build`>
                            <CreateProperty Value=`Clean%3BBuild`>
                                <Output PropertyName=`TargetsToRunLaterProperty` TaskParameter=`Value`/>
                            </CreateProperty>
                            <Message Text=`TargetsToRunLaterProperty = $(TargetsToRunLaterProperty)`/>
                            <CreateItem Include=`$(TargetsToRunLaterProperty)`>
                                <Output ItemName=`TargetsToRunLaterItem` TaskParameter=`Include`/>
                            </CreateItem>
                            <Message Text=`TargetsToRunLaterItem = @(TargetsToRunLaterItem,'----')`/>
                        </Target>
                    </Project>

                ");

            logger.AssertLogContains("TargetsToRunLaterProperty = Clean;Build");
            logger.AssertLogContains("TargetsToRunLaterItem = Clean;Build");
        }
    }
}
