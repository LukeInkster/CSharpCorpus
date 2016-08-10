// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;

namespace Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.GearsOfWarModel
{
    public class Squad
    {
        public Squad()
        {
            Members = new List<Gear>();
        }

        // non-auto generated key
        public int Id { get; set; }
        public string Name { get; set; }

        // auto-generated non-key (sequence)
        public int InternalNumber { get; set; }

        public virtual ICollection<Gear> Members { get; set; }
    }
}
