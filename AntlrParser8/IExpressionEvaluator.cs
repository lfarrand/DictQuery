namespace AntlrParser8;

public interface IExpressionEvaluator
{
    Func<Dictionary<string, object>, bool> CompileExpression(string expression,
        IEnumerable<Dictionary<string, object>> data);

    Func<T, bool> CompileExpression<T>(string expression);

    IEnumerable<Dictionary<string, object>> Evaluate(
        string expression,
        IEnumerable<Dictionary<string, object>> data);

    IEnumerable<T> Evaluate<T>(string expression, IEnumerable<T> data);
}