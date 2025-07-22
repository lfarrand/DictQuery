using System.Linq.Expressions;

namespace AntlrParser8;

public interface IExpressionBuilder
{
    Expression<Func<IDictionary<string, object>, bool>> BuildLambda(string expressionText,
        IEnumerable<IDictionary<string, object>> data, bool shouldReplaceUnderscoreWithSpaceInKeyName = false);
}