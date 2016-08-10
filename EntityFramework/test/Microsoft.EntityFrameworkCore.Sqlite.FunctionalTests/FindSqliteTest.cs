// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Specification.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.EntityFrameworkCore.Sqlite.FunctionalTests
{
    public abstract class FindSqliteTest
        : FindTestBase<FindSqliteTest.FindSqliteFixture>
    {
        protected FindSqliteTest(FindSqliteFixture fixture)
            : base(fixture)
        {
        }

        public class FindSqliteTestSet : FindSqliteTest
        {
            public FindSqliteTestSet(FindSqliteFixture fixture)
                : base(fixture)
            {
            }

            protected override TEntity Find<TEntity>(DbContext context, params object[] keyValues)
                => context.Set<TEntity>().Find(keyValues);

            protected override Task<TEntity> FindAsync<TEntity>(DbContext context, params object[] keyValues)
                => context.Set<TEntity>().FindAsync(keyValues);
        }

        public class FindSqliteTestContext : FindSqliteTest
        {
            public FindSqliteTestContext(FindSqliteFixture fixture)
                : base(fixture)
            {
            }

            protected override TEntity Find<TEntity>(DbContext context, params object[] keyValues)
                => context.Find<TEntity>(keyValues);

            protected override Task<TEntity> FindAsync<TEntity>(DbContext context, params object[] keyValues)
                => context.FindAsync<TEntity>(keyValues);
        }

        public class FindSqliteTestNonGeneric : FindSqliteTest
        {
            public FindSqliteTestNonGeneric(FindSqliteFixture fixture)
                : base(fixture)
            {
            }

            protected override TEntity Find<TEntity>(DbContext context, params object[] keyValues)
                => (TEntity)context.Find(typeof(TEntity), keyValues);

            protected override async Task<TEntity> FindAsync<TEntity>(DbContext context, params object[] keyValues)
                => (TEntity)await context.FindAsync(typeof(TEntity), keyValues);
        }

        public class FindSqliteFixture : FindFixtureBase
        {
            private readonly IServiceProvider _serviceProvider;

            public FindSqliteFixture()
            {
                _serviceProvider = new ServiceCollection()
                    .AddEntityFrameworkSqlite()
                    .AddSingleton(TestSqliteModelSource.GetFactory(OnModelCreating))
                    .BuildServiceProvider();
            }

            public override void CreateTestStore()
            {
                using (var context = CreateContext())
                {
                    context.Database.EnsureClean();
                    Seed(context);
                }
            }

            public override DbContext CreateContext()
                => new FindContext(new DbContextOptionsBuilder()
                    .UseSqlite(SqliteTestStore.CreateConnectionString("FindTest"))
                    .UseInternalServiceProvider(_serviceProvider).Options);
        }
    }
}
