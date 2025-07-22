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
    public void BuildPropertyAccess_IDictionary_ReturnsCorrectValue()
    {
        var param = Expression.Parameter(typeof(IDictionary<string, object>), "x");
        var expr = ExpressionBuilderHelpers.BuildPropertyAccess<IDictionary<string, object>>(param, "Name");
        var lambda = Expression.Lambda<Func<IDictionary<string, object>, object>>(expr, param).Compile();
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
    public void BuildPropertyLambda_IDictionary_WorksForIntAndDouble()
    {
        var lambdaInt = ExpressionBuilderHelpers.BuildPropertyLambda<IDictionary<string, object>>("Age").Compile();
        var lambdaDouble = ExpressionBuilderHelpers.BuildPropertyLambda<IDictionary<string, object>>("Salary").Compile();
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
    public void BuildPropertyAccess_IDictionary_KeyDoesNotExist_ReturnsNull()
    {
        var param = Expression.Parameter(typeof(IDictionary<string, object>), "x");
        var expr = ExpressionBuilderHelpers.BuildSafeIDictionaryAccess(param, "MissingKey");
        var lambda = Expression.Lambda<Func<IDictionary<string, object>, object>>(expr, param).Compile();
        var dict = new Dictionary<string, object>();
        Assert.Null(lambda(dict)); // Will return null if key is missing
    }
}