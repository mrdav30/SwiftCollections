# 2D Query Parity Plan

- **Status:** Phases 1–3 complete for implemented scope and local
  Standard/Lean/target gates. Unity and distribution closeout remain Phase 4.
  Hosted/released provider readiness and historical Debug-baseline resolution
  remain separate records.
- **Created:** 2026-11-21.
- **Owner:** SwiftCollections, with FixedMathSharp owning any new exact geometry
  arithmetic.
- **Tracking:**
  [Feature work overview](feature-work-overview.md#2d-query-parity).
- **Baseline:** SwiftCollections `cf4fdb3369afd555e73ee3eab84c4092c78d3647`,
  initially clean. Record actual revisions and local changes at each
  implementation phase; this planning baseline is not a release or benchmark
  result.

## Outcome and scope

Provide native 2D BVH, spatial-hash and quadtree collections with both
System.Numerics and FixedMathSharp bounds. Callers should be able to index,
move, remove and query keyed 2D bounds using the same ownership and
result-buffer contracts as the corresponding 3D family.

Reuse the dimension-independent owners. Keep XY geometry, cell traversal and
four-child subdivision at explicit dimensional boundaries. Protect the existing
3D API, storage contracts, behavior and measured costs while extracting shared
logic. Fixed-point backends are the authoritative lockstep path; Numerics
backends do not promise cross-platform floating-point determinism.

Implementation covers `SwiftCollections`, `SwiftCollections.Lean`,
`SwiftCollections.FixedMathSharp` and `SwiftCollections.FixedMathSharp.Lean`,
each targeting `netstandard2.1` and `net10.0`. Unity remains a separate
adapter/smoke-test host. This feature adds axis-aligned bounds backends and
matching custom-backend extension points; new ray, nearest-neighbor, sweep,
shape-contact and query-index persistence APIs are outside its scope. Existing
state/serializer formats stay unchanged.

## Frozen API and parity inventory

Names below are the additive surface frozen in Phase 0. Keep current 3D type
names, constructors, generic constraints, inheritance relationships and
public/protected member signatures intact.

| Family                 | Frozen surface                                                                                            | Reuse boundary                                                                                                       |
| ---------------------- | --------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------- |
| Bounds                 | `BoundVolume2D` over Vector2; `FixedBoundVolume2D` over Vector2d and FixedBoundArea                       | Implement existing `IBoundVolume<TVolume>`; introduce no second bounds interface or geometry owner.                  |
| BVH                    | `SwiftBVH2D<TKey>`, `SwiftFixedBVH2D<TKey>`                                                               | Thin wrappers over existing `SwiftBVH<TKey, TVolume>`; reuse its node type, storage, key map and traversal scratch.  |
| Spatial hash           | `SwiftSpatialHash2D<TKey, TVolume>`, Numerics `SwiftSpatialHash2D<TKey>`, `SwiftFixedSpatialHash2D<TKey>` | Native XY keys/ranges, shared entry/key/stamp/lifecycle mechanics with the existing XYZ owner.                       |
| Spatial-hash extension | `SwiftSpatialHashCellIndex2D`, `ISpatialHashCellMapper2D<TVolume>`                                        | Additive XY contracts; preserve existing XYZ cell type, mapper and protected hooks. Reuse `SwiftSpatialHashOptions`. |
| Quadtree               | `SwiftQuadtree<TKey, TVolume>`, Numerics `SwiftQuadtree<TKey>`, `SwiftFixedQuadtree<TKey>`                | Shared internal subdivision owner with four-child and eight-child boundaries; retain existing octree wrappers.       |
| Quadtree extension     | `IQuadtreeBoundsPartitioner<TVolume>`, `SwiftQuadtreeOptions`                                             | Four-child numbering and existing depth/capacity/merge semantics. Avoid a universal public options framework.        |

Parity means equivalent caller contracts within each corresponding family, not
identical signatures across different families:

- **BVH:** constructor/capacity, Count, Insert, void UpdateEntryBounds, Remove,
  Clear, FindEntry, EnsureCapacity, Query and TryQuery; retain NodePool,
  RootNode and RootNodeIndex through the generic engine. Document borrowed-array
  replacement, node-index reuse and the existing node inspection/mutation
  limits.
- **Spatial hash:** capacity/options constructors, Count, Options, Insert,
  Boolean UpdateEntryBounds, Remove, Contains, TryGetBounds, EnsureCapacity,
  Clear, exact Query and QueryNeighborhood. Fixed wrapper parity includes
  GetCellIndex(Vector2d) and CollectPointCandidates(Vector2d, results), with a
  corresponding protected XY candidate hook for derived/custom backends.
- **Quadtree:** world/options/partitioner constructors, Count, WorldBounds,
  Options, Insert, Boolean UpdateEntryBounds, Remove, Contains, TryGetBounds,
  Query and Clear. Preserve world containment checks before duplicate
  relocation.
- **All:** one entry per non-null key; Insert returns true only for a new key,
  and false after updating an equal key's bounds. Retain canonical stored keys.
  Reference keys must keep stable equality/hashing while indexed. Cross-run
  determinism also requires pure, deterministic key equality/hash and
  bounds/mapper/partitioner callbacks, consistent inputs, and no callback
  reentry. XY cell hashing must reuse SwiftHashTools' deterministic integer
  primitives.
- **Buffers:** ordinary queries append into caller-owned collections without
  clearing or deduplicating against pre-existing destination entries. Preserve
  exact hits versus broader candidates. BVH TryQuery retains its
  plain-SwiftList, disjoint scratch, worst-case capacity preflight and
  failure-without-write rules. Do not add unrelated bounded-buffer overloads
  merely for cross-family symmetry.
- **Failures/lifetimes:** retain each family's applicable exceptions, mutation
  timing, partial-output behavior, capacity/clear reuse and external
  synchronization requirements. No new transactional rollback or
  reentrant-mutation guarantee.

## Current owners and concrete reuse work

The [generic BVH](../../src/SwiftCollections/Query/BoundingVolume/SwiftBVH.cs)
already depends only on
[IBoundVolume](../../src/SwiftCollections/Query/Shared/Volume/IBoundVolume.cs).
The 2D area-growth backend supplies the dimensional heuristic; no second binary
tree, node implementation or scratch subsystem is needed.

The
[shared spatial hash core](../../src/SwiftCollections/Query/SpatialHash/SpatialHashCore.cs)
owns reusable entry arrays, free-slot reuse, upsert/relocation, key lookup,
capacity, clear and query-stamp rollover/duplicate suppression. The existing
[XYZ facade](../../src/SwiftCollections/Query/SpatialHash/SwiftSpatialHash.cs)
and new
[XY facade](../../src/SwiftCollections/Query/SpatialHash/SwiftSpatialHash2D.cs)
embed that mutable core. They retain their dimensional mapper and protected
candidate contracts. Constrained struct grid adapters map ranges and step native
XY or XYZ cell coordinates without per-cell delegates or iterator allocation.
Stable facade-bound key-map callbacks observe the current core, rather than a
copied struct. A Z=0 mapper alone would retain a third coordinate and visit
extra padded Z layers; the XY owner uses two-coordinate keys.

The [octree](../../src/SwiftCollections/Query/Octree/SwiftOctree.cs) explicitly
allocates eight children, while most traversal, redistribution and merge loops
already use Children.Length. Share these mechanics with four-child subdivision.
Preserve the existing
[octree partitioner](../../src/SwiftCollections/Query/Octree/IOctreeBoundsPartitioner.cs)
and its 0..7 contract. A quadtree must not publicly inherit from an octree just
to reuse code. Phase 0 selected an embedded owner layout below; production
extraction remains a measured gate for indirection, storage and warm costs.

Reuse
[QueryKeyIndexMap](../../src/SwiftCollections/Query/Shared/QueryKeyIndexMap.cs)
and applicable
[QueryTraversalScratch](../../src/SwiftCollections/Query/Shared/QueryTraversalScratch.cs).
New shared ownership/extraction helpers must serve the existing 3D and new 2D
owners in the same phase. Dimension-specific numeric helpers need an actual
production consumer in their owning family. Do not rely on static-abstract
interface dispatch or other newer-runtime features for netstandard2.1/Unity AOT
compatibility; avoid reflection, per-operation boxing and delegate iteration.
Keep a ledger of added/removed runtime lines and files, new owner objects,
delegates, bytes per entry/cell/node, and duplicated mutation algorithms
removed. Do not split code solely to lower complexity metrics.

## Numeric contracts and upstream ownership

### Fixed-point bounds and BVH cost

Use [FixedMathSharp](https://github.com/mrdav30/FixedMathSharp)'s
`Geometry/Bounds/FixedBoundArea.cs` as the geometry owner. It already supplies
normalized endpoints, inclusive intersection, union, containment and a
full-domain nearest-even midpoint. Full Size can be unrepresentable;
indexing/subdivision must operate from endpoints rather than materializing it.

The new provider APIs are checked `FixedBoundArea.Area` and
`FixedBoundArea.GetAreaExpansionCost(FixedBoundArea other)`, with the latter
using the same public long-cost convention as the existing 3D volume-expansion
metric: form union area minus the current rectangle's area exactly, then round
down to square coordinate units and clamp only the final result to
long.MaxValue. Zero/sub-unit growth can yield zero; preserve deterministic BVH
tie-breaking.

Area returns Fixed64 square-coordinate units using checked nearest-even Q32.32
rounding, including overflow from rounding carry. Compute it independently of
Size: a full-width, zero-height rectangle has zero area even when Size throws.
Both APIs must share one provider-private exact raw-area operation. Mark the new
derived Area getter `[JsonIgnore]` and `[MemoryPackIgnore]` to preserve native
FixedBoundArea and BoundingAreaState payloads. SwiftCollections delegates to
these APIs rather than owning another fixed-point area implementation.

Full signed Q32.32 endpoints require unsigned raw spans and exact products up to
128 bits. Subtract raw areas before discarding 64 fractional bits. Put this
implementation and its exact tests in FixedMathSharp, reusing its existing wide
arithmetic. Do not copy wide geometry into SwiftCollections, multiply
potentially unrepresentable Fixed64 sizes, or require runtime
BigInteger/System.UInt128. BigInteger is suitable as an independent test oracle.

| Readiness boundary observed during planning                                                                      | State                                                                                                                          |
| ---------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------ |
| Local FixedMathSharp `b501340558c6132484be2f40957233c1b3d29cab`                                                  | Clean; FixedBoundArea basics available; Area and area-expansion cost absent.                                                   |
| Current SwiftCollections hosted-develop FixedMathSharp pin `3da3a8ad9964d3e0413080737fec12ba107b48e4`            | New area API readiness not established; it has not been added by this plan.                                                    |
| Cached FixedMathSharp and FixedMathSharp.Lean 8.0.0, package revision `763efc57cea399cde375fe0ef4aa296c1eaa3cba` | Both TFMs expose bounds basics and public 3D volume cost; neither new area member is available. No new API compile is claimed. |

Record provider and consumer revisions and test each boundary separately:
coordinated source first, then an authorized immutable/fetchable hosted pin
after provider land, then released Standard/Lean packages before release
acceptance. Use `UseLocalLsfStack=true` for local development of the new call.
Local success does not close hosted or released-package gates. Publishing,
pushing, dependency version changes and workflow-pin updates are separate
actions, not authorized by this documentation-only plan.

### Mapping and subdivision

- Uplift the fixed spatial hash's existing private raw ToCell operation into a
  companion-owned helper with both XY and XYZ production consumers. Preserve the
  integer-cell-size fast path, floor toward negative infinity and Int32
  clamping. Point mapping and bounds-range mapping must call the same operation.
- Freeze inclusive cell ranges and boundary touches. Neighborhood padding is a
  whole-cell XY expansion, with zero/default behavior matching the current
  options contract; Default padding one differs from default(options) zero. Use
  overflow-safe loop counters and padding clipping near Int32 limits.
  Full-domain bounds support is not a feasible full-domain cell-iteration
  promise.
- Numerics policy: constructors enforce finite ordered endpoints, with a default
  zero-size origin box and finite positive cell/minimum-node sizes. Do not
  impose this new validation or normalization on existing 3D callers.
- New Numerics XY mapping uses widened quotient, floor and Int32 clamping,
  documented as computed floating-point mapping. Preserve existing 3D mapping
  behavior during extraction; any incompatible correction requires separate
  disposition.
- Compute Numerics area differences from endpoints widened before subtraction
  and multiplication; finite float spans/products fit double. Floor/clamp the
  public cost explicitly. This is not an arbitrary-precision or cross-platform
  lockstep arithmetic guarantee.
- Quadtree midpoint admission, construction and selection must agree. Numerics
  uses the SC-Issue-020 widened-then-float midpoint and checks both resulting
  child spans in double precision. Fixed uses FixedBoundArea.Center and exact
  unsigned raw spans on both sides of X and Y. Refuse endpoint-equal or
  undersized splits; retain straddlers at the parent.
- Freeze quadrant index xBit | (yBit << 1), upper-half preference for a point
  exactly on the split plane, and child traversal order. Require deterministic
  result sequences for one backend, input order and mutation history; compare
  hit sets across dimensional/numeric backends without requiring equal
  sequences.

## Implementation phases and exit gates

| Phase                         | Work                                                                                                                                                                                                                                                                              | Exit evidence                                                                                                                                                                                                                                                                                                        |
| ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 0 — Contracts and baselines   | Freeze the complete member inventory, names, numeric defaults/validation, custom-backend contracts and measured reuse mechanism candidates. Record source/package graphs and existing 3D output/order, allocation, time and storage baselines. Scope the FixedMathSharp addition. | Decision record below completed; a bounded layout/prototype comparison supports the intended common owners. No performance or dependency gate inferred from source inspection.                                                                                                                                       |
| 1 — Bounds and BVH            | Implement/test checked area and exact provider cost in FixedMathSharp, then both 2D bounds backends and thin wrappers over the existing generic BVH. Use insert → query → move → query → remove → reuse through public APIs as the first working slice.                           | Correct hits and fixed traces, edge/full-domain area/cost oracles, NodePool/TryQuery parity, warm allocation and practical time; both affected repos pass applicable coverage/variant checks. Independent first-slice review before expansion. Hosted/package dependencies remain separately pending if unavailable. |
| 2 — Native XY spatial hash    | Introduce XY cell/mapper APIs and concrete loops while sharing ownership/stamps/relocation with XYZ. Uplift fixed mapping with both consumers. Include fixed point-candidate helpers.                                                                                             | Complete mutation/exact/neighborhood/candidate contracts, rollover and range-edge tests; native two-coordinate storage; measured 3D compatibility and allocation/time/footprint gates pass.                                                                                                                          |
| 3 — Quadtree and shared tree  | Add four-child backends and public wrappers around shared internal ownership/subdivision/merge logic. Keep existing 3D extension points and order.                                                                                                                                | Four actual children, contained/shrinking bounds, straddler and merge/depth tests, default-options controls, fixed full-domain tests and measured 3D compatibility gates pass.                                                                                                                                       |
| 4 — Distribution and closeout | Complete public docs/inventory, package inspection, supported runtime/Unity checks and any authorized consumer smoke. Reconcile all decisions, measurements and dependency gates here.                                                                                            | Every matrix lane and exclusion reported; strict coverage and required hosted/package/Unity gates pass. No release claim without separate release work. Archive by repository convention after completion.                                                                                                           |

Run independent ponytail review after the first shared working slice, after any
substantial extraction, and before each phase handoff. Fix verified findings.
Never retain an old copied engine or fallback alongside a replacement to make
checks appear green.

## Correctness, determinism and performance evidence

Use meaningful production-path regressions before any discovered runtime fix.
For new APIs, assert behavior directly; tests must not merely reproduce the
implementation. Keep disposable design/profiling probes outside maintained
tests.

The maintained behavior matrix must cover:

- All families and both backends: empty/default, key zero, null keys, equal-key
  upsert/canonical retention, collisions, move across regions, remove twice,
  Clear/reuse, capacity growth, append-to-nonempty buffers and no-hit queries.
- Whole hit sets against brute-force inclusive AABB intersection; points,
  touching edges/corners, zero area, coincident and huge/tiny bounds. Compare
  fixed cost against exact BigInteger oracles before narrowing/clamping,
  including tiny increments between large areas and deterministic ties.
- Fixed full scalar endpoints, odd raw spans and minimum child halves; Numerics
  same-sign endpoint-sum overflow, full signed range, adjacent floats,
  subnormals, midpoint straddlers and validated/rejected malformed inputs per
  Phase 0.
- Spatial hash: negative/fractional boundary flooring, inclusive endpoint
  ranges, clipping/saturation, padding zero/default/large, duplicate stamps and
  rollover, exact versus candidate queries, and storage reclamation on
  move/remove.
- Trees: all quadrants/octants, multi-level routing, parent-held straddlers,
  maximum depth, minimum size, merge enabled/disabled and default options. BVH
  additionally covers borrowed node storage and TryQuery preflight/alias rules.
- Failure after destination Add, backend callbacks or mutation: applicable
  exceptions, out-value validity and partial state/output must match docs.
- For the same fixed backend, inputs and mutation history, require identical hit
  sets and ordered query sequences across Standard/Lean, net10.0, netstandard2.1
  consumers and Unity Mono/IL2CPP. Different numeric backends or dimensions
  compare hit sets without requiring identical sequences. Do not promise
  persisted topology or cross-backend order.

Benchmark existing 3D and new 2D workloads using the existing benchmark project.
Start with representative small and larger owners; provisionally use 128, 2,048
and 8,192 entries with uniform, clustered, mixed-size, sparse, coincident and
moving bounds. Freeze actual inputs, densities, cell sizes, query windows,
update fractions and reference outputs in Phase 0. Compare equivalent 2D work
against brute force and an existing 3D embedding as controls, accounting for
degenerate-volume heuristics and extra storage rather than claiming equivalence.

Measure construction/cold setup, warm insert/update/remove/query, growth/clear,
retained churn storage, and owner/entry/cell/node footprint separately. Native
2D should demonstrate the expected XY/four-child storage savings. Preserve the
agreed 3D warm allocation budget, outputs and sequences; do not hide an extra
shared-core owner, per-operation boxing, delegate or virtual-dispatch cost
behind a zero-allocation query result. No per-entry dimension tags or per-cell
temporary objects. No speculative caching or arbitrary-dimensional public
framework.

Use balanced baseline/current timings with verified loaded binaries and complete
BenchmarkDotNet child/sample results. Inconclusive measurements remain
inconclusive. A confirmed added allocation, storage or time regression needs
redesign or explicit user disposition before the phase closes; do not defer an
introduced cost into the benchmark backlog to claim completion. Independent
owner/control measurements should keep the investigation bounded.

## Validation and distribution matrix

Implementation gates below remain requirements. The Phase 0 record reports the
baseline lanes actually run; it does not close implementation or release gates.
Preserve existing CI policy.

| Lane                                    | Required evidence                                                                                                                                                                                                                                                                                                                                                                                                                           |
| --------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Release and ReleaseLean, net10.0        | Full solution build/tests; fresh assembly-specific coverage for core and companion, with 100% reported lines, branches and executable methods using the unchanged strict gate.                                                                                                                                                                                                                                                              |
| Debug, net10.0                          | Build/tests and strict per-assembly coverage for refactor/guard/null/default differences.                                                                                                                                                                                                                                                                                                                                                   |
| Release and ReleaseLean, netstandard2.1 | Both assemblies compile with C# 11 and the matching dependency graph; inspect emitted XML and packaged target assets. Build and execute a representative consumer using those actual DLLs on a supported host. netstandard is a contract target, not a runtime.                                                                                                                                                                             |
| Four package IDs × two TFMs             | Explicit pack; inspect all eight DLL/XML pairs, dependencies and expected new API. Standard/Lean must agree on query contracts; Lean must not gain MemoryPack runtime/generator dependencies. Inspect any new bounds-value serialization attributes deliberately; add no query-owner persistence contract.                                                                                                                                  |
| DocFX                                   | Fresh generated metadata/site from both Release/net10.0 DLL/XML inputs, warnings as errors; reconcile full authored member inventory and inspect bounds, generic/wrapper, options, mapper/partitioner and inherited comments/links. Check remaining targets/variants in emitted XML.                                                                                                                                                        |
| Hosted CI                               | Existing Windows/Ubuntu Release/Lean lanes, with correct package/source dependency mode and authorized fetchable upstream pins. Report unavailable lanes as pending.                                                                                                                                                                                                                                                                        |
| Unity/netstandard consumer              | Use SwiftCollections-Unity's existing isolated smoke-project route with matching Standard/Lean core, companion, FixedMathSharp and Chronicler graphs. Exercise public 2D bounds/BVH/hash/quadtree/custom-backend controls in actual Editor/Mono and High-stripping IL2CPP players. Verify backend, loaded inputs and result markers; a .NET replay is not a substitute. Do not replace the adapter checkout's checked-in packages in place. |

Use [CONTRIBUTING](../../CONTRIBUTING.md#local-validation) commands and each
test project's coverlet.runsettings. In coordinated source mode carry
`UseLocalLsfStack=true` and `DisableTransitiveProjectReferences=true` globally
through restore/build/test and build serially with `-m:1`; explicitly restore
again when changing configuration/dependency mode. Package mode uses released
dependencies and `DisableTransitiveProjectReferences=false`. Run
[.assets/scripts/assert-full-coverage.ps1](../../.assets/scripts/assert-full-coverage.ps1)
against fresh results containing both test projects. Do not add coverage
exclusions, waive guards through the complexity register or weaken existing CI.

Update the spatial guide, API map/README when the feature exists, applicable
XML, tests/benchmarks and directly relevant contributor guidance. Keep
source/header, namespace-level type-per-file and generated-output rules. No
Unity engine API enters either runtime assembly. UI conversions or engine
adapters beyond smoke coverage require separate adapter scope.

## Decision register and phase results

| Decision                                   | Current disposition                                                                                                                                                                                   | Reopening/closure criterion                                                                                                                                             |
| ------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Shared ownership                           | Reuse BVH unchanged. Select embedded mutable cores for hash/tree; retain stable wrapper callback receivers and existing public extension points. Reject an extra heap core: measured +24 B per owner. | Reopen if production XY/four-child extraction changes existing identities, storage, order or warm costs. Production latency acceptance remains required.                |
| Fixed 2D arithmetic                        | Implemented in coordinated source: checked nearest-even Area and exact expansion share private raw-area arithmetic. Hosted/released APIs remain pending.                                              | Implement/test provider, then independently close source, hosted-pin and released-package gates. Reopen only for a demonstrated correctness or caller-contract problem. |
| Names, validation and topology             | Additive inventory and boundary behavior frozen below; endpoint-only bounds, native XY keys/loops and four-child topology.                                                                            | Reopen on implementation evidence that the chosen contract cannot preserve correctness, compatibility or required costs; record any consequential departure.            |
| Persistence and additional query operators | Excluded from this parity pass; existing owner/state formats unchanged.                                                                                                                               | Reopen only for a separately requested caller contract with concrete consumers.                                                                                         |

Maintain concise phase results in this file: revisions/dirty state, chosen
contracts, net runtime growth and deletions, test/coverage counts, baseline
versus introduced failures, workload/units and measured uncertainty,
assemblies/TFMs/ packages/XML checked, consumer/Unity evidence, dependency
readiness and remaining gaps. Preserve portable inputs, commands, outcomes and
reopening criteria here; completion must not depend on disposable captures or
separate phase reports.

### Phase 0 — contracts and baseline evidence

Completed; baseline builds/tests captured 2026-11-21. SwiftCollections revision
`2db3b152d461e7abb3bf614b07b096a1ad38bf04` was clean, with an empty index.
FixedMathSharp remained clean at the local revision in the readiness table. This
phase changes this plan and the feature overview only: **zero runtime
lines/files, new runtime objects, production delegates or dependency changes**.
Disposable source-derived prototypes were independently reviewed before the
layout decision. No duplicate engine or probe becomes maintained source.

#### Complete additive member contract

All new types use namespace `SwiftCollections.Query`. Owners constrain
`TKey : notnull`; generic owners and backend interfaces constrain
`TVolume : struct, IBoundVolume<TVolume>`. Add no IEquatable requirement to
custom volumes and change no existing 3D constraints.

- **Bounds:** readonly `BoundVolume2D(Vector2 min, Vector2 max)` and
  `FixedBoundVolume2D(Vector2d min, Vector2d max)`, each implementing
  IBoundVolume and IEquatable of itself. Both expose Min, Max, Center, Size,
  Area, Union, Intersects, GetCost, BoundsEquals, typed/object Equals, ==, !=,
  GetHashCode and diagnostic ToString. Retain endpoints only, without cached
  metadata or a validity flag. Target payloads are 16 B Numerics and 32 B fixed;
  only the Numerics prototype and native FixedBoundArea sizes were measured, not
  a completed fixed wrapper. Default is a valid origin point; touching
  edges/corners intersect. Methods copy input values and do not mutate them.
- **Numerics bounds:** validate finite min, finite max, then componentwise min
  <= max. Nonfinite endpoints throw ArgumentOutOfRangeException naming min/max;
  inversion throws ArgumentException naming max. Do not normalize. Center widens
  before summing/halving and narrows to Vector2. Size widens differences and
  throws OverflowException if a positive span exceeds float.MaxValue. Area is
  double square-coordinate units from widened spans, independent of Size. Cost
  floors computed union-area increase, clamps to [0, long.MaxValue], and
  preserves zero-cost ties. This is computed floating arithmetic, not an
  exact-real or lockstep guarantee.
- **Fixed bounds:** normalize via FixedBoundArea.FromMinMax; delegate Center,
  Size, Area and area-growth cost to that owner. Size/Area retain their checked
  overflow contracts. Do not derive cost from rounded Area or Size. Use
  deterministic endpoint hashing; equal values must hash equally, including
  equivalent zero representations. Hashes are not persistent identities.
- **BVH wrappers:** `SwiftBVH2D<TKey>(int capacity)` and
  `SwiftFixedBVH2D<TKey>(int capacity)` specialize the existing generic owner.
  Inherit the entire BVH inventory above, including EnsureCapacity and generic
  SwiftBVHNode inspection. Retain capacity normalization, borrowed-array
  invalidation, physical node-index reuse, absent-update behavior and TryQuery
  preflight/alias/plain-SwiftList contracts. Add no parallel node type.
- **Hash constructors:** generic
  `(int capacity, ISpatialHashCellMapper2D<TVolume> cellMapper)` and the same
  with final SwiftSpatialHashOptions options; Numerics
  `(int capacity, float cellSize)` and fixed `(int capacity, Fixed64 cellSize)`,
  each with the corresponding three-argument options overload. All expose the
  hash inventory above. Omitted options use Default padding one;
  default(options) remains padding zero. Preserve entry-capacity normalization
  and reuse semantics.
- **XY extension surface:** readonly SwiftSpatialHashCellIndex2D(int x, int y),
  X/Y, IEquatable, typed/object equality, ==, !=, deterministic
  SwiftHashTools.CombineHashCodes(X, Y), and diagnostic ToString.
  ISpatialHashCellMapper2D.GetCellRange(TVolume bounds, out
  SwiftSpatialHashCellIndex2D minCell, out SwiftSpatialHashCellIndex2D maxCell)
  supplies inclusive, ordered ranges. The generic owner retains the mapper
  reference and exposes protected void CollectCellCandidates(
  SwiftSpatialHashCellIndex2D cell, ICollection<TKey> results). The fixed
  wrapper adds GetCellIndex(Vector2d point) and void CollectPointCandidates(
  Vector2d point, ICollection<TKey> results). Candidate methods append cell
  occupants without exact intersection filtering. XY traversal increases X then
  Y, with Y fastest. No public dimension parameter or cell-budget policy.
- **Quadtree constructors:** generic
  `(TVolume worldBounds, SwiftQuadtreeOptions options, IQuadtreeBoundsPartitioner<TVolume> boundsPartitioner)`;
  Numerics
  `(BoundVolume2D worldBounds, SwiftQuadtreeOptions options, float minNodeSize)`;
  fixed
  `(FixedBoundVolume2D worldBounds, SwiftQuadtreeOptions options, Fixed64 minNodeSize)`.
  Expose the quadtree inventory above.
- **Quadtree extension surface:** IQuadtreeBoundsPartitioner has bool
  ContainsBounds(TVolume outer, TVolume inner), bool CanSubdivide(TVolume
  bounds), bool TryGetContainingChildIndex(TVolume nodeBounds, TVolume
  entryBounds, out int childIndex), and TVolume CreateChildBounds(TVolume
  parentBounds, int childIndex). Indices are 0..3; built-ins set the false
  out-index to -1, and callers use a custom out-index only on true. Pure,
  mutually consistent callbacks must keep their behavior stable while indexed;
  the owner retains the backend reference for its lifetime. SwiftQuadtreeOptions
  has `(int maxDepth, int nodeCapacity)` and
  `(int maxDepth, int nodeCapacity, bool enableMergeOnRemove)`; MaxDepth,
  NodeCapacity, EnableMergeOnRemove; IEquatable, typed/object equality, ==, !=
  and deterministic integer-component GetHashCode. Omitted merge is true.
  Explicit negative depth/nonpositive capacity throw
  ArgumentOutOfRangeException. Default struct is accepted, zero/zero/false, and
  unsplit. No additional Default property or ToString is required.
- **Validation and failure:** null keys/backends/results throw
  ArgumentNullException before their applicable operation. Cell/minimum-node
  sizes must be positive and Numerics-finite; otherwise throw
  ArgumentOutOfRangeException naming the size. Quadtree Insert checks world
  containment before duplicate lookup; outside bounds throw
  ArgumentOutOfRangeException naming bounds. Update of an absent key returns
  false before replacement-world validation; a present outside-world update
  throws ArgumentOutOfRangeException naming newBounds before relocation.
  TryGetBounds false writes default; queries may extend outside WorldBounds.
  Retain canonical stored keys and the existing family-specific callback/Add
  failure mutation and partial-output contracts. No rollback promise is added.
- **Topology/lifetime:** xBit | (yBit << 1), upper-half preference for exact
  split-plane points, straddlers at the parent, parent entries then children in
  increasing index. Admit a split only when both actual halves on both axes meet
  the minimum and the midpoint is strictly internal. Wrappers copy
  options/bounds, retain backend references, and require externally synchronized
  ownership. No new serializer/state APIs are included. XML and inherited
  rendering for every member remain implementation acceptance work.

#### Reuse experiment and selected layout

Compare existing generic owners with (a) an embedded mutable core struct and (b)
the same core inside one extra heap object. Derive both candidates from the
revision's actual hash/octree algorithms, key map and integer stack; change only
namespace/type binding, wrapper forwarding, stable callback receivers, and
constrained instance four/eight-child selection. Preserve the existing
mapper/partitioner interface reference and pre-create one identical backend for
all compared owners. No static-abstract dispatch is required.

Select the embedded direction. The wrapper must own a mutable, non-readonly core
field; bind key-map callbacks to stable wrapper methods that consult that field.
Capturing a core instance method group or returning a writable core by value
risks boxing/copying stale entry storage. Production extraction must remove the
superseded mutation engine and serve 2D and 3D in the same phase.

| Windows x64 measurement            | Existing | Embedded | Extra heap core |
| ---------------------------------- | -------: | -------: | --------------: |
| Hash construction, capacity 16     |  2,504 B |  2,504 B |         2,528 B |
| Tree construction, node capacity 8 |  1,392 B |  1,392 B |         1,416 B |
| Numerics hash/tree entry value     |     80 B |     80 B |            80 B |
| Fixed tree entry value             |     64 B |     64 B |            64 B |

Constructor allocation averages 128 creations after 32 warmups, excluding shared
pre-created backend allocation. These are managed allocation budgets, not
retained graph sizes. Existing BVH construction at capacity 16 averaged 2,523 B,
including amortized ThreadLocal setup. Raw sizes: existing Numerics bounds 64 B,
fixed 3D bounds 48 B, prototype XY bounds 16 B, native FixedBoundArea 32 B; XYZ
cell 12 B versus XY cell 8 B. Generic BVH nodes were 104/88/48 B for Numerics
3D/fixed 3D/prototype XY; prototype XY tree entries were 32 B. These demonstrate
storage direction, not finished 2D node budgets.

The hash candidate deliberately retains XYZ loops/keys: this proves ownership
layout and callbacks, not native XY range performance. The tree candidate uses
the same traversal/mutation engine for four and eight actual children. Fixed
tree controls use the existing fixed partitioner. FixedBoundArea defensive
copies in a readonly wrapper remain a Phase 1 measurement gate.

#### Portable workloads and observed behavior

Windows 11 build 26300.10075, Intel i7-9700K x64, SDK 10.0.302, .NET 10.0.10.
Run sequentially with DOTNET_TieredCompilation=0 and blocking GC for the layout
probe. Prebind the same method delegates for every candidate outside timed
loops; no direct-call versus delegate-call comparison.

Use N = 128, 2,048, 8,192, and profiles in this order: uniform, clustered,
mixed, sparse, coincident, moving. For each profile reset xorshift32 to 0xC0FFEE
(xor shifts left 13, right 17, left 5). Draw each XYZ coordinate modulo 1024;
clustered takes each modulo 32, sparse multiplies by 64, coincident uses
(16,16,16). Size is one, except every eighth mixed entry uses eight. Keys are
insertion indices. Moving starts with uniform geometry. Replacement bounds
translate by (1,0,1). World is [-16,131104] on every axis, cell size 8, tree
options (8,8,false), minimum size 1. For q=0..15, use initial[(97*q+13)%N].Min
as anchor and query [anchor-2,anchor+4].

For each family: insert; verify each query's complete hit set against brute
force; move the first N/4; verify; remove N-1 twice and reinsert; verify;
clear/reinsert and verify. Compare ordered results against the existing owner at
the same history, not across families. Across all four Standard/Lean × TFM
graphs, 54 initial/moved trace pairs and ten added lifecycle traces matched.
FNV-1a-style trace starts at 14695981039346656037; for each query fold its
count, then each key as uint, by xor then unchecked multiplication
by 1099511628211. The N=2,048 moving initial/moved traces were both
`D521AC3F973E33C5` for all three families (16 total hits per batch).

Additional discriminating checks:

- Hash capacity 4 → 257 keys i*1024, using the clustered scene above at N=257
  and creating one key-map collision cluster; verify Count/Contains/TryGetBounds
  including an absent default out-value. Remove every third key, verify before
  reuse, reinsert moved bounds, force query-stamp rollover, verify empty clear,
  then reverse reinsert. Query destination prefixes survive. Expected decimal
  traces in that stage order: 14907020542346869103, 17918045562206898519,
  15993923500476605704, 15993923500476605704,
  9808874869469701221, 3895179500948589832.
- Fixed tree existing/embedded/heap: world [-16,128], options (6,4,true),
  minimum size 1; 65 unit entries at (i%8*8,i/8*8,i%4*8), key i*1024. Four
  queries start at (q*8,0,0) and extend by (24,96,96). Verify growth, move key
  zero to [60,61]^3, remove/reuse it, clear/reverse insert. Traces:
  3657918031866667180, 2449310433242302389,
  2449310433242302389, 4029054762149554101. Full hit-set and ordered comparisons
  pass.
- Four-child proof: world [0,16]^2, options (4,1,true), minimum size 1, unit
  boxes at (1,1), (12,1), (1,12), (12,12). Verify exactly four root children and
  each quadrant, move key zero to the fourth box, remove twice, inspect
  count/query before clearing, then clear/reuse.

Warm exact queries allocated zero bytes in every tested family/layout/profile:
eight warm and eight measured batches of 16 queries, pre-sized results. At
N=2,048 moving, existing BVH/hash/tree construction allocated
689,432/787,360/399,728 B including probe delegate bindings and insertion; these
are cold-workload allocations, not retained heap sizes. Bounded probe coverage
is not production edge-case, retained-churn or mutation-allocation acceptance.

Balanced query-only ABCCBA comparisons repeat four rounds, eight warm and 64
measured 16-query batches per sample, moving the first 512 keys before each
sample. Eight samples/layout: Standard median hash existing/embedded/heap
0.897/0.898/0.905 microseconds per batch; tree 19.661/20.161/17.056. Lean
medians were hash 1.059/1.103/1.095 and tree 16.590/15.996/16.498. Sample ranges
reached 0.889–5.116 microseconds for hash and 14.409–47.656 for tree across
these runs. This sparse-hit timing evidence is inconclusive: no
latency-equivalence or accepted-regression claim.

Existing BenchmarkDotNet DynamicNeighbor smoke baselines (milliseconds per
invocation, managed bytes in parentheses):

| Owner        |  Standard N=2,048 |      Lean N=2,048 |   Standard N=8,192 |       Lean N=8,192 |
| ------------ | ----------------: | ----------------: | -----------------: | -----------------: |
| BVH          |  6.578 (16,544 B) |  6.519 (16,544 B) |  14.573 (65,696 B) |  19.892 (65,696 B) |
| Spatial hash | 11.749 (79,200 B) | 11.502 (79,200 B) | 12.661 (327,552 B) | 14.469 (327,552 B) |
| Octree       |       3.043 (0 B) |       3.080 (0 B) |      6.132 (336 B) |      5.993 (336 B) |

Reproduce using the unchanged QueryCollectionComparison benchmarks at the
recorded revision:
`dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c <Release|ReleaseLean> -f net10.0 --no-build -- query --filter '*DynamicNeighbor_Swift*' --job Short --warmupCount 1 --iterationCount 3 --launchCount 1`.
The workload performs N/4 moves and 32 passes of 64 queries per invocation, with
iteration setup outside timing. Each configuration had six successful child
exits and 18 measured results; all 12 children/36 results completed. Existing
single-invocation setup produced MinIterationTime warnings and wide confidence
intervals. These are practical-scale baseline smoke results, not
isolated-operation budgets or evidence of an introduced cost.

#### Build, target and package baseline checks

Use released package mode: Chronicler.Core/Core.Lean and Diagnostics 1.0.0,
FixedMathSharp/Lean 8.0.0, MemoryPack.Core 1.21.4 in Standard and the existing
shim in Lean. For both configurations run:

```powershell
dotnet build SwiftCollections.slnx -c <configuration> -p:UseLocalLsfStack=false -p:DisableTransitiveProjectReferences=false
dotnet test tests/<test-project>/<test-project>.csproj -c <configuration> --no-build -p:UseLocalLsfStack=false -p:DisableTransitiveProjectReferences=false --collect:"XPlat Code Coverage" --settings tests/<test-project>/coverlet.runsettings --results-directory <fresh-results>
./.assets/scripts/assert-full-coverage.ps1 -CoverageDirectory <fresh-results>
```

Run both test projects into fresh per-configuration results before the gate. All
tests passed, with zero failures/skips; raw coverage passed unchanged:

| Configuration / assembly | Tests |       Lines |    Branches | Executable methods |
| ------------------------ | ----: | ----------: | ----------: | -----------------: |
| Release core             | 2,040 | 6,022/6,022 | 2,858/2,858 |        1,264/1,264 |
| Release companion        |    46 |     121/121 |       62/62 |              35/35 |
| ReleaseLean core         | 1,932 | 5,828/5,828 | 2,782/2,782 |        1,223/1,223 |
| ReleaseLean companion    |    46 |     121/121 |       62/62 |              35/35 |

Both runtime assemblies built for net10.0 and netstandard2.1 in both
configurations. Inspected the four fresh build-generated 0.0.0 NuGet packages:
all eight `lib/<TFM>/SwiftCollections[.FixedMathSharp].dll` and adjacent XML
pairs matched the build output; XML parsed with 139 core/30 companion query
member entries per target/variant. Standard dependency groups retained
MemoryPack; Lean retained its shim without MemoryPack runtime/generator
packages. This is baseline package inspection, not a new-API inventory check or
release pack acceptance.

The same source-derived prototypes compiled separately for netstandard2.1 with
C# 11, zero warnings/errors, and ran through a .NET 10 consumer using the actual
netstandard2.1 runtime/dependency DLLs. Both flavors passed all controls and
matched net10.0 traces. A first disposable consumer mixed a net10.0
FixedMathSharp DLL with netstandard MemoryPack and failed with
MissingMethodException for RegisterFormatter; using the matching netstandard2.1
FixedMathSharp asset resolved it. This was probe dependency assembly, not a
reproduced source defect. Future consumer runs must select the entire matching
graph, not just the two SwiftCollections DLLs.

Verified loaded core/companion SHA-256 pairs:

| Configuration / target       | SwiftCollections                                                   | SwiftCollections.FixedMathSharp                                    |
| ---------------------------- | ------------------------------------------------------------------ | ------------------------------------------------------------------ |
| Release / net10.0            | `92c6531863d4e265a76f7bf614e666276e9fb17a680947e836f3b0a9d6ecb655` | `b2085ecc8add3d92e642a9cadc7031fde75f3254331ec5f55be7b5d18ac2dad7` |
| ReleaseLean / net10.0        | `2d3f64b91a118b4db0d82607c264e9085ce579648657a7a75a6f17d2309075d3` | `a2efabff289239dec1ee5a865bd175af747efead0752590db221b68e2dcbf38c` |
| Release / netstandard2.1     | `17ce2a30a501df3147fe31401963bade6b19274a120cfb96e68efb80e021d7ae` | `8d57904fab81951041a24091278c7893253f0ed4cc4468f8196475e8bde4ccbc` |
| ReleaseLean / netstandard2.1 | `18ce03af760303ddeda1a559a578d4218df7f6e3de290cd47cc6ca3376a4a650` | `df649750551f58c29a983947ea1eedf1fabca14a92570f217f90d50146a0570b` |

Baseline build warnings: xUnit2013 in SwiftSlotMap tests; Lean additionally
xUnit1026 for the unused binary theory parameter in ObservableDictionary tests.
No baseline test/coverage failure, introduced source failure or confirmed
runtime defect was found. Independent review's initial probe gaps (growth,
collision repair, fixed candidate and observable removal) were corrected and
re-reviewed before accepting this evidence. No bug or benchmark item is needed.

Documentation checks: Prettier 3.9.5, all 16 local Markdown links/fragments, and
git diff --check passed. The index stayed unchanged. These feature-work files
are outside DocFX's published content inputs; no site rebuild was needed for
their edits.

**Remaining gates:** Phase 1 must implement and validate both provider APIs,
readonly-wrapper cost, real 2D bounds and BVH; Phases 2/3 must prove production
native XY/four-child extraction, all edge contracts, retained churn storage,
warm mutation costs and latency. Debug, hosted CI, actual Unity Mono/High
IL2CPP, new-API DocFX rendering/inventory and release dependency readiness were
not run or claimed in Phase 0. A netstandard DLL replay on .NET 10 proves no
Unity/AOT result. No source-mode provider call exists yet; source, hosted-pin
and package gates remain independently pending. No measured production
regression has been accepted or moved into a backlog.

### Phase 1 — bounds and BVH

Started 2026-11-22 from clean SwiftCollections
`34ebf95d05af72985d84f10bef7f1de1efbaa774`, FixedMathSharp
`b501340558c6132484be2f40957233c1b3d29cab` and Chronicler
`0281469ecc40e65a05867f473bc10f030e7f69f8`. Results include the uncommitted
patches in the two owning repositories; Chronicler is unchanged.

#### Implementation and first-slice review

- Added the two readonly bounds values and two thin BVH wrappers. Existing
  generic BVH, 3D bounds, key map, traversal and ownership code are unchanged.
  Four new runtime files total 314 lines including XML/headers; no extra owner
  object, delegate, dimension tag, cached metadata or parallel tree engine.
- FixedMathSharp adds 68 runtime lines in FixedBoundArea: checked Area and exact
  area-expansion cost share one private raw-area operation using existing
  Multiply64To128. No new arithmetic type, runtime file, dependency or fallback.
- Public lifecycle tests cover canonical upsert, growth, borrowed nodes,
  TryQuery preflight/alias/subclass rules, full-domain bounds, moving entries
  and complete brute-force hit sets. Independent first-slice review passed.
- Review found new Numerics Size admission could lose a tiny overflow in double
  subtraction. Epsilon/unit regressions failed before correction, then passed.
  Compare the smaller magnitude against maxFloat minus the larger; that
  subtraction is exact in the only possible overflow region. An independent
  IEEE-bit BigInteger oracle checks 256 boundary/sign pairs and 512 seeded
  finite pairs, both axes. Existing 3D metadata remains unchanged.
- Provider rounding/carry, full-domain cost and cancellation tests use
  BigInteger, including 512 seeded cases; companion cost tests add 256 cases. No
  coverage exclusion, guard waiver or complexity exception was added.

#### Build and coverage results

Build both solutions serially with
`-p:UseLocalLsfStack=true -p:DisableTransitiveProjectReferences=true -m:1`,
carrying both properties through restore/build/test. Run each existing test
project with its own coverlet.runsettings, fresh results and the owning
assert-full-coverage.ps1. FixedMathSharp.Chronicler.Tests runs separately,
outside its coverage gate.

| Actual runtime target / configuration | Core tests | Companion tests | Core lines / branches / methods | Companion lines / branches / methods |
| ------------------------------------- | ---------: | --------------: | ------------------------------- | ------------------------------------ |
| net10.0 / Release                     |      2,060 |              60 | 6,055 / 2,888 / 1,281           | 141 / 64 / 53                        |
| net10.0 / ReleaseLean                 |      1,952 |              60 | 5,861 / 2,812 / 1,240           | 141 / 64 / 53                        |
| netstandard2.1 / Release              |      2,059 |              60 | 6,055 / 2,888 / 1,281           | 141 / 64 / 53                        |
| netstandard2.1 / ReleaseLean          |      1,951 |              60 | 5,861 / 2,812 / 1,240           | 141 / 64 / 53                        |
| net10.0 / Debug                       |      2,058 |              60 | 7,991 / 2,896 / 1,281           | 176 / 64 / 53                        |

All listed SwiftCollections tests pass with zero skips; every coverage count is
covered/total, exactly 100%. Both runtime libraries compile with C# 11 for
netstandard2.1 and the configured language version for net10.0.

| Provider target / configuration | Core tests | Lines / branches | Methods: core / FluentAssertions |
| ------------------------------- | ---------: | ---------------- | -------------------------------- |
| net10.0 / Release               |      7,460 | 68,877 / 22,888  | 5,298 / 63                       |
| net10.0 / ReleaseLean           |      7,438 | 68,970 / 22,888  | 5,294 / 63                       |
| netstandard2.1 / Release        |      7,460 | 68,880 / 22,896  | 5,295 / 63                       |
| netstandard2.1 / ReleaseLean    |      7,438 | 68,973 / 22,896  | 5,291 / 63                       |

All provider rows pass with 100% raw lines, branches and executable methods.
Line/branch totals combine the two assemblies in the existing gate. Chronicler
companion tests pass 49/49 in Release, ReleaseLean and Debug.

Actual netstandard DLLs are exercised by disposable copies of the existing test
projects using original test sources, matching DLL/PDB references, original
assembly names and Coverlet source-root mappings. They execute on .NET 10;
netstandard is not a runtime. Align test-side framework defines and MemoryPack
generator 1.21.4 with the netstandard dependency API: set
DisableImplicitFrameworkDefines=true and define NETSTANDARD2_1 plus
NETSTANDARD2_1_OR_GREATER. Legacy bridges bind the existing public static
serializer and TryFormat methods where static-abstract dispatch and
ISpanFormattable are unavailable. The existing NET10-only TicketMap JSON test is
inapplicable to that compilation; no runtime member or coverage point is
excluded. In the copied tests, reflection-bound delegates call the concrete
public static Serialize/Deserialize and instance TryFormat methods; they replace
unavailable interface dispatch without changing the tested wire bytes or format
results. No maintained harness, project or test-policy change.

#### Baseline Debug failures

Full FixedMathSharp Debug passes 7,458 tests and fails two. Its raw gate also
fails: 80,017/80,028 lines and 23,114/23,160 branches. Uncovered points lie in
unchanged geometry/wide files; all three new provider methods have 100% Debug
line/branch coverage. This is not a green Debug gate; no threshold was relaxed.

Both test failures reproduce individually in a pristine package-mode export of
b501340: warmed cylinder-polytope queries allocate 576 B, and deliberately
incomplete hull topology reaches Debug.Fail before returning false. The provider
issue tracker records FMS-Issue-045 as a test/precondition conflict, without a
proven valid-closed-hull contact defect. Its benchmark backlog owns the
unattributed Debug allocation signal. Both retain inputs, portable commands,
revision and next action. The full pristine Debug coverage matrix was not
replayed; gap classification relies on unchanged affected source and separate
baseline test controls. Keep that lane open for scoped follow-up. Release/Lean
gates and new-feature Debug checks pass.

#### Behavior, storage and performance

The consumer uses N=128,2,048,8,192, xorshift32 seed 0xC0FFEE and two XY draws
modulo 1024 per entry. Size is one, except every eighth entry uses eight.
Sixteen query anchors use (97*q+13)%N and [anchor-2,anchor+4]. Move the first
N/4 by (1,1), remove/reinsert the last key, run eight bounded remove/reinsert
passes over the first N/4, then clear/reverse insert. Check full hit sets and
ordered per-backend traces at each stage, destination prefixes and unchanged
node capacity during bounded churn. All four target/variant graphs matched all
45 trace rows. The N=2,048 fixed initial trace is `793827A200840CEC`. Use the
Phase 0 FNV fold, query count then ordered keys, with row order N,
numerics/fixed/mutable-control, and initial/moved/reused/churned/clear-reverse.
SHA-256 of those `TRACE,backend,N,stage,HEX` rows joined with LF and a final LF:
`6bd7fad2e91f5be0a13bb4c37960b5250ed0c880782a173334064b7c79ad96e9`.

Actual bounds sizes are 16/32 B Numerics/fixed; int-key BVH nodes are 48/72 B
versus existing 3D 104/88 B. Standard target runs report zero warmed allocation
for Query, TryQuery and updates with pre-sized buffers. All four graphs pass
those allocation and lifecycle checks. Warm update delegates and entry points
before measurement.

Readonly fixed-wrapper controls use the same native arithmetic in a disposable
mutable value over the same BVH. ABBA repeats four rounds, eight samples/layout:
16 warm and 256 measured batches of sixteen queries; builds pre-size 2*N nodes.
Standard/net10.0 N=2,048 median query times are 14.943/15.293 us for wrapper/
control, with build medians 712.8/696.0 us. netstandard medians are
14.960/15.356 and 794.4/809.15 us. No consistent wrapper penalty was
demonstrated. Large-owner build allocations match (328,336 B at 2,048; 1,311,376
B at 8,192); small cold samples include shared ThreadLocal slot-growth noise.
This is not a retained-heap or universal latency-equivalence claim. No extra
cache/copy workaround was justified.

The maintained Query2DBVH benchmark uses actual APIs and the existing runner.
All controls clear and write the same caller-owned results. It uses unit boxes,
the same seed/XY range and 32 anchored queries; embedding has Z thickness one.
Standard CLI smoke reproduction after building with source flags (the final
kept-binary results use the configured job described below):

```powershell
$env:UseLocalLsfStack='true'
$env:DisableTransitiveProjectReferences='true'
dotnet tests/SwiftCollections.Benchmarks/bin/Release/net10.0/SwiftCollections.Benchmarks.dll all --filter '*Query2DBVHBenchmarks*' --job Short --warmupCount 2 --iterationCount 5 --launchCount 1
```

| Configuration / N |  Native 2D | 3D embedding | Brute force |
| ----------------- | ---------: | -----------: | ----------: |
| Standard / 128    |   3.098 us |     3.914 us |    6.622 us |
| Standard / 2,048  |  16.282 us |    23.771 us |  233.573 us |
| Standard / 8,192  | 106.400 us |   127.134 us |  932.733 us |
| Lean / 128        |   3.014 us |     3.993 us |    4.819 us |
| Lean / 2,048      |  15.966 us |    23.611 us |  232.408 us |
| Lean / 8,192      | 104.999 us |   127.497 us |  942.138 us |

Every method reports zero managed bytes. Both final runs completed nine child
processes with exit zero and 45 actual measured iterations; BDN's documented
outlier handling retained 44 Standard and 43 Lean results. Preserve that
qualification. An earlier Standard CLI run also completed (45 actual/40
retained) and gave 2.963/16.013/107.268 us for native 2D; it supports the
direction without replacing the final kept-binary run above. An unsupported CLI
configuration argument produced help only and was excluded, despite the runner's
zero exit.

Use Job.ShortRun.WithCustomBuildConfiguration(configuration), two warmups, five
iterations, one launch and ConfigOptions.KeepBenchmarkFiles when selecting
ReleaseLean explicitly; the CLI job otherwise defaults to Release. A disposable
launcher reused the maintained benchmark type and checked every Summary.Report
and ExecuteResult, without changing the maintained launcher or CI. Inspect the
launch paths/deps before accepting variant results. Both kept children used
source identities, and Lean had its shim with no MemoryPack.Core dependency.

Child core SHA-256: Standard
`0f9e0cf814caeef347aed2a25d86af6539e0bdc2997d6c48aee2f8dd92d0959c`; Lean
`cce4ef6c6318e619b8c6575d43372e7355c72b3438b6a086e483e808ba6f751c`. Whole-child
DLL hashes differ from ordinary outputs under BDN isolated build paths; no
whole-assembly byte-identity claim is made. Method-name-plus-IL hashes for BVH,
its node and the two new Numerics types match their ordinary outputs: Standard
`f6db4e273b7bcef926555835595e3a451e97e788bb806e03186a75456cfd3a50`; Lean
`26c077ff82bac9c118d6cc6d3efb33e6551cb97b96ed21b09f4069c6ec1c1034`. These are
bounded workload results, not universal throughput, cross-flavor speed ratios or
frame-latency claims. No performance tradeoff was accepted.

#### Documentation, wire compatibility and dependencies

Fresh isolated copies of the authored DocFX layout build with original
assembly/XML inputs and templates, warnings as errors: both sites pass with zero
warnings. Copying the layout preserves TOC paths and starts with empty
metadata/site output while preserving existing generated files. Rendered HTML
review covers all four new types, spatial guide and both provider members:
parameters, return text, property values, inherited specializations, exceptions,
links and fragments resolve. No browser visual-layout claim.

All eight SwiftCollections DLL/XML pairs in the four source-mode package IDs
match build output; each assembly adds 19 authored XML entries with applicable
tags. Property accessors/backing fields are synthesized; the values have no
authored parameterless constructor, partial extension or nested public type. All
four provider target/variant pairs also match, including both new members. Lean
retains the shim without MemoryPack runtime/generator packages. These are
source-validation packages, not release candidates. Inherited members retain
existing identities; no query serializer API was added.

Area's ignore attributes preserve native state payloads. A released-8.0.0
control and both actual Standard targets emit identical State-only JSON and
32-byte full-domain MemoryPack endpoints:
`00000000000000800000000000000080ffffffffffffff7fffffffffffffff7f`. JSON stores
raw Min=(-9223372036854775808,-9223372036854775808) and
Max=(9223372036854775807,9223372036854775807), without reading overflowing Area.
Roundtrip restores the bounds and checked Area still throws.

Local provider-to-consumer readiness is verified with these patches. Hosted pin
and released 8.0.0 packages still lack both members and remain separate pending
gates. Land the provider before selecting an authorized immutable hosted pin;
validate released Standard/Lean dependencies before release. No version/pin/
workflow change, publishing or legacy fallback. Actual Unity Mono/High IL2CPP
and distribution closeout remain Phase 4 requirements.

Final checks: targeted C# formatter verification passes in both repositories
(the SC design-time workspace emits a load warning; actual builds/tests pass).
Prettier 3.9.5 and git diff --check pass; 71 changed-document local links and
fragments resolve. Generated site path checks pass for 108 SC and 69 provider
pages (1,393 and 5,472 local links); new-page fragments were independently
reviewed. Canonical rebuilds preserve all twelve runtime DLL hashes used by the
four target/variant consumer graphs. No staged/index, commit, CI-policy or
Chronicler worktree change.

### Phase 2 — native XY spatial hash

Complete for the implemented local scope and gates below. The implementation
starts from SwiftCollections `4ca068867405b1c1eb76c409eba2043deae71a50` and
FixedMathSharp `0a81be7b59920d367692eff35e19d4dda1bb2b4b`, with initially clean
worktrees and empty indexes. Phase 2 changes SwiftCollections only; the provider
APIs from Phase 1 remain the coordinated-source dependency. Hosted pins and
released packages retain their separate readiness gates.

The generic and Numerics `SwiftSpatialHash2D` facades, fixed
`SwiftFixedSpatialHash2D`, XY cell index and mapper implement the frozen
inventory above. One embedded `SpatialHashCore` now serves both dimensional
families. `SpatialHashGrid2D` and `SpatialHashGrid3D` provide ordinary
constrained instance dispatch for mapping and coordinate stepping, compatible
with C# 11 and netstandard2.1. No extra heap core or per-cell delegate is
introduced. The core stores neighborhood padding as an integer and reconstructs
the public options value; this keeps its embedded field layout compact. The
executed owner-byte and warm-latency controls below establish acceptance for the
tested workloads, without a universal speed or startup claim.

Runtime growth is ten new files and two changed owners: a net 738 authored
lines, including XML, or 316 nonblank/noncomment lines. The superseded XYZ
engine is removed; one engine serves both dimensions. Both grids and the fixed
mapping helper have production consumers in this phase. No extra heap core,
per-entry dimension field, per-cell temporary or public dimensional framework.

The companion's existing raw fixed-cell mapping is uplifted into
`FixedSpatialHashCellMapping`, consumed by both XY and XYZ bounds/point mapping.
It retains the integral-cell-size fast path, signed floor and Int32 clamping.
Numerics XY uses widened division and clamping while existing XYZ Numerics
mapping remains unchanged. Shared mutation code keeps the existing
nontransactional failure timing, canonical keys, reusable capacity and query
stamp semantics; no new serializer or state contract is added.

Focused tests cover native cell value semantics, defaults, exact versus
neighborhood/point candidates, append prefixes, boundary order, negative and
clamped mapping, invalid inputs, callback failures, stamp rollover, collisions,
growth, relocation and reuse. Fixed tests compare raw mapping against a
BigInteger oracle and the existing XYZ mapping. The maintained
`Query2DSpatialHashBenchmarks` compares public XY queries with a 3D embedding
and brute-force materialization at 128, 2,048 and 8,192 entries.

The first shared slice and final source received independent read-only
`ponytail-review` across both facades, grid adapters, mapping helper and tests.
No confirmed correctness, public API, failure-contract or test-oracle finding
remains. The final borrowed-span cell loop was reviewed separately: callbacks
cannot mutate the index, so its private list storage remains stable. Stamp,
intersection and destination-write ordering are preserved.

#### Builds, tests and strict coverage

Only SwiftCollections source changes in this phase. Serial coordinated-source
builds use
`dotnet build SwiftCollections.slnx -c <configuration> -p:UseLocalLsfStack=true -p:DisableTransitiveProjectReferences=true -m:1`.
Both test projects use their existing runsettings and fresh coverage
directories; `assert-full-coverage.ps1` passes every lane, including executable
methods. No exclusions, weakened gate, complexity-register waiver or maintained
probe infrastructure was added.

| Runtime DLLs exercised              | Core tests | Companion tests | Coverage in both assemblies                 |
| ----------------------------------- | ---------: | --------------: | ------------------------------------------- |
| Release / net10.0                   |      2,082 |              74 | 100% lines, branches and executable methods |
| ReleaseLean / net10.0               |      1,974 |              74 | 100% lines, branches and executable methods |
| Debug / net10.0                     |      2,080 |              74 | 100% lines, branches and executable methods |
| Release / actual netstandard2.1     |      2,081 |              74 | 100% lines, branches and executable methods |
| ReleaseLean / actual netstandard2.1 |      1,973 |              74 | 100% lines, branches and executable methods |

Core covered/valid counts are 6,160/6,160 lines, 2,910/2,910 branches and
1,333/1,333 executable methods in Release; Lean is 5,966/5,966, 2,834/2,834 and
1,292/1,292. Debug is 8,131/8,131, 2,918/2,918 and 1,333/1,333. Companion counts
are 162/162 lines, 64/64 branches and 60/60 methods in both release
variants/targets; Debug has 201/201 lines and the same branch/method counts.

Both libraries compile for both targets in each release variant, including C# 11
for netstandard2.1. Disposable copies of the existing test projects run against
the actual netstandard DLL/PDB graph on .NET 10, with legacy target conditional
symbols and matching MemoryPack assets. They retain the Phase 1 bridge for
generated MemoryPack calls and source-root mappings for coverage; no production
test is replaced. The one net10-only JSON test is inapplicable to the older
target. This verifies netstandard library execution on a supported host, not a
nonexistent netstandard runtime or Unity/AOT.

Pre-existing analyzer warnings remain: xUnit2013 at SwiftSlotMap.Tests.cs:138,
and Lean-only xUnit1026 at SwiftObservableDictionary.Tests.cs:279. They also
occur in the frozen baseline. Initial extraction checks exposed missing test
coverage for inverted custom mapper ranges and forwarded getters/capacity;
focused tests close those gaps. No new test/build failure remains. The
historical FMS #45 Debug precondition concern and provider Debug allocation
signal remain Phase 1 baseline evidence; hash mapping does not use those paths.
Their independent resolution is outside this phase. This pass edits neither
provider.

#### Provider snapshots and concurrent work

The frozen comparison provider is clean commit
`0a81be7b59920d367692eff35e19d4dda1bb2b4b`. During this pass, independent
uncommitted FixedMathSharp changes appeared in `ConvexHullCylinderContact.cs`,
`WideOrientedBox.CylinderStrictOverlap.cs` and `WideOrientedBox.ScaledHull.cs`,
with related tests/guidance/CI records. They remove the disputed Debug failure
and cache box-hull triangle topology; this pass preserves them. The
production-source diff fingerprint at validation is SHA-256
`e3a0c7300c1bb27f2c6811ae78ca4266e235004cc5681a76a8012c34e51aa85a`
(`git diff --binary HEAD -- src/FixedMathSharp`). No upstream full-suite or
Debug-resolution claim is made here.

Old/new performance controls load the same frozen provider. Source-mode builds
and their test outputs also exercise the current provider patchset; final
netstandard coverage and all four consumer replays are repeated with its
canonical DLL/PDB graph. Both providers retain source identity 0.0.0.0 and the
same revision-bearing product version. Their whole DLLs differ: the bounded
metadata comparison finds real WideOrientedBox topology-storage changes, so
whole-provider equivalence is not claimed. Query-used Fixed64 comparisons and
FixedBoundBox/FixedBoundArea intersection methods and implementation hints are
byte-identical in the Standard net10.0 comparison. Current-graph query traces
also match the frozen graphs. All eight SwiftCollections runtime DLL hashes
remain stable through canonical rebuilds. Chronicler remains clean.

#### Replay, ownership and footprint

The legacy probe reuses the Phase 0 XYZ seed, six profiles, three sizes, 16
query windows, cell size eight and mutation history. It additionally checks
neighborhood candidates, complete exact hit sets, append prefixes, canonical
keys, growth, remove/reuse and reverse insertion after clear. All **144 ordered
trace rows** match old/new across all eight baseline/current x variant x target
graphs; every warmed exact/neighborhood batch allocates zero bytes.

The native XY consumer uses two coordinate draws per entry from seed 0xC0FFEE,
the same profile transforms and sizes, and the same 16 anchor/window rule. Move
the first N/4 by (1,1); then remove/reinsert N-1, churn the first N/4 for eight
remove/reinsert passes, clear and reverse reinsert. Exact hit sets use
independent endpoint inequalities. Cell/point candidate sets use independent
floor mapping, with BigInteger raw division for fixed coordinates. Fold result
counts and ordered keys using the Phase 0 FNV rule, separately for exact,
neighborhood and point candidates. All **180 lifecycle trace rows** match across
the four native graphs. All **108 warm allocation rows per graph** (exact,
neighborhood and point) are zero bytes. Canonical reference identity, cleared
reference keys, retained entry arrays and cell-table storage are checked.

For fixed N=2,048 moving, initial exact/neighborhood/point traces are
`76A9FC9DB09549D6` / `91D501EB1C45B18C` / `58A3DBF47D50BF57`. After moving,
exact is unchanged; neighborhood/point become `508C8A80229F07F3` /
`D31EF1BC66799392`. Remove/reinsert and bounded churn preserve that row;
clear/reverse insertion changes the order as permitted.

| Windows x64 public constructor, capacity 16 |  Before |   After |
| ------------------------------------------- | ------: | ------: |
| Existing Numerics XYZ, including mapper     | 2,528 B | 2,528 B |
| Existing fixed XYZ, including mapper        | 2,280 B | 2,280 B |
| Native Numerics XY, including mapper        |     N/A | 1,568 B |
| Native fixed XY, including mapper           |     N/A | 1,896 B |

These are average managed allocations over 128 constructions after 32 warmups,
not retained-heap graph sizes. XY cell values are 8 B versus XYZ's 12 B; native
entry values are 28 B Numerics and 48 B fixed. Bounded churn preserves
entry-array identity; clear preserves entry/cell-table capacity, removes
occupied cells and releases all cell-list and reference-key slots. Refilling can
allocate new lists. Native mutation controls also compare 612 public-state/query
trace rows and 324 retained-capacity rows across all four graphs: all match.
Warm same-bounds upsert/update, contains and no-growth capacity checks allocate
zero; relocation and cell recreation retain their existing allocation contracts.
The first cold Numerics insertion sample totals 163,248 B Standard versus
163,104 B Lean over 512 inserts, identically within each target. Do not turn
warm results into a zero-allocation cold-setup or mutation promise.

#### Measured shared-core costs

Host: Windows 11 10.0.26300.10075, i7-9700K, .NET SDK 10.0.302 and runtime
10.0.10 x64. Acceptance timings run without competing builds/tests/profilers.
Legacy processes disable tiered compilation and use balanced ABBA order, eight
runs per version. Each sample is 64 batches of 16 exact queries, after warmup,
using the same history and verified loaded DLLs. At N=2,048, medians in
microseconds per batch are:

| Profile    | Numerics before/after | Fixed before/after |
| ---------- | --------------------: | -----------------: |
| Uniform    |         0.859 / 0.810 |      1.020 / 0.958 |
| Clustered  |         8.770 / 8.409 |      9.707 / 8.688 |
| Mixed size |         0.964 / 0.897 |      1.143 / 1.063 |
| Sparse     |         1.189 / 1.124 |      1.419 / 1.328 |
| Coincident |     184.841 / 187.139 |  177.260 / 165.169 |
| Moving     |         0.857 / 0.786 |      1.010 / 0.939 |

The dense Numerics distributions overlap; this does not establish zero overhead
or a meaningful slowdown. The other tested query profiles are generally lower.
The first extraction added eight constructor bytes and showed a dense-loop
signal. Primitive padding storage removes the extra bytes; a local read-only
span removes repeated list-indexer work without changing cell order or callback
semantics. Unsupported inline-hint experiments were discarded. No extra heap
owner or per-cell delegate remains. These bounded controls do not establish
universal timing equivalence.

A separate balanced mutation control uses four processes per version, all six
profiles/sizes, two warm passes and 16 measured passes over up to 256 existing
keys. Moving alternates (9,0,9) and original bounds; remove/reinsert restores
the same keys. It separates construction, fresh-storage insertion, unchanged
upsert/update, lookup, movement, removal/reinsertion, no-growth capacity,
clear/refill and explicit growth. All allocation rows, **612 state/query rows**
and **324 capacity rows** agree between old/new. Relocation median ratios range
0.951-1.033, remove/reinsert 0.884-1.029 and clear/refill 0.895-1.117 across the
tested cases; short operations and individual ranges remain noisy. These are
bounded observations, not universal latency-equivalence claims.

An initial first-explicit-growth sample mixed code-warm states: old Insert uses
its public EnsureCapacity, while extracted Insert uses the core method before
the facade's first explicit call. At N=128 uniform it observed 1.382/3.777 ms
Numerics and 1.350/2.705 ms fixed before/after for growth plus 128 inserts. This
is consistent with unmatched first-use compilation, not an isolated steady-state
growth comparison; exact startup attribution is not established. The
discriminating control warms the same explicit Grow helper on three independent
prefilled owners, then times four fresh prefilled owners, excluding
construction/fill. Warm growth is 185.156/180.274 ns per insert Numerics and
196.485/168.262 ns fixed at N=128 uniform; at N=2,048 uniform it is
128.260/127.850 and 140.753/138.916 ns. Allocation totals match. Keep the
first-use observation distinct; no startup-latency guarantee or runtime fix is
claimed from it.

#### Native XY query benchmark

The maintained `Query2DSpatialHashBenchmarks` uses unit boxes, seed 0xC0FFEE,
two draws modulo 1024, capacity N, cell size eight and 32 windows anchored at
(97*q+13)%N with offsets [-2,+4]. Controls materialize the same caller-owned
results: native XY, XYZ embedding with Z=[0,1], and brute force. Setup checks
hit counts independently. BenchmarkDotNet 0.15.8 ShortRun uses one launch, two
warmups and five measured iterations with the actual build configuration.

| Variant / entries | Native XY | XYZ embedding | Brute force |
| ----------------- | --------: | ------------: | ----------: |
| Standard / 128    |  0.898 us |      1.096 us |    5.018 us |
| Standard / 2,048  |  1.011 us |      1.204 us |  182.773 us |
| Standard / 8,192  |  1.348 us |      1.635 us |  750.845 us |
| Lean / 128        |  0.924 us |      1.100 us |    3.812 us |
| Lean / 2,048      |  1.007 us |      1.221 us |  187.280 us |
| Lean / 8,192      |  1.283 us |      1.575 us |  739.050 us |

Every method reports zero managed bytes. Both runs complete nine child processes
with exit zero and 45 actual measured iterations; outlier handling retains 41
Standard and 43 Lean results. They are sparse-window workload observations, not
cross-flavor speed claims. Kept child configuration/dependencies are inspected;
Lean uses the shim and has no MemoryPack.Core runtime dependency.

Child core SHA-256: Standard
`ce4010218a8e61d5a67bd39267fa2f5ba496c405c908b2f738b376531643a050`; Lean
`af7f72d252b8e8ea135cc77f76d8f5bef024849fbd5099be61db340edb2543f2`.
Method-name-plus-IL hashes for both facades, the core/entry, grids, cell index
and concrete mappers match ordinary outputs: Standard
`10c768585417e40c6ed15e4bf2f218d6ce13b7bb1b34fac08ce5fd8dca0073c8`; Lean
`7a9ccf4e24f586f66e6ef76c85054220cd1c9bf8d9aa8aa65c834b9a252632b2`. Whole DLL
identity across different build paths is not assumed.

#### API, documentation and remaining distribution gates

Existing XYZ public/protected declarations, parameter names, generic constraints
and protected hooks match the frozen source. The additive inventory is **35
authored XML entries: 30 core and five companion**, covering five public types,
all constructors, properties, methods, operators and the protected hook. Every
entry has a summary and applicable param/typeparam/returns/value tags. Inherited
members retain the generic owner's identity and correctly specialize their
volume type in the rendered member list. Accessors/backing fields and the cell's
implicit default construction are synthesized; no public nested type, indexer,
partial declaration or generated serializer API was introduced.

All four source-validation package IDs include matching net10.0 and
netstandard2.1 DLL/XML pairs: SwiftCollections, SwiftCollections.Lean,
SwiftCollections.FixedMathSharp and SwiftCollections.FixedMathSharp.Lean. All
eight pairs match ordinary outputs and contain the 30/five additive entries;
Lean retains its shim without MemoryPack runtime/generator dependencies. These
are build fixtures, not published release packages or a release decision.

Fresh metadata/site output from the canonical DocFX configuration builds with
warnings as errors: zero warnings/errors, 113 HTML pages, 1,467 local paths and
459 local fragments resolve. Existing generated output was preserved before the
fresh build. An isolated authored-layout preview also passed; its resource
mapping needed a relative source path. Canonical edit links use tracked docs
paths. Rendered review covers both XY facades, fixed wrapper, mapper, cell
operators/properties, inherited links and the spatial guide: parameter names,
out validity, returns, values, exceptions and links agree with source. Browser
preview checks the guide at desktop and 390px width and the generic API page;
code blocks scroll within their container on narrow screens. No theme change.

C# formatter verification and documentation formatting pass; the existing SC
design-time workspace-load warning remains, with successful real builds. No new
source defect or unresolved API decision was identified for the bug tracker.
This phase makes no CI policy, dependency pin, version, index, commit or
provider source change; concurrent provider work is preserved separately.
Hosted/provider release readiness still has its separate Phase 1 gates. Actual
Unity Mono/High-stripping IL2CPP, hosted OS lanes and distribution closeout
remain Phase 4 requirements; this local result does not close them.

### Phase 3 — quadtree and shared subdivision tree

Complete for implemented scope and local Standard/Lean/target, coverage,
performance and documentation gates. The user accepted the bounded Numerics
timing tradeoff for the shared owner, prioritizing the fixed-point LSF workload.
Unity and distribution closeout remain Phase 4.

The phase starts from clean SwiftCollections
`57c6fb73e9151b66fdde9d7490977493a46cab2b`, FixedMathSharp
`8fdf889322f1bb82501c2afec63d6bb2f304ace2` and Chronicler
`0281469ecc40e65a05867f473bc10f030e7f69f8`. No provider source, dependency
version, hosted pin or CI policy change is required. The independent provider
fixes noted in Phase 2 are now committed upstream; no upstream full-suite
resolution is claimed by this phase.

#### Ownership, contracts and tests

`SwiftQuadtree<TKey,TVolume>`, Numerics `SwiftQuadtree<TKey>` and
`SwiftFixedQuadtree<TKey>` implement the frozen surface, alongside
`IQuadtreeBoundsPartitioner<TVolume>` and `SwiftQuadtreeOptions`. The existing
XYZ facade and new XY facade embed one mutable `SubdivisionTreeCore`.
Constrained struct adapters retain each public backend and supply eight/four
actual children. Ownership, key lookup, relocation, stable entry order,
subdivision and merging have one implementation; the superseded octree engine is
removed. Callbacks stay bound to the facade and read the current core after
growth/clear. Primitive option fields avoid a second stored options value;
zero-capacity getters return default without invoking validating constructors.

Numeric XY reuses BoundVolume2D.Center for the widened-then-float split plane.
Fixed XY uses FixedBoundArea's nearest-even center through its existing wrapper.
Both minimum child spans on each axis must qualify. The shared companion
`FixedTreeSubdivision` predicate/size guard serves old XYZ and new XY without
materializing full Size/Area/Volume or adding wide arithmetic. Children use xBit
| (yBit << 1), exact split points prefer the upper half and straddlers stay at
the parent. Parents are queried before children in increasing index order.

Tests cover full finite float worlds, adjacent/subnormal/degenerate bounds,
complete raw fixed domains, 512 seeded BigInteger midpoint/span controls, actual
shrinking/contained four-child topology, depth/merge/default options,
world-validation order, canonical keys, collisions, growth, freed slots, partial
callback/destination failures, reference cleanup and append prefixes.
Interface-only custom volumes and non-IEquatable reference keys protect the
frozen generic constraints.

One introduced allocation was isolated before the fix: existing-key Insert with
int keys allocated 24,000 B per 1,000 operations under the test's JIT
conditions; Query, Contains, TryGetBounds and unchanged Update allocated zero.
Replacing the object null guard with existing ChronicleGuard.ThrowIfNullGeneric
preserves null/parameter/order contracts and removes the boxing dependency. The
same five separate typed allocation tests now pass at zero bytes.

Clear preserves the existing subtlety: nonempty Clear replaces the root and
discards topology while retaining entry capacity; an already-empty Clear is a
no-op, including after removing every entry with merging disabled. Both public
partitioner interfaces now explicitly require stable behavior for the tree's
lifetime. Changing split planes while old empty children remain can misroute
later entries. No runtime clear-policy change or new restore API is added.
Independent first-slice and final source reviews verified the shared boundary,
exception/output ordering and applicable XML contracts.

#### Build and strict coverage results

Serial source builds use `UseLocalLsfStack=true`, globally carry
`DisableTransitiveProjectReferences=true`, and build the unchanged solution with
`-m:1`. Both test projects use their existing runsettings and fresh result
folders. The existing full-coverage assertion passes every lane below without
exclusions or complexity-register waivers.

| Library DLLs executed               | Core tests | Companion tests | Coverage                                                 |
| ----------------------------------- | ---------: | --------------: | -------------------------------------------------------- |
| Release / net10.0                   |      2,125 |              98 | 100% line, branch and executable method, both assemblies |
| ReleaseLean / net10.0               |      2,017 |              98 | 100% line, branch and executable method, both assemblies |
| Debug / net10.0                     |      2,123 |              98 | 100% line, branch and executable method, both assemblies |
| Release / actual netstandard2.1     |      2,124 |              98 | 100% line, branch and executable method, both assemblies |
| ReleaseLean / actual netstandard2.1 |      2,016 |              98 | 100% line, branch and executable method, both assemblies |

Core covered/valid counts: Release 6,274/6,274 lines, 2,952/2,952 branches,
1,400/1,400 executable methods; Lean 6,080/6,080, 2,876/2,876, 1,359/1,359;
Debug 8,244/8,244, 2,962/2,962, 1,400/1,400. Companion Release/Lean counts are
205/205 lines, 86/86 branches and 67/67 methods; Debug has 257/257 lines with
the same branch/method counts. The older target uses matching DLL/PDB/MemoryPack
dependencies and the existing disposable test-project/source-root/legacy-call
bridge approach on .NET 10. The one net10-only JSON case is inapplicable;
netstandard is not a runtime.

The clean baseline also passes Standard/Lean coverage with 2,082/1,974 core and
74 companion tests. Pre-existing xUnit2013 (SlotMap line 138), Lean xUnit1026
(ObservableDictionary line 279), and the formatter's design-time workspace-load
warning remain. Initial new getter and Debug-only hash branch gaps were closed
by real public API assertions. No failing product check remains in these lanes.

#### Replay, allocations and storage

XYZ uses the existing Phase 0 seeded six profiles at N=128,2,048,8,192, 16 query
windows, world [-16,131104] per axis, options (8,8,merge), minimum size one, and
both merge values. Native XY uses two seed draws per entry, identical profile
transforms and (1,1) movement. Each lifecycle inserts, queries, moves the first
N/4, removes/reinserts N-1, performs bounded churn, clears and
reverse-reinserts. Complete hit sets use independent endpoint inequalities;
query counts/ordered keys use the existing FNV fold.

All 360 ordered lifecycle rows agree across four native graphs and across all
eight old/current XYZ graphs. Warm Query and the combined unchanged
lookup/update/upsert batches allocate zero bytes. Retained entry capacity and
node/depth observations agree; populated Clear returns to one root. Custom and
reference-key contracts have separate maintained tests.

| Windows x64 footprint, node capacity eight         | Existing XYZ before/after | Native XY |
| -------------------------------------------------- | ------------------------: | --------: |
| Numerics public constructor including backend      |           1,416 / 1,416 B |     936 B |
| Fixed public constructor including backend         |           1,256 / 1,256 B |   1,096 B |
| Numerics entry payload                             |                 80 / 80 B |      32 B |
| Fixed entry payload                                |                 64 / 64 B |      48 B |
| Numerics node object, excluding referenced storage |               112 / 112 B |      64 B |
| Fixed node object, excluding referenced storage    |                 96 / 96 B |      80 B |

Constructor allocation averages 128 creations after 32 warmups. Node-only
allocation uses warmed RuntimeHelpers.GetUninitializedObject to separate object
payload from its list/children; entry size uses Unsafe.SizeOf. This is a bounded
CLR footprint check, not retained-heap sizing or Unity/AOT evidence. Native
nodes allocate four actual children rather than an eight-child embedding.

#### Shared-tree timing and disposition

Host matches Phase 2: Windows 11 x64, i7-9700K, SDK 10.0.302/runtime 10.0.10.
Balanced ABBA processes disable tiered compilation, warm the real typed paths
and measure 64 batches of 16 queries. No competing builds/tests/profilers. The
span loop views private node-index storage without clearing/copying; mutation
and reentry restrictions keep it stable. Query state, order and constructor
budgets agree with the baseline.

Broad results vary by geometry and process placement. A discriminating N=2,048
mixed-size control, four processes/version, observes Numerics medians 14.268 ->
15.363 us/batch with merging off and 14.321 -> 15.162 us with merging on:
approximately 6-8%, or 50-70 ns/query. Fixed medians in that control are 14.989
-> 8.130 and 14.676 -> 8.212 us. These are bounded caller results, not universal
backend speed ratios or zero-overhead claims.

Generated-code inspection found different recursive inlining from the old class
owner. Array caching, explicit static traversal, compiler-hint boundaries and
empty-node hints did not produce a consistent overall win; those experiments
were removed. The final source retains the simple local span scan, without
speculative dispatch frameworks or tuning attributes. Disposition: the user
accepted the bounded Numerics cost in favor of the shared owner, clarifying that
LSF consumers use the fixed-point controls. The local performance gate is closed
with this tradeoff, not with a claim of unchanged Numerics timing. Reopen for a
representative consumer whose measured query budget is exceeded, a new
allocation/storage regression, or a consistent fixed-point regression; retain
these profiles and units when comparing a proposed optimization.

A separate mutation comparison uses independent prefilled owners and the same
six profiles, three sizes, both merges and backends. Each operation has four
warmups and 16 measured passes: fresh-storage insertion, changed update out/back
pairs, remove/reinsert pairs (at most 128 touched keys), clear/refill and
implicit N->2N growth through insertion. Constructors/prefill are outside growth
timing. All 432 state/query rows, 288 retained-storage rows and allocation
vectors agree old/current. Pair timings count a two-call pair; insertion/growth
and clear/refill are amortized per inserted key. Do not label node recreation as
allocation-free or mistake first-use/setup dispersion for a proven warm
algorithmic regression.

#### Maintained benchmarks, reference and final closeout

Maintained Query2DQuadtreeBenchmarks compares native XY, positive-thickness XYZ
embedding and brute force, materializing the same caller-owned results. It uses
unit boxes, seed 0xC0FFEE/two draws modulo 1024, N=128,2,048,8,192, 32 anchor
windows [(97*q+13)%N]-2/+4, world XY=[0,1024], embedded Z=[-512,512] with entry
Z=[0,1], depth/capacity 8/8, merging off and minimum size one. BenchmarkDotNet
0.15.8 ShortRun uses two warmup and five measured iterations, one launch, in
each real package configuration. Each run completes nine child processes with
exit zero and 45 measured iterations; statistics retain 43 Standard and 39 Lean
results after outlier handling. Mean microseconds per 32-query batch (native XY
/ XYZ embedding / brute force):

| Entries | Standard                   | Lean                       |
| ------- | -------------------------- | -------------------------- |
| 128     | 0.8410 / 1.9787 / 4.0318   | 0.8882 / 2.0227 / 4.0006   |
| 2,048   | 1.7779 / 3.5549 / 185.8097 | 1.7838 / 3.6861 / 200.8102 |
| 8,192   | 2.7253 / 5.5557 / 760.0593 | 2.7400 / 5.6555 / 749.0168 |

Every method reports zero managed bytes. These sparse-window observations do not
establish a universal speed ratio or a cross-flavor speed difference. Kept child
dependencies/configurations match their intended flavor; Lean has its shim and
no MemoryPack.Core runtime dependency. Child core SHA-256: Standard
`e75e5b4e9af71881237b4f1992d323c768c3a85db517144112629f5339f133b7`; Lean
`04bfc4d66796e1966fdae8de930b3314363b8d40c0c46086bdf6ef5479a58c16`. Method-name,
IL and implementation-attribute hashes for both facades, core, entry/node,
adapters, options and concrete partitioners match ordinary outputs: Standard
`3dfc4557f7f212c83f807abf3096dcbc711be5a79758c98a7f01ea40db2d0f8e`; Lean
`2df7f36847ac3e671de013a0f344033db0c2de846f71963597f8d731fc889fa4`. Whole DLL
identity across differing build paths is not assumed.

Native mutation replays also pass all four variant/target graphs. Their 432
state/query rows, 288 storage rows and all 360 per-operation allocation rows
agree. These are function/storage/allocation controls, not a native timing
comparison.

All 32 new authored XML entries (30 core, two companion) have applicable tags
reviewed against signatures, including options operators and property values.
Eight source-validation packaged DLL/XML pairs match both targets/variants; Lean
has its shim and no MemoryPack runtime/generator dependency. Package tags now
include quadtree. Fresh canonical DocFX metadata/site output builds with
warnings as errors and zero warnings/errors. All 118 HTML pages, 1,536 local
paths and 480 fragments resolve. Rendered review covers the generic owner,
Numerics/fixed specializations, options, partitioner and guide: names, returns,
property values, inherited members, exceptions and links agree with source.
Desktop and 390px browser previews confirm the layout; narrow text wraps and
code blocks scroll within their container. No theme change.

No new serializer API, authored public nested type or indexer was added; default
struct construction/accessors are synthesized. Existing XYZ public signatures
and constraints are retained. Targeted C# formatter verification, documentation
formatting and whitespace checks pass with the existing design-time workspace
warning only. No new bug-tracker issue or unresolved API decision remains. The
final runtime binaries match those executed in the full coverage/replay matrix
after removal of the discarded tuning experiments.

Runtime growth ledger: 13 touched authored source files, 10 new files; net +762
physical lines and +326 nonblank/noncomment lines relative to the phase
baseline. Tests, benchmarks, docs and project metadata are counted separately.
The old octree ownership engine is removed; two dimensional facades share one
implementation and both fixed backends share the split-size helper. No legacy
engine, fallback traversal or unused runtime framework remains.

Unity Mono/High-stripping IL2CPP, hosted OS CI and released-provider readiness
remain Phase 4/independent gates. This phase does not publish packages, modify
CI policy, update pins or stage/commit changes.

- Phase 4: not started.
