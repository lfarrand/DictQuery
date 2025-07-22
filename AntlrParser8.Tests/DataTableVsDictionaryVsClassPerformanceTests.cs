using System.Data;
using System.Diagnostics;
using Xunit;
using Xunit.Abstractions;

namespace AntlrParser8.Tests;

public class DataTableVsIDictionaryVsClassPerformanceTests
{
    private class Person
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int Age { get; set; }
        public double Salary { get; set; }
    }

    private readonly ITestOutputHelper _testOutputHelper;

    public DataTableVsIDictionaryVsClassPerformanceTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void Compare_DataTable_IDictionary_Person_Performance()
    {
        const int numRecords = 1_000_000;
        var random = new Random(0);

        // Generate sample data
        var names = Enumerable.Range(0, numRecords).Select(i => $"Name{i}").ToArray();
        var ages = Enumerable.Range(0, numRecords).Select(_ => random.Next(18, 70)).ToArray();
        var salaries = Enumerable.Range(0, numRecords).Select(_ => random.NextDouble() * 90000 + 30000).ToArray();

        // --- DataTable ---
        var dt = new DataTable();
        dt.Columns.Add("Id", typeof(int));
        dt.Columns.Add("Name", typeof(string));
        dt.Columns.Add("Age", typeof(int));
        dt.Columns.Add("Salary", typeof(double));
        var sw = Stopwatch.StartNew();
        for (var i = 0; i < numRecords; i++)
        {
            dt.Rows.Add(i, names[i], ages[i], salaries[i]);
        }

        sw.Stop();
        var dataTableLoadMs = sw.Elapsed.TotalMilliseconds;

        // --- IDictionary List ---
        sw.Restart();
        var dictList = new List<IDictionary<string, object>>(numRecords);
        for (var i = 0; i < numRecords; i++)
        {
            dictList.Add(new Dictionary<string, object>
            {
                ["Id"] = i,
                ["Name"] = names[i],
                ["Age"] = ages[i],
                ["Salary"] = salaries[i]
            });
        }

        sw.Stop();
        var dictLoadMs = sw.Elapsed.TotalMilliseconds;

        // --- Strongly Typed List ---
        sw.Restart();
        var classList = new List<Person>(numRecords);
        for (var i = 0; i < numRecords; i++)
        {
            classList.Add(new Person
            {
                Id = i,
                Name = names[i],
                Age = ages[i],
                Salary = salaries[i]
            });
        }

        sw.Stop();
        var classLoadMs = sw.Elapsed.TotalMilliseconds;

        // --- Query: Age > 30 && Salary > 70000 ---
        var query = "Age > 30 AND Salary > 70000";

        // DataTable query
        sw.Restart();
        var dtResult = dt.Select(query);
        sw.Stop();
        var dtQueryMs = sw.Elapsed.TotalMilliseconds;

        // IDictionary query using ExpressionEvaluator<IDictionary<string, object>>
        var expressionBuilder = new ExpressionBuilder();
        var evaluator = new ExpressionEvaluator(expressionBuilder);

        sw.Restart();
        var dictResult = evaluator.Evaluate(query, dictList).ToList();
        sw.Stop();
        var dictQueryMs = sw.Elapsed.TotalMilliseconds;

        // --- Results ---
        _testOutputHelper.WriteLine(
            $"DataTable:   Load={dataTableLoadMs:F2} ms, Query={dtQueryMs:F2} ms, Matches={dtResult.Length}");
        _testOutputHelper.WriteLine(
            $"IDictionary:  Load={dictLoadMs:F2} ms, Query={dictQueryMs:F2} ms, Matches={dictResult.Count}");

        // All should return the same number of results
        Assert.Equal(dtResult.Length, dictResult.Count);
    }
}