using System.Collections.Generic;
using LazyCache;

namespace AntlrParser
{
    public class Program
    {
        public static void Main()
        {
            // Initialize cache and evaluator
            var cache = new CachingService();
            var expressionBuilder = new ExpressionBuilder();
            var evaluator = new ExpressionEvaluator(cache, expressionBuilder);

            // Sample data replacing DataTable
            var employees = new List<Dictionary<string, object>>
            {
                new Dictionary<string, object>
                {
                    ["Name"] = "John Doe",
                    ["Age"] = 28,
                    ["Department"] = "Engineering",
                    ["Salary"] = 75000.00m,
                    ["IsActive"] = true
                }
            };

            // Use existing DataTable.Select() expressions
            var results = evaluator.Evaluate("Age > 25 AND Department = 'Engineering'", employees);
        }
    }
}