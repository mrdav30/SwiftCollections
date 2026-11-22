//=======================================================================
// SpatialHashCore.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Chronicler.Diagnostics;
using SwiftCollections.Diagnostics;
using SwiftCollections.Utility;

namespace SwiftCollections.Query;

/// <summary>
/// Owns entries, cell membership, relocation and duplicate-safe queries shared by 2D and 3D spatial hashes.
/// </summary>
internal struct SpatialHashCore<TKey, TVolume, TCell, TMapping>
    where TKey : notnull
    where TVolume : struct, IBoundVolume<TVolume>
    where TCell : struct, IEquatable<TCell>
    where TMapping : struct, ISpatialHashGrid<TVolume, TCell>
{
    private const string _diagnosticSource = "SwiftSpatialHash";

    private readonly TMapping _mapping;
    private readonly QueryKeyIndexMap<TKey> _keyToEntryIndex;
    private readonly SwiftDictionary<TCell, SwiftList<int>> _cells;
    private readonly SwiftIntStack _freeEntries;

    private SpatialHashEntry[] _entries;
    private int _peakCount;
    private int _count;
    private int _queryStamp;
    private readonly int _neighborhoodPadding;

    internal SpatialHashCore(int capacity, TMapping mapping, SwiftSpatialHashOptions options,
        Func<int, TKey, bool> matchesEntryKey, Func<int, bool> isAllocatedEntry, Func<int, TKey> getEntryKey)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);
        _mapping = mapping;
        _keyToEntryIndex = new QueryKeyIndexMap<TKey>(capacity, matchesEntryKey, isAllocatedEntry, getEntryKey);
        _cells = new SwiftDictionary<TCell, SwiftList<int>>(capacity);
        _freeEntries = new SwiftIntStack();
        _entries = new SpatialHashEntry[capacity];
        _peakCount = 0;
        _count = 0;
        _queryStamp = 0;
        _neighborhoodPadding = options.NeighborhoodPadding;
    }

    internal int Count => _count;

    internal SwiftSpatialHashOptions Options => new SwiftSpatialHashOptions(_neighborhoodPadding);

    internal bool Insert(TKey key, TVolume bounds)
    {
        ChronicleGuard.ThrowIfNull(key, nameof(key));

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

    internal bool Remove(TKey key)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));

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

    internal bool UpdateEntryBounds(TKey key, TVolume newBounds)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));

        int entryIndex = FindEntryIndex(key);
        if (entryIndex < 0)
            return false;

        return UpdateEntryBounds(entryIndex, newBounds);
    }

    internal bool Contains(TKey key)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));
        return FindEntryIndex(key) >= 0;
    }

    internal bool TryGetBounds(TKey key, out TVolume bounds)
    {
        ChronicleGuard.ThrowIfNullGeneric(key, nameof(key));

        int entryIndex = FindEntryIndex(key);
        if (entryIndex < 0)
        {
            bounds = default;
            return false;
        }

        bounds = _entries[entryIndex].Bounds;
        return true;
    }

    internal void Query(TVolume queryBounds, ICollection<TKey> results)
    {
        ChronicleGuard.ThrowIfNull(results, nameof(results));
        ExecuteQuery(queryBounds, 0, true, results);
    }

    internal void QueryNeighborhood(TVolume queryBounds, ICollection<TKey> results)
    {
        ChronicleGuard.ThrowIfNull(results, nameof(results));
        ExecuteQuery(queryBounds, _neighborhoodPadding, false, results);
    }

    internal void CollectCellCandidates(
        TCell cell,
        ICollection<TKey> results)
    {
        ChronicleGuard.ThrowIfNull(results, nameof(results));
        if (!_cells.TryGetValue(cell, out SwiftList<int> entryIndices))
            return;

        for (int i = 0; i < entryIndices.Count; i++)
            results.Add(_entries[entryIndices[i]].Key);
    }

    internal void EnsureCapacity(int capacity)
    {
        capacity = SwiftHashTools.NextPowerOfTwo(capacity);
        if (capacity <= _entries.Length)
            return;

        ResizeEntryStorage(capacity);
    }

    internal void Clear()
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
        if (!_mapping.TryGetRange(queryBounds, padding, out TCell minCell, out TCell maxCell))
            return;

        TCell cell = minCell;
        do
        {
            ProcessQueryCell(cell, queryBounds, queryStamp, requireIntersection, results);
        } while (_mapping.MoveNext(ref cell, minCell, maxCell));
    }

    private void ProcessQueryCell(
        TCell cell,
        TVolume queryBounds,
        int queryStamp,
        bool requireIntersection,
        ICollection<TKey> results)
    {
        if (!_cells.TryGetValue(cell, out SwiftList<int> entryIndices))
            return;

        // Query callbacks must not mutate this index, so cell storage stays stable.
        ReadOnlySpan<int> indices = entryIndices.AsReadOnlySpan();
        for (int i = 0; i < indices.Length; i++)
            TryAddQueryResult(indices[i], queryBounds, queryStamp, requireIntersection, results);
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
        if (!_mapping.TryGetRange(bounds, 0, out TCell minCell, out TCell maxCell))
            return;

        TCell cell = minCell;
        do
        {
            if (!_cells.TryGetValue(cell, out SwiftList<int> entryIndices))
            {
                entryIndices = new SwiftList<int>(1);
                _cells[cell] = entryIndices;
            }
            entryIndices.Add(entryIndex);
        } while (_mapping.MoveNext(ref cell, minCell, maxCell));
    }

    private void RemoveEntryFromCells(int entryIndex, TVolume bounds)
    {
        if (!_mapping.TryGetRange(bounds, 0, out TCell minCell, out TCell maxCell))
            return;

        TCell cell = minCell;
        do
        {
            RemoveEntryFromCell(cell, entryIndex);
        } while (_mapping.MoveNext(ref cell, minCell, maxCell));
    }

    private void RemoveEntryFromCell(TCell cell, int entryIndex)
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
    internal bool MatchesEntryKey(int index, TKey key)
    {
        return EqualityComparer<TKey>.Default.Equals(_entries[index].Key, key);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsAllocatedEntry(int index) => _entries[index].IsAllocated;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TKey GetEntryKey(int index) => _entries[index].Key;

    private struct SpatialHashEntry
    {
        internal TKey Key;
        internal TVolume Bounds;
        internal int QueryStamp;
        internal bool IsAllocated;

        internal void Reset()
        {
            Key = default!;
            Bounds = default;
            QueryStamp = 0;
            IsAllocated = false;
        }
    }
}
