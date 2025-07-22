using System.Linq.Expressions;
using Antlr4.Runtime.Tree;
using Xunit;

namespace AntlrParser8.Tests;

public class ExpressionTreeVisitorComparisonCallTests
{
    private ExpressionTreeVisitor CreateVisitor()
    {
        var data = new List<IDictionary<string, object>>
        {
            new Dictionary<string, object>() { ["Num"] = 5, ["Str"] = "foo", ["Bool"] = true, ["Date"] = new DateTime(2020, 1, 1) }
        };
        var parameter = Expression.Parameter(typeof(IDictionary<string, object>), "row");
        return new ExpressionTreeVisitor(parameter, data);
    }

    // Minimal mock context for each operator
    private class MockComparisonContext : ModelExpressionParser.ComparisonExpressionContext
    {
        private readonly string _op;

        public MockComparisonContext(string op) : base(null, 0)
        {
            _op = op;
        }

        public override ITerminalNode EQUALS()
        {
            return _op == "EQUALS" ? new MockTerminalNode() : null;
        }

        public override ITerminalNode NOT_EQUALS()
        {
            return _op == "NOT_EQUALS" ? new MockTerminalNode() : null;
        }

        public override ITerminalNode LESS_THAN()
        {
            return _op == "LESS_THAN" ? new MockTerminalNode() : null;
        }

        public override ITerminalNode GREATER_THAN()
        {
            return _op == "GREATER_THAN" ? new MockTerminalNode() : null;
        }

        public override ITerminalNode LESS_THAN_OR_EQUAL()
        {
            return _op == "LESS_THAN_OR_EQUAL" ? new MockTerminalNode() : null;
        }

        public override ITerminalNode GREATER_THAN_OR_EQUAL()
        {
            return _op == "GREATER_THAN_OR_EQUAL" ? new MockTerminalNode() : null;
        }

        public override string GetText()
        {
            return _op;
        }
    }

    [Fact]
    public void ColumnEqualsConstant_Numeric()
    {
        var visitor = CreateVisitor();
        // Simulate: Num = 5
        var left = Expression.Call(
            typeof(ExpressionTreeVisitor).GetMethod("GetColumnValue",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic),
            visitor.Parameter,
            Expression.Constant("Num")
        );
        var right = Expression.Constant(5, typeof(int));
        var context = new MockComparisonContext("EQUALS");
        var expr = visitor.CreateComparisonCall(context, left, right);
        var lambda = Expression.Lambda<Func<IDictionary<string, object>, bool>>(expr, visitor.Parameter).Compile();
        Assert.True(lambda(new Dictionary<string, object> { ["Num"] = 5 }));
        Assert.False(lambda(new Dictionary<string, object> { ["Num"] = 6 }));
    }

    [Fact]
    public void ConstantEqualsColumn_Numeric()
    {
        var visitor = CreateVisitor();
        // Simulate: 5 = Num
        var left = Expression.Constant(5, typeof(int));
        var right = Expression.Call(
            typeof(ExpressionTreeVisitor).GetMethod("GetColumnValue",
                System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic),
            visitor.Parameter,
            Expression.Constant("Num")
        );
        var context = new MockComparisonContext("EQUALS");
        var expr = visitor.CreateComparisonCall(context, left, right);
        var lambda = Expression.Lambda<Func<IDictionary<string, object>, bool>>(expr, visitor.Parameter).Compile();
        Assert.True(lambda(new Dictionary<string, object> { ["Num"] = 5 }));
        Assert.False(lambda(new Dictionary<string, object> { ["Num"] = 7 }));
    }

    [Fact]
    public void BothConverted_StringEquality()
    {
        var visitor = CreateVisitor();
        // Simulate: ConvertToBestType("foo", typeof(string)) == ConvertToBestType("FOO", typeof(string))
        var convertMethod = typeof(NumericConverter).GetMethod("ConvertToBestType");
        var left = Expression.Call(convertMethod, Expression.Constant("foo", typeof(object)),
            Expression.Constant(typeof(string)));
        var right = Expression.Call(convertMethod, Expression.Constant("FOO", typeof(object)),
            Expression.Constant(typeof(string)));
        var context = new MockComparisonContext("EQUALS");
        var expr = visitor.CreateComparisonCall(context, left, right);
        var lambda = Expression.Lambda<Func<bool>>(expr).Compile();
        Assert.True(lambda());
    }

    [Fact]
    public void BothConverted_StringRelational_Throws()
    {
        var visitor = CreateVisitor();
        // Simulate: ConvertToBestType("foo", typeof(string)) < ConvertToBestType("bar", typeof(string))
        var convertMethod = typeof(NumericConverter).GetMethod("ConvertToBestType");
        var left = Expression.Call(convertMethod, Expression.Constant("foo", typeof(object)),
            Expression.Constant(typeof(string)));
        var right = Expression.Call(convertMethod, Expression.Constant("bar", typeof(object)),
            Expression.Constant(typeof(string)));
        var context = new MockComparisonContext("LESS_THAN");
        Assert.Throws<InvalidOperationException>(() => visitor.CreateComparisonCall(context, left, right));
    }

    [Fact]
    public void BothConverted_BoolRelational_Throws()
    {
        var visitor = CreateVisitor();
        // Simulate: ConvertToBestType(true, typeof(bool)) < ConvertToBestType(false, typeof(bool))
        var convertMethod = typeof(NumericConverter).GetMethod("ConvertToBestType");
        var left = Expression.Call(convertMethod, Expression.Constant(true, typeof(object)),
            Expression.Constant(typeof(bool)));
        var right = Expression.Call(convertMethod, Expression.Constant(false, typeof(object)),
            Expression.Constant(typeof(bool)));
        var context = new MockComparisonContext("LESS_THAN");
        Assert.Throws<InvalidOperationException>(() => visitor.CreateComparisonCall(context, left, right));
    }

    [Fact]
    public void RelationalOnBoolConstants_Throws()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(true, typeof(bool));
        var right = Expression.Constant(false, typeof(bool));
        var context = new MockComparisonContext("LESS_THAN");
        Assert.Throws<InvalidOperationException>(() => visitor.CreateComparisonCall(context, left, right));
    }

    [Fact]
    public void FallbackToTypeConverter_WorksForNotEquals()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant("foo", typeof(object));
        var right = Expression.Constant("bar", typeof(object));
        var context = new MockComparisonContext("NOT_EQUALS");
        var expr = visitor.CreateComparisonCall(context, left, right);
        var lambda = Expression.Lambda<Func<bool>>(expr).Compile();
        Assert.True(lambda());
    }

    [Fact]
    public void NotSupportedOperator_Throws()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(1, typeof(object));
        var right = Expression.Constant(2, typeof(object));
        // Use a context that returns null for all operator methods
        var context = new MockComparisonContext("UNKNOWN");
        Assert.Throws<NotSupportedException>(() => visitor.CreateComparisonCall(context, left, right));
    }
}