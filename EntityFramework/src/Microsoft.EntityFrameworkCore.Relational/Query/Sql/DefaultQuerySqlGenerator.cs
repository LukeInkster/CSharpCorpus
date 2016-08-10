// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using System.Linq.Expressions;
using JetBrains.Annotations;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.EntityFrameworkCore.Query.Expressions;
using Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.EntityFrameworkCore.Utilities;
using Remotion.Linq.Clauses;
using Remotion.Linq.Parsing;

namespace Microsoft.EntityFrameworkCore.Query.Sql
{
    /// <summary>
    ///     The default query SQL generator.
    /// </summary>
    public class DefaultQuerySqlGenerator : ThrowingExpressionVisitor, ISqlExpressionVisitor, IQuerySqlGenerator
    {
        private readonly IRelationalCommandBuilderFactory _relationalCommandBuilderFactory;
        private readonly ISqlGenerationHelper _sqlGenerationHelper;
        private readonly IParameterNameGeneratorFactory _parameterNameGeneratorFactory;
        private readonly IRelationalTypeMapper _relationalTypeMapper;

        private IRelationalCommandBuilder _relationalCommandBuilder;
        private IReadOnlyDictionary<string, object> _parametersValues;
        private ParameterNameGenerator _parameterNameGenerator;
        private RelationalTypeMapping _typeMapping;

        private static readonly Dictionary<ExpressionType, string> _binaryOperatorMap = new Dictionary<ExpressionType, string>
        {
            { ExpressionType.Equal, " = " },
            { ExpressionType.NotEqual, " <> " },
            { ExpressionType.GreaterThan, " > " },
            { ExpressionType.GreaterThanOrEqual, " >= " },
            { ExpressionType.LessThan, " < " },
            { ExpressionType.LessThanOrEqual, " <= " },
            { ExpressionType.AndAlso, " AND " },
            { ExpressionType.OrElse, " OR " },
            { ExpressionType.Subtract, " - " },
            { ExpressionType.Multiply, " * " },
            { ExpressionType.Divide, " / " },
            { ExpressionType.Modulo, " % " },
            { ExpressionType.And, " & " },
            { ExpressionType.Or, " | " }
        };

        /// <summary>
        ///     Creates a new instance of <see cref="DefaultQuerySqlGenerator" />.
        /// </summary>
        /// <param name="relationalCommandBuilderFactory"> The relational command builder factory. </param>
        /// <param name="sqlGenerationHelper"> The SQL generation helper. </param>
        /// <param name="parameterNameGeneratorFactory"> The parameter name generator factory. </param>
        /// <param name="relationalTypeMapper"> The relational type mapper. </param>
        /// <param name="selectExpression"> The select expression. </param>
        public DefaultQuerySqlGenerator(
            [NotNull] IRelationalCommandBuilderFactory relationalCommandBuilderFactory,
            [NotNull] ISqlGenerationHelper sqlGenerationHelper,
            [NotNull] IParameterNameGeneratorFactory parameterNameGeneratorFactory,
            [NotNull] IRelationalTypeMapper relationalTypeMapper,
            [NotNull] SelectExpression selectExpression)
        {
            Check.NotNull(relationalCommandBuilderFactory, nameof(relationalCommandBuilderFactory));
            Check.NotNull(sqlGenerationHelper, nameof(sqlGenerationHelper));
            Check.NotNull(parameterNameGeneratorFactory, nameof(parameterNameGeneratorFactory));
            Check.NotNull(relationalTypeMapper, nameof(relationalTypeMapper));
            Check.NotNull(selectExpression, nameof(selectExpression));

            _relationalCommandBuilderFactory = relationalCommandBuilderFactory;
            _sqlGenerationHelper = sqlGenerationHelper;
            _parameterNameGeneratorFactory = parameterNameGeneratorFactory;
            _relationalTypeMapper = relationalTypeMapper;

            SelectExpression = selectExpression;
        }

        /// <summary>
        ///     Gets or sets a value indicating whether this SQL query is cacheable.
        /// </summary>
        /// <value>
        ///     true if this SQL query is cacheable, false if not.
        /// </value>
        public virtual bool IsCacheable { get; private set; }

        /// <summary>
        ///     Gets the select expression.
        /// </summary>
        /// <value>
        ///     The select expression.
        /// </value>
        protected virtual SelectExpression SelectExpression { get; }

        /// <summary>
        ///     Gets the SQL generation helper.
        /// </summary>
        /// <value>
        ///     The SQL generation helper.
        /// </value>
        protected virtual ISqlGenerationHelper SqlGenerator => _sqlGenerationHelper;

        /// <summary>
        ///     Gets the parameter values.
        /// </summary>
        /// <value>
        ///     The parameter values.
        /// </value>
        protected virtual IReadOnlyDictionary<string, object> ParameterValues => _parametersValues;

        /// <summary>
        ///     Generates SQL for the given parameter values.
        /// </summary>
        /// <param name="parameterValues"> The parameter values. </param>
        /// <returns>
        ///     A relational command.
        /// </returns>
        public virtual IRelationalCommand GenerateSql(IReadOnlyDictionary<string, object> parameterValues)
        {
            Check.NotNull(parameterValues, nameof(parameterValues));

            _relationalCommandBuilder = _relationalCommandBuilderFactory.Create();
            _parameterNameGenerator = _parameterNameGeneratorFactory.Create();

            _parametersValues = parameterValues;
            IsCacheable = true;

            Visit(SelectExpression);

            return _relationalCommandBuilder.Build();
        }

        /// <summary>
        ///     Creates a relational value buffer factory.
        /// </summary>
        /// <param name="relationalValueBufferFactoryFactory"> The relational value buffer factory. </param>
        /// <param name="dataReader"> The data reader. </param>
        /// <returns>
        ///     The new value buffer factory.
        /// </returns>
        public virtual IRelationalValueBufferFactory CreateValueBufferFactory(
            IRelationalValueBufferFactoryFactory relationalValueBufferFactoryFactory, DbDataReader dataReader)
        {
            Check.NotNull(relationalValueBufferFactoryFactory, nameof(relationalValueBufferFactoryFactory));

            return relationalValueBufferFactoryFactory
                .Create(SelectExpression.GetProjectionTypes().ToArray(), indexMap: null);
        }

        /// <summary>
        ///     The generated SQL.
        /// </summary>
        protected virtual IRelationalCommandBuilder Sql => _relationalCommandBuilder;

        /// <summary>
        ///     The default string concatenation operator SQL.
        /// </summary>
        protected virtual string ConcatOperator => "+";

