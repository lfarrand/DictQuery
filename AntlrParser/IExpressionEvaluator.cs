using System;
using System.Collections.Generic;

namespace AntlrParser
{
    public interface IExpressionEvaluator
    {
        Func<Dictionary<string, object>, bool> CompileExpression(string expression,
            IEnumerable<Dictionary<string, object>> data);

        IEnumerable<Dictionary<string, object>> Evaluate(
            string expression,
            IEnumerable<Dictionary<string, object>> data);
    }
}