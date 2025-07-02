using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

namespace AntlrParser8;

public class ExpressionTreeVisitor : DataTableExpressionBaseVisitor<Expression>
{
    public ParameterExpression Parameter { get; }

    private static readonly MethodInfo ToDoubleMethod =
        typeof(NumericConverter).GetMethod("ToDouble");

    private static readonly MethodInfo ToDecimalMethod =
        typeof(NumericConverter).GetMethod("ToDecimal");

    private static readonly MethodInfo ConvertToBestTypeMethod =
        typeof(NumericConverter).GetMethod("ConvertToBestType");

    private static readonly MethodInfo GetColumnValueMethod =
        typeof(ExpressionTreeVisitor).GetMethod(nameof(GetColumnValue),
            BindingFlags.Static | BindingFlags.NonPublic);

    private readonly IEnumerable<Dictionary<string, object>> _data;

    public ExpressionTreeVisitor(ParameterExpression parameter, IEnumerable<Dictionary<string, object>> data)
    {
        Parameter = parameter;
        _data = data;
    }

    // Entry point for expressions
    public override Expression VisitExpression([NotNull] DataTableExpressionParser.ExpressionContext context)
    {
        return Visit(context.orExpression());
    }

    public override Expression VisitOrExpression([NotNull] DataTableExpressionParser.OrExpressionContext context)
    {
        var left = Visit(context.andExpression(0));
        for (var i = 1; i < context.andExpression().Length; i++)
        {
            var right = Visit(context.andExpression(i));
            left = Expression.OrElse(left, right);
        }

        return left;
    }

    public override Expression VisitAndExpression([NotNull] DataTableExpressionParser.AndExpressionContext context)
    {
        var left = Visit(context.notExpression(0));
        for (var i = 1; i < context.notExpression().Length; i++)
        {
            var right = Visit(context.notExpression(i));
            left = Expression.AndAlso(left, right);
        }

        return left;
    }

    public override Expression VisitNotExpression(DataTableExpressionParser.NotExpressionContext context)
    {
        if (context.NOT() != null)
        {
            var operand = Visit(context.comparisonExpression());
            // Ensure operand is boolean
            if (operand.Type != typeof(bool))
            {
                operand = Expression.Convert(operand, typeof(bool));
            }

            return Expression.Not(operand);
        }

        return Visit(context.comparisonExpression());
    }

    public Expression ConvertConstantToType(Expression expr, Type targetType)
    {
        if (expr is ConstantExpression constExpr)
        {
            var value = constExpr.Value;
            try
            {
                // If the value is null, propagate null (or default for value types)
                if (value == null)
                {
                    // If targetType is a non-nullable value type, use Expression.Default
                    if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
                    {
                        return Expression.Default(targetType);
                    }
                    else
                    {
                        return Expression.Constant(null, targetType);
                    }
                }

                // If already the correct type, return as is
                if (targetType.IsInstanceOfType(value))
                {
                    return Expression.Constant(value, targetType);
                }

                // Special handling for string to number, number to string, etc.
                if (targetType == typeof(string))
                {
                    return Expression.Constant(Convert.ToString(value, CultureInfo.InvariantCulture),
                        typeof(string));
                }

                if (targetType == typeof(int))
                {
                    return Expression.Constant(Convert.ToInt32(value, CultureInfo.InvariantCulture), typeof(int));
                }

                if (targetType == typeof(long))
                {
                    return Expression.Constant(Convert.ToInt64(value, CultureInfo.InvariantCulture), typeof(long));
                }

                if (targetType == typeof(double))
                {
                    return Expression.Constant(Convert.ToDouble(value, CultureInfo.InvariantCulture),
                        typeof(double));
                }

                if (targetType == typeof(decimal))
                {
                    return Expression.Constant(Convert.ToDecimal(value, CultureInfo.InvariantCulture),
                        typeof(decimal));
                }

                if (targetType == typeof(bool))
                {
                    return Expression.Constant(Convert.ToBoolean(value, CultureInfo.InvariantCulture),
                        typeof(bool));
                }

                if (targetType == typeof(DateTime))
                {
                    return Expression.Constant(Convert.ToDateTime(value, CultureInfo.InvariantCulture),
                        typeof(DateTime));
                }

                // Fallback: try general conversion
                return Expression.Constant(Convert.ChangeType(value, targetType, CultureInfo.InvariantCulture),
                    targetType);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Cannot convert literal '{value}' to type {targetType.Name} for comparison.", ex);
            }
        }

        // Not a constant, return as is
        return expr;
    }