        /// <summary>
        ///     The default true literal SQL.
        /// </summary>
        protected virtual string TypedTrueLiteral => "CAST(1 AS BIT)";

        /// <summary>
        ///     The default false literal SQL.
        /// </summary>
        protected virtual string TypedFalseLiteral => "CAST(0 AS BIT)";

        /// <summary>
        ///     Visit a top-level SelectExpression.
        /// </summary>
        /// <param name="selectExpression"> The select expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitSelect(SelectExpression selectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));

            IDisposable subQueryIndent = null;

            if (selectExpression.Alias != null)
            {
                _relationalCommandBuilder.AppendLine("(");

                subQueryIndent = _relationalCommandBuilder.Indent();
            }

            _relationalCommandBuilder.Append("SELECT ");

            if (selectExpression.IsDistinct)
            {
                _relationalCommandBuilder.Append("DISTINCT ");
            }

            GenerateTop(selectExpression);

            var projectionAdded = false;

            if (selectExpression.IsProjectStar)
            {
                _relationalCommandBuilder
                    .Append(_sqlGenerationHelper.DelimitIdentifier(selectExpression.Tables.Last().Alias))
                    .Append(".*");

                projectionAdded = true;
            }

            if (selectExpression.Projection.Any())
            {
                if (selectExpression.IsProjectStar)
                {
                    _relationalCommandBuilder.Append(", ");
                }

                VisitProjection(selectExpression.Projection);

                projectionAdded = true;
            }

            if (!projectionAdded)
            {
                _relationalCommandBuilder.Append("1");
            }

            if (selectExpression.Tables.Any())
            {
                _relationalCommandBuilder.AppendLine()
                    .Append("FROM ");

                VisitJoin(selectExpression.Tables, sql => sql.AppendLine());
            }

            if (selectExpression.Predicate != null)
            {
                var optimizedPredicate = ApplyOptimizations(selectExpression.Predicate, searchCondition: true);
                if (optimizedPredicate != null)
                {
                    _relationalCommandBuilder.AppendLine()
                        .Append("WHERE ");

                    Visit(optimizedPredicate);
                }
            }

            if (selectExpression.OrderBy.Any())
            {
                _relationalCommandBuilder.AppendLine();

                GenerateOrderBy(selectExpression.OrderBy);
            }

            GenerateLimitOffset(selectExpression);

            if (subQueryIndent != null)
            {
                subQueryIndent.Dispose();

                _relationalCommandBuilder.AppendLine()
                    .Append(")");

                if (selectExpression.Alias.Length > 0)
                {
                    _relationalCommandBuilder.Append(" AS ")
                        .Append(_sqlGenerationHelper.DelimitIdentifier(selectExpression.Alias));
                }
            }

            return selectExpression;
        }

        private Expression ApplyOptimizations(Expression expression, bool searchCondition, bool joinCondition = false)
        {
            var newExpression
                = new NullComparisonTransformingVisitor(_parametersValues)
                    .Visit(expression);

            var binaryExpression = newExpression as BinaryExpression;
            var relationalNullsOptimizedExpandingVisitor = new RelationalNullsOptimizedExpandingVisitor();
            var relationalNullsExpandingVisitor = new RelationalNullsExpandingVisitor();

            if (joinCondition
                && binaryExpression != null)
            {
                var optimizedLeftExpression = relationalNullsOptimizedExpandingVisitor.Visit(binaryExpression.Left);

                optimizedLeftExpression
                    = relationalNullsOptimizedExpandingVisitor.IsOptimalExpansion
                        ? optimizedLeftExpression
                        : relationalNullsExpandingVisitor.Visit(binaryExpression.Left);

                relationalNullsOptimizedExpandingVisitor = new RelationalNullsOptimizedExpandingVisitor();
                var optimizedRightExpression = relationalNullsOptimizedExpandingVisitor.Visit(binaryExpression.Right);

                optimizedRightExpression
                    = relationalNullsOptimizedExpandingVisitor.IsOptimalExpansion
                        ? optimizedRightExpression
                        : relationalNullsExpandingVisitor.Visit(binaryExpression.Right);

                newExpression = Expression.MakeBinary(binaryExpression.NodeType, optimizedLeftExpression, optimizedRightExpression);
            }
            else
            {
                var optimizedExpression = relationalNullsOptimizedExpandingVisitor.Visit(newExpression);

                newExpression
                    = relationalNullsOptimizedExpandingVisitor.IsOptimalExpansion
                        ? optimizedExpression
                        : relationalNullsExpandingVisitor.Visit(newExpression);
            }

            newExpression = new PredicateReductionExpressionOptimizer().Visit(newExpression);
            newExpression = new PredicateNegationExpressionOptimizer().Visit(newExpression);
            newExpression = new ReducingExpressionVisitor().Visit(newExpression);
            var searchConditionTranslatingVisitor = new SearchConditionTranslatingVisitor(searchCondition);
            newExpression = searchConditionTranslatingVisitor.Visit(newExpression);

            if (searchCondition && !SearchConditionTranslatingVisitor.IsSearchCondition(newExpression))
            {
                var constantExpression = newExpression as ConstantExpression;
                if ((constantExpression != null)
                    && (bool)constantExpression.Value)
                {
                    return null;
                }
                return Expression.Equal(newExpression, Expression.Constant(true, typeof(bool)));
            }

            return newExpression;
        }

        /// <summary>
        ///     Visit the projection.
        /// </summary>
        /// <param name="projections"> The projection expression. </param>
        protected virtual void VisitProjection([NotNull] IReadOnlyList<Expression> projections) => VisitJoin(
            projections
                .Select(e => ApplyOptimizations(e, searchCondition: false))
                .ToList());

        /// <summary>
        ///     Generates the ORDER BY SQL.
        /// </summary>
        /// <param name="orderings"> The orderings. </param>
        protected virtual void GenerateOrderBy([NotNull] IReadOnlyList<Ordering> orderings)
        {
            _relationalCommandBuilder.Append("ORDER BY ");

            VisitJoin(orderings, t =>
                {
                    var orderingExpression = t.Expression;
                    var aliasExpression = orderingExpression as AliasExpression;

                    if (aliasExpression != null)
                    {
                        if (aliasExpression.Alias != null)
                        {
                            var columnExpression = aliasExpression.TryGetColumnExpression();

                            if (columnExpression != null)
                            {
                                _relationalCommandBuilder
                                    .Append(_sqlGenerationHelper.DelimitIdentifier(columnExpression.TableAlias))
                                    .Append(".");
                            }

                            _relationalCommandBuilder.Append(_sqlGenerationHelper.DelimitIdentifier(aliasExpression.Alias));
                        }
                        else
                        {
                            Visit(aliasExpression.Expression);
                        }
                    }
                    else
                    {
                        Visit(ApplyOptimizations(orderingExpression, searchCondition: false));
                    }

                    if (t.OrderingDirection == OrderingDirection.Desc)
                    {
                        _relationalCommandBuilder.Append(" DESC");
                    }
                });
        }

