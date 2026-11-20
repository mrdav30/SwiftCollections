//=======================================================================
// SwiftCollectionJsonConverters.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using SwiftCollections.Observable;

namespace SwiftCollections.Serialization;

/// <summary>
/// Creates comparer-aware converters through explicit closed generic construction.
/// </summary>
/// <remarks>
/// Register the returned converters in serializer options. Source-generated hosts also require metadata for
/// raw state and its element/key/value types. Hash owners resolve equality before private table construction;
/// duplicate equal keys/items and invalid state fail with System.Text.Json.JsonException. No owner is published
/// on failure. Callers must synchronize capture; snapshots are shallow. Writer/element failures may leave a
/// partial destination after metadata and state capture succeed. Legacy state-only owner records are rejected.
/// </remarks>
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

    /// <summary>Creates a converter preserving a dictionary's effective key equality and hash policy.</summary>
    /// <typeparam name="TKey">The non-null key type and equality contract.</typeparam>
    /// <typeparam name="TValue">The stored value type.</typeparam>
    /// <param name="registry">The immutable definitions retained by the converter.</param>
    /// <returns>A closed dictionary converter; bidictionary instances through this base contract are rejected.</returns>
    /// <exception cref="ArgumentNullException">registry is null.</exception>
    public static JsonConverter<SwiftDictionary<TKey, TValue>> ForDictionary<TKey, TValue>(SwiftComparerRegistry registry)
        where TKey : notnull
        => new SwiftHashCollectionJsonConverter<SwiftDictionary<TKey, TValue>, SwiftDictionaryState<TKey, TValue>, IEqualityComparer<TKey>>(
            registry, new byte[] { 2 },
            static (owner, rules) =>
            {
                if (owner.HasAdditionalComparerRoles)
                    throw new NotSupportedException("A bidictionary requires its concrete persistence contract and reverse roles.");
                return (rules.CaptureEquality(owner.Comparer), default, default);
            },
            static owner => owner.State,
            static (descriptors, rules) => rules.RestoreEquality<TKey>(descriptors.First),
            static (state, comparer) => SwiftDictionary<TKey, TValue>.RestoreTrusted(state, comparer));

    /// <summary>Creates a converter preserving a hash set's effective element equality and hash policy.</summary>
    /// <typeparam name="T">The non-null element type and equality contract.</typeparam>
    /// <param name="registry">The immutable definitions retained by the converter.</param>
    /// <returns>A closed hash-set converter; null state items are skipped as in raw-state restoration.</returns>
    /// <exception cref="ArgumentNullException">registry is null.</exception>
    public static JsonConverter<SwiftHashSet<T>> ForHashSet<T>(SwiftComparerRegistry registry) where T : notnull
        => new SwiftHashCollectionJsonConverter<SwiftHashSet<T>, SwiftArrayState<T>, IEqualityComparer<T>>(
            registry, new byte[] { 3 },
            static (owner, rules) => (rules.CaptureEquality(owner.Comparer), default, default),
            static owner => owner.State,
            static (descriptors, rules) => rules.RestoreEquality<T>(descriptors.First),
            static (state, comparer) => SwiftHashSet<T>.RestoreTrusted(state, comparer));

    /// <summary>Creates a converter preserving forward, configured reverse and effective reverse equality.</summary>
    /// <typeparam name="TKey">The non-null forward key type.</typeparam>
    /// <typeparam name="TValue">The non-null reverse key type.</typeparam>
    /// <param name="registry">The immutable definitions retained by the converter.</param>
    /// <returns>A closed bidictionary converter validating all three rules before populating either map.</returns>
    /// <exception cref="ArgumentNullException">registry is null.</exception>
    public static JsonConverter<SwiftBiDictionary<TKey, TValue>> ForBiDictionary<TKey, TValue>(SwiftComparerRegistry registry)
        where TKey : notnull where TValue : notnull
        => new SwiftHashCollectionJsonConverter<SwiftBiDictionary<TKey, TValue>, SwiftDictionaryState<TKey, TValue>,
            (IEqualityComparer<TKey> Forward, IEqualityComparer<TValue> Configured, IEqualityComparer<TValue> Effective)>(
            registry, new byte[] { 2, 4, 5 },
            static (owner, rules) =>
            {
                SwiftComparerDescriptor forward = rules.CaptureEquality(owner.Comparer);
                SwiftComparerDescriptor configured = rules.CaptureEquality(owner.ConfiguredReverseComparer);
                SwiftComparerDescriptor effective = rules.CaptureEquality(owner.EffectiveReverseComparer);
                SwiftComparerRegistry.ValidateReverseEquality<TValue>(configured, effective);
                return (forward, configured, effective);
            },
            static owner => owner.State,
            static (descriptors, rules) =>
            {
                SwiftComparerRegistry.ValidateReverseEquality<TValue>(descriptors.Second, descriptors.Third);
                return (rules.RestoreEquality<TKey>(descriptors.First), rules.RestoreEquality<TValue>(descriptors.Second),
                    rules.RestoreEquality<TValue>(descriptors.Third));
            },
            static (state, comparers) => SwiftBiDictionary<TKey, TValue>.RestoreTrusted(state,
                comparers.Forward, comparers.Configured, comparers.Effective));

    /// <summary>Creates a converter preserving observable dictionary equality without restoring event subscribers.</summary>
    /// <typeparam name="TKey">The non-null key type and equality contract.</typeparam>
    /// <typeparam name="TValue">The stored value type.</typeparam>
    /// <param name="registry">The immutable definitions retained by the converter.</param>
    /// <returns>A closed converter constructing a new owner without restoration notifications.</returns>
    /// <exception cref="ArgumentNullException">registry is null.</exception>
    public static JsonConverter<SwiftObservableDictionary<TKey, TValue>> ForObservableDictionary<TKey, TValue>(SwiftComparerRegistry registry)
        where TKey : notnull
        => new SwiftHashCollectionJsonConverter<SwiftObservableDictionary<TKey, TValue>, SwiftDictionaryState<TKey, TValue>, IEqualityComparer<TKey>>(
            registry, new byte[] { 2 },
            static (owner, rules) => (rules.CaptureEquality(owner.Comparer), default, default),
            static owner => owner.State,
            static (descriptors, rules) => rules.RestoreEquality<TKey>(descriptors.First),
            static (state, comparer) => SwiftObservableDictionary<TKey, TValue>.RestoreTrusted(state, comparer));
}
