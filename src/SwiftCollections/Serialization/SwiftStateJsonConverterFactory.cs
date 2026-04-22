using System;
using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Provides a custom JsonConverterFactory for types that expose a 'State' property and 
/// a constructor accepting the state type, enabling serialization and deserialization based on the 'State' property.
/// </summary>
/// <remarks>
/// This factory is intended for use with types that encapsulate their state in a property named 'State' and 
/// can be constructed from that state. 
/// The factory creates converters that serialize the object's state and 
/// reconstruct the object during deserialization using the appropriate constructor. 
/// If the target type does not meet these requirements, converter creation will fail at runtime.
/// </remarks>
public sealed class SwiftStateJsonConverterFactory : JsonConverterFactory
{
    /// <summary>
    /// Determines whether the specified type can be converted by this converter.
    /// </summary>
    /// <remarks>
    /// This method checks for the presence of a property named "State" on the provided type,
    /// regardless of its accessibility. 
    /// Only types with such a property are considered convertible.
    /// </remarks>
    /// <param name="typeToConvert">The type to evaluate for conversion support.</param>
    /// <returns>true if the type defines a property named "State"; otherwise, false.</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        var stateProp = typeToConvert.GetProperty(
            "State",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        return stateProp != null;
    }

    /// <summary>
    /// Creates a custom JsonConverter for types that contain a 'State' property and a constructor accepting the state type.
    /// </summary>
    /// <remarks>
    /// The target type must define a property named 'State' and a constructor that takes the state type as its only parameter. 
    /// If these requirements are not met, an exception may be thrown at runtime.
    /// </remarks>
    /// <param name="typeToConvert">
    /// The type for which to create a JsonConverter. 
    /// Must have a 'State' property and a constructor that accepts the state type as a parameter.
    /// </param>
    /// <param name="options">The serialization options to use when creating the converter.</param>
    /// <returns>A JsonConverter instance capable of serializing and deserializing the specified type using its 'State' property.</returns>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        PropertyInfo stateProp = typeToConvert.GetProperty(
            "State",
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"Type '{typeToConvert}' must define a 'State' property.");

        Type stateType = stateProp.PropertyType;

        ConstructorInfo ctor = typeToConvert.GetConstructor(new[] { stateType })
            ?? throw new InvalidOperationException($"Type '{typeToConvert}' must define a constructor accepting '{stateType}'.");

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

        return (JsonConverter)(Activator.CreateInstance(converterType, factoryDelegate, getterDelegate)
            ?? throw new InvalidOperationException($"Failed to create converter instance for type '{converterType}'."));
    }
}