﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.Framework;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Evaluation;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Shared;
using Microsoft.Build.Execution;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using Microsoft.Build.Unittest;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    public class BuildResult_Tests
    {
        private int _nodeRequestId;

        public BuildResult_Tests()
        {
            _nodeRequestId = 1;
        }

        [Fact]
        public void TestConstructorGood()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result2 = new BuildResult(request);
        }

        [Fact]
        public void Clone()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result1 = new BuildResult(request);
            result1.ResultsByTarget.Add("FOO", TestUtilities.GetEmptySucceedingTargetResult());
            Assert.True(result1.ResultsByTarget.ContainsKey("foo")); // test comparer

            BuildResult result2 = result1.Clone();

            result1.ResultsByTarget.Add("BAR", TestUtilities.GetEmptySucceedingTargetResult());
            Assert.True(result1.ResultsByTarget.ContainsKey("foo")); // test comparer
            Assert.True(result1.ResultsByTarget.ContainsKey("bar"));

            Assert.Equal(result1.SubmissionId, result2.SubmissionId);
            Assert.Equal(result1.ConfigurationId, result2.ConfigurationId);
            Assert.Equal(result1.GlobalRequestId, result2.GlobalRequestId);
            Assert.Equal(result1.ParentGlobalRequestId, result2.ParentGlobalRequestId);
            Assert.Equal(result1.NodeRequestId, result2.NodeRequestId);
            Assert.Equal(result1.CircularDependency, result2.CircularDependency);
            Assert.Equal(result1.ResultsByTarget["foo"], result2.ResultsByTarget["foo"]);
            Assert.Equal(result1.OverallResult, result2.OverallResult);
        }

        [Fact]
        public void TestConstructorBad()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildResult result = new BuildResult(null);
            }
           );
        }
        [Fact]
        public void TestConfigurationId()
        {
            BuildRequest request = CreateNewBuildRequest(-1, new string[0]);
            BuildResult result = new BuildResult(request);
            Assert.Equal(-1, result.ConfigurationId);

            BuildRequest request2 = CreateNewBuildRequest(1, new string[0]);
            BuildResult result2 = new BuildResult(request2);
            Assert.Equal(1, result2.ConfigurationId);
        }

        [Fact]
        public void TestExceptionGood()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            Assert.Null(result.Exception);

            AccessViolationException e = new AccessViolationException();
            result = new BuildResult(request, e);

            Assert.Equal(e, result.Exception);
        }

        [Fact]
        public void TestOverallResult()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            Assert.Equal(BuildResultCode.Success, result.OverallResult);

            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            Assert.Equal(BuildResultCode.Success, result.OverallResult);

            result.AddResultsForTarget("bar", new TargetResult(new TaskItem[0] { }, new WorkUnitResult(WorkUnitResultCode.Success, WorkUnitActionCode.Continue, new Exception())));
            Assert.Equal(BuildResultCode.Success, result.OverallResult);

            result.AddResultsForTarget("baz", new TargetResult(new TaskItem[0] { }, TestUtilities.GetStopWithErrorResult(new Exception())));
            Assert.Equal(BuildResultCode.Failure, result.OverallResult);

            BuildRequest request2 = CreateNewBuildRequest(2, new string[0]);
            BuildResult result2 = new BuildResult(request2);
            result2.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            result2.AddResultsForTarget("bar", TestUtilities.GetEmptyFailingTargetResult());
            Assert.Equal(BuildResultCode.Failure, result2.OverallResult);
        }

        [Fact]
        public void TestPacketType()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            Assert.Equal(NodePacketType.BuildResult, ((INodePacket)result).Type);
        }

        [Fact]
        public void TestAddAndRetrieve()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            result.AddResultsForTarget("bar", TestUtilities.GetEmptyFailingTargetResult());

            Assert.Equal(TargetResultCode.Success, result["foo"].ResultCode);
            Assert.Equal(TargetResultCode.Failure, result["bar"].ResultCode);
        }

        [Fact]
        public void TestIndexerBad1()
        {
            Assert.Throws<KeyNotFoundException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[0]);
                BuildResult result = new BuildResult(request);
                ITargetResult targetResult = result["foo"];
            }
           );
        }
        [Fact]
        public void TestIndexerBad2()
        {
            Assert.Throws<KeyNotFoundException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[0]);
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
                ITargetResult targetResult = result["bar"];
            }
           );
        }
        [Fact]
        public void TestAddResultsInvalid1()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[0]);
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget(null, TestUtilities.GetEmptySucceedingTargetResult());
            }
           );
        }
        [Fact]
        public void TestAddResultsInvalid2()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[0]);
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("foo", null);
            }
           );
        }
        [Fact]
        public void TestAddResultsInvalid3()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[0]);
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget(null, TestUtilities.GetEmptySucceedingTargetResult());
            }
           );
        }
        [Fact]
        public void TestMergeResults()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());

            BuildResult result2 = new BuildResult(request);
            result.AddResultsForTarget("bar", TestUtilities.GetEmptyFailingTargetResult());

            result.MergeResults(result2);
            Assert.Equal(TargetResultCode.Success, result["foo"].ResultCode);
            Assert.Equal(TargetResultCode.Failure, result["bar"].ResultCode);

            BuildResult result3 = new BuildResult(request);
            result.MergeResults(result3);

            BuildResult result4 = new BuildResult(request);
            result4.AddResultsForTarget("xor", TestUtilities.GetEmptySucceedingTargetResult());
            result.MergeResults(result4);
            Assert.Equal(TargetResultCode.Success, result["foo"].ResultCode);
            Assert.Equal(TargetResultCode.Failure, result["bar"].ResultCode);
            Assert.Equal(TargetResultCode.Success, result["xor"].ResultCode);
        }

        [Fact]
        public void TestMergeResultsBad1()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[0]);
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());

                result.MergeResults(null);
            }
           );
        }
        // See the implementation of BuildResult.MergeResults for an explanation of why this
        // test is disabled.
