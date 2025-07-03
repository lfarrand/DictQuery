using AntlrParser8.Data;
using Xunit;

public class DictionaryDataReaderTests
{
    private List<Dictionary<string, object>> GetExtraSampleData()
    {
        return new List<Dictionary<string, object>>
        {
            new()
            {
                ["ByteCol"] = (byte)42,
                ["CharCol"] = 'Z',
                ["DecimalCol"] = 123.45m,
                ["FloatCol"] = 3.14f,
                ["Int16Col"] = (short)1234,
                ["Int64Col"] = 123456789012345L,
                ["StringCol"] = "Hello"
            }
        };
    }

    private List<Dictionary<string, object>> GetSampleData()
    {
        return new List<Dictionary<string, object>>
        {
            new()
            {
                ["Id"] = 1,
                ["Name"] = "Alice",
                ["Age"] = 30,
                ["Salary"] = 12345.67,
                ["Active"] = true,
                ["JoinDate"] = new DateTime(2020, 1, 1),
                ["Guid"] = Guid.Parse("11111111-1111-1111-1111-111111111111"),
                ["NullField"] = null
            },
            new()
            {
                ["Id"] = 2,
                ["Name"] = null, // null value
                ["Age"] = 25,
                ["Salary"] = 7654.32,
                ["Active"] = false,
                ["JoinDate"] = new DateTime(2021, 6, 15),
                ["Guid"] = Guid.Parse("22222222-2222-2222-2222-222222222222")
                // "NullField" missing
            }
        };
    }

    [Fact]
    public void Construction_SetsSchema_FromFirstRow()
    {
        var data = GetSampleData();
        using var reader = new DictionaryDataReader(data);

        Assert.Equal(8, reader.FieldCount);
        Assert.Equal("Id", reader.GetName(0));
        Assert.Equal(0, reader.GetOrdinal("Id"));
        Assert.Equal("Name", reader.GetName(1));
        Assert.Equal(1, reader.GetOrdinal("Name"));
        Assert.Throws<KeyNotFoundException>(() => reader.GetOrdinal("NotAField"));
    }

