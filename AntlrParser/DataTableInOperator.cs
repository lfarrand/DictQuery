using System.Collections;

namespace AntlrParser
{
    public static class DataTableInOperator
    {
        public static bool Contains(object value, object collection)
        {
            if (collection is IEnumerable enumerable)
            {
                foreach (var item in enumerable)
                {
                    if (Equals(value, item)) return true;
                }
            }

            return false;
        }
    }
}