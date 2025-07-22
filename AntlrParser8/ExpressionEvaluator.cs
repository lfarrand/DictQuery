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
    
    public IEnumerable<T> EvaluateBatch<T>(string expression, IEnumerable<T> data)
    {
        var predicate = CompileExpression<T>(expression);
        
        // Use SIMD-friendly operations when possible
        if (data is T[] array)
        {
            return EvaluateArray(predicate, array);
        }
        
        return data.Where(predicate);
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