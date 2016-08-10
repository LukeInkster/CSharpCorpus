// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Scaffolding;
using Microsoft.EntityFrameworkCore.Scaffolding.Internal;
using Microsoft.EntityFrameworkCore.Scaffolding.Metadata;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace Microsoft.EntityFrameworkCore.Tests
{
    public abstract class RelationalDatabaseCleaner
    {
        protected abstract IInternalDatabaseModelFactory CreateDatabaseModelFactory(ILoggerFactory loggerFactory);

        protected virtual bool AcceptTable(TableModel table) => true;

        protected virtual bool AcceptForeignKey(ForeignKeyModel foreignKey) => true;

        protected virtual bool AcceptIndex(IndexModel index) => true;

        protected virtual bool AcceptSequence(SequenceModel sequence) => true;

        protected virtual string BuildCustomSql(DatabaseModel databaseModel) => null;

        public virtual void Clean(DatabaseFacade facade)
        {
            var creator = facade.GetService<IRelationalDatabaseCreator>();
            var sqlGenerator = facade.GetService<IMigrationsSqlGenerator>();
            var executor = facade.GetService<IMigrationCommandExecutor>();
            var connection = facade.GetService<IRelationalConnection>();
            var sqlBuilder = facade.GetService<IRawSqlCommandBuilder>();
            var loggerFactory = facade.GetService<ILoggerFactory>();

            if (!creator.Exists())
            {
                creator.Create();
            }
            else
            {
                var databaseModelFactory = CreateDatabaseModelFactory(loggerFactory);
                var databaseModel = databaseModelFactory.Create(connection.DbConnection, TableSelectionSet.All);

                var operations = new List<MigrationOperation>();

                foreach (var index in databaseModel.Tables
                    .SelectMany(t => t.Indexes.Where(AcceptIndex)))
                {
                    operations.Add(new DropIndexOperation
                    {
                        Name = index.Name,
                        Table = index.Table.Name,
                        Schema = index.Table.SchemaName
                    });
                }

                foreach (var foreignKey in databaseModel.Tables
                    .SelectMany(t => t.ForeignKeys.Where(AcceptForeignKey)))
                {
                    operations.Add(new DropForeignKeyOperation
                    {
                        Name = foreignKey.Name,
                        Table = foreignKey.Table.Name,
                        Schema = foreignKey.Table.SchemaName
                    });
                }

                foreach (var table in databaseModel.Tables.Where(AcceptTable))
                {
                    operations.Add(new DropTableOperation
                    {
                        Name = table.Name,
                        Schema = table.SchemaName
                    });
                }

                foreach (var sequence in databaseModel.Sequences.Where(AcceptSequence))
                {
                    operations.Add(new DropSequenceOperation
                    {
                        Name = sequence.Name,
                        Schema = sequence.SchemaName
                    });
                }

                var commands = sqlGenerator.Generate(operations);

                connection.Open();

                try
                {
                    var customSql = BuildCustomSql(databaseModel);
                    if (!string.IsNullOrWhiteSpace(customSql))
                    {
                        sqlBuilder.Build(customSql).ExecuteNonQuery(connection);
                    }
                    executor.ExecuteNonQuery(commands, connection);
                }
                finally
                {
                    connection.Close();
                }
            }

            creator.CreateTables();
        }
    }
}
