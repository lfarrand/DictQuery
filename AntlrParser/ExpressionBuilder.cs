using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Antlr4.Runtime;

namespace AntlrParser
{
    public class ExpressionBuilder : IExpressionBuilder
    {
        public Expression<Func<Dictionary<string, object>, bool>> BuildLambda(string expressionText)
        {
            try
            {
                // 1. Create a single ParameterExpression:
                var parameter = Expression.Parameter(typeof(Dictionary<string, object>), "row");

                // 2. Parse and visit using that parameter:
                var lexer = new DataTableExpressionLexer(new AntlrInputStream(expressionText));
                var tokens = new CommonTokenStream(lexer);
                var parser = new DataTableExpressionParser(tokens);
                var parseTree = parser.expression();

                var visitor = new ExpressionTreeVisitor(parameter);
                Expression body = visitor.Visit(parseTree);

                // 3. Build the lambda with the same parameter instance:
                return Expression.Lambda<Func<Dictionary<string, object>, bool>>(body, parameter);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse expression: {expressionText}", ex);
            }
        }
    }
}