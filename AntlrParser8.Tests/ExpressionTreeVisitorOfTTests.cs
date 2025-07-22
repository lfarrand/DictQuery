using System.Linq.Expressions;
using Xunit;

namespace AntlrParser8.Tests;

public class ExpressionTreeVisitorOfTTests
{
    public class TestEmployee
    {
        public string Name { get; set; }
        public int Age { get; set; }
        public decimal Salary { get; set; }
        public string Department { get; set; }
        public DateTime HireDate { get; set; }
        public bool IsActive { get; set; }
        public string[] Projects { get; set; }
        public string Location { get; set; }
    }

    private ExpressionTreeVisitor<TestEmployee> CreateVisitor()
    {
        var parameter = Expression.Parameter(typeof(TestEmployee), "emp");
        return new ExpressionTreeVisitor<TestEmployee>(parameter);
    }

    [Fact]
    public void BuildPropertyAccess_ShouldCreateCorrectPropertyExpression()
    {
        var visitor = CreateVisitor();
        var parameter = Expression.Parameter(typeof(TestEmployee), "emp");
        
        var nameAccess = visitor.BuildPropertyAccess(parameter, "Name");
        var ageAccess = visitor.BuildPropertyAccess(parameter, "Age");
        
        Assert.Equal(typeof(string), nameAccess.Type);
        Assert.Equal(typeof(int), ageAccess.Type);
        Assert.Equal("Name", ((MemberExpression)nameAccess).Member.Name);
        Assert.Equal("Age", ((MemberExpression)ageAccess).Member.Name);
    }

    [Fact]
    public void BuildPropertyAccess_WithInvalidProperty_ShouldThrow()
    {
        var visitor = CreateVisitor();
        var parameter = Expression.Parameter(typeof(TestEmployee), "emp");
        
        Assert.Throws<ArgumentException>(() => visitor.BuildPropertyAccess(parameter, "InvalidProperty"));
    }

    [Fact]
    public void EnsureBoolean_WithBooleanExpression_ShouldReturnSame()
    {
        var visitor = CreateVisitor();
        var boolExpr = Expression.Constant(true);
        
        var result = visitor.EnsureBoolean(boolExpr);
        
        Assert.Same(boolExpr, result);
    }

    [Fact]
    public void EnsureBoolean_WithNonBooleanExpression_ShouldWrapWithNotEquals()
    {
        var visitor = CreateVisitor();
        var intExpr = Expression.Constant(5);
        
        var result = visitor.EnsureBoolean(intExpr);
        
        Assert.Equal(typeof(bool), result.Type);
        Assert.IsAssignableFrom<BinaryExpression>(result);
        var binary = (BinaryExpression)result;
        Assert.Equal(ExpressionType.NotEqual, binary.NodeType);
    }

    [Fact]
    public void HandleLen_WithStringProperty_ShouldReturnLengthExpression()
    {
        var visitor = CreateVisitor();
        var parameter = Expression.Parameter(typeof(TestEmployee), "emp");
        var nameAccess = visitor.BuildPropertyAccess(parameter, "Name");
        
        var lengthExpr = visitor.HandleLen(new[] { nameAccess });
        
        Assert.Equal(typeof(int), lengthExpr.Type);
        var lambda = Expression.Lambda<Func<TestEmployee, int>>(lengthExpr, parameter).Compile();
        var testEmployee = new TestEmployee { Name = "John Doe" };
        Assert.Equal(8, lambda(testEmployee));
    }

    [Fact]
    public void HandleLen_WithNullString_ShouldReturnZero()
    {
        var visitor = CreateVisitor();
        var parameter = Expression.Parameter(typeof(TestEmployee), "emp");
        var nameAccess = visitor.BuildPropertyAccess(parameter, "Name");
        
        var lengthExpr = visitor.HandleLen(new[] { nameAccess });
        var lambda = Expression.Lambda<Func<TestEmployee, int>>(lengthExpr, parameter).Compile();
        var testEmployee = new TestEmployee { Name = null };
        
        Assert.Equal(0, lambda(testEmployee));
    }

