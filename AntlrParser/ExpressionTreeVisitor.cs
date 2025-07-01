using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

namespace AntlrParser
{
    public class ExpressionTreeVisitor : DataTableExpressionBaseVisitor<Expression>
    {
        private readonly ParameterExpression _parameter;

        private static readonly MethodInfo ToDoubleMethod =
            typeof(NumericConverter).GetMethod("ToDouble");

        private static readonly MethodInfo ToDecimalMethod =
            typeof(NumericConverter).GetMethod("ToDecimal");

        private static readonly MethodInfo ConvertToBestTypeMethod =
            typeof(NumericConverter).GetMethod("ConvertToBestType");

        public ExpressionTreeVisitor(ParameterExpression parameter)
        {
            _parameter = parameter;
        }

        // Entry point for expressions
        public override Expression VisitExpression([NotNull] DataTableExpressionParser.ExpressionContext context)
        {
            return Visit(context.orExpression());
        }

        public override Expression VisitOrExpression([NotNull] DataTableExpressionParser.OrExpressionContext context)
        {
            Expression left = Visit(context.andExpression(0));
            for (int i = 1; i < context.andExpression().Length; i++)
            {
                Expression right = Visit(context.andExpression(i));
                left = Expression.OrElse(left, right);
            }

            return left;
        }

        public override Expression VisitAndExpression([NotNull] DataTableExpressionParser.AndExpressionContext context)
        {
            Expression left = Visit(context.notExpression(0));
            for (int i = 1; i < context.notExpression().Length; i++)
            {
                Expression right = Visit(context.notExpression(i));
                left = Expression.AndAlso(left, right);
            }

            return left;
        }

        public override Expression VisitNotExpression([NotNull] DataTableExpressionParser.NotExpressionContext context)
        {
            if (context.NOT() != null)
            {
                Expression operand = Visit(context.comparisonExpression());
                return Expression.Not(operand);
            }

            return Visit(context.comparisonExpression());
        }

        public override Expression VisitComparisonExpression(
            [NotNull] DataTableExpressionParser.ComparisonExpressionContext context)
        {
            Expression left = Visit(context.additiveExpression(0));

            if (context.IN() != null)
            {
                Expression right = Visit(context.inExpression());
                return CreateInExpression(left, right);
            }
            
            if (context.IS() != null)
            {
                // IS NULL or IS NOT NULL
                var isNotNull = context.NOT() != null;
                return isNotNull
                    ? Expression.NotEqual(left, Expression.Constant(null, typeof(object)))
                    : Expression.Equal(left, Expression.Constant(null, typeof(object)));
            }
            
            if (context.additiveExpression().Length > 1)
            {
                Expression right = Visit(context.additiveExpression(1));

                // Convert both sides to best comparable type
                left = ConvertForComparison(left);
                right = ConvertForComparison(right);
                
                if (context.LIKE() != null)
                {
                    return CreateLikeExpression(left, right);
                }

                return CreateComparisonCall(context, left, right);
            }

            return left;
        }

        private Expression CreateComparisonCall(
            DataTableExpressionParser.ComparisonExpressionContext context,
            Expression left, Expression right)
        {
            // If both sides are strings, use Expression.Equal/NotEqual directly
            if (left.Type == typeof(string) && right.Type == typeof(string))
            {
                if (context.EQUALS() != null)
                    return Expression.Equal(left, right);
                
                if (context.NOT_EQUALS() != null)
                    return Expression.NotEqual(left, right);
                
                // For other operators (like <, >), you may want to fall back to the helper
            }

            // Fall back to DataTableTypeConverter for other cases
            MethodInfo method;
            if (context.EQUALS() != null)
                method = typeof(DataTableTypeConverter).GetMethod("AreEqual");
            else if (context.NOT_EQUALS() != null)
                method = typeof(DataTableTypeConverter).GetMethod("AreNotEqual");
            else if (context.LESS_THAN() != null)
                method = typeof(DataTableTypeConverter).GetMethod("IsLessThan");
            else if (context.GREATER_THAN() != null)
                method = typeof(DataTableTypeConverter).GetMethod("IsGreaterThan");
            else if (context.LESS_THAN_OR_EQUAL() != null)
                method = typeof(DataTableTypeConverter).GetMethod("IsLessThanOrEqual");
            else if (context.GREATER_THAN_OR_EQUAL() != null)
                method = typeof(DataTableTypeConverter).GetMethod("IsGreaterThanOrEqual");
            else
                throw new NotSupportedException("Operator not supported");

            return Expression.Call(method, left, right);
        }

