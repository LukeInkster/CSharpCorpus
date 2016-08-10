// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;

// ReSharper disable once CheckNamespace

namespace Microsoft.EntityFrameworkCore.Specification.Tests
{
    public static class Extensions
    {
        public static IEnumerable<T> NullChecked<T>(this IEnumerable<T> enumerable)
            => enumerable ?? Enumerable.Empty<T>();

        public static void ForEach<T>(this IEnumerable<T> @this, Action<T> action)
        {
            foreach (var item in @this)
            {
                action(item);
            }
        }

        public static IModel Clone(this IModel model)
        {
            var modelClone = new Model();
            var clonedEntityTypes = new Dictionary<IEntityType, EntityType>();
            foreach (var entityType in model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;
                var clonedEntityType = clrType == null
                    ? modelClone.AddEntityType(entityType.Name)
                    : modelClone.AddEntityType(clrType);

                clonedEntityTypes.Add(entityType, clonedEntityType);
            }

            foreach (var clonedEntityType in clonedEntityTypes)
            {
                if (clonedEntityType.Key.BaseType != null)
                {
                    clonedEntityType.Value.HasBaseType(clonedEntityTypes[clonedEntityType.Key.BaseType]);
                }
            }

            foreach (var clonedEntityType in clonedEntityTypes)
            {
                CloneProperties(clonedEntityType.Key, clonedEntityType.Value);
            }

            foreach (var clonedEntityType in clonedEntityTypes)
            {
                CloneIndexes(clonedEntityType.Key, clonedEntityType.Value);
            }

            foreach (var clonedEntityType in clonedEntityTypes)
            {
                CloneKeys(clonedEntityType.Key, clonedEntityType.Value);
            }

            foreach (var clonedEntityType in clonedEntityTypes)
            {
                CloneForeignKeys(clonedEntityType.Key, clonedEntityType.Value);
            }

            foreach (var clonedEntityType in clonedEntityTypes)
            {
                CloneNavigations(clonedEntityType.Key, clonedEntityType.Value);
            }

            return modelClone;
        }

        private static void CloneProperties(IEntityType sourceEntityType, EntityType targetEntityType)
        {
            foreach (var property in sourceEntityType.GetDeclaredProperties())
            {
                var clonedProperty = targetEntityType.AddProperty(property.Name, property.ClrType, property.IsShadowProperty);
                clonedProperty.IsNullable = property.IsNullable;
                clonedProperty.IsConcurrencyToken = property.IsConcurrencyToken;
                clonedProperty.RequiresValueGenerator = property.RequiresValueGenerator;
                clonedProperty.ValueGenerated = property.ValueGenerated;
                clonedProperty.IsReadOnlyBeforeSave = property.IsReadOnlyBeforeSave;
                clonedProperty.IsReadOnlyAfterSave = property.IsReadOnlyAfterSave;
                property.GetAnnotations().ForEach(annotation => clonedProperty[annotation.Name] = annotation.Value);
            }
        }

        private static void CloneKeys(IEntityType sourceEntityType, EntityType targetEntityType)
        {
            foreach (var key in sourceEntityType.GetDeclaredKeys())
            {
                var clonedKey = targetEntityType.AddKey(
                    key.Properties.Select(p => targetEntityType.FindProperty(p.Name)).ToList());
                if (key.IsPrimaryKey())
                {
                    targetEntityType.SetPrimaryKey(clonedKey.Properties);
                }
                key.GetAnnotations().ForEach(annotation => clonedKey[annotation.Name] = annotation.Value);
            }
        }

        private static void CloneIndexes(IEntityType sourceEntityType, EntityType targetEntityType)
        {
            foreach (var index in sourceEntityType.GetDeclaredIndexes())
            {
                var clonedIndex = targetEntityType.AddIndex(
                    index.Properties.Select(p => targetEntityType.FindProperty(p.Name)).ToList());
                clonedIndex.IsUnique = index.IsUnique;
                index.GetAnnotations().ForEach(annotation => clonedIndex[annotation.Name] = annotation.Value);
            }
        }

        private static void CloneForeignKeys(IEntityType sourceEntityType, EntityType targetEntityType)
        {
            foreach (var foreignKey in sourceEntityType.GetDeclaredForeignKeys())
            {
                var targetPrincipalEntityType = targetEntityType.Model.FindEntityType(foreignKey.PrincipalEntityType.Name);
                var clonedForeignKey = targetEntityType.AddForeignKey(
                    foreignKey.Properties.Select(p => targetEntityType.FindProperty(p.Name)).ToList(),
                    targetPrincipalEntityType.FindKey(foreignKey.PrincipalKey.Properties.Select(p => targetPrincipalEntityType.FindProperty(p.Name)).ToList()),
                    targetPrincipalEntityType);
                clonedForeignKey.IsUnique = foreignKey.IsUnique;
                clonedForeignKey.IsRequired = foreignKey.IsRequired;
                foreignKey.GetAnnotations().ForEach(annotation => clonedForeignKey[annotation.Name] = annotation.Value);
            }
        }

        private static void CloneNavigations(IEntityType sourceEntityType, EntityType targetEntityType)
        {
            foreach (var navigation in sourceEntityType.GetDeclaredNavigations())
            {
                var targetDependentEntityType = targetEntityType.Model.FindEntityType(navigation.ForeignKey.DeclaringEntityType.Name);
                var targetPrincipalEntityType = targetEntityType.Model.FindEntityType(navigation.ForeignKey.PrincipalEntityType.Name);
                var targetForeignKey = targetDependentEntityType.FindForeignKey(
                    navigation.ForeignKey.Properties.Select(p => targetDependentEntityType.FindProperty(p.Name)).ToList(),
                    targetPrincipalEntityType.FindKey(navigation.ForeignKey.PrincipalKey.Properties.Select(
                        p => targetPrincipalEntityType.FindProperty(p.Name)).ToList()),
                    targetPrincipalEntityType);
                var clonedNavigation = navigation.IsDependentToPrincipal()
                    ? (navigation.GetPropertyInfo() != null
                        ? targetForeignKey.HasDependentToPrincipal(navigation.GetPropertyInfo())
                        : targetForeignKey.HasDependentToPrincipal(navigation.Name))
                    : (navigation.GetPropertyInfo() != null
                        ? targetForeignKey.HasPrincipalToDependent(navigation.GetPropertyInfo())
                        : targetForeignKey.HasPrincipalToDependent(navigation.Name));
                navigation.GetAnnotations().ForEach(annotation => clonedNavigation[annotation.Name] = annotation.Value);
            }
        }
    }
}
