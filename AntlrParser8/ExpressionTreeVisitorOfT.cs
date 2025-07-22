using System.Globalization;
using System.Linq.Expressions;
using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

namespace AntlrParser8;

public class ExpressionTreeVisitor<T> : ModelExpressionBaseVisitor<Expression>
{
    public ParameterExpression Parameter { get; }

    public ExpressionTreeVisitor(ParameterExpression parameter)
    {
        Parameter = parameter;
    }

    // --- Property Access Helper ---
    public Expression BuildPropertyAccess(ParameterExpression param, string propertyName)
    {
        var type = typeof(T);
        if (typeof(IDictionary<string, object>).IsAssignableFrom(type))
        {
            var indexer = type.GetProperty("Item");
            return Expression.Property(param, indexer, Expression.Constant(propertyName));
        }

        return Expression.PropertyOrField(param, propertyName);
    }

    // --- Boolean Conversion Helper ---
    public Expression EnsureBoolean(Expression expr)
    {
        if (expr.Type == typeof(bool))
        {
            return expr;
        }

        if (expr is ConstantExpression ce && ce.Type == typeof(object) && ce.Value is bool b)
        {
            return Expression.Constant(b, typeof(bool));
        }

        var numericTypes = new[]
        {
            typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal)
        };

        if (numericTypes.Contains(expr.Type))
        {
            var toDouble = typeof(Convert).GetMethod(nameof(Convert.ToDouble), new[] { typeof(object) });
            var asObj = Expression.Convert(expr, typeof(object));
            var doubleVal = Expression.Call(toDouble, asObj);
            return Expression.NotEqual(doubleVal, Expression.Constant(0.0));
        }

        if (expr.Type == typeof(object))
        {
            if (expr is ConstantExpression ceObj && ceObj.Value is bool bObj)
            {
                return Expression.Constant(bObj, typeof(bool));
            }

            if (expr is ConstantExpression ceNum && ceNum.Value != null &&
                numericTypes.Contains(ceNum.Value.GetType()))
            {
                var toDouble = typeof(Convert).GetMethod(nameof(Convert.ToDouble), new[] { typeof(object) });
                var doubleVal = Expression.Call(toDouble, expr);
                return Expression.NotEqual(doubleVal, Expression.Constant(0.0));
            }

            throw new InvalidOperationException(
                $"Cannot convert object of type '{(expr as ConstantExpression)?.Value?.GetType().Name ?? "unknown"}' to bool.");
        }

