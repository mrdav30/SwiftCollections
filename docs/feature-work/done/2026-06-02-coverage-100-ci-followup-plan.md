# CI Coverage 100% Follow-Up Plan

> Archived completion record from 2026-06-02. The technology, source paths, line
> coordinates and intermediate gaps below describe that checkpoint. The original
> source revision and exact SDK patch were not recorded; these totals are
> historical evidence, not current release validation. Use
> [CONTRIBUTING](../../../CONTRIBUTING.md#local-validation) for current
> commands.

**Status:** Done

**Goal:** Bring the `coverage.yml`-shaped core coverage report to 100% line
coverage and 100% branch coverage from a clean worktree.

**Architecture:** Treat the GitHub Pages coverage job as the contract: Release
build, core test project, Coverlet collector, ReportGenerator summary. Add
behavior-focused tests for reachable public paths and remove or simplify truly
dead defensive code only after proving the invariant.

**Tech Stack:** .NET 8, xUnit v3, Coverlet collector,
`tests/SwiftCollections.Tests/coverlet.runsettings`, ReportGenerator,
`SwiftCollections.Tests` in Release configuration.

---

## Baseline

The baseline was a clean-worktree Release core run using the Coverlet collector
and ReportGenerator. Its recorded result is:

| Metric               |           Current |                    Remaining |
| -------------------- | ----------------: | ---------------------------: |
| Line coverage        |             99.9% |            3 uncovered lines |
| Branch coverage      | 98.8% (2288/2314) | 26 uncovered branch outcomes |
| Method coverage      |              100% |          0 uncovered methods |
| Full method coverage |             99.7% |  3 partially covered methods |

## Final Result

The final Release core result after Phase 3 is recorded below. ReportGenerator
produced summary/Cobertura output; a coverage/complexity scan found no methods
over the recorded CRAP threshold of 30. No private tool installation or saved
report directory is required to interpret these results.

| Metric               |             Final |                   Remaining |
| -------------------- | ----------------: | --------------------------: |
| Line coverage        |              100% |           0 uncovered lines |
| Branch coverage      |  100% (2306/2306) | 0 uncovered branch outcomes |
| Method coverage      |  100% (1218/1218) |         0 uncovered methods |
| Full method coverage |  100% (1218/1218) | 0 partially covered methods |
| CRAP risk            | 0 methods over 30 |           0 flagged methods |

Cobertura gap scan result: `NO_REMAINING_GAPS`.

Combined core plus filtered FixedMathSharp coverage: 100% line coverage and 100%
branch coverage (`2370/2370` branches), with `NO_REMAINING_GAPS` and
`TOTAL_METHODS:1224`, `FLAGGED_METHODS:0` from the CRAP scan.

Runsettings resolution note: the empty Cobertura report was reproduced with the
old minimal runsettings when passed explicitly. Updating
`tests/SwiftCollections.Tests/coverlet.runsettings` to include the production
assemblies, exclude test assemblies and generated sources, and configure
standard generated-code attributes makes explicit settings work in the
`coverage.yml` shape. ReportGenerator also needs generated-file `-filefilters`
so SourceLink-style generated filenames do not produce missing-file warnings.

## Rechecking the current checkout

The repository now uses per-assembly settings. Recreate current reports with
fresh results from each test project; do not combine unrelated earlier runs. The
full Standard/Lean and strict coverage-gate commands live in
[CONTRIBUTING](../../../CONTRIBUTING.md#local-validation).

```bash
dotnet build SwiftCollections.slnx -c Release
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Release --no-build --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.Tests/coverlet.runsettings
dotnet test tests/SwiftCollections.FixedMathSharp.Tests/SwiftCollections.FixedMathSharp.Tests.csproj -c Release --no-build --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.FixedMathSharp.Tests/coverlet.runsettings
```

## Historical gap worklist

Line gaps:

- `src/SwiftCollections/Collection/SwiftSparseSet.cs:720` - non-generic
  `ICollection.CopyTo` invalid array type.
- `src/SwiftCollections/Query/Octree/SwiftOctree.cs:398` - missing-entry scan
  fall-through in private node removal.
- `src/SwiftCollections/Query/SpatialHash/SwiftSpatialHash.cs:357` -
  missing-entry scan fall-through in private cell removal.

Branch-only gaps:

- Collection no-op/trim branches:
  - `SwiftBucket.Clear`: `src/SwiftCollections/Collection/SwiftBucket.cs:387`
  - `SwiftDictionary.TrimExcess`:
    `src/SwiftCollections/Collection/SwiftDictionary.cs:695`
  - `SwiftHashSet.TrimExcess`:
    `src/SwiftCollections/Collection/SwiftHashSet.cs:536`
  - `SwiftQueue.TrimExcessCapacity`:
    `src/SwiftCollections/Collection/SwiftQueue.cs:568`
  - `SwiftQueue.CopyTo(T[])`:
    `src/SwiftCollections/Collection/SwiftQueue.cs:673`
  - `SwiftSparseSet.Clear`:
    `src/SwiftCollections/Collection/SwiftSparseSet.cs:332`
  - `SwiftSparseSet.ExceptWith`:
    `src/SwiftCollections/Collection/SwiftSparseSet.cs:354`
  - `SwiftSparseSet.TrimDenseStorage`:
    `src/SwiftCollections/Collection/SwiftSparseSet.cs:642`
  - `SwiftSparseSet.TrimSparseLookup`:
    `src/SwiftCollections/Collection/SwiftSparseSet.cs:659`
- Query branches:
  - `SwiftBVH.UpdateEntryBounds`:
    `src/SwiftCollections/Query/BoundingVolume/SwiftBVH.cs:269`
  - `SwiftBVH.Clear`:
    `src/SwiftCollections/Query/BoundingVolume/SwiftBVH.cs:521`
  - `SwiftOctree.RemoveEntryFromNode`:
    `src/SwiftCollections/Query/Octree/SwiftOctree.cs:389`
  - `SwiftSpatialHash.RemoveEntryIndex`:
    `src/SwiftCollections/Query/SpatialHash/SwiftSpatialHash.cs:349`
- Pool branches:
  - `SwiftArrayPool.Clear`:
    `src/SwiftCollections/Pool/Default/SwiftArrayPool.cs:144`
  - `SwiftArrayPool.Dispose`:
    `src/SwiftCollections/Pool/Default/SwiftArrayPool.cs:178`
  - `SwiftDictionaryPool.Release`:
    `src/SwiftCollections/Pool/Default/SwiftDictionaryPool.cs:76`
  - `SwiftDictionaryPool.Clear`:
    `src/SwiftCollections/Pool/Default/SwiftDictionaryPool.cs:86`
  - `SwiftHashSetPool.Release`:
    `src/SwiftCollections/Pool/Default/SwiftHashSetPool.cs:72`
  - `SwiftHashSetPool.Clear`:
    `src/SwiftCollections/Pool/Default/SwiftHashSetPool.cs:83`
  - `SwiftListPool.Release`:
    `src/SwiftCollections/Pool/Default/SwiftListPool.cs:71`
  - `SwiftListPool.Clear`:
    `src/SwiftCollections/Pool/Default/SwiftListPool.cs:83`
  - `SwiftPackedSetPool.Release`:
    `src/SwiftCollections/Pool/Default/SwiftPackedSetPool.cs:69`
  - `SwiftPackedSetPool.Clear`:
    `src/SwiftCollections/Pool/Default/SwiftPackedSetPool.cs:79`
  - `SwiftSparseMapPool.Release`:
    `src/SwiftCollections/Pool/Default/SwiftSparseMapPool.cs:98`
  - `SwiftStackPool.Release`:
    `src/SwiftCollections/Pool/Default/SwiftStackPool.cs:68`
  - `SwiftStackPool.Clear`:
    `src/SwiftCollections/Pool/Default/SwiftStackPool.cs:78`

## Phases

### Phase 0: Align Coverage Contract

- [x] Test explicit
      `--settings tests/SwiftCollections.Tests/coverlet.runsettings` against the
      Release core workflow shape.
- [x] Confirm the old minimal settings produced an empty report when passed
      explicitly.
- [x] Update `.github/workflows/coverage.yml` to pass the fixed runsettings file
      explicitly and filter generated files during ReportGenerator output.

Exit criteria:

- [x] Remaining gaps are production source gaps rather than generated-source
      drift.
- [x] Explicit runsettings now produce a non-empty 100% line / 100% branch core
      report.

### Phase 1: Public No-Op And Adapter Paths

- [x] Add focused tests for empty/no-op collection methods: bucket clear, sparse
      set clear/except/trim, queue trim/copy, dictionary/hash-set trim.
- [x] Preserve the existing invalid-array `ICollection.CopyTo` coverage for
      `SwiftSparseSet` and replace the unconditional throw-helper call with a
      direct `ArgumentException`.
- [x] Prefer theory/helper consolidation where the same no-op release/clear
      contract is repeated.

Exit criteria:

- [x] No hollow no-op tests: each test asserts state, exception contract, or
      reuse behavior.
- [x] Collection branch gaps are closed.

### Phase 2: Query Invariants

- [x] Cover public query behavior for missing BVH update and empty BVH clear.
- [x] Prove octree/spatial-hash private removal fall-through paths are
      unreachable under live-entry invariants.
- [x] Restructure private helpers to remove the dead fall-through branch without
      changing public semantics.

Exit criteria:

- [x] Query tests use public APIs and deterministic shapes.
- [x] No tests manufacture impossible private states solely for coverage.

### Phase 3: Pool Branch Matrix

- [x] Cover null release no-ops for
      dictionary/hash-set/list/packed-set/sparse-map/stack pools.
- [x] Cover clear-after-dispose and dispose-twice behavior where current public
      contracts allow no-op behavior.
- [x] Keep assertions focused on no throw, disposed exception, object reuse, and
      cleared state.

Exit criteria:

- [x] Pool branch gaps are closed without weakening disposal contracts.

### Phase 4: Verification And Docs

- [x] Re-run workflow-shaped Release core coverage until line and branch
      coverage are both 100%.
- [x] Re-run Debug build and core/FixedMathSharp tests.
- [x] Run `git diff --check`.
- [x] Update this plan and `docs/complexity-exceptions.md` with final coverage
      outcomes.

Exit criteria:

- [x] ReportGenerator summary shows 100% line coverage and 100% branch coverage
      for the core workflow report.
- [x] No remaining Cobertura uncovered line or partial branch gaps.

Verification evidence:

- `dotnet build SwiftCollections.slnx -c Debug`: passed, 0 warnings, 0 errors.
- `dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build`:
  passed, 1026 tests.
- `dotnet test tests/SwiftCollections.FixedMathSharp.Tests/SwiftCollections.FixedMathSharp.Tests.csproj -c Debug --no-build`:
  passed, 27 tests.
- `git diff --check`: clean.
