namespace AntlrParser.Tests
{
    public static class TestHelpers
    {
        public static DataTableExpressionParser.ComparisonExpressionContext CreateComparisonContext(string op)
        {
            return new MockComparisonExpressionContext(op);
        }
    }
}