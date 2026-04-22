using MemoryPack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace SwiftCollections;

/// <summary>Specifies that the method or property will ensure that the listed field and property members have not-null values.</summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Property, Inherited = false, AllowMultiple = true)]
#if SYSTEM_PRIVATE_CORELIB
    public
#else
internal
#endif
    sealed class MemberNotNullAttribute : Attribute
{
    /// <summary>Initializes the attribute with a field or property member.</summary>
    /// <param name="member">
    /// The field or property member that is promised to be not-null.
    /// </param>
    public MemberNotNullAttribute(string member) => Members = new[] { member };

    /// <summary>Initializes the attribute with the list of field and property members.</summary>
    /// <param name="members">
    /// The list of field and property members that are promised to be not-null.
    /// </param>
    public MemberNotNullAttribute(params string[] members) => Members = members;

    /// <summary>Gets field or property member names.</summary>
    public string[] Members { get; }
}

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
[JsonConverter(typeof(SwiftStateJsonConverterFactory))]
[MemoryPackable]
public partial class SwiftDictionary<TKey, TValue> : IDictionary<TKey, TValue>, IDictionary
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

    /// <summary>
    /// The array containing the entries of the dictionary.
    /// </summary>
    protected Entry[] _entries;

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

    #region Nested Types

    /// <summary>
    /// Represents a single key-value pair in the dictionary, including its hash code for quick access.
    /// </summary>
    protected struct Entry
    {
        /// <summary>
        /// Gets or sets the key associated with this instance.
        /// </summary>
        public TKey Key;

        /// <summary>
        /// Gets or sets the value associated with this instance.
        /// </summary>
        public TValue Value;

        /// <summary>
        /// Gets or sets the lower 31 bits of the hash code associated with this entry.
        /// </summary>
        /// <remarks>
        /// A value of -1 indicates that the entry is unused. 
        /// Only the lower 31 bits are used; the highest bit is reserved.</remarks>
        public int HashCode;

        /// <summary>
        /// Indicates whether the item is currently in use.
        /// </summary>
        public bool IsUsed;
    }

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

        SwiftThrowHelper.ThrowIfNull(_entries, nameof(_entries));
        SwiftThrowHelper.ThrowIfNull(_comparer, nameof(_comparer));
    }

    /// <inheritdoc cref="SwiftDictionary()"/>
    public SwiftDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey>? comparer = null)
    {
        SwiftThrowHelper.ThrowIfNull(dictionary, nameof(dictionary));

        Initialize(dictionary.Count, comparer);

        SwiftThrowHelper.ThrowIfNull(_entries, nameof(_entries));
        SwiftThrowHelper.ThrowIfNull(_comparer, nameof(_comparer));

        foreach (KeyValuePair<TKey, TValue> kvp in dictionary)
            InsertIfNotExist(kvp.Key, kvp.Value);
    }

    /// <inheritdoc cref="SwiftDictionary()"/>
    public SwiftDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey>? comparer = null)
    {
        SwiftThrowHelper.ThrowIfNull(collection, nameof(collection));

        int count = (collection as ICollection<TKey>)?.Count ?? DefaultCapacity;
        // Dynamic padding based on collision estimation
        int size = (int)(count / _LoadFactorThreshold);
        Initialize(size, comparer);

        SwiftThrowHelper.ThrowIfNull(_entries, nameof(_entries));
        SwiftThrowHelper.ThrowIfNull(_comparer, nameof(_comparer));

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

        SwiftThrowHelper.ThrowIfNull(_entries, nameof(_entries));
        SwiftThrowHelper.ThrowIfNull(_comparer, nameof(_comparer));
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
            SwiftThrowHelper.ThrowIfKeyInvalid(index, key);
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
            SwiftThrowHelper.ThrowIfNull(obj, nameof(obj));

            if (obj is TKey key)
            {
                int index = FindEntry(key);
                if (index >= 0) return _entries[index].Value;
            }
            return null;
        }
        set
        {
            SwiftThrowHelper.ThrowIfNullAndNullsAreIllegal(value, default(TValue));
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
            SwiftThrowHelper.ThrowIfNull(value.Items, nameof(value.Items));

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
        SwiftThrowHelper.ThrowIfNullAndNullsAreIllegal(value, default(TValue));

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
        SwiftThrowHelper.ThrowIfNull(key, nameof(key));

        int hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
        int entryIndex = hashCode & _entryMask;

        int step = 1;
        while (_entries[entryIndex].IsUsed)
        {
            if (_entries[entryIndex].HashCode == hashCode && _comparer.Equals(_entries[entryIndex].Key, key))
                return false; // Item already exists

            entryIndex = (entryIndex + step * step) & _entryMask; // Quadratic probing
            step++;
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
            if (_comparer is not IRandomedEqualityComparer && _maxStepCount > 100)
                SwitchToRandomizedComparer();  // Attempt to recompute hash code with potential randomization for better distribution
        }


        return true;
    }

    /// <inheritdoc/>
    public virtual bool Remove(TKey key)
    {
        if (key == null) return false;

        int hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
        int entryIndex = hashCode & _entryMask;

        int step = 0;
        while ((uint)step <= (uint)_lastIndex)
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
        SwiftThrowHelper.ThrowIfNull(obj, nameof(obj));
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
            _entries[i].HashCode = -1;
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
        SwiftThrowHelper.ThrowIfNull(obj, nameof(obj));

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
        SwiftThrowHelper.ThrowIfNull(array, nameof(array));
        if ((uint)arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < _count) throw new ArgumentException("Insufficient space", nameof(array));

        for (uint i = 0; i <= (uint)_lastIndex; i++)
        {
            if (_entries[i].IsUsed)
                array[arrayIndex++] = new KeyValuePair<TKey, TValue>(_entries[i].Key, _entries[i].Value);
        }
    }

    /// <inheritdoc/>
    public void CopyTo(Array array, int arrayIndex)
    {
        SwiftThrowHelper.ThrowIfNull(array, nameof(array));
        if (array.Rank != 1) throw new ArgumentException("Multidimensional array not supported", nameof(array));
        if (array.GetLowerBound(0) != 0) throw new ArgumentException("Non-zero lower bound", nameof(array));
        if ((uint)arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
        if (array.Length - arrayIndex < _count) throw new ArgumentException("Insufficient space", nameof(array));

        if (array is KeyValuePair<TKey, TValue>[] pairs)
            ((ICollection<KeyValuePair<TKey, TValue>>)this).CopyTo(pairs, arrayIndex);
        else if (array is DictionaryEntry[] dictEntryArray)
        {
            for (uint i = 0; i <= (uint)_lastIndex; i++)
            {
                if (_entries[i].IsUsed)
                    dictEntryArray[arrayIndex++] = new DictionaryEntry(_entries[i].Key, _entries[i].Value);
            }
        }
        else
        {
            if (array is not object[] objects) throw new ArgumentException("Invalid array type", nameof(array));

            try
            {
                for (uint i = 0; i <= (uint)_lastIndex; i++)
                {
                    if (_entries[i].IsUsed)
                        objects[arrayIndex++] = new KeyValuePair<TKey, TValue>(_entries[i].Key, _entries[i].Value);
                }
            }
            catch (ArrayTypeMismatchException)
            {
                throw new ArgumentException("Invalid array type", nameof(array));
            }
        }
    }

    /// <summary>
    /// Sets a new comparer for the dictionary and rehashes the entries.
    /// </summary>
    /// <param name="comparer">The new comparer to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetComparer(IEqualityComparer<TKey> comparer)
    {
        SwiftThrowHelper.ThrowIfNull(comparer, nameof(comparer));
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
        if (SwiftHashTools.IsWellKnownEqualityComparer(_comparer))
            _comparer = (IEqualityComparer<TKey>)SwiftHashTools.GetSwiftEqualityComparer(_comparer);
        else return; // nothing to do here

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
    /// Finds the arrayIndex of the entry with the specified key.
    /// </summary>
    /// <param name="key">The key to locate in the dictionary.</param>
    /// <returns>The arrayIndex of the entry if found; otherwise, -1.</returns>
    /// <exception cref="ArgumentNullException">The key is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    protected int FindEntry(TKey key)
    {
        if (key == null) return -1;

        int hashCode = _comparer.GetHashCode(key) & 0x7FFFFFFF;
        int entryIndex = hashCode & _entryMask;

        int step = 0;
        while ((uint)step <= (uint)_lastIndex)
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

    /// <summary>
    /// Provides an efficient enumerator for iterating over the key-value pairs in the SwiftDictionary, enabling smooth traversal during enumeration.
    /// </summary>
    [Serializable]
    public struct SwiftDictionaryEnumerator : IEnumerator<KeyValuePair<TKey, TValue>>, IEnumerator, IDictionaryEnumerator, IDisposable
    {
        private readonly SwiftDictionary<TKey, TValue> _dictionary;
        private readonly Entry[] _entries;
        private readonly uint _version;
        private readonly bool _returnEntry;
        private int _index;
        private KeyValuePair<TKey, TValue> _current;

        internal SwiftDictionaryEnumerator(SwiftDictionary<TKey, TValue> dictionary, bool returnEntry = false)
        {
            _dictionary = dictionary;
            _entries = dictionary._entries;
            _version = dictionary._version;
            _returnEntry = returnEntry;
            _index = -1;
            _current = default;
        }

        object IDictionaryEnumerator.Key
        {
            get
            {
                if (_index > (uint)_dictionary._lastIndex) throw new InvalidOperationException("Bad enumeration");
                return _current.Key;
            }
        }

        object IDictionaryEnumerator.Value
        {
            get
            {
                if (_index > (uint)_dictionary._lastIndex) throw new InvalidOperationException("Bad enumeration");
                return _current.Value!;
            }
        }

        DictionaryEntry IDictionaryEnumerator.Entry
        {
            get
            {
                if (_index > (uint)_dictionary._lastIndex) throw new InvalidOperationException("Bad enumeration");
                return new DictionaryEntry(_current.Key, _current.Value);
            }
        }

        /// <inheritdoc/>
        public KeyValuePair<TKey, TValue> Current => _current;

        object IEnumerator.Current
        {
            get
            {
                if (_index > (uint)_dictionary._lastIndex) throw new InvalidOperationException("Bad enumeration");
                return _returnEntry
                    ? new DictionaryEntry(_current.Key, _current.Value)
                    : new KeyValuePair<TKey, TValue>(_current.Key, _current.Value);
            }
        }

        /// <inheritdoc/>
        public bool MoveNext()
        {
            if (_version != _dictionary._version)
                throw new InvalidOperationException("Enumerator modified outside of enumeration!");

            while (++_index <= (uint)_dictionary._lastIndex)
            {
                if (_entries[_index].IsUsed)
                {
                    _current = new KeyValuePair<TKey, TValue>(_entries[_index].Key, _entries[_index].Value);
                    return true;
                }
            }

            _current = default;
            return false;
        }

        /// <inheritdoc/>
        public void Reset()
        {
            if (_version != _dictionary._version)
                throw new InvalidOperationException("Enumerator modified outside of enumeration!");

            _index = -1;
            _current = default;
        }

        /// <inheritdoc/>
        public void Dispose() => _index = -1;
    }

    #endregion

    #region Key & Value Collections

    /// <summary>
    /// Provides a dynamic, read-only collection of all keys in the dictionary, supporting enumeration and copy operations.
    /// </summary>
    [Serializable]
    public sealed class KeyCollection : ICollection<TKey>, ICollection, IReadOnlyCollection<TKey>, IEnumerable<TKey>, IEnumerable
    {
        private readonly SwiftDictionary<TKey, TValue> _dictionary;
        private readonly Entry[] _entries;

        /// <summary>
        /// Initializes a new instance of the KeyCollection class that reflects the keys in the specified dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary whose keys are reflected in the new KeyCollection.</param>
        /// <exception cref="ArgumentNullException">The dictionary is null.</exception>
        public KeyCollection(SwiftDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
            _entries = dictionary._entries;
        }

        /// <inheritdoc/>
        public int Count => _dictionary._count;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => ((ICollection)_dictionary).SyncRoot;

        bool ICollection<TKey>.IsReadOnly => true;

        void ICollection<TKey>.Add(TKey item) => throw new NotSupportedException();

        void ICollection<TKey>.Clear() => throw new NotSupportedException();

        bool ICollection<TKey>.Contains(TKey item) => _dictionary.ContainsKey(item);

        bool ICollection<TKey>.Remove(TKey item) => false;

        /// <inheritdoc/>
        public void CopyTo(TKey[] array, int arrayIndex)
        {
            SwiftThrowHelper.ThrowIfNull(array, nameof(array));
            if ((uint)arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            if (array.Length - arrayIndex < _dictionary._count) throw new ArgumentException("Insufficient space", nameof(array));

            for (int i = 0, j = arrayIndex; i < _entries.Length; i++)
            {
                if (_entries[i].IsUsed)
                    array[j++] = _entries[i].Key;
            }
        }

        void ICollection.CopyTo(Array array, int arrayIndex)
        {
            SwiftThrowHelper.ThrowIfNull(array, nameof(array));
            if (array.Rank != 1) throw new ArgumentException("Multidimensional array not supported");
            if (array.GetLowerBound(0) != 0) throw new ArgumentException("Non-zero lower bound");
            if ((uint)arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            if (array.Length - arrayIndex < _dictionary._count) throw new ArgumentException("Insufficient space", nameof(array));

            if (array is TKey[] keysArray)
                CopyTo(keysArray, arrayIndex);
            else if (array is object[] objects)
            {
                try
                {
                    for (int i = 0, j = arrayIndex; i < _entries.Length; i++)
                    {
                        if (_entries[i].IsUsed)
                            objects[j++] = _entries[i].Key;
                    }
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new ArgumentException("Invalid array type", nameof(array));
                }
            }
            else
            {
                throw new ArgumentException("Invalid array type", nameof(array));
            }
        }

        /// <summary>
        /// Returns an enumerator that iterates through the keys in the collection.
        /// </summary>
        /// <returns>An enumerator for the keys in the collection.</returns>
        public KeyCollectionEnumerator GetEnumerator() => new(_dictionary);
        IEnumerator<TKey> IEnumerable<TKey>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Enumerates the keys of a <see cref="SwiftDictionary{TKey, TValue}"/> collection.
        /// </summary>
        /// <remarks>
        /// The enumerator provides read-only, forward-only iteration over the keys in the dictionary. 
        /// The enumerator is invalidated if the dictionary is modified after the enumerator is created. 
        /// In such cases, calling MoveNext or Reset will throw an InvalidOperationException.
        /// </remarks>
        [Serializable]
        public struct KeyCollectionEnumerator : IEnumerator<TKey>, IEnumerator, IDisposable
        {
            private readonly SwiftDictionary<TKey, TValue> _dictionary;
            private readonly Entry[] _entries;
            private readonly uint _version;
            private int _index;
            private TKey _currentKey;

            internal KeyCollectionEnumerator(SwiftDictionary<TKey, TValue> dictionary)
            {
                _dictionary = dictionary;
                _entries = dictionary._entries;
                _version = dictionary._version;
                _index = -1;
                _currentKey = default!;
            }

            /// <inheritdoc/>
            public TKey Current => _currentKey;

            object IEnumerator.Current
            {
                get
                {
                    if (_index > (uint)_dictionary._lastIndex) throw new InvalidOperationException("Bad enumeration");
                    return _currentKey;
                }
            }

            /// <inheritdoc/>
            public bool MoveNext()
            {
                if (_version != _dictionary._version)
                    throw new InvalidOperationException("Enumerator modified outside of enumeration!");

                while (++_index <= (uint)_dictionary._lastIndex)
                {
                    if (_entries[_index].IsUsed)
                    {
                        _currentKey = _entries[_index].Key;
                        return true;
                    }
                }

                _currentKey = default!;
                return false;
            }

            /// <inheritdoc/>
            public void Reset()
            {
                if (_version != _dictionary._version)
                    throw new InvalidOperationException("Enumerator modified outside of enumeration!");

                _index = -1;
                _currentKey = default!;
            }

            /// <inheritdoc/>
            public void Dispose() => _index = -1;
        }
    }

    /// <summary>
    /// Offers a dynamic, read-only collection of all values in the dictionary, supporting enumeration and copy operations.
    /// </summary>
    [Serializable]
    public sealed class ValueCollection : ICollection<TValue>, ICollection, IReadOnlyCollection<TValue>, IEnumerable<TValue>, IEnumerable
    {
        private readonly SwiftDictionary<TKey, TValue> _dictionary;
        private readonly Entry[] _entries;

        /// <summary>
        /// Initializes a new instance of the ValueCollection class that reflects the values in the specified dictionary.
        /// </summary>
        /// <param name="dictionary">The dictionary whose values are reflected in the new ValueCollection.</param>
        /// <exception cref="ArgumentNullException">The dictionary is null.</exception>
        public ValueCollection(SwiftDictionary<TKey, TValue> dictionary)
        {
            _dictionary = dictionary ?? throw new ArgumentNullException(nameof(dictionary));
            _entries = dictionary._entries;
        }

        /// <inheritdoc/>
        public int Count => _dictionary._count;

        bool ICollection<TValue>.IsReadOnly => true;

        bool ICollection.IsSynchronized => false;

        object ICollection.SyncRoot => ((ICollection)_dictionary).SyncRoot;

        void ICollection<TValue>.Add(TValue item) => throw new NotSupportedException();

        void ICollection<TValue>.Clear() => throw new NotSupportedException();

        bool ICollection<TValue>.Contains(TValue item)
        {
            for (uint i = 0; i <= (uint)_dictionary._lastIndex; i++)
            {
                if (_entries[i].IsUsed && EqualityComparer<TValue>.Default.Equals(_entries[i].Value, item))
                    return true;
            }
            return false;
        }

        bool ICollection<TValue>.Remove(TValue item) => false;

        /// <inheritdoc/>
        public void CopyTo(TValue[] array, int arrayIndex)
        {
            SwiftThrowHelper.ThrowIfNull(array, nameof(array));
            if ((uint)arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            if (array.Length - arrayIndex < _dictionary._count) throw new ArgumentException("Insufficient space", nameof(array));


            for (int i = 0, j = arrayIndex; i <= _dictionary._lastIndex; i++)
            {
                if (_dictionary._entries[i].IsUsed)
                    array[j++] = _entries[i].Value;
            }
        }

        void ICollection.CopyTo(Array array, int arrayIndex)
        {
            SwiftThrowHelper.ThrowIfNull(array, nameof(array));
            if (array.Rank != 1) throw new ArgumentException("Multidimensional array not supported", nameof(array));
            if (array.GetLowerBound(0) != 0) throw new ArgumentException("Non-zero lower bound", nameof(array));
            if ((uint)arrayIndex > array.Length) throw new ArgumentOutOfRangeException(nameof(arrayIndex));
            if (array.Length - arrayIndex < _dictionary._count) throw new ArgumentException("Insufficient space", nameof(array));

            if (array is TValue[] valuesArray)
                CopyTo(valuesArray, arrayIndex);
            else if (array is object[] objects)
            {
                try
                {
                    for (int i = 0, j = arrayIndex; i <= _dictionary._lastIndex; i++)
                        if (_entries[i].IsUsed)
                            objects[j++] = _entries[i].Value!;
                }
                catch (ArrayTypeMismatchException)
                {
                    throw new ArgumentException("Invalid array type", nameof(array));
                }
            }
            else throw new ArgumentException("Invalid array type", nameof(array));
        }

        /// <summary>
        /// Returns an enumerator that iterates through the values in the collection.
        /// </summary>
        /// <returns>An enumerator for the values in the collection.</returns>
        public ValueCollectionEnumerator GetEnumerator() => new(_dictionary);
        IEnumerator<TValue> IEnumerable<TValue>.GetEnumerator() => GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>
        /// Enumerates the values in a SwiftDictionary collection.
        /// </summary>
        /// <remarks>
        /// The enumerator is invalidated if the collection is modified after the enumerator is created. 
        /// Enumerators are typically used in a foreach statement to iterate through the collection values.
        /// </remarks>
        [Serializable]
        public struct ValueCollectionEnumerator : IEnumerator<TValue>, IEnumerator, IDisposable
        {
            private readonly SwiftDictionary<TKey, TValue> _dictionary;
            private readonly Entry[] _entries;
            private readonly uint _version;
            private int _index;
            private TValue _currentValue;

            internal ValueCollectionEnumerator(SwiftDictionary<TKey, TValue> dictionary)
            {
                _dictionary = dictionary;
                _entries = dictionary._entries;
                _version = dictionary._version;
                _index = -1;
                _currentValue = default!;
            }

            /// <inheritdoc/>
            public TValue Current => _currentValue;

            object IEnumerator.Current
            {
                get
                {
                    if (_index > (uint)_dictionary._lastIndex) throw new InvalidOperationException("Bad enumeration");
                    return _currentValue!;
                }
            }

            /// <inheritdoc/>
            public bool MoveNext()
            {
                if (_version != _dictionary._version)
                    throw new InvalidOperationException("Enumerator modified outside of enumeration!");

                while (++_index <= (uint)_dictionary._lastIndex)
                {
                    if (_entries[_index].IsUsed)
                    {
                        _currentValue = _entries[_index].Value;
                        return true;
                    }
                }

                _currentValue = default!;
                return false;
            }

            /// <inheritdoc/>
            public void Reset()
            {
                if (_version != _dictionary._version)
                    throw new InvalidOperationException("Enumerator modified outside of enumeration!");

                _index = -1;
                _currentValue = default!;
            }

            /// <inheritdoc/>
            public void Dispose() => _index = -1;
        }
    }

    #endregion
}
