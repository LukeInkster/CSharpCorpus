// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Query.Internal;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.Query
{
    /// <summary>
    ///     <para>
    ///         Creates keys that uniquely identifies a query. This is used to store and lookup
    ///         compiled versions of a query in a cache.
    ///     </para>
    ///     <para>
    ///         This type is typically used by database providers (and other extensions). It is generally
    ///         not used in application code.
    ///     </para>
    /// </summary>
    public class CompiledQueryCacheKeyGenerator : ICompiledQueryCacheKeyGenerator
    {
        private readonly IModel _model;
        private readonly DbContext _context;

        /// <summary>
        ///     Initializes a new instance of the <see cref="CompiledQueryCacheKeyGenerator"/> class.
        /// </summary>
        /// <param name="model"> The model that queries will be written against. </param>
        /// <param name="currentContext"> The context that queries will be executed for. </param>
        public CompiledQueryCacheKeyGenerator([NotNull] IModel model, [NotNull] ICurrentDbContext currentContext)
        {
            Check.NotNull(model, nameof(model));
            Check.NotNull(currentContext, nameof(currentContext));

            _model = model;
            _context = currentContext.Context;
        }

        /// <summary>
        ///     Generates the cache key for the given query.
        /// </summary>
        /// <param name="query"> The query to get the cache key for. </param>
        /// <param name="async"> A value indicating whether the query will be executed asynchronously. </param>
        /// <returns> The cache key. </returns>
        public virtual object GenerateCacheKey(Expression query, bool async)
            => GenerateCacheKeyCore(query, async);

        /// <summary>
        ///     Generates the cache key for the given query.
        /// </summary>
        /// <param name="query"> The query to get the cache key for. </param>
        /// <param name="async"> A value indicating whether the query will be executed asynchronously. </param>
        /// <returns> The cache key. </returns>
        protected CompiledQueryCacheKey GenerateCacheKeyCore([NotNull] Expression query, bool async)
            => new CompiledQueryCacheKey(
                Check.NotNull(query, nameof(query)),
                _model,
                _context.ChangeTracker.QueryTrackingBehavior,
                async);

        /// <summary>
        ///     <para>
        ///         A key that uniquely identifies a query. This is used to store and lookup
        ///         compiled versions of a query in a cache. 
        ///     </para>
        ///     <para>
        ///         This type is typically used by database providers (and other extensions). It is generally
        ///         not used in application code.
        ///     </para>
        /// </summary>
        protected struct CompiledQueryCacheKey
        {
            private static readonly ExpressionEqualityComparer _expressionEqualityComparer
                = new ExpressionEqualityComparer();

            private readonly Expression _query;
            private readonly IModel _model;
            private readonly QueryTrackingBehavior _queryTrackingBehavior;
            private readonly bool _async;

            /// <summary>
            ///     Initializes a new instance of the <see cref="CompiledQueryCacheKey"/> class.
            /// </summary>
            /// <param name="query"> The query to generate the key for. </param>
            /// <param name="model"> The model that queries is written against. </param>
            /// <param name="queryTrackingBehavior"> The tracking behavior for results of the query. </param>
            /// <param name="async"> A value indicating whether the query will be executed asynchronously. </param>
            public CompiledQueryCacheKey(
                [NotNull] Expression query,
                [NotNull] IModel model,
                QueryTrackingBehavior queryTrackingBehavior,
                bool async)
            {
                _query = query;
                _model = model;
                _queryTrackingBehavior = queryTrackingBehavior;
                _async = async;
            }

            /// <summary>
            ///     Determines if this key is equivalent to a given object (i.e. if they are keys for the same query).
            /// </summary>
            /// <param name="obj">
            ///     The object to compare this key to.
            /// </param>
            /// <returns>
            ///     True if the object is a <see cref="CompiledQueryCacheKey"/> and is for the same query, otherwise false.
            /// </returns>
            public override bool Equals(object obj)
            {
                var other = (CompiledQueryCacheKey)obj;

                return ReferenceEquals(_model, other._model)
                       && _queryTrackingBehavior == other._queryTrackingBehavior
                       && _async == other._async
                       && _expressionEqualityComparer.Equals(_query, other._query);
            }

            /// <summary>
            ///     Gets the hash code for the key.
            /// </summary>
            /// <returns>
            ///     The hash code for the key.
            /// </returns>
            public override int GetHashCode()
            {
                unchecked
                {
                    var hashCode = _expressionEqualityComparer.GetHashCode(_query);
                    hashCode = (hashCode * 397) ^ _model.GetHashCode();
                    hashCode = (hashCode * 397) ^ (int)_queryTrackingBehavior;
                    hashCode = (hashCode * 397) ^ _async.GetHashCode();
                    return hashCode;
                }
            }
        }
    }
}
