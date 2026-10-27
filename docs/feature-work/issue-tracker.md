# Issue Tracker

## Purpose

Track correctness, determinism, API, documentation, build, tooling and test
reliability concerns owned by SwiftCollections, including its FixedMathSharp
companion. Keep substantial implementation work in focused feature-work plans.

## Tracker Rules

- Issue IDs use `SC-Issue-NNN`. The next available ID is `SC-Issue-004`.
- Assign an ID at intake, retain it through resolution, and never reuse IDs.
  Check repository history before advancing or repairing the counter.
- Record discovery dates, status, priority, affected owners, reproducible
  evidence, impact and the next useful action. Distinguish confirmed defects
  from unverified concerns.
- Keep entries focused. Move them to `Resolved Issues` after the fix and
  verification are complete; preserve root cause and regression evidence.
- Use repository-relative links and portable commands. Ignored artifacts are
  supporting evidence; continuing an issue must not require their survival.
- Preserve reachable line, branch and method coverage. Do not conceal gaps
  with exclusions, weaker assertions or retries until a flaky result passes.
- Use `UseLocalLsfStack=true` for coordinated unreleased source validation;
  released packages remain the promotion/release gate. Validate Release and
  ReleaseLean and the affected core or companion assembly independently.
- Keep performance claims measured. This tracker does not replace benchmarks,
  implementation plans, tests or release notes.

## Active Issues

None.

## Resolved Issues

### SC-Issue-003 - Hash-set lookup boxes value keys in unoptimized builds

- **Status / priority:** Resolved 2026-10-26; Low, Debug allocation behavior.
- **Discovered:** During downstream static-surface Debug validation on source
  revision `5e0c4e0830342508c3fb87c2fe43cf7c30167522`; pre-existing in that revision.
- **Root cause / affected owner:**
  [`SwiftHashSet<T>.FindEntry`](../../src/SwiftCollections/Collection/SwiftHashSet.cs)
  tests `item == null` without a value-type nullability guard. The emitted
  generic `box T` allocates for integer keys under the Debug JIT. Optimized
  Release removes this allocation. Debug allocation assertions therefore cannot
  assume the optimized build's zero-allocation behavior, even after warming.
  All three lookup APIs (`Contains`, `TryGetValue`, and the indexer) share this
  method. The indexer also passed every key to the object-based
  `ChronicleGuard.ThrowIfKeyInvalid` helper, boxing successful value-key lookups
  independently of the null check.
- **Original evidence / reproduction:** Keep a `net8.0` console caller in Debug and
  reference the standard library build, including its MemoryPack dependency.
  Run this code first with the Debug library, then with the Release library:

  ```csharp
  using SwiftCollections;

  var set = new SwiftHashSet<int>();
  set.Add(1);
  int found = 0;
  for (int i = 0; i < 256; i++) if (set.Contains(1)) found++;
  long before = GC.GetAllocatedBytesForCurrentThread();
  for (int i = 0; i < 128; i++) if (set.Contains(1)) found++;
  long bytes = GC.GetAllocatedBytesForCurrentThread() - before;
  Console.WriteLine($"{bytes} B, {found} successful lookups");
  ```

  The same caller reports `3072 B` with Debug and `0 B` with Release: exactly
  24 B per Debug lookup, with identical results. IL inspection confirms the
  lookup's `box T`; no geometry or physics code is involved in this reproducer.
- **Fix:** Cache `default(T) is null` once per closed set type, matching
  `SwiftDictionary`'s existing nullability pattern, and short-circuit the lookup
  null comparison for non-nullable value types. Call the indexer's error helper
  only when the lookup misses. `GenericNullability<T>` now belongs to the private
  implementation of `Chronicler.Diagnostics`, so no shared helper or public API
  was added. Reference/nullable null handling, comparer/probe behavior, exception
  contracts and serialized state are preserved.
- **Regression evidence:** The existing
  [hash-set tests](../../tests/SwiftCollections.Tests/Collection/SwiftHashSet.Tests.cs)
  now measure each lookup API independently after warm-up, including hit/miss
  checks for `Contains` and `TryGetValue`, with 128 measured iterations. Before
  the fix, all three cases failed in Debug with 6,144 B each: two boxed lookups
  per iteration for `Contains`/`TryGetValue`, and two boxes per successful
  indexer lookup. Afterward all three allocate exactly 0 B. A nullable-integer
  regression checks both populated and null keys; existing reference-null,
  comparer and missing-indexer tests remain intact. Run the allocation regression
  in Debug to retain its unoptimized-library check; Release alone can eliminate
  the original boxing and would miss the defect.
