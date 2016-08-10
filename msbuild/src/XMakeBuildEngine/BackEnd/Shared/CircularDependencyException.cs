﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>The exception which gets thrown if a circular dependency is indicated by the BuildManager.</summary>
//-----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;
using System.Runtime.Serialization;
using Microsoft.Build.Shared;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// An exception representing the case where a BuildRequest has caused a circular project dependency.  This is used to
    /// terminate the request builder which initiated the failure path.
    /// </summary>
    /// <remarks>
    /// If you add fields to this class, add a custom serialization constructor and override GetObjectData().
    /// </remarks>
    [Serializable]
    internal class CircularDependencyException : Exception
    {
        /// <summary>
        /// Constructs a standard BuildAbortedException.
        /// </summary>
        internal CircularDependencyException()
            : base()
        {
        }

        /// <summary>
        /// Constructor for deserialization.
        /// </summary>
        protected CircularDependencyException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
