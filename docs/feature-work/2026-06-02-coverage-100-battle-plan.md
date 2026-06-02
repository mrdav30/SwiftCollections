# 100% Coverage Battle Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use coverage-analysis before updating this plan from fresh report data. Use test-driven-development before adding or changing behavior tests, and verification-before-completion before claiming a phase is complete.

**Goal:** Bring SwiftCollections to 100% line coverage and 100% branch coverage without papering over real behavior gaps or adding hollow tests.

**Architecture:** Treat coverage misses as design feedback. Add focused behavior tests for public/API-reachable branches, remove or simplify truly unreachable defensive code, and keep hot-path collection/query code direct and benchmarkable.

**Tech Stack:** .NET 8 test projects, xUnit v3, FluentAssertions, Coverlet collector with `tests/SwiftCollections.Tests/coverlet.runsettings`, ReportGenerator, SwiftCollections standard and FixedMathSharp assemblies.

---

## Current Baseline

Fresh coverage was generated from `Debug` builds with the shared Coverlet runsettings.

Commands run:

```bash
dotnet build SwiftCollections.slnx -c Debug
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.Tests/coverlet.runsettings
dotnet test tests/SwiftCollections.FixedMathSharp.Tests/SwiftCollections.FixedMathSharp.Tests.csproj -c Debug --no-build --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.Tests/coverlet.runsettings
/tmp/codex-reportgenerator/reportgenerator -reports:"tests/SwiftCollections.Tests/TestResults/9ffd4658-dfcc-46cc-b645-4b97fa01ffd5/coverage.cobertura.xml;tests/SwiftCollections.FixedMathSharp.Tests/TestResults/63691ba8-1223-44cf-b2e7-59bdde4ed71a/coverage.cobertura.xml" -targetdir:"tests/TestResults/coverage-analysis/2026-06-02-full/reports" -reporttypes:"Html;TextSummary;JsonSummary;Cobertura" -assemblyfilters:"+SwiftCollections;+SwiftCollections.FixedMathSharp"
```

Artifacts:

- Core coverage input: `tests/SwiftCollections.Tests/TestResults/9ffd4658-dfcc-46cc-b645-4b97fa01ffd5/coverage.cobertura.xml`
- FixedMathSharp coverage input: `tests/SwiftCollections.FixedMathSharp.Tests/TestResults/63691ba8-1223-44cf-b2e7-59bdde4ed71a/coverage.cobertura.xml`
- Combined summary: `tests/TestResults/coverage-analysis/2026-06-02-full/reports/Summary.txt`
- Combined Cobertura: `tests/TestResults/coverage-analysis/2026-06-02-full/reports/Cobertura.xml`

Summary:

| Metric | Current | Remaining |
| --- | ---: | ---: |
| Line coverage | 98.4% | 109 uncovered lines |
| Branch coverage | 94.7% | 130 uncovered branches |
| Method coverage | 99.2% | 9 uncovered methods |
| Full method coverage | 94.4% | 69 not fully covered methods |
| CRAP risk | 0 methods over 30 | Keep this true |

Top CRAP methods remain below the register threshold:

| Method | CRAP | Complexity | Line coverage |
| --- | ---: | ---: | ---: |
| `SwiftHashSet<T>.InsertIfNotExists(T)` | 24.18 | 24 | 93.2% |
| `SwiftDictionary<TKey, TValue>.InsertIfNotExist(TKey, TValue)` | 24.17 | 24 | 93.3% |
| `SwiftBucket<T>.IndexOf(T)` | 18.02 | 18 | 96.0% |
| `SwiftDictionary<TKey, TValue>.Remove(TKey)` | 16.00 | 16 | 100.0% |
| `SwiftHashSet<T>.FindEntry(T)` | 14.04 | 14 | 94.1% |
| `SwiftBVH<TKey, TVolume>.UpdateEntryBounds(TKey, TVolume)` | 14.01 | 14 | 96.3% |

## Gap Worklist

The largest gap clusters are below. Use this table to choose phase order and to avoid scattering one-off tests across the suite.

