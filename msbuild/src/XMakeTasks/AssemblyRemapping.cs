﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
// </copyright>
// <summary>Describes a remapping entry pair</summary>
//-----------------------------------------------------------------------

using System;
using Microsoft.Build.Shared;

namespace Microsoft.Build.Tasks
{
    /// <summary>
    /// Describes a remapping entry pair
    /// </summary>
    internal class AssemblyRemapping : IEquatable<AssemblyRemapping>
    {
        /// <summary>
        /// The assemblyName we mapped from
        /// </summary>
        private readonly AssemblyNameExtension _from;

        /// <summary>
        /// The assemblyName we mapped to
        /// </summary>
        private readonly AssemblyNameExtension _to;

        /// <summary>
        /// Constructor
        /// </summary>
        public AssemblyRemapping(AssemblyNameExtension from, AssemblyNameExtension to)
        {
            _from = from;
            _to = to;
        }

        /// <summary>
        /// The assemblyName we mapped from
        /// </summary>
        public AssemblyNameExtension From
        {
            get
            {
                return _from;
            }
        }

        /// <summary>
        /// The assemblyName we mapped to
        /// </summary>
        public AssemblyNameExtension To
        {
            get
            {
                return _to;
            }
        }

        /// <summary>
        /// Compare two Assembly remapping objects
        /// </summary>
        public override bool Equals(object obj)
        {
            AssemblyNameExtension name = obj as AssemblyNameExtension;
            if (obj == null)
            {
                return false;
            }

            return Equals(name);
        }

        /// <summary>
        /// Get the hash code
        /// </summary>
        public override int GetHashCode()
        {
            return _from.GetHashCode();
        }

        /// <summary>
        /// We only compare the from because in terms of what is in the redist list unique from's are expected
        /// </summary>
        public bool Equals(AssemblyRemapping other)
        {
            return _from.Equals(other._from);
        }
    }
}