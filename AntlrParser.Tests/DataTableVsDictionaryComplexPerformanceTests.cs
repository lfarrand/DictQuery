using Xunit.Abstractions;

namespace AntlrParser.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Linq;
    using Xunit;

    public class DataTableVsDictionaryComplexPerformanceTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public DataTableVsDictionaryComplexPerformanceTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Theory]
        [InlineData(10000, 10, 10)]
        [InlineData(100000, 10, 10)]
        [InlineData(1000000, 10, 10)]
        public void Compare_DataTable_vs_Dictionary_Performance(int numRecords,int numFields,int numQueries)
        {
            var random = new Random(0);

            // --- Field definitions ---
            var fieldNames = Enumerable.Range(0, numFields).Select(i => $"Field{i}").ToArray();
            var fieldTypes = new Type[numFields];
            for (var i = 0; i < numFields; i++)
            {
                switch (i % 5)
                {
                    case 0: fieldTypes[i] = typeof(int); break;
                    case 1: fieldTypes[i] = typeof(double); break;
                    case 2: fieldTypes[i] = typeof(string); break;
                    case 3: fieldTypes[i] = typeof(DateTime); break;
                    case 4: fieldTypes[i] = typeof(bool); break;
                }
            }

            // --- DataTable ---
            var dt = new DataTable();
            for (var i = 0; i < numFields; i++)
            {
                dt.Columns.Add(fieldNames[i], fieldTypes[i]);
            }

            var sw = Stopwatch.StartNew();
            for (var row = 0; row < numRecords; row++)
            {
                var values = new object[numFields];
                for (var col = 0; col < numFields; col++)
                {
                    switch (fieldTypes[col].Name)
                    {
                        case nameof(Int32): values[col] = random.Next(0, 1000); break;
                        case nameof(Double): values[col] = random.NextDouble() * 10000; break;
                        case nameof(String): values[col] = $"Str{random.Next(0, 10000)}"; break;
                        case nameof(DateTime): values[col] = DateTime.Now.AddDays(-random.Next(0, 3650)); break;
                        case nameof(Boolean): values[col] = random.Next(0, 2) == 0; break;
                    }
                }

                dt.Rows.Add(values);
            }

            sw.Stop();
            var dataTableLoadMs = sw.Elapsed.TotalMilliseconds;

            // --- Dictionary List ---
            sw.Restart();
            var dictList = new List<Dictionary<string, object>>(numRecords);
            for (var row = 0; row < numRecords; row++)
            {
                var dict = new Dictionary<string, object>(numFields);
                for (var col = 0; col < numFields; col++)
                {
                    object value;
                    switch (fieldTypes[col].Name)
                    {
                        case nameof(Int32): value = random.Next(0, 1000); break;
                        case nameof(Double): value = random.NextDouble() * 10000; break;
                        case nameof(String): value = $"Str{random.Next(0, 10000)}"; break;
                        case nameof(DateTime): value = DateTime.Now.AddDays(-random.Next(0, 3650)); break;
                        case nameof(Boolean): value = random.Next(0, 2) == 0; break;
                        default: value = null; break;
                    }

                    dict[fieldNames[col]] = value;
                }

                dictList.Add(dict);
            }

            sw.Stop();
            var dictLoadMs = sw.Elapsed.TotalMilliseconds;

            // --- Prepare random queries ---
            var queries = new List<(string dtQuery, Func<Dictionary<string, object>, bool> dictQuery)>();
            for (var i = 0; i < numQueries; i++)
            {
                var intField = i % numFields;
                var doubleField = (i + 1) % numFields;
                var stringField = (i + 2) % numFields;
                var dateField = (i + 3) % numFields;
                var boolField = (i + 4) % numFields;

                // Only use fields of correct type
                while (fieldTypes[intField] != typeof(int))
                {
                    intField = (intField + 1) % numFields;
                }

                while (fieldTypes[doubleField] != typeof(double))
                {
                    doubleField = (doubleField + 1) % numFields;
                }

                while (fieldTypes[stringField] != typeof(string))
                {
                    stringField = (stringField + 1) % numFields;
                }

                while (fieldTypes[dateField] != typeof(DateTime))
                {
                    dateField = (dateField + 1) % numFields;
                }

                while (fieldTypes[boolField] != typeof(bool))
                {
                    boolField = (boolField + 1) % numFields;
                }

                var intThreshold = random.Next(100, 900);
                var doubleThreshold = random.NextDouble() * 9000 + 500;
                var stringPattern = random.Next(0, 2) == 0 ? "5" : "3";
                var dateThreshold = DateTime.Now.AddDays(-random.Next(100, 3000));
                var boolValue = random.Next(0, 2) == 0;

                var dtQuery =
                    $"{fieldNames[intField]} > {intThreshold} AND {fieldNames[doubleField]} > {doubleThreshold:F2} AND {fieldNames[stringField]} LIKE '%{stringPattern}%' AND {fieldNames[dateField]} > #{dateThreshold:yyyy-MM-dd}# AND {fieldNames[boolField]} = {boolValue.ToString().ToLower()}";
                Func<Dictionary<string, object>, bool> dictQuery = d =>
                    Convert.ToInt32(d[fieldNames[intField]]) > intThreshold &&
                    Convert.ToDouble(d[fieldNames[doubleField]]) > doubleThreshold &&
                    d[fieldNames[stringField]] is string s && s.Contains(stringPattern) &&
                    d[fieldNames[dateField]] is DateTime dtVal && dtVal > dateThreshold &&
                    Convert.ToBoolean(d[fieldNames[boolField]]) == boolValue;

                queries.Add((dtQuery, dictQuery));
            }

            // --- DataTable queries ---
            sw.Restart();
            var dtTotalMatches = 0;
            foreach (var (dtQuery, _) in queries)
            {
                var result = dt.Select(dtQuery);
                dtTotalMatches += result.Length;
            }

            sw.Stop();
            var dtQueryMs = sw.Elapsed.TotalMilliseconds;

            // --- Dictionary queries ---
            sw.Restart();
            var dictTotalMatches = 0;
            foreach (var (_, dictQuery) in queries)
            {
                var result = dictList.Where(dictQuery).ToList();
                dictTotalMatches += result.Count;
            }

            sw.Stop();
            var dictQueryMs = sw.Elapsed.TotalMilliseconds;

            // --- Results ---
            _testOutputHelper.WriteLine(
                $"DataTable: Load={dataTableLoadMs:F2} ms, 100 Queries={dtQueryMs:F2} ms, TotalMatches={dtTotalMatches}");
            _testOutputHelper.WriteLine(
                $"Dictionary: Load={dictLoadMs:F2} ms, 100 Queries={dictQueryMs:F2} ms, TotalMatches={dictTotalMatches}");

            // Basic sanity
            Assert.True(dtTotalMatches >= 0);
            Assert.True(dictTotalMatches >= 0);
        }
    }
}