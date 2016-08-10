// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Reflection;
using Remotion.Linq.Clauses;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class QueryFlattenerFactory : IQueryFlattenerFactory
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual QueryFlattener Create(
            IQuerySource querySource,
            RelationalQueryCompilationContext relationalQueryCompilationContext,
            MethodInfo operatorToFlatten,
            int readerOffset)
            => new QueryFlattener(
                querySource,
                relationalQueryCompilationContext,
                operatorToFlatten,
                readerOffset);
    }
}
