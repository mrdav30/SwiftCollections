//=======================================================================
// SwiftSpatialHash.cs
//=======================================================================
// MIT License, Copyright (c) 2024–present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using SwiftCollections.Diagnostics;
using SwiftCollections.Utility;

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

        capacity = SwiftHashTools.NextPowerOfTwo(capacity);

        _cellMapper = cellMapper;
        _keyToEntryIndex = new QueryKeyIndexMap<TKey>(capacity, MatchesEntryKey, IsAllocatedEntry, GetEntryKey);
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
        SwiftThrowHelper.ThrowIfNull(key, nameof(key));

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
        SwiftThrowHelper.ThrowIfNullGeneric(key, nameof(key));

        int entryIndex = FindEntryIndex(key);
        if (entryIndex < 0)
            return false;

        RemoveEntryFromCells(entryIndex, _entries[entryIndex].Bounds);
        _keyToEntryIndex.Remove(key);
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
        SwiftThrowHelper.ThrowIfNullGeneric(key, nameof(key));

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
        SwiftThrowHelper.ThrowIfNullGeneric(key, nameof(key));
        return FindEntryIndex(key) >= 0;
    }

    /// <summary>
    /// Attempts to retrieve the bounds registered for the supplied key.
    /// </summary>
    public bool TryGetBounds(TKey key, out TVolume bounds)
    {
        SwiftThrowHelper.ThrowIfNullGeneric(key, nameof(key));

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
        SwiftThrowHelper.ThrowIfNull(results, nameof(results));
        ExecuteQuery(queryBounds, 0, true, results);
    }

    /// <summary>
    /// Queries the spatial hash using the supplied query volume plus the configured neighborhood padding.
    /// </summary>
    public void QueryNeighborhood(TVolume queryBounds, ICollection<TKey> results)
    {
        SwiftThrowHelper.ThrowIfNull(results, nameof(results));
        ExecuteQuery(queryBounds, Options.NeighborhoodPadding, false, results);
    }

    /// <summary>
    /// Collects every entry registered in one already-mapped spatial cell.
    /// </summary>
    protected void CollectCellCandidates(
        SwiftSpatialHashCellIndex cell,
        ICollection<TKey> results)
    {
        SwiftThrowHelper.ThrowIfNull(results, nameof(results));
        if (!_cells.TryGetValue(cell, out SwiftList<int> entryIndices))
            return;

        for (int i = 0; i < entryIndices.Count; i++)
            results.Add(_entries[entryIndices[i]].Key);
    }

    /// <summary>
    /// Ensures the spatial hash can store the specified number of entries without growing its entry storage.
    /// </summary>
    public void EnsureCapacity(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);
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

        long minX = Math.Max(int.MinValue, (long)minCell.X - padding);
        long minY = Math.Max(int.MinValue, (long)minCell.Y - padding);
        long minZ = Math.Max(int.MinValue, (long)minCell.Z - padding);
        long maxX = Math.Min(int.MaxValue, (long)maxCell.X + padding);
        long maxY = Math.Min(int.MaxValue, (long)maxCell.Y + padding);
        long maxZ = Math.Min(int.MaxValue, (long)maxCell.Z + padding);

        for (long x = minX; x <= maxX; x++)
        {
            for (long y = minY; y <= maxY; y++)
            {
                for (long z = minZ; z <= maxZ; z++)
                {
                    var cell = new SwiftSpatialHashCellIndex((int)x, (int)y, (int)z);
                    ProcessQueryCell(cell, queryBounds, queryStamp, requireIntersection, results);
                }
            }
        }
    }

    private void ProcessQueryCell(
        SwiftSpatialHashCellIndex cell,
        TVolume queryBounds,
        int queryStamp,
        bool requireIntersection,
        ICollection<TKey> results)
    {
        if (!_cells.TryGetValue(cell, out SwiftList<int> entryIndices))
            return;

        for (int i = 0; i < entryIndices.Count; i++)
            TryAddQueryResult(entryIndices[i], queryBounds, queryStamp, requireIntersection, results);
    }

    private void TryAddQueryResult(
        int entryIndex,
        TVolume queryBounds,
        int queryStamp,
        bool requireIntersection,
        ICollection<TKey> results)
    {
        ref SpatialHashEntry entry = ref _entries[entryIndex];
        if (entry.QueryStamp == queryStamp)
            return;

        entry.QueryStamp = queryStamp;

        if (requireIntersection && !entry.Bounds.Intersects(queryBounds))
            return;

        results.Add(entry.Key);
    }

    private void AddEntryToCells(int entryIndex, TVolume bounds)
    {
        _cellMapper.GetCellRange(bounds, out SwiftSpatialHashCellIndex minCell, out SwiftSpatialHashCellIndex maxCell);

        for (long x = minCell.X; x <= maxCell.X; x++)
        {
            for (long y = minCell.Y; y <= maxCell.Y; y++)
            {
                for (long z = minCell.Z; z <= maxCell.Z; z++)
                {
                    var cell = new SwiftSpatialHashCellIndex((int)x, (int)y, (int)z);
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

        for (long x = minCell.X; x <= maxCell.X; x++)
        {
            for (long y = minCell.Y; y <= maxCell.Y; y++)
            {
                for (long z = minCell.Z; z <= maxCell.Z; z++)
                {
                    var cell = new SwiftSpatialHashCellIndex((int)x, (int)y, (int)z);
                    RemoveEntryFromCell(cell, entryIndex);
                }
            }
        }
    }

    private void RemoveEntryFromCell(SwiftSpatialHashCellIndex cell, int entryIndex)
    {
        SwiftList<int> entryIndices = _cells[cell];
        RemoveEntryIndex(entryIndices, entryIndex);
        if (entryIndices.Count == 0)
            _cells.Remove(cell);
    }

    private static void RemoveEntryIndex(SwiftList<int> entryIndices, int entryIndex)
    {
        entryIndices.RemoveAt(entryIndices.IndexOf(entryIndex));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int FindEntryIndex(TKey key)
    {
        return _keyToEntryIndex.Find(key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ResizeEntryStorage(int newCapacity)
    {
        Array.Resize(ref _entries, newCapacity);
        _cells.EnsureCapacity(newCapacity);
        _keyToEntryIndex.ResizeAndRehash(newCapacity, _peakCount);
        SwiftCollectionDiagnostics.Shared.Info($"Resized spatial hash entry storage to {newCapacity} entries.", _diagnosticSource);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int RentQueryStamp()
    {
        if (_queryStamp == int.MaxValue)
        {
            for (int i = 0; i < _peakCount; i++)
                _entries[i].QueryStamp = 0;

            _queryStamp = 0;
            SwiftCollectionDiagnostics.Shared.Warn($"Query stamp overflow detected. Spatial hash query stamps were reset.", _diagnosticSource);
        }

        return ++_queryStamp;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MatchesEntryKey(int index, TKey key)
    {
        return EqualityComparer<TKey>.Default.Equals(_entries[index].Key, key);
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
