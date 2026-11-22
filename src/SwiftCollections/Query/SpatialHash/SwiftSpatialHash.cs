//=======================================================================
// SwiftSpatialHash.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Chronicler.Diagnostics;

namespace SwiftCollections.Query;

/// <summary>
/// Indexes keyed bounds in grid cells using a supplied or built-in volume mapper.
/// </summary>
/// <typeparam name="TKey">The stable key type; equality and hashing must remain unchanged while
/// indexed.</typeparam>
/// <typeparam name="TVolume">The value-type volume backend providing consistent intersection, union, equality and
/// cost operations.</typeparam>
/// <remarks>
/// Mutable runtime index: synchronize all access externally, including queries that use shared scratch/stamps.
/// Key and volume callbacks must not reenter or mutate the index. Bounds are copied and keys are retained;
/// changing a key's equality/hash breaks lookup. Query ordering depends on current topology and mutation history;
/// it is not sorted or a serialized order guarantee. Determinism requires deterministic keys, input order and
/// volume/mapping arithmetic. A key is indexed once; duplicate Insert updates its bounds and returns false. A
/// query adds each matching stored key once, independently of keys already in the destination.
/// </remarks>
public class SwiftSpatialHash<TKey, TVolume>
    where TKey : notnull
    where TVolume : struct, IBoundVolume<TVolume>
{
    // Key-map callbacks must observe this field, never a captured copy of the mutable core.
    private SpatialHashCore<TKey, TVolume, SwiftSpatialHashCellIndex, SpatialHashGrid3D<TVolume>> _core;

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftSpatialHash`2" /> class.
    /// </summary>
    /// <param name="capacity">The requested initial storage length in entries. Non-positive values normalize to
    /// one; growth rounds up. Keep capacities below 2^30 and within available array memory.</param>
    /// <param name="cellMapper">The non-null mapper retained by reference. It must return consistent inclusive
    /// ranges for insertion, update, removal and querying, without mutating this index.</param>
    /// <exception cref="T:System.ArgumentNullException">The supplied backend is null.</exception>
    public SwiftSpatialHash(int capacity, ISpatialHashCellMapper<TVolume> cellMapper)
        : this(capacity, cellMapper, SwiftSpatialHashOptions.Default)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftSpatialHash`2" /> class.
    /// </summary>
    /// <param name="capacity">The requested initial storage length in entries. Non-positive values normalize to
    /// one; growth rounds up. Keep capacities below 2^30 and within available array memory.</param>
    /// <param name="cellMapper">The non-null mapper retained by reference. It must return consistent inclusive
    /// ranges for insertion, update, removal and querying, without mutating this index.</param>
    /// <param name="options">The options copied by value. Default property padding is one;
    /// default(SwiftSpatialHashOptions) padding is zero.</param>
    /// <exception cref="T:System.ArgumentNullException">The supplied backend is null.</exception>
    public SwiftSpatialHash(int capacity, ISpatialHashCellMapper<TVolume> cellMapper, SwiftSpatialHashOptions options)
    {
        ChronicleGuard.ThrowIfNull(cellMapper, nameof(cellMapper));
        _core = new SpatialHashCore<TKey, TVolume, SwiftSpatialHashCellIndex, SpatialHashGrid3D<TVolume>>(
            capacity, new SpatialHashGrid3D<TVolume>(cellMapper), options, MatchesEntryKey, IsAllocatedEntry, GetEntryKey);
    }

    /// <summary>
    /// Gets the number of active entries stored in the spatial hash.
    /// </summary>
    /// <value>The number of indexed distinct keys.</value>
    public int Count => _core.Count;

    /// <summary>
    /// Gets the options used by this spatial hash.
    /// </summary>
    /// <value>The constructor-supplied options value; later changes to the caller's copy have no effect.</value>
    public SwiftSpatialHashOptions Options => _core.Options;

    /// <summary>
    /// Inserts a key and copied bounds, or updates the bounds of an existing key.
    /// </summary>
    /// <param name="key">The non-null stable key to insert or locate. A new key is retained without cloning;
    /// an equal existing key keeps its original canonical key.</param>
    /// <param name="bounds">The volume in the same caller-defined coordinate space and units as other indexed
    /// bounds; copied by value. No transform is applied.</param>
    /// <returns>true for a new key; false for an existing key whose bounds have nevertheless been
    /// updated.</returns>
    /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
    /// <remarks>
    /// This operation is not transactional if key hashing, volume operations, mapping, or allocation throws.
    /// Existing bounds may be relocated even though false is returned.
    /// </remarks>
    public bool Insert(TKey key, TVolume bounds) => _core.Insert(key, bounds);

    /// <summary>
    /// Removes an entry from the spatial hash.
    /// </summary>
    /// <param name="key">The non-null key identifying the entry to remove. This lookup value is not retained.</param>
    /// <returns>true if one matching indexed entry was removed; false if absent, with entries
    /// unchanged.</returns>
    /// <exception cref="T:System.ArgumentNullException">The key is null.</exception>
    public bool Remove(TKey key) => _core.Remove(key);

    /// <summary>
    /// Updates the bounds for an existing entry.
    /// </summary>
    /// <param name="key">The non-null key identifying the entry to update. This lookup value is not retained;
    /// the original canonical key is preserved.</param>
    /// <param name="newBounds">Replacement bounds in the same coordinate space; copied by value. No transform is
    /// applied.</param>
    /// <returns>true if the key exists, including unchanged bounds; false if absent, without changing
    /// entries.</returns>
    /// <exception cref="T:System.ArgumentNullException">The key is null.</exception>
    /// <remarks>
    /// Updates one matching key's bounds; absent keys change nothing. Bounds are copied by value; the caller's
    /// original is not retained. Callback/allocation failures can leave partial mutation.
    /// </remarks>
    public bool UpdateEntryBounds(TKey key, TVolume newBounds) => _core.UpdateEntryBounds(key, newBounds);

    /// <summary>
    /// Determines whether the spatial hash contains the specified key.
    /// </summary>
    /// <param name="key">The non-null key to locate. This lookup value is not retained.</param>
    /// <returns>true if the key is indexed; otherwise false. No entries change.</returns>
    /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
    public bool Contains(TKey key) => _core.Contains(key);

    /// <summary>
    /// Attempts to retrieve the bounds registered for the supplied key.
    /// </summary>
    /// <param name="key">The non-null key to locate. This lookup value is not retained.</param>
    /// <param name="bounds">Receives a copy of the stored bounds on success; default(TVolume) when the key is
    /// absent.</param>
    /// <returns>true if the key is indexed and bounds is valid; false if absent, with bounds set to default. No
    /// entries change.</returns>
    /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
    public bool TryGetBounds(TKey key, out TVolume bounds) => _core.TryGetBounds(key, out bounds);

    /// <summary>
    /// Queries the spatial hash and returns only entries whose bounds intersect the supplied query volume.
    /// </summary>
    /// <param name="queryBounds">The query region in the index coordinate space, copied by value; it is not
    /// retained or transformed.</param>
    /// <param name="results">The caller-owned mutable collection to append matching keys to. Existing entries are
    /// preserved; no deduplication against pre-existing entries is performed.</param>
    /// <exception cref="T:System.ArgumentNullException">results is null.</exception>
    /// <remarks>
    /// Appends without clearing results. Empty/no-hit queries preserve existing entries. Destination Add or
    /// volume/backend exceptions can leave partially appended output; no rollback is provided. Do not mutate the
    /// index from result callbacks. Visits cells in increasing X, then Y, then Z (Z fastest), suppressing repeat
    /// visits of the same stored entry. Tests actual volume intersection after candidate gathering.
    /// </remarks>
    public void Query(TVolume queryBounds, ICollection<TKey> results) => _core.Query(queryBounds, results);

    /// <summary>
    /// Queries the spatial hash using the supplied query volume plus the configured neighborhood padding.
    /// </summary>
    /// <param name="queryBounds">The query region in the index coordinate space, copied by value; it is not
    /// retained or transformed.</param>
    /// <param name="results">The caller-owned mutable collection to append matching keys to. Existing entries are
    /// preserved; no deduplication against pre-existing entries is performed.</param>
    /// <exception cref="T:System.ArgumentNullException">results is null.</exception>
    /// <remarks>
    /// Appends without clearing results. Empty/no-hit queries preserve existing entries. Destination Add or
    /// volume/backend exceptions can leave partially appended output; no rollback is provided. Do not mutate the
    /// index from result callbacks. Visits cells in increasing X, then Y, then Z (Z fastest), suppressing repeat
    /// visits of the same stored entry. Returns cell-neighborhood candidates without testing actual bounds
    /// intersection; padding is clipped to Int32 cell-coordinate limits.
    /// </remarks>
    public void QueryNeighborhood(TVolume queryBounds, ICollection<TKey> results) => _core.QueryNeighborhood(queryBounds, results);

    /// <summary>
    /// Collects every entry registered in one already-mapped spatial cell.
    /// </summary>
    /// <param name="cell">The signed integer cell coordinate to inspect; copied by value.</param>
    /// <param name="results">The caller-owned mutable collection to append matching keys to. Existing entries are
    /// preserved; no deduplication against pre-existing entries is performed.</param>
    /// <exception cref="T:System.ArgumentNullException">results is null.</exception>
    /// <remarks>
    /// Appends all stored candidates for one cell, without testing point/volume intersection and without clearing
    /// results. A stored entry occupies a cell at most once; existing destination duplicates are not removed.
    /// </remarks>
    protected void CollectCellCandidates(
        SwiftSpatialHashCellIndex cell,
        ICollection<TKey> results) => _core.CollectCellCandidates(cell, results);

    /// <summary>
    /// Ensures the spatial hash can store the specified number of entries without growing its entry storage.
    /// </summary>
    /// <param name="capacity">The requested initial storage length in entries. Non-positive values normalize to
    /// one; growth rounds up. Keep capacities below 2^30 and within available array memory.</param>
    /// <remarks>
    /// Grows only when the normalized requested length exceeds existing storage. No entries are removed. Capacity is entry storage,
    /// not a no-allocation guarantee for later mutations.
    /// </remarks>
    public void EnsureCapacity(int capacity)
        => _core.EnsureCapacity(capacity);

    /// <summary>
    /// Removes all entries and cell registrations from the spatial hash.
    /// </summary>
    /// <remarks>
    /// Removes all entries and retained keys while preserving reusable entry capacity. Clears occupied-cell lists
    /// and query stamps; subsequent inserts can allocate cell lists.
    /// </remarks>
    public void Clear() => _core.Clear();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MatchesEntryKey(int index, TKey key) => _core.MatchesEntryKey(index, key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsAllocatedEntry(int index) => _core.IsAllocatedEntry(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TKey GetEntryKey(int index) => _core.GetEntryKey(index);
}
