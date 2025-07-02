using Antlr4.Runtime;
using Antlr4.Runtime.Misc;
using Antlr4.Runtime.Tree;

namespace AntlrParser8.Tests;

public class MockTerminalNode : ITerminalNode
{
    private IRuleNode _parent;
    public IToken Symbol => null;

    ITree ITree.GetChild(int i)
    {
        return GetChild(i);
    }

    public string ToStringTree()
    {
        throw new NotImplementedException();
    }

    IRuleNode ITerminalNode.Parent => _parent;

    ITree ITree.Parent => Parent;

    public object Payload { get; }
    public IParseTree Parent => null;
    public Interval SourceInterval => Interval.Invalid;

    public IParseTree GetChild(int i)
    {
        return null;
    }

    public int ChildCount => 0;

    public T Accept<T>(IParseTreeVisitor<T> visitor)
    {
        return default;
    }

    public string GetText()
    {
        return "MOCK";
    }

    public string ToStringTree(Parser parser)
    {
        return "MOCK";
    }
}