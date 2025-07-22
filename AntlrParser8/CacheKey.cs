namespace AntlrParser8;

// Use structs to avoid allocations in tight loops
public readonly struct CacheKey : IEquatable<CacheKey>
{
    public readonly Type Type;
    public readonly int ExpressionHash;
    
    public CacheKey(Type type, string expression)
    {
        Type = type;
        ExpressionHash = expression.GetHashCode();
    }
    
    public bool Equals(CacheKey other) => Type == other.Type && ExpressionHash == other.ExpressionHash;
    public override int GetHashCode() => HashCode.Combine(Type, ExpressionHash);
}