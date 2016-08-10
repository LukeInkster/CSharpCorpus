// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Query.Expressions;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionTranslators
{
    /// <summary>
    ///     A base LINQ expression translator for CLR <see cref="MethodCallExpression" /> expressions that
    ///     are instance methods and do not take arguments.
    /// </summary>
    public abstract class ParameterlessInstanceMethodCallTranslator : IMethodCallTranslator
    {
        private readonly Type _declaringType;
        private readonly string _clrMethodName;
        private readonly string _sqlFunctionName;

        /// <summary>
        ///     Specialised constructor for use only by derived class.
        /// </summary>
        /// <param name="declaringType"> The declaring type of the method. </param>
        /// <param name="clrMethodName"> Name of the method. </param>
        /// <param name="sqlFunctionName"> The name of the target SQL function. </param>
        protected ParameterlessInstanceMethodCallTranslator(
            [NotNull] Type declaringType, [NotNull] string clrMethodName, [NotNull] string sqlFunctionName)
        {
            _declaringType = declaringType;
            _clrMethodName = clrMethodName;
            _sqlFunctionName = sqlFunctionName;
        }

        /// <summary>
        ///     Translates the given method call expression.
        /// </summary>
        /// <param name="methodCallExpression"> The method call expression. </param>
        /// <returns>
        ///     A SQL expression representing the translated MethodCallExpression.
        /// </returns>
        public virtual Expression Translate(MethodCallExpression methodCallExpression)
        {
            var methodInfo = _declaringType.GetTypeInfo()
                .GetDeclaredMethods(_clrMethodName).SingleOrDefault(m => !m.GetParameters().Any());

            if (methodInfo == methodCallExpression.Method)
            {
                var sqlArguments = new[] { methodCallExpression.Object }.Concat(methodCallExpression.Arguments);
                return new SqlFunctionExpression(_sqlFunctionName, methodCallExpression.Type, sqlArguments);
            }

            return null;
        }
    }
}
