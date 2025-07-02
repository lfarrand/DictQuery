using System.Linq.Expressions;
using Antlr4.Runtime;
using Xunit;

namespace AntlrParser8.Tests;

public class ExpressionTreeVisitorApplyMultiplicativeOperatorTests
{
    private ExpressionTreeVisitor CreateVisitor()
    {
        var data = new[] { new Dictionary<string, object>() };
        var parameter = Expression.Parameter(typeof(Dictionary<string, object>), "row");
        return new ExpressionTreeVisitor(parameter, data);
    }

    private class MockToken : IToken
    {
        public int Type { get; set; }
        public int Channel => throw new NotImplementedException();
        public int StartIndex => throw new NotImplementedException();
        public int StopIndex => throw new NotImplementedException();
        public int TokenIndex => throw new NotImplementedException();
        public int Line => throw new NotImplementedException();
        public int Column => throw new NotImplementedException();
        public string Text => "MockToken";
        public ITokenSource TokenSource => throw new NotImplementedException();
        public ICharStream InputStream => throw new NotImplementedException();
    }

    [Fact]
    public void ApplyMultiplicativeOperator_Multiply()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(3.0, typeof(double));
        var right = Expression.Constant(4.0, typeof(double));
        var token = new MockToken { Type = DataTableExpressionParser.MULTIPLY };
        var expr = visitor.ApplyMultiplicativeOperator(left, right, token);
        var lambda = Expression.Lambda<Func<double>>(expr).Compile();
        Assert.Equal(12.0, lambda());
    }

    [Fact]
    public void ApplyMultiplicativeOperator_Divide()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(12.0, typeof(double));
        var right = Expression.Constant(3.0, typeof(double));
        var token = new MockToken { Type = DataTableExpressionParser.DIVIDE };
        var expr = visitor.ApplyMultiplicativeOperator(left, right, token);
        var lambda = Expression.Lambda<Func<double>>(expr).Compile();
        Assert.Equal(4.0, lambda());
    }

    [Fact]
    public void ApplyMultiplicativeOperator_Modulo()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(10.0, typeof(double));
        var right = Expression.Constant(3.0, typeof(double));
        var token = new MockToken { Type = DataTableExpressionParser.MODULO };
        var expr = visitor.ApplyMultiplicativeOperator(left, right, token);
        var lambda = Expression.Lambda<Func<double>>(expr).Compile();
        Assert.Equal(1.0, lambda());
    }

    [Fact]
    public void ApplyMultiplicativeOperator_Unsupported_Throws()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(1.0, typeof(double));
        var right = Expression.Constant(1.0, typeof(double));
        var token = new MockToken { Type = 9999 }; // Not MULTIPLY/DIVIDE/MODULO
        Assert.Throws<NotSupportedException>(() => visitor.ApplyMultiplicativeOperator(left, right, token));
    }
}