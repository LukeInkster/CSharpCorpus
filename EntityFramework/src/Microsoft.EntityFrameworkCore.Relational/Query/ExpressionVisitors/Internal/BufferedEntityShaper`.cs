// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Remotion.Linq.Clauses;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class BufferedEntityShaper<TEntity> : EntityShaper, IShaper<TEntity>
        where TEntity : class
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public BufferedEntityShaper(
            [NotNull] IQuerySource querySource,
            [NotNull] string entityType,
            bool trackingQuery,
            [NotNull] IKey key,
            [NotNull] Func<ValueBuffer, object> materializer)
            : base(querySource, entityType, trackingQuery, key, materializer)
        {
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public override Type Type => typeof(TEntity);

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual TEntity Shape(QueryContext queryContext, ValueBuffer valueBuffer)
        {
            Debug.Assert(queryContext != null);

            var entity
                = (TEntity)queryContext.QueryBuffer
                    .GetEntity(
                        Key,
                        new EntityLoadInfo(valueBuffer, Materializer),
                        queryStateManager: IsTrackingQuery,
                        throwOnNullKey: !AllowNullResult);

            return entity;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public override IShaper<TDerived> Cast<TDerived>()
            => new BufferedOffsetEntityShaper<TDerived>(
                QuerySource,
                EntityType,
                IsTrackingQuery,
                Key,
                Materializer);

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public override EntityShaper WithOffset(int offset)
            => new BufferedOffsetEntityShaper<TEntity>(
                QuerySource,
                EntityType,
                IsTrackingQuery,
                Key,
                Materializer)
                .SetOffset(offset);

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public override string ToString() => "BufferedEntityShaper<" + typeof(TEntity).Name + ">";
    }
}
