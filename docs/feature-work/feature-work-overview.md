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

### 2D Query Parity

[2D query parity plan](2d-query-parity-plan.md) — Phases 1–3 complete for local
bounds, BVH, native XY spatial hash and quadtree gates. Add Numerics and
fixed-point 2D bounds, BVH, native XY spatial hash and quadtree families while
reusing existing ownership, preserving 3D contracts and recording measured cost
tradeoffs.

The spatial hash shares one embedded mutable core between XY and XYZ owners,
with dimensional mapping and stepping in constrained struct adapters. Local
Standard/Lean and both target-framework gates pass with full coverage for Phases
1–3. Native four-child quadtrees and existing eight-child octrees share the
embedded subdivision owner. Compatibility, coverage and local performance gates
pass with the bounded Numerics timing tradeoff accepted for the fixed-point LSF
workload. Phase 4 reviews all Phases 0–3 for optimization, hardening and code
reuse, then completes final feature validation, including Unity smoke checks.
FixedMathSharp owns checked area and exact area-growth operations; retain the
reminder to update its CI build/test dependency pin after cross-repo changes
land. The plan owns the Standard/Lean, target-framework, full-coverage,
performance and Unity acceptance matrix.

## Recently Completed

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