    [Fact]
    public void HandleIsNull_WithNullProperty_ShouldReturnTrue()
    {
        var visitor = CreateVisitor();
        var parameter = Expression.Parameter(typeof(TestEmployee), "emp");
        var propertyExpr = visitor.BuildPropertyAccess(parameter, "Name");
        var isNullExpr = Expression.Equal(propertyExpr, Expression.Constant(null, typeof(object)));
        var lambda = Expression.Lambda<Func<TestEmployee, bool>>(isNullExpr, parameter).Compile();
        var testEmployee = new TestEmployee { Name = null };
        
        Assert.True(lambda(testEmployee));
    }

    [Fact]
    public void HandleIsNull_WithNonNullProperty_ShouldReturnFalse()
    {
        var visitor = CreateVisitor();
        var parameter = Expression.Parameter(typeof(TestEmployee), "emp");
        var propertyExpr = visitor.BuildPropertyAccess(parameter, "Name");
        var isNullExpr = Expression.Equal(propertyExpr, Expression.Constant(null, typeof(object)));
        var lambda = Expression.Lambda<Func<TestEmployee, bool>>(isNullExpr, parameter).Compile();
        var testEmployee = new TestEmployee { Name = "John" };
        
        Assert.False(lambda(testEmployee));
    }

    [Fact]
    public void HandleIif_WithCondition_ShouldReturnCorrectResult()
    {
        var visitor = CreateVisitor();
        var parameter = Expression.Parameter(typeof(TestEmployee), "emp");
        var isActiveAccess = visitor.BuildPropertyAccess(parameter, "IsActive");
        var trueBranch = Expression.Constant("Active");
        var falseBranch = Expression.Constant("Inactive");
        
        var iifExpr = visitor.HandleIif(new[] { isActiveAccess, trueBranch, falseBranch });
        var lambda = Expression.Lambda<Func<TestEmployee, string>>(iifExpr, parameter).Compile();
        
        Assert.Equal("Active", lambda(new TestEmployee { IsActive = true }));
        Assert.Equal("Inactive", lambda(new TestEmployee { IsActive = false }));
    }

    [Fact]
    public void HandleTrim_WithStringProperty_ShouldTrimWhitespace()
    {
        var visitor = CreateVisitor();
        var parameter = Expression.Parameter(typeof(TestEmployee), "emp");
        var nameAccess = visitor.BuildPropertyAccess(parameter, "Name");
        
        var trimExpr = visitor.HandleTrim(new[] { nameAccess });
        var lambda = Expression.Lambda<Func<TestEmployee, string>>(trimExpr, parameter).Compile();
        var testEmployee = new TestEmployee { Name = "  John  " };
        
        Assert.Equal("John", lambda(testEmployee));
    }

    [Theory]
    [InlineData(1,3, "Joh")]
    [InlineData(2,3, "ohn")]
    [InlineData(1,8, "John Doe")]
    public void HandleSubstring_WithValidParameters_ShouldReturnSubstring(int startIndex, int length, string expected)
    {
        var visitor = CreateVisitor();
        var parameter = Expression.Parameter(typeof(TestEmployee), "emp");
        var nameAccess = visitor.BuildPropertyAccess(parameter, "Name");
    
        // Use nullable int constants if your method expects Nullable<int>
        var startIndexExpr = Expression.Constant((int?)startIndex, typeof(int?));
        var lengthExpr = Expression.Constant((int?)length, typeof(int?));

        var substringExpr = visitor.HandleSubstring(new[] { nameAccess, startIndexExpr, lengthExpr });
        var lambda = Expression.Lambda<Func<TestEmployee, string>>(substringExpr, parameter).Compile();
        var testEmployee = new TestEmployee { Name = "John Doe" };

        Assert.Equal(expected, lambda(testEmployee));
    }

    [Fact]
    public void ApplyAdditiveOperator_WithAddition_ShouldAddNumbers()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(5);
        var right = Expression.Constant(3);
        var mockToken = new MockToken("+", ModelExpressionParser.PLUS);
        