| Source file | Uncovered lines | Missing branches | Primary theme |
| --- | ---: | ---: | --- |
| `src/SwiftCollections/Collection/SwiftDictionary.cs` | 25 | 11 | non-generic adapters, collision/probe tails, comparer switches |
| `src/SwiftCollections/Query/BoundingVolume/SwiftBVH.cs` | 16 | 13 | remove/update invariants, single-child defensive branches |
| `src/SwiftCollections/Collection/SwiftHashSet.cs` | 14 | 10 | probe tails, set algebra, comparer switches |
| `src/SwiftCollections/Query/Octree/SwiftOctree.cs` | 7 | 17 | merge/collapse traversal branches and internal invariants |
| `src/SwiftCollections/Collection/SwiftSortedList.cs` | 6 | 8 | insert/remove/state/clear edge cases |
| `src/SwiftCollections/Collection/SwiftBucket.cs` | 6 | 7 | copy adapters, sparse peak/freelist branches |
| `src/SwiftCollections/Collection/SwiftList.cs` | 5 | 4 | range insert/add and formatting branches |
| `src/SwiftCollections/Query/SpatialHash/SwiftSpatialHash.cs` | 3 | 6 | remove/update/query stamp branches |
| `src/SwiftCollections/Dimension/SwiftArray3D.cs` | 5 | 3 | default construction, shift normalization, enumerator |
| `src/SwiftCollections/Collection/SwiftQueue.cs` | 2 | 6 | range/state/to-array/enumerator branches |
| `src/SwiftCollections/Collection/SwiftStack.cs` | 3 | 4 | copy/state/trim/enumerator branches |
| `src/SwiftCollections/Collection/SwiftGenerationalBucket.cs` | 1 | 6 | handle/ref/remove/resize branches |
| `src/SwiftCollections/Collection/SwiftSparseSet.cs` | 0 | 6 | branch-only set operations and non-generic copy |
| `src/SwiftCollections/Query/Octree/SwiftOctree.BoundVolume.cs` | 0 | 5 | per-axis containment branch matrix |
| `src/SwiftCollections.FixedMathSharp/Query/Octree/SwiftFixedOctree.cs` | 0 | 5 | fixed-point per-axis containment branch matrix |

## Dead-Code And Optimization Candidates

Do not write tests that manufacture impossible private states. Prove reachability first. If a branch is only reachable through corrupted internals, remove or simplify it.

- `SwiftBVH<TKey, TVolume>.RemoveRootLeaf(int)` is currently uncovered. Public `Remove` handles `nodeIndex == RootNodeIndex && _leafCount == 1` by calling `Clear()` before `RemoveFromTree`, and key lookup returns only leaf nodes. Audit whether `RemoveFromTree` can ever receive a root leaf. If not, remove `RemoveRootLeaf` and the `parentIndex == -1` branch instead of testing a dead path.
- `SwiftBVH<TKey, TVolume>.GetCombinedBounds(...)` has uncovered single-child paths. The nearby comment says internal nodes have exactly two children after removal. Confirm that invariant across insert, promote, update, and refresh. If it is true, simplify to the two-child path and keep the invariant explicit.
- `SwiftBVH<TKey, TVolume>.GetInsertionCost`, `GetNodeOrDefault`, and `GetSubtreeSize` have branch misses around sentinel/default nodes. Audit these alongside the two-child invariant; avoid preserving sentinel branches that a valid tree can never hit.
- `SwiftOctree<TKey, TVolume>` uses `node.Children?.Length` inside flows that already require `HasChildren`. Replace these with local non-null child arrays where the invariant is already established. This should reduce branch count and remove unnecessary null-conditional checks from traversal/merge hot paths.
- `SwiftOctree<TKey, TVolume>.RelocateEntry` checks `_entries[entryIndex].IsAllocated` after `FindEntryIndex` has returned an allocated entry. Confirm via `QueryKeyIndexMap` semantics; remove the branch if unreachable.
- `SwiftOctree<TKey, TVolume>.RemoveEntryFromNode` throws when an entry is missing from its owning node. Treat this as an invariant guard, not normal public behavior. Keep only if tests can reach it through a supported state-restore or public mutation sequence.

## Test-Quality Findings

No broad swallowed exceptions or always-true assertions were found in the quick scan. There are a few test-quality items to clean up while hardening coverage:

- `tests/SwiftCollections.Tests/Collection/SwiftDictionary.Tests.cs:1049` has `SwiftDictionary_CanHandle_LargeInserts`, which inserts 100,000 random keys and returns without asserting. Convert it to deterministic stress coverage with count and lookup assertions, reduce the size if it does not need 100,000 inserts, or move it to benchmarks if it only measures throughput.
- `tests/SwiftCollections.Tests/Pool/SwiftArrayPool.Tests.cs:87` and `tests/SwiftCollections.Tests/Pool/SwiftQueuePool.Tests.cs:54` are legitimate no-throw checks but should assert via `Record.Exception(...)` or combine with state checks so they are not hollow.
- `tests/SwiftCollections.Tests/Query/SwiftBVH.Numerics.Tests.cs:317` and `tests/SwiftCollections.Tests/Dimension/SwiftArray3D.Tests.cs:183` use unseeded `Random`. Seed them or move the randomized cases to deterministic datasets.
- Existing tests already use good localized fixtures such as `CollisionStringFactory`, `SelectiveIntHashComparer`, and deterministic query datasets. Prefer extending those helpers over introducing ad hoc random fixtures.

