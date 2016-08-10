﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <remarks>
    /// Stores timestamps of COM components processed in the last run. The problem here is that installing/uninstalling
    /// COM components does not update their timestamps with the current time (for a good reason). So if you revert to
    /// an earlier revision of a COM component, its timestamp can go back in time and we still need to regenerate its
    /// wrapper. So in ResolveComReference we compare the stored timestamp with the current component timestamp, and if 
    /// they are different, we regenerate the wrapper.
    /// </remarks>
    [Serializable()]
    internal sealed class ResolveComReferenceCache : StateFileBase
    {
        /// <summary>
        /// Component timestamps. 
        /// Key: Component path on disk
        /// Value: DateTime struct
        /// </summary>
        private Hashtable componentTimestamps = null;
        private string tlbImpLocation = null;
        private string axImpLocation = null;

        /// <summary>
        /// indicates whether the cache contents have changed since it's been created
        /// </summary>
        internal bool Dirty
        {
            get { return dirty; }
        }


        [NonSerialized]
        private bool dirty;

        /// <summary>
        /// Construct.
        /// </summary>
        internal ResolveComReferenceCache(string tlbImpPath, string axImpPath)
        {
            ErrorUtilities.VerifyThrowArgumentNull(tlbImpPath, "tlbImpPath");
            ErrorUtilities.VerifyThrowArgumentNull(axImpPath, "axImpPath");

            tlbImpLocation = tlbImpPath;
            axImpLocation = axImpPath;
            componentTimestamps = new Hashtable();
        }

        /// <summary>
        /// Compares the tlbimp and aximp paths to what the paths were when the cache was created
        /// If these are different return false.
        /// </summary>
        /// <returns>True if both paths match what is in the cache, false otherwise</returns>
        internal bool ToolPathsMatchCachePaths(string tlbImpPath, string axImpPath)
        {
            return (String.Equals(tlbImpLocation, tlbImpPath, StringComparison.OrdinalIgnoreCase)) && (String.Equals(axImpLocation, axImpPath, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets or sets the timestamp associated with the specified component file
        /// </summary>
        /// <param name="componentPath"></param>
        /// <returns></returns>
        internal DateTime this[string componentPath]
        {
            get
            {
                if (componentTimestamps.ContainsKey(componentPath))
                {
                    return (DateTime)componentTimestamps[componentPath];
                }
                else
                {
                    // If the entry is not present in the cache, return the current time. Since no component should be timestamped
                    // with the current time, this will effectively always regenerate the wrapper.
                    return DateTime.Now;
                }
            }
            set
            {
                // only set the value and dirty the cache if the timestamp doesn't exist yet or is different than the current one
                if (DateTime.Compare(this[componentPath], value) != 0)
                {
                    componentTimestamps[componentPath] = value;
                    dirty = true;
                }
            }
        }
    }
}
