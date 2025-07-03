using Antlr4.Runtime.Tree;

namespace AntlrParser8.Tests;

public class MockComparisonExpressionContext : ModelExpressionParser.ComparisonExpressionContext
{
    private readonly string _op;

    public MockComparisonExpressionContext(string op)
        : base(null, 0) // Pass null for parent and invokingState
    {
        _op = op;
    }

    public override ITerminalNode EQUALS()
    {
        return _op == "EQUALS" ? new MockTerminalNode() : null;
    }

    public override ITerminalNode NOT_EQUALS()
    {
        return _op == "NOT_EQUALS" ? new MockTerminalNode() : null;
    }

    public override ITerminalNode LESS_THAN()
    {
        return _op == "LESS_THAN" ? new MockTerminalNode() : null;
    }

    public override ITerminalNode GREATER_THAN()
    {
        return _op == "GREATER_THAN" ? new MockTerminalNode() : null;
    }

    public override ITerminalNode LESS_THAN_OR_EQUAL()
    {
        return _op == "LESS_THAN_OR_EQUAL" ? new MockTerminalNode() : null;
    }

    public override ITerminalNode GREATER_THAN_OR_EQUAL()
    {
        return _op == "GREATER_THAN_OR_EQUAL" ? new MockTerminalNode() : null;
    }

    public override string GetText()
    {
        return "MOCK_OP";
    }
}