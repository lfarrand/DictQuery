namespace AntlrParser
{
    using Antlr4.Runtime;
    using LazyCache;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using System.Threading;
    using System.Globalization;

    public class QueryVisitor : QueryBaseVisitor<Expression>
    {
        public ParameterExpression Parameter { get; } = Expression.Parameter(typeof(Dictionary<string, object>), "row");

        private readonly List<Dictionary<string, object>> mainData;
        private readonly Dictionary<string, List<Dictionary<string, object>>> childData;
        private readonly IAppCache cache;
        private readonly ReaderWriterLockSlim cacheLock;
        private readonly bool caseSensitive;
        private readonly CultureInfo invariantCulture = CultureInfo.InvariantCulture;
        private readonly HashSet<string> enumColumns;

        public QueryVisitor(bool caseSensitive = false, List<Dictionary<string, object>> mainData = null,
            Dictionary<string, List<Dictionary<string, object>>> childData = null, IAppCache cache = null,
            HashSet<string> enumColumns = null)
        {
            this.caseSensitive = caseSensitive;
            this.mainData = mainData ?? new List<Dictionary<string, object>>();
            this.childData = childData ??
                             new Dictionary<string, List<Dictionary<string, object>>>(StringComparer.OrdinalIgnoreCase);
            this.cache = cache ?? new CachingService();
            this.cacheLock = new ReaderWriterLockSlim();
            this.enumColumns = enumColumns ?? new HashSet<string>();
        }

        public Func<Dictionary<string, object>, bool> ParseQuery(string query)
        {
            cacheLock.EnterReadLock();
            try
            {
                if (cache.Get<Func<Dictionary<string, object>, bool>>(query) is Func<Dictionary<string, object>, bool>
                    cachedPredicate)
                    return cachedPredicate;
            }
            finally
            {
                cacheLock.ExitReadLock();
            }

            cacheLock.EnterWriteLock();
            try
            {
                if (cache.Get<Func<Dictionary<string, object>, bool>>(query) is Func<Dictionary<string, object>, bool>
                    cachedPredicate)
                    return cachedPredicate;

                var inputStream = new AntlrInputStream(query);
                var lexer = new QueryLexer(inputStream);
                var tokenStream = new CommonTokenStream(lexer);
                var parser = new QueryParser(tokenStream);
                parser.RemoveErrorListeners();
                parser.AddErrorListener(new CustomErrorListener());
                parser.BuildParseTree = true;
                var tree = parser.query();
                var expression = Visit(tree);

                if (expression.Type != typeof(bool))
                    expression = Expression.NotEqual(expression, Expression.Constant(null, expression.Type));

                var predicate = Expression.Lambda<Func<Dictionary<string, object>, bool>>(expression, Parameter)
                    .Compile();
                cache.Add(query, predicate, new CacheItemPolicy { SlidingExpiration = TimeSpan.FromHours(1) });
                return predicate;
            }
            finally
            {
                cacheLock.ExitWriteLock();
            }
        }

        public override Expression VisitExpressionQuery(QueryParser.ExpressionQueryContext context)
        {
            return Visit(context.expression()) ??
                   throw new InvalidOperationException("Expression query cannot be null");
        }

        public override Expression VisitFunctionQuery(QueryParser.FunctionQueryContext context)
        {
            return Visit(context.function()) ?? throw new InvalidOperationException("Function query cannot be null");
        }

        public override Expression VisitLogicalExpression(QueryParser.LogicalExpressionContext context)
        {
            var left = Visit(context.expression(0));
            var right = Visit(context.expression(1));
            if (left == null || right == null)
                throw new InvalidOperationException("Logical expression operands cannot be null");

            left = EnsureBoolean(left);
            right = EnsureBoolean(right);

            var op = context.op.Text.ToUpper();
            return op == "AND" ? Expression.AndAlso(left, right) : Expression.OrElse(left, right);
        }

        public override Expression VisitBinaryCondition(QueryParser.BinaryConditionContext context)
        {
            var left = Visit(context.expression(0));
            var right = Visit(context.expression(1));
            if (left == null || right == null)
                throw new InvalidOperationException("Binary condition operands cannot be null");

            var leftType = GetExpressionType(left);
            var rightType = GetExpressionType(right);
            var targetType = DetermineTargetType(leftType, rightType, context);

            left = SafeConvert(left, targetType);
            right = SafeConvert(right, targetType);

            if (targetType == typeof(string))
            {
                var comparison = caseSensitive ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                var equalsMethod = typeof(string).GetMethod("Equals",
                    new[] { typeof(string), typeof(string), typeof(StringComparison) });
                var compareMethod = typeof(string).GetMethod("Compare",
                    new[] { typeof(string), typeof(string), typeof(StringComparison) });
                switch (context.op.Text)
                {
                    case "=": return Expression.Call(equalsMethod, left, right, Expression.Constant(comparison));
                    case "<>":
                        return Expression.Not(Expression.Call(equalsMethod, left, right,
                            Expression.Constant(comparison)));
                    case ">":
                        return Expression.GreaterThan(
                            Expression.Call(compareMethod, left, right, Expression.Constant(comparison)),
                            Expression.Constant(0));
                    case "<":
                        return Expression.LessThan(
                            Expression.Call(compareMethod, left, right, Expression.Constant(comparison)),
                            Expression.Constant(0));
                    case ">=":
                        return Expression.GreaterThanOrEqual(
                            Expression.Call(compareMethod, left, right, Expression.Constant(comparison)),
                            Expression.Constant(0));
                    case "<=":
                        return Expression.LessThanOrEqual(
                            Expression.Call(compareMethod, left, right, Expression.Constant(comparison)),
                            Expression.Constant(0));
                }
            }

            switch (context.op.Text)
            {
                case "=": return Expression.Equal(left, right);
                case "<>": return Expression.NotEqual(left, right);
                case ">": return Expression.GreaterThan(left, right);
                case "<": return Expression.LessThan(left, right);
                case ">=": return Expression.GreaterThanOrEqual(left, right);
                case "<=": return Expression.LessThanOrEqual(left, right);
                default: throw new NotSupportedException($"Operator {context.op.Text} not supported.");
            }
        }

        public override Expression VisitConvertFunction(QueryParser.ConvertFunctionContext context)
        {
            var expr = Visit(context.expression());
            var typeName = context.type().GetText().Trim('\'');
            var targetType = Type.GetType(typeName) ?? throw new ArgumentException($"Invalid type: {typeName}");
            return SafeConvert(expr, targetType);
        }

        public override Expression VisitLenFunction(QueryParser.LenFunctionContext context)
        {
            var expr = SafeConvert(Visit(context.expression()), typeof(string));
            return Expression.Property(expr, "Length");
        }

        public override Expression VisitSubstringFunction(QueryParser.SubstringFunctionContext context)
        {
            var expr = SafeConvert(Visit(context.expression(0)), typeof(string));
            var start = SafeConvert(Visit(context.expression(1)), typeof(int));
            var length = SafeConvert(Visit(context.expression(2)), typeof(int));
            return Expression.Call(expr, typeof(string).GetMethod("Substring", new[] { typeof(int), typeof(int) }),
                start, length);
        }

        public override Expression VisitAggregateFunction(QueryParser.AggregateFunctionContext context)
        {
            var aggregate = context.aggregate().GetText().ToUpper();
            var columnCtx = context.parentChildExpression().GetChild<QueryParser.ColumnNameContext>(0);
            var columnName = GetColumnName(columnCtx);
            var rows = mainData; // Default to main data for simplicity in this example

            switch (aggregate)
            {
                case "SUM":
                    var sum = rows.Select(row => Convert.ToDouble(row.ContainsKey(columnName) ? row[columnName] : 0))
                        .Sum();
                    return Expression.Constant(sum, typeof(double));
                default: throw new NotSupportedException($"Aggregate {aggregate} not supported.");
            }
        }

        public override Expression VisitColumnExpression(QueryParser.ColumnExpressionContext context)
        {
            var columnName = GetColumnName(context.columnName());
            var keyExpr = Expression.Constant(columnName);
            var access = Expression.Property(Parameter, "Item", keyExpr);
            return Expression.Condition(Expression.Call(Parameter, "ContainsKey", null, keyExpr), access,
                Expression.Constant(null));
        }

        public override Expression VisitLiteralExpression(QueryParser.LiteralExpressionContext context)
        {
            return Expression.Constant(ParseLiteral(context.literal()), typeof(object));
        }

        private object ParseLiteral(QueryParser.LiteralContext literal)
        {
            if (literal.STRING_LITERAL() != null)
                return literal.GetText().Trim('\'').Replace("''", "'");
            if (literal.INTEGER() != null)
            {
                var text = literal.GetText();
                if (decimal.TryParse(text, NumberStyles.Number, invariantCulture, out decimal decValue))
                    return decValue;
                throw new ArgumentException($"Invalid integer literal: {text}");
            }

            if (literal.DATE_LITERAL() != null)
            {
                var text = literal.GetText().Trim('#');
                if (DateTime.TryParse(text, invariantCulture, DateTimeStyles.None, out DateTime date))
                    return date;
                throw new ArgumentException($"Invalid date literal: {text}");
            }

            // Add other literal types as needed
            throw new NotImplementedException($"Literal type {literal.GetText()} not supported.");
        }

        private Type GetExpressionType(Expression expr)
        {
            if (expr.Type == typeof(object) && expr is ConstantExpression constExpr && constExpr.Value != null)
                return constExpr.Value.GetType();
            return expr.Type;
        }

        private Expression SafeConvert(Expression expr, Type targetType)
        {
            if (expr.Type == targetType) return expr;
            var nullCheck = Expression.Equal(expr, Expression.Constant(null));
            if (targetType == typeof(string))
                return Expression.Condition(nullCheck, Expression.Constant(""), Expression.Convert(expr, targetType));
            if (targetType == typeof(int))
                return Expression.Condition(nullCheck, Expression.Constant(0), Expression.Convert(expr, targetType));
            if (targetType == typeof(double))
                return Expression.Condition(nullCheck, Expression.Constant(0.0), Expression.Convert(expr, targetType));
            if (targetType == typeof(decimal))
                return Expression.Condition(nullCheck, Expression.Constant(0m), Expression.Convert(expr, targetType));
            if (targetType == typeof(DateTime))
                return Expression.Condition(nullCheck, Expression.Constant(DateTime.MinValue),
                    Expression.Convert(expr, targetType));
            return Expression.Convert(expr, targetType);
        }

        private Expression EnsureBoolean(Expression expr)
        {
            if (expr.Type == typeof(bool)) return expr;
            return Expression.NotEqual(expr, Expression.Constant(null, expr.Type));
        }

        private Type DetermineTargetType(Type leftType, Type rightType, QueryParser.BinaryConditionContext context)
        {
            if (leftType == typeof(DateTime) || rightType == typeof(DateTime)) return typeof(DateTime);
            if (leftType == typeof(string) || rightType == typeof(string)) return typeof(string);
            if (leftType == typeof(decimal) || rightType == typeof(decimal)) return typeof(decimal);
            if (leftType == typeof(double) || rightType == typeof(double)) return typeof(double);
            return typeof(int); // Default for simplicity
        }

        private string GetColumnName(QueryParser.ColumnNameContext context)
        {
            if (context.IDENTIFIER() != null) return context.IDENTIFIER().GetText();
            if (context.BRACKETED_IDENTIFIER() != null) return context.BRACKETED_IDENTIFIER().GetText().Trim('[', ']');
            throw new ArgumentException($"Invalid column name: {context.GetText()}");
        }
    }
}