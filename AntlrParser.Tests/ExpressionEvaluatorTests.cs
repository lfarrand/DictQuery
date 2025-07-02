using LazyCache;
using Xunit.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;

namespace AntlrParser.Tests
{
    public class ExpressionEvaluatorTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        private readonly ExpressionEvaluator _evaluator =
            new ExpressionEvaluator(new CachingService(), new ExpressionBuilder(), new ReaderWriterLockSlim());

        private readonly List<Dictionary<string, object>> _sampleData = new List<Dictionary<string, object>>
        {
            new Dictionary<string, object>
            {
                ["Name"] = "John Doe",
                ["Age"] = 30,
                ["Salary"] = 75000.50m,
                ["Department"] = "Engineering",
                ["HireDate"] = new DateTime(2020, 1, 15),
                ["IsActive"] = true,
                ["Projects"] = new[] { "ProjectA", "ProjectB" },
                ["Location"] = null,
                ["LegalEntityId"] = "L1",
                ["CUSIP"] = "CUSIP1",
                ["ISIN"] = "ISIN1",
                ["SEDOL"] = "SEDOL1"
            },
            new Dictionary<string, object>
            {
                ["Name"] = "Jane Smith",
                ["Age"] = 25,
                ["Salary"] = 65000.00m,
                ["Department"] = "Marketing",
                ["HireDate"] = new DateTime(2021, 3, 10),
                ["IsActive"] = false,
                ["Projects"] = new[] { "ProjectC" },
                ["Location"] = "London",
                ["LegalEntityId"] = "L1",
                ["CUSIP"] = "CUSIP2",
                ["ISIN"] = "123",
                ["SEDOL"] = "SEDOL2"
            },
            new Dictionary<string, object>
            {
                ["Name"] = "Robert Johnson",
                ["Age"] = 35,
                ["Salary"] = 85000.75m,
                ["Department"] = "Engineering",
                ["HireDate"] = new DateTime(2019, 7, 22),
                ["IsActive"] = true,
                ["Projects"] = new string[0],
                ["Location"] = "Toronto",
                ["LegalEntityId"] = null,
                ["CUSIP"] = "CUSIP2",
                ["ISIN"] = "ISIN2",
                ["SEDOL"] = "SEDOL2"
            },
            new Dictionary<string, object>
            {
                ["Name"] = "Ricki Lake",
                ["Age"] = 48,
                ["Salary"] = 15000.75m,
                ["Department"] = "Entertainment",
                ["HireDate"] = new DateTime(2012, 7, 22),
                ["IsActive"] = false,
                ["Projects"] = new string[0],
                ["Location"] = "Toronto",
                ["LegalEntityId"] = "L3",
                ["CUSIP"] = "9999abcd",
                ["ISIN"] = "ISIN2",
                ["SEDOL"] = "SEDOL2"
            }
        };

        public ExpressionEvaluatorTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void BooleanLiterals_ShouldBeParsedCorrectly_True()
        {
            var results = _evaluator.Evaluate("IsActive = true", _sampleData);
            Assert.Equal(2, results.Count()); // Expecting 2 active records
        }

        [Fact]
        public void BooleanLiterals_ShouldBeParsedCorrectly_False()
        {
            var results = _evaluator.Evaluate("IsActive = false", _sampleData);
            Assert.Equal(2, results.Count());
        }

        [Fact]
        public void BasicComparisonsForNumerics_ShouldReturnCorrectResults()
        {
            // Numeric comparison
            var results = _evaluator.Evaluate("Age > 28", _sampleData);
            Assert.Equal(3, results.Count()); // John (30) and Robert (35)
        }

        [Fact]
        public void BasicComparisonsForStrings_ShouldReturnCorrectResults()
        {
            // String comparison
            var results = _evaluator.Evaluate("Department = 'Engineering'", _sampleData);
            Assert.Equal(2, results.Count());
        }

        [Fact]
        public void BasicComparisonsForBooleans_ShouldReturnCorrectResults()
        {
            // Boolean comparison
            var results = _evaluator.Evaluate("IsActive = true", _sampleData);
            Assert.Equal(2, results.Count());
        }

        [Fact]
        public void ArithmeticOperations_ShouldCalculateAdditionCorrectly()
        {
            // Addition
            var results = _evaluator.Evaluate("Salary > 70000 + 5000", _sampleData).ToList();
            Assert.Equal(2, results.Count); // John and Robert
        }

