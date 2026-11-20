//=======================================================================
// SwiftComparerRegistryBuilder.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Chronicler.Diagnostics;

namespace SwiftCollections.Serialization;

/// <summary>Builds explicit typed ordering and equality/hash persistence rules.</summary>
/// <remarks>
/// Configure this builder before serialization. Build creates an independent registry snapshot; subsequent
/// registrations do not change it. Builders require external synchronization.
/// </remarks>
public sealed class SwiftComparerRegistryBuilder
{
    private readonly SwiftDictionary<(Type Type, string Id, ushort Version), object> _codecs = new();
    private readonly SwiftDictionary<Type, SwiftDictionary<object, object>> _sources = new();

    /// <summary>Registers an exact comparer instance and its configuration codec.</summary>
    /// <typeparam name="T">The declared element type whose ordering is persisted.</typeparam>
    /// <param name="id">A stable application identifier of 1–128 lowercase ASCII letters, digits, dots,
    /// underscores, hyphens or slashes. The lsf. prefix is reserved.</param>
    /// <param name="version">A nonzero semantic codec version.</param>
    /// <param name="comparer">The source instance, retained and matched by reference rather than equality.</param>
    /// <param name="encode">Captures its current configuration in at most 4,096 bytes. Returned storage is
    /// copied. Callback exceptions propagate when saving.</param>
    /// <param name="decode">Reconstructs a fresh immutable comparer snapshot, or the explicitly registered
    /// singleton with matching configuration, and reports all consumed bytes. It must not return an ambient
    /// shared instance. Configuration is read-only; retain an owned copy if needed. Factory exceptions become
    /// the inner exception of an InvalidOperationException when loading.</param>
    /// <returns>This builder for further registrations.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">The identifier/version is invalid, the typed identity or instance is
    /// already registered, or the instance has a supported canonical built-in descriptor.</exception>
    public SwiftComparerRegistryBuilder RegisterOrdering<T>(string id, ushort version, IComparer<T> comparer,
        Func<IComparer<T>, ReadOnlyMemory<byte>> encode,
        Func<ReadOnlyMemory<byte>, (IComparer<T> Comparer, int BytesConsumed)> decode)
        => Register(id, version, comparer, encode, decode,
            SwiftComparerRegistry.TryCaptureBuiltInOrdering(comparer, out _));

    /// <summary>Registers an exact equality comparer instance and its configuration codec.</summary>
    /// <typeparam name="T">The declared key or element type whose equality and hashing are persisted.</typeparam>
    /// <param name="id">A stable application identifier of 1–128 lowercase ASCII letters, digits, dots,
    /// underscores, hyphens or slashes. The lsf. prefix is reserved.</param>
    /// <param name="version">A nonzero semantic codec version.</param>
    /// <param name="comparer">The source instance, retained and matched by reference rather than equality.</param>
    /// <param name="encode">Captures its current configuration in at most 4,096 bytes. Returned storage is
    /// copied. Callback exceptions propagate when saving.</param>
    /// <param name="decode">Reconstructs a fresh immutable comparer snapshot, or the explicitly registered
    /// singleton with matching configuration, and reports all consumed bytes. It must not return an ambient
    /// shared instance. Configuration is read-only; retain an owned copy if needed. Factory exceptions become
    /// the inner exception of an InvalidOperationException when loading.</param>
    /// <returns>This builder for further registrations.</returns>
    /// <exception cref="ArgumentNullException">A required argument is null.</exception>
    /// <exception cref="ArgumentException">The identifier/version is invalid, the typed equality identity or
    /// instance is already registered, or the instance has a supported canonical built-in descriptor.</exception>
    /// <remarks>
    /// One typed equality registration serves dictionary keys, set elements and both reverse-value roles.
    /// Collection adapters validate the required wire roles separately. Ordering registrations use a distinct
    /// contract even when the same object implements both comparer interfaces.
    /// </remarks>
    public SwiftComparerRegistryBuilder RegisterEquality<T>(string id, ushort version, IEqualityComparer<T> comparer,
        Func<IEqualityComparer<T>, ReadOnlyMemory<byte>> encode,
        Func<ReadOnlyMemory<byte>, (IEqualityComparer<T> Comparer, int BytesConsumed)> decode)
        => Register(id, version, comparer, encode, decode,
            SwiftComparerRegistry.TryCaptureBuiltInEquality(comparer, out _));

    private SwiftComparerRegistryBuilder Register<TComparer>(string id, ushort version, TComparer comparer,
        Func<TComparer, ReadOnlyMemory<byte>> encode,
        Func<ReadOnlyMemory<byte>, (TComparer Comparer, int BytesConsumed)> decode, bool builtIn)
        where TComparer : class
    {
        SwiftComparerRegistry.ValidateId(id);
        ChronicleGuard.ThrowIfNull(comparer, nameof(comparer));
        ChronicleGuard.ThrowIfNull(encode, nameof(encode));
        ChronicleGuard.ThrowIfNull(decode, nameof(decode));
        ChronicleGuard.ThrowIfArgument(version == 0, nameof(version), "A codec version must be nonzero.");
        ChronicleGuard.ThrowIfArgument(id.StartsWith("lsf.", StringComparison.Ordinal), nameof(id),
            "The lsf. identifier prefix is reserved for built-in rules.");
        ChronicleGuard.ThrowIfArgument(builtIn,
            nameof(comparer), "Supported built-in comparers retain their canonical descriptors.");

        var key = (typeof(TComparer), id, version);
        ChronicleGuard.ThrowIfArgument(_codecs.ContainsKey(key), nameof(id),
            "This typed codec identifier and version are already registered.");
        if (!_sources.TryGetValue(typeof(TComparer), out SwiftDictionary<object, object>? instances))
        {
            instances = new SwiftDictionary<object, object>(SwiftDictionary<object, object>.DefaultCapacity,
                ReferenceComparer.Instance);
            _sources.Add(typeof(TComparer), instances);
        }
        ChronicleGuard.ThrowIfArgument(instances.ContainsKey(comparer), nameof(comparer),
            "This exact typed comparer instance is already registered.");

        var registration = new SwiftComparerRegistration<TComparer>(id, version, encode, decode);
        _codecs.Add(key, registration);
        instances.Add(comparer, registration);
        return this;
    }

    /// <summary>Creates an independent registry with the current immutable registration definitions.</summary>
    /// <returns>A registry retaining the registered instances and callbacks. Produced comparer associations
    /// belong only to this new registry.</returns>
    public SwiftComparerRegistry Build() => new(_codecs, _sources);

    private sealed class ReferenceComparer : IEqualityComparer<object>
    {
        internal static readonly ReferenceComparer Instance = new();
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }
}
