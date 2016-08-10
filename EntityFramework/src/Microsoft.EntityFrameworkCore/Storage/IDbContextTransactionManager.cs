// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.EntityFrameworkCore.Storage
{
    /// <summary>
    ///     <para>
    ///         Creates and manages the current transaction.
    ///     </para>
    ///     <para>
    ///         This interface is typically used by database providers (and other extensions). It is generally
    ///         not used in application code.
    ///     </para>
    /// </summary>
    public interface IDbContextTransactionManager
    {
        /// <summary>
        ///     Begins a new transaction.
        /// </summary>
        /// <returns> The newly created transaction. </returns>
        IDbContextTransaction BeginTransaction();

        /// <summary>
        ///     Asynchronously begins a new transaction.
        /// </summary>
        /// <param name="cancellationToken">A <see cref="CancellationToken" /> to observe while waiting for the task to complete.</param>
        /// <returns> 
        ///     A task that represents the asynchronous operation. The task result contains the newly created transaction. 
        /// </returns>
        Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default(CancellationToken));

        /// <summary>
        ///     Commits all changes made to the database in the current transaction.
        /// </summary>
        void CommitTransaction();

        /// <summary>
        ///     Discards all changes made to the database in the current transaction.
        /// </summary>
        void RollbackTransaction();

        /// <summary>
        ///     Gets the current transaction.
        /// </summary>
        IDbContextTransaction CurrentTransaction { get; }
    }
}
