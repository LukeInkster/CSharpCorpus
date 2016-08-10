// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Specification.Tests;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public class DbSetTest
    {
        [Fact]
        public async Task Can_add_existing_entities_to_context_to_be_deleted()
        {
            await TrackEntitiesTest((c, e) => c.Remove(e), (c, e) => c.Remove(e), EntityState.Deleted);
        }

        [Fact]
        public async Task Can_add_new_entities_to_context_graph()
        {
            await TrackEntitiesTest((c, e) => c.Add(e), (c, e) => c.Add(e), EntityState.Added);
        }

        [Fact]
        public async Task Can_add_new_entities_to_context_graph_async()
        {
            await TrackEntitiesTest((c, e) => c.AddAsync(e), (c, e) => c.AddAsync(e), EntityState.Added);
        }

        [Fact]
        public async Task Can_add_existing_entities_to_context_to_be_attached_graph()
        {
            await TrackEntitiesTest((c, e) => c.Attach(e), (c, e) => c.Attach(e), EntityState.Unchanged);
        }

        [Fact]
        public async Task Can_add_existing_entities_to_context_to_be_updated_graph()
        {
            await TrackEntitiesTest((c, e) => c.Update(e), (c, e) => c.Update(e), EntityState.Modified);
        }

        private static Task TrackEntitiesTest(
            Func<DbSet<Category>, Category, EntityEntry<Category>> categoryAdder,
            Func<DbSet<Product>, Product, EntityEntry<Product>> productAdder, EntityState expectedState)
            => TrackEntitiesTest(
                (c, e) => Task.FromResult(categoryAdder(c, e)),
                (c, e) => Task.FromResult(productAdder(c, e)),
                expectedState);

        private static async Task TrackEntitiesTest(
            Func<DbSet<Category>, Category, Task<EntityEntry<Category>>> categoryAdder,
            Func<DbSet<Product>, Product, Task<EntityEntry<Product>>> productAdder, EntityState expectedState)
        {
            using (var context = new EarlyLearningCenter())
            {
                var category1 = new Category { Id = 1, Name = "Beverages" };
                var category2 = new Category { Id = 2, Name = "Foods" };
                var product1 = new Product { Id = 1, Name = "Marmite", Price = 7.99m };
                var product2 = new Product { Id = 2, Name = "Bovril", Price = 4.99m };

                var categoryEntry1 = await categoryAdder(context.Categories, category1);
                var categoryEntry2 = await categoryAdder(context.Categories, category2);
                var productEntry1 = await productAdder(context.Products, product1);
                var productEntry2 = await productAdder(context.Products, product2);

                Assert.Same(category1, categoryEntry1.Entity);
                Assert.Same(category2, categoryEntry2.Entity);
                Assert.Same(product1, productEntry1.Entity);
                Assert.Same(product2, productEntry2.Entity);

                Assert.Same(category1, categoryEntry1.Entity);
                Assert.Equal(expectedState, categoryEntry2.State);
                Assert.Same(category2, categoryEntry2.Entity);
                Assert.Equal(expectedState, categoryEntry2.State);

                Assert.Same(product1, productEntry1.Entity);
                Assert.Equal(expectedState, productEntry1.State);
                Assert.Same(product2, productEntry2.Entity);
                Assert.Equal(expectedState, productEntry2.State);

                Assert.Same(categoryEntry1.GetInfrastructure(), context.Entry(category1).GetInfrastructure());
                Assert.Same(categoryEntry2.GetInfrastructure(), context.Entry(category2).GetInfrastructure());
                Assert.Same(productEntry1.GetInfrastructure(), context.Entry(product1).GetInfrastructure());
                Assert.Same(productEntry2.GetInfrastructure(), context.Entry(product2).GetInfrastructure());
            }
        }

        [Fact]
        public async Task Can_add_multiple_new_entities_to_set()
        {
            await TrackMultipleEntitiesTest(
                (c, e) => c.Categories.AddRange(e[0], e[1]), 
                (c, e) => c.Products.AddRange(e[0], e[1]), 
                EntityState.Added);
        }

        [Fact]
        public async Task Can_add_multiple_new_entities_to_set_async()
        {
            await TrackMultipleEntitiesTest(
                (c, e) => c.Categories.AddRangeAsync(e[0], e[1]),
                (c, e) => c.Products.AddRangeAsync(e[0], e[1]),
                EntityState.Added);
        }

        [Fact]
        public async Task Can_add_multiple_existing_entities_to_set_to_be_attached()
        {
            await TrackMultipleEntitiesTest(
                (c, e) => c.Categories.AttachRange(e[0], e[1]), 
                (c, e) => c.Products.AttachRange(e[0], e[1]), 
                EntityState.Unchanged);
        }

        [Fact]
        public async Task Can_add_multiple_existing_entities_to_set_to_be_updated()
        {
            await TrackMultipleEntitiesTest(
                (c, e) => c.Categories.UpdateRange(e[0], e[1]), 
                (c, e) => c.Products.UpdateRange(e[0], e[1]), 
                EntityState.Modified);
        }

        [Fact]
        public async Task Can_add_multiple_existing_entities_to_set_to_be_deleted()
        {
            await TrackMultipleEntitiesTest(
                (c, e) => c.Categories.RemoveRange(e[0], e[1]), 
                (c, e) => c.Products.RemoveRange(e[0], e[1]), 
                EntityState.Deleted);
        }

        private static Task TrackMultipleEntitiesTest(
            Action<EarlyLearningCenter, Category[]> categoryAdder,
            Action<EarlyLearningCenter, Product[]> productAdder, EntityState expectedState)
            => TrackMultipleEntitiesTest(
                (c, e) =>
                {
                    categoryAdder(c, e);
                    return Task.FromResult(0);
                },
                (c, e) =>
                {
                    productAdder(c, e);
                    return Task.FromResult(0);
                },
                expectedState);

        private static async Task TrackMultipleEntitiesTest(
            Func<EarlyLearningCenter, Category[], Task> categoryAdder,
            Func<EarlyLearningCenter, Product[], Task> productAdder, EntityState expectedState)
        {
            using (var context = new EarlyLearningCenter())
            {
                var category1 = new Category { Id = 1, Name = "Beverages" };
                var category2 = new Category { Id = 2, Name = "Foods" };
                var product1 = new Product { Id = 1, Name = "Marmite", Price = 7.99m };
                var product2 = new Product { Id = 2, Name = "Bovril", Price = 4.99m };

                await categoryAdder(context, new[] { category1, category2 });
                await productAdder(context, new[] { product1, product2 });

                Assert.Same(category1, context.Entry(category1).Entity);
                Assert.Same(category2, context.Entry(category2).Entity);
                Assert.Same(product1, context.Entry(product1).Entity);
                Assert.Same(product2, context.Entry(product2).Entity);

                Assert.Same(category1, context.Entry(category1).Entity);
                Assert.Equal(expectedState, context.Entry(category1).State);
                Assert.Same(category2, context.Entry(category2).Entity);
                Assert.Equal(expectedState, context.Entry(category2).State);

                Assert.Same(product1, context.Entry(product1).Entity);
                Assert.Equal(expectedState, context.Entry(product1).State);
                Assert.Same(product2, context.Entry(product2).Entity);
                Assert.Equal(expectedState, context.Entry(product2).State);
            }
        }

        [Fact]
        public void Can_add_no_new_entities_to_set()
        {
            TrackNoEntitiesTest(c => c.Categories.AddRange(), c => c.Products.AddRange());
        }

        [Fact]
        public async Task Can_add_no_new_entities_to_set_async()
        {
            using (var context = new EarlyLearningCenter())
            {
                await context.Categories.AddRangeAsync();
                await context.Products.AddRangeAsync();
                Assert.Empty(context.ChangeTracker.Entries());
            }
        }

        [Fact]
        public void Can_add_no_existing_entities_to_set_to_be_attached()
        {
            TrackNoEntitiesTest(c => c.Categories.AttachRange(), c => c.Products.AttachRange());
        }

        [Fact]
        public void Can_add_no_existing_entities_to_set_to_be_updated()
        {
            TrackNoEntitiesTest(c => c.Categories.UpdateRange(), c => c.Products.UpdateRange());
        }

        [Fact]
        public void Can_add_no_existing_entities_to_set_to_be_deleted()
        {
            TrackNoEntitiesTest(c => c.Categories.RemoveRange(), c => c.Products.RemoveRange());
        }

        private static void TrackNoEntitiesTest(Action<EarlyLearningCenter> categoryAdder, Action<EarlyLearningCenter> productAdder)
        {
            using (var context = new EarlyLearningCenter())
            {
                categoryAdder(context);
                productAdder(context);
                Assert.Empty(context.ChangeTracker.Entries());
            }
        }

        [Fact]
        public async Task Can_add_multiple_existing_entities_to_set_to_be_deleted_Enumerable()
        {
            await TrackMultipleEntitiesTestEnumerable(
                (c, e) => c.Categories.RemoveRange(e), 
                (c, e) => c.Products.RemoveRange(e), 
                EntityState.Deleted);
        }

        [Fact]
        public async Task Can_add_multiple_new_entities_to_set_Enumerable_graph()
        {
            await TrackMultipleEntitiesTestEnumerable(
                (c, e) => c.Categories.AddRange(e), 
                (c, e) => c.Products.AddRange(e), 
                EntityState.Added);
        }

        [Fact]
        public async Task Can_add_multiple_new_entities_to_set_Enumerable_graph_async()
        {
            await TrackMultipleEntitiesTestEnumerable(
                (c, e) => c.Categories.AddRangeAsync(e),
                (c, e) => c.Products.AddRangeAsync(e),
                EntityState.Added);
        }

        [Fact]
        public async Task Can_add_multiple_existing_entities_to_set_to_be_attached_Enumerable_graph()
        {
            await TrackMultipleEntitiesTestEnumerable(
                (c, e) => c.Categories.AttachRange(e), 
                (c, e) => c.Products.AttachRange(e), 
                EntityState.Unchanged);
        }

        [Fact]
        public async Task Can_add_multiple_existing_entities_to_set_to_be_updated_Enumerable_graph()
        {
            await TrackMultipleEntitiesTestEnumerable(
                (c, e) => c.Categories.UpdateRange(e), 
                (c, e) => c.Products.UpdateRange(e), 
                EntityState.Modified);
        }

        private static Task TrackMultipleEntitiesTestEnumerable(
            Action<EarlyLearningCenter, IEnumerable<Category>> categoryAdder,
            Action<EarlyLearningCenter, IEnumerable<Product>> productAdder, EntityState expectedState)
            => TrackMultipleEntitiesTestEnumerable(
                (c, e) =>
                {
                    categoryAdder(c, e);
                    return Task.FromResult(0);
                },
                (c, e) =>
                {
                    productAdder(c, e);
                    return Task.FromResult(0);
                },
                expectedState);

        private static async Task TrackMultipleEntitiesTestEnumerable(
            Func<EarlyLearningCenter, IEnumerable<Category>, Task> categoryAdder,
            Func<EarlyLearningCenter, IEnumerable<Product>, Task> productAdder, EntityState expectedState)
        {
            using (var context = new EarlyLearningCenter())
            {
                var category1 = new Category { Id = 1, Name = "Beverages" };
                var category2 = new Category { Id = 2, Name = "Foods" };
                var product1 = new Product { Id = 1, Name = "Marmite", Price = 7.99m };
                var product2 = new Product { Id = 2, Name = "Bovril", Price = 4.99m };

                await categoryAdder(context, new List<Category> { category1, category2 });
                await productAdder(context, new List<Product> { product1, product2 });

                Assert.Same(category1, context.Entry(category1).Entity);
                Assert.Same(category2, context.Entry(category2).Entity);
                Assert.Same(product1, context.Entry(product1).Entity);
                Assert.Same(product2, context.Entry(product2).Entity);

                Assert.Same(category1, context.Entry(category1).Entity);
                Assert.Equal(expectedState, context.Entry(category1).State);
                Assert.Same(category2, context.Entry(category2).Entity);
                Assert.Equal(expectedState, context.Entry(category2).State);

                Assert.Same(product1, context.Entry(product1).Entity);
                Assert.Equal(expectedState, context.Entry(product1).State);
                Assert.Same(product2, context.Entry(product2).Entity);
                Assert.Equal(expectedState, context.Entry(product2).State);
            }
        }

        [Fact]
        public void Can_add_no_existing_entities_to_set_to_be_deleted_Enumerable()
        {
            TrackNoEntitiesTestEnumerable((c, e) => c.Categories.RemoveRange(e), (c, e) => c.Products.RemoveRange(e));
        }

        [Fact]
        public void Can_add_no_new_entities_to_set_Enumerable_graph()
        {
            TrackNoEntitiesTestEnumerable((c, e) => c.Categories.AddRange(e), (c, e) => c.Products.AddRange(e));
        }

        [Fact]
        public async Task Can_add_no_new_entities_to_set_Enumerable_graph_async()
        {
            using (var context = new EarlyLearningCenter())
            {
                await context.Categories.AddRangeAsync(new HashSet<Category>());
                await context.Products.AddRangeAsync(new HashSet<Product>());
                Assert.Empty(context.ChangeTracker.Entries());
            }
        }

        [Fact]
        public void Can_add_no_existing_entities_to_set_to_be_attached_Enumerable_graph()
        {
            TrackNoEntitiesTestEnumerable((c, e) => c.Categories.AttachRange(e), (c, e) => c.Products.AttachRange(e));
        }

        [Fact]
        public void Can_add_no_existing_entities_to_set_to_be_updated_Enumerable_graph()
        {
            TrackNoEntitiesTestEnumerable((c, e) => c.Categories.UpdateRange(e), (c, e) => c.Products.UpdateRange(e));
        }

        private static void TrackNoEntitiesTestEnumerable(
            Action<EarlyLearningCenter, IEnumerable<Category>> categoryAdder,
            Action<EarlyLearningCenter, IEnumerable<Product>> productAdder)
        {
            using (var context = new EarlyLearningCenter())
            {
                categoryAdder(context, new HashSet<Category>());
                productAdder(context, new HashSet<Product>());
                Assert.Empty(context.ChangeTracker.Entries());
            }
        }

        [Fact]
        public async Task Can_use_Add_to_change_entity_state()
        {
            await ChangeStateWithMethod((c, e) => c.Categories.Add(e), EntityState.Detached, EntityState.Added);
            await ChangeStateWithMethod((c, e) => c.Categories.Add(e), EntityState.Unchanged, EntityState.Added);
            await ChangeStateWithMethod((c, e) => c.Categories.Add(e), EntityState.Deleted, EntityState.Added);
            await ChangeStateWithMethod((c, e) => c.Categories.Add(e), EntityState.Modified, EntityState.Added);
            await ChangeStateWithMethod((c, e) => c.Categories.Add(e), EntityState.Added, EntityState.Added);
        }

        [Fact]
        public async Task Can_use_Add_to_change_entity_state_async()
        {
            await ChangeStateWithMethod((c, e) => c.Categories.AddAsync(e), EntityState.Detached, EntityState.Added);
            await ChangeStateWithMethod((c, e) => c.Categories.AddAsync(e), EntityState.Unchanged, EntityState.Added);
            await ChangeStateWithMethod((c, e) => c.Categories.AddAsync(e), EntityState.Deleted, EntityState.Added);
            await ChangeStateWithMethod((c, e) => c.Categories.AddAsync(e), EntityState.Modified, EntityState.Added);
            await ChangeStateWithMethod((c, e) => c.Categories.AddAsync(e), EntityState.Added, EntityState.Added);
        }

        [Fact]
        public async Task Can_use_Attach_to_change_entity_state()
        {
            await ChangeStateWithMethod((c, e) => c.Categories.Attach(e), EntityState.Detached, EntityState.Unchanged);
            await ChangeStateWithMethod((c, e) => c.Categories.Attach(e), EntityState.Unchanged, EntityState.Unchanged);
            await ChangeStateWithMethod((c, e) => c.Categories.Attach(e), EntityState.Deleted, EntityState.Unchanged);
            await ChangeStateWithMethod((c, e) => c.Categories.Attach(e), EntityState.Modified, EntityState.Unchanged);
            await ChangeStateWithMethod((c, e) => c.Categories.Attach(e), EntityState.Added, EntityState.Unchanged);
        }

        [Fact]
        public async Task Can_use_Update_to_change_entity_state()
        {
            await ChangeStateWithMethod((c, e) => c.Categories.Update(e), EntityState.Detached, EntityState.Modified);
            await ChangeStateWithMethod((c, e) => c.Categories.Update(e), EntityState.Unchanged, EntityState.Modified);
            await ChangeStateWithMethod((c, e) => c.Categories.Update(e), EntityState.Deleted, EntityState.Modified);
            await ChangeStateWithMethod((c, e) => c.Categories.Update(e), EntityState.Modified, EntityState.Modified);
            await ChangeStateWithMethod((c, e) => c.Categories.Update(e), EntityState.Added, EntityState.Modified);
        }

        [Fact]
        public async Task Can_use_Remove_to_change_entity_state()
        {
            await ChangeStateWithMethod((c, e) => c.Categories.Remove(e), EntityState.Detached, EntityState.Deleted);
            await ChangeStateWithMethod((c, e) => c.Categories.Remove(e), EntityState.Unchanged, EntityState.Deleted);
            await ChangeStateWithMethod((c, e) => c.Categories.Remove(e), EntityState.Deleted, EntityState.Deleted);
            await ChangeStateWithMethod((c, e) => c.Categories.Remove(e), EntityState.Modified, EntityState.Deleted);
            await ChangeStateWithMethod((c, e) => c.Categories.Remove(e), EntityState.Added, EntityState.Detached);
        }

        private Task ChangeStateWithMethod(
            Action<EarlyLearningCenter, Category> action,
            EntityState initialState,
            EntityState expectedState)
            => ChangeStateWithMethod((c, e) =>
                {
                    action(c, e);
                    return Task.FromResult(0);
                },
                initialState,
                expectedState);

        private async Task ChangeStateWithMethod(
            Func<EarlyLearningCenter, Category, Task> action, 
            EntityState initialState, 
            EntityState expectedState)
        {
            using (var context = new EarlyLearningCenter())
            {
                var entity = new Category { Id = 1, Name = "Beverages" };
                var entry = context.Entry(entity);

                entry.State = initialState;

                await action(context, entity);

                Assert.Equal(expectedState, entry.State);
            }
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Can_add_new_entities_to_context_with_key_generation(bool async)
        {
            using (var context = new EarlyLearningCenter())
            {
                var gu1 = new TheGu { ShirtColor = "Red" };
                var gu2 = new TheGu { ShirtColor = "Still Red" };

                if (async)
                {
                    Assert.Same(gu1, (await context.Gus.AddAsync(gu1)).Entity);
                    Assert.Same(gu2, (await context.Gus.AddAsync(gu2)).Entity);
                }
                else
                {
                    Assert.Same(gu1, context.Gus.Add(gu1).Entity);
                    Assert.Same(gu2, context.Gus.Add(gu2).Entity);
                }

                Assert.NotEqual(default(Guid), gu1.Id);
                Assert.NotEqual(default(Guid), gu2.Id);
                Assert.NotEqual(gu1.Id, gu2.Id);

                var categoryEntry = context.Entry(gu1);
                Assert.Same(gu1, categoryEntry.Entity);
                Assert.Equal(EntityState.Added, categoryEntry.State);

                categoryEntry = context.Entry(gu2);
                Assert.Same(gu2, categoryEntry.Entity);
                Assert.Equal(EntityState.Added, categoryEntry.State);
            }
        }

        [Fact]
        public void Can_get_scoped_service_provider()
        {
            using (var context = new EarlyLearningCenter())
            {
                Assert.Same(
                    ((IInfrastructure<IServiceProvider>)context).Instance,
                    ((IInfrastructure<IServiceProvider>)context.Products).Instance);
            }
        }

#if NET451
        [Fact]
        public void Throws_when_using_with_IListSource()
        {
            using (var context = new EarlyLearningCenter())
            {
                Assert.Equal(CoreStrings.DataBindingWithIListSource,
                    Assert.Throws<NotSupportedException>(() => ((IListSource)context.Gus).GetList()).Message);
            }
        }
#endif

        private class Category
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        private class Product
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public decimal Price { get; set; }
        }

        private class TheGu
        {
            public Guid Id { get; set; }
            public string ShirtColor { get; set; }
        }

        private class EarlyLearningCenter : DbContext
        {
            public DbSet<Product> Products { get; set; }
            public DbSet<Category> Categories { get; set; }
            public DbSet<TheGu> Gus { get; set; }

            protected internal override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder
                    .UseInMemoryDatabase()
                    .UseInternalServiceProvider(TestHelpers.Instance.CreateServiceProvider());
        }
    }
}
