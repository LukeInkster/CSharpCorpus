// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.Specification.Tests.TestUtilities.Xunit;
using Microsoft.EntityFrameworkCore.SqlServer.FunctionalTests.SqlAzure.Model;
using Microsoft.EntityFrameworkCore.SqlServer.FunctionalTests.Utilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.EntityFrameworkCore.SqlServer.FunctionalTests.SqlAzure
{
    [SqlServerCondition(SqlServerCondition.IsSqlAzure)]
    public class SqlAzureBatchingTest : IClassFixture<BatchingSqlAzureFixture>
    {
        private readonly BatchingSqlAzureFixture _fixture;

        public SqlAzureBatchingTest(BatchingSqlAzureFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
        }
        
        [ConditionalTheory]
        [InlineData(1)]
        [InlineData(10)]
        [InlineData(100)]
        [InlineData(1000)]
        public void AddWithBatchSize(int batchSize)
        {
            using (var context = _fixture.CreateContext(batchSize))
            {
                using (context.Database.BeginTransaction())
                {
                    for (var i = 0; i < batchSize; i++)
                    {
                        var uuid = Guid.NewGuid().ToString();
                        context.Products.Add(new Product
                        {
                            Name = uuid,
                            ProductNumber = uuid.Substring(0, 25),
                            Weight = 1000,
                            SellStartDate = DateTime.Now
                        });
                    }

                    Assert.Equal(batchSize, context.SaveChanges());
                }
            }
        }
    }
}
