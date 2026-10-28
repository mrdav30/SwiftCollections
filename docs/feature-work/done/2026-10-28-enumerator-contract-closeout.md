# SC-Issue-004 - Enumerator position contract

Resolved in source on 2026-10-28; this record does not claim a package release.

## Discovery and reproduction

The downstream investigation used unchanged SwiftCollections revision
`813a182bd089a82b80f7802baa1ed2c5099e4c07`, a net10.0 Release local-stack
binary with SHA256
`A1BD269FDBD68B49EA72E9A05E91257BA2776D227799701189500C677F57C75F`.
Non-generic list iteration returned a default value before its first move and
threw on its final valid item:

```csharp
using System.Collections;
using SwiftCollections;

var values = new SwiftList<int> { 7 };
IEnumerator iterator = ((IEnumerable)values).GetEnumerator();
object before = iterator.Current; // Incorrectly returned 0.
bool moved = iterator.MoveNext(); // True.
object last = iterator.Current;   // Incorrectly threw InvalidOperationException.
```

The first access must throw; after a successful move, both generic and
non-generic Current must return 7. A failed move must invalidate non-generic
Current even when the final successful move returned the last item.

## Root causes and scope

Each collection already owns enough cursor state to enforce its position
contract. Fixes stay in those owners; no fields, public APIs, serialized state,
dependencies, or downstream workarounds were added.

| Owner | Verified defect | Correction |
| --- | --- | --- |
| SwiftList | Next-item cursor rejected the last item, accepted the initial state, and did not distinguish completion from the last successful move. | Reject zero and positions beyond count; mark completion as count plus one. |
| SwiftQueue | The same next-item off-by-one guard rejected the last item and accepted the initial state of nonempty queues. | Reject zero and positions beyond count. |
| SwiftSortedList | Initial zero cursor was treated as valid. | Reject zero as well as completed positions. |
| SwiftBucket | Compared a current storage slot with live count, rejecting valid high slots after removals; also accepted some initial/completed positions. | Compare the unsigned current slot against the exclusive allocated slot extent. |
| SwiftDictionary, Keys, Values, IDictionaryEnumerator accessors | Mixed signed/unsigned comparison allowed the initial negative cursor. | Compare both operands as unsigned so the negative sentinel is invalid. |
| SwiftHashSet | The same mixed signed/unsigned comparison allowed the initial negative cursor. | Compare both operands as unsigned. |
| SwiftPackedSet, SwiftSparseSet, SwiftSparseMap | Non-generic Current had no position guard; completion retained the previous cursor. | Check cursor bounds and mark completion at count. |
| SwiftGenerationalBucket | Used the current value's nullness as its position test, rejecting valid nulls and accepting invalid value-type/stale states. Reset also omitted mutation detection. | Check unsigned slot bounds and restore the Reset version guard. |

SwiftObservableList inherits the list fix. SwiftBiDictionary and
SwiftObservableDictionary inherit the dictionary fix. SwiftStack passed all
lifecycle checks without source changes. SwiftArray2D/3D use compiler-generated
iterators and passed non-generic empty/single/multiple-item final-item checks;
their generated Reset/invalid-Current behavior is outside this handwritten
enumerator correction. SwiftObservableArray is not enumerable. Spatial query
indexes, fixed-point wrappers and pools have no separate collection enumerator
implementation to repair.

## Regressions and validation

The tracked regression is
[EnumeratorContract.Tests.cs](../../../tests/SwiftCollections.Tests/Collection/EnumeratorContract.Tests.cs).
Its initial 291 cases produced **153 failures and 138 passes before the fix**,
then all passed after the fix. It covers 16 collection/view variants at counts
0, 1 and 3: before-first Current, every valid item including the last, repeated
Current, completion and repeated failed moves, reset before/after completion,
disposal, and mutation detection in MoveNext/Reset. Generic iteration supplies
the comparison sequence. Dedicated cases verify sparse bucket slots, valid
null generational values, and all IDictionaryEnumerator accessors. Final
additions exercise wrapped queue storage and both dimensional arrays.

Validation uses Windows 11 x64, Intel Core i7-9700K, SDK 10.0.302 and runtime
10.0.10, with sibling source dependencies selected explicitly. Both solution
builds compile netstandard2.1 and net10.0 runtime targets:

```powershell
$env:UseLocalLsfStack='true'
dotnet build SwiftCollections.slnx -c Release -m:1
dotnet build SwiftCollections.slnx -c ReleaseLean -m:1
# Repeat for each configuration: Release and ReleaseLean.
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Release --no-build --collect:'XPlat Code Coverage' --settings tests/SwiftCollections.Tests/coverlet.runsettings
dotnet test tests/SwiftCollections.FixedMathSharp.Tests/SwiftCollections.FixedMathSharp.Tests.csproj -c Release --no-build --collect:'XPlat Code Coverage' --settings tests/SwiftCollections.FixedMathSharp.Tests/coverlet.runsettings
```

