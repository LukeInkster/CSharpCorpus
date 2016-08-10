// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.Northwind;
using Xunit;

// ReSharper disable AccessToDisposedClosure
namespace Microsoft.EntityFrameworkCore.Specification.Tests
{
    public abstract class QueryNoClientEvalTestBase<TFixture> : IClassFixture<TFixture>
        where TFixture : NorthwindQueryRelationalFixture, new()
    {
        [Fact]
        public virtual void Throws_when_where()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning("[c].IsLondon")),
                    Assert.Throws<InvalidOperationException>(
                        () => context.Customers.Where(c => c.IsLondon).ToList()).Message);
            }
        }

        [Fact]
        public virtual void Throws_when_orderby()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning("orderby [c].IsLondon asc")),
                    Assert.Throws<InvalidOperationException>(
                        () => context.Customers.OrderBy(c => c.IsLondon).ToList()).Message);
            }
        }

        [Fact]
        public virtual void Throws_when_orderby_multiple()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning("orderby [c].IsLondon asc, ClientMethod([c]) asc")),
                    Assert.Throws<InvalidOperationException>(
                        () => context.Customers
                            .OrderBy(c => c.IsLondon)
                            .ThenBy(c => ClientMethod(c))
                            .ToList()).Message);
            }
        }

        private static object ClientMethod(object o) => o.GetHashCode();

        [Fact]
        public virtual void Throws_when_where_subquery_correlated()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning(
                        "{from Customer c2 in value(Microsoft.EntityFrameworkCore.Query.Internal.EntityQueryable`1[Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.Northwind.Customer]) where (([c1].CustomerID == [c2].CustomerID) AndAlso [c2].IsLondon) select [c2] => Any()}")),
                    Assert.Throws<InvalidOperationException>(
                        () => context.Customers
                            .Where(c1 => context.Customers
                                .Any(c2 => c1.CustomerID == c2.CustomerID && c2.IsLondon))
                            .ToList()).Message);
            }
        }

        [Fact]
        public virtual void Throws_when_all()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning("All([c].IsLondon)")),
                    Assert.Throws<InvalidOperationException>(
                        () => context.Customers.All(c => c.IsLondon)).Message);
            }
        }

        [Fact]
        public virtual void Throws_when_from_sql_composed()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning("[c].IsLondon")),
                    Assert.Throws<InvalidOperationException>(
                        () => context.Customers
                            .FromSql(@"select * from ""Customers""")
                            .Where(c => c.IsLondon)
                            .ToList()).Message);
            }
        }

        [Fact]
        public virtual void Doesnt_throw_when_from_sql_not_composed()
        {
            using (var context = CreateContext())
            {
                var customers
                    = context.Customers
                        .FromSql(@"select * from ""Customers""")
                        .ToList();

                Assert.Equal(91, customers.Count);
            }
        }

        [Fact]
        public virtual void Throws_when_subquery_main_from_clause()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning("[c].IsLondon")),
                    Assert.Throws<InvalidOperationException>(
                        () =>
                            (from c1 in context.Customers
                                .Where(c => c.IsLondon)
                                .Take(5)
                             select c1)
                                .ToList()).Message);
            }
        }

        [Fact]
        public virtual void Throws_when_select_many()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(
                    CoreStrings.WarningAsErrorTemplate(
                        $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                        RelationalStrings.ClientEvalWarning("from Int32 i in value(System.Int32[])")),
                    Assert.Throws<InvalidOperationException>(
                        () =>
                            (from c1 in context.Customers
                             from i in new[] { 1, 2, 3 }
                             select c1)
                                .ToList()).Message);
            }
        }

        [Fact]
        public virtual void Throws_when_join()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning("join Int32 i in __p_0 on [e1].EmployeeID equals [i]")),
                    Assert.Throws<InvalidOperationException>(
                        () =>
                            (from e1 in context.Employees
                             join i in new[] { 1, 2, 3 } on e1.EmployeeID equals i
                             select e1)
                                .ToList()).Message);
            }
        }

        [Fact]
        public virtual void Throws_when_group_join()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning("join Int32 i in __p_0 on [e1].EmployeeID equals [i]")),
                    Assert.Throws<InvalidOperationException>(
                        () =>
                            (from e1 in context.Employees
                             join i in new[] { 1, 2, 3 } on e1.EmployeeID equals i into g
                             select e1)
                                .ToList()).Message);
            }
        }

        [Fact]
        public virtual void Throws_when_group_by()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning("GroupBy([c].CustomerID, [c])")),
                    Assert.Throws<InvalidOperationException>(
                        () => context.Customers
                            .GroupBy(c => c.CustomerID)
                            .ToList()).Message);
            }
        }

        [Fact]
        public virtual void Throws_when_first()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning("[c].IsLondon")),
                    Assert.Throws<InvalidOperationException>(
                        () => context.Customers.First(c => c.IsLondon)).Message);
            }
        }

        [Fact]
        public virtual void Throws_when_single()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning("[c].IsLondon")),
                    Assert.Throws<InvalidOperationException>(
                        () => context.Customers.Single(c => c.IsLondon)).Message);
            }
        }

        [Fact]
        public virtual void Throws_when_first_or_default()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning("[c].IsLondon")),
                    Assert.Throws<InvalidOperationException>(
                        () => context.Customers.FirstOrDefault(c => c.IsLondon)).Message);
            }
        }

        [Fact]
        public virtual void Throws_when_single_or_default()
        {
            using (var context = CreateContext())
            {
                Assert.Equal(CoreStrings.WarningAsErrorTemplate(
                    $"{nameof(RelationalEventId)}.{nameof(RelationalEventId.QueryClientEvaluationWarning)}",
                    RelationalStrings.ClientEvalWarning("[c].IsLondon")),
                    Assert.Throws<InvalidOperationException>(
                        () => context.Customers.SingleOrDefault(c => c.IsLondon)).Message);
            }
        }

        protected NorthwindContext CreateContext() => Fixture.CreateContext();

        protected QueryNoClientEvalTestBase(TFixture fixture)
        {
            Fixture = fixture;
        }

        protected TFixture Fixture { get; }
    }
}
