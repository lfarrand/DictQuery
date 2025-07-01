using System;
using System.Globalization;

namespace AntlrParser
{
    public static class NumericConverter
    {
        public static double ToDouble(object value)
        {
            if (value == null) return 0;
            return Convert.ToDouble(value);
        }

        public static decimal ToDecimal(object value)
        {
            if (value == null) return 0;
            return Convert.ToDecimal(value);
        }

        public static object ConvertToBestType(object value)
        {
            if (value == null) return 0;
            if (value is string) return value; // Keep strings as strings
            if (value is int || value is long || value is float || value is double || value is decimal)
                return value; // Already numeric
            if (value is bool) return value;
            if (value is DateTime) return value;
            // Fallback: try to convert to double for legacy support, but only if it's a number
            if (double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out double d))
                return d;
            // If not a number, return as string
            return value.ToString();
        }
    }

}