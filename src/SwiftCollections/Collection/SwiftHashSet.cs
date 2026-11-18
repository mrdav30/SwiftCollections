//=======================================================================
// SwiftHashSet.cs
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
/// Stores unique values in a resizable hash table.
/// </summary>
/// <typeparam name="T">The type of elements in the set.</typeparam>
/// <remarks>
/// Instances require external synchronization; keys and elements must keep equality/hash identity stable while
/// stored. Copies are shallow. Enumeration follows hash slots, not insertion or sorted order. Resize, trim, and
/// comparer changes can reorder entries. Recognized string/object comparers may switch to randomized
/// collision-hardening after long probes; cross-process iteration order is not guaranteed. Custom comparers
/// define their own determinism.
/// </remarks>
[Serializable]
[JsonConverter(typeof(StateJsonConverterFactory))]
[MemoryPackable]
public sealed partial class SwiftHashSet<T> : IStateBacked<SwiftArrayState<T>>, ISet<T>, ICollection<T>, IEnumerable<T>, IEnumerable, IReadOnlyCollection<T>
    where T : notnull
{
    #region Constants

    /// <summary>
    /// The default initial capacity of the set.
    /// </summary>
    public const int DefaultCapacity = 8;

    /// <summary>
    /// Determines the maximum allowable load factor before resizing the hash set to maintain performance.
    /// </summary>
    private const float _LoadFactorThreshold = 0.85f;

    #endregion

    #region Fields

    /// <summary>
    /// The array containing the entries of the SwiftHashSet.
    /// </summary>
    /// <remarks>
    /// Capacity will always be a power of two for efficient pooling cache.
    /// </remarks>
    internal Entry[] _entries;

    /// <summary>
    /// The total number of entries in the hash set
    /// </summary>
    private int _count;

    private int _lastIndex;

    // A cold lookup must not box a default value while initializing this flag.
    private static readonly bool _ItemCanBeNull = !typeof(T).IsValueType
        || (typeof(T).IsGenericType && typeof(T).GetGenericTypeDefinition() == typeof(Nullable<>));

    /// <summary>
    /// A mask used for efficiently computing the entry index from a hash code.
    /// This is typically the size of the entry array minus one, assuming the size is a power of two.
    /// </summary>
    private int _entryMask;

    /// <summary>
    /// The comparer used to determine equality of keys and to generate hash codes.
    /// </summary>
    private IEqualityComparer<T> _comparer;

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

    private int _maxStepCount;

    /// <summary>
    /// A version counter used to track modifications to the set.
    /// Incremented on mutations to detect changes during enumeration and ensure enumerator validity.
    /// </summary>
    private uint _version;

    #endregion

    #region Constructors

    /// <summary>
    /// Initialize a new instance of <see cref="T:SwiftCollections.SwiftHashSet`1" /> with customizable capacity
    /// and comparer for optimal performance tailored to your needs.
    /// </summary>
    public SwiftHashSet() : this(DefaultCapacity, null) { }

    /// <summary>
    /// Creates a collection using the supplied initial capacity, contents, or equality comparer.
    /// </summary>
    /// <param name="comparer">The element equality comparer, retained by reference. Null selects the Swift
    /// default.</param>
    public SwiftHashSet(IEqualityComparer<T>? comparer) : this(DefaultCapacity, comparer) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftHashSet`1" /> class that is empty and
    /// has the default initial capacity.
    /// </summary>
    /// <param name="capacity">Requested storage capacity; values below DefaultCapacity use the minimum
    /// allocation. Larger values round up. Keep requests below 2^30, where the capacity helper returns a
    /// sentinel.</param>
    /// <param name="comparer">The element equality comparer, retained by reference. Null selects the Swift
    /// default.</param>
    public SwiftHashSet(int capacity, IEqualityComparer<T>? comparer = null)
    {
        Initialize(capacity, comparer);

        ChronicleGuard.ThrowIfNull(_entries, nameof(_entries));
        ChronicleGuard.ThrowIfNull(_comparer, nameof(_comparer));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftHashSet`1" /> class that contains
    /// elements copied from the specified collection.
    /// </summary>
    /// <param name="collection">The non-null source to enumerate and shallow-copy; source storage is not
    /// retained. Equal duplicates keep the first inserted mapping.</param>
    /// <param name="comparer">The element equality comparer, retained by reference. Null selects the Swift
    /// default.</param>
    /// <exception cref="T:System.ArgumentNullException">The source is null.</exception>
    public SwiftHashSet(IEnumerable<T> collection, IEqualityComparer<T>? comparer = null)
    {
        ChronicleGuard.ThrowIfNull(collection, nameof(collection));

        int count = (collection as ICollection<T>)?.Count ?? DefaultCapacity;
        int size = (int)(count / _LoadFactorThreshold);  // Dynamic padding based on collision estimation
        Initialize(size, comparer);

        ChronicleGuard.ThrowIfNull(_entries, nameof(_entries));
        ChronicleGuard.ThrowIfNull(_comparer, nameof(_comparer));

        foreach (T item in collection)
            InsertIfNotExists(item);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.SwiftHashSet`1" /> class with the
    /// specified <see cref="T:SwiftCollections.SwiftArrayState`1" />.
    /// </summary>
    /// <param name="state">The state whose non-null Items array is copied and indexed. Comparer and table layout
    /// are not retained.</param>
    /// <exception cref="T:System.ArgumentNullException">state.Items is null, including a default
    /// state.</exception>
    /// <remarks>
    /// Copies non-null items into reconstructed storage using Swift default equality. Null items are skipped and
    /// equal duplicates keep the first stored value. Custom comparers are not restored; unlike state restore,
    /// enumerable construction rejects null elements.
    /// </remarks>
    [MemoryPackConstructor]
    public SwiftHashSet(SwiftArrayState<T> state)
    {
        State = state;

        ChronicleGuard.ThrowIfNull(_entries, nameof(_entries));
        ChronicleGuard.ThrowIfNull(_comparer, nameof(_comparer));
    }

    #endregion

    #region Properties

    /// <summary>
    /// Gets the number of live entries.
    /// </summary>
    /// <value>The count of stored elements or mappings, excluding spare storage and deleted slots.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int Count => _count;

    /// <summary>
    /// Gets an upper bound on the physical entry slots inspected by a full enumeration.
    /// </summary>
    /// <value>One more than the highest tracked hash slot; may be one even when Count is zero and is not the live
    /// element count.</value>
    /// <remarks>
    /// Includes holes left by removed entries. Keep the collection unchanged after sampling the bound.
    /// </remarks>
    [JsonIgnore]
    [MemoryPackIgnore]
    public int EnumerationSlotCount => _lastIndex + 1;

    /// <summary>
    /// Gets the <see cref="T:System.Collections.Generic.IEqualityComparer`1" /> object that is used to determine
    /// equality for the values in the set.
    /// </summary>
    /// <value>The current equality comparer; it may change after explicit replacement or collision
    /// hardening.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    public IEqualityComparer<T> Comparer => _comparer;

    /// <summary>
    /// Gets the interface IsReadOnly.
    /// </summary>
    /// <value>false.</value>
    [JsonIgnore]
    [MemoryPackIgnore]
    bool ICollection<T>.IsReadOnly => false;

    /// <summary>
    /// Gets the stored value that matches the specified key.
    /// </summary>
    /// <param name="key">The key to locate or store. Null is treated as absent by lookup/removal.</param>
    /// <value>The stored element equal to key.</value>
    /// <exception cref="T:System.Collections.Generic.KeyNotFoundException">No matching key exists on
    /// get.</exception>
    [JsonIgnore]
    [MemoryPackIgnore]
    public T this[T key]
    {
        get
        {
            int index = FindEntry(key);
            // The object-based error helper would box a value key even on a successful lookup.
            if (index < 0)
                ChronicleGuard.ThrowIfKeyInvalid(index, key);
            return _entries[index].Value;
        }
    }

    /// <summary>
    /// Gets a shallow item snapshot for serialization.
    /// </summary>
    /// <value>A state containing copied live items in increasing hash-slot order; reference-type elements remain
    /// shared.</value>
    /// <remarks>
    /// Restore rebuilds storage and Swift default equality; capacity, hash slots, custom comparers and version
    /// are not retained. Equal duplicates keep the first stored value, and null state items are skipped. The
    /// enumerable constructor instead rejects null elements.
    /// </remarks>
    [JsonInclude]
    [MemoryPackInclude]
    public SwiftArrayState<T> State
    {
        get
        {
            if (_count == 0)
                return new SwiftArrayState<T>(Array.Empty<T>());

            T[] items = new T[_count];
            CopyTo(items, 0);

            return new SwiftArrayState<T>(items);
        }
        internal set
        {
            ChronicleGuard.ThrowIfNull(value.Items, nameof(value));

            T[] items = value.Items;
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

            foreach (T item in items)
                if (item != null)
                    InsertIfNotExists(item);

            _version = 0;
        }
    }

    #endregion

    #region Collection Manipulation

    /// <summary>
    /// Adds an element or mapping without replacing an existing equal key.
    /// </summary>
    /// <param name="item">The element or value; stored reference values are not cloned.</param>
    /// <returns>true if inserted; false if an equal key/element already exists. Existing contents are preserved
    /// on duplicate rejection, but a load-threshold resize may already have occurred.</returns>
    /// <exception cref="T:System.ArgumentNullException">A required key or element is null.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Add(T item)
    {
        CheckLoadThreshold();
        return InsertIfNotExists(item);
    }

    /// <summary>
    /// Adds an element through the implemented collection interface.
    /// </summary>
    /// <param name="item">The element to compare, add or remove; reference values are not cloned.</param>
    void ICollection<T>.Add(T item) => Add(item);

    /// <summary>
    /// Adds the elements of the specified collection to the set, ignoring null values and duplicates.
    /// </summary>
    /// <param name="items">The non-null source to add; null elements are skipped and duplicates are ignored.
    /// Self-add changes nothing.</param>
    /// <exception cref="T:System.ArgumentNullException">items is null.</exception>
    /// <remarks>
    /// Adds distinct non-null elements without clearing existing contents. Enumeration failure can leave a
    /// partially added prefix; capacity can grow even without new distinct elements.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddRange(IEnumerable<T> items)
    {
        ChronicleGuard.ThrowIfNull(items, nameof(items));

        if (ReferenceEquals(this, items))
            return;

        if (items is ICollection<T> collection)
        {
            AddKnownCountRange(collection, collection.Count);
            return;
        }

        if (items is IReadOnlyCollection<T> readOnlyCollection)
        {
            AddKnownCountRange(readOnlyCollection, readOnlyCollection.Count);
            return;
        }

        AddUnknownCountRange(items);
    }

    private void AddKnownCountRange(IEnumerable<T> items, int count)
    {
        EnsureCapacityForAddRange(count);

        foreach (T item in items)
            if (item != null)
                InsertIfNotExists(item);
    }

    private void AddUnknownCountRange(IEnumerable<T> items)
    {
        foreach (T item in items)
            if (item != null)
                Add(item);
    }

    /// <summary>
    /// Adds the specified element to the set if it's not already present.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool InsertIfNotExists(T item)
    {
        ChronicleGuard.ThrowIfNullGeneric(item, nameof(item));

        int hashCode = _comparer.GetHashCode(item) & 0x7FFFFFFF;
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
                if (entry.HashCode == hashCode && _comparer.Equals(entry.Value, item))
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
            return InsertIfNotExists(item);
        }

        if ((uint)entryIndex > (uint)_lastIndex) _lastIndex = entryIndex;

        _entries[entryIndex].HashCode = hashCode;
        _entries[entryIndex].Value = item;
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

    /// <summary>
    /// Removes the specified element from the set.
    /// </summary>
    /// <param name="item">The element or value; stored reference values are not cloned.</param>
    /// <returns>true if the matching entry was removed; false if absent, with contents unchanged. </returns>
    /// <exception cref="T:System.ArgumentNullException">The required key, element or reverse value is
    /// null.</exception>
    public bool Remove(T item)
    {
        ChronicleGuard.ThrowIfNullGeneric(item, nameof(item));

        int hashCode = _comparer.GetHashCode(item) & 0x7FFFFFFF;
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
            if (entry.IsUsed && entry.HashCode == hashCode && _comparer.Equals(entry.Value, item))
            {
                // Mark entry as deleted
                entry.IsUsed = false;
                entry.Value = default!;
                entry.HashCode = -1;
                _count--;
                if ((uint)_count == 0) _lastIndex = 0;
                _version++;
                return true;
            }

            // Entry not found in expected entry, it either doesn't exist or was moved via quadratic probing
            step++;
            entryIndex = (entryIndex + step * step) & _entryMask;
        }
        return false; // Item not found after full loop
    }

    /// <summary>
    /// Removes all elements from the set.
    /// </summary>
    /// <remarks>
    /// Removes live entries while preserving capacity. Ordinary clearing resets active table metadata and stored
    /// references.
    /// </remarks>
    public void Clear()
    {
        if ((uint)_count == 0) return;

        for (uint i = 0; i <= (uint)_lastIndex; i++)
        {
            // Clear is a full reset, not a delete; future probes must be able to stop
            // at these now-empty slots instead of treating them as tombstones.
            _entries[i].HashCode = 0;
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
    /// <param name="clearRetainedReferences">If true, also releases references outside the live range; otherwise
    /// uses ordinary clearing.</param>
    /// <remarks>
    /// Removes live entries while preserving capacity. The Boolean overload can additionally release references
    /// in unused storage.
    /// </remarks>
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
    /// Ensures that the hash set is resized when the current load factor exceeds the predefined threshold.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void CheckLoadThreshold()
    {
        if ((uint)_count >= _nextResizeCount)
            Resize(_entries.Length * _adaptiveResizeFactor);
    }

    /// <summary>
    /// Ensures there is enough room to add a batch of items without repeatedly checking the load threshold.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureCapacityForAddRange(int incomingCount)
    {
        if (incomingCount <= 0)
            return;

        long requiredCount = (long)_count + incomingCount;
        ChronicleGuard.ThrowIfTrue(requiredCount > int.MaxValue, message: "The collection is too large.");

        double minimumCapacity = Math.Ceiling(requiredCount / (double)_LoadFactorThreshold);
        EnsureCapacity((int)Math.Min(minimumCapacity, int.MaxValue));
    }

    /// <summary>
    /// Ensures that the set can hold up to the specified number of elements without resizing.
    /// </summary>
    /// <param name="capacity">Requested storage capacity; values below DefaultCapacity use the minimum
    /// allocation. Larger values round up. Keep requests below 2^30, where the capacity helper returns a
    /// sentinel.</param>
    /// <remarks>
    /// Grows storage only when needed; existing values survive. Capacity is storage size, not an insertion count
    /// guarantee. Keep requests below 2^30; large allocations may fail.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void EnsureCapacity(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);  // Capacity must be a power of 2 for proper masking
        if (capacity > _entries.Length)
            Resize(capacity);
    }

    /// <summary>
    /// Resizes the hash set to the specified capacity, redistributing all entries to maintain efficiency.
    /// </summary>
    /// <param name="newSize"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Resize(int newSize)
    {
        Entry[] newEntries = new Entry[newSize];
        int newMask = newSize - 1;

        int lastIndex = 0;
        for (uint i = 0; i <= (uint)_lastIndex; i++)
        {
            if (_entries[i].IsUsed)
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
    /// Sets the capacity of a <see cref="T:SwiftCollections.SwiftHashSet`1" /> to the actual number of elements
    /// it contains, rounded up to a nearby next power of 2 value.
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
    /// Initializes the hash set with a given capacity, ensuring it starts with an optimal internal structure.
    /// </summary>
    /// <param name="capacity"></param>
    /// <param name="comparer"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Initialize(int capacity, IEqualityComparer<T>? comparer = null)
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
    /// Determines whether the set contains the specified element.
    /// </summary>
    /// <param name="item">The element or value; stored reference values are not cloned.</param>
    /// <returns>true if a matching live entry exists; otherwise false. No mutation.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T item) => FindEntry(item) >= 0;

    /// <summary>
    /// Determines whether the <see cref="T:SwiftCollections.SwiftHashSet`1" /> contains an element that matches
    /// the conditions defined by the specified predicate.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated over live entries in increasing hash-slot order; it
    /// must not mutate this set.</param>
    /// <returns>true if any live entry matches; otherwise false.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public bool Exists(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        for (int i = 0; i <= _lastIndex; i++)
        {
            if (_entries[i].IsUsed && match(_entries[i].Value))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Searches for an element that matches the conditions defined by the specified predicate, and returns the
    /// first matching element.
    /// </summary>
    /// <param name="match">The non-null predicate evaluated over live entries in increasing hash-slot order; it
    /// must not mutate this set.</param>
    /// <returns>The first matching live entry in increasing hash-slot order, or default(T) if none; a stored
    /// default cannot be distinguished from absence by this result alone.</returns>
    /// <exception cref="T:System.ArgumentNullException">match is null.</exception>
    public T Find(Predicate<T> match)
    {
        ChronicleGuard.ThrowIfNull(match, nameof(match));

        for (int i = 0; i <= _lastIndex; i++)
        {
            if (_entries[i].IsUsed && match(_entries[i].Value))
                return _entries[i].Value;
        }

        return default!;
    }

    /// <summary>
    /// Searches the set for a given value and returns the equal value it finds, if any.
    /// </summary>
    /// <param name="expected">The value to locate under the set comparer; null is treated as absent.</param>
    /// <param name="actual">Receives the stored equal value on success; default(T) on failure.</param>
    /// <returns>true if a matching entry was found and the out value is valid; false if absent, with the out
    /// value set to default. No mutation.</returns>
    public bool TryGetValue(T expected, out T actual)
    {
        int index = FindEntry(expected);
        if (index >= 0)
        {
            actual = _entries[index].Value;
            return true;
        }
        actual = default!;
        return false;
    }

    /// <summary>
    /// Copies the elements of the set to an array, starting at the specified array index.
    /// </summary>
    /// <param name="array">The caller-owned destination; only the copied range is overwritten.</param>
    /// <param name="arrayIndex">The zero-based destination offset, from zero through array.Length, with remaining
    /// space for Count elements.</param>
    /// <exception cref="T:System.ArgumentNullException">array is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">arrayIndex is outside zero through
    /// array.Length.</exception>
    /// <exception cref="T:System.ArgumentException">There is insufficient remaining space or the array shape/type
    /// is unsupported.</exception>
    /// <remarks>
    /// Copies Count live entries in increasing hash-slot order into existing caller storage; does not append or
    /// clear the destination. Incompatible array elements can leave a partial copy.
    /// </remarks>
    public void CopyTo(T[] array, int arrayIndex)
    {
        ChronicleGuard.ThrowIfNull(array, nameof(array));
        ChronicleGuard.ThrowIfArrayIndexInvalid(arrayIndex, array.Length);
        ChronicleGuard.ThrowIfArgument(array.Length - arrayIndex < _count, nameof(array), "The array is not large enough to hold the elements.");

        for (uint i = 0; i <= (uint)_lastIndex; i++)
        {
            if (_entries[i].IsUsed)
                array[arrayIndex++] = _entries[i].Value;
        }
    }

    /// <summary>
    /// Switches the hash set's comparer and rehashes all entries using the new comparer to redistribute them
    /// across <see cref="F:SwiftCollections.SwiftHashSet`1._entries" />.
    /// </summary>
    /// <param name="comparer">The element equality comparer, retained by reference. Must be non-null and must not
    /// collapse previously distinct stored keys or values.</param>
    /// <exception cref="T:System.ArgumentNullException">A supplied comparer is null.</exception>
    /// <remarks>
    /// Retains the new comparer and rehashes existing entries without deduplicating newly equal keys. Changing
    /// equality must preserve distinction among existing keys. Comparer exceptions can leave partial changes;
    /// there is no rollback.
    /// </remarks>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetComparer(IEqualityComparer<T>? comparer = null)
    {
        ChronicleGuard.ThrowIfNull(comparer, nameof(comparer));
        if (ReferenceEquals(comparer, _comparer))
            return;

        _comparer = comparer;
        RehashEntries();
    }

    /// <summary>
    /// Replaces the hash set's comparer with a randomized comparer to mitigate high collision rates.
    /// </summary>
    private void SwitchToRandomizedComparer()
    {
        _comparer = (IEqualityComparer<T>)SwiftHashTools.GetSwiftEqualityComparer(_comparer);

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
                oldEntry.HashCode = _comparer.GetHashCode(oldEntry.Value) & 0x7FFFFFFF;
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
    /// Searches for an entry in the hash set by following its probing sequence, returning its index if found.
    /// </summary>
    /// <param name="item"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindEntry(T item)
    {
        if (_ItemCanBeNull && item is null) return -1;

        int hashCode = _comparer.GetHashCode(item) & 0x7FFFFFFF;
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
            if (entry.IsUsed && entry.HashCode == hashCode && _comparer.Equals(entry.Value, item))
                return entryIndex; // Match found

            // Perform quadratic probing to see if maybe the entry was shifted.
            step++;
            entryIndex = (entryIndex + step * step) & _entryMask;

        }
        return -1; // Item not found, full loop completed
    }

    #endregion

    #region Enumerators

    /// <summary>
    /// Returns an enumerator that iterates through the set.
    /// </summary>
    /// <returns>A value-type enumerator in increasing hash-slot order.</returns>
    /// <remarks>
    /// Mutation versions are checked by MoveNext and Reset. Ordering is not a persistence contract.
    /// </remarks>
    public SwiftHashSetEnumerator GetEnumerator() => new(this);
    /// <summary>
    /// Creates an enumerator through the implemented interface.
    /// </summary>
    /// <returns>An enumerator with the concrete collection's ordering and mutation rules. Interface dispatch may
    /// box the value-type enumerator.</returns>
    IEnumerator<T> IEnumerable<T>.GetEnumerator() => GetEnumerator();
    /// <summary>
    /// Creates an enumerator through the implemented interface.
    /// </summary>
    /// <returns>An enumerator with the concrete collection's ordering and mutation rules. Interface dispatch may
    /// box the value-type enumerator.</returns>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();



    #endregion

    #region ISet<T> Implementations

    /// <summary>
    /// Removes elements belonging to the other input.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Removes members of other; self-exclusion clears this set. Input is consumed without retaining its storage.
    /// Enumeration or hashing failures may leave partial mutation; no rollback is guaranteed.
    /// </remarks>
    public void ExceptWith(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        if (ReferenceEquals(this, other))
        {
            Clear();
            return;
        }

        var otherSet = new SwiftHashSet<T>(other, _comparer);

        foreach (var item in otherSet)
            Remove(item);
    }

    /// <summary>
    /// Retains elements belonging to both inputs.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Retains only members also in other; self-intersection changes nothing. Input is consumed without retaining
    /// its storage. Enumeration or hashing failures may leave partial mutation; no rollback is guaranteed.
    /// </remarks>
    public void IntersectWith(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        if (ReferenceEquals(this, other))
            return;

        var otherSet = new SwiftHashSet<T>(other, _comparer);

        for (int i = 0; i <= _lastIndex; i++)
        {
            if (_entries[i].IsUsed)
            {
                var value = _entries[i].Value;
                if (!otherSet.Contains(value))
                    Remove(value);
            }
        }
    }

    /// <summary>
    /// Checks for a subset with fewer distinct elements.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <returns>true if this set is a subset of other with fewer distinct elements; otherwise false. This set is
    /// not mutated.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Uses this set's equality semantics. Duplicate input values do not change set membership; temporary set
    /// allocation may occur.
    /// </remarks>
    public bool IsProperSubsetOf(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        var otherSet = new SwiftHashSet<T>(other, _comparer);

        if (Count >= otherSet.Count)
            return false;

        for (int i = 0; i <= _lastIndex; i++)
            if (_entries[i].IsUsed && !otherSet.Contains(_entries[i].Value))
                return false;

        return true;
    }

    /// <summary>
    /// Checks for a superset with more distinct elements.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <returns>true if this set is a superset of other with more distinct elements; otherwise false. This set is
    /// not mutated.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Uses this set's equality semantics. Duplicate input values do not change set membership; temporary set
    /// allocation may occur.
    /// </remarks>
    public bool IsProperSupersetOf(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        var otherSet = new SwiftHashSet<T>(other, _comparer);

        if (Count <= otherSet.Count)
            return false;

        foreach (var item in otherSet)
            if (!Contains(item))
                return false;

        return true;
    }

    /// <summary>
    /// Checks whether all stored elements belong to the other input.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <returns>true if every element of this set belongs to other; otherwise false. This set is not
    /// mutated.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Uses this set's equality semantics. Duplicate input values do not change set membership; temporary set
    /// allocation may occur.
    /// </remarks>
    public bool IsSubsetOf(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        var otherSet = new SwiftHashSet<T>(other, _comparer);

        if (otherSet.Count < Count)
            return false;

        for (int i = 0; i <= _lastIndex; i++)
            if (_entries[i].IsUsed && !otherSet.Contains(_entries[i].Value))
                return false;

        return true;
    }

    /// <summary>
    /// Checks whether all other-input elements are stored here.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <returns>true if every distinct element of other belongs to this set; otherwise false. This set is not
    /// mutated.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Uses this set's equality semantics. Duplicate input values do not change set membership; temporary set
    /// allocation may occur.
    /// </remarks>
    public bool IsSupersetOf(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        foreach (var item in other)
        {
            if (!Contains(item))
                return false;
        }

        return true;
    }

    /// <summary>
    /// Checks whether at least one element is shared.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <returns>true if at least one element is shared; otherwise false. This set is not mutated.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Uses this set's equality semantics. Duplicate input values do not change set membership; temporary set
    /// allocation may occur.
    /// </remarks>
    public bool Overlaps(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        foreach (var item in other)
            if (Contains(item))
                return true;

        return false;
    }

    /// <summary>
    /// Checks whether both inputs contain the same distinct elements.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <returns>true if both sets contain exactly the same distinct elements; otherwise false. This set is not
    /// mutated.</returns>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Uses this set's equality semantics. Duplicate input values do not change set membership; temporary set
    /// allocation may occur.
    /// </remarks>
    public bool SetEquals(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        var otherSet = new SwiftHashSet<T>(other, _comparer);

        if (otherSet.Count != Count)
            return false;

        foreach (var item in otherSet)
            if (!Contains(item))
                return false;

        return true;
    }

    /// <summary>
    /// Retains elements belonging to exactly one input.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Keeps elements belonging to exactly one input; duplicate input occurrences are treated once.
    /// Self-comparison clears this set. Input is consumed without retaining its storage. Enumeration or hashing
    /// failures may leave partial mutation; no rollback is guaranteed.
    /// </remarks>
    public void SymmetricExceptWith(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        if (ReferenceEquals(this, other))
        {
            Clear();
            return;
        }

        var otherSet = new SwiftHashSet<T>(other, _comparer);

        foreach (var item in otherSet)
        {
            if (!Remove(item))
                Add(item);
        }
    }

    /// <summary>
    /// Adds elements belonging to the other input.
    /// </summary>
    /// <param name="other">The non-null sequence whose distinct elements define the other set; the sequence is
    /// consumed, not retained.</param>
    /// <exception cref="T:System.ArgumentNullException">other is null.</exception>
    /// <remarks>
    /// Adds members of other, preserving existing members and ignoring duplicates. Input is consumed without
    /// retaining its storage. Enumeration or hashing failures may leave partial mutation; no rollback is
    /// guaranteed.
    /// </remarks>
    public void UnionWith(IEnumerable<T> other)
    {
        ChronicleGuard.ThrowIfNull(other, nameof(other));

        foreach (var item in other)
            Add(item);
    }

    #endregion
}