    public override Expression VisitComparisonExpression(
        [NotNull] DataTableExpressionParser.ComparisonExpressionContext context)
    {
        var left = Visit(context.additiveExpression(0));

        if (context.IN() != null)
        {
            var right = Visit(context.inExpression());
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
            var right = Visit(context.additiveExpression(1));

            // Infer target type
            var targetType = typeof(object);
            if (left is MethodCallExpression leftCall && leftCall.Method == GetColumnValueMethod)
            {
                var columnName = ((ConstantExpression)leftCall.Arguments[1]).Value as string;
                targetType = GetColumnType(columnName);
            }
            else if (right is MethodCallExpression rightCall && rightCall.Method == GetColumnValueMethod)
            {
                var columnName = ((ConstantExpression)rightCall.Arguments[1]).Value as string;
                targetType = GetColumnType(columnName);
            }

            left = ConvertForComparison(left, targetType);
            right = ConvertForComparison(right, targetType);

            if (context.LIKE() != null)
            {
                return CreateLikeExpression(left, right);
            }

            return CreateComparisonCall(context, left, right);
        }

        return left;
    }

    public Expression CreateComparisonCall(
        DataTableExpressionParser.ComparisonExpressionContext context,
        Expression left, Expression right)
    {
        // If left is a column reference, infer type from data
        if (left is MethodCallExpression leftCall && leftCall.Method == GetColumnValueMethod &&
            right is ConstantExpression rightConst)
        {
            var columnName = ((ConstantExpression)leftCall.Arguments[1]).Value as string;
            var columnType = GetColumnType(columnName);

            var rightValue = NumericConverter.ConvertToBestType(rightConst.Value, columnType);

            // Try to convert right to column's type, throw ArgumentException if not possible
            right = Expression.Constant(rightValue, columnType);
            left = Expression.Convert(left, columnType);

            // For relational operators, check type
            if (context.LESS_THAN() != null || context.GREATER_THAN() != null ||
                context.LESS_THAN_OR_EQUAL() != null || context.GREATER_THAN_OR_EQUAL() != null)
                // Only allow for numeric or DateTime
            {
                if (!IsNumericType(columnType) && columnType != typeof(DateTime))
                {
                    throw new InvalidOperationException(
                        $"Operator '{context.GetText()}' not supported for type '{columnType.Name}'");
                }
            }
        }
        else if (right is MethodCallExpression rightCall && rightCall.Method == GetColumnValueMethod &&
                 left is ConstantExpression)
        {
            var columnName = ((ConstantExpression)rightCall.Arguments[1]).Value as string;
            var columnType = GetColumnType(columnName);

            left = ConvertConstantToType(left, columnType);
            right = Expression.Convert(right, columnType);

            if (context.LESS_THAN() != null || context.GREATER_THAN() != null ||
                context.LESS_THAN_OR_EQUAL() != null || context.GREATER_THAN_OR_EQUAL() != null)
            {
                if (!IsNumericType(columnType) && columnType != typeof(DateTime))
                {
                    throw new InvalidOperationException(
                        $"Operator '{context.GetText()}' not supported for type '{columnType.Name}'");
                }
            }
        }
        else if (left is MethodCallExpression leftCall2 && leftCall2.Method == ConvertToBestTypeMethod &&
                 right is MethodCallExpression rightCall2 && rightCall2.Method == ConvertToBestTypeMethod)
        {
            var leftSecondArg = leftCall2.Arguments[1];
            var leftTargetType = leftSecondArg.Type;
            if (leftSecondArg is ConstantExpression constExprL && constExprL.Value is Type leftTypeObj)
            {
                leftTargetType = leftTypeObj.UnderlyingSystemType;
            }

            var rightSecondArg = rightCall2.Arguments[1];
            var rightTargetType = rightSecondArg.Type;
            if (rightSecondArg is ConstantExpression constExprR && constExprR.Value is Type rightTypeObj)
            {
                rightTargetType = rightTypeObj.UnderlyingSystemType;
            }

            if (leftTargetType.FullName == typeof(string).FullName &&
                rightTargetType.FullName == typeof(string).FullName)
            {
                if (context.EQUALS() == null && context.NOT_EQUALS() == null)
                {
                    throw new InvalidOperationException(
                        $"Operator '{context.GetText()}' not supported for type 'string'");
                }
            }

            if (leftTargetType.FullName == typeof(bool).FullName &&
                rightTargetType.FullName == typeof(bool).FullName)
            {
                if (context.LESS_THAN() != null || context.GREATER_THAN() != null ||
                    context.LESS_THAN_OR_EQUAL() != null || context.GREATER_THAN_OR_EQUAL() != null)
                {
                    throw new InvalidOperationException(
                        $"Operator '{context.GetText()}' not supported for type 'bool'");
                }
            }
        }

        if (context.LESS_THAN() != null || context.GREATER_THAN() != null ||
            context.LESS_THAN_OR_EQUAL() != null || context.GREATER_THAN_OR_EQUAL() != null)
        {
            if (IsBoolExpression(left) && IsBoolExpression(right))
            {
                throw new InvalidOperationException(
                    $"Operator '{context.GetText()}' not supported for type 'bool'");
            }
        }

        // Fall back to DataTableTypeConverter for other cases
        MethodInfo method;
        if (context.EQUALS() != null)
        {
            method = typeof(DataTableTypeConverter).GetMethod("AreEqual");
        }
        else if (context.NOT_EQUALS() != null)
        {
            method = typeof(DataTableTypeConverter).GetMethod("AreNotEqual");
        }
        else if (context.LESS_THAN() != null)
        {
            method = typeof(DataTableTypeConverter).GetMethod("IsLessThan");
        }
        else if (context.GREATER_THAN() != null)
        {
            method = typeof(DataTableTypeConverter).GetMethod("IsGreaterThan");
        }
        else if (context.LESS_THAN_OR_EQUAL() != null)
        {
            method = typeof(DataTableTypeConverter).GetMethod("IsLessThanOrEqual");
        }
        else if (context.GREATER_THAN_OR_EQUAL() != null)
        {
            method = typeof(DataTableTypeConverter).GetMethod("IsGreaterThanOrEqual");
        }
        else
        {
            throw new NotSupportedException("Operator not supported");
        }

        // Always box both operands to object before calling the comparison method
        var leftObj = Expression.Convert(left, typeof(object));
        var rightObj = Expression.Convert(right, typeof(object));
        Expression comparison = Expression.Call(method, leftObj, rightObj);

        return WrapComparisonWithNullCheck(leftObj, rightObj, comparison);
    }