        throw new InvalidOperationException($"Cannot convert type '{expr.Type}' to bool.");
    }
    
    // Entry point for expressions
    public override Expression VisitExpression([NotNull] ModelExpressionParser.ExpressionContext context)
    {
        return Visit(context.orExpression());
    }

    // --- Logical Operators ---
    public override Expression VisitOrExpression([NotNull] ModelExpressionParser.OrExpressionContext context)
    {
        var left = Visit(context.andExpression(0));
        for (var i = 1; i < context.andExpression().Length; i++)
        {
            var right = Visit(context.andExpression(i));
            left = Expression.OrElse(EnsureBoolean(left), EnsureBoolean(right));
        }

        return left;
    }
    
    public override Expression VisitAndExpression([NotNull] ModelExpressionParser.AndExpressionContext context)
    {
        var left = Visit(context.notExpression(0));
        for (var i = 1; i < context.notExpression().Length; i++)
        {
            var right = Visit(context.notExpression(i));
            left = Expression.AndAlso(EnsureBoolean(left), EnsureBoolean(right));
        }

        // Handle boxed bool constants
        if (left is ConstantExpression ce && ce.Type == typeof(object) && ce.Value is bool b)
        {
            return Expression.Constant(b, typeof(bool));
        }

        return left;
    }
    
    public override Expression VisitNotExpression(ModelExpressionParser.NotExpressionContext context)
    {
        var operand = Visit(context.comparisonExpression());

        if (context.NOT() != null)
        {
            // Ensure operand is boolean
            if (operand.Type != typeof(bool))
            {
                operand = Expression.Convert(operand, typeof(bool));
            }

            return Expression.Not(operand);
        }

        return operand;
    }
    
    public override Expression VisitFunctionCall(ModelExpressionParser.FunctionCallContext context)
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

    // --- Comparison, IN, NOT IN, IS NULL, IS NOT NULL ---
    public override Expression VisitComparisonExpression(ModelExpressionParser.ComparisonExpressionContext context)
    {
        var left = Visit(context.additiveExpression(0));

        // IN and NOT IN
        if (context.IN() != null)
        {
            var right = Visit(context.inExpression());
            var inExpr = CreateInExpression(left, right);
            if (context.NOT() != null)
            {
                return Expression.Not(EnsureBoolean(inExpr));
            }
            else
            {
                return EnsureBoolean(inExpr);
            }
        }

        // IS NULL / IS NOT NULL
        if (context.IS() != null)
        {
            var isNotNull = context.NOT() != null;
            return isNotNull
                ? Expression.NotEqual(left, Expression.Constant(null, typeof(object)))
                : Expression.Equal(left, Expression.Constant(null, typeof(object)));
        }

        // Standard comparisons
        if (context.additiveExpression().Length > 1)
        {
            var right = Visit(context.additiveExpression(1));
            var op = GetComparisonOperator(context);

            // Promote both sides to the widest numeric type if both are numeric
            var leftType = left.Type;
            var rightType = right.Type;

            // Only promote if both are numeric and not equal
            if (leftType != rightType &&
                IsNumericType(leftType) && IsNumericType(rightType))
            {
                var targetType = GetWiderNumericType(leftType, rightType);
                left = Expression.Convert(left, targetType);
                right = Expression.Convert(right, targetType);
            }
            
            if (context.LIKE() != null)
            {
                var likeExpr = CreateLikeExpression(left, right);
                return context.NOT() != null ? Expression.Not(EnsureBoolean(likeExpr)) : likeExpr;
            }

            switch (op)
            {
                case "=":
                case "==":
                    return Expression.Equal(left, right);
                case "<>":
                case "!=":
                    return Expression.NotEqual(left, right);
                case "<":
                    return Expression.LessThan(left, right);
                case "<=":
                    return Expression.LessThanOrEqual(left, right);
                case ">":
                    return Expression.GreaterThan(left, right);
                case ">=":
                    return Expression.GreaterThanOrEqual(left, right);
                default:
                    throw new NotSupportedException($"Operator '{op}' not supported.");
            }
        }

        return left;
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

    private string GetComparisonOperator(ModelExpressionParser.ComparisonExpressionContext context)
    {
        if (context.EQUALS() != null)
        {
            return context.EQUALS().GetText();
        }

        if (context.NOT_EQUALS() != null)
        {
            return context.NOT_EQUALS().GetText();
        }

        if (context.LESS_THAN() != null)
        {
            return context.LESS_THAN().GetText();
        }

        if (context.LESS_THAN_OR_EQUAL() != null)
        {
            return context.LESS_THAN_OR_EQUAL().GetText();
        }

        if (context.GREATER_THAN() != null)
        {
            return context.GREATER_THAN().GetText();
        }

        if (context.GREATER_THAN_OR_EQUAL() != null)
        {
            return context.GREATER_THAN_OR_EQUAL().GetText();
        }

        return null;
    }

    // --- IN List ---
    public override Expression VisitInList(ModelExpressionParser.InListContext context)
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

    // --- Arithmetic ---
    public override Expression VisitAdditiveExpression(ModelExpressionParser.AdditiveExpressionContext context)
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
        Type targetType;
        if (left.Type == typeof(decimal) || right.Type == typeof(decimal))
            targetType = typeof(decimal);
        else
            targetType = typeof(double); // fallback to double
        
        left = ConvertToNumeric(left, targetType);
        right = ConvertToNumeric(right, targetType);
        
        if (opToken.Type == ModelExpressionParser.PLUS)
        {
            return Expression.Add(left, right);
        }

        if (opToken.Type == ModelExpressionParser.MINUS)
        {
            return Expression.Subtract(left, right);
        }

        throw new NotSupportedException($"Operator '{opToken.Text}' not supported");
    }

    public override Expression VisitMultiplicativeExpression(
        ModelExpressionParser.MultiplicativeExpressionContext context)
    {
        var result = Visit(context.unaryExpression(0));
        for (var i = 1; i < context.unaryExpression().Length; i++)
        {
            var right = Visit(context.unaryExpression(i));
            var opNode = context.GetChild(i * 2 - 1) as ITerminalNode;
            if (opNode == null)
            {
                throw new InvalidOperationException("Expected operator node");
            }

            var opToken = opNode.Symbol;
            result = ApplyMultiplicativeOperator(result, right, opToken);
        }

        return result;
    }

    public Expression ApplyMultiplicativeOperator(Expression left, Expression right, IToken opToken)
    {
        Type targetType;
        if (left.Type == typeof(decimal) || right.Type == typeof(decimal))
            targetType = typeof(decimal);
        else
            targetType = typeof(double); // fallback to double

        left = ConvertToNumeric(left, targetType);
        right = ConvertToNumeric(right, targetType);

        return opToken.Type switch
        {
            ModelExpressionParser.MULTIPLY => Expression.Multiply(left, right),
            ModelExpressionParser.DIVIDE => Expression.Divide(left, right),
            ModelExpressionParser.MODULO => Expression.Modulo(left, right),
            _ => throw new NotSupportedException($"Operator '{opToken.Text}' not supported")
        };
    }

    public override Expression VisitUnaryExpression(ModelExpressionParser.UnaryExpressionContext context)
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

    // --- Primary Expressions ---
    public override Expression VisitPrimaryExpression(ModelExpressionParser.PrimaryExpressionContext context)
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

    public override Expression VisitColumnReference(ModelExpressionParser.ColumnReferenceContext context)
    {
        var columnName = context.GetText().Trim('[', ']', '`');
        return BuildPropertyAccess(Parameter, columnName);
    }

    public override Expression VisitLiteral(ModelExpressionParser.LiteralContext context)
    {
        if (context.STRING_LITERAL() != null)
        {
            var value = context.STRING_LITERAL().GetText();
            value = value.Substring(1, value.Length - 2);
            return Expression.Constant(value, typeof(string));
        }

        if (context.INTEGER_LITERAL() != null)
        {
            var numberText = context.INTEGER_LITERAL().GetText();
            return Expression.Constant(int.Parse(numberText, CultureInfo.InvariantCulture), typeof(int));
        }

        if (context.DECIMAL_LITERAL() != null)
        {
            var numberText = context.DECIMAL_LITERAL().GetText();
            return Expression.Constant(decimal.Parse(numberText, CultureInfo.InvariantCulture), typeof(decimal));
        }

        if (context.BOOLEAN_LITERAL() != null)
        {
            var value = bool.Parse(context.BOOLEAN_LITERAL().GetText());
            return Expression.Constant(value, typeof(bool));
        }

        if (context.DATE_LITERAL() != null)
        {
            var dateText = context.DATE_LITERAL().GetText();
            dateText = dateText.Substring(1, dateText.Length - 2);
            var dateValue = DateTime.Parse(dateText, CultureInfo.InvariantCulture);
            return Expression.Constant(dateValue, typeof(DateTime));
        }

        if (context.NULL_LITERAL() != null)
        {
            return Expression.Constant(null, typeof(object));
        }

        throw new InvalidOperationException("Invalid literal");
    }

    public Type GetWiderNumericType(Type a, Type b)
    {
        // Order from widest to narrowest
        var numericTypes = new[]
        {
            typeof(decimal), typeof(double), typeof(float),
            typeof(ulong), typeof(long), typeof(uint), typeof(int),
            typeof(ushort), typeof(short), typeof(byte), typeof(sbyte)
        };
        var aIndex = Array.IndexOf(numericTypes, a);
        var bIndex = Array.IndexOf(numericTypes, b);
        if (aIndex == -1 || bIndex == -1)
        {
            throw new InvalidOperationException($"Non-numeric types: {a}, {b}");
        }

        return aIndex < bIndex ? numericTypes[aIndex] : numericTypes[bIndex];
    }

    // --- Numeric Conversion Helper ---
    public Expression ConvertToNumeric(Expression expr, Type targetType = null)
    {
        targetType ??= expr.Type;

        // If already decimal, return as-is
        if (targetType == typeof(decimal))
        {
            if (expr.Type == typeof(decimal))
                return expr;
            return Expression.Convert(expr, typeof(decimal));
        }

        // If target is double, convert numeric types to double
        if (targetType == typeof(double))
        {
            if (expr.Type == typeof(double))
                return expr;
            return Expression.Convert(expr, typeof(double));
        }

        // For other integral types, convert to double (fallback)
        var numericTypes = new HashSet<Type>
        {
            typeof(byte), typeof(sbyte), typeof(short), typeof(ushort),
            typeof(int), typeof(uint), typeof(long), typeof(ulong),
            typeof(float), typeof(double), typeof(decimal)
        };

        if (numericTypes.Contains(expr.Type))
        {
            // Convert expression to targetType if numeric and different types
            if (expr.Type != targetType)
                return Expression.Convert(expr, targetType);
            return expr;
        }

        throw new InvalidOperationException($"Cannot convert type {expr.Type} to numeric type {targetType}");
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

        var likeMethod = typeof(ModelLikeOperator).GetMethod("Like");
        return Expression.Call(likeMethod, leftString, rightString);
    }
}