        private void VisitJoin(
            IReadOnlyList<Expression> expressions, Action<IRelationalCommandBuilder> joinAction = null)
            => VisitJoin(expressions, e => Visit(e), joinAction);

        private void VisitJoin<T>(
            IReadOnlyList<T> items, Action<T> itemAction, Action<IRelationalCommandBuilder> joinAction = null)
        {
            joinAction = joinAction ?? (isb => isb.Append(", "));

            for (var i = 0; i < items.Count; i++)
            {
                if (i > 0)
                {
                    joinAction(_relationalCommandBuilder);
                }

                itemAction(items[i]);
            }
        }

        /// <summary>
        ///     Visit a FromSqlExpression.
        /// </summary>
        /// <param name="fromSqlExpression"> The FromSql expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitFromSql(FromSqlExpression fromSqlExpression)
        {
            Check.NotNull(fromSqlExpression, nameof(fromSqlExpression));

            _relationalCommandBuilder.AppendLine("(");

            using (_relationalCommandBuilder.Indent())
            {
                GenerateFromSql(fromSqlExpression.Sql, fromSqlExpression.Arguments, _parametersValues);
            }

            _relationalCommandBuilder.Append(") AS ")
                .Append(_sqlGenerationHelper.DelimitIdentifier(fromSqlExpression.Alias));

            return fromSqlExpression;
        }

        /// <summary>
        ///     Generate SQL corresponding to a FromSql query.
        /// </summary>
        /// <param name="sql"> The FromSql SQL query. </param>
        /// <param name="arguments"> The arguments. </param>
        /// <param name="parameters"> The parameters for this query. </param>
        protected virtual void GenerateFromSql(
            [NotNull] string sql,
            [NotNull] Expression arguments,
            [NotNull] IReadOnlyDictionary<string, object> parameters)
        {
            Check.NotEmpty(sql, nameof(sql));
            Check.NotNull(arguments, nameof(arguments));
            Check.NotNull(parameters, nameof(parameters));

            string[] substitutions = null;

            // ReSharper disable once SwitchStatementMissingSomeCases
            switch (arguments.NodeType)
            {
                case ExpressionType.Parameter:
                {
                    var parameterExpression = (ParameterExpression)arguments;

                    object parameterValue;
                    if (parameters.TryGetValue(parameterExpression.Name, out parameterValue))
                    {
                        var argumentValues = (object[])parameterValue;

                        substitutions = new string[argumentValues.Length];

                        _relationalCommandBuilder.AddCompositeParameter(
                            parameterExpression.Name,
                            builder =>
                                {
                                    for (var i = 0; i < argumentValues.Length; i++)
                                    {
                                        var parameterName = _parameterNameGenerator.GenerateNext();

                                        substitutions[i] = SqlGenerator.GenerateParameterName(parameterName);

                                        builder.AddParameter(
                                            parameterName,
                                            substitutions[i]);
                                    }
                                });
                    }

                    break;
                }
                case ExpressionType.Constant:
                {
                    var constantExpression = (ConstantExpression)arguments;
                    var argumentValues = (object[])constantExpression.Value;

                    substitutions = new string[argumentValues.Length];

                    for (var i = 0; i < argumentValues.Length; i++)
                    {
                        var value = argumentValues[i];
                        substitutions[i] = SqlGenerator.GenerateLiteral(value, GetTypeMapping(value));
                    }

                    break;
                }
                case ExpressionType.NewArrayInit:
                {
                    var newArrayExpression = (NewArrayExpression)arguments;

                    substitutions = new string[newArrayExpression.Expressions.Count];

                    for (var i = 0; i < newArrayExpression.Expressions.Count; i++)
                    {
                        var expression = newArrayExpression.Expressions[i].RemoveConvert();

                        // ReSharper disable once SwitchStatementMissingSomeCases
                        switch (expression.NodeType)
                        {
                            case ExpressionType.Constant:
                            {
                                var value = ((ConstantExpression)expression).Value;
                                substitutions[i]
                                    = SqlGenerator
                                        .GenerateLiteral(value, GetTypeMapping(value));

                                break;
                            }
                            case ExpressionType.Parameter:
                            {
                                var parameter = (ParameterExpression)expression;

                                if (_parametersValues.ContainsKey(parameter.Name))
                                {
                                    substitutions[i] = _sqlGenerationHelper.GenerateParameterName(parameter.Name);

                                    _relationalCommandBuilder.AddParameter(
                                        parameter.Name,
                                        substitutions[i]);
                                }

                                break;
                            }
                        }
                    }

                    break;
                }
            }

            if (substitutions != null)
            {
                // ReSharper disable once CoVariantArrayConversion
                // InvariantCulture not needed since substitutions are all strings
                sql = string.Format(sql, substitutions);
            }

            _relationalCommandBuilder.AppendLines(sql);
        }

        private RelationalTypeMapping GetTypeMapping(object value)
            => _typeMapping ?? _relationalTypeMapper.GetMappingForValue(value);

        /// <summary>
        ///     Visit a TableExpression.
        /// </summary>
        /// <param name="tableExpression"> The table expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitTable(TableExpression tableExpression)
        {
            Check.NotNull(tableExpression, nameof(tableExpression));

            if (tableExpression.Schema != null)
            {
                _relationalCommandBuilder.Append(_sqlGenerationHelper.DelimitIdentifier(tableExpression.Schema))
                    .Append(".");
            }

            _relationalCommandBuilder.Append(_sqlGenerationHelper.DelimitIdentifier(tableExpression.Table))
                .Append(" AS ")
                .Append(_sqlGenerationHelper.DelimitIdentifier(tableExpression.Alias));

            return tableExpression;
        }

        /// <summary>
        ///     Visit a CrossJoin expression.
        /// </summary>
        /// <param name="crossJoinExpression"> The cross join expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitCrossJoin(CrossJoinExpression crossJoinExpression)
        {
            Check.NotNull(crossJoinExpression, nameof(crossJoinExpression));

            _relationalCommandBuilder.Append("CROSS JOIN ");

            Visit(crossJoinExpression.TableExpression);

            return crossJoinExpression;
        }

