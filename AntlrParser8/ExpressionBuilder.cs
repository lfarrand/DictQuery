using System.Linq.Expressions;
using Antlr4.Runtime;

namespace AntlrParser8;

public class ExpressionBuilder : IExpressionBuilder
{
    public Expression<Func<Dictionary<string, object>, bool>> BuildLambda(string expressionText,
        IEnumerable<Dictionary<string, object>> data)
    {
        try
        {
            // 1. Create a single ParameterExpression:
            var parameter = Expression.Parameter(typeof(Dictionary<string, object>), "row");

            // 2. Parse and visit using that parameter:
            var lexer = new ModelExpressionLexer(new AntlrInputStream(expressionText));
            var tokens = new CommonTokenStream(lexer);
            var parser = new ModelExpressionParser(tokens);
            parser.RemoveErrorListeners();
            parser.AddErrorListener(new CustomErrorListener());
            parser.BuildParseTree = true;
            var parseTree = parser.expression();

            var visitor = new ExpressionTreeVisitor(parameter, data);
            var body = visitor.Visit(parseTree);

            // 3. Build the lambda with the same parameter instance:
            return Expression.Lambda<Func<Dictionary<string, object>, bool>>(body, parameter);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse expression: {expressionText}", ex);
        }
    }

    public Expression<Func<T, bool>> BuildLambda<T>(string expressionText)
    {
        try
        {
            // 1. Create a parameter for the lambda (e.g., "x")
            var parameter = Expression.Parameter(typeof(T), "x");

            // 2. Parse the expression string using your ANTLR parser
            var inputStream = new AntlrInputStream(expressionText);
            var lexer = new ModelExpressionLexer(inputStream);
            var tokens = new CommonTokenStream(lexer);
            var parser = new ModelExpressionParser(tokens);
            var parseTree = parser.expression();

            // 3. Visit the parse tree to build the expression tree
            var visitor = new ExpressionTreeVisitor<T>(parameter);
            var body = visitor.Visit(parseTree);

            // 4. Ensure the body is a boolean expression
            if (body.Type != typeof(bool))
            {
                body = visitor.EnsureBoolean(body);
            }

            // 5. Build and return the lambda
            return Expression.Lambda<Func<T, bool>>(body, parameter);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to parse expression: {expressionText}", ex);
        }
    }
}