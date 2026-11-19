//=======================================================================
// SwiftBiDictionary.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Chronicler;
using Chronicler.Diagnostics;
using Chronicler.Serialization;
using MemoryPack;
using SwiftCollections.Utility;

namespace SwiftCollections;

/// <summary>
/// Represents a bidirectional dictionary that allows for efficient lookups in both directions, mapping keys to
/// values and values back to keys. Both keys and values must be unique to maintain the integrity of the
/// bidirectional relationship. Inherits from <see cref="T:SwiftCollections.SwiftDictionary`2" /> and maintains a
/// reverse map for reverse lookups.
/// </summary>
/// <typeparam name="T1">The type of the keys in the forward dictionary.</typeparam>
/// <typeparam name="T2">The type of the values in the forward dictionary.</typeparam>
/// <remarks>
/// Instances require external synchronization; keys and elements must keep equality/hash identity stable while
/// stored. Copies are shallow. Enumeration follows hash slots, not insertion or sorted order. Resize, trim, and
/// comparer changes can reorder entries. Recognized string/object comparers may switch to randomized
/// collision-hardening after long probes; cross-process iteration order is not guaranteed. Custom comparers
/// define their own determinism. The reverse-map lock does not make the entire bidirectional dictionary
/// thread-safe. Mutations through base-typed APIs can bypass reverse-map maintenance; use the bidirectional
/// members consistently.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftBiDictionary<T1, T2> : SwiftDictionary<T1, T2>, IStateBacked<SwiftDictionaryState<T1, T2>>
    where T1 : notnull
    where T2 : notnull
{
    #region Fields

    /// <summary>
    /// The reverse map for bidirectional lookup, mapping from <typeparamref name="T2"/> to <typeparamref name="T1"/>.
    /// </summary>
    private SwiftDictionary<T2, T1> _reverseMap;

    /// <summary>
    /// The comparer used to determine equality of keys and to generate hash codes.
    /// </summary>
    [NonSerialized]
    private IEqualityComparer<T2> _reverseComparer;

    /// <summary>
    /// An object used to synchronize access to the reverse map during serialization and deserialization.
    /// </summary>
    [NonSerialized]
    private object? _reverseSyncRoot;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftBiDictionary`2" /> class that is
    /// empty and uses the default equality comparers for the key and value types.
    /// </summary>
    public SwiftBiDictionary() : this(null, null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftBiDictionary`2" /> class that is
    /// empty and uses the specified equality comparers for the key and value types.
    /// </summary>
    /// <param name="comparer1">The key equality comparer, retained by reference. Null selects the Swift
    /// default.</param>
    /// <param name="comparer2">The reverse-value equality comparer, retained by reference. Null selects the Swift
    /// default.</param>
    public SwiftBiDictionary(IEqualityComparer<T1>? comparer1, IEqualityComparer<T2>? comparer2) : base(DefaultCapacity, comparer1)
    {
        _reverseComparer = SwiftHashTools.GetDefaultEqualityComparer(comparer2);
        _reverseMap = new SwiftDictionary<T2, T1>(DefaultCapacity, _reverseComparer);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftBiDictionary`2" /> class that
    /// contains elements copied from the specified dictionary and uses the default equality comparers for the key
    /// and value types.
    /// </summary>
    /// <param name="dictionary">The non-null source to enumerate and shallow-copy; source storage is not
    /// retained. Equal duplicates keep the first inserted mapping.</param>
    /// <exception cref="T:System.ArgumentNullException">The source is null.</exception>
    public SwiftBiDictionary(IDictionary<T1, T2> dictionary) : this(dictionary, null, null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftBiDictionary`2" /> class that
    /// contains elements copied from the specified dictionary and uses the specified equality comparers for the
    /// key and value types.
    /// </summary>
    /// <param name="dictionary">The non-null source to enumerate and shallow-copy; source storage is not
    /// retained. Equal duplicates keep the first inserted mapping.</param>
    /// <param name="comparer1">The key equality comparer, retained by reference. Null selects the Swift
    /// default.</param>
    /// <param name="comparer2">The reverse-value equality comparer, retained by reference. Null selects the Swift
    /// default.</param>
    /// <exception cref="T:System.ArgumentNullException">The source is null.</exception>
    public SwiftBiDictionary(IDictionary<T1, T2> dictionary, IEqualityComparer<T1>? comparer1, IEqualityComparer<T2>? comparer2)
        : this(comparer1, comparer2)
    {
        ChronicleGuard.ThrowIfNull(dictionary, nameof(dictionary));

        foreach (var kvp in dictionary)
            Add(kvp.Key, kvp.Value);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftBiDictionary`2" /> class with the
    /// specified <see cref="T:SwiftCollections.SwiftDictionaryState`2" />.
    /// </summary>
    /// <param name="state">The state whose non-null Items array is copied and indexed. Comparer and table layout
    /// are not retained.</param>
    /// <exception cref="T:System.NullReferenceException">state.Items is null, including a default
    /// state.</exception>
    /// <remarks>
    /// Shallow-copies items and reconstructs Swift default equality for both directions. Pairs are inserted in
    /// state-array order; a pair whose key or reverse value already exists is skipped. Custom comparers are not
    /// restored. To preserve them, create an empty dictionary with both desired comparers, then add each state
    /// pair. Changing comparers after restore cannot recover skipped pairs.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftBiDictionary(SwiftDictionaryState<T1, T2> state)
    {
        State = state;

        ChronicleGuard.ThrowIfNull(_reverseMap, nameof(_reverseMap));
        ChronicleGuard.ThrowIfNull(_reverseComparer, nameof(_reverseComparer));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets an object that can be used to synchronize access to the reverse collection.
    /// </summary>
    /// <value>A lazily allocated lock object. Callers must coordinate all related access themselves.</value>
    /// <remarks>
    /// Use this object to lock the reverse collection during multithreaded operations to ensure thread safety.
    /// This property is intended for advanced scenarios where manual synchronization is required.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public object ReverseSyncRoot => _reverseSyncRoot ??= new object();

    /// <summary>
    /// Gets or sets the value associated with the specified key. Ensures that each value is unique within the
    /// collection.
    /// </summary>
    /// <param name="key">The key to locate or store. Null is rejected.</param>
    /// <returns>The value associated with the specified key.</returns>
    /// <value>The stored value for key; assignment replaces an existing value or inserts an absent key.</value>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">No matching key exists on
    /// get.</exception>
    /// <exception cref="T:System.ArgumentNullException">key or a required reverse value is null.</exception>
    /// <exception cref="T:System.ArgumentException">The assigned value already belongs to another
    /// key.</exception>
    /// <remarks>
    /// Setting a value that already exists in the collection will result in an exception, as each value must be
    /// unique. If the key does not exist, a new key-value pair is added. If the key exists and the value is
    /// unchanged, no operation is performed.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public new T2 this[T1 key]
    {
        get => base[key];
        set
        {
            ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));

            lock (ReverseSyncRoot)
            {
                if (TryGetValue(key, out T2 oldValue))
                {
                    // No change
                    if (_reverseComparer.Equals(oldValue, value))
                        return;

                    // Prevent duplicate values
                    ChronicleGuard.ThrowIfArgument(_reverseMap.ContainsKey(value), nameof(value), "Value already exists");

                    // Remove old reverse mapping
                    _reverseMap.Remove(oldValue);

                    // Update forward value
                    int index = FindEntry(key);
                    _entries[index].Value = value;

                    // Insert new reverse mapping
                    _reverseMap.Add(value, key);

                    _version++;
                }
                else
                {
                    // New insert
                    ChronicleGuard.ThrowIfArgument(_reverseMap.ContainsKey(value), nameof(value), "Value already exists");

                    CheckLoadThreshold();

                    bool added = InsertIfNotExist(key, value);

                    if (added)
                        _reverseMap.Add(value, key);
                }
            }
        }
    }

    /// <summary>
    /// Gets a shallow item snapshot for serialization.
    /// </summary>
    /// <value>A state containing copied live items in increasing hash-slot order; reference-type elements remain
    /// shared.</value>
    /// <remarks>
    /// Restore rebuilds storage and Swift default equality in both directions; capacity, hash slots, custom
    /// comparers and version are not serialized. Pairs with an already inserted equal key or reverse value are
    /// skipped. To preserve custom equality, create an empty dictionary with both desired comparers before
    /// adding the snapshot pairs; applying comparers after restore cannot recover skipped pairs.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public new SwiftDictionaryState<T1, T2> State
    {
        get => base.State;
        internal set
        {
            _reverseComparer = SwiftHashTools.GetDefaultEqualityComparer<T2>();
            _reverseMap = new SwiftDictionary<T2, T1>(value.Items.Length, _reverseComparer);

            base.State = value;

            lock (ReverseSyncRoot)
            {
                _reverseMap.Clear();

                foreach (var kv in this)
                    _reverseMap.Add(kv.Value, kv.Key);
            }
        }
    }

    #endregion

    #region Collection Manipulation

    /// <summary>
    /// Removes the key-value pair from the <see cref="T:SwiftCollections.SwiftBiDictionary`2" />. Also removes
    /// the corresponding value-key pair from the reverse map.
    /// </summary>
    /// <param name="key">The key to locate or store. Null is rejected.</param>
    /// <param name="value">The element or value; stored reference values are not cloned.</param>
    /// <returns>true if the matching key/value pair was removed; false if absent, with contents unchanged.
    /// </returns>
    /// <exception cref="T:System.ArgumentNullException">The required key, element or reverse value is
    /// null.</exception>
    public bool Remove(T1 key, T2 value)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));
        ChronicleGuard.ThrowIfNullGeneric(value, nameof(value));

        if (TryGetValue(key, out T2 existingValue))
        {
            if (_reverseComparer.Equals(existingValue, value))
            {
                base.Remove(key);
                lock (ReverseSyncRoot)
                    _reverseMap.Remove(value);
                return true;
            }
        }
        return false;
    }

    #region Overrides

    /// <summary>
    /// Adds the specified key and value to the <see cref="T:SwiftCollections.SwiftBiDictionary`2" />. Also adds
    /// the value-key pair to the reverse map.
    /// </summary>
    /// <param name="key">The key to locate or store. Null is rejected.</param>
    /// <param name="value">The element or value; stored reference values are not cloned.</param>
    /// <returns>true if the new one-to-one mapping was inserted; false if either key or reverse value already
    /// exists, without mutation or resizing.</returns>
    /// <exception cref="T:System.ArgumentNullException">A required key or element is null.</exception>
    public override bool Add(T1 key, T2 value)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));
        ChronicleGuard.ThrowIfNullGeneric(value, nameof(value));

        if (ContainsKey(key))
            return false;

        lock (ReverseSyncRoot)
        {
            if (_reverseMap.ContainsKey(value))
                return false;

            bool added = base.Add(key, value);

            if (added)
                _reverseMap.Add(value, key);

            return added;
        }
    }

    /// <summary>
    /// Overrides the base class's <see cref="SwiftDictionary{TKey, TValue}.InsertIfNotExist"/> method to ensure synchronization with the reverse map.
    /// </summary>
    /// <param name="key">The key to insert.</param>
    /// <param name="value">The value to insert.</param>
    /// <returns><c>true</c> if a new entry was added; otherwise, <c>false</c>.</returns>
    internal override bool InsertIfNotExist(T1 key, T2 value)
    {
        lock (ReverseSyncRoot)
        {
            if (_reverseMap.ContainsKey(value))
                return false;

            bool result = base.InsertIfNotExist(key, value);

            if (result)
                _reverseMap.Add(value, key);

            return result;
        }
    }

    /// <summary>
    /// Removes the value with the specified key from the <see cref="T:SwiftCollections.SwiftBiDictionary`2" />.
    /// Also removes the corresponding key from the reverse map.
    /// </summary>
    /// <param name="key">The key to locate or store. Null is rejected.</param>
    /// <returns>true if the matching entry was removed; false if absent, with contents unchanged. </returns>
    /// <exception cref="T:System.ArgumentNullException">The required key, element or reverse value is
    /// null.</exception>
    public override bool Remove(T1 key)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));

        if (TryGetValue(key, out T2 value))
        {
            base.Remove(key);
            lock (ReverseSyncRoot)
                _reverseMap.Remove(value);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Removes all keys and values from the <see cref="T:SwiftCollections.SwiftBiDictionary`2" />. Also clears
    /// the reverse map.
    /// </summary>
    /// <remarks>
    /// Removes live entries while preserving capacity. Ordinary clearing resets active table metadata and stored
    /// references.
    /// </remarks>
    public override void Clear()
    {
        base.Clear();
        lock (ReverseSyncRoot)
            _reverseMap.Clear();
    }

    #endregion

    #endregion

    #region Utility Methods

    /// <summary>
    /// Sets the comparers used to determine equality for keys and values in the dictionary.
    /// </summary>
    /// <param name="comparer1">The key equality comparer, retained by reference. Must be non-null and must not
    /// collapse previously distinct stored keys or values.</param>
    /// <param name="comparer2">The reverse-value equality comparer, retained by reference. Must be non-null and
    /// must not collapse previously distinct stored keys or values.</param>
    /// <exception cref="T:System.ArgumentNullException">A supplied comparer is null.</exception>
    /// <remarks>
    /// Retains the new comparer and rehashes existing entries without deduplicating newly equal keys. Changing
    /// equality must preserve distinction among existing keys and reverse values. Comparer exceptions can leave
    /// partial changes; there is no rollback. Passing both current comparer instances does nothing. This does
    /// not recover pairs discarded during an earlier state restore using different equality.
    /// </remarks>
    public void SetComparer(IEqualityComparer<T1> comparer1, IEqualityComparer<T2> comparer2)
    {
        ChronicleGuard.ThrowIfNull(comparer1, nameof(comparer1));
        ChronicleGuard.ThrowIfNull(comparer2, nameof(comparer2));
        if (ReferenceEquals(comparer1, _comparer) && ReferenceEquals(comparer2, _reverseComparer))
            return;

        lock (ReverseSyncRoot)
        {
            var newReverseMap = new SwiftDictionary<T2, T1>(Count, comparer2);
            foreach (var kv in this)
                newReverseMap.Add(kv.Value, kv.Key);

            _reverseMap = newReverseMap;
            _reverseComparer = comparer2;

            base.SetComparer(comparer1);
        }
    }

    /// <summary>
    /// Attempts to get the key associated with the specified value.
    /// </summary>
    /// <param name="value">The element or value; stored reference values are not cloned.</param>
    /// <param name="key">Receives the matching forward key on success; default(T1) on failure.</param>
    /// <returns>true if a matching entry was found and the out value is valid; false if absent, with the out
    /// value set to default. No mutation.</returns>
    /// <exception cref="T:System.ArgumentNullException">value is null.</exception>
    public bool TryGetKey(T2 value, out T1 key)
    {
        ChronicleGuard.ThrowIfNullGeneric(value, nameof(value));

        lock (ReverseSyncRoot)
            return _reverseMap.TryGetValue(value, out key);
    }

    /// <summary>
    /// Gets the key associated with the specified value.
    /// </summary>
    /// <param name="value">The element or value; stored reference values are not cloned.</param>
    /// <returns>The forward key associated with the supplied reverse value.</returns>
    /// <exception cref="T:System.ArgumentNullException">value is null.</exception>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">value has no reverse
    /// mapping.</exception>
    public T1 GetKey(T2 value)
    {
        ChronicleGuard.ThrowIfNullGeneric(value, nameof(value));

        lock (ReverseSyncRoot)
            return _reverseMap[value];
    }

    /// <summary>
    /// Determines whether the <see cref="T:SwiftCollections.SwiftBiDictionary`2" /> contains the specified value.
    /// </summary>
    /// <param name="value">The element or value; stored reference values are not cloned.</param>
    /// <returns>true if a matching live entry exists; otherwise false. No mutation.</returns>
    /// <exception cref="T:System.ArgumentNullException">value is null.</exception>
    public bool ContainsValue(T2 value)
    {
        ChronicleGuard.ThrowIfNullGeneric(value, nameof(value));

        lock (ReverseSyncRoot)
            return _reverseMap.ContainsKey(value);
    }

    #endregion
}
