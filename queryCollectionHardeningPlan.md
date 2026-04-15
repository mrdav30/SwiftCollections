# Query Collection Hardening Plan

## Context Snapshot

- `SwiftBVH`, `SwiftSpatialHash`, and `SwiftOctree` are compiled query collections and follow the typed core plus numerics convenience wrapper pattern.
- Fixed-math convenience wrappers remain in the companion package rather than the core library.
- Existing tests now cover BVH, SpatialHash, and Octree in the compiled test suite.

## Recommendation

- Keep BVH as the default/general query structure.
- Promote SpatialHash first for high-churn, mostly uniform-scale workloads.
- Promote Octree second for hierarchical region/chunk/LOD semantics.
- Avoid enabling either prototype directly; instead, harden them under the same architecture pattern used for BVH.

## Design Goals

- Preserve `netstandard2.1` and `net8.0` compatibility.
- Support both:
  - numerics-native usage in this repo (`System.Numerics` style consumers), and
  - fixed-math usage via the established companion-package strategy.
- Keep query core internals backend-agnostic and deterministic:
  - no `float`-specific internal logic in shared generic query cores,
  - backend numeric mapping belongs in wrapper/package adapters.
- Minimize API churn and keep each phase shippable.
- Add tests as first-class deliverables for each phase.

## Progress Notes

- Phase 0 completed:
  - public contract locked in `queryCollectionApiSpec.md`
  - fixed-math wrappers remain in the companion package
- Phase 1 completed:
  - shared query helpers added under `Query/Shared`
  - BVH moved onto shared key lookup, pooled traversal scratch, and diagnostics plumbing
- Phase 2 completed:
  - SpatialHash ships as a typed core plus backend-owned cell mapper pattern to preserve deterministic core behavior across numerics and fixed math backends
  - collision-heavy, multi-cell, false-positive filtering, and negative-space hashing tests landed with the hardened implementation
- Phase 3 completed:
  - Octree ships as a typed core plus numerics/fixed wrappers with backend-neutral shared options for depth, node capacity, and merge-on-remove behavior
  - minimum node size and other backend-specific subdivision thresholds live in wrapper-owned partitioners rather than the generic octree API
  - backend-specific subdivision and containment mapping lives in wrapper-owned partitioners, keeping the octree core deterministic and backend-agnostic
  - FixedMathSharp octree wrapper ships in the companion package alongside the existing fixed BVH and spatial hash wrappers
  - split thresholds, minimum-size guards, reinsertion, octant-boundary queries, and non-uniform density tests landed with the hardened implementation

## Proposed Phases

### Phase 0 - Contracts And Scope Lock

Deliverables:
- Define final public API surface for:
  - `SwiftSpatialHash<TKey, TVolume>` + convenience wrapper(s)
  - `SwiftOctree<TKey, TVolume>` + convenience wrapper(s)
- Define volume/position contracts shared across query collections (or clearly scoped per collection if contracts should remain isolated).
- Decide where fixed-math wrappers live:
  - in this repo behind generic contracts, and/or
  - in companion package only (mirroring current fixed BVH posture).

Acceptance Gate:
- Written API spec committed (types, methods, generic constraints, thread-safety expectations, update semantics).

### Phase 1 - Shared Query Infrastructure

Deliverables:
- Introduce reusable query primitives (internal-first):
  - key lookup/index mapping strategy (where needed),
  - optional pooled traversal scratch structures,
  - shared guard/throw patterns and diagnostics hooks.
- Normalize naming and folder structure under `Query/` to align with `BoundingVolume` style.
- Create test helper fixtures for spatial scenarios (deterministic seeded datasets).

Acceptance Gate:
- New infra builds cleanly with no behavior change to existing BVH tests.

### Phase 2 - Spatial Hash Hardening (First Promotion)

Deliverables:
- Replace prototype `SpatialHash<T>` with production-ready implementation:
  - deterministic cell hashing,
  - insert/remove/update support,
  - query-by-cell-neighborhood and bounded-query variants,
  - duplicate suppression strategy for multi-cell overlap cases.
- Add typed core + backend-specific wrapper pattern (matching BVH strategy).
- Add capacity/rehash behavior and clear complexity guarantees in docs.

Test Requirements:
- Functional tests:
  - insert/remove/update/query correctness,
  - collision-heavy scenarios,
  - large-object multi-cell behavior,
  - edge coordinates and negative-space hashing.
- Concurrency tests (if thread-safe API is promised).
- Regression tests for false-positive filtering boundaries.

Acceptance Gate:
- SpatialHash tests pass and coverage for new code is high enough to match repo expectations for new features.

### Phase 3 - Octree Hardening (Second Promotion)

Deliverables:
- Replace prototype `Octree<T>`/`OctreeNode<T>` with production-ready implementation:
  - explicit configuration object (`maxDepth`, `nodeCapacity`, `minNodeSize`),
  - stable split/merge rules,
  - insertion/removal/update with reinsertion policy,
  - region/radius/volume query APIs,
  - optional lazy rebuild/rebalance strategy for dynamic scenes.
- Add typed core + backend-specific wrapper pattern aligned with BVH/SpatialHash.

Test Requirements:
- Functional tests:
  - split thresholds and minimum-size behavior,
  - non-uniform density stress,
  - moving object update/reinsert correctness,
  - query correctness across octant boundaries.
- Depth and memory growth guardrail tests.

Acceptance Gate:
- Octree tests pass; behavior under tuning extremes is explicitly validated.

### Phase 4 - Integration, Docs, And Benchmarks

Deliverables:
- Re-enable compiled inclusion for hardened `Query/SpatialHash/**` and `Query/Octree/**` in `SwiftCollections.csproj`.
- Add benchmark suites comparing BVH vs SpatialHash vs Octree in representative scenarios:
  - dynamic-neighbor heavy,
  - heterogeneous-size mixed broad-phase,
  - static world with regional queries.
- Update `README.md`:
  - when to use BVH vs SpatialHash vs Octree,
  - tuning guidance and caveats,
  - companion package guidance for fixed math.

Acceptance Gate:
- Build + tests + benchmarks run successfully with no project drift.

### Phase 5 - Stabilization And Release Hardening

Deliverables:
- API polish pass and XML docs completion for public types.
- Performance regression baseline captured from benchmark outputs.
- Final packaging validation (NuGet metadata + README alignment + target checks).

Acceptance Gate:
- Release candidate checklist complete and no open correctness regressions.

## Cross-Cutting Standards Per Phase

- Every behavior change in `src/SwiftCollections/Query/**` must have matching tests in `tests/SwiftCollections.Tests/Query/**`.
- Keep changes focused; avoid broad modernization unrelated to query structures.
- Avoid committing build outputs (`bin/`, `obj/`).
- Keep companion-package interoperability explicit (no hidden fixed-math coupling in core package unless intentionally introduced).

## Suggested Execution Order

1. Lock API/contract decisions (Phase 0).
2. Ship SpatialHash hardening first (Phase 2) after shared infra (Phase 1).
3. Ship Octree hardening second (Phase 3).
4. Integrate and benchmark (Phase 4), then stabilize for release (Phase 5).

## Notes On Your BVH/Octree/SpatialHash Comparison

Your summary is directionally correct and matches practical tradeoffs:
- BVH should remain primary default.
- SpatialHash should be workload-driven (high churn + local neighbor queries + uniform-ish scales).
- Octree should be semantics-driven (hierarchical region partitioning/LOD/chunking), not treated as a universal BVH replacement.

The main implementation risk is not algorithm choice but API/contract drift across numeric backends; this plan prioritizes solving that first.