        [Fact]
        public void ArithmeticOperations_ShouldCalculateDivisionAndMultiplicationCorrectly()
        {
            // Division and multiplication
            var results = _evaluator.Evaluate("Salary / 1000 * 2 > 150", _sampleData).ToList();
            Assert.Equal(2, results.Count); // John and Robert
        }

        [Fact]
        public void LogicalOperators_ShouldCombineConditionsCorrectly()
        {
            // AND operator
            var results = _evaluator.Evaluate("Department = 'Engineering' AND Age < 33", _sampleData);
            Assert.Single(results); // John (Age 30)

            // OR operator
            results = _evaluator.Evaluate("Department = 'Marketing' OR IsActive = false", _sampleData);
            Assert.Equal(2, results.Count()); // Jane and inactive records

            // NOT operator
            results = _evaluator.Evaluate("NOT (Department = 'Engineering')", _sampleData);
            Assert.Equal(2, results.Count()); // Jane (Marketing) & Ricki (Entertainment)
        }

        [Fact]
        public void LikeOperator_ShouldSupportWildcards()
        {
            // Starts with wildcard
            var results = _evaluator.Evaluate("Name LIKE 'J*'", _sampleData);
            Assert.Equal(2, results.Count()); // John and Jane

            // Contains wildcard
            results = _evaluator.Evaluate("Name LIKE '*oh*'", _sampleData);
            Assert.Equal(2, results.Count()); // John and Robert Johnson

            // Ends with wildcard
            results = _evaluator.Evaluate("Name LIKE '*Smith'", _sampleData);
            Assert.Single(results); // Jane Smith
        }

        [Fact]
        public void InOperator_ShouldCheckExistsInWithNumericList()
        {
            // Numeric IN
            var results = _evaluator.Evaluate("Age IN (25, 35)", _sampleData);
            Assert.Equal(2, results.Count()); // Jane (25) and Robert (35)
        }

        [Fact]
        public void InOperator_ShouldCheckExistsInWithStringList()
        {
            // String IN
            var results = _evaluator.Evaluate("Department IN ('Engineering', 'HR')", _sampleData);
            Assert.Equal(2, results.Count());
        }

        [Fact]
        public void InOperator_ShouldCheckExistsInWithComplexStringList()
        {
            // String IN
            var results = _evaluator.Evaluate("Department IN ('Engineering') OR Department IN ('HR')", _sampleData);
            Assert.Equal(2, results.Count());
        }

        [Fact]
        public void InOperator_ShouldCheckExistsInWithComplexStringList2()
        {
            // String IN
            var results = _evaluator.Evaluate("(Department IN ('Engineering') OR Department IN ('HR')) AND Age = 35",
                _sampleData);
            Assert.Equal(1, results.Count());
        }

        [Fact]
        public void InOperator_ShouldRunComplexQuery1()
        {
            // String IN
            var results = _evaluator.Evaluate("LegalEntityId IS NULL", _sampleData);
            Assert.Equal(1, results.Count());
        }

        [Fact]
        public void InOperator_ShouldRunComplexQuery2()
        {
            // String IN
            var results =
                _evaluator.Evaluate(
                    "LegalEntityId IS NULL OR (CUSIP IS NULL AND ISIN IS NULL AND SEDOL IS NULL) OR CUSIP LIKE '9999%' OR LEN(ISIN) = 3 OR LEN(SEDOL) = 3",
                    _sampleData);
            Assert.Equal(3, results.Count());
        }

        [Fact]
        public void Functions_ShouldExecuteCorrectly_LEN()
        {
            // LEN function
            var results = _evaluator.Evaluate("LEN(Name) > 8", _sampleData);
            Assert.Equal(3, results.Count()); // "John Doe"=8, "Jane Smith"=10, "Robert Johnson"=13
        }

        [Fact]
        public void Functions_ShouldExecuteCorrectly_Equals()
        {
            // IIF function
            var results = _evaluator.Evaluate("Name = 'Robert Johnson'", _sampleData);
            Assert.Equal(1, results.Count()); // All should match
        }

        [Fact]
        public void Functions_ShouldExecuteCorrectly_NotEquals()
        {
            // IIF function
            var results = _evaluator.Evaluate("Name != 'Robert Johnson'", _sampleData);
            Assert.Equal(3, results.Count()); // All should match
        }

        [Fact]
        public void Functions_ShouldExecuteCorrectly_NotEqualsSqlSyntax()
        {
            // IIF function
            var results = _evaluator.Evaluate("Name <> 'Robert Johnson'", _sampleData);
            Assert.Equal(3, results.Count()); // All should match
        }

