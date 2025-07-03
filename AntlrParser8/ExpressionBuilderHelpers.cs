namespace AntlrParser8;

using System;
using System.Collections.Generic;
using System.Linq.Expressions;

public static class ExpressionBuilderHelpers
{
    public static Expression BuildSafeDictionaryAccess(ParameterExpression parameter, string key)
    {
        var dictType = parameter.Type;
        var valueType = typeof(object);
        var tryGetValue = dictType.GetMethod("TryGetValue", new[] { typeof(string), valueType.MakeByRefType() });
        var valueVar = Expression.Variable(valueType, "value");
        var keyConst = Expression.Constant(key, typeof(string));
        var tryGetValueCall = Expression.Call(parameter, tryGetValue, keyConst, valueVar);

        var label = Expression.Label(valueType);
        // if (dict.TryGetValue(key, out value)) return value; else return null;
        var block = Expression.Block(
            new[] { valueVar },
            Expression.IfThenElse(
                tryGetValueCall,
                Expression.Return(label, valueVar),
                Expression.Return(label, Expression.Constant(null, valueType))
            ),
            Expression.Label(label, Expression.Constant(null, valueType))
        );
        return block;
    }

    /// <summary>
    /// Builds an expression to access a property or dictionary key for type T.
    /// </summary>
    public static Expression BuildPropertyAccess<T>(ParameterExpression parameter, string propertyName)
    {
        var type = typeof(T);
        if (typeof(IDictionary<string, object>).IsAssignableFrom(type))
        {
            // Dictionary: parameter["propertyName"]
            var indexer = type.GetProperty("Item");
            return Expression.Property(parameter, indexer, Expression.Constant(propertyName));
        }
        else
        {
            // Class: parameter.PropertyName
            return Expression.PropertyOrField(parameter, propertyName);
        }
    }

    /// <summary>
    /// Builds a lambda: x => (object)x.Property or x => (object)x["Property"]
    /// </summary>
    public static Expression<Func<T, object>> BuildPropertyLambda<T>(string propertyName)
    {
        var parameter = Expression.Parameter(typeof(T), "x");
        var propertyAccess = BuildPropertyAccess<T>(parameter, propertyName);
        var convertToObject = Expression.Convert(propertyAccess, typeof(object));
        return Expression.Lambda<Func<T, object>>(convertToObject, parameter);
    }
}