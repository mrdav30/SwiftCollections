# Query Collection API Specification

## Status

- Phase: `Phase 0 - Contracts And Scope Lock`
- Applies to: `SwiftBVH`, `SwiftSpatialHash`, and `SwiftOctree`
- Intent: lock the public query-collection shape before production hardening work lands

## Scope Decisions

- `SwiftBVH` remains the baseline query collection already shipped in this package.
- `SwiftSpatialHash` is the first additional query collection to be promoted into the compiled package.
- `SwiftOctree` is the second additional query collection to be promoted into the compiled package.
- All new query collections follow the same two-layer pattern already used by BVH:
  - typed core for backend-specific volume implementations
  - numerics convenience wrapper in this repo for `System.Numerics` consumers
- Fixed-math convenience wrappers do **not** ship in this package.
  - This repo keeps backend-agnostic typed cores.
  - Fixed-math wrappers belong in the companion package, mirroring the current fixed-BVH posture.

## Shared Contract Decision

The shared contract for production query collections is **volume-based**, not point-based.

- `SwiftBVH<TKey, TVolume>` continues to use `IBoundVolume<TVolume>`.
- `SwiftSpatialHash<TKey, TVolume>` and `SwiftOctree<TKey, TVolume>` will also be volume-based collections.
- Collection-specific configuration and traversal policies stay collection-specific.
- Position-only contracts from the current prototypes (`IPosition`, `IOctreeItem`) are not part of the final public API.

This keeps all three query structures aligned around the same data model:

- every entry is identified by `TKey`
- every entry owns a `TVolume`
- updates replace the registered volume for a key
- queries are expressed in terms of query volumes or collection-specific query configurations derived from volumes

## Volume Contract

`IBoundVolume<TVolume>` remains the shared public volume contract for query collections in this package.

```csharp
public interface IBoundVolume<TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    TVolume Union(TVolume other);
    bool Intersects(TVolume other);
    int GetCost(TVolume other);
    bool BoundsEquals(TVolume other);
}
```

The contract is intentionally small:

- `Union` and `GetCost` are required by BVH balancing.
- `Intersects` is required by all shipped query collections.
- `BoundsEquals` is required for update semantics that avoid metadata-cache false negatives.

No point-specific members are added to `IBoundVolume<TVolume>` in Phase 0. SpatialHash and Octree internals may use backend-specific helpers or companion abstractions later without widening the core public contract now.

## Public API Surface

### SwiftBVH

Existing public surface is preserved.

```csharp
public class SwiftBVH<TKey, TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    public SwiftBVH(int capacity);

    public SwiftBVHNode<TKey, TVolume>[] NodePool { get; }
    public SwiftBVHNode<TKey, TVolume> RootNode { get; }
    public int RootNodeIndex { get; }
    public int Count { get; }

    public bool Insert(TKey value, TVolume bounds);
    public bool Remove(TKey value);
    public void UpdateEntryBounds(TKey value, TVolume newBounds);
    public void Query(TVolume queryBounds, ICollection<TKey> results);
    public int FindEntry(TKey value);
    public void EnsureCapacity(int capacity);
    public void Clear();
}

public class SwiftBVH<TKey> : SwiftBVH<TKey, BoundVolume>
{
    public SwiftBVH(int capacity);
}
```

### SwiftSpatialHash

`SwiftSpatialHash` becomes a keyed, mutable, volume-based query collection.

```csharp
public class SwiftSpatialHash<TKey, TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    public SwiftSpatialHash(int capacity, ISpatialHashCellMapper<TVolume> cellMapper);
    public SwiftSpatialHash(int capacity, ISpatialHashCellMapper<TVolume> cellMapper, SwiftSpatialHashOptions options);

    public int Count { get; }
    public SwiftSpatialHashOptions Options { get; }

    public bool Insert(TKey key, TVolume bounds);
    public bool Remove(TKey key);
    public bool TryGetBounds(TKey key, out TVolume bounds);
    public bool UpdateEntryBounds(TKey key, TVolume newBounds);
    public bool Contains(TKey key);

    public void Query(TVolume queryBounds, ICollection<TKey> results);
    public void QueryNeighborhood(TVolume queryBounds, ICollection<TKey> results);

    public void EnsureCapacity(int capacity);
    public void Clear();
}

public class SwiftSpatialHash<TKey> : SwiftSpatialHash<TKey, BoundVolume>
{
    public SwiftSpatialHash(int capacity, float cellSize);
    public SwiftSpatialHash(int capacity, float cellSize, SwiftSpatialHashOptions options);
}
```

Supporting mapper and options types:

```csharp
public interface ISpatialHashCellMapper<TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    void GetCellRange(TVolume bounds, out SwiftSpatialHashCellIndex minCell, out SwiftSpatialHashCellIndex maxCell);
}

public readonly struct SwiftSpatialHashCellIndex
{
    public SwiftSpatialHashCellIndex(int x, int y, int z);
    public int X { get; }
    public int Y { get; }
    public int Z { get; }
}

public readonly struct SwiftSpatialHashOptions
{
    public SwiftSpatialHashOptions(int neighborhoodPadding);
    public int NeighborhoodPadding { get; }
}
```

