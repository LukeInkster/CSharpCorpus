// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.Scaffolding.Configuration.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class AttributeConfiguration : IAttributeConfiguration
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public AttributeConfiguration(
            [NotNull] string attributeName, [CanBeNull] params string[] attributeArguments)
        {
            Check.NotEmpty(attributeName, nameof(attributeName));

            AttributeBody =
                attributeArguments == null || attributeArguments.Length == 0
                    ? StripAttribute(attributeName)
                    : StripAttribute(attributeName) + "(" + string.Join(", ", attributeArguments) + ")";
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual string AttributeBody { get; }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected static string StripAttribute([NotNull] string attributeName)
            => attributeName.EndsWith("Attribute", StringComparison.Ordinal)
                ? attributeName.Substring(0, attributeName.Length - 9)
                : attributeName;
    }
}
