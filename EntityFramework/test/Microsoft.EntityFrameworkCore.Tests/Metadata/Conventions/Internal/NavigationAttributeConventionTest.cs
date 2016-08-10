// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Conventions;
using Microsoft.EntityFrameworkCore.Metadata.Conventions.Internal;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Xunit;

namespace Microsoft.EntityFrameworkCore.Tests.Metadata.Conventions.Internal
{
    public class NavigationAttributeConventionTest
    {
        #region NotMappedAttribute

        [Fact]
        public void NotMappedAttribute_overrides_configuration_from_convention_source()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<BlogDetails>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Blog), ConfigurationSource.Convention);

            dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                nameof(BlogDetails.Blog),
                nameof(Blog.BlogDetails),
                ConfigurationSource.Convention);

            Assert.Contains(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Blog.BlogDetails));
            Assert.Contains(dependentEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(BlogDetails.Blog));

            new NotMappedMemberAttributeConvention().Apply(principalEntityTypeBuilder);

            Assert.DoesNotContain(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Blog.BlogDetails));
            Assert.DoesNotContain(dependentEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(BlogDetails.Blog));

            new RelationshipDiscoveryConvention().Apply(dependentEntityTypeBuilder);

            Assert.DoesNotContain(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Blog.BlogDetails));
            Assert.Contains(dependentEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(BlogDetails.Blog));
        }

        [Fact]
        public void NotMappedAttribute_does_not_override_configuration_from_explicit_source()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<BlogDetails>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Blog), ConfigurationSource.Convention);

            dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                nameof(BlogDetails.Blog),
                nameof(Blog.BlogDetails),
                ConfigurationSource.Explicit);

            Assert.Contains(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Blog.BlogDetails));
            Assert.Contains(dependentEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(BlogDetails.Blog));

            new NotMappedMemberAttributeConvention().Apply(principalEntityTypeBuilder);

            Assert.Contains(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Blog.BlogDetails));
            Assert.Contains(dependentEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(BlogDetails.Blog));
        }

        [Fact]
        public void NotMappedAttribute_ignores_navigation_with_conventional_builder()
        {
            var modelBuilder = new ModelBuilder(new CoreConventionSetBuilder().CreateConventionSet());
            var model = modelBuilder.Model;
            modelBuilder.Entity<BlogDetails>();

            Assert.DoesNotContain(model.FindEntityType(typeof(Blog)).GetNavigations(), nav => nav.Name == nameof(Blog.BlogDetails));
            Assert.Contains(model.FindEntityType(typeof(BlogDetails)).GetNavigations(), nav => nav.Name == nameof(BlogDetails.Blog));
        }

        #endregion

        #region RequiredAttribute

        [Fact]
        public void RequiredAttribute_overrides_configuration_from_convention_source()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<Post>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Blog), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                nameof(Post.Blog),
                nameof(Blog.Posts),
                ConfigurationSource.Convention);

            var navigation = dependentEntityTypeBuilder.Metadata.FindNavigation(nameof(BlogDetails.Blog));

            relationshipBuilder.IsRequired(false, ConfigurationSource.Convention);

            Assert.False(relationshipBuilder.Metadata.IsRequired);

            relationshipBuilder = new RequiredNavigationAttributeConvention().Apply(relationshipBuilder, navigation);

            Assert.True(relationshipBuilder.Metadata.IsRequired);
            Assert.Contains(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Blog.Posts));
            Assert.Contains(dependentEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Post.Blog));
        }

        [Fact]
        public void RequiredAttribute_does_not_override_configuration_from_explicit_source()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<Post>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Blog), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                nameof(Post.Blog),
                nameof(Blog.Posts),
                ConfigurationSource.Convention);

            var navigation = dependentEntityTypeBuilder.Metadata.FindNavigation(nameof(BlogDetails.Blog));

            relationshipBuilder.IsRequired(false, ConfigurationSource.Explicit);

            Assert.False(relationshipBuilder.Metadata.IsRequired);

            relationshipBuilder = new RequiredNavigationAttributeConvention().Apply(relationshipBuilder, navigation);

            Assert.False(relationshipBuilder.Metadata.IsRequired);
            Assert.Contains(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Blog.Posts));
            Assert.Contains(dependentEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Post.Blog));
        }

        [Fact]
        public void RequiredAttribute_does_not_set_is_required_for_collection_navigation()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<Dependent>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                nameof(Dependent.Principal),
                nameof(Principal.Dependents),
                ConfigurationSource.Convention);

            var navigation = dependentEntityTypeBuilder.Metadata.FindNavigation(nameof(Dependent.Principal));

            Assert.False(relationshipBuilder.Metadata.IsRequired);

            relationshipBuilder = new RequiredNavigationAttributeConvention().Apply(relationshipBuilder, navigation);

            Assert.False(relationshipBuilder.Metadata.IsRequired);
        }

        [Fact]
        public void RequiredAttribute_does_not_set_is_required_for_navigation_to_dependent()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<Dependent>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                nameof(Dependent.Principal),
                nameof(Principal.Dependent),
                ConfigurationSource.Convention)
                .RelatedEntityTypes(principalEntityTypeBuilder.Metadata, dependentEntityTypeBuilder.Metadata, ConfigurationSource.Convention);

            var navigation = principalEntityTypeBuilder.Metadata.FindNavigation(nameof(Principal.Dependent));

            Assert.False(relationshipBuilder.Metadata.IsRequired);

            relationshipBuilder = new RequiredNavigationAttributeConvention().Apply(relationshipBuilder, navigation);

            Assert.False(relationshipBuilder.Metadata.IsRequired);
        }

        [Fact]
        public void RequiredAttribute_sets_is_required_with_conventional_builder()
        {
            var modelBuilder = new ModelBuilder(new CoreConventionSetBuilder().CreateConventionSet());
            var model = (Model)modelBuilder.Model;
            modelBuilder.Entity<BlogDetails>();

            Assert.True(model.FindEntityType(typeof(BlogDetails)).GetForeignKeys().Single(fk => fk.PrincipalEntityType?.ClrType == typeof(Blog)).IsRequired);
        }

        #endregion

        #region InversePropertyAttribute

        [Fact]
        public void InversePropertyAttribute_overrides_configuration_from_convention_source()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<Dependent>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                nameof(Dependent.Principal),
                nameof(Principal.Dependents),
                ConfigurationSource.Convention);

            Assert.Contains(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Principal.Dependents));
            Assert.DoesNotContain(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Principal.Dependent));
            Assert.Contains(dependentEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Dependent.Principal));

            new InversePropertyAttributeConvention().Apply(dependentEntityTypeBuilder);

            Assert.DoesNotContain(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Principal.Dependents));
            Assert.Contains(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Principal.Dependent));
            Assert.Contains(dependentEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Dependent.Principal));
        }

        [Fact]
        public void InversePropertyAttribute_does_not_override_configuration_from_explicit_source()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<Dependent>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                nameof(Dependent.Principal),
                nameof(Principal.Dependents),
                ConfigurationSource.Explicit);

            Assert.Contains(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Principal.Dependents));
            Assert.DoesNotContain(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Principal.Dependent));
            Assert.Contains(dependentEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Dependent.Principal));

            new InversePropertyAttributeConvention().Apply(dependentEntityTypeBuilder);

            Assert.Contains(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Principal.Dependents));
            Assert.DoesNotContain(principalEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Principal.Dependent));
            Assert.Contains(dependentEntityTypeBuilder.Metadata.GetNavigations(), nav => nav.Name == nameof(Dependent.Principal));
        }

        [Fact]
        public void InversePropertyAttribute_throws_if_self_navigation()
        {
            var entityTypeBuilder = CreateInternalEntityTypeBuilder<SelfReferencingEntity>();

            Assert.Equal(CoreStrings.SelfReferencingNavigationWithInverseProperty("AnotherEntity", nameof(SelfReferencingEntity), "AnotherEntity", nameof(SelfReferencingEntity)),
                Assert.Throws<InvalidOperationException>(() => new InversePropertyAttributeConvention().Apply(entityTypeBuilder)).Message);
        }

        [Fact]
        public void InversePropertyAttribute_throws_if_navigation_does_not_exist()
        {
            var entityTypeBuilder = CreateInternalEntityTypeBuilder<NonExistentNavigation>();

            Assert.Equal(CoreStrings.InvalidNavigationWithInverseProperty("Principal", nameof(NonExistentNavigation), "WrongNavigation", nameof(Principal)),
                Assert.Throws<InvalidOperationException>(() => new InversePropertyAttributeConvention().Apply(entityTypeBuilder)).Message);
        }

        [Fact]
        public void InversePropertyAttribute_throws_if_navigation_return_type_is_wrong()
        {
            var entityTypeBuilder = CreateInternalEntityTypeBuilder<WrongNavigationType>();

            Assert.Equal(CoreStrings.InvalidNavigationWithInverseProperty("Principal", nameof(WrongNavigationType), "Dependent", nameof(Principal)),
                Assert.Throws<InvalidOperationException>(() => new InversePropertyAttributeConvention().Apply(entityTypeBuilder)).Message);
        }

        [Fact]
        public void InversePropertyAttribute_throws_if_inverse_properties_are_not_pointing_at_each_other()
        {
            var entityTypeBuilder = CreateInternalEntityTypeBuilder<MismatchedInverseProperty>();

            Assert.Equal(
                CoreStrings.InversePropertyMismatch("Principal", nameof(MismatchedInverseProperty), "MismatchedInverseProperty", nameof(Principal)),
                Assert.Throws<InvalidOperationException>(() => new InversePropertyAttributeConvention().Apply(entityTypeBuilder)).Message);
        }

        #endregion

        #region ForeignKeyAttribute

        [Fact]
        public void ForeignKeyAttribute_overrides_configuration_from_convention_source()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<Dependent>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                "Principal",
                "Dependent",
                ConfigurationSource.Convention)
                .HasForeignKey(dependentEntityTypeBuilder.GetOrCreateProperties(
                    new List<PropertyInfo> { Dependent.PrincipalIdProperty }, ConfigurationSource.Convention),
                    ConfigurationSource.Convention);

            Assert.Equal("PrincipalId", relationshipBuilder.Metadata.Properties.First().Name);

            relationshipBuilder = new ForeignKeyAttributeConvention().Apply(relationshipBuilder);

            Assert.Equal("PrincipalFk", relationshipBuilder.Metadata.Properties.First().Name);
        }

        [Fact]
        public void ForeignKeyAttribute_does_not_override_configuration_from_explicit_source()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<Dependent>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                "Principal",
                "Dependent",
                ConfigurationSource.Convention)
                .HasForeignKey(dependentEntityTypeBuilder.GetOrCreateProperties(
                    new List<PropertyInfo> { Dependent.PrincipalIdProperty }, ConfigurationSource.Convention),
                    ConfigurationSource.Explicit);

            Assert.Equal("PrincipalId", relationshipBuilder.Metadata.Properties.First().Name);

            relationshipBuilder = new ForeignKeyAttributeConvention().Apply(relationshipBuilder);

            Assert.Equal("PrincipalId", relationshipBuilder.Metadata.Properties.First().Name);
        }

        [Fact]
        public void ForeignKeyAttribute_sets_foreign_key_properties_when_applied_on_dependent_to_principal_navigation()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<Dependent>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                "Principal",
                "Dependent",
                ConfigurationSource.Convention)
                .HasForeignKey(dependentEntityTypeBuilder.GetOrCreateProperties(
                    new List<PropertyInfo> { Dependent.PrincipalIdProperty }, ConfigurationSource.Convention),
                    ConfigurationSource.Convention);

            Assert.Equal("PrincipalId", relationshipBuilder.Metadata.Properties.First().Name);

            relationshipBuilder = new ForeignKeyAttributeConvention().Apply(relationshipBuilder);

            Assert.Equal("PrincipalFk", relationshipBuilder.Metadata.Properties.First().Name);
        }

        [Fact]
        public void ForeignKeyAttribute_sets_foreign_key_properties_when_applied_on_principal_to_dependent_navigation()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<Dependent>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                "AnotherPrincipal",
                "Dependent",
                ConfigurationSource.Convention)
                .HasForeignKey(dependentEntityTypeBuilder.GetOrCreateProperties(
                    new List<PropertyInfo> { Dependent.PrincipalIdProperty }, ConfigurationSource.Convention),
                    ConfigurationSource.Convention);

            Assert.Equal("PrincipalId", relationshipBuilder.Metadata.Properties.First().Name);

            relationshipBuilder = new ForeignKeyAttributeConvention().Apply(relationshipBuilder);

            Assert.Equal("PrincipalAnotherFk", relationshipBuilder.Metadata.Properties.First().Name);
        }

        [Fact]
        public void ForeignKeyAttribute_sets_foreign_key_properties_when_applied_on_property_on_dependent_side()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<Dependent>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                "AnotherPrincipal",
                "Dependent",
                ConfigurationSource.Convention)
                .HasForeignKey(dependentEntityTypeBuilder.GetOrCreateProperties(
                    new List<PropertyInfo> { Dependent.PrincipalIdProperty }, ConfigurationSource.Convention),
                    ConfigurationSource.Convention);

            Assert.Equal("PrincipalId", relationshipBuilder.Metadata.Properties.First().Name);

            relationshipBuilder = new ForeignKeyAttributeConvention().Apply(relationshipBuilder);

            Assert.Equal("PrincipalAnotherFk", relationshipBuilder.Metadata.Properties.First().Name);
        }

        [Fact]
        public void ForeignKeyAttribute_sets_foreign_key_properties_after_inverting_when_applied_on_property_on_principal_side()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<Principal>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Dependent), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                "Dependent",
                "AnotherPrincipal",
                ConfigurationSource.Convention)
                .HasForeignKey(dependentEntityTypeBuilder.GetOrCreateProperties(
                    new List<PropertyInfo> { Principal.DependentIdProperty }, ConfigurationSource.Convention),
                    ConfigurationSource.Convention);

            Assert.Equal("DependentId", relationshipBuilder.Metadata.Properties.First().Name);
            Assert.Equal(typeof(Principal), relationshipBuilder.Metadata.DeclaringEntityType.ClrType);
            Assert.Equal(typeof(Dependent), relationshipBuilder.Metadata.PrincipalEntityType.ClrType);

            relationshipBuilder = new ForeignKeyAttributeConvention().Apply(relationshipBuilder);

            Assert.Equal("PrincipalAnotherFk", relationshipBuilder.Metadata.Properties.First().Name);
            Assert.Equal(typeof(Dependent), relationshipBuilder.Metadata.DeclaringEntityType.ClrType);
            Assert.Equal(typeof(Principal), relationshipBuilder.Metadata.PrincipalEntityType.ClrType);
        }

        [Fact]
        public void ForeignKeyAttribute_sets_composite_foreign_key_properties_when_applied_on_navigation()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<Dependent>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                "CompositePrincipal",
                "Dependent",
                ConfigurationSource.Convention)
                .HasForeignKey(dependentEntityTypeBuilder.GetOrCreateProperties(
                    new List<PropertyInfo> { Dependent.PrincipalIdProperty }, ConfigurationSource.Convention),
                    ConfigurationSource.Convention);

            Assert.Equal("PrincipalId", relationshipBuilder.Metadata.Properties.First().Name);

            relationshipBuilder = new ForeignKeyAttributeConvention().Apply(relationshipBuilder);

            Assert.Equal(2, relationshipBuilder.Metadata.Properties.Count);
            Assert.Collection(
                relationshipBuilder.Metadata.Properties,
                p => Assert.Equal("PrincipalId", p.Name),
                p => Assert.Equal("PrincipalFk", p.Name));
        }

        [Fact]
        public void ForeignKeyAttribute_throws_when_values_on_property_and_navigtaion_in_entity_type_do_not_match()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<FkPropertyNavigationMismatch>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                "Principal",
                null,
                ConfigurationSource.Convention);

            Assert.Equal(CoreStrings.FkAttributeOnPropertyNavigationMismatch("PrincipalId", "Principal", nameof(FkPropertyNavigationMismatch)),
                Assert.Throws<InvalidOperationException>(() => new ForeignKeyAttributeConvention().Apply(relationshipBuilder)).Message);
        }

        [Fact]
        public void ForeignKeyAttribute_throws_when_defining_composite_foreign_key_using_attribute_on_properties()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<CompositeFkOnProperty>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                "Principal",
                null,
                ConfigurationSource.Convention);

            Assert.Equal(CoreStrings.CompositeFkOnProperty("Principal", nameof(CompositeFkOnProperty)),
                Assert.Throws<InvalidOperationException>(() => new ForeignKeyAttributeConvention().Apply(relationshipBuilder)).Message);
        }

        [Fact]
        public void ForeignKeyAttribute_throws_when_property_list_on_navigation_is_in_incorrect_format()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<InvalidPropertyListOnNavigation>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                "Principal",
                null,
                ConfigurationSource.Convention);

            Assert.Equal(CoreStrings.InvalidPropertyListOnNavigation("Principal", nameof(InvalidPropertyListOnNavigation)),
                Assert.Throws<InvalidOperationException>(() => new ForeignKeyAttributeConvention().Apply(relationshipBuilder)).Message);
        }

        [Fact]
        public void ForeignKeyAttribute_throws_when_same_set_of_properties_are_pointed_by_multiple_navigations()
        {
            var dependentEntityTypeBuilder = CreateInternalEntityTypeBuilder<MultipleNavigationsSameFk>();
            var principalEntityTypeBuilder = dependentEntityTypeBuilder.ModelBuilder.Entity(typeof(Principal), ConfigurationSource.Convention);

            var relationshipBuilder = dependentEntityTypeBuilder.Relationship(
                principalEntityTypeBuilder,
                "One",
                null,
                ConfigurationSource.Convention);

            Assert.Equal(CoreStrings.MultipleNavigationsSameFk(typeof(MultipleNavigationsSameFk).Name, "CommonFkProperty"),
                Assert.Throws<InvalidOperationException>(() => new ForeignKeyAttributeConvention().Apply(relationshipBuilder)).Message);
        }

        #endregion

        [Fact]
        public void Navigation_attribute_convention_runs_for_private_property()
        {
            var modelBuilder = new ModelBuilder(new CoreConventionSetBuilder().CreateConventionSet());
            var referenceBuilder = modelBuilder.Entity<BlogDetails>().HasOne(typeof(Post), "Post").WithOne();

            Assert.False(referenceBuilder.Metadata.Properties.First().IsNullable);
        }

        private InternalEntityTypeBuilder CreateInternalEntityTypeBuilder<T>()
        {
            var conventionSet = new ConventionSet();
            conventionSet.EntityTypeAddedConventions.Add(new PropertyDiscoveryConvention());
            conventionSet.EntityTypeAddedConventions.Add(new KeyDiscoveryConvention());

            var modelBuilder = new InternalModelBuilder(new Model(conventionSet));

            return modelBuilder.Entity(typeof(T), ConfigurationSource.Explicit);
        }

        private class Blog
        {
            public int Id { get; set; }

            [NotMapped]
            public BlogDetails BlogDetails { get; set; }

            public ICollection<Post> Posts { get; set; }
        }

        private class BlogDetails
        {
            public int Id { get; set; }

            [Required]
            public Blog Blog { get; set; }

            [Required]
            private Post Post { get; set; }
        }

        private class Post
        {
            public int Id { get; set; }

            [Required]
            public Blog Blog { get; set; }
        }

        private class Principal
        {
            public static readonly PropertyInfo DependentIdProperty = typeof(Principal).GetProperty("DependentId");

            public int Id { get; set; }

            public int DependentId { get; set; }

            [Required]
            [ForeignKey("PrincipalFk")]
            public ICollection<Dependent> Dependents { get; set; }

            [Required]
            public Dependent Dependent { get; set; }

            public NonExistentNavigation MissingNavigation { get; set; }

            [InverseProperty("AnotherPrincipal")]
            public MismatchedInverseProperty MismatchedInverseProperty { get; set; }
        }

        private class Dependent
        {
            public static readonly PropertyInfo PrincipalIdProperty = typeof(Dependent).GetProperty("PrincipalId");

            public int Id { get; set; }

            public int PrincipalId { get; set; }

            public int PrincipalFk { get; set; }

            [ForeignKey("AnotherPrincipal")]
            public int PrincipalAnotherFk { get; set; }

            [ForeignKey("PrincipalFk")]
            [InverseProperty("Dependent")]
            public Principal Principal { get; set; }

            public Principal AnotherPrincipal { get; set; }

            [ForeignKey("PrincipalId, PrincipalFk")]
            public Principal CompositePrincipal { get; set; }
        }

        private class SelfReferencingEntity
        {
            public int Id { get; set; }

            [InverseProperty("AnotherEntity")]
            public SelfReferencingEntity AnotherEntity { get; set; }
        }

        private class NonExistentNavigation
        {
            public int Id { get; set; }

            [InverseProperty("WrongNavigation")]
            public Principal Principal { get; set; }
        }

        private class WrongNavigationType
        {
            public int Id { get; set; }

            [InverseProperty("Dependent")]
            public Principal Principal { get; set; }
        }

        private class MismatchedInverseProperty
        {
            public int Id { get; set; }

            [InverseProperty("MismatchedInverseProperty")]
            public Principal Principal { get; set; }
        }

        private class FkPropertyNavigationMismatch
        {
            public int Id { get; set; }

            [ForeignKey("Principal")]
            public int PrincipalId { get; set; }

            [ForeignKey("PrincipalFk")]
            public Principal Principal { get; set; }
        }

        private class CompositeFkOnProperty
        {
            public int Id { get; set; }

            [ForeignKey("Principal")]
            public int PrincipalId1 { get; set; }

            [ForeignKey("Principal")]
            public int PrincipalId2 { get; set; }

            public Principal Principal { get; set; }
        }

        private class InvalidPropertyListOnNavigation
        {
            public int Id { get; set; }

            public int PrincipalId1 { get; set; }

            public int PrincipalId2 { get; set; }

            [ForeignKey("PrincipalId1,,PrincipalId2")]
            public Principal Principal { get; set; }
        }

        private class MultipleNavigationsSameFk
        {
            public int Id { get; set; }

            public int CommonFkProperty { get; set; }

            [ForeignKey("CommonFkProperty")]
            public Principal One { get; set; }

            [ForeignKey("CommonFkProperty")]
            public Principal Two { get; set; }
        }
    }
}
