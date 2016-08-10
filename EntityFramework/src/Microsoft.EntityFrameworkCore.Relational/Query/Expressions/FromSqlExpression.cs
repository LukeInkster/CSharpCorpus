// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Query.Sql;
using Microsoft.EntityFrameworkCore.Utilities;
using Remotion.Linq.Clauses;

namespace Microsoft.EntityFrameworkCore.Query.Expressions
{
    /// <summary>
    ///     Represents a FromSql expression.
    /// </summary>
    public class FromSqlExpression : TableExpressionBase
    {
        /// <summary>
        ///     Creates a new instance of a FromSqlExpression.
        /// </summary>
        /// <param name="sql"> The SQL. </param>
        /// <param name="arguments"> The arguments. </param>
        /// <param name="alias"> The alias. </param>
        /// <param name="querySource"> The query source. </param>
        public FromSqlExpression(
            [NotNull] string sql,
            [NotNull] Expression arguments,
            [NotNull] string alias,
            [NotNull] IQuerySource querySource)
            : base(
                Check.NotNull(querySource, nameof(querySource)),
                Check.NotEmpty(alias, nameof(alias)))
        {
            Check.NotEmpty(sql, nameof(sql));
            Check.NotNull(arguments, nameof(arguments));

            Sql = sql;
            Arguments = arguments;
        }

        /// <summary>
        ///     Gets the SQL.
        /// </summary>
        /// <value>
        ///     The SQL.
        /// </value>
        public virtual string Sql { get; }

        /// <summary>
        ///     Gets the arguments.
        /// </summary>
        /// <value>
        ///     The arguments.
        /// </value>
        public virtual Expression Arguments { get; }

        /// <summary>
        /// Dispatches to the specific visit method for this node type.
        /// </summary>
        protected override Expression Accept(ExpressionVisitor visitor)
        {
            Check.NotNull(visitor, nameof(visitor));

            var specificVisitor = visitor as ISqlExpressionVisitor;

            return specificVisitor != null
                ? specificVisitor.VisitFromSql(this)
                : base.Accept(visitor);
        }

        /// <summary>
        /// Creates a <see cref="string"/> representation of the Expression.
        /// </summary>
        /// <returns>A <see cref="string"/> representation of the Expression.</returns>
        public override string ToString() => Sql + " " + Alias;
    }
}