        var result = visitor.ApplyAdditiveOperator(left, right, mockToken);
        var lambda = Expression.Lambda<Func<int>>(Expression.Convert(result, typeof(int))).Compile();
        
        Assert.Equal(8, lambda());
    }

    [Fact]
    public void ApplyAdditiveOperator_WithSubtraction_ShouldSubtractNumbers()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(10);
        var right = Expression.Constant(3);
        var mockToken = new MockToken("-", ModelExpressionParser.MINUS);
        
        var result = visitor.ApplyAdditiveOperator(left, right, mockToken);
        var lambda = Expression.Lambda<Func<int>>(Expression.Convert(result, typeof(int))).Compile();
        
        Assert.Equal(7, lambda());
    }

    [Fact]
    public void ApplyMultiplicativeOperator_WithMultiplication_ShouldMultiplyNumbers()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(5);
        var right = Expression.Constant(3);
        var mockToken = new MockToken("*", ModelExpressionParser.MULTIPLY);
        
        var result = visitor.ApplyMultiplicativeOperator(left, right, mockToken);
        var lambda = Expression.Lambda<Func<int>>(Expression.Convert(result, typeof(int))).Compile();
        
        Assert.Equal(15, lambda());
    }

    [Fact]
    public void ApplyMultiplicativeOperator_WithDivision_ShouldDivideNumbers()
    {
        var visitor = CreateVisitor();
        var left = Expression.Constant(15.0);
        var right = Expression.Constant(3.0);
        var mockToken = new MockToken("/", ModelExpressionParser.DIVIDE);
        
        var result = visitor.ApplyMultiplicativeOperator(left, right, mockToken);
        var lambda = Expression.Lambda<Func<double>>(result).Compile();
        
        Assert.Equal(5.0, lambda());
    }

    [Fact]
    public void IsNumericType_WithNumericTypes_ShouldReturnTrue()
    {
        var visitor = CreateVisitor();
        
        Assert.True(visitor.IsNumericType(typeof(int)));
        Assert.True(visitor.IsNumericType(typeof(decimal)));
        Assert.True(visitor.IsNumericType(typeof(double)));
        Assert.True(visitor.IsNumericType(typeof(float)));
        Assert.True(visitor.IsNumericType(typeof(long)));
    }

    [Fact]
    public void IsNumericType_WithNonNumericTypes_ShouldReturnFalse()
    {
        var visitor = CreateVisitor();
        
        Assert.False(visitor.IsNumericType(typeof(string)));
        Assert.False(visitor.IsNumericType(typeof(bool)));
        Assert.False(visitor.IsNumericType(typeof(DateTime)));
    }

    [Fact]
    public void GetWiderNumericType_WithCompatibleTypes_ShouldReturnWiderType()
    {
        var visitor = CreateVisitor();
        
        Assert.Equal(typeof(decimal), visitor.GetWiderNumericType(typeof(int), typeof(decimal)));
        Assert.Equal(typeof(double), visitor.GetWiderNumericType(typeof(float), typeof(double)));
        Assert.Equal(typeof(long), visitor.GetWiderNumericType(typeof(int), typeof(long)));
    }

    [Fact]
    public void ConvertToNumeric_WithNumericExpression_ShouldConvert()
    {
        var visitor = CreateVisitor();
        var intExpr = Expression.Constant(5);
        
        var result = visitor.ConvertToNumeric(intExpr);
        
        Assert.Equal(typeof(int), result.Type);
    }

    private class MockToken : Antlr4.Runtime.IToken
    {
        public string Text { get; }
        public int Type { get; set; }
        public int Line { get; set; }
        public int Column { get; set; }
        public int Channel { get; set; }
        public int TokenIndex { get; set; }
        public int StartIndex { get; set; }
        public int StopIndex { get; set; }
        public Antlr4.Runtime.ITokenSource TokenSource { get; set; }
        public Antlr4.Runtime.ICharStream InputStream { get; set; }

        public MockToken(string text, int type)
        {
            Text = text;
            Type = type;
        }
    }
}