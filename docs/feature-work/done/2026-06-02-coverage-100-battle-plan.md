# 100% Coverage Battle Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use coverage-analysis before
> updating this plan from fresh report data. Use test-driven-development before
> adding or changing behavior tests, and verification-before-completion before
> claiming a phase is complete.

**Status:** Done

**Goal:** Bring SwiftCollections to 100% line coverage and 100% branch coverage
without papering over real behavior gaps or adding hollow tests.

**Architecture:** Treat coverage misses as design feedback. Add focused behavior
tests for public/API-reachable branches, remove or simplify truly unreachable
defensive code, and keep hot-path collection/query code direct and
benchmarkable.

**Tech Stack:** .NET 8 test projects, xUnit v3, FluentAssertions, Coverlet
collector with `tests/SwiftCollections.Tests/coverlet.runsettings`,
ReportGenerator, SwiftCollections standard and FixedMathSharp assemblies.

---

## Current Baseline

Fresh coverage was generated from `Debug` builds with the shared Coverlet
runsettings.

Commands run:

```bash
dotnet build SwiftCollections.slnx -c Debug
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.Tests/coverlet.runsettings
dotnet test tests/SwiftCollections.FixedMathSharp.Tests/SwiftCollections.FixedMathSharp.Tests.csproj -c Debug --no-build --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.Tests/coverlet.runsettings
/tmp/codex-reportgenerator/reportgenerator -reports:"tests/SwiftCollections.Tests/TestResults/9ffd4658-dfcc-46cc-b645-4b97fa01ffd5/coverage.cobertura.xml;tests/SwiftCollections.FixedMathSharp.Tests/TestResults/63691ba8-1223-44cf-b2e7-59bdde4ed71a/coverage.cobertura.xml" -targetdir:"tests/TestResults/coverage-analysis/2026-06-02-full/reports" -reporttypes:"Html;TextSummary;JsonSummary;Cobertura" -assemblyfilters:"+SwiftCollections;+SwiftCollections.FixedMathSharp"
```

Artifacts:

- Core coverage input:
  `tests/SwiftCollections.Tests/TestResults/9ffd4658-dfcc-46cc-b645-4b97fa01ffd5/coverage.cobertura.xml`
- FixedMathSharp coverage input:
  `tests/SwiftCollections.FixedMathSharp.Tests/TestResults/63691ba8-1223-44cf-b2e7-59bdde4ed71a/coverage.cobertura.xml`
- Combined summary:
  `tests/TestResults/coverage-analysis/2026-06-02-full/reports/Summary.txt`
- Combined Cobertura:
  `tests/TestResults/coverage-analysis/2026-06-02-full/reports/Cobertura.xml`

Phase 0 reproduction artifacts:

- Core coverage input:
  `tests/SwiftCollections.Tests/TestResults/75f9021f-873a-43f2-998c-1a552073e80f/coverage.cobertura.xml`
- FixedMathSharp coverage input:
  `tests/SwiftCollections.FixedMathSharp.Tests/TestResults/76454ebe-6969-471f-a1c5-c0099d6e6fd8/coverage.cobertura.xml`
- Combined summary:
  `tests/TestResults/coverage-analysis/2026-06-02-phase0/reports/Summary.txt`
- Combined Cobertura:
  `tests/TestResults/coverage-analysis/2026-06-02-phase0/reports/Cobertura.xml`

Phase 1 artifacts:

- Core coverage input:
  `tests/SwiftCollections.Tests/TestResults/514b3082-a08d-4ad7-a832-5d87a1dd20b6/coverage.cobertura.xml`
- FixedMathSharp coverage input:
  `tests/SwiftCollections.FixedMathSharp.Tests/TestResults/2abd09c3-d8bb-458b-ba3d-338e3b790f63/coverage.cobertura.xml`
- Combined summary:
  `tests/TestResults/coverage-analysis/2026-06-02-phase1b/reports/Summary.txt`
- Combined Cobertura:
  `tests/TestResults/coverage-analysis/2026-06-02-phase1b/reports/Cobertura.xml`
- Delta from Phase 0: line coverage 98.4% -> 98.7%, branch coverage 94.7% ->
  95.4%, uncovered lines 109 -> 88, missing branches 130 -> 111.

Phase 2 artifacts:

- Core coverage input:
  `tests/SwiftCollections.Tests/TestResults/15b0c896-b6b4-4b8b-99af-9918cd0dc3b4/coverage.cobertura.xml`
