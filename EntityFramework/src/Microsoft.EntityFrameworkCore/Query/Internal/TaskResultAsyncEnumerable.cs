// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Utilities;

namespace Microsoft.EntityFrameworkCore.Query.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public sealed class TaskResultAsyncEnumerable<T> : IAsyncEnumerable<T>
    {
        private readonly Task<T> _task;

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public TaskResultAsyncEnumerable([NotNull] Task<T> task)
        {
            Check.NotNull(task, nameof(task));

            _task = task;
        }

        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        public IAsyncEnumerator<T> GetEnumerator() => new Enumerator(_task);

        private sealed class Enumerator : IAsyncEnumerator<T>
        {
            private readonly Task<T> _task;
            private bool _moved;

            public Enumerator(Task<T> task)
            {
                _task = task;
            }

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!_moved)
                {
                    await _task;

                    _moved = true;

                    return _moved;
                }

                return false;
            }

            public T Current => !_moved ? default(T) : _task.Result;

            void IDisposable.Dispose()
            {
            }
        }
    }
}