| Configuration / assembly | Tests passed | Lines covered | Branches covered | Methods covered |
| --- | --- | --- | --- | --- |
| Release core | 1,369 | 5,261/5,261 | 2,310/2,310 | 1,132/1,132 |
| ReleaseLean core | 1,341 | 5,247/5,247 | 2,310/2,310 | 1,125/1,125 |
| Release companion | 43 | 121/121 | 62/62 | 35/35 |
| ReleaseLean companion | 43 | 121/121 | 62/62 | 35/35 |

Both solution builds passed. The benchmark project's existing NU1510 warning
about System.Text.Json pruning remains. Raw coverage has no missed lines,
branches or methods; no coverage exclusions changed. Before the fix, core
coverage was 5,251/5,251 lines, 2,306/2,306 branches and 1,132/1,132 methods.
The final regression suite contains 294 cases, including dimensional arrays.

Ignored artifacts are supporting evidence only; the
source regressions, commands, environment and findings here remain sufficient
to repeat the investigation. An independent ponytail review of the connected
production code, cursor invariants, tests and benchmark found no actionable
issues.

## Warm list enumeration check

The existing ListParityIntegerBenchmarks now includes a generic-interface
enumeration method beside direct struct enumeration. Both sum the same seeded
data at N = 100, 1,000, 10,000 and 100,000. No per-item check was added to
generic Current or MoveNext; the list's extra cursor assignment occurs when
MoveNext returns false.

A default-tiering out-of-process ShortRun before the fix had eight successful
child exits and all 24 expected measurement iterations. Direct enumeration
allocated 0 B and generic interface enumeration allocated 48 B per traversal.
Its latency samples were too short and sensitive to tiering to establish a
performance comparison.

The same out-of-process ShortRun after the fix also completed all eight child
exits and 24 measurement iterations, retaining 0 B direct and 48 B generic
allocation per traversal at every N. Reproduce with the existing runner:

```powershell
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net10.0 -- --filter '*ListParityIntegerBenchmarks.SwiftList_Enumerate*' --job Short
```

For the latency check, retain each benchmark output folder immediately after
its build so in-process execution loads that folder's dependency without a
child rebuild. Run baseline/fixed/fixed/baseline in separate processes, without
competing builds/tests, using:

```powershell
$env:UseLocalLsfStack='true'
$env:DOTNET_TieredCompilation='0'
$env:DOTNET_gcConcurrent='0'
# payload points to a retained baseline or fixed benchmark output folder.
dotnet "$payload/SwiftCollections.Benchmarks.dll" --filter '*ListParityIntegerBenchmarks.SwiftList_Enumerate*' --inProcess --warmupCount 10 --iterationCount 10 --invocationCount 1024 --unrollFactor 1
```

The retained baseline SwiftCollections.dll SHA256 was
`F3AF286CD0207B23B5E7A4720A6984C6F34092C4FEC6289A46A5A80FA6214F7B`;
the fixed payload was
`9BA4036E4E94D2A411A0291F95A789483C01F4EA98DB43C1263C73F556E86412`.
The baseline runtime source is the discovery revision above; only the new
generic benchmark existed when capturing its payload. All four runs completed
eight reports and 80 actual iterations each. The table averages the two runs
per payload; times are nanoseconds per complete traversal.

| Path | N | Baseline | Fixed | Difference |
| --- | --- | --- | --- | --- |
| Direct | 100 | 90.51 | 91.84 | +1.47% |
| Direct | 1,000 | 858.35 | 858.72 | +0.04% |
| Direct | 10,000 | 8,562.46 | 8,582.58 | +0.24% |
| Direct | 100,000 | 85,419.83 | 85,500.79 | +0.09% |
| Generic interface | 100 | 608.12 | 608.13 | +0.00% |
| Generic interface | 1,000 | 5,912.36 | 5,924.28 | +0.20% |
| Generic interface | 10,000 | 59,762.24 | 59,721.11 | -0.07% |
| Generic interface | 100,000 | 587,383.89 | 587,108.18 | -0.05% |

Direct N=100 varied from 90.35-90.67 ns in baseline runs and 90.64-93.04 ns
in fixed runs. This small result is inconsistent across run pairs; it does not
establish zero overhead or a per-item slowdown. Larger direct traversals and
all generic traversals differed by at most 0.24% in the balanced averages.
In-process allocation figures include a 4-5 B harness signal in both payloads
and are not used as a zero-allocation claim. These are local list measurements,
not cross-platform or whole-application performance guarantees.

Reopen for a concrete collection/lifecycle reproduction missed by these
regressions, or a repeatable per-item regression with comparable warmed inputs.
