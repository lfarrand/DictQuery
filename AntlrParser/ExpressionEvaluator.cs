using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using LazyCache;

namespace AntlrParser
{
    public class ExpressionEvaluator : IExpressionEvaluator
    {
        private readonly IAppCache _cache;
        private readonly IExpressionBuilder _expressionBuilder;

        public ExpressionEvaluator(IAppCache cache, IExpressionBuilder expressionBuilder)
        {
            _cache = cache;
            _expressionBuilder = expressionBuilder;
        }

        public Func<Dictionary<string, object>, bool> CompileExpression(string expression)
        {
            return _cache.GetOrAdd($"expr_{expression}", () =>
            {
                Expression<Func<Dictionary<string, object>, bool>> lambdaExpression = _expressionBuilder.BuildLambda(expression);
                
                return lambdaExpression.Compile();
            });
        }

        public IEnumerable<Dictionary<string, object>> Evaluate(string expression, 
            IEnumerable<Dictionary<string, object>> data)
        {
            Expression<Func<Dictionary<string, object>, bool>> lambdaExpression = _expressionBuilder.BuildLambda(expression);
            var compiledExpression = CompileExpression(expression);
            return data.Where(compiledExpression);
        }
    }
}