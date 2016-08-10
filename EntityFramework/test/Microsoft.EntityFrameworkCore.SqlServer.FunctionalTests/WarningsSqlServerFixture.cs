﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Microsoft.EntityFrameworkCore.SqlServer.FunctionalTests
{
    public class WarningsSqlServerFixture : NorthwindQuerySqlServerFixture
    {
        protected override DbContextOptionsBuilder ConfigureOptions(
            DbContextOptionsBuilder dbContextOptionsBuilder)
            => dbContextOptionsBuilder.ConfigureWarnings(c =>
                c.Throw(RelationalEventId.QueryClientEvaluationWarning));
    }
}
