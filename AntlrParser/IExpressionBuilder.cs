using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace AntlrParser
{
    public interface IExpressionBuilder
    {
        Expression<Func<Dictionary<string, object>, bool>> BuildLambda(string expressionText);
    }
}