- FixedMathSharp coverage input:
  `tests/SwiftCollections.FixedMathSharp.Tests/TestResults/98b8c15c-188a-4a9b-b432-ac49a1a9d784/coverage.cobertura.xml`
- Combined summary:
  `tests/TestResults/coverage-analysis/2026-06-02-phase2/reports/Summary.txt`
- Combined Cobertura:
  `tests/TestResults/coverage-analysis/2026-06-02-phase2/reports/Cobertura.xml`
- Delta from Phase 1: line coverage 98.7% -> 99.2%, branch coverage 95.4% ->
  95.9%, uncovered lines 88 -> 56, missing branches 111 -> 99.

Final artifacts:

- Core coverage input:
  `tests/SwiftCollections.Tests/TestResults/921ee8f4-2771-4968-86fb-71326398dfeb/coverage.cobertura.xml`
- FixedMathSharp coverage input:
  `tests/SwiftCollections.FixedMathSharp.Tests/TestResults/fb559497-ec18-455c-be57-38f73b5a2a7b/coverage.cobertura.xml`
- FixedMathSharp filtered coverage:
  `tests/TestResults/coverage-analysis/2026-06-02-phase3f/fixed-filtered/Cobertura.xml`
- Combined summary:
  `tests/TestResults/coverage-analysis/2026-06-02-phase3f/reports/Summary.txt`
- Combined Cobertura:
  `tests/TestResults/coverage-analysis/2026-06-02-phase3f/reports/Cobertura.xml`
- Delta from Phase 2: line coverage 99.2% -> 100%, branch coverage 95.9% ->
  100%, uncovered lines 56 -> 0, missing branches 99 -> 0.
- Note: the FixedMathSharp report was filtered to
  `+SwiftCollections.FixedMathSharp;-SwiftCollections` before merging so
  duplicate uncovered core assembly rows from the companion test project did not
  dilute the combined report.

Summary:

| Metric               |           Current |                             Remaining |
| -------------------- | ----------------: | ------------------------------------: |
| Line coverage        |              100% |                     0 uncovered lines |
| Branch coverage      |              100% |                  0 uncovered branches |
| Method coverage      |              100% |                   0 uncovered methods |
| Full method coverage |              100% |           0 not fully covered methods |
| CRAP risk            | 0 methods over 30 | Confirmed by `Compute-CrapScores.ps1` |

Top CRAP methods remain below the register threshold; with 100% line coverage
their CRAP score equals their complexity:

| Method                                                         |  CRAP | Complexity | Line coverage |
| -------------------------------------------------------------- | ----: | ---------: | ------------: |
| `SwiftDictionary<TKey, TValue>.InsertIfNotExist(TKey, TValue)` | 24.00 |         24 |          100% |
| `SwiftHashSet<T>.InsertIfNotExists(T)`                         | 24.00 |         24 |          100% |
| `SwiftBucket<T>.IndexOf(T)`                                    | 18.00 |         18 |          100% |
| `SwiftDictionary<TKey, TValue>.Remove(TKey)`                   | 16.00 |         16 |          100% |
| `SwiftSparseMap<T>.set_State(SwiftSparseMapState<T>)`          | 14.00 |         14 |          100% |
| `SwiftHashSet<T>.FindEntry(T)`                                 | 14.00 |         14 |          100% |

## Gap Worklist

The largest original gap clusters are below. Final `phase3f` Cobertura scan
reports no remaining uncovered lines or missing branch outcomes.

