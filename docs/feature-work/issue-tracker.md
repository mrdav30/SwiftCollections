# Issue Tracker

## Purpose

Track correctness, determinism, API, documentation, build, tooling and test
reliability concerns owned by SwiftCollections, including its FixedMathSharp
companion, with a compact index of verified resolutions or documented
investigation closures. Keep substantial implementation work in focused
feature-work plans.

## Tracker Rules

- Issue IDs use `SC-Issue-NNN`. The next available ID is `SC-Issue-024`.
- Assign an ID at intake and preserve it through closure; never delete or reuse
  IDs. Check Git history before advancing or repairing the counter. Unknown
  historical IDs stay reserved rather than acquiring an inferred cause.
- Record discovery dates, status, priority, affected owners, reproducible
  evidence, impact and the next useful action. Distinguish confirmed defects
  from unverified concerns.
- Keep active entries focused and self-contained. Link active plans rather than
  duplicating their work.
- After verification, replace the active entry with a compact `Resolved Issues`
  row: the same ID, resolution/closure date, brief outcome and an evidence link.
  Keep detailed root causes, decisions and validation in an existing completion
  record, regression or fix commit rather than duplicating them in this index.
- Distinguish verified fixes, mitigations and investigations closed without a
  reproduced defect. A closed investigation does not establish a runtime fix or
  an attributed cause; preserve its limits and recurrence guidance.
- Resolved means verified in source or closed by documented decision, not
  released. Add release/version claims only when separately confirmed. Retain
  prior evidence when new findings change an issue's status.
- Preserve existing issue anchors when compacting entries so inbound links
  continue to reach their issue. New rows may use a short `sc-issue-nnn` anchor.
- Use repository-relative links and portable commands. Ignored artifacts are
  supporting evidence; continuing an issue must not require their survival.
- Require full reported line, branch and executable-method coverage. Verify and
  remove unreachable branches; do not waive gaps through the complexity register,
  exclusions, weaker assertions or retries until a flaky result passes.
- Use `UseLocalLsfStack=true` for coordinated unreleased source validation;
  released packages remain the promotion/release gate. Validate Release and
  ReleaseLean and the affected core or companion assembly independently.
- Keep performance claims measured. This tracker does not replace benchmarks,
  implementation plans, tests or release notes.

## Reproducing active issues

The snippets are independent console programs. At the repository root, create
a console project outside the checkout, reference the core project, replace
its `Program.cs` with one snippet, and run it:

```bash
dotnet new console --framework net10.0 --output ../SwiftCollectionsRepro
dotnet add ../SwiftCollectionsRepro/SwiftCollectionsRepro.csproj reference src/SwiftCollections/SwiftCollections.csproj
dotnet run --project ../SwiftCollectionsRepro/SwiftCollectionsRepro.csproj -c Release -p:UseLocalLsfStack=false
```

