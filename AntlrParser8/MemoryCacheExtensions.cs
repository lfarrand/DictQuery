using System.Collections;
using System.Reflection;
using Microsoft.Extensions.Caching.Memory;

namespace AntlrParser8;

public static class MemoryCacheExtensions
{
    public static IEnumerable<object> GetKeys(this MemoryCache memoryCache)
    {
        // Get the private EntriesCollection property
        var entriesProperty =
            typeof(MemoryCache).GetProperty("EntriesCollection", BindingFlags.NonPublic | BindingFlags.Instance);
        var entries = entriesProperty.GetValue(memoryCache) as ICollection;
        if (entries == null)
        {
            yield break;
        }

        foreach (var entry in entries)
        {
            // Each entry is an internal type; get the "Key" property via reflection
            var keyProperty = entry.GetType().GetProperty("Key");
            yield return keyProperty.GetValue(entry);
        }
    }
}