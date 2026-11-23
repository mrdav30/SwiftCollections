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

### SwiftDenseTicketMap

[SwiftDenseTicketMap plan](swift-dense-ticket-map-plan.md) — planned dense value
storage with collection-owned generational tickets. Combine compact iteration,
swap-back removal and stale-ticket checks while retaining existing collection
contracts and each consumer's domain identity/order rules. Explore a
complementary externally keyed dense store for optional data sharing an owner's
tickets; implement the owning map first.

Phase 0 will freeze lifetime, state, capacity and borrowing contracts and
compare complete storage layouts against existing ticket-map and composed
baselines. It also records the paired ownership/lifetime checkpoint. The plan
covers determinism, code reuse, Standard/Lean, both target frameworks,
serialization, full coverage, realistic usage scenarios and isolated Unity
verification. An explicit documentation hardening step clarifies when to choose
slot, ticket, dense-ticket, sparse and packed containers in the existing
collection guide. External-store implementation and paired-store tests require a
focused follow-on plan; consumer migrations and ECS coordination remain outside
this feature.

## Recently Completed

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
