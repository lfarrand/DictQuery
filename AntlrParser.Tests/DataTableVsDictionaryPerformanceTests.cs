using Xunit.Abstractions;

namespace AntlrParser.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Linq;
    using Xunit;

    public class DataTableVsDictionaryPerformanceTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public DataTableVsDictionaryPerformanceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void Compare_DataTable_vs_Dictionary_Performance()
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

            // --- Dictionary List ---
            sw.Restart();
            var dictList = new List<Dictionary<string, object>>(numRecords);
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

            // --- Query: Age > 30 && Salary > 70000 ---
            // DataTable query
            sw.Restart();
            var dtResult = dt.Select("Age > 30 AND Salary > 70000");
            sw.Stop();
            var dtQueryMs = sw.Elapsed.TotalMilliseconds;

            // Dictionary query
            sw.Restart();
            var dictResult = dictList.Where(d =>
                Convert.ToInt32(d["Age"]) > 30 &&
                Convert.ToDouble(d["Salary"]) > 70000
            ).ToList();
            sw.Stop();
            var dictQueryMs = sw.Elapsed.TotalMilliseconds;

            // --- Results ---
            _testOutputHelper.WriteLine(
                $"DataTable: Load={dataTableLoadMs:F2} ms, Query={dtQueryMs:F2} ms, Matches={dtResult.Length}");
            _testOutputHelper.WriteLine(
                $"Dictionary: Load={dictLoadMs:F2} ms, Query={dictQueryMs:F2} ms, Matches={dictResult.Count}");

            // Sanity check: both should return the same number of results
            Assert.Equal(dtResult.Length, dictResult.Count);

            // Optionally, assert that Dictionary is faster for query (often true)
            // Assert.True(dictQueryMs < dtQueryMs);

            // Optionally, assert that DataTable is faster for loading (sometimes true)
            // Assert.True(dataTableLoadMs < dictLoadMs);
        }
    }
}