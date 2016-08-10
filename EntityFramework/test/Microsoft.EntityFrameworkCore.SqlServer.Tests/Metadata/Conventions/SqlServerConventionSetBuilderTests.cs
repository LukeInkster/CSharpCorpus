// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Tests.Metadata.Conventions;
using Xunit;

namespace Microsoft.EntityFrameworkCore.SqlServer.Tests.Metadata.Conventions
{
    public class SqlServerConventionSetBuilderTests : ConventionSetBuilderTests
    {
        public override IModel Can_build_a_model_with_default_conventions_without_DI()
        {
            var model = base.Can_build_a_model_with_default_conventions_without_DI();

            Assert.Equal("ProductTable", model.GetEntityTypes().Single().SqlServer().TableName);

            return model;
        }

        protected override ConventionSet GetConventionSet() => SqlServerConventionSetBuilder.Build();
    }
}
