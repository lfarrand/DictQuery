namespace AntlrParser.Tests
{
    public class MockPrimaryExpressionContext : DataTableExpressionParser.PrimaryExpressionContext
    {
        public MockPrimaryExpressionContext() : base(null, 0)
        {
        }

        public override bool Equals(object obj)
        {
            return false;
        }
    }
}