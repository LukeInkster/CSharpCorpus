// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.EntityFrameworkCore.Metadata.Internal;

namespace Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class CascadeDeleteConvention : IForeignKeyConvention, IPropertyNullableConvention
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual bool Apply(InternalPropertyBuilder propertyBuilder)
        {
            foreach (var foreignKey in propertyBuilder.Metadata.GetContainingForeignKeys())
            {
                Apply(foreignKey.Builder);
            }

            return true;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual InternalRelationshipBuilder Apply(InternalRelationshipBuilder relationshipBuilder)
        {
            relationshipBuilder.DeleteBehavior(
                relationshipBuilder.Metadata.IsRequired
                    ? DeleteBehavior.Cascade
                    : DeleteBehavior.Restrict,
                ConfigurationSource.Convention);

            return relationshipBuilder;
        }
    }
}