Include these imports before each snippet:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using SwiftCollections;
using SwiftCollections.Dimensions;
using SwiftCollections.Utility;
```

Each entry records the revision and environment of its observed result. Use
that revision when reproducing historical behavior; a passing run after a fix
must be distinguished from the original failure. Reproducer inputs, output,
diagnosis, decisions and reopening criteria stay with their owning issue.

## Active Issues

No active issues are currently recorded.

## Resolved Issues

Dates reflect recorded resolution/closure, which can precede the commit date.
Evidence links retain the committed root causes, regressions and validation;
they do not claim a fresh rerun or package release.

| Issue                                                                                          | Resolved/closed | Outcome                                                                                                                                                                                                                                                              | Evidence                                                                                                                                                                                                       |
| ---------------------------------------------------------------------------------------------- | --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| SC-Issue-023 | 2026-11-21 | **Verified fix:** five list object adapters isolate conversion from virtual mutation/equality, preserving original user exceptions and existing type/null/index/reentrant-return contracts. | [Resolution, regressions and verification](#sc-issue-023---list-object-adapters-misclassify-callback-invalid-casts) |
| SC-Issue-022 | 2026-11-21 | **Verified fix:** dictionary object adapters share conversion-only catches and invoke virtual mutation afterward, preserving original comparer/override/subscriber exceptions and existing conversion contracts. | [Resolution and verification](#sc-issue-022---object-dictionary-adapters-misclassify-callback-invalid-casts) |
| SC-Issue-020 | 2026-11-21 | **Verified fix:** the Numerics octree shares widened midpoint arithmetic for child creation/selection and checks both rounded child spans before subdivision, preventing overflowing and nonshrinking children. | [Root cause, regressions and verification](#sc-issue-020---numerics-octree-subdivision-overflows-finite-midpoints) |
| SC-Issue-019 | 2026-11-21 | **Verified fix; upsert policy approved:** BVH now owns one leaf per non-null key; repeated Insert updates bounds and returns false. Null is rejected before mutation. Generic, Numerics and fixed-point paths share the fix. | [Resolution and compatibility evidence](#sc-issue-019---bvh-duplicate-and-null-key-policy-differs-from-peer-indexes) |
| SC-Issue-018 | 2026-11-21 | **Verified fix:** observable Add emits CollectionChanged/Add and PropertyChanged/Entries[] only after successful insertion across all six dispatch forms. Duplicate mappings remain intact; existing resize and subscriber-failure semantics are preserved. Standard/Lean/Debug pass strict 100% reported coverage on verified package/runtime inputs. | [Resolution and verification](#sc-issue-018---duplicate-dictionary-add-emits-a-false-change-notification), [regressions](../../tests/SwiftCollections.Tests/Observable/SwiftObservableDictionary.Tests.cs) |
| SC-Issue-017 | 2026-11-21 | **Verified fix and consolidation:** one embedded lifecycle owner guards all rental/backing-access routes across six inherited collection pools and the sparse-map pool. Flush stays reusable; Dispose is terminal. Duplicate lifecycle code is removed while named Release parameters remain compatible. Standard/Lean/Debug pass strict 100% reported coverage; constructor/disposal allocation improves in the tested workloads. | [Resolution, compatibility and lifecycle decisions](#sc-issue-017---disposed-collection-wrappers-still-rent-through-get), [regressions](../../tests/SwiftCollections.Tests/Pool/SwiftPoolLifecycle.Tests.cs) |
| SC-Issue-016 | 2026-11-21 | **Verified fix:** object-pool Clear deducts only popped inactive objects before destruction, retaining outstanding rentals and correct partial-failure accounting. Shared wrapper contracts agree. Standard/Lean/Debug tests and strict 100% reported line/branch/executable-method coverage pass; the unchanged slot-map analyzer warning is recorded separately. | [Resolution, verification and contract dispositions](#sc-issue-016---pool-clear-loses-outstanding-rental-counts), [regressions](../../tests/SwiftCollections.Tests/Pool/SwiftObjectPool.Tests.cs) |
| SC-Issue-015 | 2026-11-21 | **Verified fix; storage tradeoff accepted:** slot-linked free ownership keeps reinsertion O(1), preserves remaining reuse order and eliminates stale metadata growth. The user accepted the measured extra storage for small value types; state schemas remain unchanged. Standard/Lean/Debug tests and strict 100% reported line/branch/executable-method coverage pass. Trusted-state validation policy remains separately deferred. | [Resolution, measurements and reopening criteria](#sc-issue-015---slot-reinsertion-leaves-an-allocated-slot-on-the-free-stack), [regressions](../../tests/SwiftCollections.Tests/Collection/SwiftSlotMap.Tests.cs) |
| <a id="sc-issue-014---packed-set-duplicate-state-breaks-membership-invariants"></a>SC-Issue-014 | 2026-11-21 | **Verified fix; rejection policy:** packed-set restoration rejects equal duplicate items with `ArgumentException` and null arrays/items with `ArgumentNullException`. It preserves dense order rather than compacting or discarding entries; this enforces the existing unique/non-null input contract. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `10b5f5c685cf9e58bca6086894a547326a514f47`, Windows x64, SDK 10.0.302/runtime 10.0.10, Release/published dependencies (`UseLocalLsfStack=false`). Portable reproducer: `var s = new SwiftPackedSet<int>(new SwiftArrayState<int>(new[] { 1, 1 })); s.Remove(1); Console.WriteLine($"{s.Count}; {s.Contains(1)}");` previously printed `1; False`; construction now throws before publishing an owner. **Root cause:** restoration ignored failed lookup insertion while counting every copied dense item. It now checks the existing insertion result, builds replacement storage locally and publishes storage/count/version only after success. Indexing the copied dense snapshot also prevents element hash callbacks that replace input-array entries from making lookup disagree with dense storage. Failed replacement preserves owned fields and live enumerators; references remain shallow and element callback side effects are not rolled back. Raw DTOs remain passive; schemas and normal Add/Contains/Remove paths are unchanged. The 15-case regression group produced 10 behavioral failures and five passing controls before correction; all 15 Standard/13 Lean cases now pass. Coverage includes duplicates/equal references, zero/default/null input, failure preservation, throwing hash callbacks, input-array mutation, valid order/ownership/continuation, JSON and direct/ref-existing MemoryPack. Release/ReleaseLean/Debug solution builds pass with zero warnings/errors; core tests 1849/1743/1847 and companion 43 each pass without skips. The existing strict gate passes with **100% reported lines/branches/executable methods**: core lines 6078/6078, 5884/5884, 8024/8024; branches 2880/2880, 2804/2804, 2888/2888; methods 1261/1261, 1220/1220, 1261/1261. Companion lines 121/121, 121/121, 155/155; branches 62/62 and methods 35/35 in each configuration. Both assemblies build for `netstandard2.1` and `net10.0` in Standard/Lean; all four packages' eight DLL/XML pairs match build output and XML contracts parse. Clean DocFX reports zero warnings/errors; rendered constructor exceptions, state/property lifetime contracts and guide pass inspection, along with CI site smoke checks (104 pages, 1320 local href targets). Hash-checked final Standard/Lean probes each allocate 371712 bytes for 128 restores of 32 integers (2904 bytes/restore), matching the measured pre-fix Standard baseline; 10000 warm remove/add/contains cycles allocate zero bytes before and after. No latency claim. No introduced build/test/reference failure; hosted CI and Unity were not rerun. Reopen for a supported restore violating membership, count, order or storage ownership, or an explicit requirement to normalize duplicates instead of rejecting them. | [Regressions](../../tests/SwiftCollections.Tests/Collection/SwiftPackedSet.Tests.cs), [restore contracts and implementation](../../src/SwiftCollections/Collection/SwiftPackedSet.cs), [serialization guide](../api/guides/serialization-and-diagnostics.md#trusted-state-and-comparers) |
| <a id="sc-issue-013---base-indexer-writes-bypass-bidirectional-bookkeeping"></a>SC-Issue-013 | 2026-11-21 | **Verified fix:** the dictionary indexer is virtual and BiDictionary/ObservableDictionary override the shared slot, so base, generic and non-generic assignments honor reverse-map and notification contracts. BiDictionary validates null values before mutation, retains the stored forward key in reverse mappings after equivalent-key updates, and leaves reverse insertion with its existing insertion override rather than attempting it twice. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `e4f784ce623aa7e6777f11aef9fac394af6812aa`, Windows x64, SDK 10.0.302/runtime 10.0.10, Release/published dependencies. Portable reproducer: add `(1,10)` to `SwiftBiDictionary<int,int>`, assign `((SwiftDictionary<int,int>)d)[1]=20`, then print `d[1]`, `ContainsValue(10)` and `ContainsValue(20)`. Previously `20; True; False`; corrected `20; False; True`. Related concrete null assignments changed forward/reverse state before throwing; equivalent-key updates stored the alias rather than the stored key. Final 16-case group has 14 behavioral failures and two passing validation controls before correction, all passing afterward. It covers replacement/insertion, duplicate/null/type rejection, both comparers, canonical key ownership, enumeration, notification payload/order/no-op and Add/Remove/Clear continuation. Release/ReleaseLean/Debug solution builds pass with zero warnings/errors; core tests 1834/1730/1832 and companion 43 each pass without skips. Strict coverage is 100% of reported lines/branches/executable methods: core lines 6076/6076, 5882/5882, 8022/8022; branches 2880/2880, 2804/2804, 2888/2888. Both target frameworks and all four packages are checked; all eight DLL/XML pairs match. Metadata verifies the virtual base slot and both overrides. A .NET caller compiled against the old assembly works with only the fixed core substituted. Hash-checked Standard/Lean integer indexer/interface-update probes allocate zero bytes over 10000 warm iterations; no latency claim. Clean DocFX, rendered contracts and reference/link checks pass. **Comparer-replacement disposition:** preserve the precondition that replacement equality keeps stored keys/values distinct; no merge/deduplication or general exception-atomicity policy is added. Reopen that decision for an explicit reject/merge policy with failure-state tests. Arbitrary application-comparer exceptions can still leave partial changes, as documented. No introduced build/test/reference failure; hosted CI and Unity were not rerun. Reopen for a supported assignment path that bypasses either mapping or notification contract. | [Bidirectional regressions](../../tests/SwiftCollections.Tests/Collection/SwiftBiDictionary.Tests.cs), [observable regressions](../../tests/SwiftCollections.Tests/Observable/SwiftObservableDictionary.Tests.cs), [indexer contracts](../../src/SwiftCollections/Collection/SwiftBiDictionary.cs), [trusted-state decision](#sc-issue-010---sorted-list-restore-loses-its-ordering-invariant) |
| <a id="sc-issue-012---dictionary-views-retain-detached-entry-arrays"></a>SC-Issue-012 | 2026-11-20 | **Verified fix:** key/value views retain only their owner and resolve its current entry array once per copy/membership call. Removed both cached arrays; preserved copy guards/order, default value equality and enumerator version semantics. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `3bf654f1e10e9b73e449706cb866002f0e419cdd`, Windows x64, SDK 10.0.302/runtime 10.0.10, Release/published dependencies. Portable reproducer: create `SwiftDictionary<int,int>`, add `(1,10)`, retain Keys/Values, call EnsureCapacity(64), add `(2,20)`, then copy each view into a two-element array. Previously `2; 1,0; 10,0`; corrected `2; 1,2; 10,20`. A value at key 40 after growth also reproduced IndexOutOfRangeException. Final ten-case regression group: nine failures and one clear/reuse control against the frozen unfixed core (`2C3469FCA6320196EEF85F6E658C7EA8C478A39BD5DFE6433236DBF5E1A0EF55`), all pass with the fix. Covers explicit/automatic growth, removal after growth, trim, comparer rehash, clear/reuse, empty internal state replacement, inherited BiDictionary/ObservableDictionary, typed/non-generic copies, live membership/order/identity and existing enumerator rules. Package-mode Release/ReleaseLean/Debug solution builds pass with zero warnings/errors; core tests 1815/1712/1813 and companion 43 each pass without skips. Both views retain full line/branch coverage. Initial core reports retained two unreachable persistence guards (lines 6084/6086, 5890/5892, 8030/8032; branches 2886/2888, 2810/2812, 2894/2896); this did not meet the full coverage gate. The [2026-11-21 correction](done/2026-11-20-comparer-persistence-plan.md#full-coverage-correction--2026-11-21) removes them after proof and restores 100% reported coverage in all three configurations. Hash-checked Standard/Lean probes each allocate zero bytes for 10000 warm typed-copy/membership iterations over two integer pairs; no latency claim. An initial disposable probe selected a frozen DLL from generated output; its results were discarded and reference selection corrected before the accepted runs. Eight assembly/XML pairs and all four package IDs match both target frameworks; clean DocFX, rendered view contracts and CI reference/local-link checks pass. No introduced source/build/test/reference failure; hosted CI and Unity were not rerun. Reopen for a valid owner mutation leaving retained views stale or a copy/equality contract regression. | [Regressions](../../tests/SwiftCollections.Tests/Collection/SwiftDictionary.Tests.cs), [key view](../../src/SwiftCollections/Collection/SwiftDictionary.KeyCollection.cs), [value view](../../src/SwiftCollections/Collection/SwiftDictionary.ValueCollection.cs) |
| <a id="sc-issue-011---power-of-two-recognition-accepts-a-negative-input"></a>SC-Issue-011 | 2026-11-20 | **Verified fix:** require `x > 0` before the single-bit test; all non-positive inputs now return false. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `8099dd71d08311fee8cdf8d337ad6c59143624d3` on Windows x64, SDK 10.0.302/runtime 10.0.10, Release with published dependencies. Portable reproducer: `SwiftHashTools.IsPowerOfTwo(int.MinValue)` returned True; expected and corrected result is False. The 14-case theory fails only that case before the fix (13 controls pass); afterward all 23 utility cases pass. Existing `NextPowerOfTwo(1 << 30) == int.MaxValue` sentinel policy and its test remain unchanged; reopen that decision only for an agreed capacity-domain contract and validation of bit-mask consumers. No production caller of IsPowerOfTwo exists in this checkout. Package-mode Release/ReleaseLean/Debug solution builds pass with zero warnings/errors; core tests 1805/1702/1803 and companion 43 each pass with no skips. At this checkpoint the helper had full line/branch coverage, but two uncovered persistence guards remained. The subsequent 2026-11-21 correction removes those proven-unreachable branches and restores full reported coverage; see the linked proof. Checked both runtime assemblies' eight DLL/XML pairs and all four package IDs at both TFMs; clean DocFX, rendered contract and CI reference/local-link checks pass. No introduced build/test/reference failure; no hosted CI, Unity or timing rerun is claimed. Reopen for incorrect positive-power classification or a confirmed caller regression. | [Regression and sentinel control](../../tests/SwiftCollections.Tests/Utility/SwiftHashTools.Tests.cs), [helper and contracts](../../src/SwiftCollections/Utility/SwiftHashTools.cs), [coverage correction and proof](done/2026-11-20-comparer-persistence-plan.md#full-coverage-correction--2026-11-21) |
| SC-Issue-021 | 2026-11-20 | Implemented strict comparer persistence for all five owners; both target/package graphs, actual Mono/High-stripping IL2CPP and hosted Windows/Linux tests pass. SC-Benchmark-006 subsequently closed by bounded no-source-change decision; the historical native failure remains unexplained and no crash fix is claimed. | [Resolution](#sc-issue-021), [completed plan](done/2026-11-20-comparer-persistence-plan.md#phase-3-closeout--2026-11-20) |
| <a id="sc-issue-010---sorted-list-restore-loses-its-ordering-invariant"></a>SC-Issue-010 | 2026-11-19 | **Closed by contract decision; no runtime fix.** Preserve copy-only trusted-state restoration, the items-only schema and default comparer. No scan, comparison, validation, re-sort or comparison-failure atomicity promise is added. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `31cd3eec80dff86b95f8cab621cc7ca281b4e7cc` on Windows x64, SDK 10.0.302/runtime 10.0.10, Release with published dependencies. Portable reproducer (console setup above): construct `a = new SwiftSortedList<int>(new[] { 1, 2, 3 }, Comparer<int>.Create((x, y) => y.CompareTo(x)))`, then `b = new SwiftSortedList<int>(a.State)`. Enumeration is `3,2,1`; `b.Contains(1)` is False. This remains the consequence of using a custom-ordered snapshot with default binary lookup. Callers must provide state compatible with the restored default comparer before ordering-dependent use, or explicitly copy and sort once with `new SwiftSortedList<int>(a.State.Items, a.Comparer)`. A different comparer can be installed immediately after restore before lookup/mutation; setting the existing comparer is a no-op, and assigning State resets a comparer configured beforehand. Dictionary/set restore instead reinserts with default equality and can irreversibly merge entries: choose a comparer-aware constructor before insertion when custom identity matters. Rejected automatic normalization and its measured costs are preserved in SC-Benchmark-004. **Verification:** Runtime source matches the reproduced revision after removing XML comments. Added 18 standard /17 Lean contract cases (14/13 sorted-state cases plus four equality-loss cases); all are controls for the approved behavior, not a runtime-fix red/green claim. Sorted-list suites pass 98/96 cases. `dotnet build SwiftCollections.slnx` and `dotnet test SwiftCollections.slnx --no-build`, with `-c Release`, `ReleaseLean`, and `Debug` and `-p:UseLocalLsfStack=false`, pass: core 1578/1543/1576 and companion 43 each, zero build warnings/errors. Existing core coverlet settings report lines 5368/5368, 5354/5354, 7150/7150 and branches 2406/2406, 2406/2406, 2414/2414 respectively; sorted-list methods 59/59 in each. Both `SwiftCollections.dll`/`SwiftCollections.xml` and `SwiftCollections.FixedMathSharp.dll`/`SwiftCollections.FixedMathSharp.xml` checked at `netstandard2.1` and `net10.0` in Release/ReleaseLean; matching package contents checked for SwiftCollections, SwiftCollections.Lean, SwiftCollections.FixedMathSharp and SwiftCollections.FixedMathSharp.Lean. Clean-generated DocFX passes with warnings as errors; rendered constructors, state/property values, comparer contracts, inherited State link and DTO remarks checked, plus CI reference/local-link smoke checks and desktop/phone guide preview. No baseline or introduced build/test/reference failures observed. Hosted CI, other OS/architectures and a new timing comparison were not run; the rejected prototype timings are historical evidence only. Reopen only for a valid-state continuation or ownership defect, or an explicit new requirement for unordered-state validation or comparer persistence. No release is claimed. | [Trusted-state and comparer contracts](../api/guides/serialization-and-diagnostics.md#serialization), [sorted-state tests](../../tests/SwiftCollections.Tests/Collection/SwiftSortedList.Tests.cs), [rejected experiment](benchmark-signal-hardening-backlog.md#sc-benchmark-004---sorted-state-order-validation) |
| <a id="sc-issue-009---list-to-list-copy-grows-the-destination-only-once"></a>SC-Issue-009 | 2026-11-18 | List-to-list CopyTo now guards insufficient capacity and requests the entire source Count through EnsureCapacity. Large copies obtain sufficient storage in one growth; fitting buffers, including non-power-of-two, exact-fit, self-copy and empty-to-empty targets, are reused. Policy disposition: only insufficient capacity causes growth; the old empty-to-empty allocation of eight slots is removed. Count replacement, shallow ownership, retained unused slots, target enumerator invalidation and null-target NullReferenceException remain implemented contracts. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `63c6124572b703680e797770ed0d93e0eb88bea0`: copying 100 items into an empty target threw ArgumentException and left target Count 0/Capacity 8; fixed output is Count 100/Capacity 128 with items 0..99. New group: baseline 13 failures/11 controls, all 24 pass after correction; existing JSON/MemoryPack continuation also reproduced the defect. Full list suite passed 113 cases. Release/ReleaseLean built both assemblies at netstandard2.1/net10.0 and all four package IDs; tests passed at net10.0 (core 1560/1526; companion 43 each). Debug passed 1558/43; all core lines/branches and authored list methods remained covered in all three configurations. Emitted XML and rendered CopyTo contracts checked; DocFX passed with warnings as errors. Reopen for valid-copy failure, unexpected allocation with sufficient storage, or an intentional null/capacity policy change. No release is claimed. | [Copy regressions](../../tests/SwiftCollections.Tests/Collection/SwiftList.Tests.cs), [copy owner and contract](../../src/SwiftCollections/Collection/SwiftList.cs), [performance evidence and limits](benchmark-signal-hardening-backlog.md#sc-benchmark-003---list-to-list-copy-capacity) |
| <a id="sc-issue-008---exactly-full-queue-state-restores-an-invalid-tail"></a>SC-Issue-008 | 2026-11-18 | State restore now installs the new capacity mask before assigning `tail = count & mask`, matching known-count construction, Resize and TrimExcessCapacity. Exactly full queues can dequeue/refill or drain/reuse without growth; FIFO order, ownership, capacity reconstruction and serialization schema are preserved. Enqueue/Dequeue hot paths are unchanged. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `7ce79b7376b586df5455e4aa9d04a513d7f95338`: restoring items 0..7, dequeuing 0, then enqueueing 99 threw IndexOutOfRangeException instead of retaining [1,2,3,4,5,6,7,99]. New regressions: baseline 14 failures/11 passing controls; fixed 25 cases (23 Lean), covering boundary counts, stale-mask overwrite, wrapped snapshots, views, growth controls and JSON/MemoryPack continuation. Release/ReleaseLean built both assemblies at netstandard2.1/net10.0 and all four package IDs; tests passed at net10.0 (core 1536/1502; companion 43 each). Debug passed 1534/43; all core lines/branches and authored queue methods remained covered in all three configurations. DocFX passed with warnings as errors. Existing `queue-workload --filter '*SwiftQueue_WrapAroundChurn*' --job Dry` smoke completed four cases and four successful child exits; this is not a throughput comparison. Reopen for any valid snapshot that fails FIFO continuation or restores out-of-range ring indices. No release is claimed. | [Restore regressions](../../tests/SwiftCollections.Tests/Collection/SwiftQueue.Tests.cs), [queue owner](../../src/SwiftCollections/Collection/SwiftQueue.cs) |
| <a id="sc-issue-007---int16-normalization-fails-on-constant-input"></a>SC-Issue-007 | 2026-11-18 | Replaced floating interpolation/NaN narrowing with exact, overflow-safe Int32 arithmetic. Constant input maps to the requested min (extending the existing min=0 test); reversed endpoints retain descending mapping; the final signed rational truncates toward zero. Storage and shape are preserved. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `0ffc9d76dc0ed3bfbf485cc7491c4cd66f01501e`: constant 5 mapped to [10,20] yielded 0, and identity mapping [0,53,100] yielded 52 for the middle cell. Focused suite: baseline 13 failures/21 passing controls; fixed 34 passes (33 Lean), including four full Int16 sweeps and JSON/MemoryPack continuation. Release/ReleaseLean built both runtime assemblies at netstandard2.1/net10.0 and all four package IDs; tests passed at net10.0 (core 1511/1479; companion 43 each). Debug passed 1509/43; core line/branch coverage remains complete in all three configurations. DocFX passed with warnings as errors. Policy disposition: resolved; reopen for a demonstrated arithmetic/ownership defect or an intentional change to constant/reversed-range semantics. No release is claimed. | [Regressions](../../tests/SwiftCollections.Tests/Dimension/SwiftShortArray2D.Tests.cs), [API contract](../../src/SwiftCollections/Dimension/Default/SwiftShortArray2D.cs), [performance evidence and limits](benchmark-signal-hardening-backlog.md#sc-benchmark-002---exact-int16-normalization) |
| <a id="sc-issue-006---dimensional-arrays-accept-inconsistent-shapes"></a>SC-Issue-006 | 2026-11-18 | Enforced non-negative dimensions and exact Int32 products at construction, Resize and restore; restored data must match the shape, and owner validation/copy failures preserve existing storage and stored values. Preserved 2D constructor clamping, passive state DTOs and null exception distinctions; reconsider those policies only through an intentional API change. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `9ccb87724652516232322f3fe5ab0b173b493c4b`: `(65536,65536)` produced an empty 2D array, `(-1,-1,1)` produced a one-cell 3D array, and a 2x2 state with one item was accepted. Final regression suite: 45 failures and 20 passing controls on that baseline, all 65 passing after the fix (63 in Lean). Release/ReleaseLean builds checked both runtime assemblies at netstandard2.1 and net10.0 and all four package IDs; unit tests passed at net10.0 (core 1485/1453, companion 43 each). Debug build/tests passed (1483/43); core coverage retained all reachable lines/branches/methods in all three configurations. DocFX passed with warnings as errors. No release is claimed. | [Regressions](../../tests/SwiftCollections.Tests/Dimension/SwiftDimensionalArrayValidation.Tests.cs), [restore contracts](../api/guides/serialization-and-diagnostics.md#serialization), [performance evidence and reopening criteria](benchmark-signal-hardening-backlog.md#sc-benchmark-001---dimensional-resize-validation-code-generation) |
| <a id="sc-issue-005---cold-value-key-lookup-boxes-a-default-key"></a>SC-Issue-005              | 2026-10-30      | Replaced dictionary/set nullability initializers with cached type metadata, removing the 24-byte first integer/enum lookup allocation exposed by Gravitas pair teardown. Reference, nullable and generic-struct semantics are preserved without a warmup workaround. | [Cold lookup and removal regressions](../../tests/SwiftCollections.Tests/Collection/SwiftFirstLookupAllocation.Tests.cs)                                                                                       |
| <a id="sc-issue-004---non-generic-list-enumeration-rejects-the-final-item"></a>SC-Issue-004    | 2026-10-28      | Corrected list/queue final-item guards and reproduced position-contract defects across the other affected collections and dictionary views; preserved generic iteration and full reachable coverage.                                                                 | [Closeout and regressions](done/2026-10-28-enumerator-contract-closeout.md)                                                                                                                                    |
| <a id="sc-issue-003---hash-set-lookup-boxes-value-keys-in-unoptimized-builds"></a>SC-Issue-003 | 2026-10-26      | Removed value-key boxing from all three hash-set lookup APIs, including Debug; retained null/error contracts and added warmed allocation regressions.                                                                                                                | [Closeout](https://github.com/mrdav30/SwiftCollections/blob/2b4894f87980147934b1eb74603029e04fb84ec8/docs/feature-work/issue-tracker.md#sc-issue-003---hash-set-lookup-boxes-value-keys-in-unoptimized-builds) |
| <a id="sc-issue-002---collision-probes-stop-before-reaching-stored-entries"></a>SC-Issue-002   | 2026-10-07      | Completed dictionary/set collision-probe periods for insertion, lookup and removal; deterministic wrapped-chain and trimmed-table regressions passed.                                                                                                                | [Closeout](https://github.com/mrdav30/SwiftCollections/blob/5e0c4e0830342508c3fb87c2fe43cf7c30167522/docs/feature-work/issue-tracker.md#sc-issue-002---collision-probes-stop-before-reaching-stored-entries)   |
| <a id="sc-issue-001---pool-finalizer-coverage-depends-on-incidental-gc"></a>SC-Issue-001       | 2026-10-07      | Replaced incidental-GC coverage with isolated real-GC, mutation-checked pool-finalizer regressions; closed a test reliability gap without a production cleanup change.                                                                                               | [Closeout](https://github.com/mrdav30/SwiftCollections/blob/5e0c4e0830342508c3fb87c2fe43cf7c30167522/docs/feature-work/issue-tracker.md#sc-issue-001---pool-finalizer-coverage-depends-on-incidental-gc)       |

### SC-Issue-015 - Slot reinsertion leaves an allocated slot on the free stack

- **Status / priority:** Resolved 2026-11-21 / P2; verified fix with the
  type-specific storage tradeoff explicitly accepted by the user.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`;
  reproduced before this fix at `4979ec4fc2d3a5e54b7f60d0eff8b6f132514392`.
