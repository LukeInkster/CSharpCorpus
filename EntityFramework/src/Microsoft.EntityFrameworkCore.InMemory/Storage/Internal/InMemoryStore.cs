// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.EntityFrameworkCore.Update;
using Microsoft.Extensions.Logging;

namespace Microsoft.EntityFrameworkCore.Storage.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class InMemoryStore : IInMemoryStore
    {
        private readonly IInMemoryTableFactory _tableFactory;

        private readonly object _lock = new object();

        private LazyRef<Dictionary<IEntityType, IInMemoryTable>> _tables = CreateTables();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public InMemoryStore([NotNull] IInMemoryTableFactory tableFactory)
        {
            _tableFactory = tableFactory;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual bool EnsureCreated(IModel model)
        {
            lock (_lock)
            {
                var returnValue = !_tables.HasValue;

                // ReSharper disable once UnusedVariable
                var _ = _tables.Value;

                return returnValue;
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual bool Clear()
        {
            lock (_lock)
            {
                if (!_tables.HasValue)
                {
                    return false;
                }

                _tables = CreateTables();
                return true;
            }
        }

        private static LazyRef<Dictionary<IEntityType, IInMemoryTable>> CreateTables()
        {
            return new LazyRef<Dictionary<IEntityType, IInMemoryTable>>(
                () => new Dictionary<IEntityType, IInMemoryTable>());
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual IReadOnlyList<InMemoryTableSnapshot> GetTables(IEntityType entityType)
        {
            var data = new List<InMemoryTableSnapshot>();
            lock (_lock)
            {
                if (_tables.HasValue)
                {
                    foreach (var et in entityType.GetConcreteTypesInHierarchy())
                    {
                        IInMemoryTable table;

                        if (_tables.Value.TryGetValue(et, out table))
                        {
                            data.Add(new InMemoryTableSnapshot(et, table.SnapshotRows()));
                        }
                    }
                }
            }
            return data;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual int ExecuteTransaction(IEnumerable<IUpdateEntry> entries, ILogger<InMemoryDatabase> logger)
        {
            var rowsAffected = 0;

            lock (_lock)
            {
                foreach (var entry in entries)
                {
                    var entityType = entry.EntityType;

                    Debug.Assert(!entityType.IsAbstract());

                    IInMemoryTable table;
                    if (!_tables.Value.TryGetValue(entityType, out table))
                    {
                        _tables.Value.Add(entityType, table = _tableFactory.Create(entityType));
                    }

                    switch (entry.EntityState)
                    {
                        case EntityState.Added:
                            table.Create(entry);
                            break;
                        case EntityState.Deleted:
                            table.Delete(entry);
                            break;
                        case EntityState.Modified:
                            table.Update(entry);
                            break;
                    }

                    rowsAffected++;
                }
            }

            logger.LogInformation<object>(
                InMemoryEventId.SavedChanges,
                rowsAffected,
                InMemoryStrings.LogSavedChanges);

            return rowsAffected;
        }
    }
}
