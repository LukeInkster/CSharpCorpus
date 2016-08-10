﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Wraps an onerror element.</summary>
//-----------------------------------------------------------------------

using System.Diagnostics;
using Microsoft.Build.Shared;

using Microsoft.Build.Construction;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Wraps an onerror element
    /// </summary>
    /// <remarks>
    /// This is an immutable class
    /// </remarks>
    [DebuggerDisplay("ExecuteTargets={_executeTargets} Condition={_condition}")]
    public sealed class ProjectOnErrorInstance : ProjectTargetInstanceChild
    {
        /// <summary>
        /// Unevaluated executetargets value.
        /// </summary>
        private readonly string _executeTargets;

        /// <summary>
        /// Condition on the element.
        /// </summary>
        private readonly string _condition;

        /// <summary>
        /// Location of this element
        /// </summary>
        private readonly ElementLocation _location;

        /// <summary>
        /// Location of the condition, if any
        /// </summary>
        private readonly ElementLocation _conditionLocation;

        /// <summary>
        /// Location of the executeTargets attribute
        /// </summary>
        private readonly ElementLocation _executeTargetsLocation;

        /// <summary>
        /// Constructor called by Evaluator.
        /// All parameters are in the unevaluated state.
        /// </summary>
        internal ProjectOnErrorInstance
            (
            string executeTargets,
            string condition,
            ElementLocation location,
            ElementLocation executeTargetsLocation,
            ElementLocation conditionLocation
            )
        {
            ErrorUtilities.VerifyThrowInternalLength(executeTargets, "executeTargets");
            ErrorUtilities.VerifyThrowInternalNull(condition, "condition");
            ErrorUtilities.VerifyThrowInternalNull(location, "location");

            _executeTargets = executeTargets;
            _condition = condition;
            _location = location;
            _executeTargetsLocation = executeTargetsLocation;
            _conditionLocation = conditionLocation;
        }

        /// <summary>
        /// Unevaluated condition.
        /// May be empty string.
        /// </summary>
        public override string Condition
        {
            get { return _condition; }
        }

        /// <summary>
        /// Unevaluated ExecuteTargets value.
        /// May be empty string.
        /// </summary>
        public string ExecuteTargets
        {
            get { return _executeTargets; }
        }

        /// <summary>
        /// Location of the element
        /// </summary>
        public override ElementLocation Location
        {
            get { return _location; }
        }

        /// <summary>
        /// Location of the condition, if any
        /// </summary>
        public override ElementLocation ConditionLocation
        {
            get { return _conditionLocation; }
        }

        /// <summary>
        /// Location of the execute targets attribute, if any
        /// </summary>
        public ElementLocation ExecuteTargetsLocation
        {
            get { return _executeTargetsLocation; }
        }
    }
}
