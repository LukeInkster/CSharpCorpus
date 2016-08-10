// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Infrastructure.Internal;

namespace Microsoft.EntityFrameworkCore.Infrastructure
{
    /// <summary>
    ///     <para>
    ///         Allows SQLite specific configuration to be performed on <see cref="DbContextOptions"/>.
    ///     </para>
    ///     <para>
    ///         Instances of this class are returned from a call to 
    ///         <see cref="SqliteDbContextOptionsBuilderExtensions.UseSqlite(DbContextOptionsBuilder, string, System.Action{SqliteDbContextOptionsBuilder})"/>
    ///         and it is not designed to be directly constructed in your application code.
    ///     </para>
    /// </summary>
    public class SqliteDbContextOptionsBuilder : RelationalDbContextOptionsBuilder<SqliteDbContextOptionsBuilder, SqliteOptionsExtension>
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="SqliteDbContextOptionsBuilder"/> class.
        /// </summary>
        /// <param name="optionsBuilder"> The options builder. </param>
        public SqliteDbContextOptionsBuilder([NotNull] DbContextOptionsBuilder optionsBuilder)
            : base(optionsBuilder)
        {
        }

        /// <summary>
        ///     Clones the configuration in this builder.
        /// </summary>
        /// <returns> The cloned configuration. </returns>
        protected override SqliteOptionsExtension CloneExtension()
            => new SqliteOptionsExtension(OptionsBuilder.Options.GetExtension<SqliteOptionsExtension>());

        /// <summary>
        ///     Suppresses enforcement of foreign keys in the database.
        /// </summary>
        /// <returns> The same builder instance so that multiple calls can be chained. </returns>
        public virtual SqliteDbContextOptionsBuilder SuppressForeignKeyEnforcement()
            => SetOption(e => e.EnforceForeignKeys = false);
    }
}
