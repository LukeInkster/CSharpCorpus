// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Utilities;

// ReSharper disable once CheckNamespace
namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    ///     Relational database specific extension methods for <see cref="IndexBuilder"/>.
    /// </summary>
    public static class RelationalIndexBuilderExtensions
    {
        /// <summary>
        ///     Configures the name of the index in the database when targeting a relational database.
        /// </summary>
        /// <param name="indexBuilder"> The builder for the index being configured. </param>
        /// <param name="name"> The name of the index. </param>
        /// <returns> A builder to further configure the index. </returns>
        public static IndexBuilder HasName([NotNull] this IndexBuilder indexBuilder, [CanBeNull] string name)
        {
            Check.NotNull(indexBuilder, nameof(indexBuilder));
            Check.NullButNotEmpty(name, nameof(name));

            indexBuilder.GetInfrastructure<InternalIndexBuilder>().Relational(ConfigurationSource.Explicit).HasName(name);

            return indexBuilder;
        }
    }
}
