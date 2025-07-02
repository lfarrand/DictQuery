using Xunit;

namespace AntlrParser8.Tests;

public class DataTableLikeOperatorTests
{
    [Theory]
    // Null handling
    [InlineData(null, null, false)]
    [InlineData(null, "abc", false)]
    [InlineData("abc", null, false)]

    // Exact match (no wildcards)
    [InlineData("Alice", "Alice", true)]
    [InlineData("Alice", "alice", true)] // case-insensitive
    [InlineData("Alice", "Bob", false)]

    // Pattern: starts with
    [InlineData("Alice", "A*", true)]
    [InlineData("Alice", "Al*", true)]
    [InlineData("Alice", "Alice*", true)]
    [InlineData("Alice", "Ali*", true)]
    [InlineData("Alice", "Bob*", false)]

    // Pattern: ends with
    [InlineData("Alice", "*e", true)]
    [InlineData("Alice", "*ce", true)]
    [InlineData("Alice", "*Alice", true)]
    [InlineData("Alice", "*bob", false)]

    // Pattern: contains
    [InlineData("Alice", "*lic*", true)]
    [InlineData("Alice", "*ic*", true)]
    [InlineData("Alice", "*x*", false)]

    // Pattern: illegal wildcard in the middle
    [InlineData("Alice", "A*e", false)]
    [InlineData("Alice", "A%c", false)]
    [InlineData("Alice", "Al*ce", false)]
    [InlineData("Alice", "A?e", false)]

    // Pattern: only wildcard
    [InlineData("Alice", "*", true)]
    [InlineData("Alice", "%", true)]

    // Pattern: wildcard at both ends
    [InlineData("Alice", "*l*", true)]
    [InlineData("Alice", "%l%", true)]

    // Pattern: question mark as single-character wildcard
    [InlineData("Alice", "A?ice", true)]
    [InlineData("Alice", "A?lic?", false)]
    [InlineData("Alice", "A?lic", false)]

    // Pattern: regex special characters in pattern
    [InlineData("A.C*", "A.C*", true)] // Should match literal 'A.C*'
    [InlineData("A.C", "A.C", true)]
    [InlineData("A.C", "A?C", true)] // '?' matches '.'
    [InlineData("A[C]", "A[C]", true)]
    [InlineData("A[C]", "A?C?", true)]

    // Pattern: percent as wildcard
    [InlineData("Alice", "A%", true)]
    [InlineData("Alice", "%e", true)]
    [InlineData("Alice", "%lic%", true)]

    // Anchoring: pattern must match whole string if no leading/trailing wildcard
    [InlineData("Alice", "Ali", false)]
    [InlineData("Alice", "lice", false)]
    [InlineData("Alice", "Alic", false)]
    [InlineData("Alice", "Alice", true)]
    [InlineData("Alice", "*Alice", true)]
    [InlineData("Alice", "Alice*", true)]
    [InlineData("Alice", "*Alice*", true)]
    public void Like_ShouldBehaveAsExpected(string value, string pattern, bool expected)
    {
        var result = DataTableLikeOperator.Like(value, pattern);
        Assert.Equal(expected, result);
    }
}