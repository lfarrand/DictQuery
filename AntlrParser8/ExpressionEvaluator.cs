using System.Collections.Concurrent;

namespace AntlrParser8;

public class ExpressionEvaluator : IExpressionEvaluator
{
    private readonly IExpressionBuilder _expressionBuilder;

    private static readonly ConcurrentDictionary<string, Func<IDictionary<string, object>, bool>> IDictionaryCache =
        new ConcurrentDictionary<string, Func<IDictionary<string, object>, bool>>();
    
    private static readonly ConcurrentDictionary<(Type type, string expression), object> CompiledExpressionCache 
        = new ConcurrentDictionary<(Type, string), object>();

    public ExpressionEvaluator( IExpressionBuilder expressionBuilder)
    {
        _expressionBuilder = expressionBuilder;
    }

    public Func<IDictionary<string, object>, bool> CompileExpression(string expression,
        IEnumerable<IDictionary<string, object>> data, bool shouldReplaceUnderscoreWithSpaceInKeyName = false)
    {
        var cacheKey = $"expr_replacespaces{(shouldReplaceUnderscoreWithSpaceInKeyName ? 1 : 0)}_{expression}";

        return IDictionaryCache.GetOrAdd(cacheKey, entry =>
        {
            var lambdaExpression = _expressionBuilder.BuildLambda(expression, data);
            Func<IDictionary<string, object>, bool> predicate = lambdaExpression.Compile();
            return predicate;
        });
    }

    public Func<T, bool> CompileExpression<T>(string expression)
    {
        var key = (typeof(T), expression);
    
        if (CompiledExpressionCache.TryGetValue(key, out var cached))
        {
            return (Func<T, bool>)cached;
        }

        var compiled = BuildCompiledExpression<T>(expression);
        CompiledExpressionCache[key] = compiled;
    
        return compiled;
    }
    
    private Func<T, bool> BuildCompiledExpression<T>(string expression)
    {
        var lambdaExpression = _expressionBuilder.BuildLambda<T>(expression);
        Func<T, bool> predicate = lambdaExpression.Compile();
        return predicate;
    }

    public IEnumerable<IDictionary<string, object>> Evaluate(string expression,
        IEnumerable<IDictionary<string, object>> data)
    {
        //Expression<Func<IDictionary<string, object>, bool>> lambdaExpression = _expressionBuilder.BuildLambda(expression);
        var dataList = data.ToList();
        var compiledExpression = CompileExpression(expression, dataList);
        return dataList.Where(compiledExpression);
    }

    public IEnumerable<T> Evaluate<T>(string expression, IEnumerable<T> data)
    {
        var dataList = data.ToList();
        var compiledExpression = CompileExpression<T>(expression);
        return dataList.Where(compiledExpression);
    }
}