        private Expression ConvertToNumeric(Expression expr)
        {
            // For arithmetic operations, prefer double for maximum range
            return Expression.Call(ToDoubleMethod, Expression.Convert(expr, typeof(object)));
        }

        private Expression ConvertForComparison(Expression expr)
        {
            // For comparisons, use the most appropriate type
            return Expression.Call(ConvertToBestTypeMethod, Expression.Convert(expr, typeof(object)));
        }

        public override Expression VisitFunctionCall(DataTableExpressionParser.FunctionCallContext context)
        {
            string functionName = context.functionName().GetText().ToUpperInvariant();
            var args = context.argumentList()?.expression().Select(Visit).ToArray() ?? Array.Empty<Expression>();

            // Ensure all arguments are non-null
            if (args.Any(a => a == null))
                throw new InvalidOperationException("Function argument expression cannot be null.");

            switch (functionName)
            {
                case "CONVERT":
                    return HandleConvert(args);
                case "LEN":
                    return HandleLen(args);
                case "ISNULL":
                    return HandleIsNull(args);
                case "IIF":
                    return HandleIif(args);
                case "TRIM":
                    return HandleTrim(args);
                case "SUBSTRING":
                    return HandleSubstring(args);
                case "SUM":
                case "AVG":
                case "MIN":
                case "MAX":
                case "COUNT":
                case "STDEV":
                case "VAR":
                    return HandleAggregate(functionName, args);
                default:
                    throw new NotSupportedException($"Function '{functionName}' is not supported.");
            }
        }

        private Expression HandleConvert(Expression[] args)
        {
            if (args.Length != 2)
                throw new ArgumentException("CONVERT expects 2 arguments: expression and type name.");
            if (!(args[1] is ConstantExpression typeNameExpr) || !(typeNameExpr.Value is string))
                throw new ArgumentException("Second argument to CONVERT must be a string literal type name.");

            string typeName = (string)typeNameExpr.Value;
            Type targetType = Type.GetType(typeName, throwOnError: true);
            return Expression.Convert(args[0], targetType);
        }

        private Expression HandleLen(Expression[] args)
        {
            if (args.Length != 1)
                throw new ArgumentException("LEN expects 1 argument.");
            Expression arg = args[0];
            if (arg.Type != typeof(string))
                arg = Expression.Call(arg, typeof(object).GetMethod("ToString"));
            var lengthProp = typeof(string).GetProperty("Length");
            return Expression.Property(arg, lengthProp);
        }

        private Expression HandleIsNull(Expression[] args)
        {
            if (args.Length != 2)
                throw new ArgumentException("ISNULL expects 2 arguments.");

            Expression checkExpr = args[0];
            Expression replacementExpr = args[1];

            // Convert checkExpr to object if not already (for null check)
            if (checkExpr.Type != typeof(object))
                checkExpr = Expression.Convert(checkExpr, typeof(object));

            // Convert replacementExpr to string if not already
            if (replacementExpr.Type != typeof(string))
                replacementExpr = Expression.Convert(replacementExpr, typeof(string));

            // If checkExpr is a string, convert to string before returning
            Expression resultExpr = Expression.Condition(
                Expression.NotEqual(checkExpr, Expression.Constant(null, typeof(object))),
                Expression.Call(checkExpr, typeof(object).GetMethod("ToString")),
                replacementExpr,
                typeof(string)
            );

            return resultExpr;
        }

        private Expression HandleIif(Expression[] args)
        {
            if (args.Length != 3)
                throw new ArgumentException("IIF expects 3 arguments.");

            // Convert condition to bool
            Expression condition = args[0];
            if (condition.Type != typeof(bool))
                condition = Expression.Convert(condition, typeof(bool));

            Expression truePart = args[1];
            Expression falsePart = args[2];

            // Coerce types if necessary
            if (truePart.Type != falsePart.Type)
            {
                if (truePart.Type.IsAssignableFrom(falsePart.Type))
                    falsePart = Expression.Convert(falsePart, truePart.Type);
                else if (falsePart.Type.IsAssignableFrom(truePart.Type))
                    truePart = Expression.Convert(truePart, falsePart.Type);
                else
                    throw new InvalidOperationException("IIF true and false parts must have compatible types.");
            }

            return Expression.Condition(condition, truePart, falsePart);
        }

