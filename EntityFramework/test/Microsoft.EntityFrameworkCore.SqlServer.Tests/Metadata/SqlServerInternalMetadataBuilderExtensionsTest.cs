// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Xunit;

namespace Microsoft.EntityFrameworkCore.SqlServer.Tests.Metadata
{
    public class SqlServerInternalMetadataBuilderExtensionsTest
    {
        private InternalModelBuilder CreateBuilder()
            => new InternalModelBuilder(new Model());

        [Fact]
        public void Can_access_model()
        {
            var builder = CreateBuilder();

            Assert.True(builder.SqlServer(ConfigurationSource.Convention).ValueGenerationStrategy(SqlServerValueGenerationStrategy.SequenceHiLo));
            Assert.Equal(SqlServerValueGenerationStrategy.SequenceHiLo, builder.Metadata.SqlServer().ValueGenerationStrategy);

            Assert.True(builder.SqlServer(ConfigurationSource.DataAnnotation).ValueGenerationStrategy(SqlServerValueGenerationStrategy.IdentityColumn));
            Assert.Equal(SqlServerValueGenerationStrategy.IdentityColumn, builder.Metadata.SqlServer().ValueGenerationStrategy);

            Assert.False(builder.SqlServer(ConfigurationSource.Convention).ValueGenerationStrategy(SqlServerValueGenerationStrategy.SequenceHiLo));
            Assert.Equal(SqlServerValueGenerationStrategy.IdentityColumn, builder.Metadata.SqlServer().ValueGenerationStrategy);

            Assert.Equal(1, builder.Metadata.GetAnnotations().Count(
                a => a.Name.StartsWith(SqlServerAnnotationNames.Prefix, StringComparison.Ordinal)));
        }

        [Fact]
        public void Can_access_entity_type()
        {
            var typeBuilder = CreateBuilder().Entity(typeof(Splot), ConfigurationSource.Convention);

            Assert.True(typeBuilder.SqlServer(ConfigurationSource.Convention).ToTable("Splew"));
            Assert.Equal("Splew", typeBuilder.Metadata.SqlServer().TableName);

            Assert.True(typeBuilder.SqlServer(ConfigurationSource.DataAnnotation).ToTable("Splow"));
            Assert.Equal("Splow", typeBuilder.Metadata.SqlServer().TableName);

            Assert.False(typeBuilder.SqlServer(ConfigurationSource.Convention).ToTable("Splod"));
            Assert.Equal("Splow", typeBuilder.Metadata.SqlServer().TableName);

            Assert.Equal(1, typeBuilder.Metadata.GetAnnotations().Count(
                a => a.Name.StartsWith(SqlServerAnnotationNames.Prefix, StringComparison.Ordinal)));
        }

        [Fact]
        public void Can_access_property()
        {
            var propertyBuilder = CreateBuilder()
                .Entity(typeof(Splot), ConfigurationSource.Convention)
                .Property("Id", typeof(int), ConfigurationSource.Convention);

            Assert.True(propertyBuilder.SqlServer(ConfigurationSource.Convention).HiLoSequenceName("Splew"));
            Assert.Equal("Splew", propertyBuilder.Metadata.SqlServer().HiLoSequenceName);

            Assert.True(propertyBuilder.SqlServer(ConfigurationSource.DataAnnotation).HiLoSequenceName("Splow"));
            Assert.Equal("Splow", propertyBuilder.Metadata.SqlServer().HiLoSequenceName);

            Assert.False(propertyBuilder.SqlServer(ConfigurationSource.Convention).HiLoSequenceName("Splod"));
            Assert.Equal("Splow", propertyBuilder.Metadata.SqlServer().HiLoSequenceName);

            Assert.Equal(1, propertyBuilder.Metadata.GetAnnotations().Count(
                a => a.Name.StartsWith(SqlServerAnnotationNames.Prefix, StringComparison.Ordinal)));
        }

        [Fact]
        public void Can_access_key()
        {
            var modelBuilder = CreateBuilder();
            var entityTypeBuilder = modelBuilder.Entity(typeof(Splot), ConfigurationSource.Convention);
            var idProperty = entityTypeBuilder.Property("Id", typeof(string), ConfigurationSource.Convention).Metadata;
            var keyBuilder = entityTypeBuilder.HasKey(new[] { idProperty.Name }, ConfigurationSource.Convention);

            Assert.True(keyBuilder.SqlServer(ConfigurationSource.Convention).IsClustered(true));
            Assert.True(keyBuilder.Metadata.SqlServer().IsClustered);

            Assert.True(keyBuilder.SqlServer(ConfigurationSource.DataAnnotation).IsClustered(false));
            Assert.False(keyBuilder.Metadata.SqlServer().IsClustered);

            Assert.False(keyBuilder.SqlServer(ConfigurationSource.Convention).IsClustered(true));
            Assert.False(keyBuilder.Metadata.SqlServer().IsClustered);

            Assert.Equal(1, keyBuilder.Metadata.GetAnnotations().Count(
                a => a.Name.StartsWith(SqlServerAnnotationNames.Prefix, StringComparison.Ordinal)));
        }

        [Fact]
        public void Can_access_index()
        {
            var modelBuilder = CreateBuilder();
            var entityTypeBuilder = modelBuilder.Entity(typeof(Splot), ConfigurationSource.Convention);
            entityTypeBuilder.Property("Id", typeof(int), ConfigurationSource.Convention);
            var indexBuilder = entityTypeBuilder.HasIndex(new[] { "Id" }, ConfigurationSource.Convention);

            Assert.True(indexBuilder.SqlServer(ConfigurationSource.Convention).IsClustered(true));
            Assert.True(indexBuilder.Metadata.SqlServer().IsClustered);

            Assert.True(indexBuilder.SqlServer(ConfigurationSource.DataAnnotation).IsClustered(false));
            Assert.False(indexBuilder.Metadata.SqlServer().IsClustered);

            Assert.False(indexBuilder.SqlServer(ConfigurationSource.Convention).IsClustered(true));
            Assert.False(indexBuilder.Metadata.SqlServer().IsClustered);

            Assert.Equal(1, indexBuilder.Metadata.GetAnnotations().Count(
                a => a.Name.StartsWith(SqlServerAnnotationNames.Prefix, StringComparison.Ordinal)));
        }

        [Fact]
        public void Can_access_relationship()
        {
            var modelBuilder = CreateBuilder();
            var entityTypeBuilder = modelBuilder.Entity(typeof(Splot), ConfigurationSource.Convention);
            entityTypeBuilder.Property("Id", typeof(int), ConfigurationSource.Convention);
            var relationshipBuilder = entityTypeBuilder.HasForeignKey("Splot", new[] { "Id" }, ConfigurationSource.Convention);

            Assert.True(relationshipBuilder.SqlServer(ConfigurationSource.Convention).HasConstraintName("Splew"));
            Assert.Equal("Splew", relationshipBuilder.Metadata.SqlServer().Name);

            Assert.True(relationshipBuilder.SqlServer(ConfigurationSource.DataAnnotation).HasConstraintName("Splow"));
            Assert.Equal("Splow", relationshipBuilder.Metadata.SqlServer().Name);

            Assert.False(relationshipBuilder.SqlServer(ConfigurationSource.Convention).HasConstraintName("Splod"));
            Assert.Equal("Splow", relationshipBuilder.Metadata.SqlServer().Name);

            Assert.Equal(1, relationshipBuilder.Metadata.GetAnnotations().Count(
                a => a.Name.StartsWith(SqlServerAnnotationNames.Prefix, StringComparison.Ordinal)));
        }

        private class Splot
        {
        }
    }
}
