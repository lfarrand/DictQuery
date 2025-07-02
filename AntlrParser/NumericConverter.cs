using System;
using System.Globalization;

namespace AntlrParser
{
    public static class NumericConverter
    {
        public static double ToDouble(object value)
        {
            return value == null ? 0.0 : Convert.ToDouble(value);
        }

        public static decimal ToDecimal(object value)
        {
            return value == null ? 0M : Convert.ToDecimal(value);
        }

        public static object ConvertToBestType(object value, Type targetType)
        {
            if (value == null)
            {
                if (targetType.IsValueType)
                {
                    return Activator.CreateInstance(targetType);
                }

                return null;
            }

            if (targetType.IsInstanceOfType(value))
            {
                return value;
            }

            if (value is string && targetType == typeof(short))
            {
                if (short.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                {
                    return d;
                }

                throw new ArgumentException($"Cannot convert string '{value}' to decimal");
            }

            if (value is string && targetType == typeof(int))
            {
                if (int.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                {
                    return d;
                }

                throw new ArgumentException($"Cannot convert string '{value}' to decimal");
            }

            if (value is string && targetType == typeof(long))
            {
                if (long.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                {
                    return d;
                }

                throw new ArgumentException($"Cannot convert string '{value}' to decimal");
            }

            if (value is string && targetType == typeof(decimal))
            {
                if (decimal.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                {
                    return d;
                }

                throw new ArgumentException($"Cannot convert string '{value}' to decimal");
            }

            if (value is string && targetType == typeof(float))
            {
                if (float.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                {
                    return d;
                }

                throw new ArgumentException($"Cannot convert string '{value}' to double");
            }

            if (value is string && targetType == typeof(double))
            {
                if (double.TryParse(value.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                {
                    return d;
                }

                throw new ArgumentException($"Cannot convert string '{value}' to double");
            }

            if (value is string s1 && targetType == typeof(bool))
            {
                var valLower = s1.ToLowerInvariant();

                if (valLower == "true" || valLower == "1")
                {
                    return true;
                }

                if (valLower == "false" || valLower == "0")
                {
                    return false;
                }

                throw new ArgumentException($"Cannot convert string '{value}' to bool");
            }

            if (value is string s2 && targetType == typeof(DateTime))
            {
                if (DateTime.TryParse(s2, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
                {
                    return date;
                }

                try
                {
                    return Convert.ToDateTime(value, CultureInfo.InvariantCulture);
                }
                catch (FormatException)
                {
                    throw new ArgumentException(
                        $"Invalid literal or type mismatch in expression: cannot convert {value} to {targetType.Name}");
                }
            }

            if (targetType == typeof(short))
            {
                return Convert.ToInt16(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(int))
            {
                return Convert.ToInt32(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(long))
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(float))
            {
                return Convert.ToSingle(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(double))
            {
                return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }

            if (targetType == typeof(decimal))
            {
                return Convert.ToDecimal(value, CultureInfo.InvariantCulture);
            }

            throw new ArgumentException(
                $"Invalid literal or type mismatch in expression: cannot convert {value} to {targetType.Name}");
        }
    }
}