- **Affected owners / impact:** `SwiftSlotMap<T>.InsertAt` after `RemoveAt`
  leaves a live slot available for reuse. The next `Add` overwrites it and
  overcounts allocations. Repeated remove/reinsert also accumulates stale free
  metadata, including in the existing stable-slot churn benchmark.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10,
  published dependencies (`UseLocalLsfStack=false`,
  `DisableTransitiveProjectReferences=false`).

**Reproducer** (use the console setup above):

```csharp
var s = new SwiftSlotMap<int>();
int i = s.Add(1);
s.RemoveAt(i); s.InsertAt(i, 2);
int j = s.Add(3);
Console.WriteLine($"{i}; {j}; {s.Count}; {s[i]}");
```

Before: `0; 0; 2; 3`; one live slot is counted twice and value 2 is lost.
After: `0; 1; 2; 2`; slot zero retains its value and Add allocates slot one.

**Root cause and implementation:** The integer stack had no keyed removal, and
InsertAt never relinquished the selected slot's free ownership. Free links now
live in slot entries, replacing the separate stack. Shared push/unlink helpers
serve removal, allocation, reinsertion, restore and trim; they touch only the
selected slot and its neighbors. Last-freed-first order of remaining free slots
is preserved, without a scan or deferred cleanup in Add. Never-freed gaps retain
their existing behavior. Resize copies links; trim rebuilds them. Explicit
reference cleanup clears values without erasing retained empty-map free links.
The state DTO and bottom-to-top `FreeIndices` wire order are unchanged.
`SwiftTicketMap` has no explicit reinsertion counterpart and needs no runtime
change for this valid-input defect. See [implementation and XML contracts](../../src/SwiftCollections/Collection/SwiftSlotMap.cs),
[regressions](../../tests/SwiftCollections.Tests/Collection/SwiftSlotMap.Tests.cs)
and the [slot ownership guide](../api/guides/collections.md#stable-slots-under-your-ownership).

**Verification:** The initial ten-case regression group produced nine behavioral
failures and one passing gap control before correction. All final 14 Standard /
13 Lean cases pass. They cover sole/head/middle/tail free ownership, repeated
removal/replacement, exact remaining reuse order, counts/live values, enumerator
invalidation, gaps, active-free growth, empty-map clearing, trim and raw-state,
JSON and standard MemoryPack continuation. The focused initial slot-map suite
passed 62 tests; the complete final runs include the four later cleanup/growth
cases. Independent source/test reviews pass.

| Final configuration | Core tests | Companion tests | Core lines / branches / executable methods | Companion lines / branches / executable methods |
| --- | ---: | ---: | --- | --- |
| Release | 1863 | 43 | 6106/6106; 2894/2894; 1264/1264 | 121/121; 62/62; 35/35 |
| ReleaseLean | 1756 | 43 | 5912/5912; 2818/2818; 1223/1223 | 121/121; 62/62; 35/35 |
| Debug | 1861 | 43 | 8060/8060; 2902/2902; 1264/1264 | 155/155; 62/62; 35/35 |

All solution builds have zero warnings/errors; no tests are skipped. Each test
project used its existing Coverlet settings, and the unchanged strict coverage
gate passed against fresh results from both assemblies. The first Debug gate
rejected an uncovered early-return sequence point despite the gap-control
branch executing; the final helper expresses membership as a conditional unlink
and all reported points pass without exclusions or gate changes. Both runtime
assemblies build for `netstandard2.1` and `net10.0` in Standard/Lean; all four
packages' eight DLL/XML pairs match build output. XML, clean DocFX (zero
warnings/errors), rendered insertion/constructor/guide contracts and CI site
smoke/link checks pass (104 HTML pages, 1320 local href targets). No outstanding
introduced build/test/reference failure. Hosted CI and Unity were not rerun.

**Measured storage decision — accepted 2026-11-21:** Keep constant-time
reinsertion and deterministic last-freed-first reuse, accepting the type-specific
capacity cost below. The expected workload generally uses small maps (roughly
below 5000 live slots); this is usage context, not a supported-capacity limit.
Storage follows allocated capacity rather than current Count. Do not add a
separate-array layout or narrower indices without a demonstrated workload need.
Reopen this storage decision for measured small-value-heavy capacity pressure or
a repeatable lookup/enumeration regression. Flat total allocation does not prove
latency parity; that claim remains unverified. With requested capacity 100000
(actual 131072), construction allocation measured as follows in hash-checked
before/final Standard probes; final Lean matches Standard:

| Value type | Before, bytes | Final, bytes |
| --- | ---: | ---: |
| byte | 786568 | 1572944 |
| int | 1573000 | 1572944 |
| string | 2621576 | 2097232 |

For 100000 live integer slots, 1000 warmup remove/reinsert cycles at slot 50000
followed by 300000 measured cycles previously allocated 3145776 bytes and left
301000 stale free entries. Final Standard/Lean allocate zero bytes, retain zero
free entries and permit a following Add at slot 100000. Ordinary warm
RemoveAt/Add cycles allocate zero before and after. These are allocation and
ownership checks, not latency acceptance. The existing
`SlotMapWorkloadBenchmarks.SlotMap_StableSlotChurn25` also completed all four N
values (100/1000/10000/100000) before and after, using in-process execution,
five warmup and ten actual samples per case. Its single-invocation iterations
are below BenchmarkDotNet's recommended duration and noisy; no speedup or
zero-overhead claim follows. Portable selection:
`dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net10.0 -- all --filter '*SlotMapWorkloadBenchmarks.SlotMap_StableSlotChurn25*' --inProcess --iterationCount 10 --warmupCount 5`.

- **Disposition / reopening:** Close the confirmed defect; no further runtime
  change is required for this issue. Reopen for a supported sequence that loses a
  live slot, corrupts counts or changes the remaining free-slot order. The
  accepted storage decision and the separate deferred validation-policy decision
  retain their own reopening criteria.

**Related API decision — trusted state and unchecked mutation:** Disposition
remains deferred; preserve existing preconditions in this fix. Slot/ticket
restore checks free-index range but trusts uniqueness and disjointness. For
example, `new SwiftTicketMap<int>(new SwiftTicketMapState<int>(new[] { 1 },
new[] { true }, new uint[] { 0 }, new[] { 0 }, 1)).Add(2)` overwrites a live slot
and reports Count=2. Likewise, `new SwiftSlotMap<int>().RemoveAt(0)` reports
Count=-1, whereas `TryRemoveAt(0)` returns false without mutation. These inputs
violate required ownership invariants and are separate from the fixed valid
sequence. Do not rely on a particular corruption trace for malformed state.
Reopen the validation-policy decision when explicitly choosing whether public
restore and unchecked forms reject duplicate/overlapping free slots or remain
trusted operations; require checked/unchecked and failure-state regressions.

### SC-Issue-016 - Pool Clear loses outstanding-rental counts