        [Fact]
        public void Functions_ShouldExecuteCorrectly_IIF()
        {
            // IIF function
            var results = _evaluator.Evaluate("IIF(IsActive, Salary > 70000, Salary < 70000)", _sampleData);
            Assert.Equal(4, results.Count());
        }

        [Fact]
        public void Functions_ShouldExecuteCorrectly_ISNULL()
        {
            // ISNULL function
            var results = _evaluator.Evaluate("ISNULL(Projects, 'N/A') != 'N/A'", _sampleData);
            Assert.Equal(4, results.Count());
        }

        [Fact]
        public void Functions_ShouldExecuteCorrectly_ISNULL2()
        {
            // ISNULL function
            var results = _evaluator.Evaluate("ISNULL(Location, 'N/A') != 'N/A'", _sampleData).ToList();

            //var results = _sampleData.Where(predicate).ToList();
            Assert.Equal(3, results.Count);
        }

        [Fact]
        public void Functions_ShouldExecuteCorrectly_ISNULL3()
        {
            // ISNULL function
            var results = _evaluator.Evaluate("ISNULL(Location, 'N/A') == 'N/A'", _sampleData);
            Assert.Equal(1, results.Count());
        }

        [Fact]
        public void SpecialColumnNames_ShouldHandleEscaping()
        {
            var data = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["First Name"] = "Alice", ["ID"] = 1 },
                new Dictionary<string, object> { ["First Name"] = "Bob", ["ID"] = 2 }
            };

            // Square bracket escaping
            var results = _evaluator.Evaluate("[First Name] = 'Alice'", data).ToList();
            Assert.Single(results);

            // Grave accent escaping
            results = _evaluator.Evaluate("`ID` = 2", data).ToList();
            Assert.Single(results);
        }

        [Fact]
        public void TypeConversion_MixedTypes_ShouldThrowError()
        {
            var data = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object> { ["Value"] = "100" }, // string
                new Dictionary<string, object> { ["Value"] = 100 }, // int
                new Dictionary<string, object> { ["Value"] = 100.0m } // decimal
            };

            Assert.Throws<ArgumentException>(() => _evaluator.Evaluate("Value = 100", data).ToList());
        }

        [Fact]
        public void DateComparisons_ShouldWorkCorrectly()
        {
            // Date literal comparison
            var results = _evaluator.Evaluate("HireDate > #2020-01-01#", _sampleData);
            Assert.Equal(2, results.Count()); // John (2020-01-15) and Jane (2021-03-10)
        }

        [Fact]
        public void ComplexExpressions_ShouldEvaluateCorrectly()
        {
            var expression = "(Department = 'Engineering' AND Salary > 70000) OR " +
                             "(Department = 'Marketing' AND Age < 30) OR " +
                             "Name LIKE 'Robert*'";

            var results = _evaluator.Evaluate(expression, _sampleData);
            Assert.Equal(3, results.Count()); // All records should match
        }

        [Fact]
        public void InvalidExpressions_ShouldThrowDescriptiveException_ForInvalidColumn()
        {
            // Undefined column
            Assert.Throws<InvalidOperationException>(() =>
                _evaluator.Evaluate("InvalidColumn = 123", _sampleData).ToList());
        }

        [Fact]
        public void InvalidExpressions_ShouldThrowDescriptiveException_ForSyntaxError()
        {
            // Syntax error
            Assert.Throws<ArgumentException>(() => _evaluator.Evaluate("Age > 'abc'", _sampleData).ToList());
        }

        [Fact]
        public void InvalidExpressions_ShouldThrowDescriptiveException_ForTypeMismatch()
        {
            // Type mismatch
            Assert.Throws<ArgumentException>(() => _evaluator.Evaluate("Name > 100", _sampleData).ToList());
        }

        [Fact]
        public void Caching_ShouldImprovePerformance()
        {
            // First execution (cold)
            var watch = System.Diagnostics.Stopwatch.StartNew();
            _evaluator.Evaluate("LEN(Department) > 5 AND Salary / 1000 > 70", _sampleData);
            var firstRun = watch.ElapsedTicks;

            // Second execution (cached)
            watch.Restart();
            _evaluator.Evaluate("LEN(Department) > 5 AND Salary / 1000 > 70", _sampleData);
            var secondRun = watch.ElapsedTicks;
            watch.Stop();

            _testOutputHelper.WriteLine($"First run: {firstRun} ticks, Second run: {secondRun} ticks");

            // Cached execution should be significantly faster
            Assert.True(secondRun < firstRun / 4);
        }
    }
}