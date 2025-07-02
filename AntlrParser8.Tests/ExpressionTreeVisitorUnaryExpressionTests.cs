using System.Linq.Expressions;
using Antlr4.Runtime.Tree;
using Xunit;

namespace AntlrParser8.Tests;

public class ExpressionTreeVisitorUnaryExpressionTests
{
    private ExpressionTreeVisitor CreateVisitor()
    {
        var data = new[] { new Dictionary<string, object>() };
        var parameter = Expression.Parameter(typeof(Dictionary<string, object>), "row");
        return new ExpressionTreeVisitor(parameter, data);
    }

    // Minimal mock context for unary expression
    private class MockUnaryExpressionContext : DataTableExpressionParser.UnaryExpressionContext
    {
        public bool HasPlus { get; set; }
        public bool HasMinus { get; set; }
        public DataTableExpressionParser.PrimaryExpressionContext Primary { get; set; }

        public MockUnaryExpressionContext(bool plus, bool minus, object value)
            : base(null, 0)
        {
            HasPlus = plus;
            HasMinus = minus;
            Primary = new MockPrimaryExpressionContext(value);
        }

        public override ITerminalNode PLUS()
        {
            return HasPlus ? new MockTerminalNode() : null;
        }

        public override ITerminalNode MINUS()
        {
            return HasMinus ? new MockTerminalNode() : null;
        }

        public override DataTableExpressionParser.PrimaryExpressionContext primaryExpression()
        {
            return Primary;
        }
    }

    private class MockPrimaryExpressionContext : DataTableExpressionParser.PrimaryExpressionContext
    {
        private readonly object _value;

        public MockPrimaryExpressionContext(object value) : base(null, 0)
        {
            _value = value;
        }

        public override bool Equals(object obj)
        {
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        public override string ToString()
        {
            return _value?.ToString();
        }
    }

    // You may need to override VisitPrimaryExpression to return a constant expression for the mock
    private class TestExpressionTreeVisitor : ExpressionTreeVisitor
    {
        public TestExpressionTreeVisitor(ParameterExpression parameter,
            IEnumerable<Dictionary<string, object>> data)
            : base(parameter, data)
        {
        }

        public override Expression VisitPrimaryExpression(
            DataTableExpressionParser.PrimaryExpressionContext context)
        {
            if (context is MockPrimaryExpressionContext mock)
            {
                return Expression.Constant(
                    mock.ToString() == null ? (object)null : Convert.ToInt32(mock.ToString()), typeof(int));
            }

            throw new InvalidOperationException("Unhandled context");
        }
    }

    [Fact]
    public void VisitUnaryExpression_UnaryPlus()
    {
        var visitor = new TestExpressionTreeVisitor(
            Expression.Parameter(typeof(Dictionary<string, object>), "row"),
            new[] { new Dictionary<string, object>() });
        var context = new MockUnaryExpressionContext(true, false, 42);
        var expr = visitor.VisitUnaryExpression(context);
        var lambda = Expression.Lambda<Func<int>>(expr).Compile();
        Assert.Equal(+42, lambda());
    }

    [Fact]
    public void VisitUnaryExpression_UnaryMinus()
    {
        var visitor = new TestExpressionTreeVisitor(
            Expression.Parameter(typeof(Dictionary<string, object>), "row"),
            new[] { new Dictionary<string, object>() });
        var context = new MockUnaryExpressionContext(false, true, 42);
        var expr = visitor.VisitUnaryExpression(context);
        var lambda = Expression.Lambda<Func<int>>(expr).Compile();
        Assert.Equal(-42, lambda());
    }

    [Fact]
    public void VisitUnaryExpression_NoOperator()
    {
        var visitor = new TestExpressionTreeVisitor(
            Expression.Parameter(typeof(Dictionary<string, object>), "row"),
            new[] { new Dictionary<string, object>() });
        var context = new MockUnaryExpressionContext(false, false, 7);
        var expr = visitor.VisitUnaryExpression(context);
        var lambda = Expression.Lambda<Func<int>>(expr).Compile();
        Assert.Equal(7, lambda());
    }

    [Fact]
    public void VisitUnaryExpression_UnaryMinus_NegativeNumber()
    {
        var visitor = new TestExpressionTreeVisitor(
            Expression.Parameter(typeof(Dictionary<string, object>), "row"),
            new[] { new Dictionary<string, object>() });
        var context = new MockUnaryExpressionContext(false, true, -8);
        var expr = visitor.VisitUnaryExpression(context);
        var lambda = Expression.Lambda<Func<int>>(expr).Compile();
        Assert.Equal(8, lambda());
    }
}