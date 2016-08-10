// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.EntityFrameworkCore.Specification.Tests;
using Microsoft.EntityFrameworkCore.SqlServer.FunctionalTests.Utilities;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.EntityFrameworkCore.SqlServer.FunctionalTests
{
    public class NullKeysSqlServerTest : NullKeysTestBase<NullKeysSqlServerTest.NullKeysSqlServerFixture>
    {
        public NullKeysSqlServerTest(NullKeysSqlServerFixture fixture)
            : base(fixture)
        {
        }

        public class NullKeysSqlServerFixture : NullKeysFixtureBase
        {
            private readonly DbContextOptions _options;

            public NullKeysSqlServerFixture()
            {
                _options = new DbContextOptionsBuilder()
                    .UseSqlServer(SqlServerTestStore.CreateConnectionString("StringsContext"))
                    .UseInternalServiceProvider(new ServiceCollection()
                        .AddEntityFrameworkSqlServer()
                        .AddSingleton(TestSqlServerModelSource.GetFactory(OnModelCreating))
                        .BuildServiceProvider())
                    .Options;

                EnsureCreated();
            }

            public override DbContext CreateContext()
                => new DbContext(_options);

            protected override void EnsureClean(DbContext context)
                => context.Database.EnsureClean();
        }
    }
}
