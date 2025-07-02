using Antlr4.Runtime;
using NSubstitute;

namespace AntlrParser.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Linq.Expressions;
    using Xunit;

    public class ExpressionTreeVisitorTests
    {
        private readonly List<Dictionary<string, object>> _sampleData = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object>
                { ["Name"] = "Alice", ["Age"] = 30, ["Active"] = true, ["Salary"] = 1000.5m },
            new Dictionary<string, object> { ["Name"] = "Bob", ["Age"] = 25, ["Active"] = false, ["Salary"] = 800.0m },
            new Dictionary<string, object>
                { ["Name"] = "Charlie", ["Age"] = 35, ["Active"] = true, ["Salary"] = 1200.0m },
            new Dictionary<string, object> { ["Name"] = "Dana", ["Age"] = 30, ["Active"] = false, ["Salary"] = 950.0m },
            new Dictionary<string, object> { ["Name"] = null, ["Age"] = null, ["Active"] = true, ["Salary"] = null }
        };

        private ExpressionTreeVisitor CreateVisitor()
        {
            var parameter = Expression.Parameter(typeof(Dictionary<string, object>), "row");
            return new ExpressionTreeVisitor(parameter, _sampleData);
        }

        private Expression<Func<Dictionary<string, object>, bool>> BuildPredicate(string expressionText)
        {
            var visitor = CreateVisitor();
            var lexer = new DataTableExpressionLexer(new AntlrInputStream(expressionText));
            var tokens = new CommonTokenStream(lexer);
            var parser = new DataTableExpressionParser(tokens);
            var parseTree = parser.expression();
            var body = visitor.Visit(parseTree);
            return Expression.Lambda<Func<Dictionary<string, object>, bool>>(body, visitor.Parameter);
        }

        [Theory]
        [InlineData("Age = 30", new[] { "Alice", "Dana" })]
        [InlineData("Name = 'Bob'", new[] { "Bob" })]
        [InlineData("Active = true", new[] { "Alice", "Charlie", null })]
        [InlineData("Age > 25 AND Active = true", new[] { "Alice", "Charlie" })]
        [InlineData("Name LIKE 'A*'", new[] { "Alice" })]
        [InlineData("Age IN (25, 35)", new[] { "Bob", "Charlie" })]
        [InlineData("Age <> 30", new[] { "Bob", "Charlie" })]
        [InlineData("NOT Active", new[] { "Bob", "Dana" })]
        [InlineData("Name IS NOT NULL", new[] { "Alice", "Bob", "Charlie", "Dana" })]
        [InlineData("Name IS NULL", new[] { (string)null })]
        [InlineData("Age >= 30", new[] { "Alice", "Charlie", "Dana" })]
        [InlineData("Age <= 25", new[] { "Bob" })]
        [InlineData("Salary > 900", new[] { "Alice", "Charlie", "Dana" })]
        public void Evaluate_ValidExpressions_ReturnsExpected(string expr, string[] expectedNames)
        {
            var predicate = BuildPredicate(expr).Compile();
            var results = _sampleData.Where(predicate)
                .Select(r => r.ContainsKey("Name") ? r["Name"] as string : null)
                .OrderBy(n => n)
                .ToArray();

            Assert.Equal(expectedNames.OrderBy(n => n).ToArray(), results);
        }

        [Fact]
        public void Evaluate_FunctionLen_ShouldReturnStringLength()
        {
            var expr = "LEN(Name) = 5";
            var predicate = BuildPredicate(expr).Compile();
            var results = _sampleData.Where(predicate)
                .Select(r => r["Name"] as string)
                .ToArray();

            Assert.Contains("Alice", results);
        }

        [Fact]
        public void Evaluate_FunctionConvert_ShouldConvertTypes()
        {
            var expr = "CONVERT(Age, 'System.Double') > 29.5";
            var predicate = BuildPredicate(expr).Compile();
            var results = _sampleData.Where(predicate)
                .Select(r => r["Name"] as string)
                .OrderBy(n => n)
                .ToArray();

            Assert.Contains("Alice", results);
            Assert.Contains("Dana", results);
        }

        [Fact]
        public void Evaluate_FunctionIsNull_ShouldReplaceNulls()
        {
            var expr = "ISNULL(Name, 'Unknown') = 'Unknown'";
            var predicate = BuildPredicate(expr).Compile();
            var results = _sampleData.Where(predicate)
                .Select(r => r.ContainsKey("Name") ? r["Name"] as string : null)
                .ToArray();

            Assert.Contains(null, results); // The row with null Name
        }

        [Fact]
        public void Evaluate_FunctionIif_ShouldReturnCorrectValue()
        {
            var expr = "IIF(Active, 'Yes', 'No') = 'Yes'";
            var predicate = BuildPredicate(expr).Compile();
            var results = _sampleData.Where(predicate)
                .Select(r => r["Name"] as string)
                .ToArray();

            Assert.Contains("Alice", results);
            Assert.Contains("Charlie", results);
        }

        [Fact]
        public void Evaluate_FunctionTrim_ShouldTrimStrings()
        {
            var expr = "TRIM('  Alice  ') = 'Alice'";
            var predicate = BuildPredicate(expr).Compile();
            Assert.True(predicate(_sampleData[0]));
        }

        [Fact]
        public void Evaluate_FunctionSubstring_ShouldExtractCorrectSubstring()
        {
            var expr = "SUBSTRING(Name, 1, 3) = 'Ali'";
            var predicate = BuildPredicate(expr).Compile();
            var results = _sampleData.Where(predicate)
                .Select(r => r["Name"] as string)
                .ToArray();

            Assert.Contains("Alice", results);
        }

        [Fact]
        public void Evaluate_ArithmeticExpressions_ShouldComputeCorrectly()
        {
            var expr = "Age + 5 = 35";
            var predicate = BuildPredicate(expr).Compile();
            var results = _sampleData.Where(predicate)
                .Select(r => r["Name"] as string)
                .ToArray();

            Assert.Contains("Alice", results);
            Assert.Contains("Dana", results);
        }

        [Fact]
        public void Evaluate_LogicalExpressions_ShouldEvaluateCorrectly()
        {
            var expr = "Age = 30 AND Active = false";
            var predicate = BuildPredicate(expr).Compile();
            var results = _sampleData.Where(predicate)
                .Select(r => r["Name"] as string)
                .ToArray();

            Assert.Contains("Dana", results);
        }

        [Fact]
        public void Evaluate_NotExpression_ShouldNegateCorrectly()
        {
            var expr = "NOT Active";
            var predicate = BuildPredicate(expr).Compile();
            var results = _sampleData.Where(predicate)
                .Select(r => r["Name"] as string)
                .ToArray();

            Assert.Contains("Bob", results);
            Assert.Contains("Dana", results);
        }

        [Fact]
        public void Evaluate_ComparisonWithNull_ShouldWork()
        {
            var expr = "Name IS NULL";
            var predicate = BuildPredicate(expr).Compile();
            var results = _sampleData.Where(predicate)
                .Select(r => r.ContainsKey("Name") ? r["Name"] as string : null)
                .ToArray();

            Assert.Contains(null, results);
        }

        [Fact]
        public void Evaluate_InExpression_ShouldMatchCorrectly()
        {
            var expr = "Age IN (25, 30)";
            var predicate = BuildPredicate(expr).Compile();
            var results = _sampleData.Where(predicate)
                .Select(r => r["Name"] as string)
                .ToArray();

            Assert.Contains("Alice", results);
            Assert.Contains("Bob", results);
            Assert.Contains("Dana", results);
        }

        [Fact]
        public void Evaluate_InvalidColumn_ShouldThrow()
        {
            var visitor = CreateVisitor();
            var lexer = new DataTableExpressionLexer(new AntlrInputStream("InvalidColumn = 123"));
            var tokens = new CommonTokenStream(lexer);
            var parser = new DataTableExpressionParser(tokens);
            var tree = parser.expression();

            var body = visitor.Visit(tree);
            var lambda = Expression.Lambda<Func<Dictionary<string, object>, bool>>(body, visitor.Parameter).Compile();

            var row = new Dictionary<string, object> { ["Name"] = "Alice" };
            Assert.Throws<InvalidOperationException>(() => lambda(row));
        }

        [Fact]
        public void Evaluate_InvalidLiteral_ShouldThrow()
        {
            var visitor = CreateVisitor();
            var lexer = new DataTableExpressionLexer(new AntlrInputStream("Age > 'abc'"));
            var tokens = new CommonTokenStream(lexer);
            var parser = new DataTableExpressionParser(tokens);
            var tree = parser.expression();

            var body = visitor.Visit(tree);
            var lambda = Expression.Lambda<Func<Dictionary<string, object>, bool>>(body, visitor.Parameter).Compile();

            var row = new Dictionary<string, object> { ["Age"] = 42 };
            Assert.Throws<ArgumentException>(() => lambda(row));
        }

        [Theory]
        [InlineData("Active <= true")]
        [InlineData("Active > false")]
        public void InvalidBooleanRelationalOperators_ShouldThrow(string query)
        {
            var visitor = CreateVisitor();
            var lexer = new DataTableExpressionLexer(new AntlrInputStream(query));
            var tokens = new CommonTokenStream(lexer);
            var parser = new DataTableExpressionParser(tokens);
            var tree = parser.expression();

            var body = visitor.Visit(tree);
            var lambda = Expression.Lambda<Func<Dictionary<string, object>, bool>>(body, visitor.Parameter).Compile();

            var row = new Dictionary<string, object> { ["Active"] = true };
            Assert.Throws<InvalidOperationException>(() => lambda(row));
        }

        [Fact]
        public void Evaluate_TypeMismatch_ShouldThrow()
        {
            var visitor = CreateVisitor();
            var lexer = new DataTableExpressionLexer(new AntlrInputStream("Name > 100"));
            var tokens = new CommonTokenStream(lexer);
            var parser = new DataTableExpressionParser(tokens);
            var tree = parser.expression();

            var body = visitor.Visit(tree);
            var lambda = Expression.Lambda<Func<Dictionary<string, object>, bool>>(body, visitor.Parameter).Compile();

            var row = new Dictionary<string, object> { ["Name"] = "Alice" };
            Assert.Throws<ArgumentException>(() => lambda(row));
        }

        [Fact]
        public void ConvertConstantToType_ShouldHandleNullAndTypes()
        {
            var visitor = CreateVisitor();

            // Null to int (should return Expression.Constant(null, typeof(int?)) or Expression.Default(typeof(int)))
            var resultNullInt = visitor.ConvertConstantToType(Expression.Constant(null, typeof(object)), typeof(int?));
            Assert.Equal(typeof(int?), resultNullInt.Type);
            Assert.Null(((ConstantExpression)resultNullInt).Value);

            // String to int
            var resultStrInt = visitor.ConvertConstantToType(Expression.Constant("123", typeof(object)), typeof(int));
            Assert.Equal(typeof(int), resultStrInt.Type);
            Assert.Equal(123, ((ConstantExpression)resultStrInt).Value);

            // Int to string
            var resultIntStr = visitor.ConvertConstantToType(Expression.Constant(456, typeof(object)), typeof(string));
            Assert.Equal(typeof(string), resultIntStr.Type);
            Assert.Equal("456", ((ConstantExpression)resultIntStr).Value);

            // Already correct type
            var resultBool = visitor.ConvertConstantToType(Expression.Constant(true, typeof(object)), typeof(bool));
            Assert.Equal(typeof(bool), resultBool.Type);
            Assert.True((bool)((ConstantExpression)resultBool).Value);

            // Invalid conversion
            Assert.Throws<ArgumentException>(() =>
                visitor.ConvertConstantToType(Expression.Constant("abc", typeof(object)), typeof(int)));
        }

        [Theory]
        [InlineData(typeof(int), true)]
        [InlineData(typeof(double), true)]
        [InlineData(typeof(decimal), true)]
        [InlineData(typeof(float), true)]
        [InlineData(typeof(long), true)]
        [InlineData(typeof(bool), false)]
        [InlineData(typeof(string), false)]
        public void IsNumericType_ShouldReturnExpected(Type type, bool expected)
        {
            var visitor = CreateVisitor();
            Assert.Equal(expected, visitor.IsNumericType(type));
        }

        [Fact]
        public void HandleAggregate_ShouldThrowNotSupported()
        {
            var visitor = CreateVisitor();
            var ex = Assert.Throws<NotSupportedException>(() =>
                visitor.HandleAggregate("SUM", new Expression[] { Expression.Constant(1) }));
            Assert.Contains("Aggregate function 'SUM' is not supported", ex.Message);
        }

        [Fact]
        public void CreateComparisonCall_ShouldCompareInts()
        {
            var visitor = CreateVisitor();
            // Simulate context for 'EQUALS'
            var context = TestHelpers.CreateComparisonContext("EQUALS");
            var left = Expression.Constant(5, typeof(object));
            var right = Expression.Constant(5, typeof(object));
            var expr = visitor.CreateComparisonCall(context, left, right);

            var lambda = Expression.Lambda<Func<bool>>(expr).Compile();
            Assert.True(lambda());
        }

        [Fact]
        public void CreateComparisonCall_ShouldThrowForBoolRelational()
        {
            var visitor = CreateVisitor();
            var context = TestHelpers.CreateComparisonContext("LESS_THAN");
            var left = Expression.Constant(true, typeof(object));
            var right = Expression.Constant(false, typeof(object));
            Assert.Throws<InvalidOperationException>(() => visitor.CreateComparisonCall(context, left, right));
        }

        [Fact]
        public void HandleIif_ShouldReturnCorrectBranch()
        {
            var visitor = CreateVisitor();
            var condition = Expression.Constant(true, typeof(bool));
            var truePart = Expression.Constant("yes", typeof(string));
            var falsePart = Expression.Constant("no", typeof(string));
            var expr = visitor.HandleIif(new Expression[] { condition, truePart, falsePart });

            var lambda = Expression.Lambda<Func<string>>(expr).Compile();
            Assert.Equal("yes", lambda());
        }

        [Fact]
        public void CreateInExpression_ShouldReturnTrueIfInList()
        {
            var visitor = CreateVisitor();
            var list = new List<object> { 1, 2, 3 };
            var left = Expression.Constant(2, typeof(object));
            var right = Expression.Constant(list, typeof(List<object>));
            var expr = visitor.CreateInExpression(left, right);

            var lambda = Expression.Lambda<Func<bool>>(expr).Compile();
            Assert.True(lambda());
        }

        [Fact]
        public void CreateInExpression_ShouldThrowIfRightIsNotList()
        {
            var visitor = CreateVisitor();
            var left = Expression.Constant(2, typeof(object));
            var right = Expression.Constant(2, typeof(object));
            Assert.Throws<InvalidOperationException>(() => visitor.CreateInExpression(left, right));
        }

        [Fact]
        public void ConvertConstantToType_NullAndValueTypes()
        {
            var visitor = CreateVisitor();

            // Null to int (should return Expression.Default(typeof(int)))
            var expr = Expression.Constant(null, typeof(object));
            var result = visitor.ConvertConstantToType(expr, typeof(int));
            Assert.Equal(ExpressionType.Default, result.NodeType);

            // Null to string
            result = visitor.ConvertConstantToType(expr, typeof(string));
            Assert.Equal(ExpressionType.Constant, result.NodeType);
            Assert.Null(((ConstantExpression)result).Value);

            // Already correct type
            expr = Expression.Constant(42, typeof(object));
            result = visitor.ConvertConstantToType(expr, typeof(int));
            Assert.Equal(42, ((ConstantExpression)result).Value);

            // String to int
            expr = Expression.Constant("123", typeof(object));
            result = visitor.ConvertConstantToType(expr, typeof(int));
            Assert.Equal(123, ((ConstantExpression)result).Value);

            // Invalid conversion throws
            expr = Expression.Constant("abc", typeof(object));
            Assert.Throws<ArgumentException>(() => visitor.ConvertConstantToType(expr, typeof(int)));
        }

        [Theory]
        [InlineData(typeof(int), true)]
        [InlineData(typeof(double), true)]
        [InlineData(typeof(decimal), true)]
        [InlineData(typeof(string), false)]
        [InlineData(typeof(bool), false)]
        public void IsNumericType_Works(Type type, bool expected)
        {
            var visitor = CreateVisitor();
            Assert.Equal(expected, visitor.IsNumericType(type));
        }

        [Fact]
        public void HandleAggregate_Throws()
        {
            var visitor = CreateVisitor();
            Assert.Throws<NotSupportedException>(() => visitor.HandleAggregate("SUM", new Expression[0]));
        }

        [Fact]
        public void HandleIif_ThrowsOnWrongArgCount()
        {
            var visitor = CreateVisitor();
            Assert.Throws<ArgumentException>(() => visitor.HandleIif(new Expression[2]));
        }

        [Fact]
        public void HandleIif_ThrowsOnIncompatibleTypes()
        {
            var visitor = CreateVisitor();
            var cond = Expression.Constant(true, typeof(bool));
            var left = Expression.Constant(1, typeof(int));
            var right = Expression.Constant("str", typeof(string));
            Assert.Throws<InvalidOperationException>(() => visitor.HandleIif(new[] { cond, left, right }));
        }

        [Fact]
        public void HandleIif_Works()
        {
            var visitor = CreateVisitor();
            var cond = Expression.Constant(true, typeof(bool));
            var left = Expression.Constant(1, typeof(int));
            var right = Expression.Constant(2, typeof(int));
            var expr = visitor.HandleIif(new[] { cond, left, right });
            var lambda = Expression.Lambda<Func<int>>(expr).Compile();
            Assert.Equal(1, lambda());
        }

        [Fact]
        public void CreateInExpression_ThrowsIfRightIsNotList()
        {
            var visitor = CreateVisitor();
            var left = Expression.Constant(1, typeof(object));
            var right = Expression.Constant(1, typeof(object));
            Assert.Throws<InvalidOperationException>(() => visitor.CreateInExpression(left, right));
        }

        [Fact]
        public void CreateInExpression_Works()
        {
            var visitor = CreateVisitor();
            var left = Expression.Constant(2, typeof(object));
            var right = Expression.Constant(new List<object> { 1, 2, 3 }, typeof(List<object>));
            var expr = visitor.CreateInExpression(left, right);
            var lambda = Expression.Lambda<Func<bool>>(expr).Compile();
            Assert.True(lambda());
        }

        [Fact]
        public void HandleLen_WorksForStringAndNonString()
        {
            var visitor = CreateVisitor();
            var strExpr = Expression.Constant("abc", typeof(string));
            var expr = visitor.HandleLen(new[] { strExpr });
            var lambda = Expression.Lambda<Func<int>>(expr).Compile();
            Assert.Equal(3, lambda());

            var objExpr = Expression.Constant(123, typeof(object));
            expr = visitor.HandleLen(new[] { objExpr });
            lambda = Expression.Lambda<Func<int>>(expr).Compile();
            Assert.Equal(3, lambda()); // "123".Length == 3
        }

        [Fact]
        public void HandleLen_ThrowsOnWrongArgCount()
        {
            var visitor = CreateVisitor();
            Assert.Throws<ArgumentException>(() => visitor.HandleLen(new Expression[0]));
        }

        [Fact]
        public void HandleConvert_WorksForNull()
        {
            var visitor = CreateVisitor();
            var expr = visitor.HandleConvert(new Expression[]
            {
                Expression.Constant(null, typeof(object)),
                Expression.Constant("System.Int32", typeof(string))
            });
            var lambda = Expression.Lambda<Func<int>>(expr).Compile();
            Assert.Equal(0, lambda());
        }

        [Fact]
        public void HandleConvert_ThrowsOnWrongArgCount()
        {
            var visitor = CreateVisitor();
            Assert.Throws<ArgumentException>(() => visitor.HandleConvert(new Expression[1]));
        }

        [Fact]
        public void HandleConvert_ThrowsOnNonStringTypeName()
        {
            var visitor = CreateVisitor();
            Assert.Throws<ArgumentException>(() => visitor.HandleConvert(new Expression[]
            {
                Expression.Constant(1, typeof(object)),
                Expression.Constant(1, typeof(object))
            }));
        }

        [Fact]
        public void HandleTrim_Works()
        {
            var visitor = CreateVisitor();
            var expr = visitor.HandleTrim(new[] { Expression.Constant("  abc  ", typeof(string)) });
            var lambda = Expression.Lambda<Func<string>>(expr).Compile();
            Assert.Equal("abc", lambda());
        }

        [Fact]
        public void HandleTrim_ThrowsOnWrongArgCount()
        {
            var visitor = CreateVisitor();
            Assert.Throws<ArgumentException>(() => visitor.HandleTrim(new Expression[0]));
        }


        [Fact]
        public void HandleSubstring_Works()
        {
            var visitor = CreateVisitor();
            var expr = visitor.HandleSubstring(new[]
            {
                Expression.Constant("abcdef", typeof(string)),
                Expression.Constant(2, typeof(int?)),
                Expression.Constant(3, typeof(int?))
            });
            var lambda = Expression.Lambda<Func<string>>(expr).Compile();
            Assert.Equal("bcd", lambda());
        }

        [Fact]
        public void HandleSubstring_ThrowsOnWrongArgCount()
        {
            var visitor = CreateVisitor();
            Assert.Throws<ArgumentException>(() => visitor.HandleSubstring(new Expression[2]));
        }


        [Fact]
        public void HandleIsNull_Works()
        {
            var visitor = CreateVisitor();
            var expr = visitor.HandleIsNull(new[]
            {
                Expression.Constant(null, typeof(object)),
                Expression.Constant("fallback", typeof(string))
            });
            var lambda = Expression.Lambda<Func<string>>(expr).Compile();
            Assert.Equal("fallback", lambda());
        }

        [Fact]
        public void HandleIsNull_ThrowsOnWrongArgCount()
        {
            var visitor = CreateVisitor();
            Assert.Throws<ArgumentException>(() => visitor.HandleIsNull(new Expression[1]));
        }

        [Fact]
        public void VisitPrimaryExpression_ThrowsOnInvalid()
        {
            var visitor = CreateVisitor();
            var mockContext = new MockPrimaryExpressionContext();
            Assert.Throws<InvalidOperationException>(() => visitor.VisitPrimaryExpression(mockContext));
        }


        [Fact]
        public void ApplyAdditiveOperator_ThrowsOnUnknown()
        {
            var visitor = CreateVisitor();
            var left = Expression.Constant(1.0, typeof(double));
            var right = Expression.Constant(2.0, typeof(double));
            var fakeToken = Substitute.For<IToken>();
            fakeToken.Type.Returns(9999);
            Assert.Throws<NotSupportedException>(() => visitor.ApplyAdditiveOperator(left, right, fakeToken));
        }
    }
}