- **Status / priority:** Resolved 2026-11-21 / P2; verified fix.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`;
  reproduced before this fix at `6394427636afa5d35824f92575565c6345b53f17`.
- **Affected owners / impact:** `SwiftObjectPool<T>.Clear` reset CountAll even
  when rentals remained outstanding. Returning them made CountActive negative.
  Collection wrappers delegate to the same object-pool owner.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10,
  published dependencies (`UseLocalLsfStack=false`,
  `DisableTransitiveProjectReferences=false`).

**Portable reproducer** (use the console setup above):

```csharp
using var p = new SwiftCollections.Pool.SwiftObjectPool<object>(() => new object());
var item = p.Rent(); p.Clear(); p.Release(item);
Console.WriteLine($"{p.CountAll}; {p.CountInactive}; {p.CountActive}");
```

Before: `0; 1; -1`. After: `1; 1; 0`; the outstanding rental remains counted and
can return to the same usable pool.

**Root cause and resolution:** Clear removed only inactive objects but reset the
entire creation counter. It now decrements CountAll immediately after each
successful pop, before invoking the destroy callback, and has no final reset.
If destruction throws, the popped object remains removed and deducted, remaining
cached objects and active rentals remain counted, and a later Clear can continue.
Rent/Release, storage, ownership checks, allocation behavior and concurrency
policy are unchanged. No runtime helper, lock, field or type was added. The
shared interface summary, eight collection-wrapper Clear comments, counter XML
and [pool guide](../api/guides/pools-and-observables.md#scope-a-rental) agree with
this behavior. See [implementation](../../src/SwiftCollections/Pool/SwiftObjectPool.cs)
and [regressions](../../tests/SwiftCollections.Tests/Pool/SwiftObjectPool.Tests.cs).

**Verification:** The final eleven-case group produced nine behavioral failures
and two passing empty/fully-inactive controls before correction. All eleven now
pass in Standard, Lean and Debug; the focused pool suite passes 65 tests. The
regressions cover exact active/inactive counts and object identity, repeated
clear, new rent, overflow with/without destroy callbacks, counts observed inside
destroy callbacks, partial failure and continuation, lease idempotence and a
real collection-wrapper path. Independent source/test reviews pass.

| Final configuration | Core tests | Companion tests | Core lines / branches / executable methods | Companion lines / branches / executable methods |
| --- | ---: | ---: | --- | --- |
| Release | 1874 | 43 | 6107/6107; 2894/2894; 1264/1264 | 121/121; 62/62; 35/35 |
| ReleaseLean | 1767 | 43 | 5913/5913; 2818/2818; 1223/1223 | 121/121; 62/62; 35/35 |
| Debug | 1872 | 43 | 8062/8062; 2902/2902; 1264/1264 | 155/155; 62/62; 35/35 |

All solution builds succeed with zero errors and no skipped tests. The unchanged
strict coverage gate passes with 100% reported lines, branches and executable
methods, using fresh assembly-specific reports and the existing Coverlet
settings. Both runtime assemblies build for `netstandard2.1` and `net10.0` in
Standard/Lean; all four packages' eight DLL/XML pairs match build output. XML
contracts are checked in each core output. Clean DocFX reports zero
warnings/errors; rendered object-pool values/Clear, interface/eight-wrapper
Clear and guide contracts pass inspection, as do CI site smoke/link checks
(104 HTML pages, 1320 local href targets).

**Baseline versus introduced results:** Release's final build reports zero
warnings. Lean and Debug each report the pre-existing xUnit2013 warning at
`SwiftSlotMap.Tests.cs:138`; an isolated rebuild of committed source at the
reproducer revision confirms the same warning. The slot-map test is unchanged
by this fix. No introduced build, test, coverage or reference failure remains.
Hosted CI and Unity were not rerun.

**Allocation checks:** Hash-checked before/final Standard probes and final Lean
use 1000 warmups and 10000 measured cycles. Rent/Release of one reused object
allocates 320000 bytes before and after (ConcurrentStack push nodes). A second
workload retains one outstanding rental while repeatedly renting a fresh object,
releasing it and clearing the inactive cache; allocation remains 560000 bytes.
Its final all/inactive/active counts change from `0/0/0` to `1/0/1`. These checks
show unchanged allocation for the tested workloads; they are not a zero-allocation
or latency claim. There is no existing pool benchmark selection, and no maintained
benchmark harness was added for this fix.

**Related contract dispositions:** Preserve externally synchronized lifecycle and
counter access; concurrent storage does not make counters, callbacks or max-size
checks atomic. Do not add synchronization or foreign/double-return tracking in
this fix. Preserve existing terminal diagnostics: Dispose empties cached storage
without adjusting CountAll, so post-disposal CountActive is arithmetic, not a
live-rental guarantee. Rent/release callback failures retain their existing
nontransactional behavior; the per-pop failure guarantee above is specific to
Clear. Collection Clear retains the same backing pool; array-pool Clear discards
length buckets, so finish array rentals before clearing and finish all rentals
before Flush/disposal. SC-Issue-017's disposed-wrapper Get defect remains separate.
Reopen these policy decisions for an explicit requirement for atomic concurrent
accounting, terminal live counts, transactional callbacks or cross-clear array
rental ownership, with corresponding lifecycle and failure-state tests.

- **Disposition / reopening:** Close the confirmed Clear defect. Reopen for a
  supported, synchronized Clear/return sequence that loses outstanding counts,
  breaks reuse or misaccounts a popped object when destruction throws.

### SC-Issue-017 - Disposed collection wrappers still rent through Get

- **Status / priority:** Resolved 2026-11-21 / P2; verified fix and lifecycle consolidation.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`;
  reproduced before this fix at `5c3a7f612eb140b23dbe74e64bbf409426123f7c`.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10,
  published dependencies (`UseLocalLsfStack=false`,
  `DisableTransitiveProjectReferences=false`).

**Portable reproducer** (use the console setup above):

```csharp
var p = new SwiftCollections.Pool.SwiftListPool<int>();
p.Dispose();
using var lease = p.Get(out var list);
Console.WriteLine(list.Count);
```

Before: succeeds and prints `0`, although Rent throws. After: Get throws
`ObjectDisposedException` before initializing or accessing a backing pool.
CollectionPool access after disposal also throws, closing a second revival path.

**Root cause and resolution:** Disposal flags lived in six derived wrappers,
while inherited Get and the backing-pool getter bypassed them. The existing
collection base and the standalone sparse-map pool now embed one mutable
`SwiftPoolLifecycle<T>` struct. Its nullable volatile lazy reference is the
single lifecycle state: null is terminal. Each operation captures it once;
disposal clears initialized cached objects, disposes the backing pool, then
publishes null. Clear and Flush do nothing after disposal and never initialize
unused backing storage. Flush remains reusable while the owner is alive.
Static cached factories/reset callbacks do not capture wrappers or add helper
objects. The existing real-GC tests verify finalization with retained backing
pool references. SparseMap still does not implement ICollection and does not gain
a Flush API; it shares lifecycle behavior without a new public base type.

Removed duplicated flags, Rent/Clear/Dispose/finalizers and the list's redundant
direct Clear before the release callback. Thin Release overrides remain to
preserve original named parameters and typed null-ignore behavior; the generic
base retains null rejection. Disposal is checked first. The generic base now
implements IDisposable and owns inherited cleanup. Existing type arity,
constraints, protected construction and virtual rental/release/cleanup slots
remain. ObjectDisposedException names identify the actual owner, preserving
Queue/PackedSet expectations and correcting List's old `SwiftList` name to
`SwiftListPool`. Authored pool source drops from 1229 to 712 lines including
comments (net -517); one internal helper file and no public type were added.

