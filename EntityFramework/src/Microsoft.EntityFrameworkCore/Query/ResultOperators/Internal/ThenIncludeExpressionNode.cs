// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Internal;
using Remotion.Linq;
using Remotion.Linq.Clauses;
using Remotion.Linq.Parsing.Structure.IntermediateModel;

namespace Microsoft.EntityFrameworkCore.Query.ResultOperators.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class ThenIncludeExpressionNode : ResultOperatorExpressionNodeBase
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static readonly IReadOnlyCollection<MethodInfo> SupportedMethods = new[]
        {
            EntityFrameworkQueryableExtensions.ThenIncludeAfterCollectionMethodInfo,
            EntityFrameworkQueryableExtensions.ThenIncludeAfterReferenceMethodInfo
        };

        private readonly LambdaExpression _navigationPropertyPathLambda;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public ThenIncludeExpressionNode(
            MethodCallExpressionParseInfo parseInfo,
            [NotNull] LambdaExpression navigationPropertyPathLambda)
            : base(parseInfo, null, null)
        {
            _navigationPropertyPathLambda = navigationPropertyPathLambda;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override void ApplyNodeSpecificSemantics(
            QueryModel queryModel, ClauseGenerationContext clauseGenerationContext)
        {
            var includeResultOperator
                = (IncludeResultOperator)clauseGenerationContext.GetContextInfo(Source);

            includeResultOperator
                .AppendToNavigationPath(_navigationPropertyPathLambda.GetComplexPropertyAccess());

            clauseGenerationContext.AddContextInfo(this, includeResultOperator);
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override ResultOperatorBase CreateResultOperator(ClauseGenerationContext clauseGenerationContext)
            => null;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public override Expression Resolve(
            ParameterExpression inputParameter,
            Expression expressionToBeResolved,
            ClauseGenerationContext clauseGenerationContext)
            => Source.Resolve(
                inputParameter,
                expressionToBeResolved,
                clauseGenerationContext);
    }
}