Notes:

- The typed core accepts a backend-owned cell mapper so the core package never hard-codes `float`, `Fixed64`, or any other numeric flavor into its internal logic.
- The `Query` method is the precise bounded query.
- `QueryNeighborhood` is the cell-neighborhood query surface used for broad local lookups.
- Duplicate suppression is part of the collection contract, not an optional caller responsibility.
- The exact numeric representation used by backend-specific wrappers is implementation-defined at the wrapper edge.

### SwiftOctree

`SwiftOctree` becomes a keyed, mutable, volume-based hierarchical query collection.

```csharp
public class SwiftOctree<TKey, TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    public SwiftOctree(TVolume worldBounds, SwiftOctreeOptions options, IOctreeBoundsPartitioner<TVolume> boundsPartitioner);

    public int Count { get; }
    public TVolume WorldBounds { get; }
    public SwiftOctreeOptions Options { get; }

    public bool Insert(TKey key, TVolume bounds);
    public bool Remove(TKey key);
    public bool TryGetBounds(TKey key, out TVolume bounds);
    public bool UpdateEntryBounds(TKey key, TVolume newBounds);
    public bool Contains(TKey key);

    public void Query(TVolume queryBounds, ICollection<TKey> results);
    public void Clear();
}

public sealed class SwiftOctree<TKey> : SwiftOctree<TKey, BoundVolume>
{
    public SwiftOctree(BoundVolume worldBounds, SwiftOctreeOptions options, float minNodeSize);
}
```

Supporting options type:

```csharp
public interface IOctreeBoundsPartitioner<TVolume>
    where TVolume : struct, IBoundVolume<TVolume>
{
    bool ContainsBounds(TVolume outer, TVolume inner);
    bool CanSubdivide(TVolume bounds);
    bool TryGetContainingChildIndex(TVolume nodeBounds, TVolume entryBounds, out int childIndex);
    TVolume CreateChildBounds(TVolume parentBounds, int childIndex);
}

public readonly struct SwiftOctreeOptions
{
    public SwiftOctreeOptions(int maxDepth, int nodeCapacity);
    public SwiftOctreeOptions(int maxDepth, int nodeCapacity, bool enableMergeOnRemove);

    public int MaxDepth { get; }
    public int NodeCapacity { get; }
    public bool EnableMergeOnRemove { get; }
}
```

Notes:

- World bounds are explicit and immutable after construction.
- The typed core accepts a backend-owned octree bounds partitioner so the core package never hard-codes `float`, `Fixed64`, or any other numeric flavor into its subdivision logic.
- Numeric subdivision thresholds such as minimum node size are backend-owned and live in wrapper/package adapters rather than the shared octree options type.
- Split and merge policy is defined by `SwiftOctreeOptions`, not hard-coded constants.
- Radius or point convenience overloads can be added later in backend-specific wrapper packages, but they are not required for the core package contract.

## Key Semantics

- Keys are unique logical identities for collection membership.
- `Insert` on an existing key is **replace-by-key** semantics.
  - The stored bounds for that key are replaced with the new bounds.
  - `Count` does not increase when the key already exists.
- `Remove` returns `false` when the key is absent.
- `Contains` and `TryGetBounds` reflect the currently registered key state.
- Query result ordering is unspecified.
- Query results never include duplicate keys for a single query, even when an internal representation spans multiple cells or nodes.

## Update Semantics

- `UpdateEntryBounds` returns `false` when the key is absent for SpatialHash and Octree.
- `SwiftBVH` keeps its current void-returning update method for compatibility.
- Updating to bounds that are `BoundsEquals` to the currently registered bounds is a no-op.
- Updating a key may trigger internal relocation, reinsertion, split, merge, or bucket remapping.
- Updates are eager. No lazy or deferred writeback is part of the initial contract.

## Thread-Safety Expectations

- `SwiftBVH` keeps its current coarse-grained read/write lock behavior.
  - concurrent readers are allowed
  - writes are mutually exclusive
  - read/write concurrency guarantees are best-effort and internal invariants remain the primary contract
- `SwiftSpatialHash` and `SwiftOctree` will ship as **not thread-safe by default** in their initial hardened versions.
- If opt-in thread-safe variants are added later, they will be introduced as explicit API additions rather than implied by the base types.

This avoids over-promising concurrency semantics before the hardened implementations exist.

## Diagnostics Expectations

- Query collections may emit diagnostics through `SwiftCollectionDiagnostics.Shared`.
- Diagnostics are informational and must not be required for normal operation.
- Invalid internal state that indicates collection corruption may still throw.

## Non-Goals Locked In Phase 0

- No fixed-math wrapper types in this package.
- No promise of stable internal node, cell, or bucket identifiers for SpatialHash or Octree.
- No promise that query result ordering matches insertion ordering.
- No hidden cross-package coupling to `Fixed64` or `Vector3d`.

## Acceptance Summary

Phase 0 is considered complete when this specification remains the source of truth for:

- public type names
- generic constraints
- insert/remove/update/query semantics
- thread-safety expectations
- companion-package boundary for fixed math
