﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Tests for the TargetResult class.</summary>
//-----------------------------------------------------------------------

using System;
using System.Xml;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Build.BackEnd;
using Microsoft.Build.Unittest;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using TaskItem = Microsoft.Build.Execution.ProjectItemInstance.TaskItem;
using Xunit;

namespace Microsoft.Build.UnitTests.BackEnd
{
    /// <summary>
    /// Tests for the target result test.
    /// </summary>
    public class TargetResult_Tests
    {
        /// <summary>
        /// Tests a constructor with no items.
        /// </summary>
        [Fact]
        public void TestConstructorNoItems()
        {
            TargetResult result = new TargetResult(new TaskItem[] { }, TestUtilities.GetStopWithErrorResult());
            Assert.Equal(0, result.Items.Length);
            Assert.Null(result.Exception);
            Assert.Equal(TargetResultCode.Failure, result.ResultCode);
        }

        /// <summary>
        /// Tests a constructor with items.
        /// </summary>
        [Fact]
        public void TestConstructorWithItems()
        {
            TaskItem item = new TaskItem("foo", "bar.proj");
            TargetResult result = new TargetResult(new TaskItem[] { item }, TestUtilities.GetStopWithErrorResult());
            Assert.Equal(1, result.Items.Length);
            Assert.Equal(item.ItemSpec, result.Items[0].ItemSpec);
            Assert.Equal(TargetResultCode.Failure, result.ResultCode);
        }

        /// <summary>
        /// Tests a constructor with a null item array passed.
        /// </summary>
        [Fact]
        public void TestConstructorNullItems()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                TargetResult result = new TargetResult(null, TestUtilities.GetStopWithErrorResult());
            }
           );
        }
        /// <summary>
        /// Tests a constructor with an exception passed.
        /// </summary>
        [Fact]
        public void TestConstructorWithException()
        {
            TaskItem item = new TaskItem("foo", "bar.proj");
            TargetResult result = new TargetResult(new TaskItem[] { item }, TestUtilities.GetStopWithErrorResult(new ArgumentException()));
            Assert.Equal(1, result.Items.Length);
            Assert.NotNull(result.Exception);
            Assert.Equal(typeof(ArgumentException), result.Exception.GetType());
            Assert.Equal(TargetResultCode.Failure, result.ResultCode);
        }

        /// <summary>
        /// Tests a constructor with a null exception passed.
        /// </summary>
        [Fact]
        public void TestConstructorWithExceptionNull()
        {
            TaskItem item = new TaskItem("foo", "bar.proj");
            TargetResult result = new TargetResult(new TaskItem[] { item }, TestUtilities.GetStopWithErrorResult());
            Assert.Equal(1, result.Items.Length);
            Assert.Null(result.Exception);
            Assert.Equal(TargetResultCode.Failure, result.ResultCode);
        }

        /// <summary>
        /// Tests serialization with no exception in the result.
        /// </summary>
        [Fact]
        public void TestTranslationNoException()
        {
            TaskItem item = new TaskItem("foo", "bar.proj");
            item.SetMetadata("a", "b");

            TargetResult result = new TargetResult(new TaskItem[] { item }, TestUtilities.GetStopWithErrorResult());

            ((INodePacketTranslatable)result).Translate(TranslationHelpers.GetWriteTranslator());
            TargetResult deserializedResult = TargetResult.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(result.ResultCode, deserializedResult.ResultCode);
            Assert.True(TranslationHelpers.CompareCollections(result.Items, deserializedResult.Items, TaskItemComparer.Instance));
            Assert.True(TranslationHelpers.CompareExceptions(result.Exception, deserializedResult.Exception));
        }

        /// <summary>
        /// Tests serialization with an exception in the result.
        /// </summary>
        [Fact]
        public void TestTranslationWithException()
        {
            TaskItem item = new TaskItem("foo", "bar.proj");
            item.SetMetadata("a", "b");

            TargetResult result = new TargetResult(new TaskItem[] { item }, TestUtilities.GetStopWithErrorResult(new BuildAbortedException()));

            ((INodePacketTranslatable)result).Translate(TranslationHelpers.GetWriteTranslator());
            TargetResult deserializedResult = TargetResult.FactoryForDeserialization(TranslationHelpers.GetReadTranslator());

            Assert.Equal(result.ResultCode, deserializedResult.ResultCode);
            Assert.True(TranslationHelpers.CompareCollections(result.Items, deserializedResult.Items, TaskItemComparer.Instance));
            Assert.True(TranslationHelpers.CompareExceptions(result.Exception, deserializedResult.Exception));
        }

        /// <summary>
        /// Test GetCacheDirectory is resilient to paths with strings that would normally make string.format to throw a FormatException
        /// </summary>
        [Fact]
        public void TestGetCacheDirectory()
        {
            string oldTmp = Environment.GetEnvironmentVariable("TMP");

            try
            {
                Environment.SetEnvironmentVariable("TMP", "C:\\}");
                string path1 = TargetResult.GetCacheDirectory(2, "Blah");

                Environment.SetEnvironmentVariable("TMP", "C:\\{");
                string path2 = TargetResult.GetCacheDirectory(2, "Blah");
            }
            finally
            {
                Environment.SetEnvironmentVariable("TMP", oldTmp);
            }
        }
    }
}
