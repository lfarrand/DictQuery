using System.Collections.Generic;

namespace AntlrParser
{
    public interface IMemoryCache
    {
        void Compact(double percentage);

        IEnumerable<object> GetKeys();
    }
}