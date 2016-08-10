// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.Common;
using System.Linq;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.Northwind;
using Xunit;

// ReSharper disable InconsistentNaming

// ReSharper disable ConvertToConstant.Local
// ReSharper disable AccessToDisposedClosure
namespace Microsoft.EntityFrameworkCore.Specification.Tests
{
    public abstract class FromSqlQueryTestBase<TFixture> : IClassFixture<TFixture>
        where TFixture : NorthwindQueryRelationalFixture, new()
    {
        [Fact]
        public virtual void Bad_data_error_handling_invalid_cast_key()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(
                    CoreStrings.ErrorMaterializingValueInvalidCast(typeof(int), typeof(string)),
                    Assert.Throws<InvalidOperationException>(() =>
                        context.Set<Product>()
                            .FromSql(@"SELECT ""ProductID"" AS ProductName, ""ProductName"" AS ProductID, ""SupplierID"", ""UnitsInStock"", ""Discontinued""
                               FROM ""Products""")
                            .ToList()).Message);
            }
        }

        [Fact]
        public virtual void Bad_data_error_handling_invalid_cast()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(
                    CoreStrings.ErrorMaterializingPropertyInvalidCast("Product", "ProductName", typeof(string), typeof(int)),
                    Assert.Throws<InvalidOperationException>(() =>
                        context.Set<Product>()
                            .FromSql(@"SELECT ""ProductID"", ""ProductName"" AS SupplierID, ""SupplierID"" AS ProductName, ""UnitsInStock"", ""Discontinued""
                               FROM ""Products""")
                            .ToList()).Message);
            }
        }

        [Fact]
        public virtual void Bad_data_error_handling_invalid_cast_projection()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(
                    CoreStrings.ErrorMaterializingValueInvalidCast(typeof(string), typeof(int)),
                    Assert.Throws<InvalidOperationException>(() =>
                        context.Set<Product>()
                            .FromSql(@"SELECT ""ProductID"", ""ProductName"" AS SupplierID, ""SupplierID"" AS ProductName, ""UnitsInStock"", ""Discontinued""
                               FROM ""Products""")
                            .Select(p => p.ProductName)
                            .ToList()).Message);
            }
        }

        [Fact]
        public virtual void Bad_data_error_handling_invalid_cast_no_tracking()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(
                    CoreStrings.ErrorMaterializingPropertyInvalidCast("Product", "ProductID", typeof(int), typeof(string)),
                    Assert.Throws<InvalidOperationException>(() =>
                        context.Set<Product>()
                            .AsNoTracking()
                            .FromSql(@"SELECT ""ProductID"" AS ProductName, ""ProductName"" AS ProductID, ""SupplierID"", ""UnitsInStock"", ""Discontinued""
                               FROM ""Products""")
                            .ToList()).Message);
            }
        }

        [Fact]
        public virtual void Bad_data_error_handling_null()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(
                    CoreStrings.ErrorMaterializingPropertyNullReference("Product", "Discontinued", typeof(bool)),
                    Assert.Throws<InvalidOperationException>(() =>
                        context.Set<Product>()
                            .FromSql(@"SELECT ""ProductID"", ""ProductName"", ""SupplierID"", ""UnitsInStock"", NULL AS ""Discontinued""
                               FROM ""Products""")
                            .ToList()).Message);
            }
        }

        [Fact]
        public virtual void Bad_data_error_handling_null_projection()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(
                    CoreStrings.ErrorMaterializingValueNullReference(typeof(bool)),
                    Assert.Throws<InvalidOperationException>(() =>
                        context.Set<Product>()
                            .FromSql(@"SELECT ""ProductID"", ""ProductName"", ""SupplierID"", ""UnitsInStock"", NULL AS ""Discontinued""
                               FROM ""Products""")
                            .Select(p => p.Discontinued)
                            .ToList()).Message);
            }
        }

        [Fact]
        public virtual void Bad_data_error_handling_null_no_tracking()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(
                    CoreStrings.ErrorMaterializingPropertyNullReference("Product", "Discontinued", typeof(bool)),
                    Assert.Throws<InvalidOperationException>(() =>
                        context.Set<Product>()
                            .AsNoTracking()
                            .FromSql(@"SELECT ""ProductID"", ""ProductName"", ""SupplierID"", ""UnitsInStock"", NULL AS ""Discontinued""
                               FROM ""Products""")
                            .ToList()).Message);
            }
        }

        [Fact]
        public virtual void From_sql_queryable_simple()
        {
            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(@"SELECT * FROM ""Customers"" WHERE ""ContactName"" LIKE '%z%'")
                    .ToArray();

                Assert.Equal(14, actual.Length);
                Assert.Equal(14, context.ChangeTracker.Entries().Count());
            }
        }

        [Fact]
        public virtual void From_sql_queryable_simple_columns_out_of_order()
        {
            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(@"SELECT ""Region"", ""PostalCode"", ""Phone"", ""Fax"", ""CustomerID"", ""Country"", ""ContactTitle"", ""ContactName"", ""CompanyName"", ""City"", ""Address"" FROM ""Customers""")
                    .ToArray();

                Assert.Equal(91, actual.Length);
                Assert.Equal(91, context.ChangeTracker.Entries().Count());
            }
        }

        [Fact]
        public virtual void From_sql_queryable_simple_columns_out_of_order_and_extra_columns()
        {
            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(@"SELECT ""Region"", ""PostalCode"", ""PostalCode"" AS ""Foo"", ""Phone"", ""Fax"", ""CustomerID"", ""Country"", ""ContactTitle"", ""ContactName"", ""CompanyName"", ""City"", ""Address"" FROM ""Customers""")
                    .ToArray();

                Assert.Equal(91, actual.Length);
                Assert.Equal(91, context.ChangeTracker.Entries().Count());
            }
        }

        [Fact]
        public virtual void From_sql_queryable_simple_columns_out_of_order_and_not_enough_columns_throws()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(
                    RelationalStrings.FromSqlMissingColumn("Region"),
                    Assert.Throws<InvalidOperationException>(
                        () => context.Set<Customer>()
                            .FromSql(@"SELECT ""PostalCode"", ""Phone"", ""Fax"", ""CustomerID"", ""Country"", ""ContactTitle"", ""ContactName"", ""CompanyName"", ""City"", ""Address"" FROM ""Customers""")
                            .ToArray()
                        ).Message);
            }
        }

        [Fact]
        public virtual void From_sql_queryable_composed()
        {
            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(@"SELECT * FROM ""Customers""")
                    .Where(c => c.ContactName.Contains("z"))
                    .ToArray();

                Assert.Equal(14, actual.Length);
            }
        }

        [Fact]
        public virtual void From_sql_composed_contains()
        {
            using (var context = CreateContext())
            {
                var actual
                    = (from c in context.Set<Customer>()
                       where context.Orders.FromSql(@"SELECT * FROM ""Orders""")
                           .Select(o => o.CustomerID)
                           .Contains(c.CustomerID)
                       select c)
                        .ToArray();

                Assert.Equal(89, actual.Length);
            }
        }

        [Fact]
        public virtual void From_sql_composed_contains2()
        {
            using (var context = CreateContext())
            {
                var actual
                    = (from c in context.Set<Customer>()
                       where
                           c.CustomerID == "ALFKI"
                           && context.Orders.FromSql(@"SELECT * FROM ""Orders""")
                               .Select(o => o.CustomerID)
                               .Contains(c.CustomerID)
                       select c)
                        .ToArray();

                Assert.Equal(1, actual.Length);
            }
        }

        [Fact]
        public virtual void From_sql_queryable_multiple_composed()
        {
            using (var context = CreateContext())
            {
                var actual
                    = (from c in context.Set<Customer>().FromSql(@"SELECT * FROM ""Customers""")
                       from o in context.Set<Order>().FromSql(@"SELECT * FROM ""Orders""")
                       where c.CustomerID == o.CustomerID
                       select new { c, o })
                        .ToArray();

                Assert.Equal(830, actual.Length);
            }
        }

        [Fact]
        public virtual void From_sql_queryable_multiple_composed_with_closure_parameters()
        {
            var startDate = new DateTime(1997, 1, 1);
            var endDate = new DateTime(1998, 1, 1);

            using (var context = CreateContext())
            {
                var actual
                    = (from c in context.Set<Customer>().FromSql(@"SELECT * FROM ""Customers""")
                       from o in context.Set<Order>().FromSql(@"SELECT * FROM ""Orders"" WHERE ""OrderDate"" BETWEEN {0} AND {1}",
                           startDate,
                           endDate)
                       where c.CustomerID == o.CustomerID
                       select new { c, o })
                        .ToArray();

                Assert.Equal(411, actual.Length);
            }
        }

        [Fact]
        public virtual void From_sql_queryable_multiple_composed_with_parameters_and_closure_parameters()
        {
            var city = "London";
            var startDate = new DateTime(1997, 1, 1);
            var endDate = new DateTime(1998, 1, 1);

            using (var context = CreateContext())
            {
                var actual
                    = (from c in context.Set<Customer>().FromSql(@"SELECT * FROM ""Customers"" WHERE ""City"" = {0}",
                        city)
                       from o in context.Set<Order>().FromSql(@"SELECT * FROM ""Orders"" WHERE ""OrderDate"" BETWEEN {0} AND {1}",
                           startDate,
                           endDate)
                       where c.CustomerID == o.CustomerID
                       select new { c, o })
                        .ToArray();

                Assert.Equal(25, actual.Length);
            }
        }

        [Fact]
        public virtual void From_sql_queryable_multiple_line_query()
        {
            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(@"SELECT *
FROM ""Customers""
WHERE ""City"" = 'London'")
                    .ToArray();

                Assert.Equal(6, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));
            }
        }

        [Fact]
        public virtual void From_sql_queryable_composed_multiple_line_query()
        {
            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(@"SELECT *
FROM ""Customers""")
                    .Where(c => c.City == "London")
                    .ToArray();

                Assert.Equal(6, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));
            }
        }

        [Fact]
        public virtual void From_sql_queryable_with_parameters()
        {
            var city = "London";
            var contactTitle = "Sales Representative";

            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(
                        @"SELECT * FROM ""Customers"" WHERE ""City"" = {0} AND ""ContactTitle"" = {1}",
                        city,
                        contactTitle)
                    .ToArray();

                Assert.Equal(3, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));
                Assert.True(actual.All(c => c.ContactTitle == "Sales Representative"));
            }
        }

        [Fact]
        public virtual void From_sql_queryable_with_parameters_inline()
        {
            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(
                        @"SELECT * FROM ""Customers"" WHERE ""City"" = {0} AND ""ContactTitle"" = {1}",
                        "London",
                        "Sales Representative")
                    .ToArray();

                Assert.Equal(3, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));
                Assert.True(actual.All(c => c.ContactTitle == "Sales Representative"));
            }
        }

        [Fact]
        public virtual void From_sql_queryable_with_null_parameter()
        {
            int? reportsTo = null;

            using (var context = CreateContext())
            {
                var actual = context.Set<Employee>()
                    .FromSql(
                        @"SELECT * FROM ""Employees"" WHERE ""ReportsTo"" = {0} OR (""ReportsTo"" IS NULL AND {0} IS NULL)",
                        // ReSharper disable once ExpressionIsAlwaysNull
                        reportsTo)
                    .ToArray();

                Assert.Equal(1, actual.Length);
            }
        }

        [Fact]
        public virtual void From_sql_queryable_with_parameters_and_closure()
        {
            var city = "London";
            var contactTitle = "Sales Representative";

            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(@"SELECT * FROM ""Customers"" WHERE ""City"" = {0}", city)
                    .Where(c => c.ContactTitle == contactTitle)
                    .ToArray();

                Assert.Equal(3, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));
                Assert.True(actual.All(c => c.ContactTitle == "Sales Representative"));
            }
        }

        [Fact]
        public virtual void From_sql_queryable_simple_cache_key_includes_query_string()
        {
            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(@"SELECT * FROM ""Customers"" WHERE ""City"" = 'London'")
                    .ToArray();

                Assert.Equal(6, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));

                actual = context.Set<Customer>()
                    .FromSql(@"SELECT * FROM ""Customers"" WHERE ""City"" = 'Seattle'")
                    .ToArray();

                Assert.Equal(1, actual.Length);
                Assert.True(actual.All(c => c.City == "Seattle"));
            }
        }

        [Fact]
        public virtual void From_sql_queryable_with_parameters_cache_key_includes_parameters()
        {
            var city = "London";
            var contactTitle = "Sales Representative";
            var sql = @"SELECT * FROM ""Customers"" WHERE ""City"" = {0} AND ""ContactTitle"" = {1}";

            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(sql, city, contactTitle)
                    .ToArray();

                Assert.Equal(3, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));
                Assert.True(actual.All(c => c.ContactTitle == "Sales Representative"));

                city = "Madrid";
                contactTitle = "Accounting Manager";

                actual = context.Set<Customer>()
                    .FromSql(sql, city, contactTitle)
                    .ToArray();

                Assert.Equal(2, actual.Length);
                Assert.True(actual.All(c => c.City == "Madrid"));
                Assert.True(actual.All(c => c.ContactTitle == "Accounting Manager"));
            }
        }

        [Fact]
        public virtual void From_sql_queryable_simple_as_no_tracking_not_composed()
        {
            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(@"SELECT * FROM ""Customers""")
                    .AsNoTracking()
                    .ToArray();

                Assert.Equal(91, actual.Length);
                Assert.Equal(0, context.ChangeTracker.Entries().Count());
            }
        }

        [Fact]
        public virtual void From_sql_queryable_simple_projection_composed()
        {
            using (var context = CreateContext())
            {
                var actual = context.Set<Product>()
                    .FromSql(@"SELECT *
FROM Products
WHERE Discontinued <> 1
AND ((UnitsInStock + UnitsOnOrder) < ReorderLevel)")
                    .Select(p => p.ProductName)
                    .ToArray();

                Assert.Equal(2, actual.Length);
            }
        }

        [Fact]
        public virtual void From_sql_queryable_simple_include()
        {
            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(@"SELECT * FROM ""Customers""")
                    .Include(c => c.Orders)
                    .ToArray();

                Assert.Equal(830, actual.SelectMany(c => c.Orders).Count());
            }
        }

        [Fact]
        public virtual void From_sql_queryable_simple_composed_include()
        {
            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(@"SELECT * FROM ""Customers""")
                    .Where(c => c.City == "London")
                    .Include(c => c.Orders)
                    .ToArray();

                Assert.Equal(46, actual.SelectMany(c => c.Orders).Count());
            }
        }

        [Fact]
        public virtual void From_sql_annotations_do_not_affect_successive_calls()
        {
            using (var context = CreateContext())
            {
                var actual = context.Customers
                    .FromSql(@"SELECT * FROM ""Customers"" WHERE ""ContactName"" LIKE '%z%'")
                    .ToArray();

                Assert.Equal(14, actual.Length);

                actual = context.Customers
                    .ToArray();

                Assert.Equal(91, actual.Length);
            }
        }

        [Fact]
        public virtual void From_sql_composed_with_nullable_predicate()
        {
            using (var context = CreateContext())
            {
                var actual = context.Set<Customer>()
                    .FromSql(@"SELECT * FROM ""Customers""")
                    .Where(c => c.ContactName == c.CompanyName)
                    .ToArray();

                Assert.Equal(0, actual.Length);
            }
        }

        [Fact]
        public virtual void From_sql_with_dbParameter()
        {
            using (var context = CreateContext())
            {
                var parameter = CreateDbParameter("@city", "London");

                var actual = context.Customers
                    .FromSql(@"SELECT * FROM ""Customers"" WHERE ""City"" = @city", parameter)
                    .ToArray();

                Assert.Equal(6, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));
            }
        }

        [Fact]
        public virtual void From_sql_with_dbParameter_mixed()
        {
            using (var context = CreateContext())
            {
                var city = "London";
                var title = "Sales Representative";

                var titleParameter = CreateDbParameter("@title", title);

                var actual = context.Customers
                    .FromSql(@"SELECT * FROM ""Customers"" WHERE ""City"" = {0} AND ""ContactTitle"" = @title",
                        city,
                        titleParameter)
                    .ToArray();

                Assert.Equal(3, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));
                Assert.True(actual.All(c => c.ContactTitle == "Sales Representative"));

                var cityParameter = CreateDbParameter("@city", city);

                actual = context.Customers
                    .FromSql(@"SELECT * FROM ""Customers"" WHERE ""City"" = @city AND ""ContactTitle"" = {1}",
                        cityParameter,
                        title)
                    .ToArray();

                Assert.Equal(3, actual.Length);
                Assert.True(actual.All(c => c.City == "London"));
                Assert.True(actual.All(c => c.ContactTitle == "Sales Representative"));
            }
        }

        [Fact]
        public virtual void Include_does_not_close_user_opened_connection_for_empty_result()
        {
            using (var ctx = CreateContext())
            {
                ctx.Database.OpenConnection();

                var query = ctx.Customers
                    .Include(v => v.Orders)
                    .Where(v => v.CustomerID == "MAMRFC")
                    .ToList();

                Assert.Empty(query);
                Assert.Equal(ConnectionState.Open, ctx.Database.GetDbConnection().State);
            }
        }

        protected NorthwindContext CreateContext() => Fixture.CreateContext();

        protected FromSqlQueryTestBase(TFixture fixture)
        {
            Fixture = fixture;
        }

        protected TFixture Fixture { get; }

        protected abstract DbParameter CreateDbParameter(string name, object value);
    }
}
