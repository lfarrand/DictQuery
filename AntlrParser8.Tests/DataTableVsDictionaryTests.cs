using System.Data;
using Xunit;
using Xunit.Abstractions;

namespace AntlrParser8.Tests;

public class DataTableVsIDictionaryTests
{
    private readonly ITestOutputHelper _testOutputHelper;

    private class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int? Age { get; set; }
        public double? Salary { get; set; }
    }
    
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

    private static readonly List<IDictionary<string, object>> SampleData = new()
    {
        new Dictionary<string, object> { ["Name"] = "Alice", ["Age"] = 30, ["Active"] = true },
        new Dictionary<string, object> { ["Name"] = "Bob", ["Age"] = 25, ["Active"] = false },
        new Dictionary<string, object> { ["Name"] = "Charlie", ["Age"] = 35, ["Active"] = true },
        new Dictionary<string, object> { ["Name"] = "Dana", ["Age"] = 30, ["Active"] = false }
    };

    private static DataTable CreateDataTable(IEnumerable<IDictionary<string, object>> data)
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

    private readonly ExpressionEvaluator _evaluator = new(new ExpressionBuilder());

    public DataTableVsIDictionaryTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

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
    public void IDictionaryEvaluator_Should_Match_DataTable(string query)
    {
        // Arrange
        var table = CreateDataTable(SampleData);

        // DataTable evaluation
        var dtResults = table.Select(query)
            .Select(r => r["Name"] as string)
            .OrderBy(n => n)
            .ToList();

        // IDictionary evaluator (replace with your actual evaluator)
        var dictResults = _evaluator.Evaluate(query, SampleData)
            .Select(row => row["Name"] as string)
            .OrderBy(n => n)
            .ToList();

        // Assert
        Assert.Equal(dtResults, dictResults);
    }

    [Theory]
    [InlineData("CP_CD IN ('1')")]
    [InlineData("CP_CD IN ('1') OR CPTY_TYPE IN ('EXTERNAL')")]
    [InlineData("CP_CD IN ('1') OR CPTY_TYPE NOT IN ('EXTERNAL')")]
    [InlineData("PRODUCT_TYPE_LEVEL_1 in ('CIBC Own Securities') and EXCLUSIONS like 'Exclusion -%'")]
    [InlineData("PRODUCT_TYPE_LEVEL_1 in ('CIBC Own Securities') and EXCLUSIONS not like 'Exclusion -%'")]
    public void IDictionaryEvaluator_Should_Match_DataTable2(string query)
    {
        // Arrange
        List<IDictionary<string, object>> sampleData = new()
        {
            new Dictionary<string, object> { ["CPTY_TYPE"] = "EXTERNAL", ["CP_CD"] = "3042273", ["PRODUCT_TYPE_LEVEL_1"] = "CIBC Own Securities", ["EXCLUSIONS"] = "Inclusion - O/N CS Swaps" }
        };

        var table = CreateDataTable(sampleData);

        // DataTable evaluation
        var dtResults = table.Select(query)
            .Select(r => r["CPTY_TYPE"] as string)
            .OrderBy(n => n)
            .ToList();

        // IDictionary evaluator (replace with your actual evaluator)
        var dictResults = _evaluator.Evaluate(query, sampleData)
            .Select(row => row["CPTY_TYPE"] as string)
            .OrderBy(n => n)
            .ToList();

        // Assert
        Assert.Equal(dtResults, dictResults);
    }

    [Fact]
    public void IDictionaryEvaluator_Should_Match_DataTable_Null_Handling()
    {
        var data = new List<IDictionary<string, object>>
        {
            new Dictionary<string, object>() { ["Name"] = "Eve", ["Age"] = null },
            new Dictionary<string, object>() { ["Name"] = "Frank", ["Age"] = 40 }
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

    [Theory]
    [InlineData("TRUE")]
    [InlineData("FALSE")]
    [InlineData("TRUE OR FALSE")]
    [InlineData("TRUE OR TRUE")]
    [InlineData("(TRUE OR FALSE) AND (TRUE OR FALSE)")]
    [InlineData("FALSE AND (TRUE OR FALSE)")]
    [InlineData("FALSE OR (TRUE AND FALSE)")]
    public void IDictionaryEvaluator_Should_Match_DataTable_BooleanLiteral_Handling(string criteria)
    {
        var data = new List<IDictionary<string, object>>
        {
            new Dictionary<string, object>() { ["Name"] = "Eve", ["Age"] = null },
            new Dictionary<string, object>() { ["Name"] = "Frank", ["Age"] = 40 }
        };

        var table = CreateDataTable(data);

        var dtRows = table.Select(criteria);
        var dtResults = dtRows.Select(r => r["Name"]).Cast<string>().OrderBy(n => n).ToList();

        var dictResults = _evaluator.Evaluate(criteria, data)
            .Select(row => row["Name"] as string)
            .OrderBy(n => n)
            .ToList();

        Assert.Equal(dtResults, dictResults);
    }
    
    [Fact]
    public void IDictionaryEvaluator_Should_Match_DataTable_Syntax_Error()
    {
        var ex1 = Record.Exception(() => CreateDataTable(SampleData).Select("Age > 'abc'"));
        var ex2 = Record.Exception(() => _evaluator.Evaluate("Age > 'abc'", SampleData).ToList());

        Assert.NotNull(ex1);
        Assert.NotNull(ex2);
        Assert.IsType<ArgumentException>(ex2); // Or your chosen exception type
    }
}