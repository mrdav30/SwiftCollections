# 2D Query Parity Plan

- **Status:** Planned; implementation and performance validation have not
  started.
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

## Proposed API and parity inventory

Names below are the recommended additive surface, to freeze in Phase 0. Keep
current 3D type names, constructors, generic constraints, inheritance
relationships and public/protected member signatures intact.

| Family                 | Proposed surface                                                                                          | Reuse boundary                                                                                                       |
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
  Clear, FindEntry, Query and TryQuery; retain NodePool, RootNode and
  RootNodeIndex through the generic engine. Document borrowed-array replacement,
  node-index reuse and the existing node inspection/mutation limits.
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
[spatial hash](../../src/SwiftCollections/Query/SpatialHash/SwiftSpatialHash.cs)
owns reusable entry arrays, free-slot reuse, upsert/relocation, key lookup,
capacity, clear and query-stamp rollover/duplicate suppression. Its
[mapper](../../src/SwiftCollections/Query/SpatialHash/ISpatialHashCellMapper.cs)
and protected candidate API explicitly use XYZ cell indices. Production 2D
storage must use XY keys and XY range loops. A Z=0 mapper alone retains a third
coordinate and padded neighborhood queries still visit extra Z layers. Keep
concrete loops at the dimensional boundary; share the ownership and
candidate-processing mechanics without per-cell delegates or iterator
allocation.

The [octree](../../src/SwiftCollections/Query/Octree/SwiftOctree.cs) explicitly
allocates eight children, while most traversal, redistribution and merge loops
already use Children.Length. Share these mechanics with four-child subdivision.
Preserve the existing
[octree partitioner](../../src/SwiftCollections/Query/Octree/IOctreeBoundsPartitioner.cs)
and its 0..7 contract. A quadtree must not publicly inherit from an octree just
to reuse code. The internal owner/layout mechanism remains a measured design
gate: composition can add an object, indirection or retained-owner cost.

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

The recommended new provider API is
`FixedBoundArea.GetAreaExpansionCost(FixedBoundArea other)`, with the same
public long-cost convention as the existing 3D volume-expansion metric: form
union area minus the current rectangle's area exactly, then round down to square
coordinate units and clamp only the final result to long.MaxValue. Zero/sub-unit
growth can yield zero; preserve deterministic BVH tie-breaking.

Full signed Q32.32 endpoints require unsigned raw spans and exact products up to
128 bits. Subtract raw areas before discarding 64 fractional bits. Put this
implementation and its exact tests in FixedMathSharp, reusing its existing wide
arithmetic. Do not copy wide geometry into SwiftCollections, multiply
potentially unrepresentable Fixed64 sizes, or require runtime
BigInteger/System.UInt128. BigInteger is suitable as an independent test oracle.

| Readiness boundary observed during planning                                                                      | State                                                                                                                                       |
| ---------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------- |
| Local FixedMathSharp `b501340558c6132484be2f40957233c1b3d29cab`                                                  | Clean; FixedBoundArea basics available, public area-expansion cost absent.                                                                  |
| Current SwiftCollections hosted-develop FixedMathSharp pin `3da3a8ad9964d3e0413080737fec12ba107b48e4`            | New area API readiness not established; it has not been added by this plan.                                                                 |
| Cached FixedMathSharp and FixedMathSharp.Lean 8.0.0, package revision `763efc57cea399cde375fe0ef4aa296c1eaa3cba` | Both TFMs expose bounds basics and public 3D volume cost; no public area-growth member found in package XML. No new API compile is claimed. |

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
- Recommended Numerics policy: finite ordered endpoints, a default zero-size
  origin box, and finite positive cell/minimum-node sizes. Freeze whether bounds
  constructors enforce the endpoint precondition in Phase 0; do not accidentally
  impose new validation or normalization on existing 3D callers.
- For new Numerics XY mapping, freeze an explicit floor/out-of-range conversion
  policy; recommend widened quotient plus Int32 clamping, documented as computed
  floating-point mapping. Preserve existing 3D mapping behavior during
  extraction; any incompatible correction requires separate disposition.
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

