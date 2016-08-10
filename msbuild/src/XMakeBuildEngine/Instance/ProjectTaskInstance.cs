﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>Wraps a task element.</summary>
//-----------------------------------------------------------------------

using System.Collections.Generic;
using Microsoft.Build.Construction;
using Microsoft.Build.Collections;
using Microsoft.Build.Shared;
using System.Diagnostics;
using System;

namespace Microsoft.Build.Execution
{
    /// <summary>
    /// Wraps a task element
    /// </summary>
    /// <remarks>
    /// This is an immutable class
    /// </remarks>
    [DebuggerDisplay("Name={_name} Condition={_condition} ContinueOnError={_continueOnError} MSBuildRuntime={MSBuildRuntime} MSBuildArchitecture={MSBuildArchitecture} #Parameters={_parameters.Count} #Outputs={_outputs.Count}")]
    public sealed class ProjectTaskInstance : ProjectTargetInstanceChild
    {
        /// <summary>
        /// Name of the task, possibly qualified, as it appears in the project
        /// </summary>
        private readonly string _name;

        /// <summary>
        /// Condition on the task, if any
        /// May be empty string
        /// </summary>
        private readonly string _condition;

        /// <summary>
        /// Continue on error on the task, if any
        /// May be empty string
        /// </summary>
        private readonly string _continueOnError;

        /// <summary>
        /// Runtime on the task, if any
        /// May be empty string
        /// </summary>
        private readonly string _msbuildRuntime;

        /// <summary>
        /// Architecture on the task, if any
        /// May be empty string
        /// </summary>
        private readonly string _msbuildArchitecture;

        /// <summary>
        /// Unordered set of task parameter names and unevaluated values.
        /// This is a dead, read-only collection.
        /// </summary>
        private readonly CopyOnWriteDictionary<string, Tuple<string, ElementLocation>> _parameters;

        /// <summary>
        /// Output properties and items below this task. This is an ordered collection
        /// as one may depend on another.
        /// This is a dead, read-only collection.
        /// </summary>
        private readonly IList<ProjectTaskInstanceChild> _outputs;

        /// <summary>
        /// Location of this element
        /// </summary>
        private readonly ElementLocation _location;

        /// <summary>
        /// Location of the condition, if any
        /// </summary>
        private readonly ElementLocation _conditionLocation;

        /// <summary>
        /// Location of the continueOnError attribute, if any
        /// </summary>
        private readonly ElementLocation _continueOnErrorLocation;

        /// <summary>
        /// Location of the MSBuildRuntime attribute, if any
        /// </summary>
        private readonly ElementLocation _msbuildRuntimeLocation;

        /// <summary>
        /// Location of the MSBuildArchitecture attribute, if any
        /// </summary>
        private readonly ElementLocation _msbuildArchitectureLocation;

        /// <summary>
        /// Constructor called by Evaluator.
        /// All parameters are in the unevaluated state.
        /// Locations other than the main location may be null.
        /// </summary>
        internal ProjectTaskInstance
            (
            ProjectTaskElement element,
            IList<ProjectTaskInstanceChild> outputs
            )
        {
            ErrorUtilities.VerifyThrowInternalNull(element, "element");
            ErrorUtilities.VerifyThrowInternalNull(outputs, "outputs");

            // These are all immutable
            _name = element.Name;
            _condition = element.Condition;
            _continueOnError = element.ContinueOnError;
            _msbuildArchitecture = element.MSBuildArchitecture;
            _msbuildRuntime = element.MSBuildRuntime;
            _location = element.Location;
            _conditionLocation = element.ConditionLocation;
            _continueOnErrorLocation = element.ContinueOnErrorLocation;
            _msbuildRuntimeLocation = element.MSBuildRuntimeLocation;
            _msbuildArchitectureLocation = element.MSBuildArchitectureLocation;
            _parameters = element.ParametersForEvaluation;
            _outputs = new List<ProjectTaskInstanceChild>(outputs);
        }

        /// <summary>
        /// Creates a new task instance directly.  Used for generating instances on-the-fly.
        /// </summary>
        /// <param name="name">The task name.</param>
        /// <param name="taskLocation">The location for this task.</param>
        /// <param name="condition">The unevaluated condition.</param>
        /// <param name="continueOnError">The unevaluated continue on error.</param>
        internal ProjectTaskInstance
            (
            string name,
            ElementLocation taskLocation,
            string condition,
            string continueOnError,
            string msbuildRuntime,
            string msbuildArchitecture
            )
        {
            ErrorUtilities.VerifyThrowArgumentLength("name", "name");
            ErrorUtilities.VerifyThrowArgumentNull(condition, "condition");
            ErrorUtilities.VerifyThrowArgumentNull(continueOnError, "continueOnError");
            _name = name;
            _condition = condition;
            _continueOnError = continueOnError;
            _msbuildRuntime = msbuildRuntime;
            _msbuildArchitecture = msbuildArchitecture;
            _location = taskLocation;
            _conditionLocation = (condition == String.Empty) ? null : ElementLocation.EmptyLocation;
            _continueOnErrorLocation = (continueOnError == String.Empty) ? null : ElementLocation.EmptyLocation;
            _msbuildArchitectureLocation = (msbuildArchitecture == String.Empty) ? null : ElementLocation.EmptyLocation;
            _msbuildRuntimeLocation = (msbuildRuntime == String.Empty) ? null : ElementLocation.EmptyLocation;
            _parameters = new CopyOnWriteDictionary<string, Tuple<string, ElementLocation>>(8, StringComparer.OrdinalIgnoreCase);
            _outputs = new List<ProjectTaskInstanceChild>();
        }

