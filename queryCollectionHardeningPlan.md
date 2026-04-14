# Query Collection Hardening Plan

This document is the working implementation plan for hardening the BVH query collection pipeline.

## Context

- `SwiftBVH` is performance-critical for deterministic physics workloads.
- `FixedMathSharp` dependency was removed from core `SwiftCollections` to avoid upstream break risk.
- Current `IBoundVolume` runtime polymorphism introduces hot-path overhead and runtime-only type safety.
- This release is already breaking-change friendly, so we can optimize API shape now.

## Target Architecture

- One core engine:
  - `SwiftBVH<TKey, TVolume>`
  - `where TVolume : struct, IBoundVolume<TVolume>`
- One ergonomic default wrapper:
  - `SwiftBVH<TKey> : SwiftBVH<TKey, BoundVolume>`
- Typed volume contract:
  - Replace `IBoundVolume` with `IBoundVolume<TVolume>`
  - Strongly-typed operations (`Union`, `Intersects`, `GetCost`)
  - Explicit semantic bounds equality support (min/max based)
- Deterministic/fixed-point support:
  - Moved to companion package (`SwiftCollections.FixedMathSharp`) with `FixedBoundVolume`
  - Core package remains free of `FixedMathSharp`

## Non-Goals

- No broad query algorithm rewrite unless profiling proves necessary.
- No unrelated modernization or style churn.
- No Unity-specific additions in this repo.

## Phased Plan

## Phase 0 - Baseline And Safety Rails

Status: `Completed` (2026-04-14)

Goals:
- Lock in current behavior and performance baselines before refactor.

Tasks:
- Add/verify targeted tests for existing `SwiftBVH` behavior in numerics path.
- Add focused tests for update short-circuit semantics (bounds equality behavior).
- Add baseline benchmark coverage for BVH insert/query/update workloads.

Exit Criteria:
- All existing BVH tests pass.
- Benchmarks produce stable baseline artifacts for comparison.

Completion Notes:
- Added focused BVH benchmark suite:
  - `tests/SwiftCollections.Benchmarks/Benchmarks/Bvh.Workload.Benchmarks.cs`
- Generated baseline benchmark artifacts:
  - `BenchmarkDotNet.Artifacts/results/SwiftCollections.Benchmarks.BvhWorkloadBenchmarks-report.csv`
  - `BenchmarkDotNet.Artifacts/results/SwiftCollections.Benchmarks.BvhWorkloadBenchmarks-report-github.md`
  - `BenchmarkDotNet.Artifacts/results/SwiftCollections.Benchmarks.BvhWorkloadBenchmarks-report.html`
- Added focused bounds-equality safety tests in:
  - `tests/SwiftCollections.Tests/Query/SwiftBVH.Numerics.Tests.cs`

## Phase 1 - Typed Volume Contract

Status: `Completed` (2026-04-14)

Goals:
- Remove runtime-polymorphic volume operations from hot paths.

Tasks:
- Introduce `IBoundVolume<TVolume>` self-typed interface in `Query/BoundingVolume/Volume`.
- Port `BoundVolume` to implement `IBoundVolume<BoundVolume>`.
- Add semantic equality support in volume type (min/max based; cache fields excluded).
- Update XML docs for the new interface and behavior expectations.

Exit Criteria:
- `BoundVolume` tests pass on both target frameworks.
- No runtime type mismatch checks needed for standard volume operations.

Completion Notes:
- Added typed volume contract:
  - `src/SwiftCollections/Query/BoundingVolume/Volume/IBoundVolume.cs`
  - New: `IBoundVolume<TVolume> where TVolume : struct, IBoundVolume<TVolume>`
- Kept temporary legacy bridge interface (`IBoundVolume`) so current BVH engine compiles until Phase 2 migration.
- Ported `BoundVolume` to typed contract:
  - Implements `IBoundVolume<BoundVolume>` and `IEquatable<BoundVolume>`
  - Added semantic bounds equality (`BoundsEquals`) using min/max only
  - Updated equality semantics (`Equals`/`GetHashCode`) to ignore cache metadata fields
- Updated BVH numerics tests to validate semantic equality behavior.

## Phase 2 - Generic BVH Engine Refactor

Status: `Completed` (2026-04-14)

Goals:
- Make BVH internals store concrete volume types directly.

Tasks:
- Refactor `SwiftBVHNode<T>` to `SwiftBVHNode<TKey, TVolume>`.
- Refactor `SwiftBVH<T>` core implementation to `SwiftBVH<TKey, TVolume>`.
- Update method signatures:
  - `Insert(TKey value, TVolume bounds)`
  - `UpdateEntryBounds(TKey value, TVolume newBounds)`
  - `Query(TVolume queryBounds, ICollection<TKey> results)`
- Remove interface-based boxing/dispatch from insert/query/update/combine paths.
- Ensure bounds-update propagation uses semantic equality.