#if false
        [ExpectedException(typeof(InternalErrorException))]
        [TestMethod]
        public void TestMergeResultsBad2()
        {
            BuildResult result = new BuildResult(1);
            result["foo"] = new TargetResult(new BuildItem[0] { }, BuildResultCode.Success);

            BuildResult result2 = new BuildResult(1);
            result2["foo"] = new TargetResult(new BuildItem[0] { }, BuildResultCode.Success);

            result.MergeResults(result2);
        }
#endif

        [Fact]
        public void TestMergeResultsBad3()
        {
            Assert.Throws<InternalErrorException>(() =>
            {
                BuildRequest request = CreateNewBuildRequest(1, new string[0]);
                BuildResult result = new BuildResult(request);
                result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());

                BuildRequest request2 = CreateNewBuildRequest(2, new string[0]);
                BuildResult result2 = new BuildResult(request2);
                result2.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());

                result.MergeResults(result2);
            }
           );
        }
        [Fact]
        public void TestHasResultsForTarget()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());

            Assert.True(result.HasResultsForTarget("foo"));
            Assert.False(result.HasResultsForTarget("bar"));
        }

        [Fact]
        public void TestEnumerator()
        {
            BuildRequest request = CreateNewBuildRequest(1, new string[0]);
            BuildResult result = new BuildResult(request);
            int countFound = 0;
            foreach (KeyValuePair<string, TargetResult> resultPair in result.ResultsByTarget)
            {
                countFound++;
            }
            Assert.Equal(countFound, 0);

            result.AddResultsForTarget("foo", TestUtilities.GetEmptySucceedingTargetResult());
            bool foundFoo = false;
            countFound = 0;
            foreach (KeyValuePair<string, TargetResult> resultPair in result.ResultsByTarget)
            {
                if (resultPair.Key == "foo")
                {
                    foundFoo = true;
                }
                countFound++;
            }
            Assert.Equal(countFound, 1);
            Assert.True(foundFoo);

            result.AddResultsForTarget("bar", TestUtilities.GetEmptySucceedingTargetResult());
            foundFoo = false;
            bool foundBar = false;
            countFound = 0;
            foreach (KeyValuePair<string, TargetResult> resultPair in result.ResultsByTarget)
            {
                if (resultPair.Key == "foo")
                {
                    Assert.False(foundFoo);
                    foundFoo = true;
                }
                if (resultPair.Key == "bar")
                {
                    Assert.False(foundBar);
                    foundBar = true;
                }
                countFound++;
            }
            Assert.Equal(countFound, 2);
            Assert.True(foundFoo);
            Assert.True(foundBar);
        }


        [Fact]
        public void TestTranslation()
        {
            BuildRequest request = new BuildRequest(1, 1, 2, new string[] { "alpha", "omega" }, null, new BuildEventContext(1, 1, 2, 3, 4, 5), null);
            BuildResult result = new BuildResult(request, new BuildAbortedException());

            TaskItem fooTaskItem = new TaskItem("foo", "asdf.proj");
            fooTaskItem.SetMetadata("meta1", "metavalue1");
            fooTaskItem.SetMetadata("meta2", "metavalue2");

            result.InitialTargets = new List<string> { "a", "b" };
            result.DefaultTargets = new List<string> { "c", "d" };

            result.AddResultsForTarget("alpha", new TargetResult(new TaskItem[] { fooTaskItem }, TestUtilities.GetSuccessResult()));
            result.AddResultsForTarget("omega", new TargetResult(new TaskItem[] { }, TestUtilities.GetStopWithErrorResult(new ArgumentException("The argument was invalid"))));

            Assert.Equal(NodePacketType.BuildResult, (result as INodePacket).Type);
            ((INodePacketTranslatable)result).Translate(TranslationHelpers.GetWriteTranslator());
            INodePacket packet = BuildResult.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            BuildResult deserializedResult = packet as BuildResult;

            Assert.Equal(result.ConfigurationId, deserializedResult.ConfigurationId);
            Assert.True(TranslationHelpers.CompareCollections(result.DefaultTargets, deserializedResult.DefaultTargets, StringComparer.Ordinal));
            Assert.True(TranslationHelpers.CompareExceptions(result.Exception, deserializedResult.Exception));
            Assert.Equal(result.Exception.Message, deserializedResult.Exception.Message);
            Assert.Equal(result.GlobalRequestId, deserializedResult.GlobalRequestId);
            Assert.True(TranslationHelpers.CompareCollections(result.InitialTargets, deserializedResult.InitialTargets, StringComparer.Ordinal));
            Assert.Equal(result.NodeRequestId, deserializedResult.NodeRequestId);
            Assert.Equal(result["alpha"].ResultCode, deserializedResult["alpha"].ResultCode);
            Assert.True(TranslationHelpers.CompareExceptions(result["alpha"].Exception, deserializedResult["alpha"].Exception));
            Assert.True(TranslationHelpers.CompareCollections(result["alpha"].Items, deserializedResult["alpha"].Items, TaskItemComparer.Instance));
            Assert.Equal(result["omega"].ResultCode, deserializedResult["omega"].ResultCode);
            Assert.True(TranslationHelpers.CompareExceptions(result["omega"].Exception, deserializedResult["omega"].Exception));
            Assert.True(TranslationHelpers.CompareCollections(result["omega"].Items, deserializedResult["omega"].Items, TaskItemComparer.Instance));
        }

        private BuildRequest CreateNewBuildRequest(int configurationId, string[] targets)
        {
            return new BuildRequest(1 /* submissionId */, _nodeRequestId++, configurationId, targets, null, BuildEventContext.Invalid, null);
        }
    }
}
