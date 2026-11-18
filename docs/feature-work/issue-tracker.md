# Issue Tracker

## Purpose

Track correctness, determinism, API, documentation, build, tooling and test
reliability concerns owned by SwiftCollections, including its FixedMathSharp
companion, with a compact index of verified resolutions or documented
investigation closures. Keep substantial implementation work in focused
feature-work plans.

## Tracker Rules

- Issue IDs use `SC-Issue-NNN`. The next available ID is `SC-Issue-021`.
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

### SC-Issue-010 - Sorted-list restore loses its ordering invariant

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
- **Affected owners / impact:** SwiftSortedList state constructor; descending items restore with the default ascending comparer, causing false-negative lookup.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
var a = new SwiftSortedList<int>(new[] { 1, 2, 3 },
    Comparer<int>.Create((x, y) => y.CompareTo(x)));
var b = new SwiftSortedList<int>(a.State);
Console.WriteLine(string.Join(",", b));
Console.WriteLine(b.Contains(1));
```

Observed: `3,2,1` and False. Expected: an ordered restored collection with
working lookup. State stores only items; restore installs the default comparer
without sorting them.

- **Next action:** Decide whether restore re-sorts with the default comparer or explicitly retains/provides comparer metadata, then add runtime tests. Do not promise custom comparer persistence in the reference.

### SC-Issue-011 - Power-of-two recognition accepts a negative input

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
- **Affected owners / impact:** SwiftHashTools.IsPowerOfTwo(int.MinValue) returns true despite its former positive-power contract. NextPowerOfTwo's sentinel boundary is separately recorded as an existing tested API decision.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
Console.WriteLine(SwiftHashTools.IsPowerOfTwo(int.MinValue));
Console.WriteLine(SwiftHashTools.NextPowerOfTwo(1 << 30));
```

Observed: True and 2147483647. Expected from the former documented contract:
negative values are false. The latter returns a sentinel, not a power of two,
even at the largest representable positive power of two. No huge allocations
were attempted. The sign result is a confirmed defect. The NextPowerOfTwo
sentinel at exactly 2^30 is explicitly expected by the existing
PowerHelpers_HandleBoundaryValues test: disposition is to preserve and document
that policy, not call it a second implementation defect. Reopen the sentinel
policy only with an agreed capacity-domain contract and validation of bit-mask
consumers.