| Source file                                                            | Uncovered lines | Missing branches | Primary theme                                                  |
| ---------------------------------------------------------------------- | --------------: | ---------------: | -------------------------------------------------------------- |
| `src/SwiftCollections/Collection/SwiftDictionary.cs`                   |              25 |               11 | non-generic adapters, collision/probe tails, comparer switches |
| `src/SwiftCollections/Query/BoundingVolume/SwiftBVH.cs`                |              16 |               13 | remove/update invariants, single-child defensive branches      |
| `src/SwiftCollections/Collection/SwiftHashSet.cs`                      |              14 |               10 | probe tails, set algebra, comparer switches                    |
| `src/SwiftCollections/Query/Octree/SwiftOctree.cs`                     |               7 |               17 | merge/collapse traversal branches and internal invariants      |
| `src/SwiftCollections/Collection/SwiftSortedList.cs`                   |               6 |                8 | insert/remove/state/clear edge cases                           |
| `src/SwiftCollections/Collection/SwiftBucket.cs`                       |               6 |                7 | copy adapters, sparse peak/freelist branches                   |
| `src/SwiftCollections/Collection/SwiftList.cs`                         |               5 |                4 | range insert/add and formatting branches                       |
| `src/SwiftCollections/Query/SpatialHash/SwiftSpatialHash.cs`           |               3 |                6 | remove/update/query stamp branches                             |
| `src/SwiftCollections/Dimension/SwiftArray3D.cs`                       |               5 |                3 | default construction, shift normalization, enumerator          |
| `src/SwiftCollections/Collection/SwiftQueue.cs`                        |               2 |                6 | range/state/to-array/enumerator branches                       |
| `src/SwiftCollections/Collection/SwiftStack.cs`                        |               3 |                4 | copy/state/trim/enumerator branches                            |
| `src/SwiftCollections/Collection/SwiftGenerationalBucket.cs`           |               1 |                6 | handle/ref/remove/resize branches                              |
| `src/SwiftCollections/Collection/SwiftSparseSet.cs`                    |               0 |                6 | branch-only set operations and non-generic copy                |
| `src/SwiftCollections/Query/Octree/SwiftOctree.BoundVolume.cs`         |               0 |                5 | per-axis containment branch matrix                             |
| `src/SwiftCollections.FixedMathSharp/Query/Octree/SwiftFixedOctree.cs` |               0 |                5 | fixed-point per-axis containment branch matrix                 |

## Dead-Code And Optimization Candidates

Do not write tests that manufacture impossible private states. Prove
reachability first. If a branch is only reachable through corrupted internals,
remove or simplify it.

- Removed `SwiftBVH<TKey, TVolume>.RemoveRootLeaf(int)` and related root-leaf
  removal branching; public single-leaf removal clears before `RemoveFromTree`,
  and key lookup returns leaf nodes.
- Simplified BVH child/sentinel helpers around insertion, parent refresh,
  combined bounds, and bucket rehashing after confirming valid internal nodes
  have concrete children.
- Replaced octree null-conditional child loops with non-null child arrays in
  paths guarded by `HasChildren`.
- Removed octree and spatial-hash allocation guards reached only after
  `QueryKeyIndexMap` has already resolved a live entry.
- Removed the octree missing-entry throw in `RemoveEntryFromNode`; public
  operations maintain the owning-node invariant and should not manufacture
  invalid private state to cover it.

## Test-Quality Findings

No broad swallowed exceptions or always-true assertions were found in the quick
scan. There are a few test-quality items to clean up while hardening coverage:

- The hollow large-insert dictionary test was converted to deterministic stress
  coverage with count and lookup assertions.
- Pool no-op coverage was extended through stateful flush/rent behavior rather
  than no-op-only assertions.
- Query and dimension coverage additions used deterministic shapes and state
  data; avoid adding new unseeded `Random` fixtures.
- Existing tests already use good localized fixtures such as
  `CollisionStringFactory`, `SelectiveIntHashComparer`, and deterministic query
  datasets. Prefer extending those helpers over introducing ad hoc random
  fixtures.

## Behavior Callouts

- `SwiftHashTools.MurmurHash3(string, int)` now validates `key` explicitly and
  throws `ArgumentNullException` for null input instead of relying on an
  incidental null dereference inside the unsafe pinning path.

## Phases

### Phase 0: Coverage Contract And Tooling

- [x] Keep `tests/SwiftCollections.Tests/coverlet.runsettings` as the shared
      coverage settings file for both test projects.
- [x] Do not exclude production source files to reach 100%; only generated
      MemoryPack files should stay excluded.
- [x] Add a repeatable local coverage command or script only if it avoids
      command drift without adding CI complexity.
- [x] Preserve the combined report output under
      `tests/TestResults/coverage-analysis/<run>/reports` for each major phase.
- [x] After each phase, record line/branch deltas in this document.

Exit criteria:

- [x] Baseline commands reproduce 98.4% line and 94.7% branch coverage or
      better.
- [x] CRAP scan still reports 0 methods over 30.

### Phase 1: Prune Unreachable Internal Branches

Focus files:

- `src/SwiftCollections/Query/BoundingVolume/SwiftBVH.cs`
- `src/SwiftCollections/Query/Octree/SwiftOctree.cs`

Tasks:

- [x] Prove or disprove the BVH invariant that every non-empty internal node has
      two allocated children and that public removal never sends a root leaf
      into `RemoveFromTree`.
- [x] Remove dead BVH root-leaf/single-child/sentinel branches when the
      invariant holds.
- [x] Add or adjust public behavior tests around single-item removal, two-item
      removal, sibling promotion, update propagation, and query results after
      removal.
- [x] Replace octree null-conditional child loops with non-null locals in paths
      guarded by `HasChildren`.
