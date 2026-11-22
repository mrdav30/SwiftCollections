---
title: Spatial queries
---

# Spatial queries

The query namespace provides mutable broad-phase structures over typed bounds:

| Type                                                         | Best Fit                                                                     |
| ------------------------------------------------------------ | ---------------------------------------------------------------------------- |
| `SwiftBVH<TKey>` / `SwiftBVH<TKey, TVolume>`                 | Mixed-size objects, broad intersection queries, and heterogeneous scenes.    |
| `SwiftSpatialHash<TKey>` / `SwiftSpatialHash<TKey, TVolume>` | High-churn scenes with mostly uniform object sizes and sparse query windows. |
| `SwiftOctree<TKey>` / `SwiftOctree<TKey, TVolume>`           | Dynamic scenes with uneven density and repeated regional queries.            |

The default wrappers use `System.Numerics` `BoundVolume`. The
`SwiftCollections.FixedMathSharp` package adds fixed-point wrappers for
deterministic simulations:

- `SwiftFixedBVH<TKey>`
- `SwiftFixedSpatialHash<TKey>`
- `SwiftFixedOctree<TKey>`
- `FixedBoundVolume`

`FixedBoundVolume` normalizes its two supplied endpoints and delegates derived
metadata to `FixedBoundBox`. Centers therefore use a full-domain nearest-even
midpoint. Fixed octree subdivision compares each prospective child span in raw
unsigned space, so even the complete scalar domain can be partitioned without
materializing an unrepresentable full size. Reading `Size` or `Volume` throws
`OverflowException` when an exact positive component span cannot fit in
`Fixed64`; it never substitutes a saturated under-size.
The `Volume` product itself uses rounded, saturating `Fixed64` multiplication,
so valid component spans do not guarantee an exact representable cubic volume.

`SwiftFixedSpatialHash<TKey>.GetCellIndex(...)` exposes the hash's exact Q32.32
floor mapping for callers that must classify bounds with the same cell contract.
The quotient is then clamped into the signed `int` cell-coordinate range; cell
indices are not unbounded raw quotients. `CollectPointCandidates(...)` appends
every entry from that mapped cell into caller-owned storage without exact-bound
filtering; use `Query(...)` when exact intersection results are required.

Fixed BVH insertion does not materialize `Volume`. Its insertion cost delegates
to FixedMathSharp's exact 192-bit union-volume growth metric and clamps only the
final public `long` cost, so third and later insertions remain deterministic
across full-domain unions.

Query structures are mutable runtime indexes. Treat them as single-owner
structures unless you add synchronization externally.

## Insert bounds and query exact intersections

This example uses the FixedMathSharp companion. The volume contains normalized
minimum and maximum endpoints; the index appends matching keys into storage
owned by the caller:

```csharp
using FixedMathSharp;
using SwiftCollections;
using SwiftCollections.Query;

var index = new SwiftFixedSpatialHash<string>(capacity: 16, cellSize: new Fixed64(4));
var bounds = new FixedBoundVolume(Vector3d.Zero, new Vector3d(2, 2, 2));
index.Insert("player", bounds);

var results = new SwiftList<string>();
index.Query(bounds, results); // Contains "player".
```

Clear or reuse result buffers explicitly between independent queries.
BVH, spatial hash and octree each store one entry per non-null key. `Insert`
returns `true` for a new key; an equal key updates the existing bounds and returns
`false`, without increasing `Count` or replacing the stored key. Null insertion
throws before mutation. Use distinct keys when several entries need equal values.
None of these queries deduplicates against entries already present in the
caller's result collection.
Broad-phase volume matches do not replace your application's narrow-phase shape
tests. Updates must follow changes to an indexed object's bounds; the index does
not observe object movement automatically.

The numerics wrappers require finite ordered bounds. The built-in numerics
octree widens midpoint and child-span arithmetic to double precision before
rounding split planes to float, so subdivision supports the full finite float
endpoint range. Child creation and selection use the same split plane;
subdivision stops when either rounded child span on any axis is below
`minNodeSize`, including when no interior float midpoint exists. Span comparisons
use double precision. This does not change `BoundVolume.Center`, `Size` or
`Volume`; their separate arithmetic can still overflow. Fixed-point wrappers
retain their own arithmetic contracts.

For a BVH query with fixed storage, use `TryQuery(bounds, results, traversal)`.
Supply a plain `SwiftList<TKey>` and a separate `Span<int>` for traversal.
Allow one spare result entry and one traversal slot for each live leaf.
Insufficient capacity returns `false` before appending anything. Successful
queries preserve ordinary BVH traversal order.

Use the [query API reference](xref:SwiftCollections.Query) for duplicate-key,
update, removal, and invalid-bound contracts of the selected structure.
