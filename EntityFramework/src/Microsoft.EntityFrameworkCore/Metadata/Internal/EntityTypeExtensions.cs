// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.ChangeTracking.Internal;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Storage;

namespace Microsoft.EntityFrameworkCore.Metadata.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public static class EntityTypeExtensions
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static string DisplayName([NotNull] this IEntityType entityType)
            => entityType.ClrType != null
                ? entityType.ClrType.ShortDisplayName()
                : entityType.Name;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<IEntityType> GetAllBaseTypesInclusive([NotNull] this IEntityType entityType)
        {
            var baseTypes = new List<IEntityType>();
            var currentEntityType = entityType;
            while (currentEntityType.BaseType != null)
            {
                currentEntityType = currentEntityType.BaseType;
                baseTypes.Add(currentEntityType);
            }

            baseTypes.Reverse();
            baseTypes.Add(entityType);

            return baseTypes;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<IEntityType> GetDirectlyDerivedTypes([NotNull] this IEntityType entityType)
        {
            // ReSharper disable once LoopCanBeConvertedToQuery
            foreach (var derivedType in entityType.Model.GetEntityTypes())
            {
                if (derivedType.BaseType == entityType)
                {
                    yield return derivedType;
                }
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<IEntityType> GetDerivedTypesInclusive([NotNull] this IEntityType entityType)
            => new[] { entityType }.Concat(entityType.GetDerivedTypes());

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static bool UseEagerSnapshots([NotNull] this IEntityType entityType)
        {
            var changeTrackingStrategy = entityType.GetChangeTrackingStrategy();

            return changeTrackingStrategy == ChangeTrackingStrategy.Snapshot
                   || changeTrackingStrategy == ChangeTrackingStrategy.ChangedNotifications;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static int StoreGeneratedCount([NotNull] this IEntityType entityType)
            => GetCounts(entityType).StoreGeneratedCount;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static int RelationshipPropertyCount([NotNull] this IEntityType entityType)
            => GetCounts(entityType).RelationshipCount;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static int OriginalValueCount([NotNull] this IEntityType entityType)
            => GetCounts(entityType).OriginalValueCount;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static int ShadowPropertyCount([NotNull] this IEntityType entityType)
            => GetCounts(entityType).ShadowCount;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static int NavigationCount([NotNull] this IEntityType entityType)
            => GetCounts(entityType).NavigationCount;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static int PropertyCount([NotNull] this IEntityType entityType)
            => GetCounts(entityType).PropertyCount;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static PropertyCounts GetCounts([NotNull] this IEntityType entityType)
            => entityType.AsEntityType().Counts;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static PropertyCounts CalculateCounts([NotNull] this IEntityType entityType)
        {
            var properties = entityType.GetDeclaredProperties().ToList();
            var navigations = entityType.GetDeclaredNavigations();

            var isNotifying = entityType.GetChangeTrackingStrategy() != ChangeTrackingStrategy.Snapshot;

            var propertyCount = properties.Count;
            var navigationCount = navigations.Count();
            var originalValueCount = properties.Count(p => p.RequiresOriginalValue());
            var shadowCount = properties.Count(p => p.IsShadowProperty);
            var relationshipCount = (isNotifying ? navigations.Count(n => !n.IsCollection()) : navigationCount) 
                + properties.Count(p => p.IsKeyOrForeignKey());
            var storeGeneratedCount = properties.Count(p => p.MayBeStoreGenerated());

            var baseCounts = entityType.BaseType?.CalculateCounts();

            return baseCounts == null
                ? new PropertyCounts(
                    propertyCount,
                    navigationCount,
                    originalValueCount,
                    shadowCount,
                    relationshipCount,
                    storeGeneratedCount)
                : new PropertyCounts(
                    baseCounts.PropertyCount + propertyCount,
                    baseCounts.NavigationCount + navigationCount,
                    baseCounts.OriginalValueCount + originalValueCount,
                    baseCounts.ShadowCount + shadowCount,
                    baseCounts.RelationshipCount + relationshipCount,
                    baseCounts.StoreGeneratedCount + storeGeneratedCount);
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static Func<InternalEntityEntry, ISnapshot> GetRelationshipSnapshotFactory([NotNull] this IEntityType entityType)
            => entityType.AsEntityType().RelationshipSnapshotFactory;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static Func<InternalEntityEntry, ISnapshot> GetOriginalValuesFactory([NotNull] this IEntityType entityType)
            => entityType.AsEntityType().OriginalValuesFactory;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static Func<ValueBuffer, ISnapshot> GetShadowValuesFactory([NotNull] this IEntityType entityType)
            => entityType.AsEntityType().ShadowValuesFactory;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static Func<ISnapshot> GetEmptyShadowValuesFactory([NotNull] this IEntityType entityType)
            => entityType.AsEntityType().EmptyShadowValuesFactory;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static bool HasClrType([NotNull] this IEntityType entityType)
            => entityType.ClrType != null;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<IEntityType> GetConcreteTypesInHierarchy([NotNull] this IEntityType entityType)
            => entityType.GetDerivedTypesInclusive().Where(et => !et.IsAbstract());

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static bool IsSameHierarchy([NotNull] this IEntityType firstEntityType, [NotNull] IEntityType secondEntityType)
            => firstEntityType.IsAssignableFrom(secondEntityType)
               || secondEntityType.IsAssignableFrom(firstEntityType);

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static EntityType LeastDerivedType([NotNull] this EntityType entityType, [NotNull] EntityType otherEntityType)
            => (EntityType)((IEntityType)entityType).LeastDerivedType(otherEntityType);

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static bool IsAbstract([NotNull] this IEntityType entityType)
            => entityType.ClrType?.GetTypeInfo().IsAbstract ?? false;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IKey FindDeclaredPrimaryKey([NotNull] this IEntityType entityType)
            => entityType.BaseType == null ? entityType.FindPrimaryKey() : null;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<IKey> GetDeclaredKeys([NotNull] this IEntityType entityType)
            => entityType.AsEntityType().GetDeclaredKeys();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<IForeignKey> GetDeclaredForeignKeys([NotNull] this IEntityType entityType)
            => entityType.AsEntityType().GetDeclaredForeignKeys();

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<INavigation> GetDeclaredNavigations([NotNull] this IEntityType entityType)
            => entityType.GetDeclaredForeignKeys()
                .Concat(entityType.GetDeclaredReferencingForeignKeys())
                .SelectMany(foreignKey => foreignKey.FindNavigationsFrom(entityType))
                .Distinct()
                .OrderBy(m => m.Name);

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<IForeignKey> GetDeclaredReferencingForeignKeys([NotNull] this IEntityType entityType)
            => entityType.Model.GetEntityTypes().SelectMany(et => et.GetDeclaredForeignKeys())
                .Where(fk => fk.PrincipalEntityType == entityType);

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<INavigation> FindDerivedNavigations(
            [NotNull] this IEntityType entityType, [NotNull] string navigationName)
            => entityType.GetDerivedTypes().SelectMany(et =>
                et.GetDeclaredNavigations().Where(navigation => navigationName == navigation.Name));

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<IProperty> GetDeclaredProperties([NotNull] this IEntityType entityType)
            => entityType.GetProperties().Where(p => p.DeclaringEntityType == entityType);

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<IProperty> FindDerivedProperties(
            [NotNull] this IEntityType entityType, [NotNull] string propertyName)
            => entityType.GetDerivedTypes().SelectMany(et =>
                et.GetDeclaredProperties().Where(property => propertyName.Equals(property.Name)));

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<IPropertyBase> GetPropertiesAndNavigations(
            [NotNull] this IEntityType entityType)
            => entityType.GetProperties().Concat<IPropertyBase>(entityType.GetNavigations());

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<IIndex> GetDeclaredIndexes([NotNull] this IEntityType entityType)
            => entityType.GetIndexes().Where(p => p.DeclaringEntityType == entityType);

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static string CheckChangeTrackingStrategy([NotNull] this IEntityType entityType, ChangeTrackingStrategy value)
        {
            if (entityType.ClrType != null)
            {
                if (value != ChangeTrackingStrategy.Snapshot
                    && !typeof(INotifyPropertyChanged).GetTypeInfo().IsAssignableFrom(entityType.ClrType.GetTypeInfo()))
                {
                    return CoreStrings.ChangeTrackingInterfaceMissing(entityType.DisplayName(), value, nameof(INotifyPropertyChanged));
                }

                if ((value == ChangeTrackingStrategy.ChangingAndChangedNotifications
                     || value == ChangeTrackingStrategy.ChangingAndChangedNotificationsWithOriginalValues)
                    && !typeof(INotifyPropertyChanging).GetTypeInfo().IsAssignableFrom(entityType.ClrType.GetTypeInfo()))
                {
                    return CoreStrings.ChangeTrackingInterfaceMissing(entityType.DisplayName(), value, nameof(INotifyPropertyChanging));
                }
            }

            return null;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static IEnumerable<IPropertyBase> GetNotificationProperties(
            [NotNull] this IEntityType entityType, [CanBeNull] string propertyName)
        {
            if (string.IsNullOrEmpty(propertyName))
            {
                foreach (var property in entityType.GetProperties().Where(p => !p.IsReadOnlyAfterSave))
                {
                    yield return property;
                }

                foreach (var navigation in entityType.GetNavigations())
                {
                    yield return navigation;
                }
            }
            else
            {
                var property = (IPropertyBase)entityType.FindProperty(propertyName)
                    ?? entityType.FindNavigation(propertyName);
                if (property != null)
                {
                    yield return property;
                }
            }
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public static EntityType AsEntityType([NotNull] this IEntityType entityType, [NotNull] [CallerMemberName] string methodName = "")
            => entityType.AsConcreteMetadataType<IEntityType, EntityType>(methodName);
    }
}
