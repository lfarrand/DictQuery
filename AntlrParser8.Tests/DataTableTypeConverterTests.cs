using Xunit;

namespace AntlrParser8.Tests;

public class DataTableTypeConverterTests
{
    [Theory]
    [InlineData(null, null, true)]
    [InlineData("foo", "foo", true)]
    [InlineData("foo", "FOO", true)]
    [InlineData("foo", "bar", false)]
    [InlineData(1, 1, true)]
    [InlineData(1, 1.0, true)]
    [InlineData(1, 2, false)]
    [InlineData(1.0, 1.0, true)]
    [InlineData(1.0, 2.0, false)]
    [InlineData("1", 1, true)] // fallback: string compare
    [InlineData("foo", 1, false)]
    [InlineData(1, "foo", false)]
    public void AreEqual_CoversAllCases(object left, object right, bool expected)
    {
        Assert.Equal(expected, DataTableTypeConverter.AreEqual(left, right));
    }

    [Theory]
    [InlineData(null, null, false)]
    [InlineData("foo", "foo", false)]
    [InlineData("foo", "bar", true)]
    [InlineData(1, 1, false)]
    [InlineData(1, 2, true)]
    [InlineData(1.0, 2.0, true)]
    [InlineData("1", 1, false)] // fallback: string compare
    [InlineData("foo", 1, true)]
    public void AreNotEqual_CoversAllCases(object left, object right, bool expected)
    {
        Assert.Equal(expected, DataTableTypeConverter.AreNotEqual(left, right));
    }

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(2, 1, false)]
    [InlineData(2, 2, false)]
    [InlineData(1.0, 2.0, true)]
    [InlineData(2.0, 1.0, false)]
    [InlineData(2.0, 2.0, false)]
    [InlineData("a", "b", true)]
    [InlineData("b", "a", false)]
    [InlineData("a", "a", false)]
    [InlineData(null, 1, true)]
    [InlineData(1, null, false)]
    [InlineData(null, null, false)]
    public void IsLessThan_CoversAllCases(object left, object right, bool expected)
    {
        Assert.Equal(expected, DataTableTypeConverter.IsLessThan(left, right));
    }

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 2, false)]
    [InlineData("b", "a", true)]
    [InlineData("a", "b", false)]
    [InlineData("a", "a", false)]
    [InlineData(1, null, true)]
    [InlineData(null, 1, false)]
    [InlineData(null, null, false)]
    public void IsGreaterThan_CoversAllCases(object left, object right, bool expected)
    {
        Assert.Equal(expected, DataTableTypeConverter.IsGreaterThan(left, right));
    }

    [Theory]
    [InlineData(1, 2, true)]
    [InlineData(2, 1, false)]
    [InlineData(2, 2, true)]
    [InlineData("a", "b", true)]
    [InlineData("b", "a", false)]
    [InlineData("a", "a", true)]
    [InlineData(null, 1, true)]
    [InlineData(1, null, false)]
    [InlineData(null, null, true)]
    public void IsLessThanOrEqual_CoversAllCases(object left, object right, bool expected)
    {
        Assert.Equal(expected, DataTableTypeConverter.IsLessThanOrEqual(left, right));
    }

    [Theory]
    [InlineData(2, 1, true)]
    [InlineData(1, 2, false)]
    [InlineData(2, 2, true)]
    [InlineData("b", "a", true)]
    [InlineData("a", "b", false)]
    [InlineData("a", "a", true)]
    [InlineData(1, null, true)]
    [InlineData(null, 1, false)]
    [InlineData(null, null, true)]
    public void IsGreaterThanOrEqual_CoversAllCases(object left, object right, bool expected)
    {
        Assert.Equal(expected, DataTableTypeConverter.IsGreaterThanOrEqual(left, right));
    }

    [Fact]
    public void CompareValues_Numeric()
    {
        Assert.True(DataTableTypeConverter.CompareValues(1, 2) < 0);
        Assert.True(DataTableTypeConverter.CompareValues(2, 1) > 0);
        Assert.True(DataTableTypeConverter.CompareValues(2, 2) == 0);
    }

    [Fact]
    public void CompareValues_DateTime()
    {
        var now = DateTime.Now;
        var later = now.AddMinutes(1);
        Assert.True(DataTableTypeConverter.CompareValues(now, later) < 0);
        Assert.True(DataTableTypeConverter.CompareValues(later, now) > 0);
        Assert.True(DataTableTypeConverter.CompareValues(now, now) == 0);
    }

    [Fact]
    public void CompareValues_StringFallback()
    {
        Assert.True(DataTableTypeConverter.CompareValues("abc", "def") < 0);
        Assert.True(DataTableTypeConverter.CompareValues("def", "abc") > 0);
        Assert.True(DataTableTypeConverter.CompareValues("abc", "abc") == 0);
    }

    [Fact]
    public void CompareValues_NullCases()
    {
        Assert.Equal(0, DataTableTypeConverter.CompareValues(null, null));
        Assert.True(DataTableTypeConverter.CompareValues(null, 1) < 0);
        Assert.True(DataTableTypeConverter.CompareValues(1, null) > 0);
    }

    [Fact]
    public void AreEqual_StringVsNumericFallback()
    {
        // fallback: string compare, "1" == "1"
        Assert.True(DataTableTypeConverter.AreEqual("1", 1));
        Assert.False(DataTableTypeConverter.AreEqual("1", 2));
    }

    [Fact]
    public void NumericComparison_UsesDouble()
    {
        Assert.True(DataTableTypeConverter.AreEqual(1.0f, 1.0));
        Assert.True(DataTableTypeConverter.AreEqual((short)1, 1L));
        Assert.True(DataTableTypeConverter.AreEqual((byte)1, (sbyte)1));
        Assert.True(DataTableTypeConverter.IsLessThan((short)1, 2L));
    }

    [Fact]
    public void BooleanRelationalOperators_Throw()
    {
        Assert.Throws<InvalidOperationException>(() => DataTableTypeConverter.IsLessThan(true, false));
        Assert.Throws<InvalidOperationException>(() => DataTableTypeConverter.IsGreaterThan(true, false));
        Assert.Throws<InvalidOperationException>(() => DataTableTypeConverter.IsLessThanOrEqual(true, false));
        Assert.Throws<InvalidOperationException>(() => DataTableTypeConverter.IsGreaterThanOrEqual(true, false));
    }

    [Fact]
    public void IsNumeric_CoversAllTypes()
    {
        Assert.True(InvokeIsNumeric(1));
        Assert.True(InvokeIsNumeric(1L));
        Assert.True(InvokeIsNumeric(1.0f));
        Assert.True(InvokeIsNumeric(1.0));
        Assert.True(InvokeIsNumeric(1.0m));
        Assert.True(InvokeIsNumeric((short)1));
        Assert.True(InvokeIsNumeric((ushort)1));
        Assert.True(InvokeIsNumeric((byte)1));
        Assert.True(InvokeIsNumeric((sbyte)1));
        Assert.False(InvokeIsNumeric("1"));
        Assert.False(InvokeIsNumeric(true));
        Assert.False(InvokeIsNumeric(null));
    }

    // Helper to call private IsNumeric via reflection
    private static bool InvokeIsNumeric(object value)
    {
        var method = typeof(DataTableTypeConverter).GetMethod("IsNumeric",
            System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic);
        return (bool)method.Invoke(null, new object[] { value });
    }
}