        /// <summary>
        ///     Visit a LateralJoin expression.
        /// </summary>
        /// <param name="lateralJoinExpression"> The lateral join expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitLateralJoin(LateralJoinExpression lateralJoinExpression)
        {
            Check.NotNull(lateralJoinExpression, nameof(lateralJoinExpression));

            _relationalCommandBuilder.Append("CROSS JOIN LATERAL ");

            Visit(lateralJoinExpression.TableExpression);

            return lateralJoinExpression;
        }

        /// <summary>
        ///     Visit a CountExpression
        /// </summary>
        /// <param name="countExpression"> The count expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitCount(CountExpression countExpression)
        {
            Check.NotNull(countExpression, nameof(countExpression));

            _relationalCommandBuilder.Append("COUNT(*)");

            return countExpression;
        }

        /// <summary>
        ///     Visit a SumExpression.
        /// </summary>
        /// <param name="sumExpression"> The sum expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitSum(SumExpression sumExpression)
        {
            Check.NotNull(sumExpression, nameof(sumExpression));

            _relationalCommandBuilder.Append("SUM(");

            Visit(sumExpression.Expression);

            _relationalCommandBuilder.Append(")");

            return sumExpression;
        }

        /// <summary>
        ///     Visit a MinExpression.
        /// </summary>
        /// <param name="minExpression"> The min expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitMin(MinExpression minExpression)
        {
            Check.NotNull(minExpression, nameof(minExpression));

            _relationalCommandBuilder.Append("MIN(");

            Visit(minExpression.Expression);

            _relationalCommandBuilder.Append(")");

            return minExpression;
        }

        /// <summary>
        ///     Visit a MaxExpression.
        /// </summary>
        /// <param name="maxExpression"> The max expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitMax(MaxExpression maxExpression)
        {
            Check.NotNull(maxExpression, nameof(maxExpression));

            _relationalCommandBuilder.Append("MAX(");

            Visit(maxExpression.Expression);

            _relationalCommandBuilder.Append(")");

            return maxExpression;
        }

        /// <summary>
        ///     Visit a StringCompareExpression.
        /// </summary>
        /// <param name="stringCompareExpression"> The string compare expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitStringCompare(StringCompareExpression stringCompareExpression)
        {
            Visit(stringCompareExpression.Left);

            _relationalCommandBuilder.Append(GenerateBinaryOperator(stringCompareExpression.Operator));

            Visit(stringCompareExpression.Right);

            return stringCompareExpression;
        }

        /// <summary>
        ///     Visit an InExpression.
        /// </summary>
        /// <param name="inExpression"> The in expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitIn(InExpression inExpression)
        {
            if (inExpression.Values != null)
            {
                var inValues = ProcessInExpressionValues(inExpression.Values);
                var inValuesNotNull = ExtractNonNullExpressionValues(inValues);

                if (inValues.Count != inValuesNotNull.Count)
                {
                    var relationalNullsInExpression
                        = Expression.OrElse(
                            new InExpression(inExpression.Operand, inValuesNotNull),
                            new IsNullExpression(inExpression.Operand));

                    return Visit(relationalNullsInExpression);
                }

                if (inValuesNotNull.Count > 0)
                {
                    var parentTypeMapping = _typeMapping;
                    _typeMapping = InferTypeMappingFromColumn(inExpression.Operand) ?? parentTypeMapping;

                    Visit(inExpression.Operand);

                    _relationalCommandBuilder.Append(" IN (");

                    VisitJoin(inValuesNotNull);

                    _relationalCommandBuilder.Append(")");

                    _typeMapping = parentTypeMapping;
                }
                else
                {
                    _relationalCommandBuilder.Append("0 = 1");
                }
            }
            else
            {
                var parentTypeMapping = _typeMapping;
                _typeMapping = InferTypeMappingFromColumn(inExpression.Operand) ?? parentTypeMapping;

                Visit(inExpression.Operand);

                _relationalCommandBuilder.Append(" IN ");

                Visit(inExpression.SubQuery);

                _typeMapping = parentTypeMapping;
            }

            return inExpression;
        }

        /// <summary>
        ///     Visit a negated InExpression.
        /// </summary>
        /// <param name="inExpression"> The in expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        protected virtual Expression VisitNotIn([NotNull] InExpression inExpression)
        {
            if (inExpression.Values != null)
            {
                var inValues = ProcessInExpressionValues(inExpression.Values);
                var inValuesNotNull = ExtractNonNullExpressionValues(inValues);

                if (inValues.Count != inValuesNotNull.Count)
                {
                    var relationalNullsNotInExpression
                        = Expression.AndAlso(
                            Expression.Not(new InExpression(inExpression.Operand, inValuesNotNull)),
                            Expression.Not(new IsNullExpression(inExpression.Operand)));

                    return Visit(relationalNullsNotInExpression);
                }

                if (inValues.Count > 0)
                {
                    Visit(inExpression.Operand);

                    _relationalCommandBuilder.Append(" NOT IN (");

                    VisitJoin(inValues);

                    _relationalCommandBuilder.Append(")");
                }
                else
                {
                    _relationalCommandBuilder.Append("1 = 1");
                }
            }
            else
            {
                Visit(inExpression.Operand);

                _relationalCommandBuilder.Append(" NOT IN ");

                Visit(inExpression.SubQuery);
            }

            return inExpression;
        }

        /// <summary>
        ///     Process the InExpression values.
        /// </summary>
        /// <param name="inExpressionValues"> The in expression values. </param>
        /// <returns>
        ///     A list of expressions.
        /// </returns>
        protected virtual IReadOnlyList<Expression> ProcessInExpressionValues(
            [NotNull] IEnumerable<Expression> inExpressionValues)
        {
            Check.NotNull(inExpressionValues, nameof(inExpressionValues));

            var inConstants = new List<Expression>();

            foreach (var inValue in inExpressionValues)
            {
                var inConstant = inValue as ConstantExpression;

                if (inConstant != null)
                {
                    AddInExpressionValues(inConstant.Value, inConstants, inConstant);
                }
                else
                {
                    var inParameter = inValue as ParameterExpression;

                    if (inParameter != null)
                    {
                        object parameterValue;
                        if (_parametersValues.TryGetValue(inParameter.Name, out parameterValue))
                        {
                            AddInExpressionValues(parameterValue, inConstants, inParameter);

                            IsCacheable = false;
                        }
                    }
                    else
                    {
                        var inListInit = inValue as ListInitExpression;

                        if (inListInit != null)
                        {
                            inConstants.AddRange(ProcessInExpressionValues(
                                inListInit.Initializers.SelectMany(i => i.Arguments)));
                        }
                        else
                        {
                            var newArray = inValue as NewArrayExpression;

                            if (newArray != null)
                            {
                                inConstants.AddRange(ProcessInExpressionValues(newArray.Expressions));
                            }
                        }
                    }
                }
            }

            return inConstants;
        }

