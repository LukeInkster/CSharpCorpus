// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Data.Common;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Specification.Tests
{
    public abstract class MigrationsTestBase<TFixture> : IClassFixture<TFixture>
        where TFixture : MigrationsFixtureBase, new()
    {
        private readonly TFixture _fixture;

        protected MigrationsTestBase(TFixture fixture)
        {
            _fixture = fixture;
        }

        protected string Sql { get; private set; }

        protected string ActiveProvider { get; private set; }

        [Fact]
        public void Can_apply_all_migrations()
        {
            using (var db = _fixture.CreateContext())
            {
                db.Database.EnsureDeleted();

                db.Database.Migrate();

                var history = db.GetInfrastructure().GetRequiredService<IHistoryRepository>();
                Assert.Collection(
                    history.GetAppliedMigrations(),
                    x => Assert.Equal("00000000000001_Migration1", x.MigrationId),
                    x => Assert.Equal("00000000000002_Migration2", x.MigrationId),
                    x => Assert.Equal("00000000000003_Migration3", x.MigrationId));
            }
        }

        [Fact]
        public void Can_apply_one_migration()
        {
            using (var db = _fixture.CreateContext())
            {
                db.Database.EnsureDeleted();

                var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
                migrator.Migrate("Migration1");

                var history = db.GetInfrastructure().GetRequiredService<IHistoryRepository>();
                Assert.Collection(
                    history.GetAppliedMigrations(),
                    x => Assert.Equal("00000000000001_Migration1", x.MigrationId));
            }
        }

        [Fact]
        public void Can_revert_all_migrations()
        {
            using (var db = _fixture.CreateContext())
            {
                db.Database.EnsureDeleted();
                db.Database.Migrate();

                var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
                migrator.Migrate(Migration.InitialDatabase);

                var history = db.GetInfrastructure().GetRequiredService<IHistoryRepository>();
                Assert.Empty(history.GetAppliedMigrations());
            }
        }

        [Fact]
        public void Can_revert_one_migrations()
        {
            using (var db = _fixture.CreateContext())
            {
                db.Database.EnsureDeleted();
                db.Database.Migrate();

                var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
                migrator.Migrate("Migration1");

                var history = db.GetInfrastructure().GetRequiredService<IHistoryRepository>();
                Assert.Collection(
                    history.GetAppliedMigrations(),
                    x => Assert.Equal("00000000000001_Migration1", x.MigrationId));
            }
        }

        [Fact]
        public async Task Can_apply_all_migrations_async()
        {
            using (var db = _fixture.CreateContext())
            {
                await db.Database.EnsureDeletedAsync();

                await db.Database.MigrateAsync();

                var history = db.GetInfrastructure().GetRequiredService<IHistoryRepository>();
                Assert.Collection(
                    await history.GetAppliedMigrationsAsync(),
                    x => Assert.Equal("00000000000001_Migration1", x.MigrationId),
                    x => Assert.Equal("00000000000002_Migration2", x.MigrationId),
                    x => Assert.Equal("00000000000003_Migration3", x.MigrationId));
            }
        }

        [Fact]
        public virtual void Can_generate_no_migration_script()
        {
            using (var db = _fixture.CreateEmptyContext())
            {
                var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

                SetSql(migrator.GenerateScript());
            }
        }

        [Fact]
        public virtual void Can_generate_migration_from_initial_database_to_initial()
        {
            using (var db = _fixture.CreateContext())
            {
                var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

                SetSql(migrator.GenerateScript(fromMigration: Migration.InitialDatabase, toMigration: Migration.InitialDatabase));
            }
        }

        [Fact]
        public virtual void Can_generate_up_scripts()
        {
            using (var db = _fixture.CreateContext())
            {
                var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

                SetSql(migrator.GenerateScript());
            }
        }

        [Fact]
        public virtual void Can_generate_idempotent_up_scripts()
        {
            using (var db = _fixture.CreateContext())
            {
                var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

                SetSql(migrator.GenerateScript(idempotent: true));
            }
        }

        [Fact]
        public virtual void Can_generate_down_scripts()
        {
            using (var db = _fixture.CreateContext())
            {
                var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

                SetSql(
                    migrator.GenerateScript(
                        fromMigration: "Migration2",
                        toMigration: Migration.InitialDatabase));
            }
        }

        [Fact]
        public virtual void Can_generate_idempotent_down_scripts()
        {
            using (var db = _fixture.CreateContext())
            {
                var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();

                SetSql(
                    migrator.GenerateScript(
                        fromMigration: "Migration2",
                        toMigration: Migration.InitialDatabase,
                        idempotent: true));
            }
        }

        [Fact]
        public virtual void Can_get_active_provider()
        {
            using (var db = _fixture.CreateContext())
            {
                var migrator = db.GetInfrastructure().GetRequiredService<IMigrator>();
                MigrationsFixtureBase.ActiveProvider = null;

                migrator.GenerateScript(toMigration: "Migration1");

                ActiveProvider = MigrationsFixtureBase.ActiveProvider;
            }
        }

        /// <remarks>
        ///     Creating databases and executing DDL is slow. This oddly-structured test allows us to get the most ammount of
        ///     coverage using the least ammount of database operations.
        /// </remarks>
        [Fact]
        public virtual async Task Can_execute_operations()
        {
            using (var db = _fixture.CreateContext())
            {
                await db.Database.EnsureDeletedAsync();
                await db.Database.EnsureCreatedAsync();

                var services = db.GetInfrastructure();
                var connection = db.Database.GetDbConnection();

                await db.Database.OpenConnectionAsync();

                await ExecuteAsync(services, BuildFirstMigration);
                await AssertFirstMigrationAsync(connection);
                await ExecuteAsync(services, BuildSecondMigration);
                await AssertSecondMigrationAsync(connection);
            }
        }

        protected virtual async Task ExecuteAsync(IServiceProvider services, Action<MigrationBuilder> buildMigration)
        {
            var generator = services.GetRequiredService<IMigrationsSqlGenerator>();
            var executor = services.GetRequiredService<IMigrationCommandExecutor>();
            var connection = services.GetRequiredService<IRelationalConnection>();
            var providerServices = services.GetRequiredService<IDatabaseProviderServices>();

            var migrationBuilder = new MigrationBuilder(providerServices.InvariantName);
            buildMigration(migrationBuilder);
            var operations = migrationBuilder.Operations.ToList();

            var commandList = generator.Generate(operations, model: null);

            await executor.ExecuteNonQueryAsync(commandList, connection);
        }

        protected virtual void BuildFirstMigration(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CreatedTable",
                columns: x => new
                {
                    Id = x.Column<int>(nullable: false),
                    ColumnWithDefaultToDrop = x.Column<int>(nullable: true, defaultValue: 0),
                    ColumnWithDefaultToAlter = x.Column<int>(nullable: true, defaultValue: 1)
                },
                constraints: x =>
                    {
                        x.PrimaryKey(
                            name: "PK_CreatedTable",
                            columns: t => t.Id);
                    });
        }

        protected virtual Task AssertFirstMigrationAsync(DbConnection connection)
        {
            AssertFirstMigration(connection);

            return Task.FromResult(0);
        }

        protected virtual void AssertFirstMigration(DbConnection connection)
        {
        }

        protected virtual void BuildSecondMigration(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ColumnWithDefaultToDrop",
                table: "CreatedTable");
            migrationBuilder.AlterColumn<int>(
                name: "ColumnWithDefaultToAlter",
                table: "CreatedTable",
                nullable: true);
        }

        protected virtual Task AssertSecondMigrationAsync(DbConnection connection)
        {
            AssertSecondMigration(connection);

            return Task.FromResult(0);
        }

        protected virtual void AssertSecondMigration(DbConnection connection)
        {
        }

        private void SetSql(string value) => Sql = value.Replace(ProductInfo.GetVersion(), "7.0.0-test");
    }
}
