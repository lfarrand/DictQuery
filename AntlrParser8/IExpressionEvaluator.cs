namespace AntlrParser8;

public interface IExpressionEvaluator
{
    Func<IDictionary<string, object>, bool> CompileExpression(string expression,
        IEnumerable<IDictionary<string, object>> data, bool shouldReplaceUnderscoreWithSpaceInKeyName = false);

    Func<T, bool> CompileExpression<T>(string expression);

    IEnumerable<IDictionary<string, object>> Evaluate(
        string expression,
        IEnumerable<IDictionary<string, object>> data);

    IEnumerable<T> Evaluate<T>(string expression, IEnumerable<T> data);
}