## Phases

### Phase 0: Coverage Contract And Tooling

- [ ] Keep `tests/SwiftCollections.Tests/coverlet.runsettings` as the shared coverage settings file for both test projects.
- [ ] Do not exclude production source files to reach 100%; only generated MemoryPack files should stay excluded.
- [ ] Add a repeatable local coverage command or script only if it avoids command drift without adding CI complexity.
- [ ] Preserve the combined report output under `tests/TestResults/coverage-analysis/<run>/reports` for each major phase.
- [ ] After each phase, record line/branch deltas in this document.

Exit criteria:

- [ ] Baseline commands reproduce 98.4% line and 94.7% branch coverage or better.
- [ ] CRAP scan still reports 0 methods over 30.

### Phase 1: Prune Unreachable Internal Branches

Focus files:

- `src/SwiftCollections/Query/BoundingVolume/SwiftBVH.cs`
- `src/SwiftCollections/Query/Octree/SwiftOctree.cs`

Tasks:

- [ ] Prove or disprove the BVH invariant that every non-empty internal node has two allocated children and that public removal never sends a root leaf into `RemoveFromTree`.
- [ ] Remove dead BVH root-leaf/single-child/sentinel branches when the invariant holds.
- [ ] Add or adjust public behavior tests around single-item removal, two-item removal, sibling promotion, update propagation, and query results after removal.
- [ ] Replace octree null-conditional child loops with non-null locals in paths guarded by `HasChildren`.
- [ ] Add focused octree tests for merge-not-needed, merge-blocked-by-grandchild, merge-blocked-by capacity, and collapse success.
- [ ] Re-run coverage and update this plan with removed branch totals.

Exit criteria:

- [ ] No tests rely on constructing invalid BVH or octree internals.
- [ ] Query structures keep deterministic result behavior and no hidden allocations are introduced.
- [ ] `docs/complexity-exceptions.md` is updated if any listed method complexity or coverage changes materially.

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

- [ ] Add data-driven non-generic `ICollection.CopyTo` and `IDictionary` adapter tests for wrong key type, wrong value type, wrong destination array type, `DictionaryEntry[]`, `object[]`, and insufficient space.
- [ ] Cover `SwiftBucket<T>` and `SwiftStack<T>` non-generic copy edge cases without duplicating assertions already present in generic copy tests.
- [ ] Add default constructor and non-generic enumerator tests for `SwiftArray3D<T>`, `SwiftBoolArray2D`, and `SwiftShortArray2D`.
- [ ] Cover `QueryKeyIndexMap<T>.Capacity` through a public query structure or a shared infrastructure test rather than reflection.
- [ ] Consolidate near-identical adapter tests with `[Theory]` or local helper methods when the failure contract is the same.

Exit criteria:

- [ ] Adapter tests assert exact exception types and preserve current exception contracts.
- [ ] No public API behavior changes unless explicitly called out as a breaking change.

### Phase 3: Probe, Tombstone, And Set Algebra Branches

Focus files:

- `src/SwiftCollections/Collection/SwiftDictionary.cs`
- `src/SwiftCollections/Collection/SwiftHashSet.cs`
- `src/SwiftCollections/Collection/SwiftPackedSet.cs`
- `src/SwiftCollections/Collection/SwiftSparseSet.cs`
- `src/SwiftCollections/Collection/SwiftSparseMap.cs`
- `src/SwiftCollections/Collection/SwiftSortedList.cs`

Tasks:

- [ ] Extend collision fixtures to cover tombstone reuse, probe misses through deleted entries, comparer-switch no-op paths, and randomized comparer escalation where reachable.
- [ ] Audit the uncovered probe-limit resize branches in dictionary/hash-set insert. If the load threshold makes them unreachable in valid states, remove or restructure instead of building brittle tests.
- [ ] Add set algebra tests for empty/self/duplicate/foreign-enumerable branches across hash set, packed set, and sparse set.
- [ ] Cover sorted-list insert/remove branch combinations: insert at tail, insert in middle with shift, remove last, remove middle, clear already-empty, fast-clear already-empty, state setter same-capacity and resize paths.
- [ ] Keep tests deterministic and small; do not add giant loops unless they prove a specific probe invariant that smaller collision data cannot.

Exit criteria:

- [ ] High-complexity probe methods stay direct and auditable.
- [ ] Any intentionally retained high-complexity uncovered branch is documented in `docs/complexity-exceptions.md` with rationale.

### Phase 4: Query Structure Branch Matrix

Focus files:

- `src/SwiftCollections/Query/Shared/Volume/BoundVolume.cs`
- `src/SwiftCollections.FixedMathSharp/Query/Shared/Volume/FixedBoundVolume.cs`
- `src/SwiftCollections/Query/Octree/SwiftOctree.BoundVolume.cs`
- `src/SwiftCollections.FixedMathSharp/Query/Octree/SwiftFixedOctree.cs`
- `src/SwiftCollections/Query/SpatialHash/SwiftSpatialHash.cs`
- `src/SwiftCollections/Query/BoundingVolume/SwiftBVH.cs`

Tasks:

- [ ] Add per-axis containment tests for numerics and fixed octree partitioners: min-x, max-x, min-y, max-y, min-z, max-z, straddling center plane, and fully contained.
- [ ] Add `Equals(object)` wrong-type/null/same-value tests for `BoundVolume` and `FixedBoundVolume`.
- [ ] Cover spatial-hash update that stays in the same cell set, update that moves cells, remove from head/middle/tail cell lists, duplicate suppression stamp reuse, and empty query result paths.
- [ ] Cover BVH resize diagnostics and unallocated-node query guard only through public API behavior.
- [ ] Reuse `DeterministicBoundVolumeDataset` for query shapes where possible.

Exit criteria:

- [ ] Both `SwiftCollections.Query` and `SwiftCollections.FixedMathSharp` report 100% branch coverage.
- [ ] Query all-hit APIs continue writing into caller-owned collections with deterministic duplicate-safe results.

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

- [ ] Cover string comparer `Equals(object, object)` branches for same instance, both null, one null, wrong type, equal strings, and unequal strings.
- [ ] Cover `SwiftHashTools.MurmurHash3` remainder/tail branches with deterministic strings of lengths 0 through 7 and a long string.
- [ ] Cover `SwiftThrowHelper.ThrowIfNullAndNullsAreIllegal` with nullable and non-nullable value scenarios.
- [ ] Make pool no-op tests explicit with `Record.Exception` and state checks.
- [ ] Cover pooled-object double-dispose and disposed-rent/release paths without over-mocking.

Exit criteria:

- [ ] Tail files report 100% line and branch coverage.
- [ ] No new tests only assert that an object is non-null when stronger state or behavior can be checked.

### Phase 6: Final Verification And Documentation Sweep

Tasks:

- [ ] Run `dotnet build SwiftCollections.slnx -c Debug`.
- [ ] Run both Debug coverage commands with `tests/SwiftCollections.Tests/coverlet.runsettings`.
- [ ] Generate a combined ReportGenerator report and confirm 100.0% line and 100.0% branch coverage.
- [ ] Recalculate method CRAP scores and confirm no method exceeds 30.
- [ ] Run `dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build`.
- [ ] Run `dotnet test tests/SwiftCollections.FixedMathSharp.Tests/SwiftCollections.FixedMathSharp.Tests.csproj -c Debug --no-build`.
- [ ] For release-sensitive changes, run `dotnet build SwiftCollections.slnx -c Release`, `dotnet test SwiftCollections.slnx -c Release --no-build`, `dotnet build SwiftCollections.slnx -c ReleaseLean`, and `dotnet test SwiftCollections.slnx -c ReleaseLean --no-build`.
- [ ] Update `docs/complexity-exceptions.md` with the final coverage/CRAP report path and any changed exception entries.
- [ ] Update README or `docs/OVERVIEW.md` only if public API, package shape, or coverage workflow changes.
- [ ] Run `git diff --check`.

Exit criteria:

- [ ] Combined coverage is 100.0% line and 100.0% branch across `SwiftCollections` and `SwiftCollections.FixedMathSharp`.
- [ ] Standard and lean package behavior remains aligned.
- [ ] The final diff contains no generated coverage, build, package, or benchmark output.

## Implementation Rules

- Write tests before changing production behavior unless the phase is removing proven-dead code.
- Prefer behavior-level tests over reflection or invalid internal state construction.
- Keep hot paths free of LINQ, delegates, closures, iterator allocations, and unnecessary indirection.
- If coverage can only be reached by contorting a test around impossible state, treat that as a code-design finding.
- Keep test data deterministic. Seed random inputs or replace them with named datasets.
- Combine tests only when the combined test still gives clear failure diagnostics.
- Do not chase 100% by weakening assertions, broadening exception catches, or adding coverage-only calls with no behavioral check.
