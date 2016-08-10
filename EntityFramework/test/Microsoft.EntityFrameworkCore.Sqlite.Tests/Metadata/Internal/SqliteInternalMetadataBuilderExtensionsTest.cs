// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Sqlite.Tests.Metadata.Internal
{
    public class SqliteInternalMetadataBuilderExtensionsTest
    {
        private InternalModelBuilder CreateBuilder()
            => new InternalModelBuilder(new Model());

        [Fact]
        public void Can_access_model()
        {
            var builder = CreateBuilder();

            builder.Sqlite(ConfigurationSource.Convention).GetOrAddSequence("Mine").IncrementBy = 77;

            Assert.Equal(77, builder.Metadata.Sqlite().FindSequence("Mine").IncrementBy);

            Assert.Equal(1, builder.Metadata.GetAnnotations().Count(
                a => a.Name.StartsWith(SqliteAnnotationNames.Prefix, StringComparison.Ordinal)));
        }

        [Fact]
        public void Can_access_entity_type()
        {
            var typeBuilder = CreateBuilder().Entity(typeof(Splot), ConfigurationSource.Convention);

            Assert.True(typeBuilder.Sqlite(ConfigurationSource.Convention).ToTable("Splew"));
            Assert.Equal("Splew", typeBuilder.Metadata.Sqlite().TableName);

            Assert.True(typeBuilder.Sqlite(ConfigurationSource.DataAnnotation).ToTable("Splow"));
            Assert.Equal("Splow", typeBuilder.Metadata.Sqlite().TableName);

            Assert.False(typeBuilder.Sqlite(ConfigurationSource.Convention).ToTable("Splod"));
            Assert.Equal("Splow", typeBuilder.Metadata.Sqlite().TableName);

            Assert.Equal(1, typeBuilder.Metadata.GetAnnotations().Count(
                a => a.Name.StartsWith(SqliteAnnotationNames.Prefix, StringComparison.Ordinal)));
        }

        [Fact]
        public void Can_access_property()
        {
            var propertyBuilder = CreateBuilder()
                .Entity(typeof(Splot), ConfigurationSource.Convention)
                .Property("Id", typeof(int), ConfigurationSource.Convention);

            Assert.True(propertyBuilder.Sqlite(ConfigurationSource.Convention).HasColumnName("Splew"));
            Assert.Equal("Splew", propertyBuilder.Metadata.Sqlite().ColumnName);

            Assert.True(propertyBuilder.Sqlite(ConfigurationSource.DataAnnotation).HasColumnName("Splow"));
            Assert.Equal("Splow", propertyBuilder.Metadata.Sqlite().ColumnName);

            Assert.False(propertyBuilder.Sqlite(ConfigurationSource.Convention).HasColumnName("Splod"));
            Assert.Equal("Splow", propertyBuilder.Metadata.Sqlite().ColumnName);

            Assert.Equal(1, propertyBuilder.Metadata.GetAnnotations().Count(
                a => a.Name.StartsWith(SqliteAnnotationNames.Prefix, StringComparison.Ordinal)));
        }

        [Fact]
        public void Can_access_key()
        {
            var modelBuilder = CreateBuilder();
            var entityTypeBuilder = modelBuilder.Entity(typeof(Splot), ConfigurationSource.Convention);
            var idProperty = entityTypeBuilder.Property("Id", typeof(string), ConfigurationSource.Convention).Metadata;
            var keyBuilder = entityTypeBuilder.HasKey(new[] { idProperty.Name }, ConfigurationSource.Convention);

            Assert.True(keyBuilder.Sqlite(ConfigurationSource.Convention).HasName("Splew"));
            Assert.Equal("Splew", keyBuilder.Metadata.Sqlite().Name);

            Assert.True(keyBuilder.Sqlite(ConfigurationSource.DataAnnotation).HasName("Splow"));
            Assert.Equal("Splow", keyBuilder.Metadata.Sqlite().Name);

            Assert.False(keyBuilder.Sqlite(ConfigurationSource.Convention).HasName("Splod"));
            Assert.Equal("Splow", keyBuilder.Metadata.Sqlite().Name);

            Assert.Equal(1, keyBuilder.Metadata.GetAnnotations().Count(
                a => a.Name.StartsWith(SqliteAnnotationNames.Prefix, StringComparison.Ordinal)));
        }

        [Fact]
        public void Can_access_index()
        {
            var modelBuilder = CreateBuilder();
            var entityTypeBuilder = modelBuilder.Entity(typeof(Splot), ConfigurationSource.Convention);
            entityTypeBuilder.Property("Id", typeof(int), ConfigurationSource.Convention);
            var indexBuilder = entityTypeBuilder.HasIndex(new[] { "Id" }, ConfigurationSource.Convention);

            indexBuilder.Sqlite(ConfigurationSource.Convention).HasName("Splew");
            Assert.Equal("Splew", indexBuilder.Metadata.Sqlite().Name);

            indexBuilder.Sqlite(ConfigurationSource.DataAnnotation).HasName("Splow");
            Assert.Equal("Splow", indexBuilder.Metadata.Sqlite().Name);

            indexBuilder.Sqlite(ConfigurationSource.Convention).HasName("Splod");
            Assert.Equal("Splow", indexBuilder.Metadata.Sqlite().Name);

            Assert.Equal(1, indexBuilder.Metadata.GetAnnotations().Count(
                a => a.Name.StartsWith(SqliteAnnotationNames.Prefix, StringComparison.Ordinal)));
        }

        [Fact]
        public void Can_access_relationship()
        {
            var modelBuilder = CreateBuilder();
            var entityTypeBuilder = modelBuilder.Entity(typeof(Splot), ConfigurationSource.Convention);
            entityTypeBuilder.Property("Id", typeof(int), ConfigurationSource.Convention);
            var relationshipBuilder = entityTypeBuilder.HasForeignKey("Splot", new[] { "Id" }, ConfigurationSource.Convention);

            Assert.True(relationshipBuilder.Sqlite(ConfigurationSource.Convention).HasConstraintName("Splew"));
            Assert.Equal("Splew", relationshipBuilder.Metadata.Sqlite().Name);

            Assert.True(relationshipBuilder.Sqlite(ConfigurationSource.DataAnnotation).HasConstraintName("Splow"));
            Assert.Equal("Splow", relationshipBuilder.Metadata.Sqlite().Name);

            Assert.False(relationshipBuilder.Sqlite(ConfigurationSource.Convention).HasConstraintName("Splod"));
            Assert.Equal("Splow", relationshipBuilder.Metadata.Sqlite().Name);

            Assert.Equal(1, relationshipBuilder.Metadata.GetAnnotations().Count(
                a => a.Name.StartsWith(SqliteAnnotationNames.Prefix, StringComparison.Ordinal)));
        }

        private class Splot
        {
        }
    }
}
