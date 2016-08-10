// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.EntityFrameworkCore.InMemory.FunctionalTests
{
    public class NamedDatabaseTest
    {
        [Fact]
        public void Database_per_app_domain_is_default_with_internal_service_provider()
        {
            using (var context = new PusheenContext())
            {
                context.Add(new Pusheen { Activity = "In a box" });
                context.SaveChanges();
            }

            using (var context = new PusheenContext())
            {
                Assert.Equal("In a box", context.Pusheens.Single().Activity);
            }
        }

        [Fact]
        public void Database_per_service_provider_is_default()
        {
            var provider1 = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
            var provider2 = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();

            using (var context = new PusheenContext(provider1))
            {
                context.Add(new Pusheen { Activity = "In a box" });
                context.SaveChanges();
            }

            using (var context = new PusheenContext(provider2))
            {
                Assert.Empty(context.Pusheens);

                context.Add(new Pusheen { Activity = "With some yarn" });
                context.SaveChanges();
            }

            using (var context = new PusheenContext(provider1))
            {
                Assert.Equal("In a box", context.Pusheens.Single().Activity);
            }

            using (var context = new PusheenContext(provider2))
            {
                Assert.Equal("With some yarn", context.Pusheens.Single().Activity);
            }
        }

        [Fact]
        public void Named_databases_shared_per_app_domain_with_internal_service_provider()
        {
            using (var context = new PusheenContext("Cats"))
            {
                context.Add(new Pusheen { Activity = "In a box" });
                context.SaveChanges();
            }

            using (var context = new PusheenContext("Plump"))
            {
                Assert.Empty(context.Pusheens);

                context.Add(new Pusheen { Activity = "With some yarn" });
                context.SaveChanges();
            }

            using (var context = new PusheenContext("Cats"))
            {
                Assert.Equal("In a box", context.Pusheens.Single().Activity);
            }

            using (var context = new PusheenContext("Plump"))
            {
                Assert.Equal("With some yarn", context.Pusheens.Single().Activity);
            }
        }

        [Fact]
        public void Named_databases_shared_per_service_provider()
        {
            var provider1 = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();
            var provider2 = new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();

            using (var context = new PusheenContext("Cats", provider1))
            {
                context.Add(new Pusheen { Activity = "In a box" });
                context.SaveChanges();
            }

            using (var context = new PusheenContext("Plump", provider1))
            {
                Assert.Empty(context.Pusheens);

                context.Add(new Pusheen { Activity = "With some yarn" });
                context.SaveChanges();
            }

            using (var context = new PusheenContext(provider1))
            {
                Assert.Empty(context.Pusheens);

                context.Add(new Pusheen { Activity = "On a scooter" });
                context.SaveChanges();
            }

            using (var context = new PusheenContext("Cats", provider2))
            {
                Assert.Empty(context.Pusheens);

                context.Add(new Pusheen { Activity = "Is a DJ" });
                context.SaveChanges();
            }

            using (var context = new PusheenContext("Plump", provider2))
            {
                Assert.Empty(context.Pusheens);

                context.Add(new Pusheen { Activity = "Goes to sleep" });
                context.SaveChanges();
            }

            using (var context = new PusheenContext(provider2))
            {
                Assert.Empty(context.Pusheens);

                context.Add(new Pusheen { Activity = "Loves magic unicorns" });
                context.SaveChanges();
            }

            using (var context = new PusheenContext("Cats", provider1))
            {
                Assert.Equal("In a box", context.Pusheens.Single().Activity);
            }

            using (var context = new PusheenContext("Plump", provider1))
            {
                Assert.Equal("With some yarn", context.Pusheens.Single().Activity);
            }

            using (var context = new PusheenContext(provider1))
            {
                Assert.Equal("On a scooter", context.Pusheens.Single().Activity);
            }

            using (var context = new PusheenContext("Cats", provider2))
            {
                Assert.Equal("Is a DJ", context.Pusheens.Single().Activity);
            }

            using (var context = new PusheenContext("Plump", provider2))
            {
                Assert.Equal("Goes to sleep", context.Pusheens.Single().Activity);
            }

            using (var context = new PusheenContext(provider2))
            {
                Assert.Equal("Loves magic unicorns", context.Pusheens.Single().Activity);
            }
        }

        private class PusheenContext : DbContext
        {
            private readonly string _databaseName;
            private readonly IServiceProvider _serviceProvider;

            public PusheenContext(IServiceProvider serviceProvider = null)
                : this(null, serviceProvider)
            {
            }

            public PusheenContext(string databaseName, IServiceProvider serviceProvider = null)
            {
                _databaseName = databaseName;
                _serviceProvider = serviceProvider;
            }

            public DbSet<Pusheen> Pusheens { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder) => optionsBuilder
                .UseInternalServiceProvider(_serviceProvider)
                .UseInMemoryDatabase(_databaseName);
        }

        private class Pusheen
        {
            public int Id { get; set; }
            public string Activity { get; set; }
        }
    }
}