| Phase                         | Work                                                                                                                                                                                                                                                                              | Exit evidence                                                                                                                                                                                                                                                                                                   |
| ----------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 0 — Contracts and baselines   | Freeze the complete member inventory, names, numeric defaults/validation, custom-backend contracts and measured reuse mechanism candidates. Record source/package graphs and existing 3D output/order, allocation, time and storage baselines. Scope the FixedMathSharp addition. | Decision record below completed; a bounded layout/prototype comparison supports the intended common owners. No performance or dependency gate inferred from source inspection.                                                                                                                                  |
| 1 — Bounds and BVH            | Implement/test exact provider cost in FixedMathSharp, then both 2D bounds backends and thin wrappers over the existing generic BVH. Use insert → query → move → query → remove → reuse through public APIs as the first working slice.                                            | Correct hits and fixed traces, edge/full-domain cost oracles, NodePool/TryQuery parity, warm allocation and practical time; both affected repos pass applicable coverage/variant checks. Independent first-slice review before expansion. Hosted/package dependencies remain separately pending if unavailable. |
| 2 — Native XY spatial hash    | Introduce XY cell/mapper APIs and concrete loops while sharing ownership/stamps/relocation with XYZ. Uplift fixed mapping with both consumers. Include fixed point-candidate helpers.                                                                                             | Complete mutation/exact/neighborhood/candidate contracts, rollover and range-edge tests; native two-coordinate storage; measured 3D compatibility and allocation/time/footprint gates pass.                                                                                                                     |
| 3 — Quadtree and shared tree  | Add four-child backends and public wrappers around shared internal ownership/subdivision/merge logic. Keep existing 3D extension points and order.                                                                                                                                | Four actual children, contained/shrinking bounds, straddler and merge/depth tests, default-options controls, fixed full-domain tests and measured 3D compatibility gates pass.                                                                                                                                  |
| 4 — Distribution and closeout | Complete public docs/inventory, package inspection, supported runtime/Unity checks and any authorized consumer smoke. Reconcile all decisions, measurements and dependency gates here.                                                                                            | Every matrix lane and exclusion reported; strict coverage and required hosted/package/Unity gates pass. No release claim without separate release work. Archive by repository convention after completion.                                                                                                      |

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

Implementation gates below are planned requirements, not results from this
documentation-only change. Preserve existing CI policy.

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

| Decision                                   | Current disposition                                                                                                                       | Reopening/closure criterion                                                                                                           |
| ------------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------- |
| Shared ownership                           | Reuse BVH unchanged; uplift hash/tree ownership with actual 2D+3D consumers. Internal representation remains pending Phase 0 measurement. | Choose measured layout without breaking current public/protected identities; record and resolve confirmed costs before expansion.     |
| Fixed 2D heuristic                         | Recommend exact provider-owned area expansion, floored/clamped final long like 3D. New API is not available yet.                          | Freeze units/rounding, implement/test provider, then independently close source, hosted-pin and released-package gates.               |
| Names, validation and topology             | Recommended additive names/contracts above; bounds validation, Numerics cell conversion and final core layouts await Phase 0 freeze.      | Record selected signatures, boundary behavior and rationale; consequential behavior/performance departures need explicit disposition. |
| Persistence and additional query operators | Excluded from this parity pass; existing owner/state formats unchanged.                                                                   | Reopen only for a separately requested caller contract with concrete consumers.                                                       |

Maintain concise phase results in this file: revisions/dirty state, chosen
contracts, net runtime growth and deletions, test/coverage counts, baseline
versus introduced failures, workload/units and measured uncertainty,
assemblies/TFMs/ packages/XML checked, consumer/Unity evidence, dependency
readiness and remaining gaps. Preserve portable inputs, commands, outcomes and
reopening criteria here; completion must not depend on disposable captures or
separate phase reports.

- Phase 0: not started.
- Phase 1: not started.
- Phase 2: not started.
- Phase 3: not started.
- Phase 4: not started.
