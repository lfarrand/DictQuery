using System.Linq.Expressions;
using Xunit;

namespace AntlrParser8.Tests;

public class ExpressionTreeVisitorHandleIifTests
{
    private ExpressionTreeVisitor CreateVisitor()
    {
        var data = new[] { new Dictionary<string, object>() };
        var parameter = Expression.Parameter(typeof(Dictionary<string, object>), "row");
        return new ExpressionTreeVisitor(parameter, data);
    }

    [Fact]
    public void HandleIif_ThrowsOnWrongArgCount()
    {
        var visitor = CreateVisitor();
        Assert.Throws<ArgumentException>(() => visitor.HandleIif(new Expression[2]));
        Assert.Throws<ArgumentException>(() => visitor.HandleIif(new Expression[4]));
    }

    [Fact]
    public void HandleIif_ThrowsOnIncompatibleTypes()
    {
        var visitor = CreateVisitor();
        var cond = Expression.Constant(true, typeof(bool));
        var left = Expression.Constant(1, typeof(int));
        var right = Expression.Constant("not an int", typeof(string));
        Assert.Throws<InvalidOperationException>(() => visitor.HandleIif(new[] { cond, left, right }));
    }

    [Fact]
    public void HandleIif_CoercesTypes_TruePartAssignableFromFalsePart()
    {
        var visitor = CreateVisitor();
        var cond = Expression.Constant(true, typeof(bool));
        var left = Expression.Constant(1.0, typeof(double));
        var right = Expression.Constant(2, typeof(int)); // int can be converted to double
        var expr = visitor.HandleIif(new[] { cond, left, right });
        var lambda = Expression.Lambda<Func<double>>(expr).Compile();
        Assert.Equal(1.0, lambda());
    }

    [Fact]
    public void HandleIif_CoercesTypes_FalsePartAssignableFromTruePart()
    {
        var visitor = CreateVisitor();
        var cond = Expression.Constant(false, typeof(bool));
        var left = Expression.Constant(1, typeof(int));
        var right = Expression.Constant(2.0, typeof(double)); // int can be converted to double
        var expr = visitor.HandleIif(new[] { cond, left, right });
        var lambda = Expression.Lambda<Func<double>>(expr).Compile();
        Assert.Equal(2.0, lambda());
    }

    [Fact]
    public void HandleIif_ConditionNotBool_IsConverted()
    {
        var visitor = CreateVisitor();
        var cond = Expression.Constant(1, typeof(int)); // will be converted to bool (true)
        var left = Expression.Constant("yes", typeof(string));
        var right = Expression.Constant("no", typeof(string));
        var expr = visitor.HandleIif(new[] { cond, left, right });
        var lambda = Expression.Lambda<Func<string>>(expr).Compile();
        Assert.Equal("yes", lambda());
    }

    [Fact]
    public void HandleIif_ReturnsTruePart_WhenConditionTrue()
    {
        var visitor = CreateVisitor();
        var cond = Expression.Constant(true, typeof(bool));
        var left = Expression.Constant("yes", typeof(string));
        var right = Expression.Constant("no", typeof(string));
        var expr = visitor.HandleIif(new[] { cond, left, right });
        var lambda = Expression.Lambda<Func<string>>(expr).Compile();
        Assert.Equal("yes", lambda());
    }

    [Fact]
    public void HandleIif_ReturnsFalsePart_WhenConditionFalse()
    {
        var visitor = CreateVisitor();
        var cond = Expression.Constant(false, typeof(bool));
        var left = Expression.Constant("yes", typeof(string));
        var right = Expression.Constant("no", typeof(string));
        var expr = visitor.HandleIif(new[] { cond, left, right });
        var lambda = Expression.Lambda<Func<string>>(expr).Compile();
        Assert.Equal("no", lambda());
    }
}