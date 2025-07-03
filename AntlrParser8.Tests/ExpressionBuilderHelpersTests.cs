using System.Linq.Expressions;
using Xunit;

namespace AntlrParser8.Tests;

public class ExpressionBuilderHelpersTests
{
    public class Person
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public double Salary { get; set; }
    }

    [Fact]
    public void BuildPropertyAccess_Class_ReturnsCorrectProperty()
    {
        var param = Expression.Parameter(typeof(Person), "x");
        var expr = ExpressionBuilderHelpers.BuildPropertyAccess<Person>(param, "Name");
        var lambda = Expression.Lambda<Func<Person, string>>(expr, param).Compile();
        var person = new Person { Name = "Alice" };
        Assert.Equal("Alice", lambda(person));
    }

    [Fact]
    public void BuildPropertyAccess_Dictionary_ReturnsCorrectValue()
    {
        var param = Expression.Parameter(typeof(Dictionary<string, object>), "x");
        var expr = ExpressionBuilderHelpers.BuildPropertyAccess<Dictionary<string, object>>(param, "Name");
        var lambda = Expression.Lambda<Func<Dictionary<string, object>, object>>(expr, param).Compile();
        var dict = new Dictionary<string, object> { ["Name"] = "Bob" };
        Assert.Equal("Bob", lambda(dict));
    }

    [Fact]
    public void BuildPropertyLambda_Class_WorksForIntAndDouble()
    {
        var lambdaInt = ExpressionBuilderHelpers.BuildPropertyLambda<Person>("Age").Compile();
        var lambdaDouble = ExpressionBuilderHelpers.BuildPropertyLambda<Person>("Salary").Compile();
        var person = new Person { Age = 42, Salary = 12345.67 };
        Assert.Equal(42, lambdaInt(person));
        Assert.Equal(12345.67, lambdaDouble(person));
    }

    [Fact]
    public void BuildPropertyLambda_Dictionary_WorksForIntAndDouble()
    {
        var lambdaInt = ExpressionBuilderHelpers.BuildPropertyLambda<Dictionary<string, object>>("Age").Compile();
        var lambdaDouble = ExpressionBuilderHelpers.BuildPropertyLambda<Dictionary<string, object>>("Salary").Compile();
        var dict = new Dictionary<string, object> { ["Age"] = 99, ["Salary"] = 555.5 };
        Assert.Equal(99, lambdaInt(dict));
        Assert.Equal(555.5, lambdaDouble(dict));
    }

    [Fact]
    public void BuildPropertyAccess_Class_PropertyDoesNotExist_Throws()
    {
        var param = Expression.Parameter(typeof(Person), "x");
        Assert.Throws<ArgumentException>(() =>
            ExpressionBuilderHelpers.BuildPropertyAccess<Person>(param, "NonExistent"));
    }

    [Fact]
    public void BuildPropertyAccess_Dictionary_KeyDoesNotExist_ReturnsNull()
    {
        var param = Expression.Parameter(typeof(Dictionary<string, object>), "x");
        var expr = ExpressionBuilderHelpers.BuildSafeDictionaryAccess(param, "MissingKey");
        var lambda = Expression.Lambda<Func<Dictionary<string, object>, object>>(expr, param).Compile();
        var dict = new Dictionary<string, object>();
        Assert.Null(lambda(dict)); // Will return null if key is missing
    }
}