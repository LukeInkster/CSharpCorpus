// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

namespace Microsoft.EntityFrameworkCore.Metadata
{
    public interface IRelationalEntityTypeAnnotations
    {
        string TableName { get; }
        string Schema { get; }
        IProperty DiscriminatorProperty { get; }
        object DiscriminatorValue { get; }
    }
}
