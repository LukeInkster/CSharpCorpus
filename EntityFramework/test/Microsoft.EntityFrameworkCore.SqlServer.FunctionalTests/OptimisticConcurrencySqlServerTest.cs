// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Specification.Tests;
using Microsoft.EntityFrameworkCore.SqlServer.FunctionalTests.Utilities;
using Xunit;

namespace Microsoft.EntityFrameworkCore.SqlServer.FunctionalTests
{
    public class OptimisticConcurrencySqlServerTest : OptimisticConcurrencyTestBase<SqlServerTestStore, F1SqlServerFixture>
    {
        public OptimisticConcurrencySqlServerTest(F1SqlServerFixture fixture)
            : base(fixture)
        {
        }

        [Fact]
        public async Task Modifying_concurrency_token_only_is_noop()
        {
            byte[] firstVersion;
            using (var context = CreateF1Context())
            {
                var driver = context.Drivers.Single(d => d.CarNumber == 1);
                Assert.NotEqual(1, context.Entry(driver).Property<byte[]>("Version").CurrentValue[0]);
                driver.Podiums = StorePodiums;
                firstVersion = context.Entry(driver).Property<byte[]>("Version").CurrentValue;
                await context.SaveChangesAsync();
            }

            byte[] secondVersion;
            using (var context = CreateF1Context())
            {
                var driver = context.Drivers.Single(d => d.CarNumber == 1);
                Assert.NotEqual(firstVersion, context.Entry(driver).Property<byte[]>("Version").CurrentValue);
                Assert.Equal(StorePodiums, driver.Podiums);

                secondVersion = context.Entry(driver).Property<byte[]>("Version").CurrentValue;
                context.Entry(driver).Property<byte[]>("Version").CurrentValue = firstVersion;
                await context.SaveChangesAsync();
            }

            using (var validationContext = CreateF1Context())
            {
                var driver = validationContext.Drivers.Single(d => d.CarNumber == 1);
                Assert.Equal(secondVersion, validationContext.Entry(driver).Property<byte[]>("Version").CurrentValue);
                Assert.Equal(StorePodiums, driver.Podiums);
            }
        }
    }
}
