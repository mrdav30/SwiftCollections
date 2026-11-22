//=======================================================================
// SwiftOctree.cs
//=======================================================================
// MIT License, Copyright (c) 2024-present David Oravsky (mrdav30)
// See LICENSE file in the project root for full license information.
//=======================================================================

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Chronicler.Diagnostics;

namespace SwiftCollections.Query;

/// <summary>
/// Represents a mutable octree that stores keyed bounding volumes within immutable world bounds.
/// </summary>
/// <typeparam name="TKey">The stable key type; equality and hashing must remain unchanged while
/// indexed.</typeparam>
/// <typeparam name="TVolume">The value-type volume backend providing consistent intersection, union, equality and
/// cost operations.</typeparam>
/// <remarks>
/// Mutable runtime index: synchronize access externally, including queries concurrent with mutations.
/// Key and volume callbacks must not reenter or mutate the index. Bounds are copied and keys are retained;
/// changing a key's equality/hash breaks lookup. Query ordering depends on current topology and mutation history;
/// it is not sorted or a serialized order guarantee. Determinism requires deterministic keys, input order and
/// volume/mapping arithmetic. A key is indexed once; duplicate Insert updates its bounds and returns false. A
/// query adds each matching stored key once, independently of keys already in the destination.
/// </remarks>
public class SwiftOctree<TKey, TVolume>
    where TKey : notnull
    where TVolume : struct, IBoundVolume<TVolume>
{
    private SubdivisionTreeCore<TKey, TVolume, OctreePartitioner<TVolume>> _core;

    /// <summary>
    /// Initializes a new instance of the <see cref="T:SwiftCollections.Query.SwiftOctree`2" /> class.
    /// </summary>
    /// <param name="worldBounds">The fixed root region in caller coordinates, copied by value. All inserted entry
    /// bounds must be fully contained, including their boundaries.</param>
    /// <param name="options">The options copied by value. default(SwiftOctreeOptions) yields depth/capacity zero
    /// and merging false; the tree does not validate it.</param>
    /// <param name="boundsPartitioner">The non-null partitioner retained by reference. It must return mutually
    /// consistent containment, child-index and subdivision results without mutating this index.</param>
    /// <exception cref="T:System.ArgumentNullException">The supplied backend is null.</exception>
    public SwiftOctree(TVolume worldBounds, SwiftOctreeOptions options, IOctreeBoundsPartitioner<TVolume> boundsPartitioner)
    {
        ChronicleGuard.ThrowIfNull(boundsPartitioner, nameof(boundsPartitioner));
        _core = new SubdivisionTreeCore<TKey, TVolume, OctreePartitioner<TVolume>>(
            worldBounds, options.MaxDepth, options.NodeCapacity, options.EnableMergeOnRemove,
            new OctreePartitioner<TVolume>(boundsPartitioner), MatchesEntryKey, IsAllocatedEntry, GetEntryKey);
    }

    /// <summary>
    /// Gets the number of active entries stored in the octree.
    /// </summary>
    /// <value>The number of indexed distinct keys.</value>
    public int Count => _core.Count;

    /// <summary>
    /// Gets the immutable world bounds covered by this octree.
    /// </summary>
    /// <value>The constructor-supplied root volume, copied by value; the tree does not automatically expand
    /// it.</value>
    public TVolume WorldBounds => _core.WorldBounds;

    /// <summary>
    /// Gets the subdivision options used by this octree.
    /// </summary>
    /// <value>The constructor-supplied options value; later changes to the caller's copy have no effect.</value>
    public SwiftOctreeOptions Options => _core.NodeCapacity == 0
        ? default
        : new SwiftOctreeOptions(_core.MaxDepth, _core.NodeCapacity, _core.EnableMergeOnRemove);

    internal int DebugNodeCount => _core.DebugNodeCount;

    internal int DebugMaxDepth => _core.DebugMaxDepth;

    internal bool DebugRootHasChildren => _core.DebugRootHasChildren;

    /// <summary>
    /// Inserts a key and copied bounds, or updates the bounds of an existing key.
    /// </summary>
    /// <param name="key">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <param name="bounds">The volume in the same caller-defined coordinate space and units as other indexed
    /// bounds; copied by value. No transform is applied.</param>
    /// <returns>true for a new key; false for an existing key whose bounds have nevertheless been
    /// updated.</returns>
    /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">bounds is not fully contained within WorldBounds;
    /// this validation precedes insertion/update.</exception>
    /// <remarks>
    /// This operation is not transactional if key hashing, volume operations, mapping, or allocation throws.
    /// Existing bounds may be relocated even though false is returned.
    /// </remarks>
    public bool Insert(TKey key, TVolume bounds) => _core.Insert(key, bounds);

    /// <summary>
    /// Removes an entry from the octree.
    /// </summary>
    /// <param name="key">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <returns>true if one matching indexed entry was removed; false if absent, with entries
    /// unchanged.</returns>
    /// <exception cref="T:System.ArgumentNullException">The key is null.</exception>
    public bool Remove(TKey key) => _core.Remove(key);

    /// <summary>
    /// Attempts to retrieve the bounds registered for the supplied key.
    /// </summary>
    /// <param name="key">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <param name="bounds">Receives a copy of the stored bounds on success; default(TVolume) when the key is
    /// absent.</param>
    /// <returns>true if the key is indexed and bounds is valid; false if absent, with bounds set to default. No
    /// entries change.</returns>
    /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
    public bool TryGetBounds(TKey key, out TVolume bounds) => _core.TryGetBounds(key, out bounds);

    /// <summary>
    /// Updates the bounds for an existing entry.
    /// </summary>
    /// <param name="key">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <param name="newBounds">Replacement bounds in the same coordinate space; copied by value. No transform is
    /// applied.</param>
    /// <returns>true if the key exists, including unchanged bounds; false if absent, without changing
    /// entries.</returns>
    /// <exception cref="T:System.ArgumentNullException">The key is null.</exception>
    /// <exception cref="T:System.ArgumentOutOfRangeException">The key exists and newBounds is outside
    /// WorldBounds.</exception>
    /// <remarks>
    /// Updates one matching key's bounds; absent keys change nothing. Bounds outside WorldBounds are rejected for
    /// an existing key; absent keys return false before that bounds validation. Callback/allocation failures can
    /// leave partial mutation.
    /// </remarks>
    public bool UpdateEntryBounds(TKey key, TVolume newBounds) => _core.UpdateEntryBounds(key, newBounds);

    /// <summary>
    /// Determines whether the octree contains the specified key.
    /// </summary>
    /// <param name="key">The non-null stable key to insert or locate; reference keys are retained, not
    /// cloned.</param>
    /// <returns>true if the key is indexed; otherwise false. No entries change.</returns>
    /// <exception cref="T:System.ArgumentNullException">key is null.</exception>
    public bool Contains(TKey key) => _core.Contains(key);

    /// <summary>
    /// Queries the octree and returns only entries whose bounds intersect the supplied query volume.
    /// </summary>
    /// <param name="queryBounds">The query region in the index coordinate space, copied by value; it is not
    /// retained or transformed.</param>
    /// <param name="results">The caller-owned mutable collection to append matching keys to. Existing entries are
    /// preserved; no deduplication against pre-existing entries is performed.</param>
    /// <exception cref="T:System.ArgumentNullException">results is null.</exception>
    /// <remarks>
    /// Appends without clearing results. Empty/no-hit queries preserve existing entries. Destination Add or
    /// volume/backend exceptions can leave partially appended output; no rollback is provided. Do not mutate the
    /// index from result callbacks. Visits current node entries before children in index order; each stored key
    /// is appended once. Boundary contact counts according to the volume backend.
    /// </remarks>
    public void Query(TVolume queryBounds, ICollection<TKey> results) => _core.Query(queryBounds, results);

    /// <summary>
    /// Removes all entries from the octree while preserving the configured world bounds.
    /// </summary>
    /// <remarks>
    /// For a nonempty tree, removes entries and retained keys while preserving reusable entry capacity,
    /// replacing the root and discarding its subdivided topology. When Count is zero this is a no-op,
    /// including any empty topology retained after removals with merging disabled.
    /// </remarks>
    public void Clear() => _core.Clear();

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MatchesEntryKey(int index, TKey key) => _core.MatchesEntryKey(index, key);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsAllocatedEntry(int index) => _core.IsAllocatedEntry(index);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private TKey GetEntryKey(int index) => _core.GetEntryKey(index);
}
