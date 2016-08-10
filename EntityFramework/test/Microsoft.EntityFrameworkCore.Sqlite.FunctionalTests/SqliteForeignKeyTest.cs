// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Sqlite.FunctionalTests
{
    public class SqliteForeignKeyTest : IDisposable
    {
        private readonly SqliteTestStore _testStore;

        public SqliteForeignKeyTest()
        {
            _testStore = SqliteTestStore.CreateScratch();
        }

        public void Dispose() => _testStore.Dispose();

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public void It_enforces_foreign_key(bool suppress)
        {
            var options = new DbContextOptionsBuilder()
                .UseSqlite(_testStore.ConnectionString,
                    b =>
                        {
                            if (suppress)
                            {
                                b.SuppressForeignKeyEnforcement();
                            }
                        }).Options;

            using (var context = new MyContext(options))
            {
                context.Database.EnsureClean();
                context.Add(new Child { ParentId = 4 });
                if (suppress)
                {
                    context.SaveChanges();
                }
                else
                {
                    var ex = Assert.Throws<DbUpdateException>(() => { context.SaveChanges(); });
                    Assert.Contains("FOREIGN KEY constraint failed", ex.InnerException.Message, StringComparison.OrdinalIgnoreCase);
                }
            }
        }

        public class MyContext : DbContext
        {
            public MyContext(DbContextOptions options)
                : base(options)
            {
            }

            public DbSet<Parent> Parents { get; set; }
            public DbSet<Child> Children { get; set; }

            protected override void OnModelCreating(ModelBuilder modelBuilder)
            {
                modelBuilder.Entity<Parent>()
                    .HasMany(b => b.Children)
                    .WithOne(b => b.MyParent)
                    .HasForeignKey(b => b.ParentId);
            }
        }

        public class Child
        {
            public int Id { get; set; }
            public int ParentId { get; set; }
            public Parent MyParent { get; set; }
        }

        public class Parent
        {
            public int Id { get; set; }
            public ICollection<Child> Children { get; set; }
        }

        [Fact]
        public void It_allows_foreign_key_to_unique_index()
        {
            var builder = new DbContextOptionsBuilder();
            var sqliteBuilder = builder.UseSqlite(_testStore.ConnectionString);
            var options = builder.Options;
            _testStore.ExecuteNonQuery(@"
CREATE TABLE User (
    Id INTEGER PRIMARY KEY,
    AltId INTEGER NOT NULL UNIQUE
);
CREATE TABLE Comment (
    Id INTEGER PRIMARY KEY,
    UserAltId INTEGER NOT NULL,
    Comment TEXT,
    FOREIGN KEY (UserAltId) REFERENCES User (AltId)
);");

            long id;

            using (var context = new BloggingContext(options))
            {
                var entry = context.User.Add(new User { AltId = 1356524 });
                context.Comments.Add(new Comment { User = entry.Entity });
                context.SaveChanges();
                id = entry.Entity.Id;
            }

            using (var context = new BloggingContext(options))
            {
                var comment = context.Comments.Include(u => u.User).Single();
                Assert.Equal(id, comment.User.Id);
            }
        }
    }

    internal class BloggingContext : DbContext
    {
        public BloggingContext(DbContextOptions options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Comment>(entity =>
                {
                    entity.ToTable("Comment");

                    entity.HasOne(d => d.User)
                        .WithMany(p => p.Comments)
                        .HasPrincipalKey(p => p.AltId)
                        .HasForeignKey(d => d.UserAltId);
                });

            modelBuilder.Entity<User>(entity => { entity.HasAlternateKey(e => e.AltId); });
        }

        public virtual DbSet<Comment> Comments { get; set; }
        public virtual DbSet<User> User { get; set; }
    }

    internal class Comment
    {
        public long Id { get; set; }

        public int UserAltId { get; set; }

        public virtual User User { get; set; }
    }

    internal class User
    {
        public long Id { get; set; }

        public int AltId { get; set; }

        public virtual ICollection<Comment> Comments { get; set; }
    }
}
