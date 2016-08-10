// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.SqlServer.FunctionalTests.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.EntityFrameworkCore.SqlServer.FunctionalTests
{
    public class SequentialGuidEndToEndTest
    {
        [Fact]
        public async Task Can_use_sequential_GUID_end_to_end_async()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkSqlServer()
                .BuildServiceProvider();

            using (var context = new BronieContext(serviceProvider, "GooieBronies"))
            {
                context.Database.EnsureClean();

                for (var i = 0; i < 50; i++)
                {
                    context.Add(new Pegasus { Name = "Rainbow Dash " + i });
                }

                await context.SaveChangesAsync();
            }

            using (var context = new BronieContext(serviceProvider, "GooieBronies"))
            {
                var pegasuses = await context.Pegasuses.OrderBy(e => e.Id).ToListAsync();

                for (var i = 0; i < 50; i++)
                {
                    Assert.Equal("Rainbow Dash " + i, pegasuses[i].Name);
                }
            }
        }

        [Fact]
        public async Task Can_use_explicit_values()
        {
            var serviceProvider = new ServiceCollection()
                .AddEntityFrameworkSqlServer()
                .BuildServiceProvider();

            var guids = new List<Guid>();

            using (var context = new BronieContext(serviceProvider, "GooieExplicitBronies"))
            {
                context.Database.EnsureClean();

                for (var i = 0; i < 50; i++)
                {
                    guids.Add(context.Add(new Pegasus { Name = "Rainbow Dash " + i, Index = i, Id = Guid.NewGuid() }).Entity.Id);
                }

                await context.SaveChangesAsync();
            }

            using (var context = new BronieContext(serviceProvider, "GooieExplicitBronies"))
            {
                var pegasuses = await context.Pegasuses.OrderBy(e => e.Index).ToListAsync();

                for (var i = 0; i < 50; i++)
                {
                    Assert.Equal("Rainbow Dash " + i, pegasuses[i].Name);
                    Assert.Equal(guids[i], pegasuses[i].Id);
                }
            }
        }

        private class BronieContext : DbContext
        {
            private readonly IServiceProvider _serviceProvider;
            private readonly string _databaseName;

            public BronieContext(IServiceProvider serviceProvider, string databaseName)
            {
                _serviceProvider = serviceProvider;
                _databaseName = databaseName;
            }

            public DbSet<Pegasus> Pegasuses { get; set; }

            protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
                => optionsBuilder
                    .UseSqlServer(SqlServerTestStore.CreateConnectionString(_databaseName))
                    .UseInternalServiceProvider(_serviceProvider);
        }

        private class Pegasus
        {
            public Guid Id { get; set; }
            public string Name { get; set; }
            public int Index { get; set; }
        }
    }
}
