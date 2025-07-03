namespace AntlrParser8.Tests;

public static class TestHelpers
{
    public static ModelExpressionParser.ComparisonExpressionContext CreateComparisonContext(string op)
    {
        return new MockComparisonExpressionContext(op);
    }
}