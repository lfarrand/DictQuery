namespace AntlrParser8;

public interface IExpressionEvaluator
{
    Func<IDictionary<string, object>, bool> CompileExpression(string expression,
        IEnumerable<IDictionary<string, object>> data, bool shouldReplaceUnderscoreWithSpaceInKeyName = false);

    IEnumerable<IDictionary<string, object>> Evaluate(
        string expression,
        IEnumerable<IDictionary<string, object>> data,
        bool shouldReplaceUnderscoreWithSpaceInKeyName = false);
}