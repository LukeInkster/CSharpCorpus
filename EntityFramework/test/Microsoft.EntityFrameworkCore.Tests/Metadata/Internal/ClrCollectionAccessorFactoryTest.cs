// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Moq;
using Xunit;

// ReSharper disable ConvertToAutoPropertyWhenPossible
// ReSharper disable ConvertToAutoPropertyWithPrivateSetter
// ReSharper disable UnusedMember.Local
namespace Microsoft.EntityFrameworkCore.Tests.Metadata.Internal
{
    public class ClrCollectionAccessorFactoryTest
    {
        [Fact]
        public void Navigation_is_returned_if_it_implements_IClrCollectionAccessor()
        {
            var accessorMock = new Mock<IClrCollectionAccessor>();
            var navigationMock = accessorMock.As<INavigation>();

            var source = new ClrCollectionAccessorFactory();

            Assert.Same(accessorMock.Object, source.Create(navigationMock.Object));
        }

        [Fact]
        public void Delegate_accessor_is_returned_for_IEnumerable_navigation()
        {
            AccessorTest("AsIEnumerable", e => e.AsIEnumerable);
        }

        [Fact]
        public void Delegate_accessor_is_returned_for_ICollection_navigation()
        {
            AccessorTest("AsICollection", e => e.AsICollection);
        }

        [Fact]
        public void Delegate_accessor_is_returned_for_interface_navigation_derived_from_ICollection()
        {
            AccessorTest("AsIList", e => e.AsIList);
        }

        [Fact]
        public void Delegate_accessor_is_returned_for_concrete_generic_type_navigation()
        {
            AccessorTest("AsList", e => e.AsList);
        }

        [Fact]
        public void Delegate_accessor_is_returned_for_concrete_non_generic_type_navigation()
        {
            AccessorTest("AsMyCollection", e => e.AsMyCollection);
        }

        [Fact]
        public void Delegate_accessor_is_returned_when_no_setter()
        {
            AccessorTest("WithNoSetter", e => e.WithNoSetter);
        }

        [Fact]
        public void Delegate_accessor_is_returned_when_no_backing_field_found()
        {
            AccessorTest("NoBackingFound", e => e.NoBackingFound);
        }

        [Fact]
        public void Delegate_accessor_is_returned_when_no_public_constructor()
        {
            AccessorTest("AsMyPrivateCollection", e => e.AsMyPrivateCollection);
        }

        [Fact]
        public void Delegate_accessor_is_returned_when_no_internal_constructor()
        {
            AccessorTest("AsMyInternalCollection", e => e.AsMyInternalCollection);
        }

        [Fact]
        public void Delegate_accessor_is_returned_when_no_parameterless_constructor()
        {
            AccessorTest("AsMyUnavailableCollection", e => e.AsMyUnavailableCollection);
        }

        [Fact]
        public void Delegate_accessor_handles_uninitialized_collections_with_no_setter()
        {
            AccessorTest("WithNoSetter", e => e.WithNoSetter, initializeCollections: false);
        }

        [Fact]
        public void Delegate_accessor_handles_uninitialized_collections()
        {
            AccessorTest("AsICollection", e => e.AsICollection, initializeCollections: false);
        }

        [Fact]
        public void Delegate_accessor_handles_uninitialized_collections_for_interface_navigation_derived_from_ICollection()
        {
            AccessorTest("AsIList", e => e.AsIList, initializeCollections: false);
        }

        [Fact]
        public void Delegate_accessor_handles_uninitialized_collections_for_concrete_generic_type_navigation()
        {
            AccessorTest("AsList", e => e.AsList, initializeCollections: false);
        }

        [Fact]
        public void Delegate_accessor_handles_uninitialized_collections_for_concrete_non_generic_type_navigation()
        {
            AccessorTest("AsMyCollection", e => e.AsMyCollection, initializeCollections: false);
        }

        private static void AccessorTest(
            string navigationName, Func<MyEntity, IEnumerable<MyOtherEntity>> reader, bool initializeCollections = true)
        {
            var accessor = new ClrCollectionAccessorFactory().Create(CreateNavigation(navigationName));

            var entity = new MyEntity();
            var value = new MyOtherEntity();

            if (initializeCollections)
            {
                entity.InitializeCollections();
            }

            Assert.False(accessor.Contains(entity, value));
            accessor.Remove(entity, value);

            accessor.Add(entity, value);

            Assert.True(accessor.Contains(entity, value));
            Assert.Equal(1, reader(entity).Count());

            accessor.Remove(entity, value);

            Assert.False(accessor.Contains(entity, value));
            Assert.Equal(0, reader(entity).Count());
        }

