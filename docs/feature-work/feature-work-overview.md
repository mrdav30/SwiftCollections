# Feature Work Overview

## Purpose

This is the evergreen entry point for SwiftCollections feature work. Keep a
curated view of active scope, deferred design and significant completed work.
Update it when priorities or feature milestones change; keep design decisions,
phase results, measurements and detailed acceptance evidence in the owning plan.

Active and deferred plans live in this directory. Move completed plans into
`done/`, mark their disposition and update incoming and relative links. Archived
results describe the evidence at that time; they do not imply a fresh validation
run or a package release.

## Coordination

- **Features:** Track significant work here by plan name and link. Substantial
  new capabilities and extensions belong in focused plans, without bug IDs.
- **Bugs:** Use the [issue tracker](issue-tracker.md) for reproducible defects,
  correctness risks and defect investigations, including bugs discovered during
  feature work. Keep their reproducers and resolution evidence with the bug.
- **Performance signals:** Use the
  [benchmark signal backlog](benchmark-signal-hardening-backlog.md) for measured
  allocation, timing, scaling and benchmark-reliability concerns. Link a feature
  plan when substantial implementation is needed.

Feature plans end at implementation, hardening, tests, documentation and
supported-host validation. Keep release/publication, distribution and downstream
adoption outside their completion criteria. When source changes span repos,
include a reminder to update the owning CI build/test dependency pins after the
changes land; do not turn that reminder into a release-readiness gate.

Keep status in one owning record. Link related bugs or benchmark signals from a
plan rather than duplicating their histories here. Do not add speculative ideas
without an agreed scope or concrete evidence gate.

## Active Feature Work

### SwiftTicketStore

[Externally keyed dense ticket store plan](swift-ticket-store-plan.md) - packed
values attached to tickets issued by another owner; Phases 0 through 2 are complete. The
accepted name distinguishes local membership from an issuing map's lifetime authority. Dense
live iteration and a contiguous Values span remain explicit contracts. The
frozen contracts and measured storage decision select sparse positions, dense
tickets and packed values, with a narrow shared cold growth policy. The store,
validated state and persistence paths pass full Standard/Lean managed-target
coverage, including closed JSON metadata and cross-target replay. Phase 3 final
hardening, documentation and supported-host checks are next.

The plan covers optional components, exact ticket matching, explicit teardown
and coordinated restore, with Standard/Lean, both target frameworks, strict
coverage and actual Unity verification. It prioritizes shared sparse mechanics
where contracts agree, while preserving existing collection behavior. Automatic
ECS coordination, issuer callbacks and consumer migrations remain outside scope.

## Recently Completed

### SwiftDenseTicketMap

[Completed SwiftDenseTicketMap plan](done/2026-11-23-swift-dense-ticket-map-plan.md)
— collection-owned generation-checked tickets with packed live values, swap-back
removal, terminal-generation retirement and validated state preserving future
reuse. Standard/Lean and both target assets pass strict full coverage; selected
JSON/MemoryPack closures also pass Mono and High-stripping IL2CPP.

The [collection guide](../api/guides/collections.md#who-issues-the-identity)
compares identity ownership, slot-backed lookup, dense iteration, equality,
borrowing and externally owned IDs. The completed plan retains hardening,
performance/storage controls and verification limits. Consumer migrations and
the separate external store remain outside this completed feature.

### 2D Query Parity

[Completed 2D query parity plan](done/2026-11-22-2d-query-parity-plan.md) —
native Numerics and fixed-point bounds, BVH, spatial hash and quadtree families
reuse the existing dimension-independent owners. XY cells and four-child
subdivision remain explicit dimensional boundaries while preserving the
corresponding XYZ contracts.

The final optimization/hardening review removed value-key spatial-hash upsert
boxing. Standard/Lean and both target frameworks pass strict full coverage;
Windows x64 Unity Editor/Mono and High-stripping IL2CPP smoke checks verify the
fixed-point and custom-backend paths. The plan retains measured tradeoffs,
source revisions, verification limits and the cross-repo CI pin reminder. The
[spatial query guide](../api/guides/spatial-queries.md) owns current usage and
caller contracts.

### Comparer Persistence

[Completed comparer persistence plan](done/2026-11-20-comparer-persistence-plan.md)
— implemented comparison-aware persistence for sorted lists and hash owners,
including inherited collections, explicit custom factories and Standard/Lean
support. The
[serialization guide](../api/guides/serialization-and-diagnostics.md) owns
current usage and compatibility contracts.

The archived plan retains decisions, phase verification and historical tracking
context. Measured performance and reliability dispositions stay in the benchmark
backlog.
