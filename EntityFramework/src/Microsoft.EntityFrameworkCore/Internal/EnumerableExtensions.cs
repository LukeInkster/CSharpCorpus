// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;

namespace Microsoft.EntityFrameworkCore.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [DebuggerStepThrough]
    public static class EnumerableExtensions
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IOrderedEnumerable<TSource> OrderByOrdinal<TSource>(
            [NotNull] this IEnumerable<TSource> source,
            [NotNull] Func<TSource, string> keySelector)
            => source.OrderBy(keySelector, StringComparer.Ordinal);

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<T> Distinct<T>(
            [NotNull] this IEnumerable<T> source,
            [NotNull] Func<T, T, bool> comparer)
            where T : class
            => source.Distinct(new DynamicEqualityComparer<T>(comparer));

        private sealed class DynamicEqualityComparer<T> : IEqualityComparer<T>
            where T : class
        {
            private readonly Func<T, T, bool> _func;

            public DynamicEqualityComparer(Func<T, T, bool> func)
            {
                _func = func;
            }

            public bool Equals(T x, T y) => _func(x, y);

            public int GetHashCode(T obj) => 0;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static string Join(
            [NotNull] this IEnumerable<object> source,
            [NotNull] string separator = ", ")
            => string.Join(separator, source);

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static bool StructuralSequenceEqual<TSource>(
            [NotNull] this IEnumerable<TSource> first,
            [NotNull] IEnumerable<TSource> second)
        {
            if (ReferenceEquals(first, second))
            {
                return true;
            }

            var firstEnumerator = first.GetEnumerator();
            var secondEnumerator = second.GetEnumerator();

            while (firstEnumerator.MoveNext())
            {
                if (!secondEnumerator.MoveNext()
                    || !StructuralComparisons.StructuralEqualityComparer
                        .Equals(firstEnumerator.Current, secondEnumerator.Current))
                {
                    return false;
                }
            }

            return !secondEnumerator.MoveNext();
        }
    }
}