    private bool IsBoolExpression(Expression expr)
    {
        if (expr.Type == typeof(bool))
        {
            return true;
        }

        if (expr is ConstantExpression ce && ce.Type == typeof(object) && ce.Value is bool)
        {
            return true;
        }

        return false;
    }

    private Expression WrapComparisonWithNullCheck(Expression left, Expression right, Expression comparison)
    {
        // Box both operands to object, then check for null
        var leftObj = Expression.Convert(left, typeof(object));
        var rightObj = Expression.Convert(right, typeof(object));
        return Expression.Condition(
            Expression.OrElse(
                Expression.Equal(leftObj, Expression.Constant(null, typeof(object))),
                Expression.Equal(rightObj, Expression.Constant(null, typeof(object)))
            ),
            Expression.Constant(false),
            comparison
        );
    }


    public bool IsNumericType(Type type)
    {
        return type == typeof(byte) || type == typeof(sbyte) ||
               type == typeof(short) || type == typeof(ushort) ||
               type == typeof(int) || type == typeof(uint) ||
               type == typeof(long) || type == typeof(ulong) ||
               type == typeof(float) || type == typeof(double) ||
               type == typeof(decimal);
    }

    private Type GetColumnType(string columnName)
    {
        foreach (var row in _data)
        {
            if (row.TryGetValue(columnName, out var value) && value != null)
            {
                return value.GetType();
            }
        }

        // If column never found or always null, default to string or object
        return typeof(object);
    }

