using System;
using System.Collections.Generic;
using Microsoft.Extensions.Caching.Memory;

namespace AntlrParser
{
    public class MemoryCacheAdapter : IMemoryCache, IDisposable
    {
        private readonly MemoryCache _cache;

        public MemoryCacheAdapter(MemoryCache cache)
        {
            _cache = cache;
        }

        public void Compact(double percentage)
        {
            _cache.Compact(percentage);
        }

        public IEnumerable<object> GetKeys()
        {
            return _cache.GetKeys();
        }

        public TItem Set<TItem>(object key, TItem value)
        {
            _cache.Set(key, value);

            return value;
        }

        public void Dispose()
        {
            _cache?.Dispose();
        }
    }
}