using System.Linq.Expressions;
using Xunit;

namespace AntlrParser8.Tests;

public class ExpressionTreeVisitorVisitFunctionCallTests
{
    private ExpressionTreeVisitor CreateVisitor()
    {
        var data = new[] { new Dictionary<string, object>() };
        var parameter = Expression.Parameter(typeof(Dictionary<string, object>), "row");
        return new ExpressionTreeVisitor(parameter, data);
    }

    // Minimal mock context for function call
    private class MockFunctionCallContext : DataTableExpressionParser.FunctionCallContext
    {
        private readonly string _fnName;
        private readonly Expression[] _args;

        public MockFunctionCallContext(string fnName, params Expression[] args) : base(null, 0)
        {
            _fnName = fnName;
            _args = args;
        }

        public override DataTableExpressionParser.FunctionNameContext functionName()
        {
            return new MockFunctionNameContext(_fnName);
        }

        public override DataTableExpressionParser.ArgumentListContext argumentList()
        {
            return _args.Length == 0 ? null : new MockArgumentListContext(_args);
        }
    }

    private class MockFunctionNameContext : DataTableExpressionParser.FunctionNameContext
    {
        private readonly string _name;

        public MockFunctionNameContext(string name) : base(null, 0)
        {
            _name = name;
        }

        public override string GetText()
        {
            return _name;
        }
    }

    private class MockArgumentListContext : DataTableExpressionParser.ArgumentListContext
    {
        private readonly Expression[] _args;

        public MockArgumentListContext(Expression[] args) : base(null, 0)
        {
            _args = args;
        }

        public override DataTableExpressionParser.ExpressionContext[] expression()
        {
            return _args.Select(a => new MockExpressionContext(a))
                .ToArray<DataTableExpressionParser.ExpressionContext>();
        }
    }

    private class MockExpressionContext : DataTableExpressionParser.ExpressionContext
    {
        public readonly Expression _expr;

        public MockExpressionContext(Expression expr) : base(null, 0)
        {
            _expr = expr;
        }

        public override bool Equals(object obj)
        {
            return false;
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    // Override Visit to return the expression directly for mocks
    private class TestExpressionTreeVisitor : ExpressionTreeVisitor
    {
        public TestExpressionTreeVisitor(ParameterExpression parameter,
            IEnumerable<Dictionary<string, object>> data)
            : base(parameter, data)
        {
        }

        public Expression Visit(DataTableExpressionParser.ExpressionContext context)
        {
            if (context is MockExpressionContext mock)
            {
                return mock._expr;
            }

            return base.Visit(context);
        }
    }

    [Fact]
    public void VisitFunctionCall_NoArguments()
    {
        var visitor = CreateVisitor();
        var context = new MockFunctionCallContext("LEN");
        // Should throw in HandleLen for wrong arg count
        Assert.Throws<ArgumentException>(() => visitor.VisitFunctionCall(context));
    }
}