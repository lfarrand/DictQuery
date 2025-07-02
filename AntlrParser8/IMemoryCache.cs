namespace AntlrParser8;

public interface IMemoryCache
{
    void Compact(double percentage);

    IEnumerable<object> GetKeys();
}