        private Expression HandleTrim(Expression[] args)
        {
            if (args.Length != 1)
                throw new ArgumentException("TRIM expects 1 argument.");
            var arg = args[0];
            if (arg.Type != typeof(string))
                arg = Expression.Call(arg, typeof(object).GetMethod("ToString"));
            var trimMethod = typeof(string).GetMethod("Trim", Type.EmptyTypes);
            return Expression.Call(arg, trimMethod);
        }

        private Expression HandleSubstring(Expression[] args)
        {
            if (args.Length != 3)
                throw new ArgumentException("SUBSTRING expects 3 arguments.");
            var strExpr = args[0];
            var startExpr = args[1];
            var lengthExpr = args[2];
            if (strExpr.Type != typeof(string))
                strExpr = Expression.Call(strExpr, typeof(object).GetMethod("ToString"));
            var substringMethod = typeof(string).GetMethod("Substring", new[] { typeof(int), typeof(int) });
            return Expression.Call(strExpr, substringMethod, startExpr, lengthExpr);
        }

        private Expression HandleAggregate(string functionName, Expression[] args)
        {
            // Aggregate functions are not supported in-memory for single-row evaluation
            // (they require iterating over a collection)
            throw new NotSupportedException($"Aggregate function '{functionName}' is not supported in this context.");
        }

        public override Expression VisitAdditiveExpression(DataTableExpressionParser.AdditiveExpressionContext context)
        {
            Expression result = Visit(context.multiplicativeExpression(0));

            for (int i = 1; i < context.multiplicativeExpression().Length; i++)
            {
                Expression right = Visit(context.multiplicativeExpression(i));

                // Get the operator terminal node (a parse tree node, not a token)
                // The operator is at index i*2-1 in the children list
                var opNode = context.GetChild(i * 2 - 1) as ITerminalNode;
                if (opNode == null)
                    throw new InvalidOperationException("Expected operator node");

                // Get the token from the terminal node
                IToken opToken = opNode.Symbol;

                result = ApplyAdditiveOperator(result, right, opToken);
            }

            return result;
        }

        private Expression ApplyAdditiveOperator(Expression left, Expression right, IToken opToken)
        {
            left = ConvertToNumeric(left);
            right = ConvertToNumeric(right);

            if (opToken.Type == DataTableExpressionParser.PLUS)
                return Expression.Add(left, right);
            if (opToken.Type == DataTableExpressionParser.MINUS)
                return Expression.Subtract(left, right);
            throw new NotSupportedException($"Operator '{opToken.Text}' not supported");
        }

        public override Expression VisitMultiplicativeExpression(
            DataTableExpressionParser.MultiplicativeExpressionContext context)
        {
            Expression result = Visit(context.unaryExpression(0));

            for (int i = 1; i < context.unaryExpression().Length; i++)
            {
                Expression right = Visit(context.unaryExpression(i));

                // Get the operator terminal node
                var opNode = context.GetChild(i * 2 - 1) as ITerminalNode;
                if (opNode == null)
                    throw new InvalidOperationException("Expected operator node");

                // Get the token from the terminal node
                IToken opToken = opNode.Symbol;

                result = ApplyMultiplicativeOperator(result, right, opToken);
            }

            return result;
        }

        private Expression ApplyMultiplicativeOperator(Expression left, Expression right, IToken opToken)
        {
            left = ConvertToNumeric(left);
            right = ConvertToNumeric(right);

            if (opToken.Type == DataTableExpressionParser.MULTIPLY)
                return Expression.Multiply(left, right);
            if (opToken.Type == DataTableExpressionParser.DIVIDE)
                return Expression.Divide(left, right);
            if (opToken.Type == DataTableExpressionParser.MODULO)
                return Expression.Modulo(left, right);
            throw new NotSupportedException($"Operator '{opToken.Text}' not supported");
        }

        public override Expression VisitUnaryExpression(
            [NotNull] DataTableExpressionParser.UnaryExpressionContext context)
        {
            Expression expr = Visit(context.primaryExpression());
            if (context.PLUS() != null)
                return Expression.UnaryPlus(expr);
            if (context.MINUS() != null)
                return Expression.Negate(expr);
            return expr;
        }

