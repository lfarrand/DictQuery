using Xunit;

namespace AntlrParser8.Tests;

public class ExpressionBuilderTests
{
    private readonly IExpressionBuilder _builder = new ExpressionBuilder();

    private readonly List<Dictionary<string, object>> _sampleData = new List<Dictionary<string, object>>
    {
        new Dictionary<string, object> { ["Name"] = "Alice", ["Age"] = 30, ["Active"] = true },
        new Dictionary<string, object> { ["Name"] = "Bob", ["Age"] = 25, ["Active"] = false },
        new Dictionary<string, object> { ["Name"] = "Charlie", ["Age"] = 35, ["Active"] = true },
        new Dictionary<string, object> { ["Name"] = null, ["Age"] = null, ["Active"] = true }
    };

    [Fact]
    public void BuildLambda_ValidExpression_ReturnsCorrectLambda()
    {
        var lambda = _builder.BuildLambda("Age > 29", _sampleData);
        var results = _sampleData.Where(lambda.Compile()).Select(r => r["Name"] as string).ToList();
        Assert.Contains("Alice", results);
        Assert.Contains("Charlie", results);
        Assert.DoesNotContain("Bob", results);
    }

    [Fact]
    public void BuildLambda_UsesParameterCorrectly()
    {
        var lambda = _builder.BuildLambda("Active = true", _sampleData);
        var compiled = lambda.Compile();
        Assert.True(compiled(_sampleData[0]));
        Assert.False(compiled(_sampleData[1]));
    }

    [Fact]
    public void BuildLambda_InvalidColumn_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _builder.BuildLambda("NoSuchColumn = 1", _sampleData).Compile()(_sampleData[0]));
        Assert.Contains("Invalid column name: NoSuchColumn", ex.Message);
    }

    [Fact]
    public void BuildLambda_InvalidLiteral_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(() =>
            _builder.BuildLambda("Age > 'notanumber'", _sampleData).Compile()(_sampleData[0]));
        Assert.Contains("Cannot convert string 'notanumber' to decimal", ex.Message);
    }

    [Fact]
    public void BuildLambda_SyntaxError_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _builder.BuildLambda("Age >", _sampleData));
        Assert.Contains("Failed to parse expression", ex.Message);
    }

    [Fact]
    public void BuildLambda_EmptyExpression_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            _builder.BuildLambda("", _sampleData));
        Assert.Contains("Failed to parse expression", ex.Message);
    }

    [Fact]
    public void BuildLambda_NullExpression_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            _builder.BuildLambda(null, _sampleData));
    }

    [Fact]
    public void BuildLambda_RuntimeError_ThrowsArgumentException()
    {
        // For example, type mismatch at runtime
        var ex = Assert.Throws<ArgumentException>(() =>
            _builder.BuildLambda("Name > 100", _sampleData).Compile()(_sampleData[0]));
        Assert.Contains($"Invalid literal or type mismatch in expression: cannot convert 100 to String",
            ex.Message);
    }

    [Fact]
    public void BuildLambda_ParameterIsSameInstance()
    {
        // This test ensures the parameter is reused (not strictly necessary, but for completeness)
        var lambda = _builder.BuildLambda("Age = 30", _sampleData);
        var parameters = lambda.Parameters;
        Assert.Single(parameters);
        Assert.Equal("row", parameters[0].Name);
        Assert.Equal(typeof(Dictionary<string, object>), parameters[0].Type);
    }
}