        [Fact]
        public void Creating_accessor_for_navigation_without_getter_throws()
        {
            var navigation = CreateNavigation("WithNoGetter");

            Assert.Equal(
                CoreStrings.NavigationNoGetter("WithNoGetter", typeof(MyEntity).Name),
                Assert.Throws<InvalidOperationException>(() => new ClrCollectionAccessorFactory().Create(navigation)).Message);
        }

        [Fact]
        public void Add_for_enumerable_backed_by_non_collection_throws()
        {
            Enumerable_backed_by_non_collection_throws((a, e, v) => a.Add(e, v));
        }

        [Fact]
        public void AddRange_for_enumerable_backed_by_non_collection_throws()
        {
            Enumerable_backed_by_non_collection_throws((a, e, v) => a.AddRange(e, new[] { v }));
        }

        [Fact]
        public void Contains_for_enumerable_backed_by_non_collection_throws()
        {
            Enumerable_backed_by_non_collection_throws((a, e, v) => a.Contains(e, v));
        }

        [Fact]
        public void Remove_for_enumerable_backed_by_non_collection_throws()
        {
            Enumerable_backed_by_non_collection_throws((a, e, v) => a.Remove(e, v));
        }

        [Fact]
        public void GetOrCreate_for_enumerable_backed_by_non_collection_throws()
        {
            Enumerable_backed_by_non_collection_throws((a, e, v) => a.GetOrCreate(e));
        }

        private void Enumerable_backed_by_non_collection_throws(Action<IClrCollectionAccessor, MyEntity, MyOtherEntity> test)
        {
            var accessor = new ClrCollectionAccessorFactory().Create(CreateNavigation("AsIEnumerableNotCollection"));

            var entity = new MyEntity();
            var value = new MyOtherEntity();
            entity.InitializeCollections();

            Assert.Equal(
                CoreStrings.NavigationBadType(
                    "AsIEnumerableNotCollection", typeof(MyEntity).Name, typeof(MyEnumerable).Name, typeof(MyOtherEntity).Name),
                Assert.Throws<InvalidOperationException>(() => test(accessor, entity, value)).Message);
        }

        [Fact]
        public void Creating_accessor_for_array_navigation_throws()
        {
            var navigation = CreateNavigation("AsArray");

            Assert.Equal(
                CoreStrings.NavigationArray("AsArray", typeof(MyEntity).Name, typeof(MyOtherEntity[]).Name),
                Assert.Throws<InvalidOperationException>(() => new ClrCollectionAccessorFactory().Create(navigation)).Message);
        }

        [Fact]
        public void Initialization_for_navigation_without_backing_field_throws()
        {
            var accessor = new ClrCollectionAccessorFactory().Create(CreateNavigation("NoBackingFound"));

            Assert.Equal(
                CoreStrings.NavigationNoSetter("NoBackingFound", typeof(MyEntity).Name),
                Assert.Throws<InvalidOperationException>(() => accessor.Add(new MyEntity(), new MyOtherEntity())).Message);
        }

        [Fact]
        public void Initialization_for_navigation_with_private_constructor_throws()
        {
            var accessor = new ClrCollectionAccessorFactory().Create(CreateNavigation("AsMyPrivateCollection"));

            Assert.Equal(
                CoreStrings.NavigationCannotCreateType("AsMyPrivateCollection", typeof(MyEntity).Name, typeof(MyPrivateCollection).Name),
                Assert.Throws<InvalidOperationException>(() => accessor.Add(new MyEntity(), new MyOtherEntity())).Message);
        }

        [Fact]
        public void Initialization_for_navigation_with_internal_constructor_throws()
        {
            var accessor = new ClrCollectionAccessorFactory().Create(CreateNavigation("AsMyInternalCollection"));

            Assert.Equal(
                CoreStrings.NavigationCannotCreateType("AsMyInternalCollection", typeof(MyEntity).Name, typeof(MyInternalCollection).Name),
                Assert.Throws<InvalidOperationException>(() => accessor.Add(new MyEntity(), new MyOtherEntity())).Message);
        }

        [Fact]
        public void Initialization_for_navigation_without_parameterless_constructor_throws()
        {
            var accessor = new ClrCollectionAccessorFactory().Create(CreateNavigation("AsMyUnavailableCollection"));

            Assert.Equal(
                CoreStrings.NavigationCannotCreateType("AsMyUnavailableCollection", typeof(MyEntity).Name, typeof(MyUnavailableCollection).Name),
                Assert.Throws<InvalidOperationException>(() => accessor.Add(new MyEntity(), new MyOtherEntity())).Message);
        }

