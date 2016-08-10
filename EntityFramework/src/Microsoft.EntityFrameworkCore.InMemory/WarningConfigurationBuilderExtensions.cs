// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore
{
    /// <summary>
    ///     In-memory specific extension methods for <see cref="WarningsConfigurationBuilder"/>.
    /// </summary>
    public static class WarningConfigurationBuilderExtensions
    {
        /// <summary>
        ///     Causes an exception to be thrown when the specified in-memory warnings are generated.  
        /// </summary>
        /// <param name="warningsConfigurationBuilder"> The builder being used to configure warnings. </param>
        /// <param name="inMemoryEventIds">
        ///     The <see cref="InMemoryEventId"/>(s) for the warnings.
        /// </param>
        /// <returns> The same builder instance so that multiple calls can be chained. </returns>
        public static WarningsConfigurationBuilder Throw(
            [NotNull] this WarningsConfigurationBuilder warningsConfigurationBuilder,
            [NotNull] params InMemoryEventId[] inMemoryEventIds)
        {
            Check.NotNull(warningsConfigurationBuilder, nameof(warningsConfigurationBuilder));
            Check.NotNull(inMemoryEventIds, nameof(inMemoryEventIds));

            warningsConfigurationBuilder.Configuration
                .AddExplicit(inMemoryEventIds.Cast<object>(), WarningBehavior.Throw);

            return warningsConfigurationBuilder;
        }

        /// <summary>
        ///     Causes a warning to be logged when the specified in-memory warnings are generated.
        /// </summary>
        /// <param name="warningsConfigurationBuilder"> The builder being used to configure warnings. </param>
        /// <param name="inMemoryEventIds">
        ///     The <see cref="InMemoryEventId"/>(s) for the warnings.
        /// </param>
        /// <returns> The same builder instance so that multiple calls can be chained. </returns>
        public static WarningsConfigurationBuilder Log(
            [NotNull] this WarningsConfigurationBuilder warningsConfigurationBuilder,
            [NotNull] params InMemoryEventId[] inMemoryEventIds)
        {
            Check.NotNull(warningsConfigurationBuilder, nameof(warningsConfigurationBuilder));
            Check.NotNull(inMemoryEventIds, nameof(inMemoryEventIds));

            warningsConfigurationBuilder.Configuration
                .AddExplicit(inMemoryEventIds.Cast<object>(), WarningBehavior.Log);

            return warningsConfigurationBuilder;
        }

        /// <summary>
        ///     Causes nothing to happen when the specified in-memory warnings are generated.
        /// </summary>
        /// <param name="warningsConfigurationBuilder"> The builder being used to configure warnings. </param>
        /// <param name="inMemoryEventIds">
        ///     The <see cref="InMemoryEventId"/>(s) for the warnings.
        /// </param>
        /// <returns> The same builder instance so that multiple calls can be chained. </returns>
        public static WarningsConfigurationBuilder Ignore(
            [NotNull] this WarningsConfigurationBuilder warningsConfigurationBuilder,
            [NotNull] params InMemoryEventId[] inMemoryEventIds)
        {
            Check.NotNull(warningsConfigurationBuilder, nameof(warningsConfigurationBuilder));
            Check.NotNull(inMemoryEventIds, nameof(inMemoryEventIds));

            warningsConfigurationBuilder.Configuration
                .AddExplicit(inMemoryEventIds.Cast<object>(), WarningBehavior.Ignore);

            return warningsConfigurationBuilder;
        }
    }
}
