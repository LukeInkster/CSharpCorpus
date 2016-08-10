﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using Moq;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.ModelBinding.Binders
{
    public class CollectionModelBinderProviderTest
    {
        [Theory]
        [InlineData(typeof(object))]
        [InlineData(typeof(int))]
        [InlineData(typeof(Person))]
        [InlineData(typeof(int[]))]
        public void Create_ForNonSupportedTypes_ReturnsNull(Type modelType)
        {
            // Arrange
            var provider = new CollectionModelBinderProvider();

            var context = new TestModelBinderProviderContext(modelType);

            // Act
            var result = provider.GetBinder(context);

            // Assert
            Assert.Null(result);
        }

        [Theory]

        // These aren't ICollection<> - we can handle them by creating a List<>
        [InlineData(typeof(IEnumerable<int>))]
        [InlineData(typeof(IReadOnlyCollection<int>))]
        [InlineData(typeof(IReadOnlyList<int>))]

        // These are ICollection<> - we can handle them by adding items to the existing collection or
        // creating a new one.
        [InlineData(typeof(ICollection<int>))]
        [InlineData(typeof(IList<int>))]
        [InlineData(typeof(List<int>))]
        [InlineData(typeof(Collection<int>))]
        [InlineData(typeof(IEnumerable<int>))]
        [InlineData(typeof(IReadOnlyCollection<int>))]
        [InlineData(typeof(IReadOnlyList<int>))]
        public void Create_ForSupportedTypes_ReturnsBinder(Type modelType)
        {
            // Arrange
            var provider = new CollectionModelBinderProvider();

            var context = new TestModelBinderProviderContext(modelType);

            Type elementType = null;
            context.OnCreatingBinder(m =>
            {
                Assert.Equal(typeof(int), m.ModelType);
                elementType = m.ModelType;
                return Mock.Of<IModelBinder>();
            });

            // Act
            var result = provider.GetBinder(context);

            // Assert
            Assert.NotNull(elementType);
            Assert.IsType<CollectionModelBinder<int>>(result);
        }

        private class Person
        {
            public string Name { get; set; }

            public int Age { get; set; }
        }
    }
}
