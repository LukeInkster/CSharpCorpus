// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.InheritanceRelationships
{
    public class BaseCollectionOnBase
    {
        [NotMapped]
        public int Id { get; set; }

        public string Name { get; set; }

        public int? BaseParentId { get; set; }
        public BaseInheritanceRelationshipEntity BaseParent { get; set; }

        [NotMapped]
        public NestedReferenceBase NestedReference { get; set; }

        [NotMapped]
        public List<NestedCollectionBase> NestedCollection { get; set; }
    }
}
