﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>A dummy element location</summary>
//-----------------------------------------------------------------------

using Microsoft.Build.Construction;
using Microsoft.Build.Framework;
using Microsoft.Build.Shared;
using Microsoft.Build.BackEnd;
using System;

namespace Microsoft.Build.UnitTests
{
    /// <summary>
    /// A dummy element location.
    /// </summary>
    internal class MockElementLocation : ElementLocation
    {
        /// <summary>
        /// Single instance
        /// </summary>
        private static MockElementLocation s_instance = new MockElementLocation();

        /// <summary>
        /// Private constructor
        /// </summary>
        private MockElementLocation()
        {
        }

        /// <summary>
        /// File of element, eg a targets file
        /// </summary>
        public override string File
        {
            get { return "mock.targets"; }
        }

        /// <summary>
        /// Line number
        /// </summary>
        public override int Line
        {
            get { return 0; }
        }

        /// <summary>
        /// Column number
        /// </summary>
        public override int Column
        {
            get { return 1; }
        }

        /// <summary>
        /// Get single instance
        /// </summary>
        internal static MockElementLocation Instance
        {
            get { return s_instance; }
        }
    }
}