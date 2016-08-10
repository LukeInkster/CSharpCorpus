// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests.ChangeTracking
{
    public class ReferenceEntryTest
    {
        [Fact]
        public void Can_get_back_reference()
        {
            using (var context = new FreezerContext())
            {
                var entity = new Chunky();
                context.Add(entity);

                var entityEntry = context.Entry(entity);
                Assert.Same(entityEntry.Entity, entityEntry.Reference("Garcia").EntityEntry.Entity);
            }
        }

        [Fact]
        public void Can_get_back_reference_generic()
        {
            using (var context = new FreezerContext())
            {
                var entity = new Chunky();
                context.Add(entity);

                var entityEntry = context.Entry(entity);
                Assert.Same(entityEntry.Entity, entityEntry.Reference(e => e.Garcia).EntityEntry.Entity);
            }
        }

        [Fact]
        public void Can_get_metadata()
        {
            using (var context = new FreezerContext())
            {
                var entity = new Chunky();
                context.Add(entity);

                Assert.Equal("Garcia", context.Entry(entity).Reference("Garcia").Metadata.Name);
            }
        }

        [Fact]
        public void Can_get_metadata_generic()
        {
            using (var context = new FreezerContext())
            {
                var entity = new Chunky();
                context.Add(entity);

                Assert.Equal("Garcia", context.Entry(entity).Reference(e => e.Garcia).Metadata.Name);
            }
        }

        [Fact]
        public void Can_get_and_set_current_value()
        {
            using (var context = new FreezerContext())
            {
                var cherry = new Cherry();
                var chunky = new Chunky();
                context.AddRange(chunky, cherry);

                var reference = context.Entry(chunky).Reference("Garcia");

                Assert.Null(reference.CurrentValue);

                reference.CurrentValue = cherry;

                Assert.Same(cherry, chunky.Garcia);
                Assert.Same(chunky, cherry.Monkeys.Single());
                Assert.Equal(cherry.Id, chunky.GarciaId);
                Assert.Same(cherry, reference.CurrentValue);

                reference.CurrentValue = null;

                Assert.Null(chunky.Garcia);
                Assert.Empty(cherry.Monkeys);
                Assert.Null(chunky.GarciaId);
                Assert.Null(reference.CurrentValue);
            }
        }

        [Fact]
        public void Can_get_and_set_current_value_generic()
        {
            using (var context = new FreezerContext())
            {
                var cherry = new Cherry();
                var chunky = new Chunky();
                context.AddRange(chunky, cherry);

                var reference = context.Entry(chunky).Reference(e => e.Garcia);

                Assert.Null(reference.CurrentValue);

                reference.CurrentValue = cherry;

                Assert.Same(cherry, chunky.Garcia);
                Assert.Same(chunky, cherry.Monkeys.Single());
                Assert.Equal(cherry.Id, chunky.GarciaId);
                Assert.Same(cherry, reference.CurrentValue);

                reference.CurrentValue = null;

                Assert.Null(chunky.Garcia);
                Assert.Empty(cherry.Monkeys);
                Assert.Null(chunky.GarciaId);
                Assert.Null(reference.CurrentValue);
            }
        }

        [Fact]
        public void Can_get_and_set_current_value_not_tracked()
        {
            using (var context = new FreezerContext())
            {
                var cherry = new Cherry();
                var chunky = new Chunky();

                var reference = context.Entry(chunky).Reference("Garcia");

                Assert.Null(reference.CurrentValue);

                reference.CurrentValue = cherry;

                Assert.Same(cherry, chunky.Garcia);
                Assert.Null(cherry.Monkeys);
                Assert.Null(chunky.GarciaId);
                Assert.Same(cherry, reference.CurrentValue);

                reference.CurrentValue = null;

                Assert.Null(chunky.Garcia);
                Assert.Null(cherry.Monkeys);
                Assert.Null(chunky.GarciaId);
                Assert.Null(reference.CurrentValue);
            }
        }

        [Fact]
        public void Can_get_and_set_current_value_not_tracked_generic()
        {
            using (var context = new FreezerContext())
            {
                var cherry = new Cherry();
                var chunky = new Chunky();

                var reference = context.Entry(chunky).Reference(e => e.Garcia);

                Assert.Null(reference.CurrentValue);

                reference.CurrentValue = cherry;

                Assert.Same(cherry, chunky.Garcia);
                Assert.Null(cherry.Monkeys);
                Assert.Null(chunky.GarciaId);
                Assert.Same(cherry, reference.CurrentValue);

                reference.CurrentValue = null;

                Assert.Null(chunky.Garcia);
                Assert.Null(cherry.Monkeys);
                Assert.Null(chunky.GarciaId);
                Assert.Null(reference.CurrentValue);
            }
        }

        [Fact]
        public void Can_get_and_set_current_value_start_tracking()
        {
            using (var context = new FreezerContext())
            {
                var cherry = new Cherry();
                var chunky = new Chunky();
                context.Add(chunky);

                var reference = context.Entry(chunky).Reference("Garcia");

                Assert.Null(reference.CurrentValue);

                reference.CurrentValue = cherry;

                Assert.Same(cherry, chunky.Garcia);
                Assert.Same(chunky, cherry.Monkeys.Single());
                Assert.Equal(cherry.Id, chunky.GarciaId);
                Assert.Same(cherry, reference.CurrentValue);

                Assert.Equal(EntityState.Added, context.Entry(cherry).State);
                Assert.Equal(EntityState.Added, context.Entry(chunky).State);

                reference.CurrentValue = null;

                Assert.Null(chunky.Garcia);
                Assert.Empty(cherry.Monkeys);
                Assert.Null(chunky.GarciaId);
                Assert.Null(reference.CurrentValue);

                Assert.Equal(EntityState.Added, context.Entry(cherry).State);
                Assert.Equal(EntityState.Added, context.Entry(chunky).State);
            }
        }

        [Fact]
        public void Can_get_and_set_current_value_start_tracking_generic()
        {
            using (var context = new FreezerContext())
            {
                var cherry = new Cherry();
                var chunky = new Chunky();
                context.Add(chunky);

                var reference = context.Entry(chunky).Reference(e => e.Garcia);

                Assert.Null(reference.CurrentValue);

                reference.CurrentValue = cherry;

                Assert.Same(cherry, chunky.Garcia);
                Assert.Same(chunky, cherry.Monkeys.Single());
                Assert.Equal(cherry.Id, chunky.GarciaId);
                Assert.Same(cherry, reference.CurrentValue);

                Assert.Equal(EntityState.Added, context.Entry(cherry).State);
                Assert.Equal(EntityState.Added, context.Entry(chunky).State);

                reference.CurrentValue = null;

                Assert.Null(chunky.Garcia);
                Assert.Empty(cherry.Monkeys);
                Assert.Null(chunky.GarciaId);
                Assert.Null(reference.CurrentValue);

                Assert.Equal(EntityState.Added, context.Entry(cherry).State);
                Assert.Equal(EntityState.Added, context.Entry(chunky).State);
            }
        }

        [Fact]
        public void Can_get_and_set_current_value_attached()
        {
            using (var context = new FreezerContext())
            {
                var cherry = new Cherry();
                var chunky = new Chunky();
                context.AttachRange(chunky, cherry);

                var reference = context.Entry(chunky).Reference("Garcia");

                Assert.Null(reference.CurrentValue);

                reference.CurrentValue = cherry;

                Assert.Same(cherry, chunky.Garcia);
                Assert.Same(chunky, cherry.Monkeys.Single());
                Assert.Equal(cherry.Id, chunky.GarciaId);
                Assert.Same(cherry, reference.CurrentValue);

                Assert.Equal(EntityState.Unchanged, context.Entry(cherry).State);
                Assert.Equal(EntityState.Modified, context.Entry(chunky).State);
                Assert.True(context.Entry(chunky).Property(e => e.GarciaId).IsModified);

                reference.CurrentValue = null;

                Assert.Null(chunky.Garcia);
                Assert.Empty(cherry.Monkeys);
                Assert.Null(chunky.GarciaId);
                Assert.Null(reference.CurrentValue);

                Assert.Equal(EntityState.Unchanged, context.Entry(cherry).State);
                Assert.Equal(EntityState.Modified, context.Entry(chunky).State);
                Assert.True(context.Entry(chunky).Property(e => e.GarciaId).IsModified);
            }
        }

        [Fact]
        public void Can_get_and_set_current_value_generic_attached()
        {
            using (var context = new FreezerContext())
            {
                var cherry = new Cherry();
                var chunky = new Chunky();
                context.AttachRange(chunky, cherry);

                var reference = context.Entry(chunky).Reference(e => e.Garcia);

                Assert.Null(reference.CurrentValue);

                reference.CurrentValue = cherry;

                Assert.Same(cherry, chunky.Garcia);
                Assert.Same(chunky, cherry.Monkeys.Single());
                Assert.Equal(cherry.Id, chunky.GarciaId);
                Assert.Same(cherry, reference.CurrentValue);

                Assert.Equal(EntityState.Unchanged, context.Entry(cherry).State);
                Assert.Equal(EntityState.Modified, context.Entry(chunky).State);
                Assert.True(context.Entry(chunky).Property(e => e.GarciaId).IsModified);

                reference.CurrentValue = null;

                Assert.Null(chunky.Garcia);
                Assert.Empty(cherry.Monkeys);
                Assert.Null(chunky.GarciaId);
                Assert.Null(reference.CurrentValue);

                Assert.Equal(EntityState.Unchanged, context.Entry(cherry).State);
                Assert.Equal(EntityState.Modified, context.Entry(chunky).State);
                Assert.True(context.Entry(chunky).Property(e => e.GarciaId).IsModified);
            }
        }

        [Fact]
        public void IsModified_tracks_state_of_FK_property()
        {
            using (var context = new FreezerContext())
            {
                var cherry = new Cherry();
                var chunky = new Chunky { Garcia = cherry };
                cherry.Monkeys = new List<Chunky> { chunky };
                context.AttachRange(cherry, chunky);

                var reference = context.Entry(chunky).Reference(e => e.Garcia);

                Assert.False(reference.IsModified);

                chunky.GarciaId = null;
                context.ChangeTracker.DetectChanges();

                Assert.True(reference.IsModified);

                context.Entry(chunky).State = EntityState.Unchanged;

                Assert.False(reference.IsModified);
            }
        }

        [Fact]
        public void IsModified_can_set_fk_to_modified()
        {
            using (var context = new FreezerContext())
            {
                var cherry = new Cherry();
                var chunky = new Chunky { Garcia = cherry };
                cherry.Monkeys = new List<Chunky> { chunky };
                context.AttachRange(cherry, chunky);

                var entityEntry = context.Entry(chunky);
                var reference = entityEntry.Reference(e => e.Garcia);

                Assert.False(reference.IsModified);

                reference.IsModified = true;

                Assert.True(reference.IsModified);
                Assert.True(entityEntry.Property(e => e.GarciaId).IsModified);

                reference.IsModified = false;

                Assert.False(reference.IsModified);
                Assert.False(entityEntry.Property(e => e.GarciaId).IsModified);
                Assert.Equal(EntityState.Unchanged, entityEntry.State);
            }
        }

        [Fact]
        public void IsModified_tracks_state_of_FK_property_principal()
        {
            using (var context = new FreezerContext())
            {
                var half = new Half();
                var chunky = new Chunky { Baked = half };
                half.Monkey =chunky;
                context.AttachRange(chunky, half);

                var reference = context.Entry(chunky).Reference(e => e.Baked);

                Assert.False(reference.IsModified);

                context.Entry(half).State = EntityState.Modified;

                Assert.True(reference.IsModified);

                context.Entry(half).State = EntityState.Unchanged;

                Assert.False(reference.IsModified);
            }
        }

        [Fact]
        public void IsModified_can_set_fk_to_modified_principal()
        {
            using (var context = new FreezerContext())
            {
                var half = new Half();
                var chunky = new Chunky { Baked = half };
                half.Monkey = chunky;
                context.AttachRange(chunky, half);

                var reference = context.Entry(chunky).Reference(e => e.Baked);

                Assert.False(reference.IsModified);

                reference.IsModified = true;

                Assert.True(reference.IsModified);
                Assert.True(context.Entry(half).Property(e => e.MonkeyId).IsModified);

                reference.IsModified = false;

                Assert.False(reference.IsModified);
                Assert.False(context.Entry(half).Property(e => e.MonkeyId).IsModified);
                Assert.Equal(EntityState.Unchanged, context.Entry(half).State);
            }
        }

        private class Chunky
        {
            public int Monkey { get; set; }
            public int Id { get; set; }

            public int? GarciaId { get; set; }
            public Cherry Garcia { get; set; }

            public Half Baked { get; set; }
        }

        private class Half
        {
            public int Baked { get; set; }
            public int Id { get; set; }

            public int? MonkeyId { get; set; }
            public Chunky Monkey { get; set; }
        }

        private class Cherry
        {
            public int Garcia { get; set; }
            public int Id { get; set; }

            public ICollection<Chunky> Monkeys { get; set; }
        }

        private class FreezerContext : DbContext
        {
            protected internal override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder.UseInMemoryDatabase();

            public DbSet<Chunky> Icecream { get; set; }
        }
    }
}
