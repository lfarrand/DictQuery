using System.Linq.Expressions;
using Xunit;

namespace AntlrParser8.Tests;

public class ExpressionTreeVisitorCreateInExpressionTests
{
    private ExpressionTreeVisitor CreateVisitor()
    {
        var data = new[] { new Dictionary<string, object>() };
        var parameter = Expression.Parameter(typeof(IDictionary<string, object>), "row");
        return new ExpressionTreeVisitor(parameter, data);
    }

    [Fact]
    public void Throws_IfRightIsNotConstantExpression()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(1, typeof(object));
        var right = Expression.Parameter(typeof(List<object>), "notConstant");
        Assert.Throws<InvalidOperationException>(() => visitor.CreateInExpression(left, right));
    }

    [Fact]
    public void Throws_IfRightIsNotList()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(1, typeof(object));
        var right = Expression.Constant(123, typeof(object)); // not a list
        Assert.Throws<InvalidOperationException>(() => visitor.CreateInExpression(left, right));
    }

    [Fact]
    public void ConvertsLeftToObject_IfNotObject()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(2, typeof(int)); // not object
        var right = Expression.Constant(new List<object> { 2, 3 }, typeof(List<object>));
        var expr = visitor.CreateInExpression(left, right);
        var lambda = Expression.Lambda<Func<bool>>(expr).Compile();
        Assert.True(lambda());
    }

    [Fact]
    public void DoesNotConvertLeft_IfAlreadyObject()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant("foo", typeof(object));
        var right = Expression.Constant(new List<object> { "foo", "bar" }, typeof(List<object>));
        var expr = visitor.CreateInExpression(left, right);
        var lambda = Expression.Lambda<Func<bool>>(expr).Compile();
        Assert.True(lambda());
    }

    [Fact]
    public void ReturnsTrue_IfValueInList()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(5, typeof(object));
        var right = Expression.Constant(new List<object> { 1, 2, 5 }, typeof(List<object>));
        var expr = visitor.CreateInExpression(left, right);
        var lambda = Expression.Lambda<Func<bool>>(expr).Compile();
        Assert.True(lambda());
    }

    [Fact]
    public void ReturnsFalse_IfValueNotInList()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(9, typeof(object));
        var right = Expression.Constant(new List<object> { 1, 2, 5 }, typeof(List<object>));
        var expr = visitor.CreateInExpression(left, right);
        var lambda = Expression.Lambda<Func<bool>>(expr).Compile();
        Assert.False(lambda());
    }

    [Fact]
    public void ReturnsFalse_IfListIsEmpty()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(1, typeof(object));
        var right = Expression.Constant(new List<object>(), typeof(List<object>));
        var expr = visitor.CreateInExpression(left, right);
        var lambda = Expression.Lambda<Func<bool>>(expr).Compile();
        Assert.False(lambda());
    }

    [Fact]
    public void ReturnsFalse_IfLeftIsNull()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(null, typeof(object));
        var right = Expression.Constant(new List<object> { 1, 2, 3 }, typeof(List<object>));
        var expr = visitor.CreateInExpression(left, right);
        var lambda = Expression.Lambda<Func<bool>>(expr).Compile();
        Assert.False(lambda());
    }
}