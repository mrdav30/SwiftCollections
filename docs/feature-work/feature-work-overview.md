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

Keep status in one owning record. Link related bugs or benchmark signals from a
plan rather than duplicating their histories here. Do not add speculative ideas
without an agreed scope or concrete evidence gate.

## Active Feature Work

### 2D Query Parity

[2D query parity plan](2d-query-parity-plan.md) — planned; implementation has
not started. Add Numerics and fixed-point 2D bounds, BVH, native XY spatial hash
and quadtree families while reusing existing ownership and preserving 3D
contracts and measured costs.

The next step is Phase 0: freeze the API/numeric contracts and establish reuse
baselines. FixedMathSharp owns the required exact area-growth operation; local
source, hosted dependency pins and released packages have separate readiness
gates. The plan owns the Standard/Lean, target-framework, full-coverage,
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
