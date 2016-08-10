﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;

namespace Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.UpdatesModel
{
    public class Product
    {
        public Guid Id { get; set; }
        public int? DependentId { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
    }
}
