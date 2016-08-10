// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.EntityFrameworkCore.CrossStore.FunctionalTests.TestModels
{
    public class SimpleEntity
    {
        public static string ShadowPropertyName = "ShadowStringProperty";
        public static string ShadowPartitionIdName = "ShadowPartitionIdProperty";

        public virtual int Id { get; set; }

        public virtual string StringProperty { get; set; }
    }
}