        private static void AddInExpressionValues(
            object value, List<Expression> inConstants, Expression expression)
        {
            var valuesEnumerable = value as IEnumerable;

            if (valuesEnumerable != null
                && value.GetType() != typeof(string)
                && value.GetType() != typeof(byte[]))
            {
                inConstants.AddRange(valuesEnumerable.Cast<object>().Select(Expression.Constant));
            }
            else
            {
                inConstants.Add(expression);
            }
        }

        /// <summary>
        ///     Extracts the non null expression values from a list of expressions.
        /// </summary>
        /// <param name="inExpressionValues"> The list of expressions. </param>
        /// <returns>
        ///     The extracted non null expression values.
        /// </returns>
        protected virtual IReadOnlyList<Expression> ExtractNonNullExpressionValues(
            [NotNull] IReadOnlyList<Expression> inExpressionValues)
        {
            var inValuesNotNull = new List<Expression>();

            foreach (var inValue in inExpressionValues)
            {
                var inConstant = inValue as ConstantExpression;

                if (inConstant?.Value != null)
                {
                    inValuesNotNull.Add(inValue);

                    continue;
                }

                var inParameter = inValue as ParameterExpression;

                if (inParameter != null)
                {
                    object parameterValue;

                    if (_parametersValues.TryGetValue(inParameter.Name, out parameterValue))
                    {
                        if (parameterValue != null)
                        {
                            inValuesNotNull.Add(inValue);
                        }
                    }
                }
            }

            return inValuesNotNull;
        }

        /// <summary>
        ///     Visit an InnerJoinExpression.
        /// </summary>
        /// <param name="innerJoinExpression"> The inner join expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitInnerJoin(InnerJoinExpression innerJoinExpression)
        {
            Check.NotNull(innerJoinExpression, nameof(innerJoinExpression));

            _relationalCommandBuilder.Append("INNER JOIN ");

            Visit(innerJoinExpression.TableExpression);

            _relationalCommandBuilder.Append(" ON ");

            Visit(ApplyOptimizations(innerJoinExpression.Predicate, searchCondition: true, joinCondition: true));

            return innerJoinExpression;
        }

        /// <summary>
        ///     Visit an LeftOuterJoinExpression.
        /// </summary>
        /// <param name="leftOuterJoinExpression"> The left outer join expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitLeftOuterJoin(LeftOuterJoinExpression leftOuterJoinExpression)
        {
            Check.NotNull(leftOuterJoinExpression, nameof(leftOuterJoinExpression));

            _relationalCommandBuilder.Append("LEFT JOIN ");

            Visit(leftOuterJoinExpression.TableExpression);

            _relationalCommandBuilder.Append(" ON ");

            Visit(ApplyOptimizations(leftOuterJoinExpression.Predicate, searchCondition: true, joinCondition: true));

            return leftOuterJoinExpression;
        }