Exit Criteria:
- Full BVH unit suite passes.
- No interface-typed bound storage remains in core node/engine path.

Completion Notes:
- Refactored node storage to typed volumes:
  - `SwiftBVHNode<TKey, TVolume>`
  - `Bounds` is now `TVolume`, not `IBoundVolume`
- Refactored core tree engine:
  - `SwiftBVH<TKey, TVolume> where TVolume : struct, IBoundVolume<TVolume>`
  - Typed signatures for `Insert`, `UpdateEntryBounds`, and `Query`
  - Typed combine/intersection/cost/equality paths throughout insert/update/query traversal
- Removed interface-typed bound usage from core node and BVH engine paths.
- Added temporary compatibility wrapper:
  - `SwiftBVH<T> : SwiftBVH<T, BoundVolume>`
  - Preserves current call sites while typed core migration lands.

## Phase 3 - Public API Ergonomics

Status: `Completed` (2026-04-14)

Goals:
- Preserve simple default UX while exposing typed performance path.

Tasks:
- Add wrapper/default type:
  - `SwiftBVH<TKey>` bound to `BoundVolume`
- Ensure wrapper docs and examples are clear for default users.
- Keep advanced typed API available for deterministic/custom numerics users.

Exit Criteria:
- Default usage remains concise for `System.Numerics`.
- Advanced users can opt into strongly typed custom volumes without runtime mismatch checks.

Completion Notes:
- Added wrapper type in its own file for default numerics UX:
  - `src/SwiftCollections/Query/BoundingVolume/SwiftBVH.BoundVolume.cs`
- Kept typed core API available:
  - `SwiftBVH<TKey, TVolume> where TVolume : struct, IBoundVolume<TVolume>`
- Added dedicated typed-volume tests for advanced API ergonomics:
  - `tests/SwiftCollections.Tests/Query/TestVolumes/TestBoundVolume.cs`
  - `tests/SwiftCollections.Tests/Query/SwiftBVH.TypedVolume.Tests.cs`

## Phase 4 - FixedMathSharp Companion Package

Status: `Planned`

Goals:
- Restore fixed-point volume support without coupling core package to external breaking changes.

Tasks:
- Create companion package project for FixedMathSharp integration.
- Implement `FixedBoundVolume : IBoundVolume<FixedBoundVolume>` in companion package.
- Add BVH tests for fixed-point path in companion package.
- Validate packaging/dependency metadata and versioning flow.

Exit Criteria:
- Deterministic fixed-point users have first-class support via companion package.
- Core `SwiftCollections` package has no `FixedMathSharp` dependency.

## Phase 5 - Docs, Migration, And Release Readiness

Status: `Planned`

Goals:
- Prevent user confusion and provide clean upgrade path.

Tasks:
- Update `README.md`:
  - Remove stale FixedMathSharp dependency claims in core package.
  - Document typed BVH API and default wrapper usage.
  - Document companion package path for fixed-point users.
- Add migration notes:
  - Old `IBoundVolume` -> `IBoundVolume<TVolume>`
  - `SwiftBVH<T>` behavior and new typed option
- Validate benchmark deltas versus Phase 0 baseline.

Exit Criteria:
- Docs reflect shipped code.
- Migration guidance is explicit for breaking changes.
- Benchmarks show neutral or improved performance on targeted workloads.

## Validation Matrix (Per Phase)

- Build:
  - `dotnet build SwiftCollections.sln -c Debug`
- Unit tests:
  - `dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build`
- Coverage gate when behavior is changed/refactored:
  - `dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build --collect:"XPlat Code Coverage" --settings coverlet.runsettings`
- Benchmarks (during baseline and final validation):
  - `dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8`

## Risk Register

- API break blast radius:
  - Mitigation: add migration guidance and wrapper for default path.
- Performance regressions during generic refactor:
  - Mitigation: baseline benchmarks + phased check-ins.
- Deterministic package drift:
  - Mitigation: dedicated companion tests + clear package boundary.

## Progress Tracker

- [x] Phase 0 complete
- [x] Phase 1 complete
- [x] Phase 2 complete
- [x] Phase 3 complete
- [ ] Phase 4 complete
- [ ] Phase 5 complete

## Notes / Decisions Log

- 2026-04-14: Agreed direction is one typed BVH core engine plus default wrapper and a FixedMathSharp companion package.
- 2026-04-14: Phase 0 completed with BVH insert/query/update benchmark baseline and focused bounds-equality tests.
- 2026-04-14: Phase 1 completed with `IBoundVolume<TVolume>` and semantic `BoundVolume` equality.
- 2026-04-14: Phase 2 completed with `SwiftBVH<TKey, TVolume>` typed core and typed node storage.
- 2026-04-14: Phase 3 completed with default wrapper file split and custom typed-volume API tests.
