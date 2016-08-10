// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using JetBrains.Annotations;

namespace Microsoft.EntityFrameworkCore.Migrations.Operations
{
    public class CreateSequenceOperation : MigrationOperation
    {
        public virtual string Schema { get; [param: CanBeNull] set; }
        public virtual string Name { get; [param: NotNull] set; }
        public virtual Type ClrType { get; [param: NotNull] set; }
        public virtual long StartValue { get; set; } = 1L;
        public virtual int IncrementBy { get; set; } = 1;
        public virtual long? MaxValue { get; [param: CanBeNull] set; }
        public virtual long? MinValue { get; [param: CanBeNull] set; }
        public virtual bool IsCyclic { get; set; }
    }
}
