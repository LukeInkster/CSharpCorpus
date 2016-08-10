// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Tests;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Relational.Tests
{
    public class RelationalDatabaseFacadeExtensionsTest
    {
        [Fact]
        public void GetDbConnection_returns_the_current_connection()
        {
            var dbConnection = Mock.Of<DbConnection>();

            var connectionMock = new Mock<IRelationalConnection>();
            connectionMock.SetupGet(m => m.DbConnection).Returns(dbConnection);

            var context = RelationalTestHelpers.Instance.CreateContext(
                new ServiceCollection().AddSingleton(connectionMock.Object));

            Assert.Same(dbConnection, context.Database.GetDbConnection());
        }

        [Fact]
        public void Relational_specific_methods_throws_when_non_relational_provider_is_in_use()
        {
            var context = RelationalTestHelpers.Instance.CreateContext();

            Assert.Equal(
                RelationalStrings.RelationalNotInUse,
                Assert.Throws<InvalidOperationException>(() => context.Database.GetDbConnection()).Message);
        }

        [Fact]
        public void Can_open_the_underlying_connection()
        {
            var connectionMock = new Mock<IRelationalConnection>();

            var context = RelationalTestHelpers.Instance.CreateContext(
                new ServiceCollection().AddSingleton(connectionMock.Object));

            context.Database.OpenConnection();

            connectionMock.Verify(m => m.Open(), Times.Once);
        }

        [Fact]
        public void Can_open_the_underlying_connection_async()
        {
            var connectionMock = new Mock<IRelationalConnection>();

            var context = RelationalTestHelpers.Instance.CreateContext(
                new ServiceCollection().AddSingleton(connectionMock.Object));

            var cancellationToken = new CancellationToken();

            context.Database.OpenConnectionAsync(cancellationToken);

            connectionMock.Verify(m => m.OpenAsync(cancellationToken), Times.Once);
        }

        [Fact]
        public void Can_close_the_underlying_connection()
        {
            var connectionMock = new Mock<IRelationalConnection>();

            var context = RelationalTestHelpers.Instance.CreateContext(
                new ServiceCollection().AddSingleton(connectionMock.Object));

            context.Database.CloseConnection();

            connectionMock.Verify(m => m.Close(), Times.Once);
        }

        [Fact]
        public void Can_begin_transaction_with_isolation_level()
        {
            var transactionManagerMock = new Mock<IRelationalTransactionManager>();
            var transaction = Mock.Of<IDbContextTransaction>();

            transactionManagerMock.Setup(m => m.BeginTransaction(It.IsAny<IsolationLevel>())).Returns(transaction);

            var context = RelationalTestHelpers.Instance.CreateContext(
                new ServiceCollection().AddSingleton<IDbContextTransactionManager>(transactionManagerMock.Object));

            var isolationLevel = IsolationLevel.Chaos;

            Assert.Same(transaction, context.Database.BeginTransaction(isolationLevel));

            transactionManagerMock.Verify(m => m.BeginTransaction(isolationLevel), Times.Once);
        }

        [Fact]
        public void Can_begin_transaction_with_isolation_level_async()
        {
            var transactionManagerMock = new Mock<IRelationalTransactionManager>();
            var transaction = Mock.Of<IDbContextTransaction>();

            var transactionTask = new Task<IDbContextTransaction>(() => transaction);

            transactionManagerMock.Setup(m => m.BeginTransactionAsync(It.IsAny<IsolationLevel>(), It.IsAny<CancellationToken>()))
                .Returns(transactionTask);

            var context = RelationalTestHelpers.Instance.CreateContext(
                new ServiceCollection().AddSingleton<IDbContextTransactionManager>(transactionManagerMock.Object));

            var cancellationToken = new CancellationToken();
            var isolationLevel = IsolationLevel.Chaos;

            Assert.Same(transactionTask, context.Database.BeginTransactionAsync(isolationLevel, cancellationToken));

            transactionManagerMock.Verify(m => m.BeginTransactionAsync(isolationLevel, cancellationToken), Times.Once);
        }

        [Fact]
        public void Can_use_transaction()
        {
            var transactionManagerMock = new Mock<IRelationalTransactionManager>();
            var dbTransaction = Mock.Of<DbTransaction>();
            var transaction = Mock.Of<IDbContextTransaction>();

            transactionManagerMock.Setup(m => m.UseTransaction(dbTransaction)).Returns(transaction);

            var context = RelationalTestHelpers.Instance.CreateContext(
                new ServiceCollection().AddSingleton<IDbContextTransactionManager>(transactionManagerMock.Object));

            Assert.Same(transaction, context.Database.UseTransaction(dbTransaction));

            transactionManagerMock.Verify(m => m.UseTransaction(dbTransaction), Times.Once);
        }

        [Fact]
        public void Begin_transaction_ignores_isolation_level_on_non_relational_provider()
        {
            var transactionManagerMock = new Mock<IDbContextTransactionManager>();
            var transaction = Mock.Of<IDbContextTransaction>();

            transactionManagerMock.Setup(m => m.BeginTransaction()).Returns(transaction);

            var context = RelationalTestHelpers.Instance.CreateContext(
                new ServiceCollection().AddSingleton(transactionManagerMock.Object));

            var isolationLevel = IsolationLevel.Chaos;

            Assert.Same(transaction, context.Database.BeginTransaction(isolationLevel));

            transactionManagerMock.Verify(m => m.BeginTransaction(), Times.Once);
        }

        [Fact]
        public void Begin_transaction_ignores_isolation_level_on_non_relational_provider_async()
        {
            var transactionManagerMock = new Mock<IDbContextTransactionManager>();
            var transaction = Mock.Of<IDbContextTransaction>();

            var transactionTask = new Task<IDbContextTransaction>(() => transaction);

            transactionManagerMock.Setup(m => m.BeginTransactionAsync(It.IsAny<CancellationToken>()))
                .Returns(transactionTask);

            var context = RelationalTestHelpers.Instance.CreateContext(
                new ServiceCollection().AddSingleton(transactionManagerMock.Object));

            var cancellationToken = new CancellationToken();
            var isolationLevel = IsolationLevel.Chaos;

            Assert.Same(transactionTask, context.Database.BeginTransactionAsync(isolationLevel, cancellationToken));

            transactionManagerMock.Verify(m => m.BeginTransactionAsync(cancellationToken), Times.Once);
        }

        [Fact]
        public void use_transaction_throws_on_non_relational_provider()
        {
            var transactionManagerMock = new Mock<IDbContextTransactionManager>();
            var dbTransaction = Mock.Of<DbTransaction>();
            var transaction = Mock.Of<IDbContextTransaction>();

            transactionManagerMock.Setup(m => m.BeginTransaction()).Returns(transaction);

            var context = RelationalTestHelpers.Instance.CreateContext(
                new ServiceCollection().AddSingleton(transactionManagerMock.Object));

            Assert.Equal(
                RelationalStrings.RelationalNotInUse,
                Assert.Throws<InvalidOperationException>(
                    () => context.Database.UseTransaction(dbTransaction)).Message);
        }
    }
}
