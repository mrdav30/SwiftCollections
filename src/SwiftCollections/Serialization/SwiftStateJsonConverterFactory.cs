using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftCollections;

public sealed class SwiftStateJsonConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
    {
        var stateProp = typeToConvert.GetProperty(
            "State",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return stateProp != null;
    }

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        var stateProp = typeToConvert.GetProperty(
            "State",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        Type stateType = stateProp.PropertyType;

        var ctor = typeToConvert.GetConstructor(new[] { stateType });

        Type converterType = typeof(SwiftStateJsonConverter<,>)
            .MakeGenericType(typeToConvert, stateType);

        // Build constructor delegate
        var stateParam = Expression.Parameter(stateType);
        var newExpr = Expression.New(ctor, stateParam);

        var factoryLambda = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(stateType, typeToConvert),
            newExpr,
            stateParam);

        var factoryDelegate = factoryLambda.Compile();

        // Build state getter delegate
        var collectionParam = Expression.Parameter(typeToConvert);

        var getterExpr = Expression.Property(collectionParam, stateProp);

        var getterLambda = Expression.Lambda(
            typeof(Func<,>).MakeGenericType(typeToConvert, stateType),
            getterExpr,
            collectionParam);

        var getterDelegate = getterLambda.Compile();

        return (JsonConverter)Activator.CreateInstance(
            converterType,
            factoryDelegate,
            getterDelegate);
    }
}