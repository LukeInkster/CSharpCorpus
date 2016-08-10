// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using Microsoft.EntityFrameworkCore.Specification.Tests.TestModels.Inheritance;

namespace Microsoft.EntityFrameworkCore.Specification.Tests
{
    public abstract class InheritanceFixtureBase
    {
        public virtual void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Kiwi>();
            modelBuilder.Entity<Eagle>();
            modelBuilder.Entity<Bird>();
            modelBuilder.Entity<Animal>().HasKey(e => e.Species);
            modelBuilder.Entity<Rose>();
            modelBuilder.Entity<Daisy>();
            modelBuilder.Entity<Flower>();
            modelBuilder.Entity<Plant>().HasKey(e => e.Species);
            modelBuilder.Entity<Country>();
            modelBuilder.Entity<Drink>();
            modelBuilder.Entity<Tea>();
            modelBuilder.Entity<Lilt>();
            modelBuilder.Entity<Coke>();
        }

        public abstract InheritanceContext CreateContext();

        protected void SeedData(InheritanceContext context)
        {
            var kiwi = new Kiwi
            {
                Species = "Apteryx haastii",
                Name = "Great spotted kiwi",
                IsFlightless = true,
                FoundOn = Island.South
            };

            var eagle = new Eagle
            {
                Species = "Aquila chrysaetos canadensis",
                Name = "American golden eagle",
                Group = EagleGroup.Booted
            };

            eagle.Prey.Add(kiwi);

            var rose = new Rose
            {
                Species = "Rosa canina",
                Name = "Dog-rose",
                HasThorns = true
            };

            var daisy = new Daisy
            {
                Species = "Bellis perennis",
                Name = "Common daisy"
            };

            var nz = new Country { Id = 1, Name = "New Zealand" };

            nz.Animals.Add(kiwi);

            var usa = new Country { Id = 2, Name = "USA" };

            usa.Animals.Add(eagle);

            context.Set<Animal>().Add(kiwi);
            context.Set<Bird>().Add(eagle);
            context.Set<Country>().Add(nz);
            context.Set<Country>().Add(usa);
            context.Set<Rose>().Add(rose);
            context.Set<Daisy>().Add(daisy);

            context.AddRange(
                new Tea { HasMilk = true, CaffeineGrams = 1 },
                new Lilt { SugarGrams = 4, Carbination = 7 },
                new Coke { SugarGrams = 6, CaffeineGrams = 4, Carbination = 5 });

            context.SaveChanges();
        }
    }
}
