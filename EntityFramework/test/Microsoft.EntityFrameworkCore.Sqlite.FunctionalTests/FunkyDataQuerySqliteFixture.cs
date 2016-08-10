﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.Specification.Tests;
using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.FunkyDataModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.EntityFrameworkCore.Sqlite.FunctionalTests
{
    public class FunkyDataQuerySqliteFixture : FunkyDataQueryFixtureBase<SqliteTestStore>
    {
        public static readonly string DatabaseName = "FunkyDataQueryTest";

        private readonly IServiceProvider _serviceProvider;

        private readonly string _connectionString = SqliteTestStore.CreateConnectionString(DatabaseName);

        public FunkyDataQuerySqliteFixture()
        {
            _serviceProvider = new ServiceCollection()
                .AddEntityFrameworkSqlite()
                .AddSingleton(TestSqliteModelSource.GetFactory(OnModelCreating))
                .AddSingleton<ILoggerFactory>(new TestSqlLoggerFactory())
                .BuildServiceProvider();
        }

        public override SqliteTestStore CreateTestStore()
        {
            return SqliteTestStore.GetOrCreateShared(DatabaseName, () =>
            {
                var optionsBuilder = new DbContextOptionsBuilder()
                    .UseSqlite(_connectionString)
                    .UseInternalServiceProvider(_serviceProvider);

                using (var context = new FunkyDataContext(optionsBuilder.Options))
                {
                    context.Database.EnsureClean();
                    FunkyDataModelInitializer.Seed(context);

                    TestSqlLoggerFactory.Reset();
                }
            });
        }

        public override FunkyDataContext CreateContext(SqliteTestStore testStore)
        {
            var optionsBuilder = new DbContextOptionsBuilder()
                .UseSqlite(testStore.Connection)
                .UseInternalServiceProvider(_serviceProvider);

            var context = new FunkyDataContext(optionsBuilder.Options);

            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;

            context.Database.UseTransaction(testStore.Transaction);

            return context;
        }
    }
}
