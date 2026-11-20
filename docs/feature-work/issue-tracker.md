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
- Preserve reachable line, branch and method coverage. Do not conceal gaps with
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

### SC-Issue-012 - Dictionary views retain detached entry arrays

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
- **Affected owners / impact:** SwiftDictionary.Keys/Values copying and membership can use stale arrays while Count reflects the owner.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
var d = new SwiftDictionary<int, int>();
d.Add(1, 10);
var keys = d.Keys; var values = d.Values;
d.EnsureCapacity(64); d.Add(2, 20);
var k = new int[2]; var v = new int[2];
keys.CopyTo(k, 0); values.CopyTo(v, 0);
Console.WriteLine($"{keys.Count}; {string.Join(",", k)}; {string.Join(",", v)}");
```

Observed: `2; 1,0; 10,0`, rather than current keys [1,2] and values [10,20].
Views cache old entries while reading current Count/last index. Value membership
can also read stale storage or run out of range after growth. Expected: consistent
live views.

- **Next action:** Resolve current owner storage in every view operation; verify resize/trim/rehash/Clear and generic/non-generic dispatch.

### SC-Issue-013 - Base-indexer writes bypass bidirectional bookkeeping

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
- **Affected owners / impact:** SwiftBiDictionary hidden indexer cannot protect base-typed writes; forward and reverse lookups disagree.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
var d = new SwiftBiDictionary<int, int>();
d.Add(1, 10);
((SwiftDictionary<int, int>)d)[1] = 20;
Console.WriteLine($"{d[1]}; {d.ContainsValue(10)}; {d.ContainsValue(20)}");
```

Observed: `20; True; False`. Expected: reverse lookup follows the new forward
value.

- **Next action:** Decide an enforceable mutation API including interface dispatch and verify every supported path maintains both maps.

**Related API decision — comparer replacement:** Dictionary/BiDictionary
`SetComparer` rehashes without deduplicating keys or reverse values that become
equal under the new comparer. Disposition: retain the documented precondition
that replacement equality preserves existing distinctions; defer API policy.
Reopen with an explicit reject, merge or duplicate-preservation policy and
exception-atomicity tests. Sorted-list comparer restore is the separate
[SC-Issue-010 contract decision](#sc-issue-010---sorted-list-restore-loses-its-ordering-invariant).

### SC-Issue-014 - Packed-set duplicate state breaks membership invariants

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
- **Affected owners / impact:** SwiftPackedSet restore copies duplicate dense values but lookup stores one mapping; later removal leaves inaccessible live data.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
var s = new SwiftPackedSet<int>(new SwiftArrayState<int>(new[] { 1, 1 }));
s.Remove(1);
Console.WriteLine($"{s.Count}; {s.Contains(1)}");
```

Observed: `1; False`: a dense item remains that membership cannot find. Expected:
reject or deduplicate equal state items consistently.

- **Next action:** Decide the restore policy and verify Count, lookup, remove and both serializers. Until then, only unique non-null items are valid restore input.

### SC-Issue-015 - Slot reinsertion leaves an allocated slot on the free stack

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
- **Affected owners / impact:** SwiftSlotMap.InsertAt after RemoveAt allows the next Add to overwrite a live slot and overcount allocations.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
var s = new SwiftSlotMap<int>();
int i = s.Add(1);
s.RemoveAt(i); s.InsertAt(i, 2);
int j = s.Add(3);
Console.WriteLine($"{i}; {j}; {s.Count}; {s[i]}");
```

Observed: `0; 0; 2; 3`; Add overwrites the allocated value 2 and Count becomes 2
for one live slot. Expected: preserve slot 0 and allocate another slot.

- **Next action:** Reconcile InsertAt with free-index ownership and verify remove/reinsert/add and trim/restore sequences without a linear hot-path workaround.

**Related API decision — trusted state and unchecked mutation:** Slot/ticket
restore checks free-index range but trusts uniqueness and disjointness. This
malformed state overwrites live slot zero when another value is added:

```csharp
var state = new SwiftTicketMapState<int>(new[] { 1 }, new[] { true },
    new uint[] { 0 }, new[] { 0 }, 1);
var map = new SwiftTicketMap<int>(state);
map.Add(2); // overwrites slot 0; Count becomes 2
```

Likewise, `new SwiftSlotMap<int>().RemoveAt(0)` reports Count=-1, whereas
`TryRemoveAt(0)` returns false without mutation. These inputs violate required
allocation/free-list invariants; they are distinct from the confirmed valid
remove/InsertAt/Add sequence above. Disposition: deferred validation-policy
choice, with current preconditions documented. Reopen when deciding whether
public restore and unchecked forms reject malformed inputs or remain explicitly
trusted operations; cover duplicate/overlapping free slots and checked versus
unchecked mutation.

### SC-Issue-016 - Pool Clear loses outstanding-rental counts

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
- **Affected owners / impact:** SwiftObjectPool.Clear resets CountAll while active rentals remain; returning them makes CountActive negative.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
using var p = new SwiftCollections.Pool.SwiftObjectPool<object>(() => new object());
var item = p.Rent(); p.Clear(); p.Release(item);
Console.WriteLine($"{p.CountAll}; {p.CountInactive}; {p.CountActive}");
```

Observed: `0; 1; -1`. Expected: non-negative counts accounting for the outstanding
rental when Clear removes only inactive objects.

- **Next action:** Decide counter semantics and verify Clear/overflow/return with outstanding rentals. Concurrent counter accuracy requires its own synchronization decision.

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

**Related API decision — notification coverage:** Hidden indexers and
observable overrides do not observe direct storage writes or base-typed and
non-overridden mutations. Disposition: document the concrete notification
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
| <a id="sc-issue-011---power-of-two-recognition-accepts-a-negative-input"></a>SC-Issue-011 | 2026-11-20 | **Verified fix:** require `x > 0` before the single-bit test; all non-positive inputs now return false. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `8099dd71d08311fee8cdf8d337ad6c59143624d3` on Windows x64, SDK 10.0.302/runtime 10.0.10, Release with published dependencies. Portable reproducer: `SwiftHashTools.IsPowerOfTwo(int.MinValue)` returned True; expected and corrected result is False. The 14-case theory fails only that case before the fix (13 controls pass); afterward all 23 utility cases pass. Existing `NextPowerOfTwo(1 << 30) == int.MaxValue` sentinel policy and its test remain unchanged; reopen that decision only for an agreed capacity-domain contract and validation of bit-mask consumers. No production caller of IsPowerOfTwo exists in this checkout. Package-mode Release/ReleaseLean/Debug solution builds pass with zero warnings/errors; core tests 1805/1702/1803 and companion 43 each pass with no skips. Coverage is unchanged, the helper has full line/branch coverage, and the only core gaps remain the two documented unreachable persistence guards. Checked both runtime assemblies' eight DLL/XML pairs and all four package IDs at both TFMs; clean DocFX, rendered contract and CI reference/local-link checks pass. No introduced build/test/reference failure; no hosted CI, Unity or timing rerun is claimed. Reopen for incorrect positive-power classification or a confirmed caller regression. | [Regression and sentinel control](../../tests/SwiftCollections.Tests/Utility/SwiftHashTools.Tests.cs), [helper and contracts](../../src/SwiftCollections/Utility/SwiftHashTools.cs), [existing coverage limits](../complexity-exceptions.md#fresh-table-persistence-guards) |
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