See [shared lifecycle](../../src/SwiftCollections/Pool/SwiftPoolLifecycle.cs),
[collection base](../../src/SwiftCollections/Pool/Default/SwiftCollectionPool.cs),
[sparse-map owner](../../src/SwiftCollections/Pool/Default/SwiftSparseMapPool.cs),
[regressions](../../tests/SwiftCollections.Tests/Pool/SwiftPoolLifecycle.Tests.cs)
and [lifetime guidance](../api/guides/pools-and-observables.md#scope-a-rental).

**Verification:** The initial 55-case group produced 25 behavioral failures and
30 passing controls on unfixed code. All final 58 cases pass in Standard, Lean
and Debug. They cover fresh/initialized disposal, all seven families, Get and
backing access, base-reference dispatch, null handling, borrowed values across
Clear, nonallocating unused cleanup, reusable Flush and prevention of revival,
and custom generic subclasses with single-reset and diagnostic-name checks.
The old reflection-dependent list test was replaced by public-behavior checks;
the focused pool suite passes 122 tests. Independent source/test reviews pass.

| Final configuration | Core tests | Companion tests | Core lines / branches / executable methods | Companion lines / branches / executable methods |
| --- | ---: | ---: | --- | --- |
| Release | 1931 | 43 | 6021/6021; 2858/2858; 1258/1258 | 121/121; 62/62; 35/35 |
| ReleaseLean | 1824 | 43 | 5827/5827; 2782/2782; 1217/1217 | 121/121; 62/62; 35/35 |
| Debug | 1929 | 43 | 7949/7949; 2866/2866; 1258/1258 | 155/155; 62/62; 35/35 |

The unchanged strict gate passes with 100% reported lines, branches and
executable methods using fresh assembly-specific Coverlet reports. An earlier
Debug gate caught an unreachable sequence point following a throwing guard;
cold name formatting was separated from the guard without exclusions or gate
changes. All builds succeed with zero errors and no skipped tests. Final Release
reports zero warnings; Lean/Debug retain only the pre-existing xUnit2013 warning
in unchanged `SwiftSlotMap.Tests.cs:138`, already confirmed against committed
source under SC-Issue-016. No introduced failure remains.

Both runtime assemblies build for `netstandard2.1` and `net10.0` in Standard/Lean;
all four packages' eight DLL/XML pairs match build output. Emitted XML verifies
the base constructor, lifecycle contracts, exceptions, property values and all
six original Release parameter names. Clean DocFX reports zero warnings/errors;
rendered base/sparse contracts, typed Release parameters and inherited lifecycle
links pass inspection. CI site smoke/link checks pass (104 HTML pages, 1318 local
href targets). Contributor guidance records mutable state, static callbacks and
lifetime rules. CI policy is unchanged. Hosted CI and Unity were not rerun.

**Compatibility and allocation evidence:** A console caller compiled against
the unfixed Standard assembly runs unchanged with only the fixed core DLL
substituted. It exercises all six typed constructors/Rent/Get/Release/Clear/Flush/
Dispose paths, named Release arguments and a custom generic subclass's protected
construction, null rejection and Flush. It additionally detects the new inherited
IDisposable contract and verifies terminal behavior. Hash checks confirm both
the unchanged caller and loaded replacement DLL. Final Lean runs the same source
against its own dependencies; it is not a claim about an old Lean binary.

Allocation controls use blocking GC, `DOTNET_TieredCompilation=0`, eight
constructor/disposal warmups, 128 measured owners, and 1000 warmups plus 10000
rent/return cycles. List and sparse-map owner construction drops from 112 to
104 bytes each. Initialized list disposal drops from 80 to zero allocated bytes;
sparse-map disposal remains zero. Warm list and sparse-map rent/return each
remain 320000 bytes per 10000 cycles (existing ConcurrentStack push allocation).
Final Standard and Lean agree. An earlier tiered first-slice probe showed 24
extra aggregate bytes; it was not used for acceptance, and controlled before/
after workloads match. No latency, universal allocation parity or zero-allocation
rental claim is made; no maintained pool benchmark harness was added.

**API/lifecycle dispositions:** Keep existing collection constraints and sparse-map
API shape, with shared internal state rather than a new public hierarchy. Typed
returns ignore null; generic-base returns reject it. Keep Flush reusable and
Dispose terminal, with no-op cleanup after disposal and guards before lazy
access. Custom subclasses with their own disposal must chain `base.Dispose()`;
adding IDisposable to the base does not retrofit an old custom disposal body.
The frozen caller does not establish terminal behavior for such an old binary.
Reopen that integration decision for a specific external cleanup compatibility
requirement. Array-pool bucket clearing remains separate. Lifecycle operations
and counters still require external synchronization; arbitrary custom overrides
and callbacks own their contracts. Shared singleton identities are not recreated.

- **Disposition / reopening:** Close the confirmed defect. Reopen for a supported
  rental/backing-access route that revives a disposed owner, unused cleanup that
  initializes storage, lost finalizer cleanup, or a named-argument/binary
  compatibility regression in the preserved built-in API surface.

### SC-Issue-018 - Duplicate dictionary Add emits a false change notification

- **Status / priority:** Resolved 2026-11-21 / P2; verified fix.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`;
  reproduced before this fix at `5d9e3c1932e59749694e5ec2f46c8d46f6db371c`.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10,
  published dependencies (`UseLocalLsfStack=false`,
  `DisableTransitiveProjectReferences=false`).

**Portable reproducer** (use the console setup above):

```csharp
var d = new SwiftCollections.Observable.SwiftObservableDictionary<int, int>();
int events = 0;
d.CollectionChanged += (_, _) => events++;
d.Add(1, 10);
bool added = d.Add(1, 20);
Console.WriteLine($"{added}; {events}; {d[1]}");
```

Before: `False; 2; 10`. After: `False; 1; 10`; only the successful insertion
produces a collection event, and the duplicate produces neither notification.

**Root cause and resolution:** The observable Add override emitted both events
unconditionally after base Add returned false. It now emits only inside the
successful-result branch, retaining exactly one base Add call and no preliminary
lookup. All six dispatch forms reach that override: concrete/base Boolean Add,
public pair Add, generic IDictionary, ICollection of pairs and non-generic
IDictionary. Successful insertion still commits before CollectionChanged(Add),
then PropertyChanged(Entries[]); subscriber failure can interrupt notification
without rollback. Equal-key rejection preserves the stored key/value and Count,
but base load-threshold checking may resize before lookup/validation and invalidate
enumerators. The fix deliberately preserves that behavior. No helper, field,
public API or persistence format was added or changed. Raw-state and versioned
JSON/MemoryPack reconstruction retain their notification-free restore paths.
See [implementation and contracts](../../src/SwiftCollections/Observable/SwiftObservableDictionary.cs),
[regressions](../../tests/SwiftCollections.Tests/Observable/SwiftObservableDictionary.Tests.cs)
and the [observable guide](../api/guides/pools-and-observables.md#observable-collections).

**Verification:** The 21-case Standard regression group produced eleven
behavioral failures and ten passing controls before correction. All 21 Standard /
20 Lean cases now pass; the focused dictionary suite passes 43 tests. Cases cover
all six dispatch forms, exact sender/payload/order and callback-visible storage,
case-insensitive equal keys and canonical key retention, throwing subscribers
that must not run on rejection, invalid keys/types and hash failures, subscriber
failure after real insertion, spare-capacity enumeration, threshold resize/
invalidation, and JSON/MemoryPack continuation. Independent source/test reviews
pass. The old misleading duplicate-Add test was renamed: it actually tested an
unchanged indexer assignment.

| Final configuration | Core tests | Companion tests | Core lines / branches / executable methods | Companion lines / branches / executable methods |
| --- | ---: | ---: | --- | --- |
| Release | 1952 | 43 | 6022/6022; 2860/2860; 1258/1258 | 121/121; 62/62; 35/35 |
| ReleaseLean | 1844 | 43 | 5828/5828; 2784/2784; 1217/1217 | 121/121; 62/62; 35/35 |
| Debug | 1950 | 43 | 7952/7952; 2868/2868; 1258/1258 | 155/155; 62/62; 35/35 |

All solution builds succeed with zero errors and no skipped tests. The unchanged
strict gate passes with 100% reported lines, branches and executable methods,
using both projects' existing Coverlet settings. Final acceptance reruns use the
exact DLL/PDB/XML payloads extracted from fresh nupkg/snupkg files for Standard
and Lean, and the built Debug inputs. Before/after hashes verify that coverage
restores those inputs unchanged. Initial auto-pack artifacts and earlier test
copies differed from later build outputs; they were not used for final package
acceptance. Explicit `dotnet pack --no-build --no-restore` produced four fresh
packages, and both test projects were rerun without rebuilding against those
payloads. This resolves the input-identity gap without changing CI or runtime code.

Coverage is collected on net10.0 per the test projects. Both runtime assemblies
also build for netstandard2.1 in Standard/Lean. All four packages' eight DLL/XML
pairs match the final build outputs; core Add parameters/returns/exceptions/
remarks are checked in all four core XML files. Additional netstandard2.1 core
replays run on .NET 10 with matching framework-specific dependency DLLs; a first
manual attempt mixing net10 MemoryPack with netstandard types was rejected as
an invalid dependency combination and corrected before acceptance. These replays
are not Unity execution or a claim about every consumer runtime.

Clean DocFX reports zero warnings/errors. Rendered Add parameters, returns,
exceptions, ordering/invalidation text and guide pass inspection; CI site smoke/
link checks pass (104 HTML pages, 1318 local href targets). Release/Lean final
build logs have zero warnings; Debug retains the pre-existing xUnit2013 warning
in unchanged `SwiftSlotMap.Tests.cs:138`, confirmed previously under SC-Issue-016.
No introduced build/test/coverage/reference failure remains. Hosted CI and Unity
were not rerun.

**Allocation checks:** Hash-checked before/final probes use blocking GC,
`DOTNET_TieredCompilation=0`, 1000 warmups and 10000 measured additions. Integer
keys/values and spare capacity isolate notification cost. Rejected additions
without subscribers drop from 960000 to zero allocated bytes; with collection
and property subscribers they drop from 1200000 to zero and from 20000 callback
invocations to none. Successful additions retain 960000/1200000 bytes and
0/20000 callbacks respectively. The success workload restores 16384 unique pairs,
clears while retaining capacity, warms 1000 inserts then clears again before
measurement, avoiding growth during 10000 measured inserts. Final Standard/Lean
net10 and matching-dependency netstandard replays agree. No universal allocation
or latency claim is made. No maintained benchmark harness was added.

- **Disposition / reopening:** Close the false-notification defect. Reopen for a
  supported Add route that emits on rejection, loses successful notification
  order/payload or bypasses the observable override. Preserve base resize and
  subscriber-failure semantics. The separately reproduced object-adapter
  exception misclassification remains open as
  [SC-Issue-022](#sc-issue-022---object-dictionary-adapters-misclassify-callback-invalid-casts).

**Related API decision — notification coverage:** Dictionary indexer assignments
now dispatch through the observable override under
[SC-Issue-013](#sc-issue-013---base-indexer-writes-bypass-bidirectional-bookkeeping),
including base and dictionary-interface calls. The observable list indexer
remains hidden; direct storage writes and inherited non-overridden mutations
can still bypass notifications. Disposition: document the concrete notification
surface, as described in the
[observable guide](../api/guides/pools-and-observables.md#observable-collections).
Reopen if an API promises complete notifications; first verify base/interface
dispatch and subscriber-exception behavior. This limitation is separate from
the confirmed false Add notification above.

### SC-Issue-019 - BVH duplicate and null-key policy differs from peer indexes

- **Status / priority:** Resolved 2026-11-21 / P2; user-approved upsert contract and verified fix.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`;
  reproduced before this fix at `b5234b3f67720f2d887e703f0ede396161f9adf1`.
- **Affected owners / impact:** SwiftBVH inserts duplicate leaves and accepts null keys that guarded lookups cannot address; hash/octree duplicate insertion instead updates bounds.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
var b = new SwiftCollections.Query.BoundVolume(
    System.Numerics.Vector3.Zero, System.Numerics.Vector3.One);
var t = new SwiftCollections.Query.SwiftBVH<int>(4);
t.Insert(1, b); t.Insert(1, b);
var hits = new List<int>(); t.Query(b, hits);
Console.WriteLine($"{t.Count}; {string.Join(",", hits)}");
var n = new SwiftCollections.Query.SwiftBVH<string>(4);
n.Insert(null!, b);
Console.WriteLine(n.Count);
// n.FindEntry(null!) and n.Remove(null!) throw ArgumentNullException.
```

Before: `2; 1,1` and 1. Both inserts return true and null insertion succeeds.
After: `1; 1` and null insertion throws `ArgumentNullException` with parameter
`value`, leaving Count zero. The first insert returns true, the second false.

**Root cause and resolution:** Generic BVH Insert allocated a leaf before checking
its shared key map. The map deliberately supplies low-level bucket storage, not
uniqueness enforcement. BVH now guards null and looks up the key before mutation.
An existing key uses the same private index-based bounds update as public
UpdateEntryBounds, avoiding a second key lookup. The original stored key, leaf
index, topology, node-array capacity and Count are retained; changed bounds
refresh ancestors and equal bounds do nothing. New keys follow the existing
allocation/insertion path. Numerics, typed backends and FixedMathSharp inherit
the fix. No fields, helper types, public signatures or persistence formats changed.
Volume/hash/allocation exceptions remain nontransactional; null rejection alone
is guaranteed to precede mutation. BVH has no authored serialization/restore path.

**Disposition and compatibility:** The user explicitly selected the same upsert
contract as spatial hash and octree on 2026-11-21. This replaces the earlier
deferred choice. Duplicate-key multi-entry ownership is intentionally unsupported;
callers needing multiple leaves must supply distinct stable keys. False means an
existing entry was updated, not that the call did nothing. Equal reference keys
retain the original canonical stored key. The unreleased migration guide records
this behavior change without claiming publication. Reopen only for an explicit
multi-entry requirement, a supported consumer relying on the former policy, or
an invariant/behavior regression under the selected contract.

**Regression evidence:** The old DuplicateKeyHandling_ReplacesOldEntry queried
only the second non-overlapping region and passed with an orphaned old leaf.
The strengthened test checks old/new/all regions, Count, result flags, stable
leaf/storage identity, ordinary/buffered query equivalence, repeated removal and
reinsertion, both root-only and multi-leaf trees, and same/moved bounds. Further
cases cover null snapshots before mutation, colliding equal reference keys,
canonical retention, Clear/reuse, key zero/default point bounds, custom typed
volumes, fixed generic/wrapper paths and warmed allocation. All 9 core and 3
companion `KeyPolicy_` cases failed against unfixed source for the intended
return/null/ownership behavior. After the fix, the `FullyQualifiedName~SwiftBVH`
focused suites passed 59 core and 7 companion tests.

**Full validation:** Windows x64, SDK 10.0.302/runtime 10.0.10, package mode
(`UseLocalLsfStack=false`, `DisableTransitiveProjectReferences=false`). From the
repository root, run `dotnet build SwiftCollections.slnx -c CONFIG` with those
properties, then each test project with `dotnet test tests/PROJECT/PROJECT.csproj
-c CONFIG --no-build --collect:"XPlat Code Coverage" --settings
tests/PROJECT/coverlet.runsettings --results-directory FRESH_RESULTS` and the
same properties. Run `.assets/scripts/assert-full-coverage.ps1 -CoverageDirectory
FRESH_RESULTS` after both projects; use a separate fresh directory for each
configuration. The unchanged strict gate passed, including every reported point.

| Configuration | Core tests | Companion tests | Core lines | Core branches | Core executable methods | Companion lines / branches / methods |
| --- | ---: | ---: | --- | --- | --- | --- |
| Release | 1960 | 46 | 6029/6029 | 2862/2862 | 1259/1259 | 121/121; 62/62; 35/35 |
| ReleaseLean | 1852 | 46 | 5835/5835 | 2786/2786 | 1218/1218 | 121/121; 62/62; 35/35 |
| Debug | 1958 | 46 | 7961/7961 | 2870/2870 | 1259/1259 | 155/155; 62/62; 35/35 |

No tests failed or were skipped, and no exclusions or coverage/CI policy changed.
The focused red command uses `--filter FullyQualifiedName~KeyPolicy_` before the
runtime fix; the green command uses `--filter FullyQualifiedName~SwiftBVH` after
it. Build warnings are pre-existing in untouched committed test code:
`xUnit2013` at SwiftSlotMap.Tests.cs:138 (all configurations) and `xUnit1026` for
the unused `binary` theory parameter at SwiftObservableDictionary.Tests.cs:220
(Lean only). No introduced build errors or warnings were observed.

**Downstream compatibility:** Source review and focused coordinated-source runs
cover the real BVH consumers. Gravitas PhysicsMesh clears each triangle tree and
inserts unique integer triangle indices; GridForge GridSpatialIndex rejects
already-owned grid IDs, and sparse voxel storage inserts unique owned Voxel
references. These callers ignore Insert's Boolean result and do not depend on
multiple equal leaves. Both test output directories were hash-checked against
the exact source-built core and companion DLLs before returning to package mode.

- GridForge `24ca0f51600c3f1ade727bc1e57ac85d2b22c2b3`: filter
  `FullyQualifiedName~GridSpatialIndexTests|FullyQualifiedName~SparseVoxelGridTests`,
  56/56 Release and 56/56 ReleaseLean.
- Gravitas `556d4051e19560720231a58b5c5b79c6ab9fc22d`: filter
  `FullyQualifiedName~PhysicsMesh|FullyQualifiedName~MeshTriangleQueryAllocationTests|FullyQualifiedName~MeshAuthored`,
  268/268 Release and 267/267 ReleaseLean.

For either consumer, run `dotnet test tests/OWNER.Tests/OWNER.Tests.csproj
-c CONFIG -f net10.0 -p:UseLocalLsfStack=true
-p:DisableTransitiveProjectReferences=true -m:1 --filter FILTER` from its checkout.
Sibling source revisions were Chronicler
`0281469ecc40e65a05867f473bc10f030e7f69f8` and FixedMathSharp
`b501340558c6132484be2f40957233c1b3d29cab`. Existing consumer workflow/guidance
edits and GridForge's staged AGENTS changes were preserved; no consumer runtime
source or test changes were needed. These are focused integration checks, not a
claim of full consumer-suite coverage, hosted CI readiness or a package release.

**Targets, packages and reference:** The full solution built both runtime
assemblies for `netstandard2.1` and `net10.0` in Release/ReleaseLean (and Debug).
Explicit `dotnet pack src/OWNER/OWNER.csproj -c CONFIG --no-build --no-restore`
with package-mode properties produced SwiftCollections, SwiftCollections.Lean,
SwiftCollections.FixedMathSharp and SwiftCollections.FixedMathSharp.Lean. All
eight assembly/target/variant DLL and adjacent XML pairs matched the final build
outputs byte-for-byte; the coverage test DLL inputs also matched those outputs.
All packaged XML parsed, and Insert parameter names, false-return text and null
exception were checked in all four core XML variants. Fresh DocFX metadata/site
built with `dotnet tool run docfx docs/api/docfx.json --warningsAsErrors`, zero
warnings/errors. Generic BVH and both wrapper pages show the new key contract;
Insert, Count, Query and inherited member links were inspected. All 1,624 local
href targets across 104 generated HTML pages resolved.

**Allocation and bounded workload check:** The maintained custom-volume test
warms 1,000 alternating duplicate-bound updates, then measures another 1,000
actual Insert calls: zero thread allocations, no newly inserted leaf, Count two.
It passes in all three configurations. Existing BvhWorkloadBenchmarks.SwiftBVH_Insert
ran all three sizes (1,024 / 4,096 / 16,384 entries) under BenchmarkDotNet 0.15.8,
one launch, one warmup and three measured iterations, Windows x64/.NET 10.0.10.
Means were 511.2 us / 2.1681 ms / 11.4320 ms per newly constructed/populated tree,
with 336.72 / 1,347.2 / 5,377.1 KiB allocated per tree. All three children exited
zero and provided all nine expected measured results. This is current-workload
evidence only; no baseline latency comparison or universal allocation claim is
made. The new-key path adds the required key lookup; duplicate updates reuse its
found index without a second lookup. No scan or additional per-entry storage
was added.

The first benchmark attempts could not generate a child because old ignored
baseline snapshots supplied duplicate project names. Their two project files
were temporarily hidden during the accepted run and restored afterward; no
maintained benchmark/build infrastructure changed. Those incomplete attempts
are excluded from performance evidence. No source defect was inferred from
that local artifact collision. Hosted CI, Unity/IL2CPP, other OS/architectures and
full downstream suites were not run. No release is claimed.

### SC-Issue-020 - Numerics octree subdivision overflows finite midpoints

- **Status / priority:** Resolved 2026-11-21 / P2; verified fix.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`;
  reproduced before this fix at `948fca0f3791430c4da3b1bce317e4bc37d6ac0b`.
- **Affected owners / impact:** SwiftOctree<TKey> built-in numerics partitioner creates infinite, nonshrinking children from ordered finite endpoints near 1e38f..3e38f.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
var world = new SwiftCollections.Query.BoundVolume(
    new System.Numerics.Vector3(1e38f), new System.Numerics.Vector3(3e38f));
var t = new SwiftCollections.Query.SwiftOctree<int>(world,
    new SwiftCollections.Query.SwiftOctreeOptions(2, 1), 1f);
var point = new SwiftCollections.Query.BoundVolume(
    new System.Numerics.Vector3(1e38f), new System.Numerics.Vector3(1e38f));
t.Insert(1, point); t.Insert(2, point);
var root = typeof(SwiftCollections.Query.SwiftOctree<int,
    SwiftCollections.Query.BoundVolume>).GetField("_root",
    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
    .GetValue(t)!;
var children = (Array)root.GetType().GetProperty("Children")!.GetValue(root)!;
var child = children.GetValue(0)!;
Console.WriteLine(child.GetType().GetProperty("Bounds")!.GetValue(child));
```

Before: the first child has Min=1e38 and Max=+Infinity on every axis. Its first
grandchild has the same nonshrinking bounds and stores both entries. After: the
first child has Min=1e38 and finite Max near 2e38 on each axis; deeper children
remain inside their parents and strictly shrink. The reproducer is bounded to
depth two and does not depend on exhausting memory or stack depth.

**Root cause and resolution:** The built-in Numerics partitioner computed child
size with `(Max-Min)/2` but constructed and selected children with `(Min+Max)/2`.
Single-precision sums and differences can overflow despite finite endpoints.
Parent half-size also ignores asymmetric rounding of the actual split plane:
adjacent or unevenly spaced endpoints can produce an endpoint-equal midpoint or
one child below the configured minimum edge length.

One private midpoint helper now widens endpoints before addition and halves in
double precision, then rounds the shared split plane to float. CanSubdivide,
CreateChildBounds and TryGetContainingChildIndex use that same calculation. A
private per-axis predicate checks both resulting child spans in double precision
against the finite positive minimum size. Both halves must pass on every axis;
zero-width and endpoint-equal splits are refused. No public signature, field,
helper type or persistence format changed. Runtime growth is two private helper
methods within the existing owner, reused by its production partition operations.

**Scope and numeric contract:** Ordered finite endpoints are still required;
this does not add validation of malformed/nonfinite worlds. Split planes are
float values; span comparisons are double-precision calculations, not arbitrary-
precision real-distance guarantees. Subdivision no longer needs finite float
endpoint sums or parent differences. BoundVolume.Center, Size and Volume retain
their independent arithmetic and overflow contracts. Generic custom partitioners
and the fixed-point partitioner are unchanged; existing fixed full-domain tests
remain controls. Source search found no octree runtime callers in GridForge,
Gravitas or Trailblazer: their current index consumers use other owners, so no
consumer source change or consumer test-suite claim is made here. The corrected
rounding/minimum-size behavior may change topology and traversal order for
previously overflowing or undersized splits; no sorted query-order guarantee is
introduced. Octrees have no authored serialization/restore contract.

**Regression evidence:** 17 `Midpoint_` cases ran against the unfixed owner:
12 failed for the intended invalid-child/minimum-size behavior and 5 controls
passed. Cases cover positive/negative same-sign extremes, the complete finite
float range, all eight child octants at two levels, finite endpoints, containment,
strict progress, actual split boundaries, query/update/removal/reinsertion,
per-axis adjacent floats, odd-ULP spans, asymmetric subnormal rounding,
zero-width axes, overflowing parent spans and a valid subnormal split. The
independent containment/progress/hit checks supplement the expected split-plane
oracle. After the fix, `FullyQualifiedName~SwiftOctree` passed 52 core tests and
15 companion tests, including existing custom/fixed partitioner controls.

**Full validation:** Windows x64, SDK 10.0.302/runtime 10.0.10, published
package dependencies (`UseLocalLsfStack=false`,
`DisableTransitiveProjectReferences=false`). Run `dotnet build
SwiftCollections.slnx -c CONFIG` with those properties, then each test project
with `dotnet test tests/PROJECT/PROJECT.csproj -c CONFIG --no-build
--collect:"XPlat Code Coverage" --settings tests/PROJECT/coverlet.runsettings
--results-directory FRESH_RESULTS` and the same properties. After both projects,
run `.assets/scripts/assert-full-coverage.ps1 -CoverageDirectory FRESH_RESULTS`.
Use fresh, distinct result directories per configuration. The unchanged strict
gate passed every reported line, branch and executable method:

| Configuration | Core tests | Companion tests | Core lines | Core branches | Core executable methods | Companion lines / branches / methods |
| --- | ---: | ---: | --- | --- | --- | --- |
| Release | 1977 | 46 | 6035/6035 | 2858/2858 | 1261/1261 | 121/121; 62/62; 35/35 |
| ReleaseLean | 1869 | 46 | 5841/5841 | 2782/2782 | 1220/1220 | 121/121; 62/62; 35/35 |
| Debug | 1975 | 46 | 7971/7971 | 2866/2866 | 1261/1261 | 155/155; 62/62; 35/35 |

No failed/skipped tests, coverage exclusions or gate/CI policy changes. The red
command adds `--filter FullyQualifiedName~Midpoint_` before fixing runtime source;
the green command uses `--filter FullyQualifiedName~SwiftOctree` after the fix.
Release build reported no warnings. Lean reported the two pre-existing warnings
in untouched committed tests: xUnit2013 at SwiftSlotMap.Tests.cs:138 and
xUnit1026 for unused `binary` at SwiftObservableDictionary.Tests.cs:220. Debug
reported the same slot-map warning. No introduced build warning/error was found.

Both assemblies built `netstandard2.1` and `net10.0` in all three configurations.
Explicit package-mode `dotnet pack src/OWNER/OWNER.csproj -c CONFIG --no-build
--no-restore` produced SwiftCollections, SwiftCollections.Lean,
SwiftCollections.FixedMathSharp and SwiftCollections.FixedMathSharp.Lean. Each
of the eight assembly/target/variant DLL and XML pairs matched final build
outputs byte-for-byte, and the DLLs used by the coverage suites still matched
those same outputs. All packaged XML parsed. Fresh DocFX metadata/site generation with `dotnet tool run docfx
docs/api/docfx.json --warningsAsErrors` passed with zero warnings/errors. The
rendered Numerics octree page shows widened subdivision, both child spans,
constructor parameter names and the existing size exception. Emitted constructor
XML was checked in all four core target/variant combinations; the guide also
renders the corrected contract. All 1,624 local href targets across 104 generated
HTML pages resolved.

**Bounded performance checks:** A disposable optimized net10.0 caller used the
same extreme positive world, two keys at opposite endpoint points, depth two,
capacity one, merging disabled and a caller-owned List<int> of capacity two.
After 2,000 alternating key-1 endpoint updates and lower-point queries, 10,000
measured update/query cycles allocated zero thread bytes in Standard and Lean;
all updates returned true, lower queries alternated exactly [1]/empty, and
Count remained two. Blocking GC was used; loaded core SHA256 values were
`FE0521DD85A743E46DE3437D545F8F7BA56533D04F0A08BF6053D76B09548F8E`
(Standard) and
`A9B7F7C5E4DB4ADAF7800AAE7E9EBA5B5BF1C0F82040A086BB9ABFA6E946A600`
(Lean). This is allocation evidence for that warmed, retained-topology workload,
not an allocation guarantee for subdivision/merging or arbitrary backends.

The existing QueryCollectionComparisonBenchmarks.DynamicNeighbor_SwiftOctree
workload completed both sizes (2,048 / 8,192 objects) under BenchmarkDotNet
0.15.8: one launch, one warmup, three measured iterations, invocation count one.
Both children exited zero with all six expected measured results. Means were
3.087 / 5.657 ms; reported allocations were zero / 336 bytes. The short iterations
raised MinIterationTime warnings and had wide confidence intervals. These are
workload-execution results only, not a latency comparison, regression attribution
or zero-overhead claim. Old ignored baseline project copies were temporarily
hidden to avoid the known local BenchmarkDotNet project-name collision and
restored afterward; no maintained benchmark infrastructure changed.

Hosted CI, Unity/IL2CPP, other architectures/OSes and downstream suites were not
run; no release or cross-platform floating-point determinism is claimed.

**Disposition / reopening:** Close the confirmed defect. Reopen for an ordered
finite world that creates nonfinite/out-of-parent or nonshrinking children,
violates the documented computed minimum-size test, or routes an entry into a
child that does not contain it. Reassess separately if cross-platform floating-
point reproducibility or exact-real span admission becomes a supported contract.

### SC-Issue-022 - Object dictionary adapters misclassify callback invalid casts

- **Status / priority:** Resolved 2026-11-21 / P2; verified fix, separate from SC-Issue-018.
- **Discovered / revision:** 2026-11-21 at
  `5d9e3c1932e59749694e5ec2f46c8d46f6db371c`; reproduced before and after the
  notification fix in Standard/Lean, including matching-dependency netstandard
  replays on .NET 10. Reproduced again before this fix at
  `b5e03e40130c95879b7936ceafec1baf0d57329a`.
- **Affected members:** `SwiftDictionary<TKey, TValue>.Add(object, object)` and
  the non-generic IDictionary indexer setter. Their InvalidCastException catches
  include virtual mutation, not just argument conversion, so derived notification
  code can be misclassified as a bad key/value argument.
- **Environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with
  published dependencies (`UseLocalLsfStack=false`).
- **Caller impact:** A valid insertion/replacement commits, then the caller sees
  an argument-type error rather than the subscriber failure. The message falsely
  describes a correctly typed value as incompatible and discards the original
  exception instead of retaining it as InnerException. Retrying under an
  assumption that validation rejected the mutation is unsafe.

**Portable reproducer** (use the console setup above):

```csharp
foreach (bool replace in new[] { false, true })
{
    var d = new SwiftCollections.Observable.SwiftObservableDictionary<int, int>();
    if (replace) d.Add(1, 10);
    var original = new InvalidCastException("subscriber");
    d.CollectionChanged += (_, _) => throw original;
    try
    {
        if (replace) ((System.Collections.IDictionary)d)[1] = 20;
        else ((System.Collections.IDictionary)d).Add(1, 10);
    }
    catch (Exception error)
    {
        Console.WriteLine($"{replace}; {error.GetType().Name}; " +
            $"{ReferenceEquals(original, error)}; {d.Count}; {d[1]}");
        Console.WriteLine(error.Message);
    }
}
```

Expected: the original InvalidCastException propagates after the documented
committed mutation. Observed: `False; ArgumentException; False; 1; 10` and
`True; ArgumentException; False; 1; 20`. Messages say `Value 10 does not match
expected System.Int32` and likewise for value 20. The original is not retained as
InnerException. A direct typed Add control propagates the same original
InvalidCastException instance and also leaves the mapping committed.

After: `False; InvalidCastException; True; 1; 10` and
`True; InvalidCastException; True; 1; 20`. Both retain the original `subscriber`
exception and the same committed mapping. Typed controls behave the same way.

**Root cause and resolution:** Both object adapters enclosed virtual mutation
in the value-conversion catch, nested inside a key-conversion catch. The shared
private static ConvertObjectPair helper now performs only the existing input
conversion sequence: null-value guard, key cast, then value cast. Each catch
surrounds only its own cast. Add and the explicit IDictionary setter invoke
virtual mutation after the helper returns, outside both catches. This also
prevents an InvalidCastException from a malformed value's error-message ToString
from being relabeled as a key conversion error.

There is one helper with two production consumers, no new fields/types or
intermediate tuple/pair objects. Typed mutation, notification order, duplicate
semantics, serialization/restore and public signatures are unchanged. Existing
validation order, messages and exception types are preserved: disallowed null
values are checked first; a null non-nullable value-type key still fails
unboxing with NullReferenceException, and reference null keys still reach typed
validation. Nullable/reference null values remain valid. Exceptions from
comparers, overrides and subscribers now propagate unchanged. Failure is not a
rollback guarantee: notification failure follows committed mutation, and
existing load-threshold checks may resize before a later typed failure.

**Regression evidence:** The `FullyQualifiedName~ObjectAdapter_` filter ran
28 cases against unfixed source: 18 failed for the intended exception type/identity
mismatch and 10 controls passed. Tests cover public object Add, non-generic
IDictionary.Add and indexer insertion/replacement; collection/property subscriber
failures and notification interruption; forward hash/equality comparers;
bidictionary reverse comparers; value formatting; exact validation precedence,
messages, nullable/reference nulls and non-nullable key unboxing. The assertions
distinguish pre-mutation comparer failures with spare capacity from committed
subscriber failures and retain typed controls. After the fix, the focused
SwiftDictionaryTests/SwiftObservableDictionaryTests/SwiftBiDictionaryTests filter
passed all 219 cases.

**Full validation:** Windows x64, SDK 10.0.302/runtime 10.0.10, published
package mode (`UseLocalLsfStack=false`, `DisableTransitiveProjectReferences=false`).
From the repository root, run `dotnet build SwiftCollections.slnx -c CONFIG`
with those properties. Run each test project with `dotnet test
tests/PROJECT/PROJECT.csproj -c CONFIG --no-build --collect:"XPlat Code Coverage"
--settings tests/PROJECT/coverlet.runsettings --results-directory FRESH_RESULTS`
and the same properties, then `.assets/scripts/assert-full-coverage.ps1
-CoverageDirectory FRESH_RESULTS` after both projects. Fresh configuration-specific
results passed the unchanged strict raw-count gate:

| Configuration | Core tests | Companion tests | Core lines | Core branches | Core executable methods | Companion lines / branches / methods |
| --- | ---: | ---: | --- | --- | --- | --- |
| Release | 2005 | 46 | 6031/6031 | 2858/2858 | 1262/1262 | 121/121; 62/62; 35/35 |
| ReleaseLean | 1897 | 46 | 5837/5837 | 2782/2782 | 1221/1221 | 121/121; 62/62; 35/35 |
| Debug | 2003 | 46 | 7964/7964 | 2866/2866 | 1262/1262 | 155/155; 62/62; 35/35 |

No failed/skipped tests, exclusions or coverage/CI-policy changes. Existing
analyzer warnings remain: xUnit2013 for the unchanged slot-map size assertion
and Lean xUnit1026 for the pre-existing observable round-trip theory's unused
`binary` parameter. The added tests do not alter that theory. No introduced
warning or build error was observed. Full builds include both runtime assemblies
for `netstandard2.1` and `net10.0`, with C# 11 compatibility retained for netstandard.

Explicit `dotnet pack src/OWNER/OWNER.csproj -c CONFIG --no-build --no-restore`
with the package-mode properties produced SwiftCollections, SwiftCollections.Lean,
SwiftCollections.FixedMathSharp and SwiftCollections.FixedMathSharp.Lean. All
eight assembly/target/variant DLL and XML pairs matched final source-build outputs
byte-for-byte. DLL inputs from all six coverage suites still matched those same
outputs. All packaged XML parsed. Fresh DocFX metadata/site generation with
`dotnet tool run docfx docs/api/docfx.json --warningsAsErrors` passed with zero
warnings/errors. The rendered object Add section shows key/value parameters,
existing conversion exceptions and original-exception/no-rollback remarks. The
explicit IDictionary indexer is hidden by the existing site metadata selection; its updated
contract was checked in emitted XML. Both adapters were checked in all four
core target/variant XML files. The observable guide renders the corrected
contract. All 1,624 local href targets across 104 generated HTML pages resolved.

**Warm allocation control:** A disposable optimized net10.0 caller preboxed an
integer key and two values once and reused a capacity-eight SwiftDictionary<int,int>.
Each cycle exercised public object Add/remove, IDictionary.Add/remove, and
IDictionary indexer insert/replace/remove, checking exact typed values and counts.
After 2,000 warmup cycles, 10,000 measured cycles allocated zero thread bytes in
the frozen baseline, current Standard and current Lean, with every value correct
and final Count zero. Blocking GC was used. Loaded core SHA256 values were
`D85F26EACB85C8B26AA0E59E03DEFEAF3297913F75206DE74F02D2A6DB341747`
(baseline), `28244B4BFD01E45D3E6379E49C229DC548D8D4B4F930A2BED938FD29FF6CB30D`
(Standard), and `2E8AB11EA0D205568CA92FC05759545DC8FAA9536F2792CE56BAE796F547A5C9`
(Lean). This excludes boxing performed by callers before entering the adapters,
exception allocation, notifications and resizing; it is not a universal allocation
or latency guarantee. Typed probe/mutation code is unchanged, and no new timing
benchmark or before/after latency claim is introduced.

Hosted CI, Unity/IL2CPP, other platforms and downstream suites were not run;
no release is claimed. The list reproducer remains unchanged after this fix.

**Disposition / reopening:** Close the confirmed dictionary defect. Reopen for
an object dictionary route that still relabels a correctly typed comparer,
override or subscriber InvalidCastException, changes conversion/null precedence,
or bypasses derived mutation rules. The analogous SwiftList owner is separately
tracked as [SC-Issue-023](#sc-issue-023---list-object-adapters-misclassify-callback-invalid-casts);
this fix does not expand list runtime behavior.

### SC-Issue-023 - List object adapters misclassify callback invalid casts

- **Status / priority:** Resolved 2026-11-21 / P2; verified fix, separate owner from SC-Issue-022.
- **Discovered / revision:** 2026-11-21 at
  `b5e03e40130c95879b7936ceafec1baf0d57329a`; reproduced using its frozen core
  DLL while investigating SC-Issue-022. Reproduced before this fix at
  `094e2af216c6f7d137cd09e9e74d68f1e77844fa`.
- **Affected members:** SwiftList<T>'s explicit IList.Add, IList.Insert and
  IList.Remove enclosed virtual mutation in conversion catches; IList.Contains
  and IList.IndexOf also enclosed user equality. Correctly typed callback failures
  were relabeled as NotSupportedException.
- **Environment:** Windows x64, SDK 10.0.302/runtime 10.0.10, Release with
  published dependencies. The dictionary fix in SC-Issue-022 did not change list runtime.

**Portable reproducer** (use the console setup above):

```csharp
foreach (string op in new[] { "Add", "Insert", "Remove" })
{
    var d = new SwiftCollections.Observable.SwiftObservableList<int>();
    if (op == "Remove") d.Add(1);
    var original = new InvalidCastException("subscriber");
    d.CollectionChanged += (_, _) => throw original;
    System.Collections.IList list = d;
    try
    {
        if (op == "Add") list.Add(1);
        else if (op == "Insert") list.Insert(0, 1);
        else list.Remove(1);
    }
    catch (Exception error)
    {
        Console.WriteLine($"{op}; {error.GetType().Name}; " +
            $"{ReferenceEquals(original, error)}; {d.Count}");
    }
}
```

Observed: `Add; NotSupportedException; False; 1`,
`Insert; NotSupportedException; False; 1`, and
`Remove; NotSupportedException; False; 0`. Expected: the original
InvalidCastException instance propagates, with those same committed counts.
Callers cannot safely interpret the current error as rejecting an incompatible
input or leaving the list unchanged.

After: `Add; InvalidCastException; True; 1`,
`Insert; InvalidCastException; True; 1`, and
`Remove; InvalidCastException; True; 0`. Mutation timing and contents are unchanged;
the original subscriber exception is preserved.

**Related lookup reproducer** (same console setup; run independently):

```csharp
var original = new InvalidCastException("equality");
var value = new EqualityFailure(original);
System.Collections.IList list = new SwiftList<EqualityFailure> { value };
foreach (string op in new[] { "Contains", "IndexOf" })
{
    try
    {
        if (op == "Contains") list.Contains(value);
        else list.IndexOf(value);
    }
    catch (Exception error)
    {
        Console.WriteLine($"{op}; {error.GetType().Name}; " +
            $"{ReferenceEquals(original, error)}; {list.Count}");
    }
}
sealed class EqualityFailure(Exception failure) : IEquatable<EqualityFailure>
{
    public bool Equals(EqualityFailure other) => throw failure;
}
```

Both lookups previously printed NotSupportedException/False/1. After the fix,
both print InvalidCastException/True/1; no library mutation occurs. These paths
share the same owner/cause and are included here rather than split into another
issue. Tests also cover an InvalidOperationException from equality.

**Root cause and resolution:** Catch scopes included user mutation/equality,
not just casts. Three mutation adapters now share private ConvertObjectValue,
which catches only InvalidCastException from conversion and preserves their
`Wrong value type for ...` message. Contains and IndexOf share private
ConvertObjectLookupValue, whose cast-only catch-all preserves their separate
`Unsupported value type for ...` policy. Virtual mutation and equality execute
after conversion completes, outside both helpers' catches. There are two private
helpers with five production consumers, no fields/types or intermediate objects,
and fewer duplicated runtime conversion blocks. Typed hot paths, storage layout,
serialization/state restoration and public signatures are unchanged.

**Preserved contracts and scope:** Mutation adapters still let null unboxing to
non-nullable T throw NullReferenceException; lookup adapters still translate it
to NotSupportedException. Reference/nullable null elements remain valid. Insert
converts before index validation. IList.Add still returns final Count minus one
after synchronous callbacks, including -1 after a handler clears the list. This
existing reentrant behavior is documented, not redesigned. Notification failures
follow committed mutation and may interrupt later notifications; failure does
not imply rollback. The non-generic indexer calls the nonvirtual base setter;
its separately documented catch-all/invalid-index behavior and hidden observable
indexer contract are preserved. Array.SetValue copy catches in other owners do
not invoke these user callbacks and remain outside this fix.

**Regression evidence:** 35 new ObjectAdapter_ cases ran on unfixed source:
11 failed for the intended exception type/identity mismatch and 24 controls
passed. The red filter is
`FullyQualifiedName~SwiftListTests.ObjectAdapter_|FullyQualifiedName~SwiftObservableListTests.ObjectAdapter_`.
Cases cover all five adapters, typed controls, both observable notification
stages with sender/payload/order and committed contents, equality failure before
mutation with preserved enumeration, InvalidCast/InvalidOperation lookups,
wrong-type/null conversion messages and precedence, reference/nullable nulls,
invalid insertion indices and reentrant Add results. The post-fix focused filter
`FullyQualifiedName~SwiftListTests|FullyQualifiedName~SwiftObservableListTests`
passed all 189 tests.

**Full validation:** Windows x64, SDK 10.0.302/runtime 10.0.10, published
package dependencies (`UseLocalLsfStack=false`,
`DisableTransitiveProjectReferences=false`). Run `dotnet build
SwiftCollections.slnx -c CONFIG` with those properties, then both test projects
with `dotnet test tests/PROJECT/PROJECT.csproj -c CONFIG --no-build
--collect:"XPlat Code Coverage" --settings tests/PROJECT/coverlet.runsettings
--results-directory FRESH_RESULTS` and the same properties. Run
`.assets/scripts/assert-full-coverage.ps1 -CoverageDirectory FRESH_RESULTS` after
both projects, using fresh directories per configuration. The unchanged strict
gate passed every reported line, branch and executable method:

| Configuration | Core tests | Companion tests | Core lines | Core branches | Core executable methods | Companion lines / branches / methods |
| --- | ---: | ---: | --- | --- | --- | --- |
| Release | 2040 | 46 | 6022/6022 | 2858/2858 | 1264/1264 | 121/121; 62/62; 35/35 |
| ReleaseLean | 1932 | 46 | 5828/5828 | 2782/2782 | 1223/1223 | 121/121; 62/62; 35/35 |
| Debug | 2038 | 46 | 7951/7951 | 2866/2866 | 1264/1264 | 155/155; 62/62; 35/35 |

No failed/skipped tests, coverage exclusions or gate/CI-policy changes. Release
build reported zero warnings/errors. Lean reported the pre-existing xUnit2013
slot-map assertion and xUnit1026 observable-dictionary unused round-trip theory
parameter warnings; Debug reported the slot-map warning. Those source assertions
are unchanged here. No introduced warning or build error was observed.

Both runtime assemblies built `netstandard2.1` and `net10.0` in all three
configurations. Explicit package-mode `dotnet pack src/OWNER/OWNER.csproj
-c CONFIG --no-build --no-restore` produced SwiftCollections,
SwiftCollections.Lean, SwiftCollections.FixedMathSharp and
SwiftCollections.FixedMathSharp.Lean. All eight assembly/target/variant DLL/XML
pairs matched final build outputs byte-for-byte, and the DLLs used by all six
coverage suites still matched those outputs. All packaged XML parsed.
Fresh DocFX metadata/site generation with
`dotnet tool run docfx docs/api/docfx.json --warningsAsErrors` passed with zero
warnings/errors. The observable guide renders the corrected mutation/equality
exception contract. All five explicit IList adapters remain hidden by the existing
site metadata selection; their summaries, parameters, applicable returns,
exceptions and remarks were checked in all four core target/variant XML files.
The Add return text includes post-callback Count minus one. The public list page
and guide links resolve; all 1,624 local href targets across 104 generated HTML
pages passed file-target checks.

**Warm allocation control:** An optimized disposable net10.0 caller preboxed
integer values 10 and 20 and reused a capacity-eight SwiftList<int>. Each cycle
called IList.Add, Insert, Contains, IndexOf and two Removes, checking Add's return,
lookup results, typed contents and empty final Count. After 2,000 warmup cycles,
10,000 measured cycles allocated zero thread bytes in the frozen baseline and
current Standard/Lean, with every result correct. Blocking GC was used. Loaded
core SHA256 values were
`98B3F799EB1A95FE0816C44F56BA13531F9CB9E4D9B1B48EB0D65D8A8D5926A4`
(baseline), `EBC4A304CAB5B5E7D735FF6059DB759611B5CD578015106A3A7A172D22452E5A`
(Standard), and `E6D460F647D2177A3A4BF1A7540F1D91B87B65F0CAE8788AEA74FFAF3FF061F9`
(Lean). Caller boxing, exceptions, observable notifications and capacity growth
are excluded from that workload; no universal allocation or latency claim is
made. Typed list operations are unchanged and no new timing benchmark was added.

Hosted CI, Unity/IL2CPP, other platforms, downstream suites and a latency
comparison were not run. No release is claimed.

**Disposition / reopening:** Close the confirmed list defect. Reopen for a
supported list mutation/lookup route that still relabels a user exception, changes
the preserved conversion/null/index precedence, bypasses derived mutation or
changes post-callback Add results. An indexer-policy redesign requires its own
explicit scope; it is not a remaining callback gap in these five adapters.

### SC-Issue-021 - Persist collection comparers across serializers

<a id="sc-issue-021"></a>

- **Status / priority:** Resolved, implemented and validated 2026-11-20 / P2.
  All five families pass actual standard/Lean High-stripping IL2CPP, Mono and
  hosted Windows/Linux checks. SC-Benchmark-006 subsequently closed by a bounded
  no-source-change decision; its historical native failure remains unexplained.
- **Opened:** 2026-11-19, following the SC-Issue-010 trusted-state contract
  decision and the explicit request for comparer persistence.
- **Scope / impact:** Preserve supported ordering and equality/hash rules before
  collection population in normal direct/nested JSON and standard MemoryPack
  serialization. Cover all collection families, both runtime frameworks
  (`netstandard2.1`, `net10.0`) and standard/Lean package graphs; Lean retains
  JSON without a MemoryPack dependency. Losing comparison metadata can change
  lookup semantics or discard custom-distinct entries.
- **Disposition:** Keep raw-state schemas and their trusted restoration path.
  Freeze strict v1 comparison metadata, typed custom
  registrations/factories and clear
  unsupported/unknown-codec failures at persistence boundaries. Do not add
  registry work to collection hot paths or reinstate the rejected ordering scan.
  SC-Issue-010 remains a historical contract closure, not a persistence fix.
  The user superseded the initial legacy-helper design during pre-commit review:
  owner persistence supports only version one as an intentional breaking change,
  with no legacy reader, migration helper or default fallback. Reopen that
  disposition only for an explicit compatibility requirement and agreed evidence.
- **Phase 0 evidence:** Thirteen legacy families / 52 exact payloads were captured;
  the sorted-list legacy bytes now live directly in executable rejection tests.
  The transport model passes modern and matched standard-2.1 graphs; actual
  Unity 6000.3.9f1 Mono smoke suites pass standard 25/25 and Lean 24/24. Baseline
  solution tests pass core 1578/1543 plus companion 43 each. All 6240 planned
  warm baseline samples completed; these are calibration, not accepted future
  overhead budgets. The plan preserves protocol, factory-recognition, fallback
  and compatibility decisions and their limits.
- **Initial Phase 1 evidence (before pre-commit review):** The actual sorted-list family preserves supported
  rules through strict-v1 direct/nested JSON and standard MemoryPack, with typed
  factories and configured lambda recapture. The initial explicit migration
  helper is removed by the pre-commit decision above. Raw
  state remains copy-only. From baseline
  `bf69e2152dff02f5f9f3ad335afcddf7560b4e81`, the two intended unsupported-capture
  regressions fail before the change and pass afterward. Release/ReleaseLean/Debug
  solution builds pass without warnings; core tests 1702/1632/1700 and companion
  43 each pass with full reachable line/branch/method coverage. Both assemblies
  and XMLs at both TFMs match all four package outputs. Clean-generated DocFX
  and local-path/fragment checks pass; actual Unity 6000.3.9f1 Mono smoke passes
  26 standard / 25 Lean cases using the production standard-2.1 plugins.
  Independent runtime reviews found no remaining correctness issue.
  These counts describe the initial snapshot, including the removed helper;
  the post-review validation below supersedes that snapshot.
- **Performance disposition — 2026-11-20:** The user accepts the measured fixed
  serialization cost and, after the bounded hot-path investigation, explicitly
  accepts the remaining native-context timing uncertainty to close Phase 1.
  [SC-Benchmark-005](benchmark-signal-hardening-backlog.md#sc-benchmark-005---comparer-persistence-hot-timing-acceptance)
  closes as no actionable hot-path regression identified. Preserve adverse
  timings and attribution limits; there is no equal-cycle or universal timing
  equivalence claim. No algorithmic work or warm allocation was added in the
  inspected hot paths, and no repeatable meaningful caller elapsed-time
  regression attributable to the implementation was established. Reopen for an
  attributable representative-caller regression, hot allocation or behavior
  defect. Later families require their own evidence.
- **Pre-commit review:** The user removed the migration helper and selected
  v1-only owner records as an intentional breaking format. Legacy literals now
  test strict rejection. Independent reviews found no remaining correctness
  issue; new tests protect reference identity, factory-buffer isolation and
  segmented configurations. Empty JSON configurations skip 10,928 bytes of
  scratch work; the focused empty-owner decode median improves about 12%, with
  unchanged allocation, without making a hot-path claim. Current
  Release/ReleaseLean/Debug core tests pass 1704/1635/1702, companion 43 each,
  with full reachable line/branch/method coverage and zero build warnings.
  All target/package XML checks and clean reference checks pass; updated Unity
  smoke remains 26/25. The plan preserves the current counts and measurement
  limits. SC-Benchmark-005 was open at that review checkpoint and is now closed
  by the separate performance disposition above.
- **Hot-path isolation:** At `cbd9a734082d7a002699766937b79dc547a8cebb`,
  optimized direct callers and a fixed-binary JIT-policy counterfactual
  demonstrate timing sensitivity to the execution context. Standard token-pair
  cycles change from -6.1% to +9.4% relative to baseline across alignment
  policies; optimized Lean is about -0.1% in both. Longer Standard string churn
  retains +3.4% cycles despite lower wall time. No algorithm defect or justified
  runtime fix was found. All 798 corrected timing/control intervals pass
  behavior, zero timed allocation and no-GC checks. Fresh Release/ReleaseLean
  builds pass both TFMs without warnings; tests pass 1704/1635 core plus 43
  companion each. The benchmark signal preserves inputs, exclusions, denied
  hardware tracing and remaining attribution limits. The user accepts those
  limits for Phase 1; hardware tracing is no longer a closure requirement.
- **Phase 1 closeout:** All Phase 1 tasks are checked in the
  [2026-11-20 closeout](done/2026-11-20-comparer-persistence-plan.md#phase-1-closeout--2026-11-20).
  This documentation-only decision reuses the recorded validation of
  `cbd9a734082d7a002699766937b79dc547a8cebb`; it adds no runtime changes or new
  validation runs. Previous uncommitted benchmark work is preserved.
- **Phase 2 evidence:** Dictionary, HashSet, BiDictionary and ObservableDictionary
  preserve typed equality through shared registry and transport helpers. Fresh
  table construction uses no new entropy; all three bidirectional roles survive
  actual collision fallback and subsequent mutation. Observable replacement is
  private and notification-free. Raw DTOs and ordinary hot loops are unchanged.
  Four initial unsupported-save regressions fail before implementation and pass
  after it. Release/Lean/Debug builds pass both TFMs without warnings; core tests
  pass 1789/1687/1787 and companion 43 each. All reachable coverage is complete;
  the two unreachable probe-exhaustion guards and raw counts are documented.
  All four packages match their assembly/XML outputs, the complete authored core
  XML inventory has no tag gaps, and clean DocFX/rendered link checks pass.
  Unity Mono smoke passes 29/27. Fourteen comparerless families retain 42 exact
  payloads across variants. The final boundary comparison completes 1728 checked
  intervals; descriptor-array allocations were removed and large bidictionary
  decode allocation falls through pre-sized construction. The
  [Phase 2 record](done/2026-11-20-comparer-persistence-plan.md#phase-2-execution-record--2026-11-20)
  preserves inputs, hashes, costs, exclusions and review results.
- **Initial Phase 3 evidence:** Independent old/new readers, all 16 target/variant
  combinations and 32 Windows/Linux exchanges pass; configured captures and
  continuation traces agree across fresh processes. Final Unity Mono checks
  pass both variants with reflection-disabled JSON and standard MemoryPack.
  Full Release/Lean/Debug builds pass; tests are 1791/1688/1789 core plus 43
  companion each, with all reachable coverage. All eight assembly/XML pairs
  match packages; complete authored/compiled inventories and clean rendered
  references pass. The hardening review removes BiDictionary's temporary reverse
  array through shared specialized population, saving 524312 bytes at 65536
  int/int entries. Local large binary medians improve about 2.3%/6.2% for
  Dictionary/BiDictionary; tiny boundary timings remain mixed. Independent
  code/render reviews find no remaining confirmed defect. Both IL2CPP builds
  are blocked by missing MSVC/Windows SDK; no player/AOT success is claimed.
  See the [Phase 3 record](done/2026-11-20-comparer-persistence-plan.md#phase-3-execution-record--2026-11-20)
  for complete inputs, adverse variants, raw counts, exclusions and limits.
- **Final closeout:** Installed MSVC x64/x86 and Windows SDK 10.0.26100 through
  the existing Build Tools installation. Both actual Unity 6000.3.9f1 Windows64
  IL2CPP players pass High stripping with JSON reflection disabled, including
  default/nullable/enum/ordinal and custom rules, generated metadata, recapture
  and continuation. Standard also passes closed registered MemoryPack paths.
  Fresh Release/Lean/Debug local builds/tests pass 1791/1688/1789 core plus 43
  companion each. The five approved commits through `cab616d` are pushed;
  [hosted CI](https://github.com/mrdav30/SwiftCollections/actions/runs/44807840605)
  passes actual tests in all four Windows/Ubuntu Release/Lean jobs. A stale Debug
  source/package restore mismatch is corrected without a source or CI change.
- **Initial reliability disposition:** The user delegated final judgment. Close
  feature implementation acceptance while keeping SC-Benchmark-006 explicitly open for
  release review. The calibrated full capture identifies BiDictionary JSON
  after Dictionary binary IO; two current replays and thirty million BCL-only
  reference-copy controls pass. No root cause or crash fix is claimed. Reopen
  this issue for an attributable collection defect or failed supported contract.
- **Measurement reliability:** One earlier disposable measurement process ended
  with a native access violation. A recovered Windows minidump confirms its
  invocation and fatal CLR exception handling with System.Text.Json callers,
  but that dump's originating managed method is unresolved. Two additional
  larger Phase 3 baseline JSON batches fail in the nested Utf8JsonReader path.
  Replays and independent controls complete without establishing a fix, common
  cause or ownership. [SC-Benchmark-006](benchmark-signal-hardening-backlog.md#sc-benchmark-006---unattributed-native-exit-during-persistence-measurement)
  retains the evidence and subsequent no-source-change closure. Its binary-free
  BiDictionary control reproduces the frozen baseline's native CLR failure;
  both current Standard and both current Lean processes pass. Source review
  finds no attributable defect. No crash fix or source exoneration is claimed.
- **Next action:** No implementation or investigation work remains queued.
  Keep SC-Benchmark-006's historical uncertainty visible in release review and
  reopen for a current-code recurrence, concrete source defect or changed risk
  evidence under its recorded criteria. Implementation acceptance is complete;
  no package release or universal runtime/architecture/AOT guarantee is claimed.
- **Plan / acceptance:** [Comparer persistence plan](done/2026-11-20-comparer-persistence-plan.md)
  owns the inventory, decisions, phases, validation and performance gates.
  The former plan-only fixture directory is removed; maintained tests consume
  the required legacy sorted-list literals directly.

## Issue Template

```markdown
### SC-Issue-NNN - Concise title

- **Status / priority:** Needs reproduction | Confirmed | Planned | Deferred
- **Discovered:** Date and source revision
- **Affected owners / impact:** Source owner and observable risk
- **Evidence / reproduction:** Focused failing case or portable command
- **Next action:** Focused investigation or fix and required verification
```
