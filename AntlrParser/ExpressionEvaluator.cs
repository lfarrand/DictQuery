using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using LazyCache;
using Microsoft.Extensions.Caching.Memory;

namespace AntlrParser
{
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

            _cacheLock.EnterReadLock();
            try
            {
                if (_cache.Get<Func<Dictionary<string, object>, bool>>(cacheKey) is
                    Func<Dictionary<string, object>, bool> cachedPredicate)
                {
                    return cachedPredicate;
                }
            }
            finally
            {
                _cacheLock.ExitReadLock();
            }

            _cacheLock.EnterWriteLock();

            try
            {
                if (_cache.Get<Func<Dictionary<string, object>, bool>>(cacheKey) is
                    Func<Dictionary<string, object>, bool> cachedPredicate)
                {
                    return cachedPredicate;
                }

                var lambdaExpression =
                    _expressionBuilder.BuildLambda(expression, data);

                var predicate = lambdaExpression.Compile();
                _cache.Add(cacheKey, predicate,
                    new MemoryCacheEntryOptions().SetAbsoluteExpiration(TimeSpan.FromHours(1)));
                return predicate;
            }
            finally
            {
                _cacheLock.ExitWriteLock();
            }
        }

        public IEnumerable<Dictionary<string, object>> Evaluate(string expression,
            IEnumerable<Dictionary<string, object>> data)
        {
            //Expression<Func<Dictionary<string, object>, bool>> lambdaExpression = _expressionBuilder.BuildLambda(expression);
            var dataList = data.ToList();
            var compiledExpression = CompileExpression(expression, dataList);
            return dataList.Where(compiledExpression);
        }
    }
}