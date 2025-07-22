namespace AntlrParser8;

public readonly struct ExpressionResult
{
    public readonly bool Success;
    public readonly object Value;
    
    public ExpressionResult(bool success, object value)
    {
        Success = success;
        Value = value;
    }
}