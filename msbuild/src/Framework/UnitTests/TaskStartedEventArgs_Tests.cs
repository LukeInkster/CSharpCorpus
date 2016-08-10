﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Unit tests for TaskStartedEventArgs</summary>
//-----------------------------------------------------------------------

using System;

using Microsoft.Build.Framework;
using Xunit;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// Verify the functioning of the TaskStartedEventArgs class.
    /// </summary>
    public class TaskStartedEventArgs_Tests
    {
        /// <summary>
        /// Default event to use in tests.
        /// </summary>
        private TaskStartedEventArgs _baseTaskStartedEvent = new TaskStartedEventArgs("Message", "HelpKeyword", "ProjectFile", "TaskFile", "TaskName");

        /// <summary>
        /// Trivially exercise event args default ctors to boost Frameworks code coverage
        /// </summary>
        [Fact]
        public void EventArgsCtors()
        {
            TaskStartedEventArgs taskStartedEvent = new TaskStartedEventArgs2();
            taskStartedEvent = new TaskStartedEventArgs("Message", "HelpKeyword", "ProjectFile", "TaskFile", "TaskName");
            taskStartedEvent = new TaskStartedEventArgs("Message", "HelpKeyword", "ProjectFile", "TaskFile", "TaskName", DateTime.Now);
            taskStartedEvent = new TaskStartedEventArgs(null, null, null, null, null);
            taskStartedEvent = new TaskStartedEventArgs(null, null, null, null, null, DateTime.Now);
        }

        /// <summary>
        /// Create a derived class so that we can test the default constructor in order to increase code coverage and 
        /// verify this code path does not cause any exceptions.
        /// </summary>
        private class TaskStartedEventArgs2 : TaskStartedEventArgs
        {
            /// <summary>
            /// Default constructor
            /// </summary>
            public TaskStartedEventArgs2()
                : base()
            {
            }
        }
    }
}