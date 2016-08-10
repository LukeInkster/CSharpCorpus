// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Query.Internal;

namespace Microsoft.EntityFrameworkCore.Metadata.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public static class KeyExtensions
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static Func<IIdentityMap> GetIdentityMapFactory([NotNull] this IKey key)
            => key.AsKey().IdentityMapFactory;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static Func<IWeakReferenceIdentityMap> GetWeakReferenceIdentityMapFactory([NotNull] this IKey key)
            => key.AsKey().WeakReferenceIdentityMapFactory;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IPrincipalKeyValueFactory<TKey> GetPrincipalKeyValueFactory<TKey>([NotNull] this IKey key)
            => key.AsKey().GetPrincipalKeyValueFactory<TKey>();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static bool IsPrimaryKey([NotNull] this IKey key)
            => key == key.DeclaringEntityType.FindPrimaryKey();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static Key AsKey([NotNull] this IKey key, [NotNull] [CallerMemberName] string methodName = "")
            => key.AsConcreteMetadataType<IKey, Key>(methodName);
    }
}
