// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.Migrations.Operations.Builders
{
    public class OperationBuilder<TOperation> : IInfrastructure<TOperation>
        where TOperation : MigrationOperation
    {
        public OperationBuilder([NotNull] TOperation operation)
        {
            Check.NotNull(operation, nameof(operation));

            Operation = operation;
        }

        protected virtual TOperation Operation { get; }

        TOperation IInfrastructure<TOperation>.Instance => Operation;

        public virtual OperationBuilder<TOperation> Annotation(
            [NotNull] string name,
            [NotNull] object value)
        {
            Check.NotEmpty(name, nameof(name));
            Check.NotNull(value, nameof(value));

            Operation.AddAnnotation(name, value);

            return this;
        }
    }
}
