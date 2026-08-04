# Full Coverage Restoration Design

## Goal

Restore SwiftCollections to exactly 100% line, branch, and method coverage while preserving correctness, determinism, hot-path performance, and zero-allocation behavior.

## Release Gates

- Coverage: 100% lines, 100% branches, and 100% methods for both runtime assemblies under the repository Coverlet configuration.
- Correctness: all Debug, Release, and ReleaseLean tests pass with no skipped or flaky cases.
- Determinism: tests use fixed inputs and seeds; repeated operations produce identical ordering and state.
- Allocations: no new allocations in existing zero-allocation tests or relevant BenchmarkDotNet memory results.
- Performance: relevant before/after BenchmarkDotNet medians must not regress by more than 5%; any larger change blocks release unless measurement shows environmental noise and a repeat run clears the gate.
- Compatibility: preserve public APIs and exception contracts unless a separate breaking change is explicitly approved.
- Coverage integrity: do not use `ExcludeFromCodeCoverage`, runsettings exclusions, or hollow tests to manufacture 100%.

## Approach

Use behavior-focused tests for reachable paths, delete or simplify private code proven unreachable, and rewrite untestable sequence-point shapes without changing behavior. Iterate fresh coverage after each independent domain so line, branch, and method deltas remain attributable.

The work has four implementation domains:

1. Sorting and sorted-range ingestion.
2. Diagnostic throw helpers and interpolated handlers.
3. Remaining collection, observable, dimension, and spatial-query gaps.
4. Test-quality cleanup and reflection removal.

Independent domains may be implemented in parallel. Each domain receives a focused review, and the integrated result receives fresh independent code and test-quality reviews.

## Sorting And Sorted-Range Ingestion

- Exercise `SwiftSortedList<T>` through public range APIs for non-collection iterators, self-add aliasing, reusable-capacity layouts, growth layouts, and read-only collections.
- Exercise both class- and struct-comparer introsort heap fallbacks with deterministic organ-pipe input.
- Keep the custom struct-comparer path because it exists to avoid boxing and enable devirtualization.
- Remove redundant zero-count guards, impossible median-index comparisons, and redundant capacity predicates only after caller proofs are recorded in tests.
- Prefer existing framework/standard-library helpers where they preserve allocation and performance gates.

## Diagnostics

- Consolidate helper tests into contract matrices covering exception type, parameter/object name, actual value, canonical message, and lazy interpolation.
- Exercise every compiler-selectable interpolation shape, including formatting, alignment, strings, and spans.
- Preserve public overloads. Unused public members are not zombie code merely because this repository has no current caller.
- Convert non-returning throw-only methods to expression-bodied forms where Coverlet assigns an unreachable closing-brace sequence point.
- Do not exclude diagnostic code from coverage.

## Remaining Coverage Gaps

- Add compact behavior matrices for reachable queue, bucket, sparse collection, observable, dimension, dictionary/hash-set, BVH, and octree branches.
- Fix missing-key assignment in `SwiftObservableDictionary<TKey,TValue>` so the indexer adds and raises the correct notification rather than silently doing nothing.
- Remove invariant-only private guards when all mutation paths prove the invariant locally and unsupported concurrent mutation is the only way to violate it.
- Keep validation at public, serialized-state, and other trust boundaries even if it is harder to cover.
- Rewrite compiler/return sequence-point artifacts to equivalent simpler forms rather than hiding them.

## Test Quality And Internal Visibility

- Remove exact duplicate tests only after confirming the retained test covers the same behavior and the coverage total does not fall.
- Merge repeated contract tests into xUnit theories or compact matrices when failure diagnostics remain clear.
- Replace tautological assertions with observable behavior and seed randomized tests.
- Retain legitimate “does not throw” tests when that is the contract.
- Prefer public behavior tests. When tests intentionally verify internal storage, collision, pooling, or allocation invariants, change the narrowest required private member to `internal` and use the existing `InternalsVisibleTo` relationship instead of reflection.
- Do not make private algorithms internal solely to force coverage when a deterministic public path reaches them.

## TDD And Integration Flow

For each behavior change or newly covered contract:

1. Add or strengthen a test and run it against the pre-change behavior.
2. Confirm the expected failure for bugs or missing contracts. For coverage-only tests of valid existing behavior, confirm the exact baseline coverage gap instead.
3. Apply the minimum production change.
4. Run the focused test, affected test file, and fresh domain coverage.
5. Remove or merge redundant tests and confirm coverage does not decrease.
6. Request an independent review before integration.

## Final Verification

Run from the solution root:

```powershell
dotnet build SwiftCollections.slnx -c Debug
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.Tests/coverlet.runsettings
dotnet build SwiftCollections.slnx -c Release
dotnet test SwiftCollections.slnx -c Release --no-build
dotnet build SwiftCollections.slnx -c ReleaseLean
dotnet test SwiftCollections.slnx -c ReleaseLean --no-build
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- list
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- query --list flat
```

Generate final ReportGenerator and CRAP reports, confirm no CRAP score exceeds 30, compare allocation/performance output with the pre-change baseline, and obtain independent code and test-quality reviews.