        public override Expression VisitPrimaryExpression(
            [NotNull] DataTableExpressionParser.PrimaryExpressionContext context)
        {
            if (context.LPAREN() != null)
                return Visit(context.expression());
            if (context.functionCall() != null)
                return Visit(context.functionCall());
            if (context.columnReference() != null)
                return Visit(context.columnReference());
            if (context.literal() != null)
                return Visit(context.literal());
            throw new InvalidOperationException("Invalid primary expression");
        }

        public override Expression VisitLiteral(DataTableExpressionParser.LiteralContext context)
        {
            if (context.STRING_LITERAL() != null)
            {
                string value = context.STRING_LITERAL().GetText();
                value = value.Substring(1, value.Length - 2);
                return Expression.Constant(value, typeof(object));
            }

            if (context.INTEGER_LITERAL() != null)
            {
                string numberText = context.INTEGER_LITERAL().GetText();
                return Expression.Constant(int.Parse(numberText, CultureInfo.InvariantCulture), typeof(object));
            }

            if (context.DECIMAL_LITERAL() != null)
            {
                string numberText = context.DECIMAL_LITERAL().GetText();
                return Expression.Constant(decimal.Parse(numberText, CultureInfo.InvariantCulture), typeof(object));
            }

            if (context.BOOLEAN_LITERAL() != null)
            {
                bool value = bool.Parse(context.BOOLEAN_LITERAL().GetText());
                return Expression.Constant(value, typeof(object));
            }

            if (context.DATE_LITERAL() != null)
            {
                string dateText = context.DATE_LITERAL().GetText();
                dateText = dateText.Substring(1, dateText.Length - 2);
                DateTime dateValue = DateTime.Parse(dateText, CultureInfo.InvariantCulture);
                return Expression.Constant(dateValue, typeof(object));
            }

            if (context.NULL_LITERAL() != null)
            {
                return Expression.Constant(null, typeof(object));
            }

            throw new InvalidOperationException("Invalid literal");
        }

        public override Expression VisitColumnReference(DataTableExpressionParser.ColumnReferenceContext context)
        {
            string columnName = ExtractColumnName(context);
            var keyExpression = Expression.Constant(columnName);
            var indexerInfo = typeof(Dictionary<string, object>).GetProperty("Item");
            return Expression.MakeIndex(_parameter, indexerInfo, new[] { keyExpression });
        }

        // Helper methods
        private string ExtractColumnName(DataTableExpressionParser.ColumnReferenceContext context)
        {
            string text = context.GetText();

            // Handle [ColumnName] format
            if (text.StartsWith("[") && text.EndsWith("]"))
            {
                return text.Substring(1, text.Length - 2)
                    .Replace("\\]", "]")
                    .Replace("\\\\", "\\");
            }

            // Handle `ColumnName` format  
            if (text.StartsWith("`") && text.EndsWith("`"))
            {
                return text.Substring(1, text.Length - 2);
            }

            // Regular column name
            return text;
        }

        private Expression CreateLikeExpression(Expression left, Expression right)
        {
            // Convert both to strings for LIKE operation
            var leftString = Expression.Call(left, typeof(object).GetMethod("ToString"));
            var rightString = Expression.Call(right, typeof(object).GetMethod("ToString"));

            // Implement LIKE with wildcards (* and %)
            var likeMethod = typeof(DataTableLikeOperator).GetMethod("Like");
            return Expression.Call(likeMethod, leftString, rightString);
        }
        
        public override Expression VisitInList([NotNull] DataTableExpressionParser.InListContext context)
        {
            var values = new List<object>();
            foreach (var expr in context._expr)
            {
                var valueExpr = Visit(expr);
                if (valueExpr is ConstantExpression constant)
                    values.Add(constant.Value);
                else
                    throw new NotSupportedException("IN operator values must be constant expressions.");
            }
            return Expression.Constant(values, typeof(List<object>));
        }
        
        private Expression CreateInExpression(Expression left, Expression right)
        {
            if (!(right is ConstantExpression constantRight) || !(constantRight.Value is IEnumerable<object> values))
                throw new InvalidOperationException("IN operator requires a list of values on the right.");

            if (left.Type != typeof(object))
                left = Expression.Convert(left, typeof(object));

            var containsMethod = typeof(Enumerable).GetMethods()
                .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
                .MakeGenericMethod(typeof(object));

            return Expression.Call(containsMethod, right, left);
        }
    }
}