    private Expression ConvertToNumeric(Expression expr)
    {
        // For arithmetic operations, prefer double for maximum range
        return Expression.Call(ToDoubleMethod, Expression.Convert(expr, typeof(object)));
    }

    private Expression ConvertForComparison(Expression expr, Type targetType)
    {
        // Box expr to object for null check
        var exprObj = Expression.Convert(expr, typeof(object));
        var nullCheck = Expression.Equal(exprObj, Expression.Constant(null, typeof(object)));
        var convertExpr = Expression.Call(
            ConvertToBestTypeMethod,
            exprObj,
            Expression.Constant(targetType)
        );

        // Use nullable type if targetType is a non-nullable value type
        var resultType = targetType;
        if (targetType.IsValueType && Nullable.GetUnderlyingType(targetType) == null)
        {
            resultType = typeof(Nullable<>).MakeGenericType(targetType);
        }

        return Expression.Condition(
            nullCheck,
            Expression.Constant(null, resultType),
            Expression.Convert(convertExpr, resultType)
        );
    }

    public override Expression VisitFunctionCall(DataTableExpressionParser.FunctionCallContext context)
    {
        var functionName = context.functionName().GetText().ToUpperInvariant();
        var args = context.argumentList()?.expression().Select(Visit).ToArray() ?? Array.Empty<Expression>();

        // Ensure all arguments are non-null
        if (args.Any(a => a == null))
        {
            throw new InvalidOperationException("Function argument expression cannot be null.");
        }

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

    public Expression HandleConvert(Expression[] args)
    {
        if (args.Length != 2)
        {
            throw new ArgumentException("CONVERT expects 2 arguments: expression and type name.");
        }

        if (!(args[1] is ConstantExpression typeNameExpr) || !(typeNameExpr.Value is string))
        {
            throw new ArgumentException("Second argument to CONVERT must be a string literal type name.");
        }

        var typeName = (string)typeNameExpr.Value;
        var targetType = Type.GetType(typeName, true);

        var arg = args[0];

        // If arg is nullable (object), check for null before converting
        if (arg.Type == typeof(object))
        {
            var changeTypeMethod = typeof(Convert).GetMethod(nameof(Convert.ChangeType),
                new[] { typeof(object), typeof(Type), typeof(IFormatProvider) });
            var convertExpr = Expression.Convert(
                Expression.Call(
                    changeTypeMethod,
                    arg,
                    Expression.Constant(targetType),
                    Expression.Constant(CultureInfo.InvariantCulture)
                ),
                targetType
            );

            // If arg == null, return default(targetType), else do the conversion
            return Expression.Condition(
                Expression.Equal(arg, Expression.Constant(null)),
                Expression.Constant(GetDefaultValue(targetType), targetType),
                convertExpr
            );
        }

        return Expression.Convert(arg, targetType);
    }

    private static object GetDefaultValue(Type t)
    {
        if (t.IsValueType)
        {
            return Activator.CreateInstance(t);
        }

        return null;
    }

    public Expression HandleLen(Expression[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException("LEN expects 1 argument.");
        }

        var arg = args[0];

        // If arg is not string, first check for null, then call ToString
        if (arg.Type != typeof(string))
        {
            var nullCheck = Expression.Equal(arg, Expression.Constant(null, arg.Type));
            var toStringCall = Expression.Call(arg, typeof(object).GetMethod("ToString"));
            var lengthProp = typeof(string).GetProperty("Length");

            // If arg is null, return 0, else arg.ToString().Length
            return Expression.Condition(
                nullCheck,
                Expression.Constant(0),
                Expression.Property(toStringCall, lengthProp)
            );
        }
        else
        {
            // arg is string: if null, return 0; else arg.Length
            var nullCheck = Expression.Equal(arg, Expression.Constant(null, typeof(string)));
            var lengthProp = typeof(string).GetProperty("Length");
            return Expression.Condition(
                nullCheck,
                Expression.Constant(0),
                Expression.Property(arg, lengthProp)
            );
        }
    }

    public Expression HandleIsNull(Expression[] args)
    {
        if (args.Length != 2)
        {
            throw new ArgumentException("ISNULL expects 2 arguments.");
        }

        var checkExpr = args[0];
        var replacementExpr = args[1];

        // Convert checkExpr to object if not already (for null check)
        if (checkExpr.Type != typeof(object))
        {
            checkExpr = Expression.Convert(checkExpr, typeof(object));
        }

        // Convert replacementExpr to string if not already
        if (replacementExpr.Type != typeof(string))
        {
            replacementExpr = Expression.Convert(replacementExpr, typeof(string));
        }

        // If checkExpr is a string, convert to string before returning
        Expression resultExpr = Expression.Condition(
            Expression.NotEqual(checkExpr, Expression.Constant(null, typeof(object))),
            Expression.Call(checkExpr, typeof(object).GetMethod("ToString")),
            replacementExpr,
            typeof(string)
        );

        return resultExpr;
    }

    private static Type GetWiderNumericType(Type t1, Type t2)
    {
        // Order: decimal > double > float > ulong > long > uint > int
        var types = new[]
        {
            typeof(decimal), typeof(double), typeof(float), typeof(ulong), typeof(long), typeof(uint), typeof(int)
        };
        foreach (var t in types)
        {
            if (t1 == t || t2 == t)
            {
                return t;
            }
        }

        return t1; // fallback
    }

    public Expression HandleIif(Expression[] args)
    {
        if (args.Length != 3)
        {
            throw new ArgumentException("IIF expects 3 arguments.");
        }

        // Convert condition to bool
        var condition = args[0];
        if (condition.Type != typeof(bool))
        {
            if (IsNumericType(condition.Type))
            {
                // (Convert.ToDouble(condition) != 0)
                var toDouble = typeof(Convert).GetMethod(nameof(Convert.ToDouble), new[] { typeof(object) });
                var conditionObj = Expression.Convert(condition, typeof(object));
                var doubleVal = Expression.Call(toDouble, conditionObj);
                condition = Expression.NotEqual(doubleVal, Expression.Constant(0.0));
            }
            else if (condition.Type == typeof(object))
            {
                // Handle boxed bool and boxed numeric
                var isBool = Expression.TypeIs(condition, typeof(bool));
                var unboxBool = Expression.Convert(condition, typeof(bool));
                var isNumeric = Expression.OrElse(
                    Expression.TypeIs(condition, typeof(byte)),
                    Expression.OrElse(
                        Expression.TypeIs(condition, typeof(sbyte)),
                        Expression.OrElse(
                            Expression.TypeIs(condition, typeof(short)),
                            Expression.OrElse(
                                Expression.TypeIs(condition, typeof(ushort)),
                                Expression.OrElse(
                                    Expression.TypeIs(condition, typeof(int)),
                                    Expression.OrElse(
                                        Expression.TypeIs(condition, typeof(uint)),
                                        Expression.OrElse(
                                            Expression.TypeIs(condition, typeof(long)),
                                            Expression.OrElse(
                                                Expression.TypeIs(condition, typeof(ulong)),
                                                Expression.OrElse(
                                                    Expression.TypeIs(condition, typeof(float)),
                                                    Expression.OrElse(
                                                        Expression.TypeIs(condition, typeof(double)),
                                                        Expression.TypeIs(condition, typeof(decimal))
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        )
                    )
                );

                var toDouble = typeof(Convert).GetMethod(nameof(Convert.ToDouble), new[] { typeof(object) });
                var doubleVal = Expression.Call(toDouble, Expression.Convert(condition, typeof(object)));
                var numericTest = Expression.NotEqual(doubleVal, Expression.Constant(0.0));

                // if bool, use unbox; else if numeric, use numericTest; else throw
                condition = Expression.Condition(
                    isBool,
                    unboxBool,
                    Expression.Condition(
                        isNumeric,
                        numericTest,
                        Expression.Throw(Expression.New(
                                typeof(InvalidOperationException).GetConstructor(new[] { typeof(string) }),
                                Expression.Constant("Condition must be boolean or numeric.")),
                            typeof(bool)
                        )
                    )
                );
            }
            else
            {
                throw new InvalidOperationException("Condition must be boolean or numeric.");
            }
        }

        var truePart = args[1];
        var falsePart = args[2];

        // Coerce types if necessary
        if (truePart.Type != falsePart.Type)
        {
            if (truePart.Type.IsAssignableFrom(falsePart.Type))
            {
                falsePart = Expression.Convert(falsePart, truePart.Type);
            }
            else if (falsePart.Type.IsAssignableFrom(truePart.Type))
            {
                truePart = Expression.Convert(truePart, falsePart.Type);
            }
            else if (IsNumericType(truePart.Type) && IsNumericType(falsePart.Type))
            {
                // Convert both to the wider numeric type
                var targetType = GetWiderNumericType(truePart.Type, falsePart.Type);
                truePart = Expression.Convert(truePart, targetType);
                falsePart = Expression.Convert(falsePart, targetType);
            }
            else
            {
                throw new InvalidOperationException("IIF true and false parts must have compatible types.");
            }
        }

        return Expression.Condition(condition, truePart, falsePart);
    }


    public Expression HandleTrim(Expression[] args)
    {
        if (args.Length != 1)
        {
            throw new ArgumentException("TRIM expects 1 argument.");
        }

        var arg = args[0];
        if (arg.Type != typeof(string))
        {
            arg = Expression.Call(arg, typeof(object).GetMethod("ToString"));
        }

        var trimMethod = typeof(string).GetMethod("Trim", Type.EmptyTypes);
        return Expression.Call(arg, trimMethod);
    }

    public Expression HandleSubstring(Expression[] args)
    {
        if (args.Length != 3)
        {
            throw new ArgumentException("SUBSTRING expects 3 arguments.");
        }

        var strExpr = args[0];
        var startExpr = args[1];
        var lengthExpr = args[2];

        // Ensure strExpr is string
        if (strExpr.Type != typeof(string))
        {
            strExpr = Expression.Condition(
                Expression.Equal(strExpr, Expression.Constant(null, strExpr.Type)),
                Expression.Constant(null, typeof(string)),
                Expression.Call(strExpr, typeof(object).GetMethod("ToString"))
            );
        }

        // Use int? for all nulls and conversions
        var startInt = Expression.Condition(
            Expression.Equal(startExpr, Expression.Constant(null, startExpr.Type)),
            Expression.Constant(null, typeof(int?)),
            Expression.Convert(startExpr, typeof(int?))
        );
        var lengthInt = Expression.Condition(
            Expression.Equal(lengthExpr, Expression.Constant(null, lengthExpr.Type)),
            Expression.Constant(null, typeof(int?)),
            Expression.Convert(lengthExpr, typeof(int?))
        );

        // Adjust for 1-based to 0-based index
        var zeroBasedStart = Expression.Subtract(startInt, Expression.Constant(1, typeof(int?)));

        // Variables for block
        var strVar = Expression.Variable(typeof(string), "str");
        var startVar = Expression.Variable(typeof(int?), "start");
        var lenVar = Expression.Variable(typeof(int?), "len");

        var substringMethod = typeof(string).GetMethod("Substring", new[] { typeof(int), typeof(int) });

        // Use Expression.Coalesce to provide default values for comparisons and method call
        var invalid =
            Expression.OrElse(
                Expression.Equal(strVar, Expression.Constant(null, typeof(string))),
                Expression.OrElse(
                    Expression.LessThan(Expression.Coalesce(startVar, Expression.Constant(0)),
                        Expression.Constant(0)),
                    Expression.OrElse(
                        Expression.LessThan(Expression.Coalesce(lenVar, Expression.Constant(0)),
                            Expression.Constant(0)),
                        Expression.OrElse(
                            Expression.GreaterThan(Expression.Coalesce(startVar, Expression.Constant(0)),
                                Expression.Property(strVar, "Length")),
                            Expression.GreaterThan(
                                Expression.Add(Expression.Coalesce(startVar, Expression.Constant(0)),
                                    Expression.Coalesce(lenVar, Expression.Constant(0))),
                                Expression.Property(strVar, "Length"))
                        )
                    )
                )
            );

        // If invalid, return null; else call Substring
        return Expression.Block(
            new[] { strVar, startVar, lenVar },
            Expression.Assign(strVar, strExpr),
            Expression.Assign(startVar, zeroBasedStart),
            Expression.Assign(lenVar, lengthInt),
            Expression.Condition(
                invalid,
                Expression.Constant(null, typeof(string)),
                Expression.Call(
                    strVar,
                    substringMethod,
                    Expression.Convert(Expression.Coalesce(startVar, Expression.Constant(0)), typeof(int)),
                    Expression.Convert(Expression.Coalesce(lenVar, Expression.Constant(0)), typeof(int))
                )
            )
        );
    }


    public Expression HandleAggregate(string functionName, Expression[] args)
    {
        throw new NotSupportedException(
            $"Aggregate function '{functionName}' is not supported for single-row evaluation, they require iterating over a collection.");
    }

    public override Expression VisitAdditiveExpression(DataTableExpressionParser.AdditiveExpressionContext context)
    {
        var result = Visit(context.multiplicativeExpression(0));

        for (var i = 1; i < context.multiplicativeExpression().Length; i++)
        {
            var right = Visit(context.multiplicativeExpression(i));

            // Get the operator terminal node (a parse tree node, not a token)
            // The operator is at index i*2-1 in the children list
            var opNode = context.GetChild(i * 2 - 1) as ITerminalNode;
            if (opNode == null)
            {
                throw new InvalidOperationException("Expected operator node");
            }

            // Get the token from the terminal node
            var opToken = opNode.Symbol;

            result = ApplyAdditiveOperator(result, right, opToken);
        }

        return result;
    }

    public Expression ApplyAdditiveOperator(Expression left, Expression right, IToken opToken)
    {
        left = ConvertToNumeric(left);
        right = ConvertToNumeric(right);

        if (opToken.Type == DataTableExpressionParser.PLUS)
        {
            return Expression.Add(left, right);
        }

        if (opToken.Type == DataTableExpressionParser.MINUS)
        {
            return Expression.Subtract(left, right);
        }

        throw new NotSupportedException($"Operator '{opToken.Text}' not supported");
    }

    public override Expression VisitMultiplicativeExpression(
        DataTableExpressionParser.MultiplicativeExpressionContext context)
    {
        var result = Visit(context.unaryExpression(0));

        for (var i = 1; i < context.unaryExpression().Length; i++)
        {
            var right = Visit(context.unaryExpression(i));

            // Get the operator terminal node
            var opNode = context.GetChild(i * 2 - 1) as ITerminalNode;
            if (opNode == null)
            {
                throw new InvalidOperationException("Expected operator node");
            }

            // Get the token from the terminal node
            var opToken = opNode.Symbol;

            result = ApplyMultiplicativeOperator(result, right, opToken);
        }

        return result;
    }

    public Expression ApplyMultiplicativeOperator(Expression left, Expression right, IToken opToken)
    {
        left = ConvertToNumeric(left);
        right = ConvertToNumeric(right);

        if (opToken.Type == DataTableExpressionParser.MULTIPLY)
        {
            return Expression.Multiply(left, right);
        }

        if (opToken.Type == DataTableExpressionParser.DIVIDE)
        {
            return Expression.Divide(left, right);
        }

        if (opToken.Type == DataTableExpressionParser.MODULO)
        {
            return Expression.Modulo(left, right);
        }

        throw new NotSupportedException($"Operator '{opToken.Text}' not supported");
    }

    public override Expression VisitUnaryExpression(
        [NotNull] DataTableExpressionParser.UnaryExpressionContext context)
    {
        var expr = Visit(context.primaryExpression());
        if (context.PLUS() != null)
        {
            return Expression.UnaryPlus(expr);
        }

        if (context.MINUS() != null)
        {
            return Expression.Negate(expr);
        }

        return expr;
    }

    public override Expression VisitPrimaryExpression(
        [NotNull] DataTableExpressionParser.PrimaryExpressionContext context)
    {
        if (context.LPAREN() != null)
        {
            return Visit(context.expression());
        }

        if (context.functionCall() != null)
        {
            return Visit(context.functionCall());
        }

        if (context.columnReference() != null)
        {
            return Visit(context.columnReference());
        }

        if (context.literal() != null)
        {
            return Visit(context.literal());
        }

        throw new InvalidOperationException("Invalid primary expression");
    }

    public override Expression VisitLiteral(DataTableExpressionParser.LiteralContext context)
    {
        if (context.STRING_LITERAL() != null)
        {
            var value = context.STRING_LITERAL().GetText();
            value = value.Substring(1, value.Length - 2);
            return Expression.Constant(value, typeof(object));
        }

        if (context.INTEGER_LITERAL() != null)
        {
            var numberText = context.INTEGER_LITERAL().GetText();
            return Expression.Constant(int.Parse(numberText, CultureInfo.InvariantCulture), typeof(object));
        }

        if (context.DECIMAL_LITERAL() != null)
        {
            var numberText = context.DECIMAL_LITERAL().GetText();
            return Expression.Constant(decimal.Parse(numberText, CultureInfo.InvariantCulture), typeof(object));
        }

        if (context.BOOLEAN_LITERAL() != null)
        {
            var value = bool.Parse(context.BOOLEAN_LITERAL().GetText());
            return Expression.Constant(value, typeof(object));
        }

        if (context.DATE_LITERAL() != null)
        {
            var dateText = context.DATE_LITERAL().GetText();
            dateText = dateText.Substring(1, dateText.Length - 2);
            var dateValue = DateTime.Parse(dateText, CultureInfo.InvariantCulture);
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
        var columnName = ExtractColumnName(context);
        var keyExpression = Expression.Constant(columnName);
        // Use the static helper instead of Expression.MakeIndex
        return Expression.Call(GetColumnValueMethod, Parameter, keyExpression);
    }

    private static object GetColumnValue(Dictionary<string, object> row, string columnName)
    {
        if (!row.ContainsKey(columnName))
        {
            throw new InvalidOperationException($"Invalid column name: {columnName}");
        }

        return row[columnName];
    }

    // Helper methods
    public static string GetFullTextIncludingSpaces(ParserRuleContext context)
    {
        var inputStream = context.Start.InputStream;
        var startIndex = context.Start.StartIndex;
        var stopIndex = context.Stop.StopIndex;
        return inputStream.GetText(new Interval(startIndex, stopIndex));
    }

    private string ExtractColumnName(DataTableExpressionParser.ColumnReferenceContext context)
    {
        var rawName = GetFullTextIncludingSpaces(context);

        if (rawName.StartsWith("[") && rawName.EndsWith("]"))
        {
            rawName = rawName.Substring(1, rawName.Length - 2);
            // Unescape per DataTable rules
            rawName = rawName.Replace(@"\\", @"\").Replace(@"\]", "]");
        }
        else if (rawName.StartsWith("`") && rawName.EndsWith("`"))
        {
            rawName = rawName.Substring(1, rawName.Length - 2);
        }

        return rawName;
    }

    private Expression CreateLikeExpression(Expression left, Expression right)
    {
        // Convert both to strings, but if null, pass null (do not call .ToString() on null)
        Expression leftString = Expression.Condition(
            Expression.Equal(left, Expression.Constant(null, left.Type)),
            Expression.Constant(null, typeof(string)),
            Expression.Call(left, typeof(object).GetMethod("ToString"))
        );
        Expression rightString = Expression.Condition(
            Expression.Equal(right, Expression.Constant(null, right.Type)),
            Expression.Constant(null, typeof(string)),
            Expression.Call(right, typeof(object).GetMethod("ToString"))
        );

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
            {
                values.Add(constant.Value);
            }
            else
            {
                throw new NotSupportedException("IN operator values must be constant expressions.");
            }
        }

        return Expression.Constant(values, typeof(List<object>));
    }

    public Expression CreateInExpression(Expression left, Expression right)
    {
        if (!(right is ConstantExpression constantRight) || !(constantRight.Value is IEnumerable<object> values))
        {
            throw new InvalidOperationException("IN operator requires a list of values on the right.");
        }

        if (left.Type != typeof(object))
        {
            left = Expression.Convert(left, typeof(object));
        }

        var containsMethod = typeof(Enumerable).GetMethods()
            .First(m => m.Name == "Contains" && m.GetParameters().Length == 2)
            .MakeGenericMethod(typeof(object));

        return Expression.Call(containsMethod, right, left);
    }
}