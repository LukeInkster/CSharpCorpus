﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Resources;
using System.Reflection;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.Build.Tasks
{
    /// <remarks>
    /// Represents a cache of inputs to a compilation-style task.
    /// </remarks>
    [Serializable()]
    internal class Dependencies
    {
        /// <summary>
        /// Hashtable of other dependency files.
        /// Key is filename and value is DependencyFile.
        /// </summary>
        private Hashtable dependencies = new Hashtable();

        /// <summary>
        /// Look up a dependency file. Return null if its not there.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        internal DependencyFile GetDependencyFile(string filename)
        {
            return (DependencyFile)dependencies[filename];
        }


        /// <summary>
        /// Add a new dependency file.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        internal void AddDependencyFile(string filename, DependencyFile file)
        {
            dependencies[filename] = file;
        }

        /// <summary>
        /// Remove new dependency file.
        /// </summary>
        /// <param name="filename"></param>
        /// <returns></returns>
        internal void RemoveDependencyFile(string filename)
        {
            dependencies.Remove(filename);
        }

        /// <summary>
        /// Remove all entries from the dependency table.
        /// </summary>
        internal void Clear()
        {
            dependencies.Clear();
        }
    }
}