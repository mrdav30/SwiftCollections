---
title: Spatial queries
---

# Spatial queries

The query namespace provides mutable broad-phase structures over typed bounds:

| Type                                                             | Best Fit                                                                     |
| ---------------------------------------------------------------- | ---------------------------------------------------------------------------- |
| `SwiftBVH<TKey>` / `SwiftBVH<TKey, TVolume>`                     | Mixed-size objects, broad intersection queries, and heterogeneous scenes.    |
| `SwiftBVH2D<TKey>`                                               | Planar intersection queries over native XY bounds, using the same BVH owner. |
| `SwiftSpatialHash<TKey>` / `SwiftSpatialHash<TKey, TVolume>`     | High-churn scenes with mostly uniform object sizes and sparse query windows. |
| `SwiftSpatialHash2D<TKey>` / `SwiftSpatialHash2D<TKey, TVolume>` | Planar high-churn scenes with native XY cell storage and traversal.          |
| `SwiftOctree<TKey>` / `SwiftOctree<TKey, TVolume>`               | Dynamic scenes with uneven density and repeated regional queries.            |

The 3D wrappers use `System.Numerics` `BoundVolume`; 2D wrappers use
`BoundVolume2D`. The `SwiftCollections.FixedMathSharp` package adds fixed-point
wrappers for deterministic simulations:

- `SwiftFixedBVH<TKey>`
- `SwiftFixedBVH2D<TKey>` / `FixedBoundVolume2D`
- `SwiftFixedSpatialHash<TKey>`
- `SwiftFixedSpatialHash2D<TKey>`
- `SwiftFixedOctree<TKey>`
- `FixedBoundVolume`

`FixedBoundVolume` normalizes its two supplied endpoints and delegates derived
metadata to `FixedBoundBox`. Centers therefore use a full-domain nearest-even
midpoint. Fixed octree subdivision compares each prospective child span in raw
unsigned space, so even the complete scalar domain can be partitioned without
materializing an unrepresentable full size. Reading `Size` or `Volume` throws
`OverflowException` when an exact positive component span cannot fit in
`Fixed64`; it never substitutes a saturated under-size. The `Volume` product
itself uses rounded, saturating `Fixed64` multiplication, so valid component
spans do not guarantee an exact representable cubic volume.

The fixed spatial hashes' `GetCellIndex(...)` methods expose exact Q32.32 floor
mapping for callers that must classify bounds with the same cell contract. The
quotient is then clamped into the signed `int` cell-coordinate range; cell
indices are not unbounded raw quotients. `CollectPointCandidates(...)` appends
every entry from that mapped cell into caller-owned storage without exact-bound
filtering; use `Query(...)` when exact intersection results are required.

Fixed BVH insertion does not materialize `Volume`. Its insertion cost delegates
to FixedMathSharp's exact 192-bit union-volume growth metric and clamps only the
final public `long` cost, so third and later insertions remain deterministic
across full-domain unions.

Query structures are mutable runtime indexes. Treat them as single-owner
structures unless you add synchronization externally.

## 2D bounds and BVH queries

Use native XY bounds for a planar world. The Numerics pair is `BoundVolume2D` /
`SwiftBVH2D<TKey>`; the deterministic fixed-point pair is `FixedBoundVolume2D` /
`SwiftFixedBVH2D<TKey>` in the companion package. Both wrappers specialize
`SwiftBVH<TKey, TVolume>` rather than owning another tree implementation. The
spatial hash also has native XY wrappers, described below. Octree wrappers use
3D bounds.

```csharp
using FixedMathSharp;
using SwiftCollections;
using SwiftCollections.Query;

var index = new SwiftFixedBVH2D<int>(capacity: 16);
var player = new FixedBoundVolume2D(new Vector2d(1, 2), new Vector2d(3, 4));
index.Insert(7, player);

var results = new SwiftList<int>(16);
index.Query(new FixedBoundVolume2D(Vector2d.Zero, new Vector2d(4, 4)), results);
// results contains 7; Query preserves any entries already in the destination.

index.UpdateEntryBounds(7, new FixedBoundVolume2D(new Vector2d(8, 2), new Vector2d(10, 4)));
index.Remove(7);
```

The readonly bounds values store endpoints only. Default is a zero-size origin
rectangle; touching edges/corners intersect. Fixed bounds normalize swapped
components through `FixedBoundArea.FromMinMax`; Numerics construction rejects
nonfinite or inverted endpoints. Use one caller-defined coordinate space and
unit system for every bound in an index.

Centers avoid endpoint-addition overflow. `Size` throws if an exact positive
span exceeds its scalar's maximum, even when adding a tiny excess to the largest
Numerics span would disappear in double precision. Numerics `Area` returns a
double; fixed `Area` delegates to checked, nearest-even Q32.32 area. Area is
independent of Size: a full-width zero-height line has area zero even when Size
throws. BVH insertion reads endpoint-based union growth rather than Size or
rounded Area. The fixed heuristic subtracts exact raw areas before flooring to
integer square units and clamping the final `long`; the Numerics heuristic uses
computed double arithmetic and can lose a small difference between large areas.

`Insert` upserts one canonical non-null key and returns true only for a new key.
The inherited `UpdateEntryBounds` returns void; an absent key is unchanged.
Query results append in topology-dependent order, and `TryQuery` uses the same
capacity and disjoint-scratch rules described below. `NodePool` is borrowed
storage that growth can replace, and node indices can be reused after mutation;
do not modify indexed nodes. Identical fixed inputs, deterministic key behavior
and mutation history determine query order. There is no cross-backend,
cross-dimension or persisted-topology order guarantee.

