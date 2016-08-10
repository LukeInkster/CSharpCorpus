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
    public class InternalIndexBuilder : InternalMetadataItemBuilder<Index>
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public InternalIndexBuilder([NotNull] Index index, [NotNull] InternalModelBuilder modelBuilder)
            : base(index, modelBuilder)
        {
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual bool IsUnique(bool isUnique, ConfigurationSource configurationSource)
        {
            if (configurationSource.Overrides(Metadata.GetIsUniqueConfigurationSource())
                || (Metadata.IsUnique == isUnique))
            {
                Metadata.SetIsUnique(isUnique, configurationSource);
                return true;
            }

            return false;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual InternalIndexBuilder Attach(ConfigurationSource configurationSource)
        {
            var entityTypeBuilder = Metadata.DeclaringEntityType.Builder;
            var newIndexBuilder = entityTypeBuilder.HasIndex(Metadata.Properties.Select(p => p.Name).ToList(), configurationSource);

            newIndexBuilder.MergeAnnotationsFrom(this);

            var isUniqueConfigurationSource = Metadata.GetIsUniqueConfigurationSource();
            if (isUniqueConfigurationSource.HasValue)
            {
                newIndexBuilder.IsUnique(Metadata.IsUnique, isUniqueConfigurationSource.Value);
            }

            return newIndexBuilder;
        }
    }
}
