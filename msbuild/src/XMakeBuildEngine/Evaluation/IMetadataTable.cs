﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Interface for a table of metadata.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Evaluation
{
    /// <summary>
    /// Table of metadata useable to expand expressions
    /// </summary>
    internal interface IMetadataTable
    {
        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name specified.
        /// If no value is available, returns empty string.
        /// </summary>
        string GetEscapedValue(string name);

        /// <summary>
        /// Retrieves any value we have in our metadata table for the metadata name and item type specified.
        /// If item type is null, it is ignored.
        /// If no value is available, returns empty string.
        /// </summary>
        string GetEscapedValue(string itemType, string name);

        /// <summary>
        /// Returns the value if it exists, null otherwise.
        /// If item type is null, it is ignored.
        /// </summary>
        string GetEscapedValueIfPresent(string itemType, string name);
    }
}
