namespace AntlrParser8;

public class Program
{
    public static void Main()
    {
        Console.WriteLine(ModelLikeOperator.Like("Alice", "%"));
        Console.WriteLine(ModelLikeOperator.Like("Alice", "%e"));
        Console.WriteLine(ModelLikeOperator.Like("Alice", "%l%"));
        Console.WriteLine(ModelLikeOperator.Like("Alice", "%lic%"));
        Console.WriteLine(ModelLikeOperator.Like("Alice", "A%"));
        Console.WriteLine(ModelLikeOperator.Like("Alice", "A?e"));
        Console.WriteLine(ModelLikeOperator.Like("Alice", "A?lic?"));


        // Initialize cache and evaluator
        var expressionBuilder = new ExpressionBuilder();
        var evaluator = new ExpressionEvaluator(expressionBuilder);

        // Sample data replacing DataTable
        var employees = new List<IDictionary<string, object>>
        {
            new Dictionary<string, object>()
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