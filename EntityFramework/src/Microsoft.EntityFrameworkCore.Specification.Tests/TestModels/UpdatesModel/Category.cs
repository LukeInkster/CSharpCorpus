﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.UpdatesModel
{
    public class Category
    {
        public int Id { get; set; }
        public int? PrincipalId { get; set; }
        public string Name { get; set; }
    }
}
