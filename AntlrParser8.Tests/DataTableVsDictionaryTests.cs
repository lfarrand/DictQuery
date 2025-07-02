using System.Data;
using LazyCache;
using Xunit;

namespace AntlrParser8.Tests;

public class DataTableVsDictionaryTests
{
    public static IEnumerable<object[]> GenerateTestQueries()
    {
        string[] names = { "Alice", "Bob", "Charlie", "Dana" };
        int[] ages = { 25, 30, 35 };
        bool[] actives = { true, false };
        string[] namePatterns = { "A*", "*e", "*li*", "C*", "*a*", "B*", "*ar*" };
        string[] boolOperators = { "=", "<>" };
        string[] numericOperators = { "=", "<>", ">", "<", ">=", "<=" };

        // For booleans, only yield these:
        foreach (var op in boolOperators)
        {
            foreach (var name in names)
            {
                yield return new object[] { $"Name {op} '{name}'" };
            }

            foreach (var active in actives)
            {
                yield return new object[] { $"Active {op} {active.ToString().ToLower()}" };
            }
        }

        // For numerics, use all operators:
        foreach (var op in numericOperators)
        foreach (var age in ages)
        {
            yield return new object[] { $"Age {op} {age}" };
        }

        // LIKE patterns
        foreach (var pattern in namePatterns)
        {
            yield return new object[] { $"Name LIKE '{pattern}'" };
        }

        // IN operator
        yield return new object[] { "Age IN (25, 30, 35)" };
        yield return new object[] { "Name IN ('Alice', 'Dana')" };

        // IS NULL/IS NOT NULL
        yield return new object[] { "Name IS NULL" };
        yield return new object[] { "Name IS NOT NULL" };
        yield return new object[] { "Age IS NULL" };
        yield return new object[] { "Active IS NOT NULL" };

        // Math expressions
        foreach (var age in ages)
        {
            yield return new object[] { $"Age + 5 = {age + 5}" };
        }

        foreach (var age in ages)
        {
            yield return new object[] { $"Age - 5 = {age - 5}" };
        }

        foreach (var age in ages)
        {
            yield return new object[] { $"Age * 2 = {age * 2}" };
        }

        foreach (var age in ages)
        {
            yield return new object[] { $"Age / 5 = {age / 5}" };
        }

        // NOT operator
        yield return new object[] { "NOT Active" };
        yield return new object[] { "NOT (Age = 30)" };

        // Nested AND/OR logic
        foreach (var age in ages)
        foreach (var name in names)
        {
            yield return new object[] { $"Age = {age} AND Name = '{name}'" };
        }

        foreach (var age in ages)
        foreach (var name in names)
        {
            yield return new object[] { $"Age = {age} OR Name = '{name}'" };
        }

        foreach (var age in ages)
        foreach (var active in actives)
        {
            yield return new object[]
                { $"(Age = {age} AND Active = {active.ToString().ToLower()}) OR Name = 'Alice'" };
        }

        foreach (var age in ages)
        foreach (var active in actives)
        {
            yield return new object[]
                { $"Age = {age} AND (Active = {active.ToString().ToLower()} OR Name = 'Charlie')" };
        }

        // Double-nested logic
        foreach (var age in ages)
        foreach (var name in names)
        foreach (var active in actives)
        {
            yield return new object[]
                { $"(Age = {age} AND Name = '{name}') OR Active = {active.ToString().ToLower()}" };
        }

        // Deeply nested logic and math
        foreach (var age in ages)
        foreach (var name in names)
        foreach (var active in actives)
        {
            yield return new object[]
            {
                $"((Age + 5 = {age + 5} AND Name LIKE '{name[0]}*') OR (Active = {active.ToString().ToLower()} AND Age < 40))"
            };
        }

        // Edge and empty cases
        yield return new object[] { "Age > 100" };
        yield return new object[] { "Name = 'Nonexistent'" };
        yield return new object[] { "Active = true AND Age < 0" };
        yield return new object[] { "Name LIKE 'Z*'" };
    }

    private static readonly List<Dictionary<string, object>> SampleData = new List<Dictionary<string, object>>
    {
        new Dictionary<string, object> { ["Name"] = "Alice", ["Age"] = 30, ["Active"] = true },
        new Dictionary<string, object> { ["Name"] = "Bob", ["Age"] = 25, ["Active"] = false },
        new Dictionary<string, object> { ["Name"] = "Charlie", ["Age"] = 35, ["Active"] = true },
        new Dictionary<string, object> { ["Name"] = "Dana", ["Age"] = 30, ["Active"] = false }
    };

    private static DataTable CreateDataTable(IEnumerable<Dictionary<string, object>> data)
    {
        var table = new DataTable();
        var dataList = data.ToList();

        if (!dataList.Any())
        {
            return table;
        }

        foreach (var key in dataList.First().Keys)
        {
            table.Columns.Add(key, dataList.First()[key]?.GetType() ?? typeof(object));
        }

        foreach (var row in dataList)
        {
            table.Rows.Add(table.Columns.Cast<DataColumn>()
                .Select(c => row.TryGetValue(c.ColumnName, out var v) ? v : DBNull.Value).ToArray());
        }

        return table;
    }

    private readonly ExpressionEvaluator _evaluator = new ExpressionEvaluator(new CachingService(),
        new ExpressionBuilder(), new ReaderWriterLockSlim());

    [Theory]
    [InlineData("Active <= true")]
    [InlineData("Active > false")]
    public void InvalidBooleanRelationalOperators_ShouldThrow(string query)
    {
        var table = CreateDataTable(SampleData);
        Assert.Throws<InvalidOperationException>(() => _evaluator.Evaluate(query, SampleData).ToList());
        Assert.Throws<EvaluateException>(() => table.Select(query));
    }

    [Theory]
    [MemberData(nameof(GenerateTestQueries))]
    public void DictionaryEvaluator_Should_Match_DataTable(string query)
    {
        // Arrange
        var table = CreateDataTable(SampleData);

        // DataTable evaluation
        List<string?> dtResults = table.Select(query)
            .Select(r => r["Name"] as string)
            .OrderBy(n => n)
            .ToList();

        // Dictionary evaluator (replace with your actual evaluator)
        List<string?> dictResults = _evaluator.Evaluate(query, SampleData)
            .Select(row => row["Name"] as string)
            .OrderBy(n => n)
            .ToList();

        // Assert
        Assert.Equal(dtResults, dictResults);
    }

    [Fact]
    public void DictionaryEvaluator_Should_Match_DataTable_Null_Handling()
    {
        var data = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object> { ["Name"] = "Eve", ["Age"] = null },
            new Dictionary<string, object> { ["Name"] = "Frank", ["Age"] = 40 }
        };

        var table = CreateDataTable(data);

        var dtRows = table.Select("Age IS NULL");
        var dtResults = dtRows.Select(r => r["Name"]).Cast<string>().OrderBy(n => n).ToList();

        var dictResults = _evaluator.Evaluate("Age IS NULL", data)
            .Select(row => row["Name"] as string)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(dtResults, dictResults);
    }

    [Fact]
    public void DictionaryEvaluator_Should_Match_DataTable_Syntax_Error()
    {
        var ex1 = Record.Exception(() => CreateDataTable(SampleData).Select("Age > 'abc'"));
        var ex2 = Record.Exception(() => _evaluator.Evaluate("Age > 'abc'", SampleData).ToList());

        Assert.NotNull(ex1);
        Assert.NotNull(ex2);
        Assert.IsType<ArgumentException>(ex2); // Or your chosen exception type
    }
}