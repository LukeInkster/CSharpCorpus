// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.ChangeTracking
{
    /// <summary>
    ///     Provides access to change tracking information and operations for a node in a
    ///     graph of entities that is being traversed.
    /// </summary>
    public class EntityEntryGraphNode : IInfrastructure<InternalEntityEntry>
    {
        private readonly InternalEntityEntry _internalEntityEntry;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public EntityEntryGraphNode(
            [NotNull] InternalEntityEntry internalEntityEntry,
            [CanBeNull] INavigation inboundNavigation)
        {
            Check.NotNull(internalEntityEntry, nameof(internalEntityEntry));

            _internalEntityEntry = internalEntityEntry;
            InboundNavigation = inboundNavigation;
        }

        /// <summary>
        ///     Gets the navigation property that is being traversed to reach this node in the graph.
        /// </summary>
        public virtual INavigation InboundNavigation { get; }

        /// <summary>
        ///     Gets or sets state that will be available to all nodes that are visited after this node.
        /// </summary>
        public virtual object NodeState { get; [param: CanBeNull] set; }

        /// <summary>
        ///     Gets the entry tracking information about this entity.
        /// </summary>
        public virtual EntityEntry Entry => new EntityEntry(_internalEntityEntry);

        /// <summary>
        ///     <para>
        ///         Gets the internal entry that is tracking information about this entity.
        ///     </para>
        ///     <para>
        ///         This property is intended for use by extension methods. It is not intended to be used in
        ///         application code.
        ///     </para>
        /// </summary>
        InternalEntityEntry IInfrastructure<InternalEntityEntry>.Instance => _internalEntityEntry;

        /// <summary>
        ///     Creates a new node for the entity that is being traversed next in the graph.
        /// </summary>
        /// <param name="currentNode"> The node that the entity is being traversed from. </param>
        /// <param name="internalEntityEntry">
        ///     The internal entry tracking information about the entity being traversed to.
        /// </param>
        /// <param name="reachedVia"> The navigation property that is being traversed to reach the new node. </param>
        /// <returns> The newly created node. </returns>
        public virtual EntityEntryGraphNode CreateNode(
            [NotNull] EntityEntryGraphNode currentNode,
            [NotNull] InternalEntityEntry internalEntityEntry,
            [NotNull] INavigation reachedVia)
            => new EntityEntryGraphNode(
                Check.NotNull(internalEntityEntry, nameof(internalEntityEntry)),
                Check.NotNull(reachedVia, nameof(reachedVia)))
            {
                NodeState = Check.NotNull(currentNode, nameof(currentNode)).NodeState
            };
    }
}
