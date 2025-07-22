using LazyCache;
using Microsoft.Extensions.Caching.Memory;

namespace AntlrParser8;

public class ExpressionEvaluator : IExpressionEvaluator
{
    private readonly IAppCache _cache;
    private readonly IExpressionBuilder _expressionBuilder;
    private readonly ReaderWriterLockSlim _cacheLock;

    public ExpressionEvaluator(IAppCache cache, IExpressionBuilder expressionBuilder,
        ReaderWriterLockSlim cacheLock)
    {
        _cache = cache;
        _expressionBuilder = expressionBuilder;
        _cacheLock = cacheLock;
    }

    public Func<Dictionary<string, object>, bool> CompileExpression(string expression,
        IEnumerable<Dictionary<string, object>> data)
    {
        var cacheKey = $"expr_{expression}";

        return _cache.GetOrAdd(cacheKey, () =>
        {
            var lambdaExpression = _expressionBuilder.BuildLambda(expression, data);
            var predicate = lambdaExpression.Compile();
            return predicate;
        });
    }

    public Func<T, bool> CompileExpression<T>(string expression)
    {
        var cacheKey = $"expr_{expression}";

        return _cache.GetOrAdd(cacheKey, () =>
        {
            var lambdaExpression = _expressionBuilder.BuildLambda<T>(expression);
            var predicate = lambdaExpression.Compile();
            return predicate;
        });
    }

    public IEnumerable<Dictionary<string, object>> Evaluate(string expression,
        IEnumerable<Dictionary<string, object>> data)
    {
        //Expression<Func<Dictionary<string, object>, bool>> lambdaExpression = _expressionBuilder.BuildLambda(expression);
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