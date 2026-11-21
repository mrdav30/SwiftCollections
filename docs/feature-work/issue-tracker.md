# Issue Tracker

## Purpose

Track correctness, determinism, API, documentation, build, tooling and test
reliability concerns owned by SwiftCollections, including its FixedMathSharp
companion, with a compact index of verified resolutions or documented
investigation closures. Keep substantial implementation work in focused
feature-work plans.

## Tracker Rules

- Issue IDs use `SC-Issue-NNN`. The next available ID is `SC-Issue-022`.
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

### SC-Issue-017 - Disposed collection wrappers still rent through Get

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
- **Affected owners / impact:** SwiftCollectionPool.Get bypasses derived disposal gates after Flush replaces the backing lazy pool; ordinary Rent correctly throws.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
var p = new SwiftCollections.Pool.SwiftListPool<int>();
p.Dispose();
using var lease = p.Get(out var list); // succeeds
Console.WriteLine(list.Count);        // 0
// p.Rent() throws ObjectDisposedException.
```

Expected: the lease path follows the same disposal gate as Rent. Dispose/Flush
replaces the lazy backing pool, and inherited Get bypasses the wrapper flag.

- **Next action:** Enforce consistent lifecycle checks in the existing owner and cover all wrappers. SwiftSparseMapPool has its own guarded Get and is unaffected.

### SC-Issue-018 - Duplicate dictionary Add emits a false change notification

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
- **Affected owners / impact:** SwiftObservableDictionary.Add emits Add/Entries[] notifications even when duplicate insertion returns false.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
var d = new SwiftCollections.Observable.SwiftObservableDictionary<int, int>();
int events = 0;
d.CollectionChanged += (_, _) => events++;
d.Add(1, 10);
bool added = d.Add(1, 20);
Console.WriteLine($"{added}; {events}; {d[1]}");
```

Observed: `False; 2; 10`. Expected: one Add event for one successful insertion.
Duplicate rejection should not report an addition that never happened.

- **Next action:** Emit notifications only after a successful insertion and verify all Add dispatch paths.

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

- **Status / priority:** Deferred API decision / P2; preserve current behavior pending the disposition below.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
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

Observed: `2; 1,1` and 1. Duplicate Insert creates separate leaves; null creates
a leaf that guarded lookup/removal cannot address. The existing
`DuplicateKeyHandling_ReplacesOldEntry` test queries only the second
non-overlapping volume, so it does not prove replacement. Spatial hash/octree
instead upsert and return false. **Disposition: deferred API choice; preserve
and document current behavior pending a policy decision.** Choose rejection, upsert, or
explicit multi-entry ownership before runtime changes. Reopen with an agreed
policy and tests of Count, a query intersecting both leaves, repeated removal,
and null insertion before mutation.

- **Next action:** Choose rejection, upsert or multi-entry semantics, then cover Count, both-leaf queries, removal and null validation.

### SC-Issue-020 - Numerics octree subdivision overflows finite midpoints

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
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

Observed: child Min=1e38 and Max=+Infinity on every axis. Its first grandchild
has the same nonshrinking bounds and stores both entries. Expected: finite
children inside the finite parent, or safe subdivision refusal. CanSubdivide
uses (Max-Min)/2, while child construction/selection use (Min+Max)/2, which
overflows here. The bounded probe uses depth two.

- **Next action:** Use a consistent overflow-aware midpoint and verify containment/progress. The fixed-point counterpart uses its backend's safe center. Until fixed, keep endpoint sums and differences finite.

## Resolved Issues

Dates reflect recorded resolution/closure, which can precede the commit date.
Evidence links retain the committed root causes, regressions and validation;
they do not claim a fresh rerun or package release.

| Issue                                                                                          | Resolved/closed | Outcome                                                                                                                                                                                                                                                              | Evidence                                                                                                                                                                                                       |
| ---------------------------------------------------------------------------------------------- | --------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
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
