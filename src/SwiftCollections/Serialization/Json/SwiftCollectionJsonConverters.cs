//=======================================================================
// SwiftCollectionJsonConverters.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System.Text.Json.Serialization;

namespace SwiftCollections.Serialization;

/// <summary>
/// Creates comparer-aware converters through explicit closed generic construction.
/// </summary>
public static class SwiftCollectionJsonConverters
{
    /// <summary>
    /// Creates a sorted-list converter without reflective generic converter activation.
    /// </summary>
    /// <typeparam name="T">The list element type and comparison contract.</typeparam>
    /// <param name="registry">The immutable comparison definitions retained by the converter.</param>
    /// <returns>A converter for exact closed sorted-list owners using the supplied registry.</returns>
    /// <exception cref="System.ArgumentNullException"><paramref name="registry"/> is null.</exception>
    /// <remarks>
    /// Register the result in JSON options. AOT hosts must additionally supply raw-state and element metadata
    /// and preserve the concrete comparer factories they use.
    /// </remarks>
    public static JsonConverter<SwiftSortedList<T>> ForSortedList<T>(SwiftComparerRegistry registry)
    {
        return new SwiftSortedListJsonConverter<T>(registry);
    }
}