- [x] Add focused octree tests for merge-not-needed,
      merge-blocked-by-grandchild, merge-blocked-by capacity, and collapse
      success.
- [x] Re-run coverage and update this plan with removed branch totals.

Exit criteria:

- [x] No tests rely on constructing invalid BVH or octree internals.
- [x] Query structures keep deterministic result behavior and no hidden
      allocations are introduced.
- [x] `docs/complexity-exceptions.md` is updated if any listed method complexity
      or coverage changes materially.

### Phase 2: Public Adapter And Default-State Gaps

Focus files:

- `src/SwiftCollections/Collection/SwiftDictionary.cs`
- `src/SwiftCollections/Collection/SwiftBucket.cs`
- `src/SwiftCollections/Collection/SwiftStack.cs`
- `src/SwiftCollections/Dimension/SwiftArray2D.cs`
- `src/SwiftCollections/Dimension/SwiftArray3D.cs`
- `src/SwiftCollections/Dimension/Default/SwiftBoolArray2D.cs`
- `src/SwiftCollections/Dimension/Default/SwiftShortArray2D.cs`
- `src/SwiftCollections/Query/Shared/QueryKeyIndexMap.cs`

Tasks:

- [x] Add data-driven non-generic `ICollection.CopyTo` and `IDictionary` adapter
      tests for wrong key type, wrong value type, wrong destination array type,
      `DictionaryEntry[]`, `object[]`, and insufficient space.
- [x] Cover `SwiftBucket<T>` and `SwiftStack<T>` non-generic copy edge cases
      without duplicating assertions already present in generic copy tests.
- [x] Add default constructor and non-generic enumerator tests for
      `SwiftArray3D<T>`, `SwiftBoolArray2D`, and `SwiftShortArray2D`.
- [x] Cover `QueryKeyIndexMap<T>.Capacity` through a public query structure or a
      shared infrastructure test rather than reflection.
- [x] Consolidate near-identical adapter tests with `[Theory]` or local helper
      methods when the failure contract is the same.

Exit criteria:

- [x] Adapter tests assert exact exception types and preserve current exception
      contracts.
- [x] No public API behavior changes unless explicitly called out as a breaking
      change.

### Phase 3: Probe, Tombstone, And Set Algebra Branches

Focus files:

- `src/SwiftCollections/Collection/SwiftDictionary.cs`
- `src/SwiftCollections/Collection/SwiftHashSet.cs`
- `src/SwiftCollections/Collection/SwiftPackedSet.cs`
- `src/SwiftCollections/Collection/SwiftSparseSet.cs`
- `src/SwiftCollections/Collection/SwiftSparseMap.cs`
- `src/SwiftCollections/Collection/SwiftSortedList.cs`

Tasks:

- [x] Extend collision fixtures to cover tombstone reuse, probe misses through
      deleted entries, comparer-switch no-op paths, and randomized comparer
      escalation where reachable.
- [x] Audit the uncovered probe-limit resize branches in dictionary/hash-set
      insert. If the load threshold makes them unreachable in valid states,
      remove or restructure instead of building brittle tests.
- [x] Add set algebra tests for empty/self/duplicate/foreign-enumerable branches
      across hash set, packed set, and sparse set.
- [x] Cover sorted-list insert/remove branch combinations: insert at tail,
      insert in middle with shift, remove last, remove middle, clear
      already-empty, fast-clear already-empty, state setter same-capacity and
      resize paths.
- [x] Keep tests deterministic and small; do not add giant loops unless they
      prove a specific probe invariant that smaller collision data cannot.

Exit criteria:

- [x] High-complexity probe methods stay direct and auditable.
- [x] Any intentionally retained high-complexity uncovered branch is documented
      in `docs/complexity-exceptions.md` with rationale.

### Phase 4: Query Structure Branch Matrix

Focus files:

- `src/SwiftCollections/Query/Shared/Volume/BoundVolume.cs`
- `src/SwiftCollections.FixedMathSharp/Query/Shared/Volume/FixedBoundVolume.cs`
- `src/SwiftCollections/Query/Octree/SwiftOctree.BoundVolume.cs`
- `src/SwiftCollections.FixedMathSharp/Query/Octree/SwiftFixedOctree.cs`
- `src/SwiftCollections/Query/SpatialHash/SwiftSpatialHash.cs`
- `src/SwiftCollections/Query/BoundingVolume/SwiftBVH.cs`

Tasks:

- [x] Add per-axis containment tests for numerics and fixed octree partitioners:
      min-x, max-x, min-y, max-y, min-z, max-z, straddling center plane, and
      fully contained.
