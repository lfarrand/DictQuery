using System.Linq.Expressions;

namespace AntlrParser8;

public interface IExpressionBuilder
{
    Expression<Func<Dictionary<string, object>, bool>> BuildLambda(string expressionText,
        IEnumerable<Dictionary<string, object>> data);
}