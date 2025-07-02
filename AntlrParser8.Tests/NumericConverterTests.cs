using System.Globalization;
using Xunit;

namespace AntlrParser8.Tests;

public class NumericConverterTests
{
    [Theory]
    // ToDouble
    [InlineData(null, 0.0)]
    [InlineData(42, 42.0)]
    [InlineData(42.5, 42.5)]
    [InlineData("123.4", 123.4)]
    public void ToDouble_ShouldConvert(object value, double expected)
    {
        Assert.Equal(expected, NumericConverter.ToDouble(value));
    }

    [Theory]
    // ToDecimal
    [InlineData(null, 0.0)]
    [InlineData(42, 42.0)]
    [InlineData(42.5, 42.5)]
    [InlineData("123.4", 123.4)]
    public void ToDecimal_ShouldConvert(object value, decimal expected)
    {
        Assert.Equal(expected, NumericConverter.ToDecimal(value));
    }

    [Fact]
    public void ConvertToBestType_ShouldReturnDefaultForNullValueType()
    {
        Assert.Equal(0, NumericConverter.ConvertToBestType(null, typeof(int)));
        Assert.Equal(0.0, NumericConverter.ConvertToBestType(null, typeof(double)));
        Assert.Equal(0M, NumericConverter.ConvertToBestType(null, typeof(decimal)));
        Assert.Equal(false, NumericConverter.ConvertToBestType(null, typeof(bool)));
        Assert.Equal(DateTime.MinValue, NumericConverter.ConvertToBestType(null, typeof(DateTime)));
    }

    [Fact]
    public void ConvertToBestType_ShouldReturnNullForNullReferenceType()
    {
        Assert.Null(NumericConverter.ConvertToBestType(null, typeof(string)));
        Assert.Null(NumericConverter.ConvertToBestType(null, typeof(object)));
    }

    [Theory]
    [InlineData(42, typeof(int), 42)]
    [InlineData(42.5, typeof(double), 42.5)]
    [InlineData(42.5, typeof(float), 42.5f)]
    [InlineData(42, typeof(long), 42L)]
    [InlineData(42, typeof(decimal), 42.0)]
    [InlineData(true, typeof(bool), true)]
    [InlineData("true", typeof(bool), true)]
    [InlineData("false", typeof(bool), false)]
    [InlineData("1", typeof(bool), true)]
    [InlineData("0", typeof(bool), false)]
    [InlineData("123", typeof(short), (short)123)]
    [InlineData("456", typeof(int), 456)]
    [InlineData("789", typeof(long), 789L)]
    [InlineData("12.34", typeof(decimal), 12.34)]
    [InlineData("12.34", typeof(float), 12.34f)]
    [InlineData("12.34", typeof(double), 12.34)]
    [InlineData("2024-07-01", typeof(DateTime), "2024-07-01")]
    [InlineData("2024-07-01T12:34:56", typeof(DateTime), "2024-07-01T12:34:56")]
    public void ConvertToBestType_ShouldConvertStrings(object value, Type targetType, object expected)
    {
        if (targetType == typeof(DateTime))
        {
            var dt = (DateTime)NumericConverter.ConvertToBestType(value, targetType);
            Assert.Equal(DateTime.Parse((string)value, CultureInfo.InvariantCulture, DateTimeStyles.None), dt);
        }
        else if (targetType == typeof(float))
        {
            Assert.Equal((float)Convert.ChangeType(expected, typeof(float), CultureInfo.InvariantCulture),
                (float)NumericConverter.ConvertToBestType(value, targetType), 3);
        }
        else if (targetType == typeof(decimal))
        {
            Assert.Equal(Convert.ToDecimal(expected, CultureInfo.InvariantCulture),
                (decimal)NumericConverter.ConvertToBestType(value, targetType));
        }
        else if (targetType == typeof(double))
        {
            Assert.Equal(Convert.ToDouble(expected, CultureInfo.InvariantCulture),
                (double)NumericConverter.ConvertToBestType(value, targetType), 3);
        }
        else
        {
            Assert.Equal(expected, NumericConverter.ConvertToBestType(value, targetType));
        }
    }

    [Theory]
    [InlineData("notanint", typeof(int))]
    [InlineData("notadouble", typeof(double))]
    [InlineData("notafloat", typeof(float))]
    [InlineData("notashort", typeof(short))]
    [InlineData("notalong", typeof(long))]
    [InlineData("notadecimal", typeof(decimal))]
    [InlineData("notabool", typeof(bool))]
    [InlineData("notadate", typeof(DateTime))]
    public void ConvertToBestType_ShouldThrowOnInvalidString(string value, Type targetType)
    {
        Assert.Throws<ArgumentException>(() => NumericConverter.ConvertToBestType(value, targetType));
    }

    [Fact]
    public void ConvertToBestType_ShouldThrowOnInvalidType()
    {
        Assert.Throws<ArgumentException>(() => NumericConverter.ConvertToBestType(123, typeof(Guid)));
    }

    [Fact]
    public void ConvertToBestType_ShouldConvertStringToDateTime()
    {
        var dt = (DateTime)NumericConverter.ConvertToBestType("2025-07-02", typeof(DateTime));
        Assert.Equal(new DateTime(2025, 7, 2), dt);
    }

    [Fact]
    public void ConvertToBestType_ShouldConvertStringToBool()
    {
        Assert.True((bool)NumericConverter.ConvertToBestType("true", typeof(bool)));
        Assert.False((bool)NumericConverter.ConvertToBestType("false", typeof(bool)));
        Assert.True((bool)NumericConverter.ConvertToBestType("1", typeof(bool)));
        Assert.False((bool)NumericConverter.ConvertToBestType("0", typeof(bool)));
    }
}