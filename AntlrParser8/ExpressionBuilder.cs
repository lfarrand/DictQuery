using System.Linq.Expressions;
using Antlr4.Runtime;

namespace AntlrParser8;

public class ExpressionBuilder : IExpressionBuilder
{
    public Expression<Func<IDictionary<string, object>, bool>> BuildLambda(string expressionText,
        IEnumerable<IDictionary<string, object>> data, bool shouldReplaceUnderscoreWithSpaceInKeyName = false)
    {
        try
        {
            // 1. Create a single ParameterExpression:
            var parameter = Expression.Parameter(typeof(IDictionary<string, object>), "row");

            // 2. Parse and visit using that parameter:
            var lexer = new ModelExpressionLexer(new AntlrInputStream(expressionText));
            var tokens = new CommonTokenStream(lexer);
            var parser = new ModelExpressionParser(tokens);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new CustomErrorListener());
            parser.BuildParseTree = true;
            var parseTree = parser.expression();

            var visitor = new ExpressionTreeVisitor(parameter, data, shouldReplaceUnderscoreWithSpaceInKeyName);
            var body = visitor.Visit(parseTree);

            // 3. Build the lambda with the same parameter instance:
            return Expression.Lambda<Func<IDictionary<string, object>, bool>>(body, parameter);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse expression: {expressionText}", ex);
        }
    }
}