using System;
using System.Globalization;

namespace AntlrParser
{
    public static class DataTableTypeConverter
    {
        public static bool AreEqual(object left, object right)
        {
            // Handle nulls
            if (ReferenceEquals(left, right))
            {
                return true;
            }

            if (left == null || right == null)
            {
                return false;
            }

            // If both are strings, compare as strings
            if (left is string s1 && right is string s2)
            {
                return string.Equals(s1, s2, StringComparison.OrdinalIgnoreCase);
            }

            // If both are numeric, compare as numbers
            if (IsNumeric(left) && IsNumeric(right))
            {
                var d1 = Convert.ToDouble(left, CultureInfo.InvariantCulture);
                var d2 = Convert.ToDouble(right, CultureInfo.InvariantCulture);
                return Math.Abs(d1 - d2) < double.Epsilon;
            }

            // Fallback: compare as strings
            return string.Equals(left.ToString(), right.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        public static bool AreNotEqual(object left, object right)
        {
            return CompareValues(left, right) != 0;
        }

        public static bool IsLessThan(object left, object right)
        {
            if (left is bool && right is bool)
            {
                throw new InvalidOperationException("Relational operators are not supported for booleans.");
            }

            return CompareValues(left, right) < 0;
        }

        public static bool IsGreaterThan(object left, object right)
        {
            if (left is bool && right is bool)
            {
                throw new InvalidOperationException("Relational operators are not supported for booleans.");
            }

            return CompareValues(left, right) > 0;
        }

        public static bool IsLessThanOrEqual(object left, object right)
        {
            if (left is bool && right is bool)
            {
                throw new InvalidOperationException("Relational operators are not supported for booleans.");
            }

            return CompareValues(left, right) <= 0;
        }

        public static bool IsGreaterThanOrEqual(object left, object right)
        {
            if (left is bool && right is bool)
            {
                throw new InvalidOperationException("Relational operators are not supported for booleans.");
            }

            return CompareValues(left, right) >= 0;
        }

        public static int CompareValues(object left, object right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            if (left == null)
            {
                return -1;
            }

            if (right == null)
            {
                return 1;
            }

            if (IsNumeric(left) && IsNumeric(right))
            {
                var d1 = Convert.ToDouble(left);
                var d2 = Convert.ToDouble(right);
                return d1.CompareTo(d2);
            }

            if (left is DateTime dt1 && right is DateTime dt2)
            {
                return dt1.CompareTo(dt2);
            }

            return string.Compare(
                left.ToString(),
                right.ToString(),
                StringComparison.OrdinalIgnoreCase
            );
        }

        private static bool IsNumeric(object value)
        {
            return value is int || value is long || value is float || value is double ||
                   value is decimal || value is short || value is ushort || value is byte || value is sbyte;
        }
    }
}