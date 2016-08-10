﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Internal;
using Remotion.Linq.Parsing;

namespace Microsoft.EntityFrameworkCore.Query.ExpressionVisitors.Internal
{
    /// <summary>
    ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
    ///     directly from your code. This API may change or be removed in future releases.
    /// </summary>
    public class PredicateReductionExpressionOptimizer : RelinqExpressionVisitor
    {
        /// <summary>
        ///     This API supports the Entity Framework Core infrastructure and is not intended to be used 
        ///     directly from your code. This API may change or be removed in future releases.
        /// </summary>
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.IsLogicalOperation())
            {
                var newLeft = Visit(node.Left);
                var newRight = Visit(node.Right);
                var constantLeft = newLeft as ConstantExpression;
                var constantRight = newRight as ConstantExpression;

                if (node.NodeType == ExpressionType.AndAlso)
                {
                    if ((constantLeft != null)
                        && (constantLeft.Type == typeof(bool)))
                    {
                        // true && a => a
                        // false && a => false
                        return (bool)constantLeft.Value ? newRight : newLeft;
                    }

                    if ((constantRight != null)
                        && (constantRight.Type == typeof(bool)))
                    {
                        // a && true => a
                        // a && false => false
                        return (bool)constantRight.Value ? newLeft : newRight;
                    }
                }

                if (node.NodeType == ExpressionType.OrElse)
                {
                    if ((constantLeft != null)
                        && (constantLeft.Type == typeof(bool)))
                    {
                        // true || a => true
                        // false || a => a
                        return (bool)constantLeft.Value ? newLeft : newRight;
                    }

                    if ((constantRight != null)
                        && (constantRight.Type == typeof(bool)))
                    {
                        // a || true => true
                        // a || false => a
                        return (bool)constantRight.Value ? newRight : newLeft;
                    }
                }

                return node.Update(newLeft, node.Conversion, newRight);
            }

            // a == true -> a
            if (node.NodeType == ExpressionType.Equal
                && node.Left.Type.UnwrapNullableType() == typeof(bool)
                && node.Right.Type.UnwrapNullableType() == typeof(bool))
            {
                var newLeft = Visit(node.Left);
                var newRight = Visit(node.Right);

                var leftConstant = newLeft as ConstantExpression;
                if (leftConstant != null && (bool?)leftConstant.Value == true)
                {
                    return newRight.Type == typeof(bool) ? newRight : Expression.Convert(newRight, typeof(bool));
                }

                var rightConstant = newRight as ConstantExpression;
                if (rightConstant != null && (bool?)rightConstant.Value == true)
                {
                    return newLeft.Type == typeof(bool) ? newLeft : Expression.Convert(newLeft, typeof(bool));
                }

                return node.Update(newLeft, node.Conversion, newRight);
            }

            return base.VisitBinary(node);
        }
    }
}
