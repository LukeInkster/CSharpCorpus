// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Specification.Tests;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.EntityFrameworkCore.InMemory.FunctionalTests
{
    public class DataAnnotationInMemoryFixture : DataAnnotationFixtureBase<InMemoryTestStore>
    {
        public static readonly string DatabaseName = "DataAnnotations";

        private readonly IServiceProvider _serviceProvider;

        public DataAnnotationInMemoryFixture()
        {
            _serviceProvider = new ServiceCollection()
                .AddEntityFrameworkInMemoryDatabase()
                .AddSingleton(TestInMemoryModelSource.GetFactory(OnModelCreating))
                .AddSingleton<ThrowingModelValidator>()
                .BuildServiceProvider();
        }

        public override ModelValidator ThrowingValidator
            => _serviceProvider.GetService<ThrowingModelValidator>();

        // ReSharper disable once ClassNeverInstantiated.Local
        private class ThrowingModelValidator : ModelValidator
        {
            protected override void ShowWarning(string message)
            {
                throw new InvalidOperationException(message);
            }
        }

        public override InMemoryTestStore CreateTestStore()
        {
            return InMemoryTestStore.GetOrCreateShared(DatabaseName, () =>
                {
                    var optionsBuilder = new DbContextOptionsBuilder()
                        .UseInMemoryDatabase()
                        .UseInternalServiceProvider(_serviceProvider);

                    using (var context = new DataAnnotationContext(optionsBuilder.Options))
                    {
                        context.Database.EnsureDeleted();
                        if (context.Database.EnsureCreated())
                        {
                            DataAnnotationModelInitializer.Seed(context);
                        }
                    }
                });
        }

        public override DataAnnotationContext CreateContext(InMemoryTestStore testStore)
            => new DataAnnotationContext(new DbContextOptionsBuilder()
                .UseInMemoryDatabase()
                .UseInternalServiceProvider(_serviceProvider).Options);
    }
}
