using MemoryPack;
using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>
/// Represents a bidirectional dictionary that allows for efficient lookups in both directions,
/// mapping keys to values and values back to keys. Both keys and values must be unique to maintain
/// the integrity of the bidirectional relationship.
/// Inherits from <see cref="SwiftDictionary{TKey, TValue}"/> and maintains a reverse map for reverse lookups.
/// </summary>
/// <typeparam name="T1">The type of the keys in the forward dictionary.</typeparam>
/// <typeparam name="T2">The type of the values in the forward dictionary.</typeparam>
/// <remarks>
/// The comparer is not serialized. After deserialization the dictionary uses
/// <see cref="EqualityComparer{T1}.Default"/> for keys and <see cref="EqualityComparer{T2}.Default"/> for values.
/// 
/// If a custom comparer is required it can be reapplied using
/// <see cref="SetComparer(IEqualityComparer{T1}, IEqualityComparer{T2})"/>.
/// </remarks>
[Serializable]
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftBiDictionary<T1, T2> : SwiftDictionary<T1, T2>
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
    private object _reverseSyncRoot;

    #endregion

    #region Constructors

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftBiDictionary{T1, T2}"/> class that is empty and uses the default equality comparers for the key and value types.
    /// </summary>
    public SwiftBiDictionary() : this(null, null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftBiDictionary{T1, T2}"/> class that is empty and uses the specified equality comparers for the key and value types.
    /// </summary>
    /// <param name="comparer1">The comparer to use for the keys.</param>
    /// <param name="comparer2">The comparer to use for the values.</param>
    public SwiftBiDictionary(IEqualityComparer<T1> comparer1, IEqualityComparer<T2> comparer2) : base(DefaultCapacity, comparer1)
    {
        _reverseComparer = comparer2 ?? EqualityComparer<T2>.Default;
        _reverseMap = new SwiftDictionary<T2, T1>(DefaultCapacity, _reverseComparer);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftBiDictionary{T1, T2}"/> class that contains elements copied from the specified dictionary and uses the default equality comparers for the key and value types.
    /// </summary>
    /// <param name="dictionary">The dictionary whose elements are copied to the new <see cref="SwiftBiDictionary{T1, T2}"/>.</param>
    public SwiftBiDictionary(IDictionary<T1, T2> dictionary) : this(dictionary, null, null) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftBiDictionary{T1, T2}"/> class that contains elements copied from the specified dictionary and uses the specified equality comparers for the key and value types.
    /// </summary>
    /// <param name="dictionary">The dictionary whose elements are copied to the new <see cref="SwiftBiDictionary{T1, T2}"/>.</param>
    /// <param name="comparer1">The comparer to use for the keys.</param>
    /// <param name="comparer2">The comparer to use for the values.</param>
    public SwiftBiDictionary(IDictionary<T1, T2> dictionary, IEqualityComparer<T1> comparer1, IEqualityComparer<T2> comparer2)
        : this(comparer1, comparer2)
    {
        ThrowHelper.ThrowIfNull(dictionary, nameof(dictionary));

        foreach (var kvp in dictionary)
            Add(kvp.Key, kvp.Value);
    }

    ///  <summary>
    ///  Initializes a new instance of the <see cref="SwiftBiDictionary{T1, T2}"/> class with the specified <see cref="SwiftDictionaryState{T1, T2}"/>.
    ///  </summary>
    ///  <param name="state">The state containing the internal array, count, offset, and version for initialization.</param>
    [MemoryPackConstructor]
    public SwiftBiDictionary(SwiftDictionaryState<T1, T2> state)
    {
        State = state;
    }

    #endregion

    #region Properties

    [JsonIgnore]
    [MemoryPackIgnore]
    public object ReverseSyncRoot => _reverseSyncRoot ??= new object();

    [JsonIgnore]
    [MemoryPackIgnore]
    public new T2 this[T1 key]
    {
        get => base[key];

        set
        {
            ThrowHelper.ThrowIfNull(key, nameof(key));

            lock (ReverseSyncRoot)
            {
                if (TryGetValue(key, out T2 oldValue))
                {
                    // No change
                    if (_reverseComparer.Equals(oldValue, value))
                        return;

                    // Prevent duplicate values
                    if (_reverseMap.ContainsKey(value))
                        throw new ArgumentException("Value already exists", nameof(value));

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
                    if (_reverseMap.ContainsKey(value))
                        throw new ArgumentException("Value already exists", nameof(value));

                    CheckLoadThreshold();

                    bool added = InsertIfNotExist(key, value);

                    if (added)
                        _reverseMap.Add(value, key);
                }
            }
        }
    }

    [JsonInclude]
    [MemoryPackInclude]
    public new SwiftDictionaryState<T1, T2> State
    {
        get => base.State;

        internal set
        {
            _reverseComparer = EqualityComparer<T2>.Default;
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
    /// Removes the key-value pair from the <see cref="SwiftBiDictionary{T1, T2}"/>.
    /// Also removes the corresponding value-key pair from the reverse map.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <param name="value">The value of the element to remove.</param>
    /// <returns><c>true</c> if the element is successfully found and removed; otherwise, <c>false</c>.</returns>
    public bool Remove(T1 key, T2 value)
    {
        ThrowHelper.ThrowIfNull(key, nameof(key));
        ThrowHelper.ThrowIfNull(value, nameof(value));

        if (TryGetValue(key, out T2 existingValue))
        {
            if (_reverseComparer.Equals(existingValue, value))
            {
                bool removed = base.Remove(key);
                if (removed)
                {
                    lock (ReverseSyncRoot)
                        _reverseMap.Remove(value);
                }
                return removed;
            }
        }
        return false;
    }

    #region Overrides

    /// <summary>
    /// Adds the specified key and value to the <see cref="SwiftBiDictionary{T1, T2}"/>.
    /// Also adds the value-key pair to the reverse map.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <exception cref="ArgumentException">An element with the same key or value already exists.</exception>
    public override bool Add(T1 key, T2 value)
    {
        ThrowHelper.ThrowIfNull(key, nameof(key));
        ThrowHelper.ThrowIfNull(value, nameof(value));

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
    /// Removes the value with the specified key from the <see cref="SwiftBiDictionary{T1, T2}"/>.
    /// Also removes the corresponding key from the reverse map.
    /// </summary>
    /// <param name="key">The key of the element to remove.</param>
    /// <returns><c>true</c> if the element is successfully found and removed; otherwise, <c>false</c>.</returns>
    public override bool Remove(T1 key)
    {
        ThrowHelper.ThrowIfNull(key, nameof(key));

        if (TryGetValue(key, out T2 value))
        {
            bool removed = base.Remove(key);

            if (removed)
            {
                lock (ReverseSyncRoot)
                    _reverseMap.Remove(value);
            }

            return removed;
        }

        return false;
    }

    /// <summary>
    /// Removes all keys and values from the <see cref="SwiftBiDictionary{T1, T2}"/>.
    /// Also clears the reverse map.
    /// </summary>
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
    /// <remarks>This method updates the internal comparers and rebuilds the reverse mapping using the
    /// specified value comparer. The operation is thread-safe and locks the internal state during the update. Changing
    /// comparers may affect key and value lookup behavior.</remarks>
    /// <param name="comparer1">The equality comparer to use for keys of type T1. Cannot be null.</param>
    /// <param name="comparer2">The equality comparer to use for values of type T2. Cannot be null.</param>
    public void SetComparer(IEqualityComparer<T1> comparer1, IEqualityComparer<T2> comparer2)
    {
        ThrowHelper.ThrowIfNull(comparer1, nameof(comparer1));
        ThrowHelper.ThrowIfNull(comparer2, nameof(comparer2));
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
    /// <param name="value">The value whose associated key is to be retrieved.</param>
    /// <param name="key">When this method returns, contains the key associated with the specified value, if the key is found; otherwise, the default value for the type of the key.</param>
    /// <returns><c>true</c> if the key was found; otherwise, <c>false</c>.</returns>
    public bool TryGetKey(T2 value, out T1 key)
    {
        ThrowHelper.ThrowIfNull(value, nameof(value));

        lock (ReverseSyncRoot)
            return _reverseMap.TryGetValue(value, out key);
    }

    /// <summary>
    /// Gets the key associated with the specified value.
    /// </summary>
    /// <param name="value">The value whose associated key is to be retrieved.</param>
    /// <returns>The key associated with the specified value.</returns>
    /// <exception cref="KeyNotFoundException">The property is retrieved and <paramref name="value"/> does not exist in the reverse map.</exception>
    public T1 GetKey(T2 value)
    {
        ThrowHelper.ThrowIfNull(value, nameof(value));

        lock (ReverseSyncRoot)
            return _reverseMap[value];
    }

    /// <summary>
    /// Determines whether the <see cref="SwiftBiDictionary{T1, T2}"/> contains the specified value.
    /// </summary>
    /// <param name="value">The value to locate in the dictionary.</param>
    /// <returns><c>true</c> if the dictionary contains an element with the specified value; otherwise, <c>false</c>.</returns>
    public bool ContainsValue(T2 value)
    {
        ThrowHelper.ThrowIfNull(value, nameof(value));

        lock (ReverseSyncRoot)
            return _reverseMap.ContainsKey(value);
    }

    #endregion
}
