using System.Collections.Concurrent;
using System.Globalization;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace AntlrParser8;

public static class NumericConverter
{
    private static readonly ConcurrentDictionary<Type, Func<object, decimal>> DecimalConverters = new();
    private static readonly ConcurrentDictionary<Type, Func<object, double>> DoubleConverters = new();
    private static readonly ConcurrentDictionary<Type, Func<object, float>> SingleConverters = new();
    private static readonly ConcurrentDictionary<Type, Func<object, short>> Int16Converters = new();
    private static readonly ConcurrentDictionary<Type, Func<object, int>> Int32Converters = new();
    private static readonly ConcurrentDictionary<Type, Func<object, long>> Int64Converters = new();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static decimal ToDecimal(object value)
    {
        if (value == null) return 0m;
        
        return value switch
        {
            decimal d => d,
            int i => i,
            long l => l,
            double db => (decimal)db,
            float f => (decimal)f,
            byte b => b,
            short s => s,
            _ => GetDecimalConverter(value.GetType())(value)
        };
    }
    
    private static Func<object, decimal> GetDecimalConverter(Type type)
    {
        return DecimalConverters.GetOrAdd(type, t =>
        {
            var param = Expression.Parameter(typeof(object));
            var cast = Expression.Convert(param, t);
            var convert = Expression.Convert(cast, typeof(decimal));
            return Expression.Lambda<Func<object, decimal>>(convert, param).Compile();
        });
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double ToDouble(object value)
    {
        if (value == null) return 0d;
        
        return value switch
        {
            decimal d => (double)d,
            int i => i,
            long l => l,
            double db => db,
            float f => f,
            byte b => b,
            short s => s,
            _ => GetDoubleConverter(value.GetType())(value)
        };
    }
    
    private static Func<object, double> GetDoubleConverter(Type type)
    {
        return DoubleConverters.GetOrAdd(type, t =>
        {
            var param = Expression.Parameter(typeof(object));
            var cast = Expression.Convert(param, t);
            var convert = Expression.Convert(cast, typeof(double));
            return Expression.Lambda<Func<object, double>>(convert, param).Compile();
        });
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float ToSingle(object value)
    {
        if (value == null) return 0f;
        
        return value switch
        {
            decimal d => (float)d,
            int i => i,
            long l => l,
            double db => (float)db,
            float f => f,
            byte b => b,
            short s => s,
            _ => GetSingleConverter(value.GetType())(value)
        };
    }
    
    private static Func<object, Single> GetSingleConverter(Type type)
    {
        return SingleConverters.GetOrAdd(type, t =>
        {
            var param = Expression.Parameter(typeof(object));
            var cast = Expression.Convert(param, t);
            var convert = Expression.Convert(cast, typeof(Single));
            return Expression.Lambda<Func<object, Single>>(convert, param).Compile();
        });
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static short ToInt16(object value)
    {
        if (value == null) return 0;
        
        return value switch
        {
            decimal d => (short)d,
            int i => (short)i,
            long l => (short)l,
            double db => (short)db,
            float f => (short)f,
            byte b => b,
            short s => s,
            _ => GetInt16Converter(value.GetType())(value)
        };
    }

    private static Func<object, Int16> GetInt16Converter(Type type)
    {
        return Int16Converters.GetOrAdd(type, t =>
        {
            var param = Expression.Parameter(typeof(object));
            var cast = Expression.Convert(param, t);
            var convert = Expression.Convert(cast, typeof(Int16));
            return Expression.Lambda<Func<object, Int16>>(convert, param).Compile();
        });
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ToInt32(object value)
    {
        if (value == null) return 0;
        
        return value switch
        {
            decimal d => (int)d,
            int i => i,
            long l => (int)l,
            double db => (int)db,
            float f => (int)f,
            byte b => b,
            short s => s,
            _ => GetInt32Converter(value.GetType())(value)
        };
    }
    
    private static Func<object, Int32> GetInt32Converter(Type type)
    {
        return Int32Converters.GetOrAdd(type, t =>
        {
            var param = Expression.Parameter(typeof(object));
            var cast = Expression.Convert(param, t);
            var convert = Expression.Convert(cast, typeof(Int32));
            return Expression.Lambda<Func<object, Int32>>(convert, param).Compile();
        });
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToInt64(object value)
    {
        if (value == null) return 0;
        
        return value switch
        {
            decimal d => (long)d,
            int i => i,
            long l => l,
            double db => (long)db,
            float f => (long)f,
            byte b => b,
            short s => s,
            _ => GetInt64Converter(value.GetType())(value)
        };
    }

    private static Func<object, Int64> GetInt64Converter(Type type)
    {
        return Int64Converters.GetOrAdd(type, t =>
        {
            var param = Expression.Parameter(typeof(object));
            var cast = Expression.Convert(param, t);
            var convert = Expression.Convert(cast, typeof(Int64));
            return Expression.Lambda<Func<object, Int64>>(convert, param).Compile();
        });
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
            return ToInt16(value);
        }

        if (targetType == typeof(int))
        {
            return ToInt32(value);
        }

        if (targetType == typeof(long))
        {
            return ToInt64(value);
        }

        if (targetType == typeof(float))
        {
            return ToSingle(value);
        }

        if (targetType == typeof(double))
        {
            return ToDouble(value);
        }

        if (targetType == typeof(decimal))
        {
            return ToDecimal(value);
        }

        throw new ArgumentException(
            $"Invalid literal or type mismatch in expression: cannot convert {value} to {targetType.Name}");
    }
}