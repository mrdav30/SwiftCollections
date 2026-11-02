//=======================================================================
// SwiftDictionary.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Chronicler;
using Chronicler.Diagnostics;
using Chronicler.Serialization;
using MemoryPack;
using SwiftCollections.Utility;

namespace SwiftCollections;



/// <summary>
/// A high-performance, memory-efficient dictionary providing lightning-fast O(1) operations for addition, retrieval, and removal, optimized to outperform standard dictionaries.
/// </summary>
/// <typeparam name="TKey">Specifies the type of keys in the dictionary.</typeparam>
/// <typeparam name="TValue">Specifies the type of values in the dictionary.</typeparam>
/// <remarks>
/// The comparer is not serialized. After deserialization the dictionary reverts
/// to the same default comparer selection used by a new instance. String keys
/// use SwiftCollections' deterministic default comparer. Object keys use a
/// SwiftCollections comparer that hashes strings deterministically, while other
/// object-key determinism still depends on the underlying key type's
/// <see cref="object.GetHashCode()"/> implementation. Other key types use
/// <see cref="EqualityComparer{TKey}.Default"/>.
/// 
/// If a custom comparer is required it can be reapplied using
/// <see cref="SetComparer(IEqualityComparer{TKey})"/>.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftDictionary<TKey, TValue> : IStateBacked<SwiftDictionaryState<TKey, TValue>>, IDictionary<TKey, TValue>, IDictionary
    where TKey : notnull
{
    #region Constants

    /// <summary>
    /// The default initial capacity of the dictionary.
    /// </summary>
    public const int DefaultCapacity = 8;

    /// <summary>
    /// Determines the maximum allowable load factor before resizing the hash set to maintain performance.
    /// </summary>
    private const double _LoadFactorThreshold = 0.82;

    #endregion

    #region Fields

    // Skip per-operation boxing for non-nullable value keys, including in Debug builds.
    // A cold lookup must not box a default value while initializing this flag.
    private static readonly bool _KeyCanBeNull = !typeof(TKey).IsValueType
        || (typeof(TKey).IsGenericType && typeof(TKey).GetGenericTypeDefinition() == typeof(Nullable<>));

    /// <summary>
    /// The array containing the entries of the dictionary.
    /// </summary>
    protected internal Entry[] _entries;

    /// <summary>
    /// The total number of entries in the dictionary
    /// </summary>
    private int _count;

    /// <summary>
    /// The index of the last used entry in the dictionary.
    /// </summary>
    private int _lastIndex;

    /// <summary>
    /// A mask used for efficiently computing the entry arrayIndex from a hash code.
    /// This is typically the size of the entry array minus one, assuming the size is a power of two.
    /// </summary>
    private int _entryMask;

    /// <summary>
    /// The comparer used to determine equality of keys and to generate hash codes.
    /// </summary>
    protected IEqualityComparer<TKey> _comparer;

    /// <summary>
    /// Specifies the dynamic growth factor for resizing, adjusted based on recent usage patterns.
    /// </summary>
    private int _adaptiveResizeFactor;

    /// <summary>
    /// Tracks the count threshold at which the hash set should resize based on the load factor.
    /// </summary>
    private uint _nextResizeCount;

    /// <summary>
    /// Represents the moving average of the fill rate, used to dynamically adjust resizing behavior.
    /// </summary>
    private double _movingFillRate;

    /// <summary>
    /// The maximum number of steps allowed during probing to resolve collisions.
    /// </summary>
    private int _maxStepCount;

    /// <summary>
    /// A version counter used to track modifications to the dictionary.
    /// Incremented on mutations to detect changes during enumeration and ensure enumerator validity.
    /// </summary>
    [NonSerialized]
    protected uint _version;

    /// <summary>
    /// An object that can be used to synchronize access to the SwiftDictionary.
    /// </summary>
    [NonSerialized]
    private object? _syncRoot;

    #endregion

    #region Constructors

    /// <summary>
    /// Initialize a new instance of <see cref="SwiftDictionary{TKey, TValue}"/> with customizable capacity and comparer for optimal performance tailored to your needs.
    /// </summary>
    public SwiftDictionary() : this(DefaultCapacity, null) { }

    /// <inheritdoc cref="SwiftDictionary()"/>
    public SwiftDictionary(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        Initialize(capacity, comparer);

        ChronicleGuard.ThrowIfNull(_entries, nameof(_entries));
        ChronicleGuard.ThrowIfNull(_comparer, nameof(_comparer));
    }

    /// <inheritdoc cref="SwiftDictionary()"/>
    public SwiftDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey>? comparer = null)
    {
        ChronicleGuard.ThrowIfNull(dictionary, nameof(dictionary));

        Initialize(dictionary.Count, comparer);

        ChronicleGuard.ThrowIfNull(_entries, nameof(_entries));
        ChronicleGuard.ThrowIfNull(_comparer, nameof(_comparer));

        foreach (KeyValuePair<TKey, TValue> kvp in dictionary)
            InsertIfNotExist(kvp.Key, kvp.Value);
    }

    /// <inheritdoc cref="SwiftDictionary()"/>
    public SwiftDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey>? comparer = null)
    {
        ChronicleGuard.ThrowIfNull(collection, nameof(collection));

        int count = (collection as ICollection<KeyValuePair<TKey, TValue>>)?.Count ?? DefaultCapacity;
        // Dynamic padding based on collision estimation
        int size = (int)(count / _LoadFactorThreshold);
        Initialize(size, comparer);

        ChronicleGuard.ThrowIfNull(_entries, nameof(_entries));
        ChronicleGuard.ThrowIfNull(_comparer, nameof(_comparer));

        foreach (KeyValuePair<TKey, TValue> kvp in collection)
            InsertIfNotExist(kvp.Key, kvp.Value);
    }

    ///  <summary>
    ///  Initializes a new instance of the <see cref="SwiftDictionary{TKey, TValue}"/> class with the specified <see cref="SwiftDictionaryState{TKey, TValue}"/>.
    ///  </summary>
    ///  <param name="state">The state containing the internal array, count, offset, and version for initialization.</param>
    [MemoryPackConstructor]
    public SwiftDictionary(SwiftDictionaryState<TKey, TValue> state)
    {
        State = state;

        ChronicleGuard.ThrowIfNull(_entries, nameof(_entries));
        ChronicleGuard.ThrowIfNull(_comparer, nameof(_comparer));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the number of elements contained in the dictionary.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Count => _count;

    /// <summary>
    /// Gets the total number of elements that the collection can hold without resizing.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Capacity => _entries.Length;

    /// <summary>
    /// Gets the equality comparer used to determine equality of keys in the collection.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    public IEqualityComparer<TKey> Comparer => _comparer;

    /// <summary>
    /// Gets or sets the value associated with the specified key.
    /// </summary>
    /// <remarks>
    /// Getting a value with a key that does not exist will throw an exception. 
    /// Setting a value for a key that does not exist will add a new entry with the specified key and value.
    /// </remarks>
    /// <param name="key">The key whose value to get or set.</param>
    [JsonIgnore]
    [MemoryPackIgnore]
    public TValue this[TKey key]
    {
        get
        {
            int index = FindEntry(key);
            // The object-based error helper would box a value key even on a successful lookup.
            if (index < 0)
                ChronicleGuard.ThrowIfKeyInvalid(index, key);
            return _entries[index].Value;
        }
        set
        {
            int index = FindEntry(key);
            if (index >= 0)
                _entries[index].Value = value;
            else
            {
                CheckLoadThreshold();
                InsertIfNotExist(key, value);
            }
        }
    }

    /// <inheritdoc/>
    [JsonIgnore]
    [MemoryPackIgnore]
    object? IDictionary.this[object obj]
    {
        get
        {
            ChronicleGuard.ThrowIfNull(obj, nameof(obj));

            if (obj is TKey key)
            {
                int index = FindEntry(key);
                if (index >= 0) return _entries[index].Value;
            }
            return null;
        }
        set
        {
            ChronicleGuard.ThrowIfNullAndNullsAreIllegal(value, default(TValue));
            try
            {
                TKey tempKey = (TKey)obj;
                try
                {
                    this[tempKey] = (TValue)value!;
                }
                catch (InvalidCastException)
                {
                    throw new ArgumentException($"Value {value} does not match expected {typeof(TValue)}");
                }
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException($"Key {obj} does not match expected {typeof(TKey)}");
            }
        }
    }

    /// <summary>
    /// The collection containing the keys of the dictionary.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    private KeyCollection? _keyCollection;

    /// <inheritdoc/>
    [JsonIgnore]
    [MemoryPackIgnore]
    public ICollection<TKey> Keys => _keyCollection ??= new KeyCollection(this);

    [JsonIgnore]
    [MemoryPackIgnore]
    ICollection IDictionary.Keys => _keyCollection ??= new KeyCollection(this);

    /// <summary>
    /// The collection containing the values of the dictionary.
    /// </summary>
    [JsonIgnore]
    [MemoryPackIgnore]
    private ValueCollection? _valueCollection;

    /// <inheritdoc/>
    [JsonIgnore]
    [MemoryPackIgnore]
    public ICollection<TValue> Values => _valueCollection ??= new ValueCollection(this);

    [JsonIgnore]
    [MemoryPackIgnore]
    ICollection IDictionary.Values => _valueCollection ??= new ValueCollection(this);

    [JsonIgnore]
    [MemoryPackIgnore]
    bool ICollection<KeyValuePair<TKey, TValue>>.IsReadOnly => false;

    [JsonIgnore]
    [MemoryPackIgnore] bool IDictionary.IsReadOnly => false;
    bool IDictionary.IsFixedSize => false;

    [JsonIgnore]
    [MemoryPackIgnore]
    bool ICollection.IsSynchronized => false;

    /// <inheritdoc/>
    [JsonIgnore]
    [MemoryPackIgnore]
    public object SyncRoot => _syncRoot ??= new object();

    /// <summary>
    /// Gets or sets the current state of the dictionary, including all key-value pairs.
    /// </summary>
    /// <remarks>
    /// The state can be used to serialize or restore the contents of the dictionary. 
    /// Setting this property replaces the entire contents of the dictionary with the provided state. 
    /// The setter is intended for internal use and is not accessible to external callers.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public SwiftDictionaryState<TKey, TValue> State
    {
        get
        {
            if (_count == 0)
                return new SwiftDictionaryState<TKey, TValue>(Array.Empty<KeyValuePair<TKey, TValue>>());

            var items = new KeyValuePair<TKey, TValue>[_count];
            CopyTo(items, 0);

            return new SwiftDictionaryState<TKey, TValue>(items);
        }
        internal set
        {
            ChronicleGuard.ThrowIfNull(value.Items, nameof(value.Items));

            var items = value.Items;
            int count = items.Length;

            if (count == 0)
            {
                Initialize(DefaultCapacity);
                _count = 0;
                _version = 0;
                return;
            }

            int size = (int)(count / _LoadFactorThreshold);
            Initialize(size);

            foreach (var kvp in items)
                InsertIfNotExist(kvp.Key, kvp.Value);

            _version = 0;
        }
    }

    #endregion

    #region Collection Manipulation

    /// <summary>
    /// Attempts to add the specified key and value to the dictionary.
    /// </summary>
    /// <param name="key">The key of the element to add.</param>
    /// <param name="value">The value of the element to add.</param>
    /// <returns>
    /// true if the key/value pair was added to the dictionary successfully; 
    /// false if the key already exists.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public virtual bool Add(TKey key, TValue value)
    {
        CheckLoadThreshold();
        return InsertIfNotExist(key, value);
    }

    /// <inheritdoc/>
    public void Add(KeyValuePair<TKey, TValue> item) => Add(item.Key, item.Value);

    void IDictionary<TKey, TValue>.Add(TKey key, TValue value) => Add(key, value);

    /// <inheritdoc/>
    public void Add(object key, object? value)
    {
        ChronicleGuard.ThrowIfNullAndNullsAreIllegal(value, default(TValue));

        try
        {
            TKey tempKey = (TKey)key;
            try
            {
                Add(tempKey, (TValue)value!);
            }
            catch (InvalidCastException)
            {
                throw new ArgumentException($"Value {value} does not match expected {typeof(TValue)}");
            }
        }
        catch (InvalidCastException)
        {
            throw new ArgumentException($"Key {key} does not match expected {typeof(TKey)}");
        }
    }

    /// <summary>
    /// Inserts a key/value pair into the dictionary. If the key already exists and 
    /// pair is added, or the method returns false if the key already exists.
    /// </summary>
    /// <param name="key">The key to insert or update.</param>
    /// <param name="value">The value to insert or update.</param>
    /// <returns>
    /// true if the key/value pair was added to the dictionary successfully; 
    /// false if the key already exists.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when the key is null.</exception>
    internal virtual bool InsertIfNotExist(TKey key, TValue value)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));

        int hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
        int entryIndex = hashCode & _entryMask;

        int firstDeletedIndex = -1;
        int step = 1;
        // Cumulative square offsets repeat after 2 * capacity for power-of-two tables.
        // One capacity can revisit buckets before reaching every slot, including after trim.
        uint probeLimit = (uint)_entries.Length * 2;
        while ((uint)step <= probeLimit)
        {
            ref Entry entry = ref _entries[entryIndex];
            if (entry.IsUsed)
            {
                if (entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                    return false; // Item already exists
            }
            else if (entry.HashCode == -1)
            {
                if (firstDeletedIndex < 0) firstDeletedIndex = entryIndex;
            }
            else
            {
                break;
            }

            entryIndex = (entryIndex + step * step) & _entryMask; // Quadratic probing
            step++;
        }

        if (firstDeletedIndex >= 0)
            entryIndex = firstDeletedIndex;
        else if ((uint)step > probeLimit)
        {
            Resize(_entries.Length * _adaptiveResizeFactor);
            return InsertIfNotExist(key, value);
        }

        if ((uint)entryIndex > (uint)_lastIndex) _lastIndex = entryIndex;

        _entries[entryIndex].HashCode = hashCode;
        _entries[entryIndex].Key = key;
        _entries[entryIndex].Value = value;
        _entries[entryIndex].IsUsed = true;
        _count++;
        _version++;

        if ((uint)step > (uint)_maxStepCount)
        {
            _maxStepCount = step;
            if (_comparer is not IRandomedEqualityComparer &&
                _maxStepCount > 100 &&
                SwiftHashTools.IsWellKnownEqualityComparer(_comparer))
                SwitchToRandomizedComparer();  // Attempt to recompute hash code with potential randomization for better distribution
        }


        return true;
    }

    /// <inheritdoc/>
    public virtual bool Remove(TKey key)
    {
        if (_KeyCanBeNull && key is null) return false;

        int hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
        int entryIndex = hashCode & _entryMask;

        int step = 0;
        // A bucket index is not a probe distance: wrapped chains can take more steps.
        // Use the same complete cumulative-square period as insertion.
        while ((uint)step < (uint)_entries.Length * 2)
        {
            ref Entry entry = ref _entries[entryIndex];
            // Stop probing if an unused entry is found (not deleted)
            if (!entry.IsUsed && entry.HashCode != -1)
                return false;
            if (entry.IsUsed && entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
            {
                // Mark entry as deleted
                entry.IsUsed = false;
                entry.Key = default!;
                entry.Value = default!;
                entry.HashCode = -1;
                _count--;
                if ((uint)_count == 0) _lastIndex = 0;
                _version++;
                return true;
            }

            // Move to the next entry using linear probing
            step++;
            entryIndex = (entryIndex + step * step) & _entryMask;
        }
        return false; // Item not found after full loop
    }

    void IDictionary.Remove(object obj)
    {
        ChronicleGuard.ThrowIfNull(obj, nameof(obj));
        if (obj is TKey key) Remove(key);
    }

    /// <inheritdoc/>
    public bool Remove(KeyValuePair<TKey, TValue> item)
    {
        int index = FindEntry(item.Key);
        if (index >= 0 && EqualityComparer<TValue>.Default.Equals(_entries[index].Value, item.Value))
        {
            Remove(item.Key);
            return true;
        }
        return false;
    }

    /// <inheritdoc/>
    public virtual void Clear()
    {
        if ((uint)_count == 0) return;

        for (uint i = 0; i <= (uint)_lastIndex; i++)
        {
            // Clear is a full reset, not a delete; future probes must be able to stop
            // at these now-empty slots instead of treating them as tombstones.
            _entries[i].HashCode = 0;
            _entries[i].Key = default!;
            _entries[i].Value = default!;
            _entries[i].IsUsed = false;
        }

        _count = 0;
        _lastIndex = 0;
        _maxStepCount = 0;
        _movingFillRate = 0;
        _adaptiveResizeFactor = 4;

        _version++;
    }

    /// <summary>
    /// Removes all elements, optionally releasing references throughout retained backing storage.
    /// </summary>
    /// <param name="clearRetainedReferences">If true, also releases references outside the live range; otherwise uses ordinary clearing.</param>
    /// <remarks>Capacity is preserved. Full reference cleanup can take time proportional to retained capacity.</remarks>
    public void Clear(bool clearRetainedReferences)
    {
        if (clearRetainedReferences && RuntimeHelpers.IsReferenceOrContainsReferences<Entry>())
        {
            int start = _count == 0 ? 0 : _lastIndex + 1;
            Array.Clear(_entries, start, _entries.Length - start);
        }
        Clear();
    }

    #endregion

    #region Capacity Management

    /// <summary>
    /// Ensures that the dictionary is resized when the current load factor exceeds the predefined threshold.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected void CheckLoadThreshold()
    {
        if ((uint)_count >= _nextResizeCount)
            Resize(_entries.Length * _adaptiveResizeFactor);
    }

    /// <summary>
    /// Ensures that the dictionary can hold up to the specified number of entries, if not it resizes.
    /// </summary>
    /// <param name="capacity">The minimum capacity to ensure.</param>
    /// <returns>The new capacity of the dictionary.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The capacity is less than zero.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);  // Capacity must be a power of 2 for proper masking
        if (capacity > _entries.Length)
            Resize(capacity);
    }

    /// <summary>
    /// Resizes the internal arrays to the specified new size.
    /// </summary>
    /// <param name="newSize">The new size for the internal arrays.</param>
    private void Resize(int newSize)
    {
        Entry[] newEntries = new Entry[newSize];
        int newMask = newSize - 1;

        int lastIndex = 0;
        for (uint i = 0; i <= (uint)_lastIndex; i++)
        {
            if (_entries[i].IsUsed) // Only rehash valid entries
            {
                ref Entry oldEntry = ref _entries[i];
                int newIndex = oldEntry.HashCode & newMask;
                // If current entry not available, perform Quadratic probing to find the next available entry
                int step = 1;
                while (newEntries[newIndex].IsUsed)
                {
                    newIndex = (newIndex + step * step) & newMask;
                    step++;
                }
                newEntries[newIndex] = oldEntry;
                if (newIndex > lastIndex) lastIndex = newIndex;
            }
        }

        _lastIndex = lastIndex;

        CalculateAdaptiveResizeFactors(newSize);

        _entries = newEntries;
        _entryMask = newMask;

        _version++;
    }

    /// <summary>
    /// Sets the capacity of a <see cref="SwiftDictionary{TKey, TValue}"/> to the actual 
    /// number of elements it contains, rounded up to a nearby next power of 2 value.
    /// </summary>
    public void TrimExcess()
    {
        int newSize = _count <= DefaultCapacity ? DefaultCapacity : SwiftHashTools.NextPowerOfTwo(_count);
        if (newSize >= _entries.Length) return;

        Entry[] newEntries = new Entry[newSize];
        int newMask = newSize - 1;

        int lastIndex = 0;
        for (int i = 0; i <= (uint)_lastIndex; i++)
        {
            if (_entries[i].IsUsed)
            {
                ref Entry oldEntry = ref _entries[i];
                int newIndex = oldEntry.HashCode & newMask;
                // If current entry not available, perform quadratic probing to find the next available entry
                int step = 1;
                while (newEntries[newIndex].IsUsed)
                {
                    newIndex = (newIndex + step * step) & newMask;
                    step++;
                }
                newEntries[newIndex] = oldEntry;
                if (newIndex > lastIndex) lastIndex = newIndex;
            }
        }

        _lastIndex = lastIndex;

        CalculateAdaptiveResizeFactors(newSize);

        _entryMask = newMask;
        _entries = newEntries;

        _version++;
    }

    /// <summary>
    ///  Updates adaptive resize parameters based on the current fill rate to balance memory usage and performance.
    /// </summary>
    /// <param name="newSize"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CalculateAdaptiveResizeFactors(int newSize)
    {
        // Calculate current fill rate and update moving average
        double currentFillRate = (double)_count / newSize;
        _movingFillRate = _movingFillRate == 0 ? currentFillRate : (_movingFillRate * 0.7 + currentFillRate * 0.3);

        if (_movingFillRate > 0.3f)
            _adaptiveResizeFactor = 2; // Growth stabilizing
        else if (_movingFillRate < 0.28f)
            _adaptiveResizeFactor = 4; // Rapid growth

        // Reset the resize threshold based on the new size
        _nextResizeCount = (uint)(newSize * _LoadFactorThreshold);
    }

    #endregion

    #region Utility Methods

    /// <summary>
    /// Initializes the dictionary with the specified capacity.
    /// </summary>
    /// <param name="capacity">The initial number of elements that the dictionary can contain.</param>
    /// <param name="comparer">The comparer to use for the dictionary.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    [MemberNotNull(nameof(_comparer))]
    private void Initialize(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        _comparer = SwiftHashTools.GetDefaultEqualityComparer(comparer);

        int size = capacity < DefaultCapacity ? DefaultCapacity : SwiftHashTools.NextPowerOfTwo(capacity);
        _entries = new Entry[size];
        _entryMask = size - 1;

        _nextResizeCount = (uint)(size * _LoadFactorThreshold);
        _adaptiveResizeFactor = 4; // start agressive
        _movingFillRate = 0.0;
    }

    /// <summary>
    /// Determines whether the dictionary contains an element with the specified key.
    /// </summary>
    /// <param name="key">The key to locate in the dictionary.</param>
    /// <returns>true if the dictionary contains an element with the specified key; otherwise, false.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool ContainsKey(TKey key) => FindEntry(key) >= 0;

    bool IDictionary.Contains(object obj)
    {
        ChronicleGuard.ThrowIfNull(obj, nameof(obj));

        if (obj is TKey key) return ContainsKey(key);
        return false;
    }

    /// <inheritdoc/>
    public bool Contains(KeyValuePair<TKey, TValue> item)
    {
        int index = FindEntry(item.Key);
        if (index >= 0 && EqualityComparer<TValue>.Default.Equals(_entries[index].Value, item.Value))
            return true;
        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryGetValue(TKey key, out TValue value)
    {
        int index = FindEntry(key);
        if (index >= 0)
        {
            value = _entries[index].Value;
            return true;
        }
        value = default!;
        return false;
    }

    /// <summary>
    /// Copies the elements of the collection to the specified array, starting at the given array index.
    /// </summary>
    /// <param name="array">
    /// The one-dimensional array of key/value pairs that is the destination of the elements copied from the collection.
    /// The array must have zero-based indexing.
    /// </param>
    /// <param name="arrayIndex">The zero-based index in the destination array at which copying begins.</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if arrayIndex is less than 0 or greater than the length of array.</exception>
    /// <exception cref="ArgumentException">
    /// Thrown if the number of elements in the source collection is greater than the available space from arrayIndex to
    /// the end of the destination array.
    /// </exception>
    public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
        ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _count, nameof(array), "Insufficient space in the target array.");

        for (uint i = 0; i <= (uint)_lastIndex; i++)
        {
            if (_entries[i].IsUsed)
                array[arrayIndex++] = new KeyValuePair<TKey, TValue>(_entries[i].Key, _entries[i].Value);
        }
    }

    /// <inheritdoc/>
    public void CopyTo(Array array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArgument(array.Rank != 1, nameof(array), "Multidimensional array not supported");
        ChronicleGuard.ThrowIfArgument(array.GetLowerBound(0) != 0, nameof(array), "Non-zero lower bound");
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
        ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _count, nameof(array), "Insufficient space in the target array.");

        if (array is KeyValuePair<TKey, TValue>[] pairs)
            ((ICollection<KeyValuePair<TKey, TValue>>)this).CopyTo(pairs, arrayIndex);
        else if (array is DictionaryEntry[] dictEntryArray)
            CopyToDictionaryEntries(dictEntryArray, arrayIndex);
        else
            CopyToObjects(array, arrayIndex);
    }

    private void CopyToDictionaryEntries(DictionaryEntry[] array, int arrayIndex)
    {
        for (uint i = 0; i <= (uint)_lastIndex; i++)
        {
            if (_entries[i].IsUsed)
                array[arrayIndex++] = new DictionaryEntry(_entries[i].Key, _entries[i].Value);
        }
    }

    private void CopyToObjects(Array array, int arrayIndex)
    {
        if (array is not object[] objects)
            throw new ArgumentException("Invalid array type", nameof(array));

        try
        {
            CopyToObjects(objects, arrayIndex);
        }
        catch (ArrayTypeMismatchException)
        {
            throw new ArgumentException("Invalid array type", nameof(array));
        }
    }

    private void CopyToObjects(object[] array, int arrayIndex)
    {
        for (uint i = 0; i <= (uint)_lastIndex; i++)
        {
            if (_entries[i].IsUsed)
                array[arrayIndex++] = new KeyValuePair<TKey, TValue>(_entries[i].Key, _entries[i].Value);
        }
    }

    /// <summary>
    /// Sets a new comparer for the dictionary and rehashes the entries.
    /// </summary>
    /// <param name="comparer">The new comparer to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetComparer(IEqualityComparer<TKey> comparer)
    {
        ChronicleGuard.ThrowIfNull(comparer, nameof(comparer));
        if (ReferenceEquals(comparer, _comparer))
            return;

        _comparer = comparer;
        RehashEntries();
        _maxStepCount = 0;
    }

    /// <summary>
    /// Switches the dictionary's comparer to a randomized comparer to mitigate the effects of high collision counts,
    /// and rehashes all entries using the new comparer to redistribute them across <see cref="_entries"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SwitchToRandomizedComparer()
    {
        _comparer = (IEqualityComparer<TKey>)SwiftHashTools.GetSwiftEqualityComparer(_comparer);

        RehashEntries();
        _maxStepCount = 0;

        _version++;
    }

    /// <summary>
    /// Reconstructs the internal entry structure to align with updated hash codes, ensuring efficient access and storage.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RehashEntries()
    {
        Entry[] newEntries = new Entry[_entries.Length];
        int newMask = newEntries.Length - 1;

        int lastIndex = 0;
        for (uint i = 0; i <= (uint)_lastIndex; i++)
        {
            if (_entries[i].IsUsed)
            {
                ref Entry oldEntry = ref _entries[i];
                oldEntry.HashCode = _comparer.GetHashCode(oldEntry.Key) & 0x7FFFFFFF;
                int newIndex = oldEntry.HashCode & newMask;
                int step = 1;
                while (newEntries[newIndex].IsUsed)
                {
                    newIndex = (newIndex + step * step) & newMask; // Quadratic probing
                    step++;
                }
                newEntries[newIndex] = _entries[i];
                if (newIndex > lastIndex) lastIndex = newIndex;
            }
        }

        _lastIndex = lastIndex;

        _entryMask = newMask;
        _entries = newEntries;

        _version++;
    }

    /// <summary>
    /// Finds the array index of the entry with the specified key.
    /// </summary>
    /// <param name="key">The key to locate in the dictionary.</param>
    /// <returns>The array index of the entry if found; otherwise, -1, including for a null key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected int FindEntry(TKey key)
    {
        if (_KeyCanBeNull && key is null) return -1;

        int hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
        int entryIndex = hashCode & _entryMask;

        int step = 0;
        // A bucket index is not a probe distance: wrapped chains can take more steps.
        // Use the same complete cumulative-square period as insertion.
        while ((uint)step < (uint)_entries.Length * 2)
        {
            ref Entry entry = ref _entries[entryIndex];
            // Stop probing if an unused entry is found (not deleted)
            if (!entry.IsUsed && entry.HashCode != -1)
                return -1;
            if (entry.IsUsed && entry.HashCode == hashCode && _comparer.Equals(entry.Key, key))
                return entryIndex; // Match found

            // Perform quadratic probing to see if maybe the entry was shifted.
            step++;
            entryIndex = (entryIndex + step * step) & _entryMask;
        }
        return -1; // Item not found, full loop completed
    }

    #endregion

    #region IEnumerable Implementation

    /// <inheritdoc cref="IEnumerable.GetEnumerator()"/>
    public SwiftDictionaryEnumerator GetEnumerator() => new(this);
    IEnumerator<KeyValuePair<TKey, TValue>> IEnumerable<KeyValuePair<TKey, TValue>>.GetEnumerator() => GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    IDictionaryEnumerator IDictionary.GetEnumerator() => new SwiftDictionaryEnumerator(this, true);



    #endregion

    #region Key & Value Collections





    #endregion
}
