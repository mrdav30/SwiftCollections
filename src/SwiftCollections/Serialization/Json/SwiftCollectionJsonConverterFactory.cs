//=======================================================================
// SwiftCollectionJsonConverterFactory.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using SwiftCollections.Observable;

namespace SwiftCollections.Serialization;

/// <summary>
/// Creates versioned comparer-aware JSON converters for supported concrete collection types.
/// </summary>
/// <remarks>
/// Supports exact closed sorted-list, dictionary, hash-set, bidictionary and observable-dictionary types. Register an instance in
/// serializer options to supply custom comparison definitions. Generic converter activation uses reflection;
/// hosts requiring closed construction can use <see cref="SwiftCollectionJsonConverters.ForSortedList{T}(SwiftComparerRegistry)"/>.
/// Full derived and interface-typed contracts require host integration. An explicitly declared
/// SwiftSortedList base contract persists its base items and ordering only, even when the instance is derived.
/// </remarks>
public sealed class SwiftCollectionJsonConverterFactory : JsonConverterFactory
{
    private readonly SwiftComparerRegistry _registry;

    /// <summary>
    /// Creates a factory supporting built-in comparison rules.
    /// </summary>
    public SwiftCollectionJsonConverterFactory() : this(SwiftComparerRegistry.Empty) { }

    /// <summary>
    /// Creates a factory retaining the supplied immutable comparison definitions.
    /// </summary>
    /// <param name="registry">The registry used by every converter this factory creates.</param>
    /// <exception cref="ArgumentNullException"><paramref name="registry"/> is null.</exception>
    public SwiftCollectionJsonConverterFactory(SwiftComparerRegistry registry)
    {
        _registry = registry ?? throw new ArgumentNullException(nameof(registry));
    }

    /// <summary>
    /// Reports whether the requested type is an exact closed comparer-aware owner.
    /// </summary>
    /// <param name="typeToConvert">The runtime serialization contract to inspect.</param>
    /// <returns>True for a supported exact closed collection; otherwise false.</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        if (!typeToConvert.IsGenericType || typeToConvert.ContainsGenericParameters) return false;
        Type definition = typeToConvert.GetGenericTypeDefinition();
        return definition == typeof(SwiftSortedList<>) || definition == typeof(SwiftDictionary<,>)
            || definition == typeof(SwiftHashSet<>) || definition == typeof(SwiftBiDictionary<,>)
            || definition == typeof(SwiftObservableDictionary<,>);
    }

    /// <summary>
    /// Constructs a converter for the supported closed owner using the retained registry.
    /// </summary>
    /// <param name="typeToConvert">The exact supported closed collection type.</param>
    /// <param name="options">The requesting serializer options; they are not retained or modified.</param>
    /// <returns>A converter bound to the element type and retained registry.</returns>
    /// <exception cref="NotSupportedException"><paramref name="typeToConvert"/> is not a supported closed owner.</exception>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (!CanConvert(typeToConvert)) throw new NotSupportedException("The type is not a supported exact closed collection.");
        Type definition = typeToConvert.GetGenericTypeDefinition();
        string creator = definition == typeof(SwiftSortedList<>) ? nameof(SwiftCollectionJsonConverters.ForSortedList)
            : definition == typeof(SwiftDictionary<,>) ? nameof(SwiftCollectionJsonConverters.ForDictionary)
            : definition == typeof(SwiftHashSet<>) ? nameof(SwiftCollectionJsonConverters.ForHashSet)
            : definition == typeof(SwiftBiDictionary<,>) ? nameof(SwiftCollectionJsonConverters.ForBiDictionary)
            : nameof(SwiftCollectionJsonConverters.ForObservableDictionary);
        MethodInfo method = typeof(SwiftCollectionJsonConverters).GetMethod(creator)!;
        return (JsonConverter)method.MakeGenericMethod(typeToConvert.GetGenericArguments()).Invoke(null, new object[] { _registry })!;
    }
}
