// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.Northwind;
using Microsoft.EntityFrameworkCore.Specification.Tests.TestUtilities.Xunit;
using Xunit;

// ReSharper disable StringStartsWithIsCultureSpecific

#if NETSTANDARD1_3
using System.Reflection;
#endif
namespace Microsoft.EntityFrameworkCore.Specification.Tests
{
    public abstract class IncludeTestBase<TFixture> : IClassFixture<TFixture>
        where TFixture : NorthwindQueryFixtureBase, new()
    {
        protected IncludeTestBase(TFixture fixture)
        {
            Fixture = fixture;
        }

        protected TFixture Fixture { get; }

        protected NorthwindContext CreateContext() => Fixture.CreateContext();

        [Fact]
        public virtual void Include_reference_invalid()
        {
            Assert.Throws<InvalidOperationException>(
                () =>
                    {
                        using (var context = CreateContext())
                        {
                            return context.Set<Order>()
                                .Include(o => o.Customer.CustomerID)
                                .ToList();
                        }
                    });
        }

        [Fact]
        public virtual void Include_property_expression_invalid()
        {
            var anonymousType = new { Customer = default(Customer), OrderDetails = default(ICollection<OrderDetail>) }.GetType();
            var lambdaExpression = Expression.Lambda(
                Expression.New(
                    anonymousType.GetConstructors()[0],
                    new List<Expression>
                    {
                        Expression.MakeMemberAccess(Expression.Parameter(typeof(Order), "o"), typeof(Order).GetMember("Customer")[0]),
                        Expression.MakeMemberAccess(Expression.Parameter(typeof(Order), "o"), typeof(Order).GetMember("OrderDetails")[0])
                    },
                    anonymousType.GetMember("Customer")[0],
                    anonymousType.GetMember("OrderDetails")[0]
                    ),
                Expression.Parameter(typeof(Order), "o"));

            Assert.Equal(
                CoreStrings.InvalidComplexPropertyExpression(lambdaExpression.ToString()),
                Assert.Throws<InvalidOperationException>(
                    () =>
                        {
                            using (var context = CreateContext())
                            {
                                context.Set<Order>()
                                    .Include(o => new { o.Customer, o.OrderDetails })
                                    // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                                    .ToList();
                            }
                        }).Message);
        }

        [Fact]
        public virtual void Then_include_collection_order_by_collection_column()
        {
            using (var context = CreateContext())
            {
                var customer
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .ThenInclude(o => o.OrderDetails)
                        .Where(c => c.CustomerID.StartsWith("W"))
                        .OrderByDescending(c => c.Orders.OrderByDescending(oo => oo.OrderDate).FirstOrDefault().OrderDate)
                        .FirstOrDefault();

                Assert.NotNull(customer);
                Assert.Equal("WHITC", customer.CustomerID);
                Assert.NotNull(customer.Orders);
                Assert.Equal(14, customer.Orders.Count);
                Assert.NotNull(customer.Orders.First().OrderDetails);
                Assert.Equal(2, customer.Orders.First().OrderDetails.Count);
                Assert.NotNull(customer.Orders.Last().OrderDetails);
                Assert.Equal(3, customer.Orders.Last().OrderDetails.Count);

                CheckIsLoaded(
                    context,
                    customer,
                    ordersLoaded: true,
                    orderDetailsLoaded: true,
                    productLoaded: false);
            }
        }

        [Fact]
        public virtual void Then_include_property_expression_invalid()
        {
            var anonymousType = new { Customer = default(Customer), OrderDetails = default(ICollection<OrderDetail>) }.GetType();
            var lambdaExpression = Expression.Lambda(
                Expression.New(
                    anonymousType.GetConstructors()[0],
                    new List<Expression>
                    {
                        Expression.MakeMemberAccess(Expression.Parameter(typeof(Order), "o"), typeof(Order).GetMember("Customer")[0]),
                        Expression.MakeMemberAccess(Expression.Parameter(typeof(Order), "o"), typeof(Order).GetMember("OrderDetails")[0])
                    },
                    anonymousType.GetMember("Customer")[0],
                    anonymousType.GetMember("OrderDetails")[0]
                    ),
                Expression.Parameter(typeof(Order), "o"));

            Assert.Equal(
                CoreStrings.InvalidComplexPropertyExpression(lambdaExpression.ToString()),
                Assert.Throws<ArgumentException>(
                    () =>
                        {
                            using (var context = CreateContext())
                            {
                                context.Set<Customer>()
                                    .Include(o => o.Orders)
                                    .ThenInclude(o => new { o.Customer, o.OrderDetails })
                                    // ReSharper disable once ReturnValueOfPureMethodIsNotUsed
                                    .ToList();
                            }
                        }).Message);
        }

        [Fact]
        public virtual void Include_closes_reader()
        {
            using (var context = CreateContext())
            {
                var customer = context.Set<Customer>().Include(c => c.Orders).FirstOrDefault();
                var products = context.Products.ToList();

                Assert.NotNull(customer);
                Assert.NotNull(products);
            }
        }

        [Fact]
        public virtual void Include_when_result_operator()
        {
            using (var context = CreateContext())
            {
                var any
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .Any();

                Assert.True(any);
            }
        }

        [Fact]
        public virtual void Include_collection()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .ToList();

                Assert.Equal(91, customers.Count);
                Assert.Equal(830, customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).Count());
                Assert.True(customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(91 + 830, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_skip_no_order_by()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .Skip(10)
                        .Include(c => c.Orders)
                        .ToList();

                Assert.Equal(81, customers.Count);
                Assert.True(customers.All(c => c.Orders != null));

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_skip_take_no_order_by()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .Skip(10)
                        .Take(5)
                        .Include(c => c.Orders)
                        .ToList();

                Assert.Equal(5, customers.Count);
                Assert.True(customers.All(c => c.Orders != null));

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_list()
        {
            using (var context = CreateContext())
            {
                var products
                    = context.Set<Product>()
                        .Include(c => c.OrderDetails).ThenInclude(od => od.Order)
                        .ToList();

                Assert.Equal(77, products.Count);

                foreach (var product in products)
                {
                    CheckIsLoaded(
                        context,
                        product,
                        orderDetailsLoaded: true,
                        orderLoaded: true);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_alias_generation()
        {
            using (var context = CreateContext())
            {
                var orders
                    = context.Set<Order>()
                        .Include(o => o.OrderDetails)
                        .ToList();

                Assert.Equal(830, orders.Count);

                foreach (var order in orders)
                {
                    CheckIsLoaded(
                        context,
                        order,
                        orderDetailsLoaded: true,
                        productLoaded: false,
                        customerLoaded: false,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_and_reference()
        {
            using (var context = CreateContext())
            {
                var orders
                    = context.Set<Order>()
                        .Include(o => o.OrderDetails)
                        .Include(o => o.Customer)
                        .ToList();

                Assert.Equal(830, orders.Count);

                foreach (var order in orders)
                {
                    CheckIsLoaded(
                        context,
                        order,
                        orderDetailsLoaded: true,
                        productLoaded: false,
                        customerLoaded: true,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_as_no_tracking()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .AsNoTracking()
                        .ToList();

                Assert.Equal(91, customers.Count);
                Assert.Equal(830, customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).Count());
                Assert.True(customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(0, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: false,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_as_no_tracking2()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .AsNoTracking()
                        .OrderBy(c => c.CustomerID)
                        .Take(5)
                        .Include(c => c.Orders)
                        .ToList();

                Assert.Equal(5, customers.Count);
                Assert.Equal(48, customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).Count());
                Assert.True(customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(0, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: false,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_dependent_already_tracked()
        {
            using (var context = CreateContext())
            {
                var orders
                    = context.Set<Order>()
                        .Where(o => o.CustomerID == "ALFKI")
                        .ToList();

                Assert.Equal(6, context.ChangeTracker.Entries().Count());

                var customer
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .Single(c => c.CustomerID == "ALFKI");

                Assert.Equal(orders, customer.Orders, ReferenceEqualityComparer.Instance);
                Assert.Equal(6, customer.Orders.Count);
                Assert.True(customer.Orders.All(o => o.Customer != null));
                Assert.Equal(6 + 1, context.ChangeTracker.Entries().Count());

                CheckIsLoaded(
                    context,
                    customer,
                    ordersLoaded: true,
                    orderDetailsLoaded: false,
                    productLoaded: false);
            }
        }

        [Fact]
        public virtual void Include_collection_dependent_already_tracked_as_no_tracking()
        {
            using (var context = CreateContext())
            {
                var orders
                    = context.Set<Order>()
                        .Where(o => o.CustomerID == "ALFKI")
                        .ToList();

                Assert.Equal(6, context.ChangeTracker.Entries().Count());

                var customer
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .AsNoTracking()
                        .Single(c => c.CustomerID == "ALFKI");

                Assert.NotEqual(orders, customer.Orders, ReferenceEqualityComparer.Instance);
                Assert.Equal(6, customer.Orders.Count);
                Assert.True(customer.Orders.All(o => o.Customer != null));
                Assert.Equal(6, context.ChangeTracker.Entries().Count());

                CheckIsLoaded(
                    context,
                    customer,
                    ordersLoaded: false,
                    orderDetailsLoaded: false,
                    productLoaded: false);
            }
        }

        [Fact]
        public virtual void Include_collection_on_additional_from_clause()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c1 in context.Set<Customer>().OrderBy(c => c.CustomerID).Take(5)
                       from c2 in context.Set<Customer>().Include(c => c.Orders)
                       select c2)
                        .ToList();

                Assert.Equal(455, customers.Count);
                Assert.Equal(4150, customers.SelectMany(c => c.Orders).Count());
                Assert.True(customers.SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(455 + 466, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_on_additional_from_clause_no_tracking()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c1 in context.Set<Customer>().OrderBy(c => c.CustomerID).Take(5)
                       from c2 in context.Set<Customer>().AsNoTracking().Include(c => c.Orders)
                       select c2)
                        .ToList();

                Assert.Equal(455, customers.Count);
                Assert.Equal(4150, customers.SelectMany(c => c.Orders).Count());
                Assert.True(customers.SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(0, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: false,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_on_additional_from_clause_with_filter()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c1 in context.Set<Customer>()
                       from c2 in context.Set<Customer>()
                           .Include(c => c.Orders)
                           .Where(c => c.CustomerID == "ALFKI")
                       select c2)
                        .ToList();

                Assert.Equal(91, customers.Count);
                Assert.Equal(546, customers.SelectMany(c => c.Orders).Count());
                Assert.True(customers.SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(1 + 6, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_on_additional_from_clause2()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c1 in context.Set<Customer>().OrderBy(c => c.CustomerID).Take(5)
                       from c2 in context.Set<Customer>().Include(c => c.Orders)
                       select c1)
                        .ToList();

                Assert.Equal(455, customers.Count);
                Assert.True(customers.All(c => c.Orders == null));
                Assert.Equal(5, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: false,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_where_skip_take_projection()
        {
            using (var context = CreateContext())
            {
                var orders
                    = context.OrderDetails.Include(od => od.Order)
                        .Where(od => od.Quantity == 10)
                        .OrderBy(od => od.OrderID)
                        .ThenBy(od => od.ProductID)
                        .Skip(1)
                        .Take(2)
                        .Select(od =>
                            new
                            {
                                od.Order.CustomerID
                            })
                        .ToList();

                Assert.Equal(2, orders.Count);
            }
        }

        [ConditionalFact]
        public virtual void Include_collection_on_join_clause_with_filter()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c in context.Set<Customer>().Include(c => c.Orders)
                       join o in context.Set<Order>() on c.CustomerID equals o.CustomerID
                       where c.CustomerID == "ALFKI"
                       select c)
                        .ToList();

                Assert.Equal(6, customers.Count);
                Assert.Equal(36, customers.SelectMany(c => c.Orders).Count());
                Assert.True(customers.SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(1 + 6, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [ConditionalFact]
        public virtual void Include_collection_on_join_clause_with_order_by_and_filter()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c in context.Set<Customer>().Include(c => c.Orders)
                       join o in context.Set<Order>() on c.CustomerID equals o.CustomerID
                       where c.CustomerID == "ALFKI"
                       orderby c.City
                       select c)
                        .ToList();

                Assert.Equal(6, customers.Count);
                Assert.Equal(36, customers.SelectMany(c => c.Orders).Count());
                Assert.True(customers.SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(1 + 6, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [ConditionalFact]
        public virtual void Include_collection_on_group_join_clause_with_filter()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c in context.Set<Customer>().Include(c => c.Orders).ThenInclude(o => o.Customer)
                       join o in context.Set<Order>() on c.CustomerID equals o.CustomerID into g
                       where c.CustomerID == "ALFKI"
                       select new { c, g })
                        .ToList();

                Assert.Equal(1, customers.Count);
                Assert.Equal(6, customers.SelectMany(c => c.c.Orders).Count());
                Assert.True(customers.SelectMany(c => c.c.Orders).All(o => o.Customer != null));
                Assert.Equal(1 + 6, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers.Select(a => a.c))
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [ConditionalFact]
        public virtual void Include_collection_on_inner_group_join_clause_with_filter()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c in context.Set<Customer>()
                       join o in context.Set<Order>().Include(o => o.OrderDetails).Include(o => o.Customer)
                           on c.CustomerID equals o.CustomerID into g
                       where c.CustomerID == "ALFKI"
                       select new { c, g })
                        .ToList();

                Assert.Equal(1, customers.Count);
                Assert.Equal(6, customers.SelectMany(c => c.g).Count());
                Assert.True(customers.SelectMany(c => c.g).SelectMany(o => o.OrderDetails).All(od => od.Order != null));
                Assert.Equal(1 + 6 + 12, context.ChangeTracker.Entries().Count());

                foreach (var order in customers.SelectMany(a => a.c.Orders))
                {
                    CheckIsLoaded(
                        context,
                        order,
                        orderDetailsLoaded: true,
                        productLoaded: false,
                        customerLoaded: true,
                        ordersLoaded: false);
                }
            }
        }

        [ConditionalFact]
        public virtual void Include_collection_when_groupby()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c in context.Set<Customer>().Include(c => c.Orders)
                       where c.CustomerID == "ALFKI"
                       group c by c.City)
                        .ToList();

                Assert.Equal(1, customers.Count);
                Assert.Equal(6, customers.SelectMany(c => c.Single().Orders).Count());
                Assert.Equal(1 + 6, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers.Select(e => e.Single()))
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_order_by_collection_column()
        {
            using (var context = CreateContext())
            {
                var customer
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .Where(c => c.CustomerID.StartsWith("W"))
                        .OrderByDescending(c => c.Orders.OrderByDescending(oo => oo.OrderDate).FirstOrDefault().OrderDate)
                        .FirstOrDefault();

                Assert.NotNull(customer);
                Assert.Equal("WHITC", customer.CustomerID);
                Assert.NotNull(customer.Orders);
                Assert.Equal(14, customer.Orders.Count);

                CheckIsLoaded(
                    context,
                    customer,
                    ordersLoaded: true,
                    orderDetailsLoaded: false,
                    productLoaded: false);
            }
        }

        [Fact]
        public virtual void Include_collection_order_by_key()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .OrderBy(c => c.CustomerID)
                        .ToList();

                Assert.Equal(91, customers.Count);
                Assert.Equal(830, customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).Count());
                Assert.True(customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(91 + 830, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_order_by_non_key()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .OrderBy(c => c.City)
                        .ToList();

                Assert.Equal(91, customers.Count);
                Assert.Equal(830, customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).Count());
                Assert.True(customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(91 + 830, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [ConditionalFact]
        public virtual void Include_collection_order_by_non_key_with_take()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .OrderBy(c => c.ContactTitle)
                        .Take(10)
                        .ToList();

                Assert.Equal(10, customers.Count);
                Assert.Equal(116, customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).Count());
                Assert.True(customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(10 + 116, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_order_by_non_key_with_skip()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .OrderBy(c => c.ContactTitle)
                        .Skip(10)
                        .ToList();

                Assert.Equal(81, customers.Count);
                Assert.Equal(714, customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).Count());
                Assert.True(customers.Where(c => c.Orders != null).SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(81 + 714, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_order_by_non_key_with_first_or_default()
        {
            using (var context = CreateContext())
            {
                var customer
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .OrderByDescending(c => c.CompanyName)
                        .FirstOrDefault();

                Assert.NotNull(customer);
                Assert.Equal(7, customer.Orders.Count);
                Assert.True(customer.Orders.All(o => o.Customer != null));
                Assert.Equal(1 + 7, context.ChangeTracker.Entries().Count());

                CheckIsLoaded(
                    context,
                    customer,
                    ordersLoaded: true,
                    orderDetailsLoaded: false,
                    productLoaded: false);
            }
        }

        [Fact]
        public virtual void Include_collection_order_by_subquery()
        {
            using (var context = CreateContext())
            {
                var customer
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .Where(c => c.CustomerID == "ALFKI")
                        .OrderBy(c => c.Orders.OrderBy(o => o.EmployeeID).Select(o => o.OrderDate).FirstOrDefault())
                        .FirstOrDefault();

                Assert.NotNull(customer);
                Assert.NotNull(customer.Orders);
                Assert.Equal(6, customer.Orders.Count);

                CheckIsLoaded(
                    context,
                    customer,
                    ordersLoaded: true,
                    orderDetailsLoaded: false,
                    productLoaded: false);
            }
        }

        [Fact]
        public virtual void Include_collection_principal_already_tracked()
        {
            using (var context = CreateContext())
            {
                var customer1
                    = context.Set<Customer>()
                        .Single(c => c.CustomerID == "ALFKI");

                Assert.Equal(1, context.ChangeTracker.Entries().Count());

                var customer2
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .Single(c => c.CustomerID == "ALFKI");

                Assert.Same(customer1, customer2);
                Assert.Equal(6, customer2.Orders.Count);
                Assert.True(customer2.Orders.All(o => o.Customer != null));
                Assert.Equal(1 + 6, context.ChangeTracker.Entries().Count());

                CheckIsLoaded(
                    context,
                    customer2,
                    ordersLoaded: true,
                    orderDetailsLoaded: false,
                    productLoaded: false);
            }
        }

        [Fact]
        public virtual void Include_collection_principal_already_tracked_as_no_tracking()
        {
            using (var context = CreateContext())
            {
                var customer1
                    = context.Set<Customer>()
                        .Single(c => c.CustomerID == "ALFKI");

                Assert.Equal(1, context.ChangeTracker.Entries().Count());

                var customer2
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .AsNoTracking()
                        .Single(c => c.CustomerID == "ALFKI");

                Assert.Equal(customer1.CustomerID, customer2.CustomerID);
                Assert.Null(customer1.Orders);
                Assert.Equal(6, customer2.Orders.Count);
                Assert.True(customer2.Orders.All(o => o.Customer != null));
                Assert.Equal(1, context.ChangeTracker.Entries().Count());

                CheckIsLoaded(
                    context,
                    customer2,
                    ordersLoaded: false,
                    orderDetailsLoaded: false,
                    productLoaded: false);
            }
        }

        [Fact]
        public virtual void Include_collection_single_or_default_no_result()
        {
            using (var context = CreateContext())
            {
                var customer
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .SingleOrDefault(c => c.CustomerID == "ALFKI ?");

                Assert.Null(customer);
            }
        }

        [Fact]
        public virtual void Include_collection_when_projection()
        {
            using (var context = CreateContext())
            {
                var productIds
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .Select(c => c.CustomerID)
                        .ToList();

                Assert.Equal(91, productIds.Count);
                Assert.Equal(0, context.ChangeTracker.Entries().Count());
            }
        }

        [Fact]
        public virtual void Include_collection_with_filter()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .Where(c => c.CustomerID == "ALFKI")
                        .ToList();

                Assert.Equal(1, customers.Count);
                Assert.Equal(6, customers.SelectMany(c => c.Orders).Count());
                Assert.True(customers.SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(1 + 6, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_with_filter_reordered()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .Where(c => c.CustomerID == "ALFKI")
                        .Include(c => c.Orders)
                        .ToList();

                Assert.Equal(1, customers.Count);
                Assert.Equal(6, customers.SelectMany(c => c.Orders).Count());
                Assert.True(customers.SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(1 + 6, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_duplicate_collection()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c1 in context.Set<Customer>()
                        .Include(c => c.Orders)
                        .OrderBy(c => c.CustomerID)
                        .Take(2)
                       from c2 in context.Set<Customer>()
                           .Include(c => c.Orders)
                           .OrderBy(c => c.CustomerID)
                           .Skip(2)
                           .Take(2)
                       select new { c1, c2 })
                        .ToList();

                Assert.Equal(4, customers.Count);
                Assert.Equal(20, customers.SelectMany(c => c.c1.Orders).Count());
                Assert.True(customers.SelectMany(c => c.c1.Orders).All(o => o.Customer != null));
                Assert.Equal(40, customers.SelectMany(c => c.c2.Orders).Count());
                Assert.True(customers.SelectMany(c => c.c2.Orders).All(o => o.Customer != null));
                Assert.Equal(34, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers.Select(e => e.c1))
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }

                foreach (var customer in customers.Select(e => e.c2))
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_duplicate_collection_result_operator()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c1 in context.Set<Customer>()
                        .Include(c => c.Orders)
                        .OrderBy(c => c.CustomerID)
                        .Take(2)
                       from c2 in context.Set<Customer>()
                           .Include(c => c.Orders)
                           .OrderBy(c => c.CustomerID)
                           .Skip(2)
                           .Take(2)
                       select new { c1, c2 })
                        .Take(1)
                        .ToList();

                Assert.Equal(1, customers.Count);
                Assert.Equal(6, customers.SelectMany(c => c.c1.Orders).Count());
                Assert.True(customers.SelectMany(c => c.c1.Orders).All(o => o.Customer != null));
                Assert.Equal(7, customers.SelectMany(c => c.c2.Orders).Count());
                Assert.True(customers.SelectMany(c => c.c2.Orders).All(o => o.Customer != null));
                Assert.Equal(15, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers.Select(e => e.c1))
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }

                foreach (var customer in customers.Select(e => e.c2))
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_duplicate_collection_result_operator2()
        {
            using (var context = CreateContext())
            {
                var customers
                    = (from c1 in context.Set<Customer>()
                        .Include(c => c.Orders)
                        .OrderBy(c => c.CustomerID)
                        .Take(2)
                       from c2 in context.Set<Customer>()
                           .OrderBy(c => c.CustomerID)
                           .Skip(2)
                           .Take(2)
                       select new { c1, c2 })
                        .Take(1)
                        .ToList();

                Assert.Equal(1, customers.Count);
                Assert.Equal(6, customers.SelectMany(c => c.c1.Orders).Count());
                Assert.True(customers.SelectMany(c => c.c1.Orders).All(o => o.Customer != null));
                Assert.True(customers.All(c => c.c2.Orders == null));
                Assert.Equal(8, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers.Select(e => e.c1))
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }

                foreach (var customer in customers.Select(e => e.c2))
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: false,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_duplicate_reference()
        {
            using (var context = CreateContext())
            {
                var orders
                    = (from o1 in context.Set<Order>()
                        .Include(o => o.Customer)
                        .OrderBy(o => o.CustomerID)
                        .Take(2)
                       from o2 in context.Set<Order>()
                           .Include(o => o.Customer)
                           .OrderBy(o => o.CustomerID)
                           .Skip(2)
                           .Take(2)
                       select new { o1, o2 })
                        .ToList();

                Assert.Equal(4, orders.Count);
                Assert.True(orders.All(o => o.o1.Customer != null));
                Assert.True(orders.All(o => o.o2.Customer != null));
                Assert.Equal(1, orders.Select(o => o.o1.Customer).Distinct().Count());
                Assert.Equal(1, orders.Select(o => o.o2.Customer).Distinct().Count());
                Assert.Equal(5, context.ChangeTracker.Entries().Count());

                foreach (var order in orders.Select(e => e.o1))
                {
                    CheckIsLoaded(
                        context,
                        order,
                        orderDetailsLoaded: false,
                        productLoaded: false,
                        customerLoaded: true,
                        ordersLoaded: false);
                }

                foreach (var order in orders.Select(e => e.o2))
                {
                    CheckIsLoaded(
                        context,
                        order,
                        orderDetailsLoaded: false,
                        productLoaded: false,
                        customerLoaded: true,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_duplicate_reference2()
        {
            using (var context = CreateContext())
            {
                var orders
                    = (from o1 in context.Set<Order>()
                        .Include(o => o.Customer)
                        .OrderBy(o => o.OrderID)
                        .Take(2)
                       from o2 in context.Set<Order>()
                           .OrderBy(o => o.OrderID)
                           .Skip(2)
                           .Take(2)
                       select new { o1, o2 })
                        .ToList();

                Assert.Equal(4, orders.Count);
                Assert.True(orders.All(o => o.o1.Customer != null));
                Assert.True(orders.All(o => o.o2.Customer == null));
                Assert.Equal(2, orders.Select(o => o.o1.Customer).Distinct().Count());
                Assert.Equal(6, context.ChangeTracker.Entries().Count());

                foreach (var order in orders.Select(e => e.o1))
                {
                    CheckIsLoaded(
                        context,
                        order,
                        orderDetailsLoaded: false,
                        productLoaded: false,
                        customerLoaded: true,
                        ordersLoaded: false);
                }

                foreach (var order in orders.Select(e => e.o2))
                {
                    CheckIsLoaded(
                        context,
                        order,
                        orderDetailsLoaded: false,
                        productLoaded: false,
                        customerLoaded: false,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_duplicate_reference3()
        {
            using (var context = CreateContext())
            {
                var orders
                    = (from o1 in context.Set<Order>()
                        .OrderBy(o => o.OrderID)
                        .Take(2)
                       from o2 in context.Set<Order>()
                           .OrderBy(o => o.OrderID)
                           .Include(o => o.Customer)
                           .Skip(2)
                           .Take(2)
                       select new { o1, o2 })
                        .ToList();

                Assert.Equal(4, orders.Count);
                Assert.True(orders.All(o => o.o1.Customer == null));
                Assert.True(orders.All(o => o.o2.Customer != null));
                Assert.Equal(2, orders.Select(o => o.o2.Customer).Distinct().Count());
                Assert.Equal(6, context.ChangeTracker.Entries().Count());

                foreach (var order in orders.Select(e => e.o1))
                {
                    CheckIsLoaded(
                        context,
                        order,
                        orderDetailsLoaded: false,
                        productLoaded: false,
                        customerLoaded: false,
                        ordersLoaded: false);
                }

                foreach (var order in orders.Select(e => e.o2))
                {
                    CheckIsLoaded(
                        context,
                        order,
                        orderDetailsLoaded: false,
                        productLoaded: false,
                        customerLoaded: true,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_with_client_filter()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .Include(c => c.Orders)
                        .Where(c => c.IsLondon)
                        .ToList();

                Assert.Equal(6, customers.Count);
                Assert.Equal(46, customers.SelectMany(c => c.Orders).Count());
                Assert.True(customers.SelectMany(c => c.Orders).All(o => o.Customer != null));
                Assert.Equal(13, customers.First().Orders.Count); // AROUT
                Assert.Equal(9, customers.Last().Orders.Count); // SEVES
                Assert.Equal(6 + 46, context.ChangeTracker.Entries().Count());

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_multi_level_reference_and_collection_predicate()
        {
            using (var context = CreateContext())
            {
                var order
                    = context.Set<Order>()
                        .Include(o => o.Customer.Orders)
                        .Single(o => o.OrderID == 10248);

                Assert.NotNull(order.Customer);
                Assert.True(order.Customer.Orders.All(o => o != null));

                CheckIsLoaded(
                    context,
                    order,
                    orderDetailsLoaded: false,
                    productLoaded: false,
                    customerLoaded: true,
                    ordersLoaded: true);
            }
        }

        [Fact]
        public virtual void Include_multi_level_collection_and_then_include_reference_predicate()
        {
            using (var context = CreateContext())
            {
                var order
                    = context.Set<Order>()
                        .Include(o => o.OrderDetails).ThenInclude(od => od.Product)
                        .Single(o => o.OrderID == 10248);

                Assert.NotNull(order.OrderDetails);
                Assert.True(order.OrderDetails.Count > 0);
                Assert.True(order.OrderDetails.All(od => od.Product != null));

                CheckIsLoaded(
                    context,
                    order,
                    orderDetailsLoaded: true,
                    productLoaded: true,
                    customerLoaded: false,
                    ordersLoaded: false);
            }
        }

        [Fact]
        public virtual void Include_multiple_references()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(o => o.Order)
                        .Include(o => o.Product)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(o => o.Order != null));
                Assert.True(orderDetails.All(o => o.Product != null));
                Assert.Equal(830, orderDetails.Select(o => o.Order).Distinct().Count());
                Assert.True(orderDetails.Select(o => o.Product).Distinct().Any());

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: true,
                        customerLoaded: false,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_multiple_references_and_collection_multi_level()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Order.Customer.Orders)
                        .Include(od => od.Product)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));
                Assert.True(orderDetails.All(od => od.Order.Customer.Orders != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: true,
                        customerLoaded: true,
                        ordersLoaded: true);
                }
            }
        }

        [Fact]
        public virtual void Include_multiple_references_and_collection_multi_level_reverse()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Product)
                        .Include(od => od.Order.Customer.Orders)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));
                Assert.True(orderDetails.All(od => od.Order.Customer.Orders != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: true,
                        customerLoaded: true,
                        ordersLoaded: true);
                }
            }
        }

        [Fact]
        public virtual void Include_multiple_references_multi_level()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Order.Customer)
                        .Include(od => od.Product)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: true,
                        customerLoaded: true,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_multiple_references_multi_level_reverse()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Product)
                        .Include(od => od.Order.Customer)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: true,
                        customerLoaded: true,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_reference()
        {
            using (var context = CreateContext())
            {
                var orders
                    = context.Set<Order>()
                        .Include(o => o.Customer)
                        .ToList();

                Assert.Equal(830, orders.Count);
                Assert.True(orders.All(o => o.Customer != null));
                Assert.Equal(89, orders.Select(o => o.Customer).Distinct().Count());
                Assert.Equal(830 + 89, context.ChangeTracker.Entries().Count());

                foreach (var order in orders)
                {
                    CheckIsLoaded(
                        context,
                        order,
                        customerLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_reference_alias_generation()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(o => o.Order)
                        .ToList();

                Assert.True(orderDetails.Any());

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: false,
                        customerLoaded: false,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_reference_and_collection()
        {
            using (var context = CreateContext())
            {
                var orders
                    = context.Set<Order>()
                        .Include(o => o.Customer)
                        .Include(o => o.OrderDetails)
                        .ToList();

                Assert.Equal(830, orders.Count);

                foreach (var order in orders)
                {
                    CheckIsLoaded(
                        context,
                        order,
                        customerLoaded: true,
                        orderDetailsLoaded: true,
                        productLoaded: false,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_force_alias_uniquefication()
        {
            using (var context = CreateContext())
            {
                var result
                    = (from o in context.Set<Order>().Include(o => o.OrderDetails)
                       where o.CustomerID == "ALFKI"
                       select o)
                        .ToList();

                Assert.Equal(6, result.Count);
                Assert.True(result.SelectMany(r => r.OrderDetails).All(od => od.Order != null));

                foreach (var order in result)
                {
                    CheckIsLoaded(
                        context,
                        order,
                        customerLoaded: false,
                        orderDetailsLoaded: true,
                        productLoaded: false,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_reference_as_no_tracking()
        {
            using (var context = CreateContext())
            {
                var orders
                    = context.Set<Order>()
                        .Include(o => o.Customer)
                        .AsNoTracking()
                        .ToList();

                Assert.Equal(830, orders.Count);
                Assert.True(orders.All(o => o.Customer != null));
                Assert.Equal(0, context.ChangeTracker.Entries().Count());

                foreach (var order in orders)
                {
                    CheckIsLoaded(
                        context,
                        order,
                        customerLoaded: false,
                        orderDetailsLoaded: false,
                        productLoaded: false,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_reference_dependent_already_tracked()
        {
            using (var context = CreateContext())
            {
                var orders1
                    = context.Set<Order>()
                        .Where(o => o.CustomerID == "ALFKI")
                        .ToList();

                Assert.Equal(6, context.ChangeTracker.Entries().Count());

                var orders2
                    = context.Set<Order>()
                        .Include(o => o.Customer)
                        .ToList();

                Assert.True(orders1.All(o1 => orders2.Contains(o1, ReferenceEqualityComparer.Instance)));
                Assert.True(orders2.All(o => o.Customer != null));
                Assert.Equal(830 + 89, context.ChangeTracker.Entries().Count());

                foreach (var order in orders2)
                {
                    CheckIsLoaded(
                        context,
                        order,
                        customerLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_reference_single_or_default_when_no_result()
        {
            using (var context = CreateContext())
            {
                var order
                    = context.Set<Order>()
                        .Include(o => o.Customer)
                        .SingleOrDefault(o => o.OrderID == -1);

                Assert.Null(order);
            }
        }

        [Fact]
        public virtual void Include_reference_when_projection()
        {
            using (var context = CreateContext())
            {
                var orders
                    = context.Set<Order>()
                        .Include(o => o.Customer)
                        .Select(o => o.CustomerID)
                        .ToList();

                Assert.Equal(830, orders.Count);
                Assert.Equal(0, context.ChangeTracker.Entries().Count());
            }
        }

        [Fact]
        public virtual void Include_reference_when_entity_in_projection()
        {
            using (var context = CreateContext())
            {
                var orders
                    = context.Set<Order>()
                        .Include(o => o.Customer)
                        .Select(o => new { o, o.CustomerID })
                        .ToList();

                Assert.Equal(830, orders.Count);
                Assert.Equal(919, context.ChangeTracker.Entries().Count());

                foreach (var order in orders.Select(e => e.o))
                {
                    CheckIsLoaded(
                        context,
                        order,
                        customerLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_reference_with_filter()
        {
            using (var context = CreateContext())
            {
                var orders
                    = context.Set<Order>()
                        .Include(o => o.Customer)
                        .Where(o => o.CustomerID == "ALFKI")
                        .ToList();

                Assert.Equal(6, orders.Count);
                Assert.True(orders.All(o => o.Customer != null));
                Assert.Equal(1, orders.Select(o => o.Customer).Distinct().Count());
                Assert.Equal(6 + 1, context.ChangeTracker.Entries().Count());

                foreach (var order in orders)
                {
                    CheckIsLoaded(
                        context,
                        order,
                        customerLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_reference_with_filter_reordered()
        {
            using (var context = CreateContext())
            {
                var orders
                    = context.Set<Order>()
                        .Where(o => o.CustomerID == "ALFKI")
                        .Include(o => o.Customer)
                        .ToList();

                Assert.Equal(6, orders.Count);
                Assert.True(orders.All(o => o.Customer != null));
                Assert.Equal(1, orders.Select(o => o.Customer).Distinct().Count());
                Assert.Equal(6 + 1, context.ChangeTracker.Entries().Count());

                foreach (var order in orders)
                {
                    CheckIsLoaded(
                        context,
                        order,
                        customerLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_references_and_collection_multi_level()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Order.Customer.Orders)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));
                Assert.True(orderDetails.All(od => od.Order.Customer.Orders != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: false,
                        customerLoaded: true,
                        ordersLoaded: true);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_then_include_collection()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .Include(c => c.Orders).ThenInclude(o => o.OrderDetails)
                        .ToList();

                Assert.Equal(91, customers.Count);
                Assert.True(customers.All(c => c.Orders != null));
                Assert.True(customers.All(c => c.Orders.All(o => o.OrderDetails != null)));

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: true,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_then_include_collection_then_include_reference()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .Include(c => c.Orders).ThenInclude(o => o.OrderDetails).ThenInclude(od => od.Product)
                        .ToList();

                Assert.Equal(91, customers.Count);
                Assert.True(customers.All(c => c.Orders != null));
                Assert.True(customers.All(c => c.Orders.All(o => o.OrderDetails != null)));

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: true,
                        productLoaded: true);
                }
            }
        }

        [Fact]
        public virtual void Include_collection_then_include_collection_predicate()
        {
            using (var context = CreateContext())
            {
                var customer
                    = context.Set<Customer>()
                        .Include(c => c.Orders).ThenInclude(o => o.OrderDetails)
                        .SingleOrDefault(c => c.CustomerID == "ALFKI");

                Assert.NotNull(customer);
                Assert.Equal(6, customer.Orders.Count);
                Assert.True(customer.Orders.SelectMany(o => o.OrderDetails).Count() >= 6);

                CheckIsLoaded(
                    context,
                    customer,
                    ordersLoaded: true,
                    orderDetailsLoaded: true,
                    productLoaded: false);
            }
        }

        [Fact]
        public virtual void Include_references_and_collection_multi_level_predicate()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Order.Customer.Orders)
                        .Where(od => od.OrderID == 10248)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));
                Assert.True(orderDetails.All(od => od.Order.Customer.Orders != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: false,
                        customerLoaded: true,
                        ordersLoaded: true);
                }
            }
        }

        [Fact]
        public virtual void Include_references_multi_level()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Order.Customer)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: false,
                        customerLoaded: true,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_multi_level_reference_then_include_collection_predicate()
        {
            using (var context = CreateContext())
            {
                var order
                    = context.Set<Order>()
                        .Include(o => o.Customer).ThenInclude(c => c.Orders)
                        .Single(o => o.OrderID == 10248);

                Assert.NotNull(order.Customer);
                Assert.True(order.Customer.Orders.All(o => o != null));

                CheckIsLoaded(
                    context,
                    order,
                    customerLoaded: true,
                    orderDetailsLoaded: false,
                    productLoaded: false,
                    ordersLoaded: true);
            }
        }

        [Fact]
        public virtual void Include_multiple_references_then_include_collection_multi_level()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Order).ThenInclude(o => o.Customer).ThenInclude(c => c.Orders)
                        .Include(od => od.Product)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));
                Assert.True(orderDetails.All(od => od.Order.Customer.Orders != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: true,
                        customerLoaded: true,
                        ordersLoaded: true);
                }
            }
        }

        [Fact]
        public virtual void Include_multiple_references_then_include_collection_multi_level_reverse()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Product)
                        .Include(od => od.Order).ThenInclude(o => o.Customer).ThenInclude(c => c.Orders)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));
                Assert.True(orderDetails.All(od => od.Order.Customer.Orders != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: true,
                        customerLoaded: true,
                        ordersLoaded: true);
                }
            }
        }

        [Fact]
        public virtual void Include_multiple_references_then_include_multi_level()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Order).ThenInclude(o => o.Customer)
                        .Include(od => od.Product)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: true,
                        customerLoaded: true,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_multiple_references_then_include_multi_level_reverse()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Product)
                        .Include(od => od.Order).ThenInclude(o => o.Customer)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: true,
                        customerLoaded: true,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_references_then_include_collection_multi_level()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Order).ThenInclude(o => o.Customer).ThenInclude(c => c.Orders)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));
                Assert.True(orderDetails.All(od => od.Order.Customer.Orders != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: false,
                        customerLoaded: true,
                        ordersLoaded: true);
                }
            }
        }

        [Fact]
        public virtual void Include_references_then_include_collection_multi_level_predicate()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Order).ThenInclude(o => o.Customer).ThenInclude(c => c.Orders)
                        .Where(od => od.OrderID == 10248)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));
                Assert.True(orderDetails.All(od => od.Order.Customer.Orders != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: false,
                        customerLoaded: true,
                        ordersLoaded: true);
                }
            }
        }

        [Fact]
        public virtual void Include_references_then_include_multi_level()
        {
            using (var context = CreateContext())
            {
                var orderDetails
                    = context.Set<OrderDetail>()
                        .Include(od => od.Order).ThenInclude(o => o.Customer)
                        .ToList();

                Assert.True(orderDetails.Count > 0);
                Assert.True(orderDetails.All(od => od.Order.Customer != null));

                foreach (var orderDetail in orderDetails)
                {
                    CheckIsLoaded(
                        context,
                        orderDetail,
                        orderLoaded: true,
                        productLoaded: false,
                        customerLoaded: true,
                        ordersLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_with_complex_projection()
        {
            using (var context = CreateContext())
            {
                var query = from o in context.Orders.Include(o => o.Customer)
                            select new
                            {
                                CustomerId = new
                                {
                                    Id = o.Customer.CustomerID
                                }
                            };

                var results = query.ToList();

                Assert.Equal(830, results.Count);
            }
        }

        [Fact]
        public virtual void Include_with_take()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Set<Customer>()
                        .OrderByDescending(c => c.City)
                        .Include(c => c.Orders)
                        .Take(10)
                        .ToList();

                Assert.True(customers.All(c => c.Orders.Count > 0));

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        [Fact]
        public virtual void Include_with_skip()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Customers
                        .Include(c => c.Orders)
                        .OrderBy(c => c.ContactName)
                        .Skip(80)
                        .ToList();

                Assert.True(customers.All(c => c.Orders.Count > 0));

                foreach (var customer in customers)
                {
                    CheckIsLoaded(
                        context,
                        customer,
                        ordersLoaded: true,
                        orderDetailsLoaded: false,
                        productLoaded: false);
                }
            }
        }

        private static void CheckIsLoaded(
            NorthwindContext context,
            Customer customer,
            bool ordersLoaded,
            bool orderDetailsLoaded,
            bool productLoaded)
        {
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            Assert.Equal(ordersLoaded, context.Entry(customer).Collection(e => e.Orders).IsLoaded);
            if (customer.Orders != null)
            {
                foreach (var order in customer.Orders)
                {
                    Assert.Equal(ordersLoaded, context.Entry(order).Reference(e => e.Customer).IsLoaded);

                    Assert.Equal(orderDetailsLoaded, context.Entry(order).Collection(e => e.OrderDetails).IsLoaded);
                    if (order.OrderDetails != null)
                    {
                        foreach (var orderDetail in order.OrderDetails)
                        {
                            Assert.Equal(orderDetailsLoaded, context.Entry(orderDetail).Reference(e => e.Order).IsLoaded);

                            Assert.Equal(productLoaded, context.Entry(orderDetail).Reference(e => e.Product).IsLoaded);
                            if (orderDetail.Product != null)
                            {
                                Assert.False(context.Entry(orderDetail.Product).Collection(e => e.OrderDetails).IsLoaded);
                            }
                        }
                    }
                }
            }
        }

        private static void CheckIsLoaded(
            NorthwindContext context,
            Product product,
            bool orderDetailsLoaded,
            bool orderLoaded)
        {
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            Assert.Equal(orderDetailsLoaded, context.Entry(product).Collection(e => e.OrderDetails).IsLoaded);

            if (product.OrderDetails != null)
            {
                foreach (var orderDetail in product.OrderDetails)
                {
                    Assert.Equal(orderDetailsLoaded, context.Entry(orderDetail).Reference(e => e.Product).IsLoaded);

                    Assert.Equal(orderLoaded, context.Entry(orderDetail).Reference(e => e.Order).IsLoaded);
                    if (orderDetail.Order != null)
                    {
                        Assert.False(context.Entry(orderDetail.Order).Collection(e => e.OrderDetails).IsLoaded);
                    }
                }
            }
        }

        private static void CheckIsLoaded(
            NorthwindContext context,
            Order order,
            bool orderDetailsLoaded,
            bool productLoaded,
            bool customerLoaded,
            bool ordersLoaded)
        {
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            Assert.Equal(orderDetailsLoaded, context.Entry(order).Collection(e => e.OrderDetails).IsLoaded);
            if (order.OrderDetails != null)
            {
                foreach (var orderDetail in order.OrderDetails)
                {
                    Assert.Equal(orderDetailsLoaded, context.Entry(orderDetail).Reference(e => e.Order).IsLoaded);

                    Assert.Equal(productLoaded, context.Entry(orderDetail).Reference(e => e.Product).IsLoaded);
                    if (orderDetail.Product != null)
                    {
                        Assert.False(context.Entry(orderDetail.Product).Collection(e => e.OrderDetails).IsLoaded);
                    }
                }
            }

            Assert.Equal(customerLoaded, context.Entry(order).Reference(e => e.Customer).IsLoaded);
            if (order.Customer != null)
            {
                Assert.Equal(ordersLoaded, context.Entry(order.Customer).Collection(e => e.Orders).IsLoaded);
                if (ordersLoaded 
                    && order.Customer.Orders != null)
                {
                    foreach (var backOrder in order.Customer.Orders)
                    {
                        Assert.Equal(ordersLoaded, context.Entry(backOrder).Reference(e => e.Customer).IsLoaded);
                    }
                }
            }
        }

        private static void CheckIsLoaded(
            NorthwindContext context,
            OrderDetail orderDetail,
            bool orderLoaded,
            bool productLoaded,
            bool customerLoaded,
            bool ordersLoaded)
        {
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            Assert.Equal(orderLoaded, context.Entry(orderDetail).Reference(e => e.Order).IsLoaded);
            if (orderDetail.Order != null)
            {
                Assert.False(context.Entry(orderDetail.Order).Collection(e => e.OrderDetails).IsLoaded);

                Assert.Equal(customerLoaded, context.Entry(orderDetail.Order).Reference(e => e.Customer).IsLoaded);
                if (orderDetail.Order.Customer != null)
                {
                    Assert.Equal(ordersLoaded, context.Entry(orderDetail.Order.Customer).Collection(e => e.Orders).IsLoaded);
                    if (ordersLoaded
                        && orderDetail.Order.Customer.Orders != null)
                    {
                        foreach (var backOrder in orderDetail.Order.Customer.Orders)
                        {
                            Assert.Equal(ordersLoaded, context.Entry(backOrder).Reference(e => e.Customer).IsLoaded);
                        }
                    }
                }
            }

            Assert.Equal(productLoaded, context.Entry(orderDetail).Reference(e => e.Product).IsLoaded);
            if (orderDetail.Product != null)
            {
                Assert.False(context.Entry(orderDetail.Product).Collection(e => e.OrderDetails).IsLoaded);
            }
        }
    }
}
