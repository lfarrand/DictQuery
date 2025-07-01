using System;
using System.Collections.Generic;

namespace AntlrParser
{
    public interface IExpressionEvaluator
    {
        IEnumerable<Dictionary<string, object>> Evaluate(
            string expression, 
            IEnumerable<Dictionary<string, object>> data);
        
        Func<Dictionary<string, object>, bool> CompileExpression(string expression);
    }
}