    [Fact]
    public void Read_ReturnsRows_And_Fields_AreAccessible()
    {
        var data = GetSampleData();
        using var reader = new DictionaryDataReader(data);

        Assert.True(reader.Read());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.Equal("Alice", reader.GetString(1));
        Assert.Equal(30, reader.GetInt32(2));
        Assert.Equal(12345.67, reader.GetDouble(3));
        Assert.True(reader.GetBoolean(4));
        Assert.Equal(new DateTime(2020, 1, 1), reader.GetDateTime(5));
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), reader.GetGuid(6));

        Assert.True(reader.Read());
        Assert.Equal(2, reader.GetInt32(0));
        Assert.Throws<InvalidCastException>(() => reader.GetString(1)); // Name is null
        Assert.Equal(25, reader.GetInt32(2));
        Assert.Equal(7654.32, reader.GetDouble(3));
        Assert.False(reader.GetBoolean(4));
        Assert.Equal(new DateTime(2021, 6, 15), reader.GetDateTime(5));
        Assert.Equal(Guid.Parse("22222222-2222-2222-2222-222222222222"), reader.GetGuid(6));

        Assert.False(reader.Read());
    }

    [Fact]
    public void Indexers_ByName_And_ByIndex()
    {
        var data = GetSampleData();
        using var reader = new DictionaryDataReader(data);

        Assert.True(reader.Read());
        Assert.Equal("Alice", reader["Name"]);
        Assert.Equal("Alice", reader[1]);
        Assert.Equal(1, reader["Id"]);
        Assert.Equal(1, reader[0]);
    }

    [Fact]
    public void GetValue_ReturnsDBNull_ForNulls_And_MissingKeys()
    {
        var data = GetSampleData();
        using var reader = new DictionaryDataReader(data);

        Assert.True(reader.Read());
        Assert.Equal(DBNull.Value, reader.GetValue(7)); // NullField is null

        Assert.True(reader.Read());
        Assert.Equal(DBNull.Value, reader.GetValue(7)); // NullField is missing in this row
    }

    [Fact]
    public void IsDBNull_ReturnsTrue_ForNulls_And_MissingKeys()
    {
        var data = GetSampleData();
        using var reader = new DictionaryDataReader(data);

        Assert.True(reader.Read());
        Assert.True(reader.IsDBNull(7)); // NullField is null

        Assert.True(reader.Read());
        Assert.True(reader.IsDBNull(7)); // NullField is missing in this row
    }

    [Fact]
    public void GetValues_FillsArray()
    {
        var data = GetSampleData();
        using var reader = new DictionaryDataReader(data);

        Assert.True(reader.Read());
        var arr = new object[10];
        var count = reader.GetValues(arr);
        Assert.Equal(reader.FieldCount, count);
        Assert.Equal(1, arr[0]);
        Assert.Equal("Alice", arr[1]);
        Assert.Equal(DBNull.Value, arr[7]);
    }

    [Fact]
    public void GetFieldType_And_GetDataTypeName()
    {
        var data = GetSampleData();
        using var reader = new DictionaryDataReader(data);

        Assert.True(reader.Read());
        Assert.Equal(typeof(int), reader.GetFieldType(0));
        Assert.Equal("Int32", reader.GetDataTypeName(0));
        Assert.Equal(typeof(string), reader.GetFieldType(1));
        Assert.Equal("String", reader.GetDataTypeName(1));
        Assert.Equal(typeof(Guid), reader.GetFieldType(6));
        Assert.Equal("Guid", reader.GetDataTypeName(6));
    }

    [Fact]
    public void Throws_On_EmptySource()
    {
        Assert.Throws<InvalidOperationException>(() =>
            new DictionaryDataReader(new List<Dictionary<string, object>>()));
    }

    [Fact]
    public void Throws_On_OutOfRange_Index()
    {
        var data = GetSampleData();
        using var reader = new DictionaryDataReader(data);

        Assert.True(reader.Read());
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetValue(99));
        Assert.Throws<ArgumentOutOfRangeException>(() => reader.GetName(99));
        Assert.Throws<KeyNotFoundException>(() => reader.GetOrdinal("NotAField"));
    }

    [Fact]
    public void NotSupported_Methods_Throw()
    {
        var data = GetSampleData();
        using var reader = new DictionaryDataReader(data);

        Assert.True(reader.Read());
        Assert.Throws<NotSupportedException>(() => reader.GetBytes(0, 0, null, 0, 0));
        Assert.Throws<NotSupportedException>(() => reader.GetChars(0, 0, null, 0, 0));
        Assert.Throws<NotSupportedException>(() => reader.GetData(0));
        Assert.Null(reader.GetSchemaTable());
        Assert.False(reader.NextResult());
    }

    [Fact]
    public void Depth_And_RecordsAffected()
    {
        var data = GetSampleData();
        using var reader = new DictionaryDataReader(data);
        Assert.Equal(1, reader.Depth);
        Assert.Equal(-1, reader.RecordsAffected);
    }

    [Fact]
    public void Dispose_ClosesEnumerator_And_IsClosed()
    {
        var data = GetSampleData();
        var reader = new DictionaryDataReader(data);
        reader.Dispose();
        Assert.True(reader.IsClosed);
    }

    [Fact]
    public void GetByte_Works()
    {
        using var reader = new DictionaryDataReader(GetExtraSampleData());
        Assert.True(reader.Read());
        Assert.Equal((byte)42, reader.GetByte(0));
        Assert.Equal((byte)42, reader.GetByte(reader.GetOrdinal("ByteCol")));
    }

    [Fact]
    public void GetChar_Works()
    {
        using var reader = new DictionaryDataReader(GetExtraSampleData());
        Assert.True(reader.Read());
        Assert.Equal('Z', reader.GetChar(1));
        Assert.Equal('Z', reader.GetChar(reader.GetOrdinal("CharCol")));
    }

    [Fact]
    public void GetDecimal_Works()
    {
        using var reader = new DictionaryDataReader(GetExtraSampleData());
        Assert.True(reader.Read());
        Assert.Equal(123.45m, reader.GetDecimal(2));
        Assert.Equal(123.45m, reader.GetDecimal(reader.GetOrdinal("DecimalCol")));
    }

    [Fact]
    public void GetFloat_Works()
    {
        using var reader = new DictionaryDataReader(GetExtraSampleData());
        Assert.True(reader.Read());
        Assert.Equal(3.14f, reader.GetFloat(3));
        Assert.Equal(3.14f, reader.GetFloat(reader.GetOrdinal("FloatCol")));
    }

    [Fact]
    public void GetInt16_Works()
    {
        using var reader = new DictionaryDataReader(GetExtraSampleData());
        Assert.True(reader.Read());
        Assert.Equal((short)1234, reader.GetInt16(4));
        Assert.Equal((short)1234, reader.GetInt16(reader.GetOrdinal("Int16Col")));
    }

    [Fact]
    public void GetInt64_Works()
    {
        using var reader = new DictionaryDataReader(GetExtraSampleData());
        Assert.True(reader.Read());
        Assert.Equal(123456789012345L, reader.GetInt64(5));
        Assert.Equal(123456789012345L, reader.GetInt64(reader.GetOrdinal("Int64Col")));
    }

    [Fact]
    public void Close_SetsIsClosed()
    {
        using var reader = new DictionaryDataReader(GetExtraSampleData());
        reader.Close();
        Assert.True(reader.IsClosed);
    }

    [Fact]
    public void GetName_ReturnsCorrectColumnName()
    {
        using var reader = new DictionaryDataReader(GetExtraSampleData());
        for (var i = 0; i < reader.FieldCount; i++)
        {
            var expected =
                new[] { "ByteCol", "CharCol", "DecimalCol", "FloatCol", "Int16Col", "Int64Col", "StringCol" }[i];
            Assert.Equal(expected, reader.GetName(i));
        }
    }
}