- **Next action:** Correct sign recognition with an Int32.MinValue regression; preserve the tested sentinel policy unless an explicit API decision changes it.

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
exception-atomicity tests. Sorted-list comparer restore is the separate defect
in SC-Issue-010.

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
| <a id="sc-issue-009---list-to-list-copy-grows-the-destination-only-once"></a>SC-Issue-009 | 2026-11-18 | List-to-list CopyTo now guards insufficient capacity and requests the entire source Count through EnsureCapacity. Large copies obtain sufficient storage in one growth; fitting buffers, including non-power-of-two, exact-fit, self-copy and empty-to-empty targets, are reused. Policy disposition: only insufficient capacity causes growth; the old empty-to-empty allocation of eight slots is removed. Count replacement, shallow ownership, retained unused slots, target enumerator invalidation and null-target NullReferenceException remain implemented contracts. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `63c6124572b703680e797770ed0d93e0eb88bea0`: copying 100 items into an empty target threw ArgumentException and left target Count 0/Capacity 8; fixed output is Count 100/Capacity 128 with items 0..99. New group: baseline 13 failures/11 controls, all 24 pass after correction; existing JSON/MemoryPack continuation also reproduced the defect. Full list suite passed 113 cases. Release/ReleaseLean built both assemblies at netstandard2.1/net10.0 and all four package IDs; tests passed at net10.0 (core 1560/1526; companion 43 each). Debug passed 1558/43; all core lines/branches and authored list methods remained covered in all three configurations. Emitted XML and rendered CopyTo contracts checked; DocFX passed with warnings as errors. Reopen for valid-copy failure, unexpected allocation with sufficient storage, or an intentional null/capacity policy change. No release is claimed. | [Copy regressions](../../tests/SwiftCollections.Tests/Collection/SwiftList.Tests.cs), [copy owner and contract](../../src/SwiftCollections/Collection/SwiftList.cs), [performance evidence and limits](benchmark-signal-hardening-backlog.md#sc-benchmark-003---list-to-list-copy-capacity) |
| <a id="sc-issue-008---exactly-full-queue-state-restores-an-invalid-tail"></a>SC-Issue-008 | 2026-11-18 | State restore now installs the new capacity mask before assigning `tail = count & mask`, matching known-count construction, Resize and TrimExcessCapacity. Exactly full queues can dequeue/refill or drain/reuse without growth; FIFO order, ownership, capacity reconstruction and serialization schema are preserved. Enqueue/Dequeue hot paths are unchanged. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `7ce79b7376b586df5455e4aa9d04a513d7f95338`: restoring items 0..7, dequeuing 0, then enqueueing 99 threw IndexOutOfRangeException instead of retaining [1,2,3,4,5,6,7,99]. New regressions: baseline 14 failures/11 passing controls; fixed 25 cases (23 Lean), covering boundary counts, stale-mask overwrite, wrapped snapshots, views, growth controls and JSON/MemoryPack continuation. Release/ReleaseLean built both assemblies at netstandard2.1/net10.0 and all four package IDs; tests passed at net10.0 (core 1536/1502; companion 43 each). Debug passed 1534/43; all core lines/branches and authored queue methods remained covered in all three configurations. DocFX passed with warnings as errors. Existing `queue-workload --filter '*SwiftQueue_WrapAroundChurn*' --job Dry` smoke completed four cases and four successful child exits; this is not a throughput comparison. Reopen for any valid snapshot that fails FIFO continuation or restores out-of-range ring indices. No release is claimed. | [Restore regressions](../../tests/SwiftCollections.Tests/Collection/SwiftQueue.Tests.cs), [queue owner](../../src/SwiftCollections/Collection/SwiftQueue.cs) |
| <a id="sc-issue-007---int16-normalization-fails-on-constant-input"></a>SC-Issue-007 | 2026-11-18 | Replaced floating interpolation/NaN narrowing with exact, overflow-safe Int32 arithmetic. Constant input maps to the requested min (extending the existing min=0 test); reversed endpoints retain descending mapping; the final signed rational truncates toward zero. Storage and shape are preserved. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `0ffc9d76dc0ed3bfbf485cc7491c4cd66f01501e`: constant 5 mapped to [10,20] yielded 0, and identity mapping [0,53,100] yielded 52 for the middle cell. Focused suite: baseline 13 failures/21 passing controls; fixed 34 passes (33 Lean), including four full Int16 sweeps and JSON/MemoryPack continuation. Release/ReleaseLean built both runtime assemblies at netstandard2.1/net10.0 and all four package IDs; tests passed at net10.0 (core 1511/1479; companion 43 each). Debug passed 1509/43; core line/branch coverage remains complete in all three configurations. DocFX passed with warnings as errors. Policy disposition: resolved; reopen for a demonstrated arithmetic/ownership defect or an intentional change to constant/reversed-range semantics. No release is claimed. | [Regressions](../../tests/SwiftCollections.Tests/Dimension/SwiftShortArray2D.Tests.cs), [API contract](../../src/SwiftCollections/Dimension/Default/SwiftShortArray2D.cs), [performance evidence and limits](benchmark-signal-hardening-backlog.md#sc-benchmark-002---exact-int16-normalization) |
| <a id="sc-issue-006---dimensional-arrays-accept-inconsistent-shapes"></a>SC-Issue-006 | 2026-11-18 | Enforced non-negative dimensions and exact Int32 products at construction, Resize and restore; restored data must match the shape, and owner validation/copy failures preserve existing storage and stored values. Preserved 2D constructor clamping, passive state DTOs and null exception distinctions; reconsider those policies only through an intentional API change. Discovered 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`; reproduced at `9ccb87724652516232322f3fe5ab0b173b493c4b`: `(65536,65536)` produced an empty 2D array, `(-1,-1,1)` produced a one-cell 3D array, and a 2x2 state with one item was accepted. Final regression suite: 45 failures and 20 passing controls on that baseline, all 65 passing after the fix (63 in Lean). Release/ReleaseLean builds checked both runtime assemblies at netstandard2.1 and net10.0 and all four package IDs; unit tests passed at net10.0 (core 1485/1453, companion 43 each). Debug build/tests passed (1483/43); core coverage retained all reachable lines/branches/methods in all three configurations. DocFX passed with warnings as errors. No release is claimed. | [Regressions](../../tests/SwiftCollections.Tests/Dimension/SwiftDimensionalArrayValidation.Tests.cs), [restore contracts](../api/guides/serialization-and-diagnostics.md#serialization), [performance evidence and reopening criteria](benchmark-signal-hardening-backlog.md#sc-benchmark-001---dimensional-resize-validation-code-generation) |
| <a id="sc-issue-005---cold-value-key-lookup-boxes-a-default-key"></a>SC-Issue-005              | 2026-10-30      | Replaced dictionary/set nullability initializers with cached type metadata, removing the 24-byte first integer/enum lookup allocation exposed by Gravitas pair teardown. Reference, nullable and generic-struct semantics are preserved without a warmup workaround. | [Cold lookup and removal regressions](../../tests/SwiftCollections.Tests/Collection/SwiftFirstLookupAllocation.Tests.cs)                                                                                       |
| <a id="sc-issue-004---non-generic-list-enumeration-rejects-the-final-item"></a>SC-Issue-004    | 2026-10-28      | Corrected list/queue final-item guards and reproduced position-contract defects across the other affected collections and dictionary views; preserved generic iteration and full reachable coverage.                                                                 | [Closeout and regressions](done/2026-10-28-enumerator-contract-closeout.md)                                                                                                                                    |
| <a id="sc-issue-003---hash-set-lookup-boxes-value-keys-in-unoptimized-builds"></a>SC-Issue-003 | 2026-10-26      | Removed value-key boxing from all three hash-set lookup APIs, including Debug; retained null/error contracts and added warmed allocation regressions.                                                                                                                | [Closeout](https://github.com/mrdav30/SwiftCollections/blob/2b4894f87980147934b1eb74603029e04fb84ec8/docs/feature-work/issue-tracker.md#sc-issue-003---hash-set-lookup-boxes-value-keys-in-unoptimized-builds) |
| <a id="sc-issue-002---collision-probes-stop-before-reaching-stored-entries"></a>SC-Issue-002   | 2026-10-07      | Completed dictionary/set collision-probe periods for insertion, lookup and removal; deterministic wrapped-chain and trimmed-table regressions passed.                                                                                                                | [Closeout](https://github.com/mrdav30/SwiftCollections/blob/5e0c4e0830342508c3fb87c2fe43cf7c30167522/docs/feature-work/issue-tracker.md#sc-issue-002---collision-probes-stop-before-reaching-stored-entries)   |
| <a id="sc-issue-001---pool-finalizer-coverage-depends-on-incidental-gc"></a>SC-Issue-001       | 2026-10-07      | Replaced incidental-GC coverage with isolated real-GC, mutation-checked pool-finalizer regressions; closed a test reliability gap without a production cleanup change.                                                                                               | [Closeout](https://github.com/mrdav30/SwiftCollections/blob/5e0c4e0830342508c3fb87c2fe43cf7c30167522/docs/feature-work/issue-tracker.md#sc-issue-001---pool-finalizer-coverage-depends-on-incidental-gc)       |

## Issue Template

```markdown
### SC-Issue-NNN - Concise title

- **Status / priority:** Needs reproduction | Confirmed | Planned | Deferred
- **Discovered:** Date and source revision
- **Affected owners / impact:** Source owner and observable risk
- **Evidence / reproduction:** Focused failing case or portable command
- **Next action:** Focused investigation or fix and required verification
```