- **Verification:** Windows x64, SDK 10.0.302 / runtime 10.0.10, with
  `UseLocalLsfStack=true`. Solution builds passed for Debug, Release and
  ReleaseLean, including both `netstandard2.1` and `net10.0` runtime targets.
  Full core suites passed 1,073 / 1,075 / 1,047 tests respectively; the companion
  passed 43 tests per profile. Raw Release core coverage was 5,251/5,251 lines,
  2,306/2,306 branches and 1,132/1,132 methods; Lean was 5,237/5,237 lines,
  2,306/2,306 branches and 1,125/1,125 methods. Independent companion captures
  covered all 121 lines, 62 branches and 35 methods in both profiles. No zero-hit
  line or method nodes remained. Independent review found no actionable issues.
  Builds retained the pre-existing benchmark-project NU1510 package warning.
- **Warmed performance:** The existing
  [integer set benchmark](../../tests/SwiftCollections.Benchmarks/Benchmarks/Set.Parity.Integer.Benchmarks.cs)
  performed 10,000 successful shuffled-key lookups per operation, with identical
  seed-42 inputs. Balanced baseline/fix/fix/baseline Release runs with tiered
  compilation disabled measured 40.81 / 38.69 / 38.74 / 41.05 microseconds per
  operation; all allocated 0 B. Each run used four warm-up iterations, ten
  measurement iterations and 8,192 invocations per iteration. The 252-byte native
  disassembly matched after normalizing addresses. These runs showed no warmed
  lookup regression; they do not establish a general speedup. The earlier
  tiered-JIT capture mixed compilation tiers and was retained as inconclusive.
- **Repeat verification:** Build the solution from the root with `-m:1` and
  `-p:UseLocalLsfStack=true` in Debug, Release and ReleaseLean. Run the full
  solution tests with the same configuration/property and `--no-build`. To
  reproduce only the allocation checks, run the core project in Debug with
  `--filter "FullyQualifiedName~Lookup_WithInt32Keys_DoesNotAllocateSteadyState"`.
  Collect Release/Lean coverage from each test project using its own
  `coverlet.runsettings` and `--collect:"XPlat Code Coverage"`. For the timed
  workload, set `UseLocalLsfStack=true` and `DOTNET_TieredCompilation=0` in the
  environment, and run `set-parity-integer` with
  `--filter "*SwiftHashSet_Contains(N: 10000)" --job short --iterationCount 10
  --warmupCount 4 --launchCount 1 --invocationCount 8192 --unrollFactor 1
  --disasm --disasmDepth 2` through the benchmark runner.
- **Scope / supporting evidence:** This is a SwiftCollections-only runtime/test
  fix; no dependency, CI or package-version changes were needed. Linux,
  released-package mode and the original .NET 8 caller were not rerun. Ignored
  TRX, raw coverage and before/after benchmark captures are under
  `artifacts/issue-003/`; the regressions and commands above are the durable
  evidence.

### SC-Issue-001 - Pool finalizer coverage depends on incidental GC

- **Status / priority:** Resolved 2026-10-07; Medium coverage reliability gap.
- **Discovered:** 2026-10-07 during upstream develop CI validation, at revision
  `3668bb4db9e36875f9d3e63e3b0e7af776f5613e`. Transferred from
  `GRV-Issue-091`; SwiftCollections owns the follow-up.
- **Affected owners:**
  [SwiftPackedSetPool](../../src/SwiftCollections/Pool/Default/SwiftPackedSetPool.cs)
  and [SwiftQueuePool](../../src/SwiftCollections/Pool/Default/SwiftQueuePool.cs),
  specifically their finalizers (lines 111 and 114 at discovery).
- **Root cause / reproduction:** Both finalizers call `Dispose()`, but existing
  disposal tests suppress finalization. Other tests abandon pool instances
  without deliberately collecting them or asserting finalizer effects. The
  original full local-stack Release capture missed both finalizers while Lean
  happened to cover them. A focused pre-fix Release capture reproduced the gap:
  all six packed-set/queue pool tests passed, with zero hits for both finalizers.
  Coverage therefore depended on incidental GC and suite ordering; no production
  cleanup or source-mode defect was demonstrated.
- **Fix:** Added one real-GC finalization regression per wrapper in the existing
  [packed-set pool tests](../../tests/SwiftCollections.Tests/Pool/SwiftPackedSetPool.Tests.cs)
  and [queue pool tests](../../tests/SwiftCollections.Tests/Pool/SwiftQueuePool.Tests.cs).
  Non-inlined factories return long weak references; explicit collection and a
  finalizer wait precede assertions that the backing pool is empty/disposed and
  the wrapper rejects rent/release. The
  [isolated xUnit collection](../../tests/SwiftCollections.Tests/Support/PoolFinalizationCollection.cs)
  prevents competing test collections from reclaiming the weak targets before
  inspection. Ordinary tests now explicitly dispose their pools. Production
  code, public APIs and coverage exclusions are unchanged.
- **Regression strength:** The nested lazy holder can independently dispose the
  backing pool, so merely expecting `ObjectDisposedException` can mask a missing
  wrapper finalizer. Assertions also require the wrapper's `ObjectName`.
  Temporarily replacing both wrapper finalizers with no-ops makes both new tests
  fail on that assertion; restoring the original finalizers makes them pass.
