// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using System.Reflection;
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
    public class PropertyAccessorsFactory
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public virtual PropertyAccessors Create([NotNull] IPropertyBase propertyBase)
            => (PropertyAccessors)_genericCreate
                .MakeGenericMethod(propertyBase.GetClrType())
                .Invoke(null, new object[] { propertyBase });

        private static readonly MethodInfo _genericCreate
            = typeof(PropertyAccessorsFactory).GetTypeInfo().GetDeclaredMethod(nameof(CreateGeneric));

        [UsedImplicitly]
        private static PropertyAccessors CreateGeneric<TProperty>(IPropertyBase propertyBase)
        {
            var property = propertyBase as IProperty;
            return new PropertyAccessors(
                CreateCurrentValueGetter<TProperty>(propertyBase),
                property == null ? null : CreateOriginalValueGetter<TProperty>(property),
                CreateRelationshipSnapshotGetter<TProperty>(propertyBase),
                property == null ? null : CreateValueBufferGetter(property));
        }

        private static Func<InternalEntityEntry, TProperty> CreateCurrentValueGetter<TProperty>(IPropertyBase propertyBase)
        {
            var entityClrType = propertyBase.DeclaringEntityType.ClrType;
            var entryParameter = Expression.Parameter(typeof(InternalEntityEntry), "entry");

            var shadowIndex = (propertyBase as IProperty)?.GetShadowIndex() ?? -1;
            Expression currentValueExpression;
            if (shadowIndex >= 0)
            {
                currentValueExpression = Expression.Call(
                    entryParameter,
                    InternalEntityEntry.ReadShadowValueMethod.MakeGenericMethod(typeof(TProperty)),
                    Expression.Constant(shadowIndex));
            }
            else
            {
                currentValueExpression = Expression.Property(
                    Expression.Convert(
                        Expression.Property(entryParameter, "Entity"),
                        entityClrType),
                    propertyBase.GetPropertyInfo());
            }

            var storeGeneratedIndex = propertyBase.GetStoreGeneratedIndex();
            if (storeGeneratedIndex >= 0)
            {
                currentValueExpression = Expression.Call(
                    entryParameter,
                    InternalEntityEntry.ReadStoreGeneratedValueMethod.MakeGenericMethod(typeof(TProperty)),
                    currentValueExpression,
                    Expression.Constant(storeGeneratedIndex));
            }

            return Expression.Lambda<Func<InternalEntityEntry, TProperty>>(
                currentValueExpression,
                entryParameter)
                .Compile();
        }

        private static Func<InternalEntityEntry, TProperty> CreateOriginalValueGetter<TProperty>(IProperty property)
        {
            var entryParameter = Expression.Parameter(typeof(InternalEntityEntry), "entry");
            var originalValuesIndex = property.GetOriginalValueIndex();

            return Expression.Lambda<Func<InternalEntityEntry, TProperty>>(
                originalValuesIndex >= 0
                    ? (Expression)Expression.Call(
                        entryParameter,
                        InternalEntityEntry.ReadOriginalValueMethod.MakeGenericMethod(typeof(TProperty)),
                        Expression.Constant(property),
                        Expression.Constant(originalValuesIndex))
                    : Expression.Block(
                        Expression.Throw(Expression.Constant(
                            new InvalidOperationException(
                                CoreStrings.OriginalValueNotTracked(property.Name, property.DeclaringEntityType.DisplayName())))),
                        Expression.Constant(default(TProperty), typeof(TProperty))),
                entryParameter)
                .Compile();
        }

        private static Func<InternalEntityEntry, TProperty> CreateRelationshipSnapshotGetter<TProperty>(IPropertyBase propertyBase)
        {
            var entryParameter = Expression.Parameter(typeof(InternalEntityEntry), "entry");
            var relationshipIndex = (propertyBase as IProperty)?.GetRelationshipIndex() ?? -1;

            return Expression.Lambda<Func<InternalEntityEntry, TProperty>>(
                relationshipIndex >= 0
                    ? Expression.Call(
                        entryParameter,
                        InternalEntityEntry.ReadRelationshipSnapshotValueMethod.MakeGenericMethod(typeof(TProperty)),
                        Expression.Constant(propertyBase),
                        Expression.Constant(relationshipIndex))
                    : Expression.Call(
                        entryParameter,
                        InternalEntityEntry.GetCurrentValueMethod.MakeGenericMethod(typeof(TProperty)),
                        Expression.Constant(propertyBase)),
                entryParameter)
                .Compile();
        }

        private static Func<ValueBuffer, object> CreateValueBufferGetter(IProperty property)
        {
            var valueBufferParameter = Expression.Parameter(typeof(ValueBuffer), "valueBuffer");

            return Expression.Lambda<Func<ValueBuffer, object>>(
                Expression.Call(
                    valueBufferParameter,
                    ValueBuffer.GetValueMethod,
                    Expression.Constant(property.GetIndex())),
                valueBufferParameter)
                .Compile();
        }
    }
}