- [x] Add `Equals(object)` wrong-type/null/same-value tests for `BoundVolume`
      and `FixedBoundVolume`.
- [x] Cover spatial-hash update that stays in the same cell set, update that
      moves cells, remove from head/middle/tail cell lists, duplicate
      suppression stamp reuse, and empty query result paths.
- [x] Cover BVH resize diagnostics and unallocated-node query guard only through
      public API behavior.
- [x] Reuse `DeterministicBoundVolumeDataset` for query shapes where possible.

Exit criteria:

- [x] Both `SwiftCollections.Query` and `SwiftCollections.FixedMathSharp` report
      100% branch coverage.
- [x] Query all-hit APIs continue writing into caller-owned collections with
      deterministic duplicate-safe results.

### Phase 5: Tail Utility, Equality, Pool, And Diagnostics Gaps

Focus files:

- `src/SwiftCollections/EqualityComparer/SwiftStringEqualityComparer.cs`
- `src/SwiftCollections/EqualityComparer/SwiftDeterministicStringEqualityComparer.cs`
- `src/SwiftCollections/Utility/SwiftHashTools.cs`
- `src/SwiftCollections/Utility/SwiftThrowHelper.cs`
- `src/SwiftCollections/Pool/SwiftPooledObject.cs`
- `src/SwiftCollections/Pool/Default/SwiftCollectionPool.cs`
- relevant pool wrapper tests

Tasks:

- [x] Cover string comparer `Equals(object, object)` branches for same instance,
      both null, one null, wrong type, equal strings, and unequal strings.
- [x] Cover `SwiftHashTools.MurmurHash3` remainder/tail branches with
      deterministic strings of lengths 0 through 7 and a long string.
- [x] Cover `SwiftThrowHelper.ThrowIfNullAndNullsAreIllegal` with nullable and
      non-nullable value scenarios.
- [x] Make pool no-op tests explicit with `Record.Exception` and state checks.
- [x] Cover pooled-object double-dispose and disposed-rent/release paths without
      over-mocking.

Exit criteria:

- [x] Tail files report 100% line and branch coverage.
- [x] No new tests only assert that an object is non-null when stronger state or
      behavior can be checked.

### Phase 6: Final Verification And Documentation Sweep

Tasks:

- [x] Run `dotnet build SwiftCollections.slnx -c Debug`.
- [x] Run both Debug coverage commands with
      `tests/SwiftCollections.Tests/coverlet.runsettings`.
- [x] Generate a combined ReportGenerator report and confirm 100.0% line and
      100.0% branch coverage.
- [x] Recalculate method CRAP scores and confirm no method exceeds 30.
- [x] Run
      `dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build`.
- [x] Run
      `dotnet test tests/SwiftCollections.FixedMathSharp.Tests/SwiftCollections.FixedMathSharp.Tests.csproj -c Debug --no-build`.
- [x] For release-sensitive changes, run
      `dotnet build SwiftCollections.slnx -c Release`,
      `dotnet test SwiftCollections.slnx -c Release --no-build`,
      `dotnet build SwiftCollections.slnx -c ReleaseLean`, and
      `dotnet test SwiftCollections.slnx -c ReleaseLean --no-build`. Not run for
      this pass because no package variant, target-framework, serialization
      shape, or release workflow behavior changed.
- [x] Update `docs/complexity-exceptions.md` with the final coverage/CRAP report
      path and any changed exception entries.
- [x] Update README or `docs/OVERVIEW.md` only if public API, package shape, or
      coverage workflow changes. No README/overview update needed.
- [x] Run `git diff --check`.

Exit criteria:

- [x] Combined coverage is 100.0% line and 100.0% branch across
      `SwiftCollections` and `SwiftCollections.FixedMathSharp`.
- [x] Standard and lean package behavior remains aligned.
- [x] The final diff contains no generated coverage, build, package, or
      benchmark output.

## Implementation Rules

- Write tests before changing production behavior unless the phase is removing
  proven-dead code.
- Prefer behavior-level tests over reflection or invalid internal state
  construction.
- Keep hot paths free of LINQ, delegates, closures, iterator allocations, and
  unnecessary indirection.
- If coverage can only be reached by contorting a test around impossible state,
  treat that as a code-design finding.
- Keep test data deterministic. Seed random inputs or replace them with named
  datasets.
- Combine tests only when the combined test still gives clear failure
  diagnostics.
- Do not chase 100% by weakening assertions, broadening exception catches, or
  adding coverage-only calls with no behavioral check.
