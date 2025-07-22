using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.ObjectPool;

namespace AntlrParser8;

public class ExpressionEvaluator : IExpressionEvaluator
{
    private readonly IExpressionBuilder _expressionBuilder;

    private static readonly ConcurrentDictionary<string, Func<IDictionary<string, object>, bool>> DictionaryCache =
        new ConcurrentDictionary<string, Func<IDictionary<string, object>, bool>>();
    
    private static readonly ConcurrentDictionary<(Type type, string expression), object> CompiledExpressionCache 
        = new ConcurrentDictionary<(Type, string), object>();
    
    private static readonly ObjectPool<StringBuilder> StringBuilderPool = new DefaultObjectPoolProvider().CreateStringBuilderPool();
    
    public ExpressionEvaluator( IExpressionBuilder expressionBuilder)
    {
        _expressionBuilder = expressionBuilder;
    }
    
    // Use pooled objects for temporary operations
    private string BuildCacheKey<T>(string expression)
    {
        var sb = StringBuilderPool.Get();
        try
        {
            sb.Clear();
            sb.Append(typeof(T).FullName);
            sb.Append(":::");
            sb.Append(expression);
            return sb.ToString();
        }
        finally
        {
            StringBuilderPool.Return(sb);
        }
    }

    public Func<IDictionary<string, object>, bool> CompileExpression(string expression,
        IEnumerable<IDictionary<string, object>> data, bool shouldReplaceUnderscoreWithSpaceInKeyName = false)
    {
        var cacheKey =
            BuildCacheKey<IDictionary<string, object>>(
                $"expr_replacespaces{(shouldReplaceUnderscoreWithSpaceInKeyName ? 1 : 0)}_{expression}");

        return DictionaryCache.GetOrAdd(cacheKey, entry =>
        {
            var lambdaExpression = _expressionBuilder.BuildLambda(expression, data, shouldReplaceUnderscoreWithSpaceInKeyName);
            Func<IDictionary<string, object>, bool> predicate = lambdaExpression.Compile();
            return predicate;
        });
    }

    public IEnumerable<IDictionary<string, object>> Evaluate(string expression,
        IEnumerable<IDictionary<string, object>> data, bool shouldReplaceUnderscoreWithSpaceInKeyName = false)
    {
        //Expression<Func<IDictionary<string, object>, bool>> lambdaExpression = _expressionBuilder.BuildLambda(expression);
        var dataList = data.ToList();
        var compiledExpression = CompileExpression(expression, dataList, shouldReplaceUnderscoreWithSpaceInKeyName);
        return dataList.Where(compiledExpression);
    }
    
    private static T[] EvaluateArray<T>(Func<T, bool> predicate, T[] array)
    {
        var results = new List<T>(array.Length / 4); // Estimate result size
        
        // Process in chunks for better cache locality
        const int chunkSize = 1000;
        for (int i = 0; i < array.Length; i += chunkSize)
        {
            var end = Math.Min(i + chunkSize, array.Length);
            for (int j = i; j < end; j++)
            {
                if (predicate(array[j]))
                {
                    results.Add(array[j]);
                }
            }
        }
        
        return results.ToArray();
    }
}