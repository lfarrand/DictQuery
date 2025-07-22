namespace AntlrParser8.Data;

using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;

public class DictionaryDataReader : IDataReader
{
    /*
using (var reader = new DictionaryDataReader(yourEnumerableOfDictionaries))
using (var bulkCopy = new SqlBulkCopy(yourConnectionString))
{
    bulkCopy.DestinationTableName = "YourTable";
    // Optionally map columns if names differ
    // bulkCopy.ColumnMappings.Add("SourceColumn", "DestColumn");

    bulkCopy.BatchSize = 10000; // Tune as needed
    bulkCopy.BulkCopyTimeout = 0; // Unlimited
    bulkCopy.WriteToServer(reader);
}
     */

    private IEnumerator<IDictionary<string, object>> _enumerator;
    private readonly List<string> _fieldNames;
    private readonly IDictionary<string, int> _nameToIndex;
    private IDictionary<string, object> _current;

    private bool _firstRecordBuffered = false;
    private readonly IDictionary<string, object> _bufferedFirstRecord;

    public DictionaryDataReader(IEnumerable<IDictionary<string, object>> source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        _enumerator = source.GetEnumerator();
        if (!_enumerator.MoveNext())
        {
            throw new InvalidOperationException("Source sequence is empty.");
        }

        _bufferedFirstRecord = _enumerator.Current;
        _fieldNames = _bufferedFirstRecord.Keys.ToList();
        _nameToIndex = _fieldNames.Select((name, idx) => new { name, idx })
            .ToDictionary(x => x.name, x => x.idx);
        _firstRecordBuffered = true;
    }

    public int FieldCount => _fieldNames.Count;
    public object this[int i] => GetValue(i);
    public object this[string name] => _current.TryGetValue(name, out var value) ? value ?? DBNull.Value : DBNull.Value;

    public string GetName(int i)
    {
        return _fieldNames[i];
    }

    public int GetOrdinal(string name)
    {
        return _nameToIndex[name];
    }

    public object GetValue(int i)
    {
        return _current.TryGetValue(_fieldNames[i], out var value) ? value ?? DBNull.Value : DBNull.Value;
    }

    public int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, FieldCount);
        for (var i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }

        return count;
    }

    public bool IsDBNull(int i)
    {
        return GetValue(i) == DBNull.Value;
    }

    public Type GetFieldType(int i)
    {
        return GetValue(i)?.GetType() ?? typeof(object);
    }

    public string GetDataTypeName(int i)
    {
        return GetFieldType(i).Name;
    }

    public bool GetBoolean(int i)
    {
        return Convert.ToBoolean(GetValue(i));
    }

    public byte GetByte(int i)
    {
        return Convert.ToByte(GetValue(i));
    }

    public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length)
    {
        throw new NotSupportedException();
    }

    public char GetChar(int i)
    {
        return Convert.ToChar(GetValue(i));
    }

    public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length)
    {
        throw new NotSupportedException();
    }

    public IDataReader GetData(int i)
    {
        throw new NotSupportedException();
    }

    public DateTime GetDateTime(int i)
    {
        return Convert.ToDateTime(GetValue(i));
    }

    public decimal GetDecimal(int i)
    {
        return Convert.ToDecimal(GetValue(i));
    }

    public double GetDouble(int i)
    {
        return Convert.ToDouble(GetValue(i));
    }

    public float GetFloat(int i)
    {
        return Convert.ToSingle(GetValue(i));
    }

    public Guid GetGuid(int i)
    {
        return (Guid)GetValue(i);
    }

    public short GetInt16(int i)
    {
        return Convert.ToInt16(GetValue(i));
    }

    public int GetInt32(int i)
    {
        return Convert.ToInt32(GetValue(i));
    }

    public long GetInt64(int i)
    {
        return Convert.ToInt64(GetValue(i));
    }

    public string GetString(int i)
    {
        var value = GetValue(i);
        if (value == null || value == DBNull.Value)
        {
            throw new InvalidCastException($"Column at index {i} is null.");
        }

        return Convert.ToString(value);
    }

    public int Depth => 1;
    public bool IsClosed => _enumerator == null;
    public int RecordsAffected => -1;

    public void Close()
    {
        Dispose();
    }

    public void Dispose()
    {
        _enumerator?.Dispose();
        _enumerator = null;
    }

    public bool Read()
    {
        if (_enumerator == null)
        {
            return false;
        }

        if (_firstRecordBuffered)
        {
            _current = _bufferedFirstRecord;
            _firstRecordBuffered = false;
            return true;
        }

        var hasNext = _enumerator.MoveNext();
        if (hasNext)
        {
            _current = _enumerator.Current;
        }

        return hasNext;
    }

    public bool NextResult()
    {
        return false;
    }

    public DataTable GetSchemaTable()
    {
        return null;
    }
}