// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Diagnostics;
using System.Linq;
using JetBrains.Annotations;

namespace Microsoft.EntityFrameworkCore.Metadata.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    [DebuggerDisplay("{Metadata,nq}")]
    public class InternalKeyBuilder : InternalMetadataItemBuilder<Key>
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public InternalKeyBuilder([NotNull] Key key, [NotNull] InternalModelBuilder modelBuilder)
            : base(key, modelBuilder)
        {
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual InternalKeyBuilder Attach(ConfigurationSource configurationSource)
        {
            // TODO: attach to same entity type
            // Issue #2611
            var entityTypeBuilder = Metadata.DeclaringEntityType.RootType().Builder;

            var propertyNames = Metadata.Properties.Select(p => p.Name).ToList();
            foreach (var propertyName in propertyNames)
            {
                if (entityTypeBuilder.Metadata.FindProperty(propertyName) == null)
                {
                    return null;
                }
            }

            var newKeyBuilder = entityTypeBuilder.HasKey(propertyNames, configurationSource);

            newKeyBuilder?.MergeAnnotationsFrom(this);

            return newKeyBuilder;
        }
    }
}
