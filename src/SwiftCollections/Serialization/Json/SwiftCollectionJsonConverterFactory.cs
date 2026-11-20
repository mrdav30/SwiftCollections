//=======================================================================
// SwiftCollectionJsonConverterFactory.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SwiftCollections.Serialization;

/// <summary>
/// Creates versioned comparer-aware JSON converters for supported concrete collection types.
/// </summary>
/// <remarks>
/// This implementation supports exact closed <see cref="SwiftSortedList{T}"/> types. Register an instance in
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
    /// Reports whether the requested type is an exact closed sorted-list owner.
    /// </summary>
    /// <param name="typeToConvert">The runtime serialization contract to inspect.</param>
    /// <returns>True for an exact closed <see cref="SwiftSortedList{T}"/>; otherwise false.</returns>
    public override bool CanConvert(Type typeToConvert)
    {
        return typeToConvert.IsGenericType && !typeToConvert.ContainsGenericParameters
            && typeToConvert.GetGenericTypeDefinition() == typeof(SwiftSortedList<>);
    }

    /// <summary>
    /// Constructs a converter for the supported closed owner using the retained registry.
    /// </summary>
    /// <param name="typeToConvert">The exact closed sorted-list type.</param>
    /// <param name="options">The requesting serializer options; they are not retained or modified.</param>
    /// <returns>A converter bound to the element type and retained registry.</returns>
    /// <exception cref="NotSupportedException"><paramref name="typeToConvert"/> is not a supported closed owner.</exception>
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (!CanConvert(typeToConvert)) throw new NotSupportedException("Only exact closed SwiftSortedList types are supported.");
        Type converterType = typeof(SwiftSortedListJsonConverter<>).MakeGenericType(typeToConvert.GetGenericArguments());
        return (JsonConverter)Activator.CreateInstance(converterType, new object[] { _registry })!;
    }
}
