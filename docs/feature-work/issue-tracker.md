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

### SC-Issue-006 - Dimensional arrays accept inconsistent shapes

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
- **Affected owners / impact:** SwiftArray2D/3D construction, Resize and restore; unchecked products/negative shapes or mismatched state produce unusable arrays.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
var a = new SwiftArray2D<int>(65536, 65536);
Console.WriteLine($"{a.Width},{a.Height}; length={a.Length}; size={a.Size}");
var b = new SwiftArray3D<int>(-1, -1, 1);
Console.WriteLine($"{b.Width},{b.Height},{b.Depth}; length={b.Length}");
var c = new SwiftArray2D<int>(new Array2DState<int>(2, 2, new[] { 7 }));
Console.WriteLine(c[1, 1]);
```

Observed: `65536,65536; length=0; size=0`; `-1,-1,1; length=1`; then
IndexOutOfRangeException for a logically valid coordinate. Expected: reject
unrepresentable products, negative 3D shapes, and inconsistent restored shapes
before installing unusable storage. The 2D normal constructor deliberately
clamps negative dimensions, unlike Resize, which rejects them. Default 2D state
throws ArgumentNullException; default 3D state and a null rectangular-source
constructor throw NullReferenceException. Those exception differences are
documented as implemented, not silently harmonized.

- **Next action:** Validate dimensions, products and restored shape before mutation; preserve or explicitly decide the 2D negative-clamping contract.

**Related documentation decision:** Readonly state structs retain mutable
array arguments; collection `State` getters allocate shallow snapshots.
Disposition: resolved documentation correction, described in the
[state ownership guide](../api/guides/serialization-and-diagnostics.md#serialization).
Reopen if storage ownership changes. Null/default exception differences above
remain implemented behavior, not harmonized API guarantees.

### SC-Issue-007 - Int16 normalization fails on constant input

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
- **Affected owners / impact:** SwiftShortArray2D.Normalize; a constant 5 normalized to [10,20] becomes 0 on the observed runtime; floating-point arithmetic also limits determinism.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
var a = new SwiftShortArray2D(1, 1, 5);
a.Normalize(10, 20);
Console.WriteLine(a[0, 0]);
```

Observed: 0 on the recorded runtime, outside [10,20], because the formula
divides 0 by 0 in floating point and narrows NaN. Expected: an explicit
constant-input policy and deterministic arithmetic for deterministic consumers.
Cross-platform behavior has not been established. Decision remains open: use
min, preserve constants, or reject the operation. Reopen/resolve with a specified
constant and reversed-range policy and focused upstream regression tests.

- **Next action:** Specify constant/reversed-range behavior and replace floating interpolation in a separately scoped fix.

### SC-Issue-008 - Exactly full queue state restores an invalid tail

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
- **Affected owners / impact:** SwiftQueue state constructor and Enqueue; dequeue followed by enqueue throws for eight restored items at capacity eight.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
var q = new SwiftQueue<int>(new SwiftArrayState<int>(Enumerable.Range(0, 8).ToArray()));
Console.WriteLine(q.Dequeue());
q.Enqueue(99);
```

Observed: prints 0, then IndexOutOfRangeException. Restore sets tail to Count
(8) rather than masking it to capacity; dequeue leaves tail unchanged. Expected:
FIFO [1,2,3,4,5,6,7,99].

- **Next action:** Mask the restored tail and test exactly-full and wrapped state round trips.

### SC-Issue-009 - List-to-list copy grows the destination only once

- **Status / priority:** Confirmed / P2; open.
- **Discovered:** 2026-11-17 at `17307f1460313e64043da7995fbc4a4458da09a3`.
- **Affected owners / impact:** SwiftList.CopyTo(SwiftList); copying 100 items to an empty list throws after allocating only eight slots.
- **Evidence environment:** Windows x64, SDK 10.0.302, runtime 10.0.10, Release with published dependencies (`UseLocalLsfStack=false`).

**Reproducer** (use the console setup above):

```csharp
var source = new SwiftList<int>(Enumerable.Range(0, 100));
var destination = new SwiftList<int>();
source.CopyTo(destination);
```

Observed: ArgumentException from Array.Copy; destination capacity becomes 8
and count remains 0. Expected: grow to at least source.Count and copy all 100
elements. Capacity may change on failure.

- **Next action:** Grow from the required count, then cover empty, small, preallocated and self-copy targets, including destination state on failure.

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
