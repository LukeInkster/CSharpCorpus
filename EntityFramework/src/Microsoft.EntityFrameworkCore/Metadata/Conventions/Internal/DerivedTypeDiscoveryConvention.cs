// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class DerivedTypeDiscoveryConvention : InheritanceDiscoveryConventionBase, IEntityTypeConvention
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual InternalEntityTypeBuilder Apply(InternalEntityTypeBuilder entityTypeBuilder)
        {
            var entityType = entityTypeBuilder.Metadata;
            var clrType = entityType.ClrType;
            if (clrType == null)
            {
                return entityTypeBuilder;
            }

            var directlyDerivedTypes = entityType.Model.GetEntityTypes().Where(t =>
                (t.BaseType == entityType.BaseType)
                && t.HasClrType()
                && (FindClosestBaseType(t) == entityType))
                .ToList();

            foreach (var directlyDerivedType in directlyDerivedTypes)
            {
                entityTypeBuilder.ModelBuilder.Entity(directlyDerivedType.ClrType, ConfigurationSource.Convention)
                    .HasBaseType(entityType, ConfigurationSource.Convention);
            }

            return entityTypeBuilder;
        }
    }
}
