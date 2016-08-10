﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//-----------------------------------------------------------------------
// </copyright>
// <summary>TaskParameterTypeVerifier verifies the correct type for both input and output parameters.</summary>
//-----------------------------------------------------------------------

using System;
using Microsoft.Build.Framework;

namespace Microsoft.Build.BackEnd
{
    /// <summary>
    /// Provide a class which can verify the correct type for both input and output parameters.
    /// </summary>
    internal static class TaskParameterTypeVerifier
    {
        /// <summary>
        /// Is the parameter type a valid scalar input value
        /// </summary>
        internal static bool IsValidScalarInputParameter(Type parameterType)
        {
            bool result = (parameterType.IsValueType || parameterType == typeof(string) || parameterType == typeof(ITaskItem));
            return result;
        }

        /// <summary>
        /// Is the passed in parameterType a valid vector input parameter
        /// </summary>
        internal static bool IsValidVectorInputParameter(Type parameterType)
        {
            bool result = (parameterType.IsArray && parameterType.GetElementType().IsValueType) ||
                        parameterType == typeof(string[]) ||
                        parameterType == typeof(ITaskItem[]);
            return result;
        }

        /// <summary>
        /// Is the passed in value type assignable to an ITask or Itask[] object
        /// </summary>
        internal static bool IsAssignableToITask(Type parameterType)
        {
            bool result = typeof(ITaskItem[]).IsAssignableFrom(parameterType) ||   /* ITaskItem array or derived type, or */
                          typeof(ITaskItem).IsAssignableFrom(parameterType);        /* ITaskItem or derived type */
            return result;
        }

        /// <summary>
        /// Is the passed parameter a valid value type output parameter
        /// </summary>
        internal static bool IsValueTypeOutputParameter(Type parameterType)
        {
            bool result = (parameterType.IsArray && parameterType.GetElementType().IsValueType) || /* array of value types, or */
                          parameterType == typeof(string[]) ||                                     /* string array, or */
                          parameterType.IsValueType ||                                             /* value type, or */
                          parameterType == typeof(string);                                         /* string */
            return result;
        }

        /// <summary>
        /// Is the parameter type a valid scalar or value type input parameter
        /// </summary>
        internal static bool IsValidInputParameter(Type parameterType)
        {
            return IsValidScalarInputParameter(parameterType) || IsValidVectorInputParameter(parameterType);
        }

        /// <summary>
        /// Is the parameter type a valid scalar or value type output parameter
        /// </summary>
        internal static bool IsValidOutputParameter(Type parameterType)
        {
            return IsValueTypeOutputParameter(parameterType) || IsAssignableToITask(parameterType);
        }
    }
}