        /// <summary>
        ///     Generates the TOP part of the SELECT statement,
        /// </summary>
        /// <param name="selectExpression"> The select expression. </param>
        protected virtual void GenerateTop([NotNull] SelectExpression selectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));

            if (selectExpression.Limit != null
                && selectExpression.Offset == null)
            {
                _relationalCommandBuilder.Append("TOP(");

                Visit(selectExpression.Limit);

                _relationalCommandBuilder.Append(") ");
            }
        }

        /// <summary>
        ///     Generates the LIMIT OFFSET part of the SELECT statement,
        /// </summary>
        /// <param name="selectExpression"> The select expression. </param>
        protected virtual void GenerateLimitOffset([NotNull] SelectExpression selectExpression)
        {
            Check.NotNull(selectExpression, nameof(selectExpression));

            if (selectExpression.Offset != null)
            {
                _relationalCommandBuilder.AppendLine()
                    .Append("OFFSET ");

                Visit(selectExpression.Offset);

                _relationalCommandBuilder.Append(" ROWS");

                if (selectExpression.Limit != null)
                {
                    _relationalCommandBuilder.Append(" FETCH NEXT ");

                    Visit(selectExpression.Limit);

                    _relationalCommandBuilder.Append(" ROWS ONLY");
                }
            }
        }

        /// <summary>
        ///     Visit a ConditionalExpression.
        /// </summary>
        /// <param name="expression"> The conditional expression to visit. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        protected override Expression VisitConditional(ConditionalExpression expression)
        {
            Check.NotNull(expression, nameof(expression));

            _relationalCommandBuilder.AppendLine("CASE");

            using (_relationalCommandBuilder.Indent())
            {
                _relationalCommandBuilder.Append("WHEN ");

                Visit(expression.Test);

                _relationalCommandBuilder.AppendLine();
                _relationalCommandBuilder.Append("THEN ");

                var constantIfTrue = expression.IfTrue as ConstantExpression;

                if (constantIfTrue != null
                    && constantIfTrue.Type == typeof(bool))
                {
                    _relationalCommandBuilder.Append((bool)constantIfTrue.Value ? TypedTrueLiteral : TypedFalseLiteral);
                }
                else
                {
                    Visit(expression.IfTrue);
                }

                _relationalCommandBuilder.Append(" ELSE ");

                var constantIfFalse = expression.IfFalse as ConstantExpression;

                if (constantIfFalse != null
                    && constantIfFalse.Type == typeof(bool))
                {
                    _relationalCommandBuilder.Append((bool)constantIfFalse.Value ? TypedTrueLiteral : TypedFalseLiteral);
                }
                else
                {
                    Visit(expression.IfFalse);
                }

                _relationalCommandBuilder.AppendLine();
            }

            _relationalCommandBuilder.Append("END");

            return expression;
        }

        /// <summary>
        ///     Visit an ExistsExpression.
        /// </summary>
        /// <param name="existsExpression"> The exists expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitExists(ExistsExpression existsExpression)
        {
            Check.NotNull(existsExpression, nameof(existsExpression));

            _relationalCommandBuilder.AppendLine("EXISTS (");

            using (_relationalCommandBuilder.Indent())
            {
                Visit(existsExpression.Expression);
            }

            _relationalCommandBuilder.Append(")");

            return existsExpression;
        }

        /// <summary>
        ///     Visit a BinaryExpression.
        /// </summary>
        /// <param name="expression"> The binary expression to visit. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        protected override Expression VisitBinary(BinaryExpression expression)
        {
            Check.NotNull(expression, nameof(expression));

            if (expression.NodeType == ExpressionType.Coalesce)
            {
                _relationalCommandBuilder.Append("COALESCE(");
                Visit(expression.Left);
                _relationalCommandBuilder.Append(", ");
                Visit(expression.Right);
                _relationalCommandBuilder.Append(")");
            }
            else
            {
                var parentTypeMapping = _typeMapping;

                if (expression.IsComparisonOperation()
                    || (expression.NodeType == ExpressionType.Add))
                {
                    _typeMapping
                        = InferTypeMappingFromColumn(expression.Left)
                          ?? InferTypeMappingFromColumn(expression.Right)
                          ?? parentTypeMapping;
                }

                var needParens = expression.Left is BinaryExpression;

                if (needParens)
                {
                    _relationalCommandBuilder.Append("(");
                }

                Visit(expression.Left);

                if (needParens)
                {
                    _relationalCommandBuilder.Append(")");
                }

                string op;
                if (!TryGenerateBinaryOperator(expression.NodeType, out op))
                {
                    switch (expression.NodeType)
                    {
                        case ExpressionType.Add:
                        {
                            op = expression.Type == typeof(string)
                                ? " " + ConcatOperator + " "
                                : " + ";
                            break;
                        }
                        default:
                        {
                            throw new ArgumentOutOfRangeException();
                        }
                    }
                }

                _relationalCommandBuilder.Append(op);

                needParens = expression.Right is BinaryExpression;

                if (needParens)
                {
                    _relationalCommandBuilder.Append("(");
                }

                Visit(expression.Right);

                if (needParens)
                {
                    _relationalCommandBuilder.Append(")");
                }

                _typeMapping = parentTypeMapping;
            }

            return expression;
        }

        /// <summary>
        ///     Visits a ColumnExpression.
        /// </summary>
        /// <param name="columnExpression"> The column expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitColumn(ColumnExpression columnExpression)
        {
            Check.NotNull(columnExpression, nameof(columnExpression));

            _relationalCommandBuilder.Append(_sqlGenerationHelper.DelimitIdentifier(columnExpression.TableAlias))
                .Append(".")
                .Append(_sqlGenerationHelper.DelimitIdentifier(columnExpression.Name));

            return columnExpression;
        }

        /// <summary>
        ///     Visits an AliasExpression.
        /// </summary>
        /// <param name="aliasExpression"> The alias expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitAlias(AliasExpression aliasExpression)
        {
            Check.NotNull(aliasExpression, nameof(aliasExpression));

            if (!aliasExpression.IsProjected)
            {
                Visit(aliasExpression.Expression);

                if (aliasExpression.Alias != null)
                {
                    _relationalCommandBuilder.Append(" AS ");
                }
            }

            if (aliasExpression.Alias != null)
            {
                _relationalCommandBuilder.Append(_sqlGenerationHelper.DelimitIdentifier(aliasExpression.Alias));
            }

            return aliasExpression;
        }

        /// <summary>
        ///     Visits an IsNullExpression.
        /// </summary>
        /// <param name="isNullExpression"> The is null expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitIsNull(IsNullExpression isNullExpression)
        {
            Check.NotNull(isNullExpression, nameof(isNullExpression));

            Visit(isNullExpression.Operand);

            _relationalCommandBuilder.Append(" IS NULL");

            return isNullExpression;
        }

        /// <summary>
        ///     Visits an IsNotNullExpression.
        /// </summary>
        /// <param name="isNotNullExpression"> The is not null expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitIsNotNull([NotNull] IsNullExpression isNotNullExpression)
        {
            Check.NotNull(isNotNullExpression, nameof(isNotNullExpression));

            Visit(isNotNullExpression.Operand);

            _relationalCommandBuilder.Append(" IS NOT NULL");

            return isNotNullExpression;
        }

        /// <summary>
        ///     Visit a LikeExpression.
        /// </summary>
        /// <param name="likeExpression"> The like expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitLike(LikeExpression likeExpression)
        {
            Check.NotNull(likeExpression, nameof(likeExpression));

            var parentTypeMapping = _typeMapping;
            _typeMapping = InferTypeMappingFromColumn(likeExpression.Match) ?? parentTypeMapping;

            Visit(likeExpression.Match);

            _relationalCommandBuilder.Append(" LIKE ");

            Visit(likeExpression.Pattern);

            _typeMapping = parentTypeMapping;

            return likeExpression;
        }

        /// <summary>
        ///     Visits a SqlFunctionExpression.
        /// </summary>
        /// <param name="sqlFunctionExpression"> The SQL function expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitSqlFunction(SqlFunctionExpression sqlFunctionExpression)
        {
            _relationalCommandBuilder.Append(sqlFunctionExpression.FunctionName);
            _relationalCommandBuilder.Append("(");

            VisitJoin(sqlFunctionExpression.Arguments.ToList());

            _relationalCommandBuilder.Append(")");

            return sqlFunctionExpression;
        }

        /// <summary>
        ///     Visit a SQL ExplicitCastExpression.
        /// </summary>
        /// <param name="explicitCastExpression"> The explicit cast expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitExplicitCast(ExplicitCastExpression explicitCastExpression)
        {
            _relationalCommandBuilder.Append("CAST(");

            var parentTypeMapping = _typeMapping;

            _typeMapping = InferTypeMappingFromColumn(explicitCastExpression.Operand);

            Visit(explicitCastExpression.Operand);

            _relationalCommandBuilder.Append(" AS ");

            var typeMapping = _relationalTypeMapper.FindMapping(explicitCastExpression.Type);

            if (typeMapping == null)
            {
                throw new InvalidOperationException(RelationalStrings.UnsupportedType(explicitCastExpression.Type.ShortDisplayName()));
            }

            _relationalCommandBuilder.Append(typeMapping.StoreType);

            _relationalCommandBuilder.Append(")");

            _typeMapping = parentTypeMapping;

            return explicitCastExpression;
        }

        /// <summary>
        ///     Visits a UnaryExpression.
        /// </summary>
        /// <param name="expression"> The unary expression to visit. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        protected override Expression VisitUnary(UnaryExpression expression)
        {
            Check.NotNull(expression, nameof(expression));

            switch (expression.NodeType)
            {
                case ExpressionType.Not:
                {
                    var inExpression = expression.Operand as InExpression;

                    if (inExpression != null)
                    {
                        return VisitNotIn(inExpression);
                    }

                    var isNullExpression = expression.Operand as IsNullExpression;

                    if (isNullExpression != null)
                    {
                        return VisitIsNotNull(isNullExpression);
                    }

                    if (expression.Operand is ExistsExpression)
                    {
                        _relationalCommandBuilder.Append("NOT ");

                        Visit(expression.Operand);

                        return expression;
                    }

                    _relationalCommandBuilder.Append("NOT (");

                    Visit(expression.Operand);

                    _relationalCommandBuilder.Append(")");

                    return expression;
                }
                case ExpressionType.Convert:
                {
                    Visit(expression.Operand);

                    return expression;
                }
                case ExpressionType.Negate:
                {
                    _relationalCommandBuilder.Append("-");
                    Visit(expression.Operand);

                    return expression;
                }
            }

            return base.VisitUnary(expression);
        }

        /// <summary>
        ///     Visits a ConstantExpression.
        /// </summary>
        /// <param name="expression"> The constant expression to visit. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        protected override Expression VisitConstant(ConstantExpression expression)
        {
            Check.NotNull(expression, nameof(expression));

            var value = expression.Value;
            _relationalCommandBuilder.Append(value == null
                ? "NULL"
                : _sqlGenerationHelper.GenerateLiteral(value, GetTypeMapping(value)));

            return expression;
        }

        /// <summary>
        ///     Visits a ParameterExpression.
        /// </summary>
        /// <param name="parameterExpression"> The parameter expression to visit. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        protected override Expression VisitParameter(ParameterExpression parameterExpression)
        {
            Check.NotNull(parameterExpression, nameof(parameterExpression));

            var parameterName = _sqlGenerationHelper.GenerateParameterName(parameterExpression.Name);

            if (_relationalCommandBuilder.ParameterBuilder.Parameters
                .All(p => p.InvariantName != parameterExpression.Name))
            {
                _relationalCommandBuilder.AddParameter(
                    parameterExpression.Name,
                    parameterName,
                    _typeMapping ?? _relationalTypeMapper.GetMapping(parameterExpression.Type),
                    parameterExpression.Type.IsNullableType());
            }

            _relationalCommandBuilder.Append(parameterName);

            return parameterExpression;
        }

        /// <summary>
        ///     Visits a PropertyParameterExpression.
        /// </summary>
        /// <param name="propertyParameterExpression"> The property parameter expression. </param>
        /// <returns>
        ///     An Expression.
        /// </returns>
        public virtual Expression VisitPropertyParameter(PropertyParameterExpression propertyParameterExpression)
        {
            var parameterName
                = _sqlGenerationHelper.GenerateParameterName(
                    propertyParameterExpression.PropertyParameterName);

            if (_relationalCommandBuilder.ParameterBuilder.Parameters
                .All(p => p.InvariantName != propertyParameterExpression.PropertyParameterName))
            {
                _relationalCommandBuilder.AddPropertyParameter(
                    propertyParameterExpression.Name,
                    parameterName,
                    propertyParameterExpression.Property);
            }

            _relationalCommandBuilder.Append(parameterName);

            return propertyParameterExpression;
        }

        /// <summary>
        ///     Infers a type mapping from a column expression.
        /// </summary>
        /// <param name="expression"> The expression to infer a type mapping for. </param>
        /// <returns>
        ///     A RelationalTypeMapping.
        /// </returns>
        protected virtual RelationalTypeMapping InferTypeMappingFromColumn([NotNull] Expression expression)
        {
            var column = expression.TryGetColumnExpression();
            return column?.Property != null
                ? _relationalTypeMapper.FindMapping(column.Property)
                : null;
        }

        /// <summary>
        ///     Attempts to generate binary operator for a given expression type.
        /// </summary>
        /// <param name="op"> The operation. </param>
        /// <param name="result"> [out] The SQL binary operator. </param>
        /// <returns>
        ///     true if it succeeds, false if it fails.
        /// </returns>
        protected virtual bool TryGenerateBinaryOperator(ExpressionType op, [NotNull] out string result)
            => _binaryOperatorMap.TryGetValue(op, out result);

        /// <summary>
        ///     Generates SQL for a given binary operation type.
        /// </summary>
        /// <param name="op"> The operation. </param>
        /// <returns>
        ///     The binary operator.
        /// </returns>
        protected virtual string GenerateBinaryOperator(ExpressionType op) => _binaryOperatorMap[op];

        /// <summary>
        ///     Creates unhandled item exception.
        /// </summary>
        /// <typeparam name="T"> Generic type parameter. </typeparam>
        /// <param name="unhandledItem"> The unhandled item. </param>
        /// <param name="visitMethod"> The visit method. </param>
        /// <returns>
        ///     The new unhandled item exception.
        /// </returns>
        protected override Exception CreateUnhandledItemException<T>(T unhandledItem, string visitMethod)
            => new NotImplementedException(visitMethod);

        private class NullComparisonTransformingVisitor : RelinqExpressionVisitor
        {
            private readonly IReadOnlyDictionary<string, object> _parameterValues;

            public NullComparisonTransformingVisitor(IReadOnlyDictionary<string, object> parameterValues)
            {
                _parameterValues = parameterValues;
            }

            protected override Expression VisitBinary(BinaryExpression expression)
            {
                if (expression.NodeType == ExpressionType.Equal
                    || expression.NodeType == ExpressionType.NotEqual)
                {
                    var leftExpression = expression.Left.RemoveConvert();
                    var rightExpression = expression.Right.RemoveConvert();

                    var parameter
                        = rightExpression as ParameterExpression
                          ?? leftExpression as ParameterExpression;

                    object parameterValue;
                    if (parameter != null
                        && _parameterValues.TryGetValue(parameter.Name, out parameterValue))
                    {
                        if (parameterValue == null)
                        {
                            var columnExpression
                                = leftExpression.TryGetColumnExpression()
                                  ?? rightExpression.TryGetColumnExpression();

                            if (columnExpression != null)
                            {
                                return
                                    expression.NodeType == ExpressionType.Equal
                                        ? (Expression)new IsNullExpression(columnExpression)
                                        : Expression.Not(new IsNullExpression(columnExpression));
                            }
                        }

                        var constantExpression
                            = leftExpression as ConstantExpression
                              ?? rightExpression as ConstantExpression;

                        if (constantExpression != null)
                        {
                            if (parameterValue == null
                                && constantExpression.Value == null)
                            {
                                return
                                    expression.NodeType == ExpressionType.Equal
                                        ? Expression.Constant(true)
                                        : Expression.Constant(false);
                            }

                            if ((parameterValue == null && constantExpression.Value != null)
                                || (parameterValue != null && constantExpression.Value == null))
                            {
                                return
                                    expression.NodeType == ExpressionType.Equal
                                        ? Expression.Constant(false)
                                        : Expression.Constant(true);
                            }
                        }
                    }
                }

                return base.VisitBinary(expression);
            }
        }

        private class SearchConditionTranslatingVisitor : RelinqExpressionVisitor
        {
            private bool _isSearchCondition;

            public SearchConditionTranslatingVisitor(bool isSearchCondition)
            {
                _isSearchCondition = isSearchCondition;
            }

            public static bool IsSearchCondition(Expression expression)
            {
                expression = expression.RemoveConvert();

                if (!(expression is BinaryExpression)
                    && (expression.NodeType != ExpressionType.Not)
                    && (expression.NodeType != ExpressionType.Extension))
                {
                    return false;
                }

                if (expression.IsComparisonOperation()
                    || expression.IsLogicalOperation()
                    || expression is LikeExpression
                    || expression is IsNullExpression
                    || expression is InExpression
                    || expression is ExistsExpression
                    || expression is StringCompareExpression)
                {
                    return true;
                }

                return false;
            }

            protected override Expression VisitBinary(BinaryExpression expression)
            {
                if (_isSearchCondition)
                {
                    if (expression.IsComparisonOperation()
                        || expression.NodeType == ExpressionType.Or
                        || expression.NodeType == ExpressionType.And)
                    {
                        var parentIsSearchCondition = _isSearchCondition;
                        _isSearchCondition = false;
                        var left = Visit(expression.Left);
                        var right = Visit(expression.Right);
                        _isSearchCondition = parentIsSearchCondition;

                        return Expression.MakeBinary(expression.NodeType, left, right);
                    }
                }
                else
                {
                    Expression newLeft;
                    Expression newRight;
                    if (expression.IsLogicalOperation()
                        || expression.NodeType == ExpressionType.Or
                        || expression.NodeType == ExpressionType.And)
                    {
                        var parentIsSearchCondition = _isSearchCondition;
                        _isSearchCondition = expression.IsLogicalOperation();
                        newLeft = Visit(expression.Left);
                        newRight = Visit(expression.Right);
                        _isSearchCondition = parentIsSearchCondition;
                    }
                    else
                    {
                        newLeft = Visit(expression.Left);
                        newRight = Visit(expression.Right);
                    }

                    var newExpression = expression.Update(newLeft, expression.Conversion, newRight);
                    if (IsSearchCondition(newExpression))
                    {
                        return Expression.Condition(
                            newExpression,
                            Expression.Constant(true, typeof(bool)),
                            Expression.Constant(false, typeof(bool)));
                    }
                }

                return base.VisitBinary(expression);
            }

            protected override Expression VisitConditional(ConditionalExpression expression)
            {
                var parentIsSearchCondition = _isSearchCondition;
                _isSearchCondition = true;
                var test = Visit(expression.Test);
                _isSearchCondition = false;
                var ifTrue = Visit(expression.IfTrue);
                var ifFalse = Visit(expression.IfFalse);
                _isSearchCondition = parentIsSearchCondition;

                var newExpression = Expression.Condition(test, ifTrue, ifFalse);

                if (_isSearchCondition)
                {
                    return Expression.MakeBinary(
                        ExpressionType.Equal,
                        newExpression,
                        Expression.Constant(true, typeof(bool)));
                }
                return newExpression;
            }

            protected override Expression VisitUnary(UnaryExpression expression)
            {
                var parentIsSearchCondition = _isSearchCondition;
                if (expression.NodeType == ExpressionType.Convert)
                {
                    _isSearchCondition = false;
                }

                if (expression.NodeType == ExpressionType.Not)
                {
                    _isSearchCondition = true;
                }

                var operand = Visit(expression.Operand);

                if (expression.NodeType == ExpressionType.Convert
                    || expression.NodeType == ExpressionType.Not)
                {
                    _isSearchCondition = parentIsSearchCondition;
                }

                if (_isSearchCondition)
                {
                    if (expression.NodeType == ExpressionType.Not
                        && expression.Operand.IsSimpleExpression())
                    {
                        return Expression.Equal(expression.Operand, Expression.Constant(false, typeof(bool)));
                    }

                    if (expression.NodeType == ExpressionType.Convert
                        && operand.IsSimpleExpression())
                    {
                        return Expression.Equal(
                            Expression.Convert(operand, expression.Type),
                            Expression.Constant(true, typeof(bool)));
                    }
                }
                else
                {
                    if (IsSearchCondition(expression))
                    {
                        if (expression.NodeType == ExpressionType.Not)
                        {
                            return Expression.Condition(
                                operand,
                                Expression.Constant(false, typeof(bool)),
                                Expression.Constant(true, typeof(bool)));
                        }

                        if (expression.NodeType == ExpressionType.Convert
                            || expression.NodeType == ExpressionType.ConvertChecked)
                        {
                            return Expression.MakeUnary(expression.NodeType, operand, expression.Type);
                        }

                        return Expression.Condition(
                            Expression.MakeUnary(expression.NodeType, operand, expression.Type),
                            Expression.Constant(true, typeof(bool)),
                            Expression.Constant(false, typeof(bool)));
                    }
                }

                return base.VisitUnary(expression);
            }

            protected override Expression VisitExtension(Expression expression)
            {
                if (_isSearchCondition)
                {
                    var parentIsSearchCondition = _isSearchCondition;
                    _isSearchCondition = false;
                    var newExpression = base.VisitExtension(expression);
                    _isSearchCondition = parentIsSearchCondition;

                    return expression is AliasExpression || expression is ColumnExpression || expression is SelectExpression
                        ? Expression.Equal(newExpression, Expression.Constant(true, typeof(bool)))
                        : newExpression;
                }
                else
                {
                    if (IsSearchCondition(expression))
                    {
                        var newExpression = base.VisitExtension(expression);

                        return Expression.Condition(
                            newExpression,
                            Expression.Constant(true),
                            Expression.Constant(false));
                    }
                }

                return base.VisitExtension(expression);
            }

            protected override Expression VisitParameter(ParameterExpression expression)
            {
                var newExpression = base.VisitParameter(expression);
                return _isSearchCondition && (newExpression.Type == typeof(bool))
                    ? Expression.Equal(newExpression, Expression.Constant(true, typeof(bool)))
                    : newExpression;
            }
        }
    }
}
