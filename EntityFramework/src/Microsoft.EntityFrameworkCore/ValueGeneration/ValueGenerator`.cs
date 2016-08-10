// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Microsoft.EntityFrameworkCore.ValueGeneration
{
    /// <summary>
    ///     Generates values for properties when an entity is added to a context.
    /// </summary>
    public abstract class ValueGenerator<TValue> : ValueGenerator
    {
        /// <summary>
        ///     Template method to be overridden by implementations to perform value generation.
        /// </summary>
        /// <para>The change tracking entry of the entity for which the value is being generated.</para>
        /// <returns> The generated value. </returns>
        public new abstract TValue Next([NotNull] EntityEntry entry);

        /// <summary>
        ///     Template method to be overridden by implementations to perform value generation.
        /// </summary>
        /// <para>The change tracking entry of the entity for which the value is being generated.</para>
        /// <returns> The generated value. </returns>
        public new virtual Task<TValue> NextAsync(
            [NotNull] EntityEntry entry,
            CancellationToken cancellationToken = default(CancellationToken))
            => Task.FromResult(Next(entry));

        /// <summary>
        ///     Gets a value to be assigned to a property.
        /// </summary>
        /// <para>The change tracking entry of the entity for which the value is being generated.</para>
        /// <returns> The value to be assigned to a property. </returns>
        protected override object NextValue(EntityEntry entry)
            => Next(entry);

        /// <summary>
        ///     Gets a value to be assigned to a property.
        /// </summary>
        /// <para>The change tracking entry of the entity for which the value is being generated.</para>
        /// <returns> The value to be assigned to a property. </returns>
        protected override Task<object> NextValueAsync(
            EntityEntry entry,
            CancellationToken cancellationToken = default(CancellationToken))
            => Task.FromResult((object)Next(entry));
    }
}