        /// <summary>
        /// Name of the task, possibly qualified, as it appears in the project
        /// </summary>
        public string Name
        {
            get { return _name; }
        }

        /// <summary>
        /// Unevaluated condition on the task
        /// May be empty string.
        /// </summary>
        public override string Condition
        {
            get { return _condition; }
        }

        /// <summary>
        /// Unevaluated ContinueOnError on the task.
        /// May be empty string.
        /// </summary>
        public string ContinueOnError
        {
            get { return _continueOnError; }
        }

        /// <summary>
        /// Unevaluated MSBuildRuntime on the task.
        /// May be empty string.
        /// </summary>
        public string MSBuildRuntime
        {
            get { return _msbuildRuntime; }
        }

        /// <summary>
        /// Unevaluated MSBuildArchitecture on the task.
        /// May be empty string.
        /// </summary>
        public string MSBuildArchitecture
        {
            get { return _msbuildArchitecture; }
        }

        /// <summary>
        /// Read-only dead unordered set of task parameter names and unevaluated values.
        /// Condition and ContinueOnError, which have their own properties, are not included in this collection.
        /// </summary>
        public IDictionary<string, string> Parameters
        {
            get
            {
                Dictionary<string, string> filteredParameters = new Dictionary<string, string>(_parameters.Count, StringComparer.OrdinalIgnoreCase);
                foreach (KeyValuePair<string, Tuple<string, ElementLocation>> parameter in _parameters)
                {
                    filteredParameters[parameter.Key] = parameter.Value.Item1;
                }

                return filteredParameters;
            }
        }

        /// <summary>
        /// Ordered set of output property and item objects.
        /// This is a read-only dead collection.
        /// </summary>
        public IList<ProjectTaskInstanceChild> Outputs
        {
            get { return _outputs; }
        }

        /// <summary>
        /// Location of the ContinueOnError attribute, if any
        /// </summary>
        public ElementLocation ContinueOnErrorLocation
        {
            get { return _continueOnErrorLocation; }
        }

        /// <summary>
        /// Location of the MSBuildRuntime attribute, if any
        /// </summary>
        public ElementLocation MSBuildRuntimeLocation
        {
            get { return _msbuildRuntimeLocation; }
        }

        /// <summary>
        /// Location of the MSBuildArchitecture attribute, if any
        /// </summary>
        public ElementLocation MSBuildArchitectureLocation
        {
            get { return _msbuildArchitectureLocation; }
        }

        /// <summary>
        /// Location of the original element
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
        /// Retrieves the parameters dictionary as used during the build.
        /// </summary>
        internal IDictionary<string, Tuple<string, ElementLocation>> ParametersForBuild
        {
            get { return _parameters; }
        }

        /// <summary>
        /// Returns the value of a named parameter, or null if there is no such parameter.
        /// </summary>
        /// <param name="parameterName">The name of the parameter to retrieve.</param>
        /// <returns>The parameter value, or null if it does not exist.</returns>
        internal string GetParameter(string parameterName)
        {
            Tuple<string, ElementLocation> parameterValue = null;
            if (_parameters.TryGetValue(parameterName, out parameterValue))
            {
                return parameterValue.Item1;
            }

            return null;
        }

        /// <summary>
        /// Sets the unevaluated value for the specified parameter.
        /// </summary>
        /// <param name="parameterName">The name of the parameter to set.</param>
        /// <param name="unevaluatedValue">The unevaluated value for the parameter.</param>
        internal void SetParameter(string parameterName, string unevaluatedValue)
        {
            _parameters[parameterName] = new Tuple<string, ElementLocation>(unevaluatedValue, ElementLocation.EmptyLocation);
        }

        /// <summary>
        /// Adds an output item to the task.
        /// </summary>
        /// <param name="taskOutputParameterName">The name of the parameter on the task which produces the output.</param>
        /// <param name="itemName">The item which will receive the output.</param>
        /// <param name="condition">The condition.</param>
        internal void AddOutputItem(string taskOutputParameterName, string itemName, string condition)
        {
            ErrorUtilities.VerifyThrowArgumentLength(taskOutputParameterName, "taskOutputParameterName");
            ErrorUtilities.VerifyThrowArgumentLength(itemName, "itemName");
            _outputs.Add(new ProjectTaskOutputItemInstance(itemName, taskOutputParameterName, condition ?? String.Empty, ElementLocation.EmptyLocation, ElementLocation.EmptyLocation, ElementLocation.EmptyLocation, condition == null ? null : ElementLocation.EmptyLocation));
        }

        /// <summary>
        /// Adds an output property to the task.
        /// </summary>
        /// <param name="taskOutputParameterName">The name of the parameter on the task which produces the output.</param>
        /// <param name="propertyName">The property which will receive the output.</param>
        /// <param name="condition">The condition.</param>
        internal void AddOutputProperty(string taskOutputParameterName, string propertyName, string condition)
        {
            ErrorUtilities.VerifyThrowArgumentLength(taskOutputParameterName, "taskOutputParameterName");
            ErrorUtilities.VerifyThrowArgumentLength(propertyName, "propertyName");
            _outputs.Add(new ProjectTaskOutputPropertyInstance(propertyName, taskOutputParameterName, condition ?? String.Empty, ElementLocation.EmptyLocation, ElementLocation.EmptyLocation, ElementLocation.EmptyLocation, condition == null ? null : ElementLocation.EmptyLocation));
        }
    }
}
