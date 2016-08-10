// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.Specification.Tests.TestUtilities.Xunit;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Specification.Tests
{
    public abstract class AsTrackingTestBase<TFixture> : IClassFixture<TFixture>
        where TFixture : NorthwindQueryFixtureBase, new()
    {
        [Fact]
        public virtual void Entity_added_to_state_manager()
        {
            using (var context = CreateContext())
            {
                var customers = context.Set<Customer>().AsTracking().ToList();

                Assert.Equal(91, customers.Count);
                Assert.Equal(91, context.ChangeTracker.Entries().Count());
            }
        }

        [ConditionalFact]
        public virtual void Applied_to_body_clause()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c in context.Set<Customer>()
                       join o in context.Set<Order>().AsTracking()
                           on c.CustomerID equals o.CustomerID
                       where c.CustomerID == "ALFKI"
                       select o)
                        .ToList();

                Assert.Equal(6, customers.Count);
                Assert.Equal(6, context.ChangeTracker.Entries().Count());
            }
        }

        [Fact]
        public virtual void Applied_to_multiple_body_clauses()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c in context.Set<Customer>().AsTracking()
                       from o in context.Set<Order>().AsTracking()
                       where c.CustomerID == o.CustomerID
                       select new { c, o })
                        .ToList();

                Assert.Equal(830, customers.Count);
                Assert.Equal(919, context.ChangeTracker.Entries().Count());
            }
        }

        [ConditionalFact]
        public virtual void Applied_to_body_clause_with_projection()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c in context.Set<Customer>()
                       join o in context.Set<Order>().AsTracking()
                           on c.CustomerID equals o.CustomerID
                       where c.CustomerID == "ALFKI"
                       select new { c.CustomerID, c, ocid = o.CustomerID, o })
                        .ToList();

                Assert.Equal(6, customers.Count);
                Assert.Equal(7, context.ChangeTracker.Entries().Count());
            }
        }

        [ConditionalFact]
        public virtual void Applied_to_projection()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c in context.Set<Customer>()
                       join o in context.Set<Order>().AsTracking()
                           on c.CustomerID equals o.CustomerID
                       where c.CustomerID == "ALFKI"
                       select new { c, o })
                        .AsTracking()
                        .ToList();

                Assert.Equal(6, customers.Count);
                Assert.Equal(7, context.ChangeTracker.Entries().Count());
            }
        }

        protected NorthwindContext CreateContext()
        {
            var context = Fixture.CreateContext();

            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            return context;
        }

        protected AsTrackingTestBase(TFixture fixture)
        {
            Fixture = fixture;
        }

        protected TFixture Fixture { get; }
    }
}
