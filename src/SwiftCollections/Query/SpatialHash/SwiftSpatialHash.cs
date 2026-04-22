using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace SwiftCollections.Query;

/// <summary>
/// Represents a mutable spatial hash that indexes keyed bounding volumes into deterministic integer grid cells.
/// </summary>
/// <typeparam name="TKey">The key used to identify each stored entry.</typeparam>
/// <typeparam name="TVolume">The volume type used for broad-phase registration and queries.</typeparam>
public class SwiftSpatialHash<TKey, TVolume>
    where TKey : notnull
    where TVolume : struct, IBoundVolume<TVolume>
{
    private const string _diagnosticSource = nameof(SwiftSpatialHash<TKey, TVolume>);

    private readonly ISpatialHashCellMapper<TVolume> _cellMapper;
    private readonly QueryKeyIndexMap<TKey> _keyToEntryIndex;
    private readonly SwiftDictionary<SwiftSpatialHashCellIndex, SwiftList<int>> _cells;
    private readonly SwiftIntStack _freeEntries;

    private SpatialHashEntry[] _entries;
    private int _peakCount;
    private int _count;
    private int _queryStamp;

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftSpatialHash{TKey, TVolume}"/> class.
    /// </summary>
    /// <param name="capacity">The initial entry capacity.</param>
    /// <param name="cellMapper">The mapper that projects volumes into deterministic cell coordinates.</param>
    public SwiftSpatialHash(int capacity, ISpatialHashCellMapper<TVolume> cellMapper)
        : this(capacity, cellMapper, SwiftSpatialHashOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SwiftSpatialHash{TKey, TVolume}"/> class.
    /// </summary>
    /// <param name="capacity">The initial entry capacity.</param>
    /// <param name="cellMapper">The mapper that projects volumes into deterministic cell coordinates.</param>
    /// <param name="options">Spatial hash query options.</param>
    public SwiftSpatialHash(int capacity, ISpatialHashCellMapper<TVolume> cellMapper, SwiftSpatialHashOptions options)
    {
        SwiftThrowHelper.ThrowIfNull(cellMapper, nameof(cellMapper));

        capacity = QueryCollectionGuards.NormalizeCapacity(capacity);

        _cellMapper = cellMapper;
        _keyToEntryIndex = new QueryKeyIndexMap<TKey>(capacity);
        _cells = new SwiftDictionary<SwiftSpatialHashCellIndex, SwiftList<int>>(capacity);
        _freeEntries = new SwiftIntStack();
        _entries = new SpatialHashEntry[capacity];
        Options = options;
    }

    /// <summary>
    /// Gets the number of active entries stored in the spatial hash.
    /// </summary>
    public int Count => _count;

    /// <summary>
    /// Gets the options used by this spatial hash.
    /// </summary>
    public SwiftSpatialHashOptions Options { get; }

    /// <summary>
    /// Inserts a new entry or replaces the bounds of an existing key.
    /// </summary>
    /// <param name="key">The entry key.</param>
    /// <param name="bounds">The entry bounds.</param>
    /// <returns><c>true</c> when a new key was added; <c>false</c> when an existing key was replaced.</returns>
    public bool Insert(TKey key, TVolume bounds)
    {
        QueryCollectionGuards.ThrowIfKeyNull(key, nameof(key));

        int existingIndex = FindEntryIndex(key);
        if (existingIndex >= 0)
        {
            UpdateEntryBounds(existingIndex, bounds);
            return false;
        }

        EnsureCapacity(_count + 1);

        int entryIndex = AllocateEntry(key, bounds);
        AddEntryToCells(entryIndex, bounds);
        _keyToEntryIndex.Insert(key, entryIndex);
        _count++;
        return true;
    }

    /// <summary>
    /// Removes an entry from the spatial hash.
    /// </summary>
    /// <param name="key">The entry key.</param>
    /// <returns><c>true</c> when the key existed and was removed; otherwise, <c>false</c>.</returns>
    public bool Remove(TKey key)
    {
        QueryCollectionGuards.ThrowIfKeyNull(key, nameof(key));

        int entryIndex = FindEntryIndex(key);
        if (entryIndex < 0)
            return false;

        RemoveEntryFromCells(entryIndex, _entries[entryIndex].Bounds);
        _keyToEntryIndex.Remove(key, MatchesEntryKey, IsAllocatedEntry, GetEntryKey);
        _entries[entryIndex].Reset();
        _freeEntries.Push(entryIndex);
        _count--;
        return true;
    }

    /// <summary>
    /// Updates the bounds for an existing entry.
    /// </summary>
    /// <param name="key">The entry key.</param>
    /// <param name="newBounds">The replacement bounds.</param>
    /// <returns><c>true</c> when the key existed; otherwise, <c>false</c>.</returns>
    public bool UpdateEntryBounds(TKey key, TVolume newBounds)
    {
        QueryCollectionGuards.ThrowIfKeyNull(key, nameof(key));

        int entryIndex = FindEntryIndex(key);
        if (entryIndex < 0)
            return false;

        return UpdateEntryBounds(entryIndex, newBounds);
    }

    /// <summary>
    /// Determines whether the spatial hash contains the specified key.
    /// </summary>
    public bool Contains(TKey key)
    {
        QueryCollectionGuards.ThrowIfKeyNull(key, nameof(key));
        return FindEntryIndex(key) >= 0;
    }

    /// <summary>
    /// Attempts to retrieve the bounds registered for the supplied key.
    /// </summary>
    public bool TryGetBounds(TKey key, out TVolume bounds)
    {
        QueryCollectionGuards.ThrowIfKeyNull(key, nameof(key));

        int entryIndex = FindEntryIndex(key);
        if (entryIndex < 0)
        {
            bounds = default;
            return false;
        }

        bounds = _entries[entryIndex].Bounds;
        return true;
    }

    /// <summary>
    /// Queries the spatial hash and returns only entries whose bounds intersect the supplied query volume.
    /// </summary>
    public void Query(TVolume queryBounds, ICollection<TKey> results)
    {
        QueryCollectionGuards.ThrowIfResultsCollectionNull(results, nameof(results));
        ExecuteQuery(queryBounds, 0, true, results);
    }

    /// <summary>
    /// Queries the spatial hash using the supplied query volume plus the configured neighborhood padding.
    /// </summary>
    public void QueryNeighborhood(TVolume queryBounds, ICollection<TKey> results)
    {
        QueryCollectionGuards.ThrowIfResultsCollectionNull(results, nameof(results));
        ExecuteQuery(queryBounds, Options.NeighborhoodPadding, false, results);
    }

    /// <summary>
    /// Ensures the spatial hash can store the specified number of entries without growing its entry storage.
    /// </summary>
    public void EnsureCapacity(int capacity)
    {
        capacity = QueryCollectionGuards.NormalizeCapacity(capacity);
        if (capacity <= _entries.Length)
            return;

        ResizeEntryStorage(capacity);
    }

    /// <summary>
    /// Removes all entries and cell registrations from the spatial hash.
    /// </summary>
    public void Clear()
    {
        if (_count == 0)
            return;

        for (int i = 0; i < _peakCount; i++)
            _entries[i].Reset();

        _cells.Clear();
        _keyToEntryIndex.Clear();
        _freeEntries.Reset();
        _peakCount = 0;
        _count = 0;
        _queryStamp = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AllocateEntry(TKey key, TVolume bounds)
    {
        int entryIndex;
        if (_freeEntries.Count > 0)
            entryIndex = _freeEntries.Pop();
        else
            entryIndex = _peakCount++;

        _entries[entryIndex].Key = key;
        _entries[entryIndex].Bounds = bounds;
        _entries[entryIndex].IsAllocated = true;
        _entries[entryIndex].QueryStamp = 0;
        return entryIndex;
    }

    private bool UpdateEntryBounds(int entryIndex, TVolume newBounds)
    {
        if (!_entries[entryIndex].IsAllocated)
            return false;

        TVolume currentBounds = _entries[entryIndex].Bounds;
        if (currentBounds.BoundsEquals(newBounds))
            return true;

        RemoveEntryFromCells(entryIndex, currentBounds);
        _entries[entryIndex].Bounds = newBounds;
        AddEntryToCells(entryIndex, newBounds);
        return true;
    }

    private void ExecuteQuery(TVolume queryBounds, int padding, bool requireIntersection, ICollection<TKey> results)
    {
        if (_count == 0)
            return;

        int queryStamp = RentQueryStamp();
        _cellMapper.GetCellRange(queryBounds, out SwiftSpatialHashCellIndex minCell, out SwiftSpatialHashCellIndex maxCell);

        for (int x = minCell.X - padding; x <= maxCell.X + padding; x++)
        {
            for (int y = minCell.Y - padding; y <= maxCell.Y + padding; y++)
            {
                for (int z = minCell.Z - padding; z <= maxCell.Z + padding; z++)
                {
                    var cell = new SwiftSpatialHashCellIndex(x, y, z);
                    if (!_cells.TryGetValue(cell, out SwiftList<int> entryIndices))
                        continue;

                    for (int i = 0; i < entryIndices.Count; i++)
                    {
                        int entryIndex = entryIndices[i];
                        ref SpatialHashEntry entry = ref _entries[entryIndex];
                        if (!entry.IsAllocated || entry.QueryStamp == queryStamp)
                            continue;

                        entry.QueryStamp = queryStamp;

                        if (requireIntersection && !entry.Bounds.Intersects(queryBounds))
                            continue;

                        results.Add(entry.Key);
                    }
                }
            }
        }
    }

    private void AddEntryToCells(int entryIndex, TVolume bounds)
    {
        _cellMapper.GetCellRange(bounds, out SwiftSpatialHashCellIndex minCell, out SwiftSpatialHashCellIndex maxCell);

        for (int x = minCell.X; x <= maxCell.X; x++)
        {
            for (int y = minCell.Y; y <= maxCell.Y; y++)
            {
                for (int z = minCell.Z; z <= maxCell.Z; z++)
                {
                    var cell = new SwiftSpatialHashCellIndex(x, y, z);
                    if (!_cells.TryGetValue(cell, out SwiftList<int> entryIndices))
                    {
                        entryIndices = new SwiftList<int>(1);
                        _cells[cell] = entryIndices;
                    }

                    entryIndices.Add(entryIndex);
                }
            }
        }
    }

    private void RemoveEntryFromCells(int entryIndex, TVolume bounds)
    {
        _cellMapper.GetCellRange(bounds, out SwiftSpatialHashCellIndex minCell, out SwiftSpatialHashCellIndex maxCell);

        for (int x = minCell.X; x <= maxCell.X; x++)
        {
            for (int y = minCell.Y; y <= maxCell.Y; y++)
            {
                for (int z = minCell.Z; z <= maxCell.Z; z++)
                {
                    var cell = new SwiftSpatialHashCellIndex(x, y, z);
                    if (!_cells.TryGetValue(cell, out SwiftList<int> entryIndices))
                        continue;

                    for (int i = 0; i < entryIndices.Count; i++)
                    {
                        if (entryIndices[i] != entryIndex)
                            continue;

                        entryIndices.RemoveAt(i);
                        break;
                    }

                    if (entryIndices.Count == 0)
                        _cells.Remove(cell);
                }
            }
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindEntryIndex(TKey key)
    {
        return _keyToEntryIndex.Find(key, MatchesEntryKey);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResizeEntryStorage(int newCapacity)
    {
        Array.Resize(ref _entries, newCapacity);
        _cells.EnsureCapacity(newCapacity);
        _keyToEntryIndex.ResizeAndRehash(newCapacity, _peakCount, IsAllocatedEntry, GetEntryKey);
        QueryCollectionDiagnostics.WriteInfo(_diagnosticSource, $"Resized spatial hash entry storage to {newCapacity} entries.");
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RentQueryStamp()
    {
        if (_queryStamp == int.MaxValue)
        {
            for (int i = 0; i < _peakCount; i++)
                _entries[i].QueryStamp = 0;

            _queryStamp = 0;
            QueryCollectionDiagnostics.WriteWarning(_diagnosticSource, "Query stamp overflow detected. Spatial hash query stamps were reset.");
        }

        return ++_queryStamp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MatchesEntryKey(int index, TKey key)
    {
        return _entries[index].IsAllocated && EqualityComparer<TKey>.Default.Equals(_entries[index].Key, key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsAllocatedEntry(int index) => _entries[index].IsAllocated;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TKey GetEntryKey(int index) => _entries[index].Key;

    private struct SpatialHashEntry
    {
        public TKey Key;
        public TVolume Bounds;
        public int QueryStamp;
        public bool IsAllocated;

        public void Reset()
        {
            Key = default!;
            Bounds = default;
            QueryStamp = 0;
            IsAllocated = false;
        }
    }
}