        private static Navigation CreateNavigation(string navigationName)
        {
            var model = new Model();
            var entityType = model.AddEntityType(typeof(MyEntity));
            var otherType = model.AddEntityType(typeof(MyOtherEntity));
            var foreignKey = otherType.GetOrAddForeignKey(otherType.AddProperty("MyEntityId", typeof(int)),
                entityType.GetOrSetPrimaryKey(entityType.AddProperty("Id", typeof(int))),
                entityType);

            return foreignKey.HasPrincipalToDependent(
                typeof(MyEntity).GetProperty(navigationName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
        }

        private class MyEntity
        {
            private ICollection<MyOtherEntity> _asICollection;
            private IList<MyOtherEntity> _asIList;
            private List<MyOtherEntity> _asList;
            private MyCollection _myCollection;
            private ICollection<MyOtherEntity> _withNoBackingFieldFound;
            private ICollection<MyOtherEntity> _withNoSetter;
            // ReSharper disable once NotAccessedField.Local
            private ICollection<MyOtherEntity> _withNoGetter;
            private IEnumerable<MyOtherEntity> _enumerable;
            private IEnumerable<MyOtherEntity> _enumerableNotCollection;
            private MyOtherEntity[] _array;
            private MyPrivateCollection _privateCollection;
            private MyInternalCollection _internalCollection;
            private MyUnavailableCollection _unavailableCollection;

            public void InitializeCollections()
            {
                _asICollection = new HashSet<MyOtherEntity>();
                _asIList = new List<MyOtherEntity>();
                _asList = new List<MyOtherEntity>();
                _myCollection = new MyCollection();
                _withNoBackingFieldFound = new HashSet<MyOtherEntity>();
                _withNoSetter = new HashSet<MyOtherEntity>();
                _withNoGetter = new HashSet<MyOtherEntity>();
                _enumerable = new HashSet<MyOtherEntity>();
                _enumerableNotCollection = new MyEnumerable();
                _array = new MyOtherEntity[0];
                _privateCollection = MyPrivateCollection.Create();
                _internalCollection = new MyInternalCollection();
                _unavailableCollection = new MyUnavailableCollection(true);
            }

            internal ICollection<MyOtherEntity> AsICollection
            {
                get { return _asICollection; }
                set { _asICollection = value; }
            }

            internal IList<MyOtherEntity> AsIList
            {
                get { return _asIList; }
                set { _asIList = value; }
            }

            internal List<MyOtherEntity> AsList
            {
                get { return _asList; }
                set { _asList = value; }
            }

            internal MyCollection AsMyCollection
            {
                get { return _myCollection; }
                set { _myCollection = value; }
            }

            internal ICollection<MyOtherEntity> WithNoSetter => _withNoSetter;

            internal ICollection<MyOtherEntity> NoBackingFound => _withNoBackingFieldFound;

            internal ICollection<MyOtherEntity> WithNoGetter
            {
                set { _withNoGetter = value; }
            }

            internal IEnumerable<MyOtherEntity> AsIEnumerable
            {
                get { return _enumerable; }
                set { _enumerable = value; }
            }

            internal IEnumerable<MyOtherEntity> AsIEnumerableNotCollection
            {
                get { return _enumerableNotCollection; }
                set { _enumerableNotCollection = value; }
            }

            internal MyOtherEntity[] AsArray
            {
                get { return _array; }
                set { _array = value; }
            }

            internal MyPrivateCollection AsMyPrivateCollection
            {
                get { return _privateCollection; }
                set { _privateCollection = value; }
            }

            internal MyInternalCollection AsMyInternalCollection
            {
                get { return _internalCollection; }
                set { _internalCollection = value; }
            }

            internal MyUnavailableCollection AsMyUnavailableCollection
            {
                get { return _unavailableCollection; }
                set { _unavailableCollection = value; }
            }
        }

        private class MyOtherEntity
        {
        }

        private class MyCollection : List<MyOtherEntity>
        {
        }

        private class MyPrivateCollection : List<MyOtherEntity>
        {
            private MyPrivateCollection()
            {
            }

            public static MyPrivateCollection Create()
            {
                return new MyPrivateCollection();
            }
        }

        private class MyInternalCollection : List<MyOtherEntity>
        {
            // ReSharper disable once EmptyConstructor
            internal MyInternalCollection()
            {
            }
        }

        private class MyUnavailableCollection : List<MyOtherEntity>
        {
            public MyUnavailableCollection(bool _)
            {
            }
        }

        private class MyEnumerable : IEnumerable<MyOtherEntity>
        {
            public IEnumerator<MyOtherEntity> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }
    }
}
