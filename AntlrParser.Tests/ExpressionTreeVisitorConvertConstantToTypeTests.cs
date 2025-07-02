namespace AntlrParser.Tests
{
    using System;
    using System.Globalization;
    using System.Linq.Expressions;
    using Xunit;

    public class ExpressionTreeVisitorConvertConstantToTypeTests
    {
        private ExpressionTreeVisitor CreateVisitor()
        {
            var data = new[] { new System.Collections.Generic.Dictionary<string, object>() };
            var parameter = Expression.Parameter(typeof(System.Collections.Generic.Dictionary<string, object>), "row");
            return new ExpressionTreeVisitor(parameter, data);
        }

        [Fact]
        public void NullToReferenceType_ReturnsNullConstant()
        {
            var visitor = CreateVisitor();
            var expr = Expression.Constant(null, typeof(object));
            var result = visitor.ConvertConstantToType(expr, typeof(string));
            Assert.Equal(typeof(string), ((ConstantExpression)result).Type);
            Assert.Null(((ConstantExpression)result).Value);
        }

        [Fact]
        public void NullToValueType_ReturnsDefault()
        {
            var visitor = CreateVisitor();
            var expr = Expression.Constant(null, typeof(object));
            var result = visitor.ConvertConstantToType(expr, typeof(int));
            Assert.Equal(ExpressionType.Default, result.NodeType);
            Assert.Equal(typeof(int), result.Type);
        }

        [Fact]
        public void AlreadyCorrectType_ReturnsSameValue()
        {
            var visitor = CreateVisitor();
            var expr = Expression.Constant(42, typeof(object));
            var result = visitor.ConvertConstantToType(expr, typeof(int));
            Assert.Equal(typeof(int), ((ConstantExpression)result).Type);
            Assert.Equal(42, ((ConstantExpression)result).Value);
        }

        [Fact]
        public void StringToInt_Converts()
        {
            var visitor = CreateVisitor();
            var expr = Expression.Constant("123", typeof(object));
            var result = visitor.ConvertConstantToType(expr, typeof(int));
            Assert.Equal(123, ((ConstantExpression)result).Value);
        }

        [Fact]
        public void IntToString_Converts()
        {
            var visitor = CreateVisitor();
            var expr = Expression.Constant(456, typeof(object));
            var result = visitor.ConvertConstantToType(expr, typeof(string));
            Assert.Equal("456", ((ConstantExpression)result).Value);
        }

        [Fact]
        public void StringToDouble_Converts()
        {
            var visitor = CreateVisitor();
            var expr = Expression.Constant("1.23", typeof(object));
            var result = visitor.ConvertConstantToType(expr, typeof(double));
            Assert.Equal(1.23, ((ConstantExpression)result).Value);
        }

        [Fact]
        public void StringToDecimal_Converts()
        {
            var visitor = CreateVisitor();
            var expr = Expression.Constant("1.23", typeof(object));
            var result = visitor.ConvertConstantToType(expr, typeof(decimal));
            Assert.Equal(1.23m, ((ConstantExpression)result).Value);
        }

        [Fact]
        public void StringToBool_Converts()
        {
            var visitor = CreateVisitor();
            var expr = Expression.Constant("true", typeof(object));
            var result = visitor.ConvertConstantToType(expr, typeof(bool));
            Assert.True((bool)((ConstantExpression)result).Value);
        }

        [Fact]
        public void StringToDateTime_Converts()
        {
            var visitor = CreateVisitor();
            var dt = DateTime.Parse("2024-01-01", CultureInfo.InvariantCulture);
            var expr = Expression.Constant("2024-01-01", typeof(object));
            var result = visitor.ConvertConstantToType(expr, typeof(DateTime));
            Assert.Equal(dt, ((ConstantExpression)result).Value);
        }

        [Fact]
        public void Fallback_UsesChangeType()
        {
            var visitor = CreateVisitor();
            var expr = Expression.Constant((short)1, typeof(object));
            var result = visitor.ConvertConstantToType(expr, typeof(long));
            Assert.Equal(1L, ((ConstantExpression)result).Value);
        }

        [Fact]
        public void InvalidConversion_Throws()
        {
            var visitor = CreateVisitor();
            var expr = Expression.Constant("notanumber", typeof(object));
            Assert.Throws<ArgumentException>(() => visitor.ConvertConstantToType(expr, typeof(int)));
        }

        [Fact]
        public void NonConstantExpression_ReturnsAsIs()
        {
            var visitor = CreateVisitor();
            var expr = Expression.Parameter(typeof(object), "x");
            var result = visitor.ConvertConstantToType(expr, typeof(int));
            Assert.Same(expr, result);
        }
    }
}