- **Verification:** Windows local-stack solution builds passed with zero warnings
  and errors in Release and ReleaseLean. Full core suites passed 1,109/1,081
  tests respectively; the companion passed all 43 tests per profile. Independent
  captures for both assemblies had exact 100% line, branch and method coverage.
  Running only the two new finalizer tests in fresh Release/Lean test processes
  also passed and covered both finalizer lines/methods. Independent review found
  no actionable issues. Released-package and hosted Linux validation were not
  run for this test-only change.
- **Repeat verification:** From the root, build `SwiftCollections.slnx` with
  `-c Release` or `-c ReleaseLean`, `-p:UseLocalLsfStack=true` and `-m:1`.
  Run the core test project with the same configuration/property, `--no-build`,
  `--collect:"XPlat Code Coverage"` and its `coverlet.runsettings`. Add
  `--filter "FullyQualifiedName~Finalizer_ShouldClearAndDisposeUnreleasedPool"`
  to isolate the regressions. Use the companion's project and runsettings for
  its independent capture. Inspect both `Finalize` methods in the core XML.
- **Supporting evidence:** Ignored captures and the mutation TRX are under
  `artifacts/issue-001/`. The original Gravitas capture was under
  `artifacts/upstream-ci/validation-20261007T111424/`; the regression tests
  and portable commands above do not depend on retained artifacts.

### SC-Issue-002 - Collision probes stop before reaching stored entries

- **Status / priority:** Resolved 2026-10-07; High correctness defect.
- **Discovered:** 2026-10-07 in
  [run 37645072233](https://github.com/mrdav30/SwiftCollections/actions/runs/37645072233/job/112873606861),
  at revision `fb93b8a20078c921333cf212c49e796584b8d8aa`. Transferred from
  `GRV-Issue-092`.
- **Affected owners / impact:**
  [SwiftDictionary](../../src/SwiftCollections/Collection/SwiftDictionary.cs)
  and [SwiftHashSet](../../src/SwiftCollections/Collection/SwiftHashSet.cs).
  Windows Release failed `Add_RandomStringKeys_AddsElements`: a just-added key
  could not be retrieved. Restore/build succeeded; random test inputs exposed
  a production collection defect that also affected removal and trimmed tables.
- **Root cause / reproduction:** Lookup and removal used `_lastIndex`, the
  highest occupied bucket, to bound probe distance. In an eight-slot table,
  five distinct keys with hashes `[3, 3, 0, 0, 3]` occupy buckets at or below
  four, yet the last key needs five collisions to reach bucket two. Lookup
  stops early. Insertion's one-capacity limit also misses part of the
  cumulative-square sequence; trim and rehash can place entries beyond it.
- **Fix:** Insertion, lookup and removal now use the complete `2 * capacity`
  period for power-of-two tables, preserving probe order, tombstones, comparer
  behavior and storage layout. Unsigned doubling keeps the bound valid at large
  capacities. No new helper, allocation or serialization contract is introduced.
- **Regression evidence:** Eight deterministic cases failed before the fix and
  pass afterward: wrapped chains and fully trimmed collision tables at
  capacities 8, 16 and 32 in the existing
  [dictionary tests](../../tests/SwiftCollections.Tests/Collection/SwiftDictionary.Tests.cs)
  and [hash-set tests](../../tests/SwiftCollections.Tests/Collection/SwiftHashSet.Tests.cs).
  Unknown-count constructor cases also cover full-table growth and duplicates.
  Full Windows/native Linux Release/Lean suites and exact 100% core line,
  branch and method coverage passed. Independent review found no actionable
  issue. Those captures alone did not close SC-Issue-001; the deliberate
  finalizer regressions recorded above resolve its separate reliability gap.
- **Commit / hosted verification:** Fix committed in
  `1de0a343298bf85c8f638db5d9308e61228d00d0`.
  [Run 37654294859](https://github.com/mrdav30/SwiftCollections/actions/runs/37654294859)
  passed all Windows/Linux Release/Lean lanes. Package consumers require the
  upstream release; coordinated source consumers select an appropriate revision.
- **Downstream verification:** Local-stack full suites passed GridForge's 1,171
  tests per profile and Gravitas's 4,549/4,488 Release/Lean tests, including
  shared replay fixtures. The original ignored RCA captures and benchmark smoke
  results are in Gravitas at `artifacts/swift-ci-rca/`; the committed regressions
  are the durable reproduction.

## Issue Template

```markdown
### SC-Issue-NNN - Concise title

- **Status / priority:** Needs reproduction | Confirmed | Planned | Deferred
- **Discovered:** Date and source revision
- **Affected owners / impact:** Source owner and observable risk
- **Evidence / reproduction:** Focused failing case or portable command
- **Next action:** Focused investigation or fix and required verification
```