## 2D spatial hash queries

Use `SwiftSpatialHash2D<TKey>` when planar objects have mostly similar sizes and
you query small windows in a sparse grid. Use `SwiftFixedSpatialHash2D<TKey>`
for deterministic fixed-point mapping. These owners use two-coordinate cells and
visit X then Y, with Y changing fastest; they do not embed rectangles into a
padded 3D grid.

```csharp
using FixedMathSharp;
using SwiftCollections;
using SwiftCollections.Query;

var index = new SwiftFixedSpatialHash2D<int>(capacity: 16, cellSize: new Fixed64(4));
var player = new FixedBoundVolume2D(new Vector2d(1, 2), new Vector2d(3, 4));
index.Insert(7, player);

var results = new SwiftList<int>(16);
index.Query(player, results); // Contains 7 after testing actual rectangle intersection.

results.Clear();
index.CollectPointCandidates(new Vector2d(0, 0), results);
// Contains 7 because the player occupies cell (0, 0); the point is outside the player.

index.UpdateEntryBounds(7, new FixedBoundVolume2D(new Vector2d(8, 2), new Vector2d(10, 4)));
index.Remove(7);
```

`Query` tests actual intersections and adds each matching indexed key once.
`QueryNeighborhood` instead returns all candidates in the mapped rectangle plus
the configured whole-cell padding, without intersection filtering.
`CollectPointCandidates` returns the occupants of one mapped cell. All append to
caller-owned results, leave existing entries intact and do not deduplicate
against the existing prefix. Clear the destination between independent queries.

Constructors without options use `SwiftSpatialHashOptions.Default`, whose
padding is one. Explicit `default(SwiftSpatialHashOptions)` uses zero padding.
Both mapped endpoints are inclusive, so touching cell boundaries can register a
rectangle in more than one cell. Negative fractional coordinates floor toward
negative infinity. Fixed mapping divides raw coordinates exactly before flooring
and clamping to signed Int32 cells. Numerics mapping widens the quotient to
double, then floors and clamps; it does not promise cross-platform lockstep
determinism. Neighborhood expansion clips to Int32 limits. A broad range still
visits every mapped cell and can require impractical work.

`Insert` retains one canonical key and returns false after updating an equal
key's bounds. `UpdateEntryBounds` returns true for an existing key, including
unchanged bounds, and false for an absent key. `TryGetBounds` returns a copied
volume on success and writes default on failure. Bounds are copied; use one
coordinate space and unit system throughout the index. Cell size must be
positive, and the Numerics cell size must also be finite. `Clear` removes
registrations and retained keys while preserving reusable entry capacity; later
inserts can allocate cell lists. `EnsureCapacity` reserves entry slots, not
every future cell list or result buffer.

For custom volumes, implement `IBoundVolume<TVolume>` and
`ISpatialHashCellMapper2D<TVolume>`, then construct
`SwiftSpatialHash2D<TKey, TVolume>`. The retained mapper must return stable,
ordered inclusive ranges that cover every possible intersection. Derived owners
can expose the protected `CollectCellCandidates` hook for an already mapped
`SwiftSpatialHashCellIndex2D`. Keep key, volume and mapper callbacks pure and
avoid reentry. Mutations are not transactional when callbacks or allocation
throw; destination failures can leave partially appended output.

Query order follows cells and mutation history rather than sorting keys.
Identical fixed inputs, deterministic keys and the same mutation history
determine results within this family. There is no cross-dimension, cross-backend
or serialized-order guarantee. Synchronize all access externally, including
queries that mutate duplicate-suppression stamps. Query owners have no
persistence contract.

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

Clear or reuse result buffers explicitly between independent queries. BVH,
spatial hash and octree each store one entry per non-null key. `Insert` returns
`true` for a new key; an equal key updates the existing bounds and returns
`false`, without increasing `Count` or replacing the stored key. Null insertion
throws before mutation. Use distinct keys when several entries need equal
values. None of these queries deduplicates against entries already present in
the caller's result collection. Broad-phase volume matches do not replace your
application's narrow-phase shape tests. Updates must follow changes to an
indexed object's bounds; the index does not observe object movement
automatically.

The numerics wrappers require finite ordered bounds. The built-in numerics
octree widens midpoint and child-span arithmetic to double precision before
rounding split planes to float, so subdivision supports the full finite float
endpoint range. Child creation and selection use the same split plane;
subdivision stops when either rounded child span on any axis is below
`minNodeSize`, including when no interior float midpoint exists. Span
comparisons use double precision. This does not change `BoundVolume.Center`,
`Size` or `Volume`; their separate arithmetic can still overflow. Fixed-point
wrappers retain their own arithmetic contracts.

For a BVH query with fixed storage, use `TryQuery(bounds, results, traversal)`.
Supply a plain `SwiftList<TKey>` and a separate `Span<int>` for traversal. Allow
one spare result entry and one traversal slot for each live leaf. Insufficient
capacity returns `false` before appending anything. Successful queries preserve
ordinary BVH traversal order.

Use the [query API reference](xref:SwiftCollections.Query) for duplicate-key,
update, removal, and invalid-bound contracts of the selected structure.
