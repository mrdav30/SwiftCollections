# SwiftDenseTicketMap Plan

- **Status:** Complete. Phases 0–3 implemented and verified the owning dense
  map, persistence, allocation/performance controls, collection-selection guide
  and supported target/Unity paths. External-store implementation is a
  [separate completed feature](2026-11-23-swift-ticket-store-plan.md).
- **Completed:** 2026-11-23.
- **Created:** 2026-11-22.
- **Owner:** SwiftCollections core.
- **Tracking:**
  [Feature work overview](../feature-work-overview.md#swiftdenseticketmap).
- **Planning baseline:** SwiftCollections
  `adfb709a92f3840853b6f38e3940e0c43557dac9`, initially clean. Record the actual
  revisions, dependencies and local changes at each implementation phase.

> Archived phase evidence: commands, counts and intermediate next actions apply
> to the recorded checkpoints. The later owner-reader optimization is recorded
> under
> [SC-Benchmark-008](../benchmark-signal-hardening-backlog.md#sc-benchmark-008---discarded-old-state-snapshots-during-ticket-owner-replacement).

## Outcome and boundaries

Add `SwiftDenseTicketMap<T>`: one collection owns reusable generational tickets
and keeps its live values in a contiguous dense prefix. Removing an entry moves
the last value into the gap while preserving every surviving ticket's identity.
The target workloads combine frequent iteration, allocation/reuse and delayed
lookups in simulations and other performance-sensitive registries.

`T` is unconstrained: duplicate, null, default and mutable values are allowed.
Membership depends on the ticket, not value equality or hashing. This is an
additive collection; existing ticket, slot, sparse and packed collections retain
their public behavior, state schemas and ordering.

Cover `SwiftCollections` and `SwiftCollections.Lean`, both `net10.0` and
`netstandard2.1`, including JSON, standard-build MemoryPack and Unity smoke
verification. Build/test the unchanged companion as part of solution
compatibility. No new provider API or engine dependency is expected.

Explore the companion externally keyed store's ownership contract in Phase 0;
its implementation and multi-store verification require a focused follow-on
plan. ECS joins/scheduling, global entity IDs, thread-safe mutation, stable
addresses, sorted iteration, automatic consumer migrations and
release/publication work are outside this feature. Completion is implementation,
hardening, documentation and verification of the owning map plus the recorded
external-store contract checkpoint. If a justified cross-repo change becomes
necessary, retain a CI build/test pin reminder after it lands; pin updates and
published packages are not completion gates.

## Paired ownership direction

The two containers support distinct lifetimes. `SwiftDenseTicketMap<T>` issues
and retires local tickets and owns their associated records. An externally keyed
dense store accepts tickets issued by another owner and owns only its entries.
Its name and API were deferred at this planning checkpoint and subsequently
defined by the [SwiftTicketStore plan](2026-11-23-swift-ticket-store-plan.md).
It must not issue tickets, advance the agent's generation when a component is
removed, or use value equality for membership. Both support mutable, duplicate
values.

A concrete design scenario is one agent owner with optional attached data:

```text
SwiftDenseTicketMap<AgentRecord> -- issues agent tickets
    |-- Position store          -- accepts the same tickets
    |-- Velocity store
    `-- Health store
```

This is also applicable to optional data attached to domain registrations;
neither collection requires an ECS or a shared allocator across unrelated
physics, navigation and grid owners. The owning map can serve internal
registries or an agent authority.

The Phase 0 checkpoint confirmed the following boundaries. External-store API
choices were deferred to its follow-on plan:

- Membership is not authoritative liveness. A leftover component still matches
  its old ticket after the issuer destroys that agent. Local generation checks
  cannot establish that a supplied ticket is current, including insertion into
  an empty store. Validate against the issuer at the attachment boundary.
- Prefer explicit structural-change phases: validate attachments, remove all
  associated entries before retiring the agent, then permit slot reuse in a
  defined order. Coordination belongs to the caller; neither map secretly
  traverses stores or consults a delegate/global registry on every lookup.
  Review a concrete teardown sequence and failed/stale attachment sequence.
- Removing and reattaching Health to a live agent uses the same ticket. It does
  not identify a particular Health allocation. Component-instance handles and
  delayed commands that require them need a separate contract.
- Define duplicate-ticket and same-index/different-generation insertion policy
  in the follow-on plan. Never silently replace a current entry merely because a
  delayed command carries another generation. The issuer remains the source of
  liveness; generation ordering alone is insufficient across rollback.
- Tickets remain issuer-scoped. Their current shape cannot distinguish matching
  numeric tickets from different worlds/owners. External stores must not claim
  to reject such aliases. Snapshots must restore the owner and attached stores
  coherently; discard commands/tickets from an abandoned future timeline.
- Demonstrate that the proposed owner surface can issue tickets, validate them,
  enumerate live values with their tickets and snapshot/restore without exposing
  mutable metadata. Record compatibility conclusions and unresolved choices;
  this checkpoint is a design review, not proof of an implemented external
  store.

Reuse dense values, reverse mappings and swap-back repair where their contracts
coincide. Keep issuance, free-slot reuse and generation retirement with the
owning map. Do not extract a generic allocator or shared storage engine solely
for a future consumer. Reconsider shared implementation when both production
paths exist and can be verified together.

## Existing owners and reuse

| Owner                                                                                  | Reuse or comparison                                                       | Boundary to preserve                                                                                           |
| -------------------------------------------------------------------------------------- | ------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| [SwiftTicket](../../../src/SwiftCollections/Collection/Support/SwiftTicket.cs)         | Existing Index/Generation token shape.                                    | No owner or timeline identity; existing users and CLR identity stay intact.                                    |
| [SwiftTicketMap](../../../src/SwiftCollections/Collection/TicketMap/SwiftTicketMap.cs) | Ticket checks, explicit state and borrowing conventions; direct baseline. | Slot-order iteration, existing default-token/wrap behavior and lenient state restore are not silently changed. |
| [SwiftSparseMap](../../../src/SwiftCollections/Collection/SparseMap/SwiftSparseMap.cs) | Dense values, reverse slot IDs and swap-back repair.                      | Its integer IDs are externally owned and it has no generation allocator.                                       |
| [SwiftPackedSet](../../../src/SwiftCollections/Collection/PackedSet/SwiftPackedSet.cs) | Dense iteration and removal mechanics.                                    | Value uniqueness and a hash lookup are unnecessary for this new owner.                                         |
| [SwiftIntStack](../../../src/SwiftCollections/Collection/Stack/SwiftIntStack.cs)       | Existing LIFO storage candidate.                                          | Reserve enough storage before mutations; Push must not unexpectedly allocate during removal.                   |
| Existing guards, capacity helpers and serializer patterns                              | Reuse applicable behavior and Standard/Lean conventions.                  | Check capacity sentinels and arithmetic; do not copy a helper call without checking its contract.              |

Gravitas's `ColliderRegistry<TCollider>` is a concrete analogue: a slot map for
ID lookup, a dense list and manual service-index repair. It also owns separate
registration/replay order. Use that ownership pattern to shape a representative
workload; migrating it or deleting its replay machinery requires separate
consumer work and evidence.

Keep one direct storage owner. Do not wrap three complete collections or create
a universal allocator/storage engine. Shared helper extraction needs real
production consumers in the same change and evidence that it improves reuse
without obscuring invariants or adding hot-path cost. Reuse existing utilities
first; keep collection-specific mechanics private.

## Storage and invariants

```text
ticket.Index -> slot metadata { Generation, DenseIndex/status }
                                      |
                                      v
                              DenseValues[denseIndex]

DenseSlots[denseIndex] -> ticket.Index
```

Store values separately from slot/generation metadata. A dense pass reads live
values without testing holes or loading per-element generations. Removal repairs
the reverse mapping for the moved last value before returning. Ticket lookup has
an additional mapping hop compared with the current slot-backed ticket map.

Phase 0 compared two complete owner-local representations:

1. Slot metadata, `T[]`, reverse `int[]` and the existing `SwiftIntStack`, with
   free storage reserved during construction/growth.
2. The same slot/value/reverse layout with an owner-local free head and free
   links encoded in inactive slot metadata. This can avoid a stack object and
   array; prove sentinel arithmetic and measure the benefit before selecting it.

Selected: the intrusive representation. Slot metadata is eight bytes: UInt32
generation and Int32 position/status. Live positions are nonnegative; free
positions encode `-2 - nextFreeSlot`, with head/end `-1`. Generation zero marks
a retired issued slot, distinct from unissued storage beyond the preserved
issued boundary. At the supported limit the encoding cannot overflow. Pop
decodes the link; release advances the generation and pushes, or retires a
maximum-generation slot. Maintain owner-local head, free count and retired
count. This removes one array and the stack object without introducing a generic
allocator. State exports free IDs in bottom-to-top order independently of this
representation. See the Phase 0 evidence for costs and limits.

The invariant set is:

- `DenseValues[0..Count)` and `DenseSlots[0..Count)` describe the same live
  entries.
- Each live dense position has one unique issued slot mapping back to it.
- Issued slots partition into live, reusable free and retired slots; free slots
  are unique and exclude live/retired slots.
- Removal changes dense positions, not the tickets of surviving entries.
- All historically issued generation/retirement information survives Clear and
  dense-capacity changes. Unissued spare capacity is a separate concept.
- Failed ticket checks do not mutate values, order, generations or reuse state.

## Frozen caller contract

The new owner is `public sealed partial class SwiftDenseTicketMap<T>` with
unconstrained T, implementing `IStateBacked<SwiftDenseTicketMapState<T>>`,
`ISwiftCloneable<T>` and `IEnumerable<T>`. Do not add
collection/value-membership interfaces or convenience APIs without a caller or
parity need.

| Surface                                                                                     | Frozen behavior                                                                                                                                                                                                                                                                                        |
| ------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| `public const int DefaultCapacity = 8`; parameterless / `SwiftDenseTicketMap(int capacity)` | Default reserves eight; explicit zero uses empty arrays. Negative or above `2^30` throws `ArgumentOutOfRangeException` (parameter `capacity`). Positive capacity rounds up to a power of two, minimum eight.                                                                                           |
| `SwiftDenseTicketMap(SwiftDenseTicketMapState<T> state)`; getter-only `State`               | Public state constructor; no in-place restore/setter. Shallow-copy arrays and strictly validate. Structural malformation within the supported size budget throws `ArgumentException` (parameter `state`); unsupported size budget throws `InvalidOperationException`. Snapshot arrays are independent. |
| `int Count`, `int DenseCapacity`, `int SlotCapacity`                                        | Live count and allocated value/slot capacities. Slot history outlives live population; no public history/retirement counters.                                                                                                                                                                          |
| `SwiftTicket Add(T value)`                                                                  | Copy/retain without cloning, reserve before publication, issue from free LIFO or next unissued slot, append live value. Identity-budget exhaustion throws `InvalidOperationException`; allocation can fail separately.                                                                                 |
| `bool TryGet(SwiftTicket ticket, out T value)`, `bool IsValid(SwiftTicket ticket)`          | Validate issued boundary, live status and generation. All invalid/default/negative/unissued/stale/retired tickets fail; TryGet writes default. No mutation. Foreign numeric aliases remain possible.                                                                                                   |
| `ref T GetRef(SwiftTicket ticket)`                                                          | Writable borrowed ref. Every invalid ticket throws exactly `ArgumentException` (parameter `ticket`) via ChronicleGuard.ThrowIfArgument; reservation cannot change that classification.                                                                                                                 |
| `bool Remove(SwiftTicket ticket)`                                                           | Invalid tickets return false without mutation. Success invalidates that allocation, repairs swap-back and returns true; no allocation.                                                                                                                                                                 |
| `Span<T> Values`                                                                            | Writable live dense prefix, no slot metadata or allocating projection.                                                                                                                                                                                                                                 |
| `SwiftTicket GetTicketAt(int denseIndex)`                                                   | Current ticket for live dense position; otherwise `ArgumentOutOfRangeException` (parameter `denseIndex`).                                                                                                                                                                                              |
| `void EnsureCapacity(int capacity)`                                                         | Nonnegative requested live capacity within `2^30`; same range errors as constructor. Account for current retired slots. Remaining identity budget insufficient: `InvalidOperationException`. No-op preserves storage/version.                                                                          |
| `void Clear()`                                                                              | Invalidate live tickets in increasing old dense order; preserve prior free order and historical generations/retirement. Empty Clear is a no-op.                                                                                                                                                        |
| `SwiftDenseTicketMapEnumerator GetEnumerator()`; explicit generic/non-generic enumeration   | Public nested value-type IEnumerator<T> following dense order; concrete enumeration does not box, interface enumeration boxes. Authored Current, MoveNext, Reset and Dispose contracts below; default enum is not initialized.                                                                         |
| `void CloneTo(ICollection<T> output)`                                                       | Null throws `ArgumentNullException` (parameter `output`); clear destination then shallow-copy values in dense order. No tickets/identity history copied. Destination callback failures can leave partial output; callbacks must not mutate source.                                                     |

Do not expose mutable slot/reverse arrays, value-membership interfaces,
arbitrary ID insertion, an allocating Tickets property, sorting, a full-capacity
cleanup overload or TrimExcess in the first surface. A future trim operation
cannot discard issued-slot history and then reissue those IDs as fresh slots.

### Tickets, ordering and lifetime

Frozen new-owner rules, without changing existing `SwiftTicketMap` rules:

- Reserve generation zero. Fresh issued slots start at one; default tickets are
  invalid in this collection.
- Removing a live allocation at `uint.MaxValue` retires the slot instead of
  wrapping. Generation zero can encode retired issued slots. Never issue a
  retired slot again during that owner's forward history.
- Reuse free slots in LIFO order. For Clear, walk the old dense positions in
  increasing order, advance/retire each live generation and append newly free
  IDs to the existing free sequence. Preserve pre-existing free order; an empty
  Clear is a logical no-op. Remove and Clear always release discarded
  reference-bearing values; the supported API exposes no writable spare range.
- Dense iteration is deterministic for the same initial state and ordered
  mutations. Swap-back is not insertion, slot or registration order. Physics and
  replay consumers retain any stronger domain ordering they require.
- Tickets have no collection identity: a numerically matching foreign ticket
  cannot be rejected reliably. Use tickets only with their originating owner.
- Explicit restore/clone forks or rewinds identity. Restoring an older snapshot
  can make previously stale numeric tickets valid again. Discard external
  tickets from an abandoned future; retirement is not cross-timeline uniqueness.

Update shared `SwiftTicket` XML wording to describe owner-specific generation
policy, preserving its layout, equality and existing owners' semantics. Replay
checks must fold Index and Generation explicitly with deterministic facilities;
`SwiftTicket.GetHashCode()` currently uses `HashCode.Combine` and is not a
replay identity or ordering oracle.

All access requires caller synchronization. Structural mutations invalidate
borrowed dense indices, refs and spans; growth can detach backing storage and
swap-back can make an address refer to another entry. Stable tickets do not
promise stable addresses. Successful Add/Remove, nonempty Clear and actual
storage growth advance an unchecked UInt32 structural version. Failed
operations, no-op reservation and empty Clear preserve it. MoveNext/Reset throw
InvalidOperationException after a version change; Current is valid only after a
successful MoveNext. Generic Current caches a value without position/version
checks; non-generic Current throws InvalidOperationException before/after
enumeration. Reset clears Current; Dispose clears the cached value and ends this
enumerator. Version wrap is not an unlimited mutation-detection guarantee.
Direct value writes through refs/spans bypass version checks. Constructing a new
owner from State creates a separate instance; there is no in-place
restore/version reset.

### Capacity and failure mutation

The supported count/slot-array limit is `1 << 30`; usable indices are below it.
Public capacity arguments outside `[0, 1 << 30]` fail before allocation. Share
one private reservation calculation between Add and EnsureCapacity:
`requiredSlots = max(issuedSlots, (long)retiredSlots + requestedLiveCapacity)`.
An above-limit result throws InvalidOperationException before allocation. Add
passes `(long)Count + 1`, without a separate exhausted-capacity guard. Test the
shared limit branch with one retired slot and EnsureCapacity(1 << 30), rather
than a billion-element allocation. After that check, round growth from at least
eight with doubling, clamped at the limit. Normalize
SwiftHashTools.NextPowerOfTwo's Int32.MaxValue sentinel with
Math.Min(MaxCapacity, result) at the exact limit; reuse its arithmetic without a
separate maximum-only guard. State construction may allocate exact logical
lengths; future growth uses this rounding policy. Practical allocation can fail
below the supported limit. Test negative, zero, overflowing and sentinel
requests without allocating enormous arrays. Do not retain unreachable maximum
guards as coverage gaps or add test-only public seams.

EnsureCapacity's live reservation must account for retired slots. For example,
an allocated slot table of length 16 with eight retired slots cannot guarantee
16 live entries without growth. Checked reservation arithmetic must count usable
free/unissued slots, not just compare array length with the requested Count.
Subsequent retirement consumes usable slot capacity and can require growth
without increasing the peak live count. For example, restore 16 live slots at
maximum generation with slot capacity 16, reserve 16, remove one and add a
replacement: removal retires a slot, so replacement needs another usable slot
and can grow. Include this small state as a focused control; reservation is not
an unlimited retirement/churn budget.

Finish needed value, reverse, slot and freelist allocation before consuming an
ID or publishing an entry. Invalid input or failed Add/reservation must preserve
logical contents, ticket validity, dense order and the next ticket sequence.
Allocate/copy every required replacement array into locals before publishing any
replacement. Failure preserves capacities, backing arrays/borrows, logical
contents, reuse sequence and version; successful growth invalidates borrows. No
separately published preliminary growth is allowed. This publication order is
auditable without an injectable allocator; forced OOM is not a unit test.

Remove and Clear must not need allocation after mutation starts. Pre-reserve
freelist capacity or use the validated intrusive representation. Clear/remove
release discarded reference-bearing values and leave spare value slots free of
retained references. A full-capacity cleanup overload requires a concrete legal
retention path before it is added. Values are copied shallowly.

## Persistence and restore

Frozen `SwiftDenseTicketMapState<T>` logical schema:

| Field                    | Meaning                                                                                            |
| ------------------------ | -------------------------------------------------------------------------------------------------- |
| `T[] DenseValues`        | Live values in exact dense order.                                                                  |
| `int[] DenseSlots`       | Parallel live slot IDs.                                                                            |
| `uint[] SlotGenerations` | Every historically issued slot; length defines the issued-slot boundary. Zero marks retired slots. |
| `int[] FreeSlots`        | Exact reuse stack order, bottom to top; the last element is reused first.                          |

Rebuild slot-to-dense positions. Avoid redundant serialized allocation flags,
slot positions or runtime capacities. Spare capacities need not survive restore;
logical order, identity history and subsequent issuance sequence must survive.

Frozen default-state policy: normalize null arrays to empty, then validate all
invariants, making all-default state a valid empty state consistently with the
existing ticket map. Missing metadata for nonempty contents is invalid. Use a
public readonly partial struct with public readonly fields in the table's order,
JsonInclude/MemoryPackInclude, and JsonConstructor/MemoryPackConstructor
parameters `T[]? denseValues`, `int[]? denseSlots`, `uint[]? slotGenerations`,
`int[]? freeSlots`. Its constructor normalizes null arrays to empty and retains
arrays without validating/copying; default(struct) bypasses it and can contain
null fields. The owner normalizes again, validates and copies. Referenced
payload objects remain shared through raw snapshots/restores; serializer round
trips promise values/continuation, not identity with the original object graph.
Do not mutate input DTO arrays concurrently with construction. A retired-only
history, generations=[0] with no live/free IDs, is valid; generations=[1]
without live/free ownership is invalid. This initial four-field format adds no
version field or general version-tolerance promise; future schema changes
require an explicit compatibility decision.

Reject mismatched dense lengths, duplicate/out-of-range dense or free IDs,
live/free overlap, zero-generation live/free IDs and any nonzero-generation slot
that is neither live nor free. Every zero-generation issued slot is retired and
appears in neither list. Use the common slot-budget validator for state lengths
before allocation: requiredSlots=max(issuedLength, requestedDenseLength), with
the same above-limit InvalidOperationException branch used by reservation. Do
not add separate large-array-length guards requiring huge test fixtures.
Dense-length equality and partition checks reject structural malformation with
ArgumentException (parameter state); the supported budget is a distinct failure
condition. Structural validation cannot authenticate caller-forged generation
history.

Validate, allocate/copy and reconstruct before completing the state constructor.
Rollback reconstructs a new owner, then the caller can replace its reference.
Failed construction never replaces an existing owner; no public/internal State
setter or separate Restore API is required by the current serializer contract.
This does not roll back mutations performed by arbitrary payload serializers or
shared reference objects.

JSON's existing converter emits exactly one case-sensitive outer State property.
The nested DTO uses ordinary System.Text.Json handling; structural validation of
the decoded state does not promise rejection of every unknown/duplicate JSON
field. ArgumentException (parameter `state`) and size-budget
InvalidOperationException from owner construction propagate through the
converter; do not promise JsonException for every invalid state. Keep JSON tests
active in Lean, outside MemoryPack conditionals (the existing ticket-map fixture
is not a sufficient template for that separation).

Reuse the established state-backed JSON converter and MemoryPack partial/
constructor/ignore conventions. Ensure only the state is serialized, not Span,
capacity or computed view properties. Standard supports JSON and generated
MemoryPack owner/state paths; Lean supports the same JSON/state contract without
MemoryPack runtime/generator dependencies. No comparer codec is needed.

Chronicler's general StateJsonConverterFactory uses reflection and expression
compilation. Verify the existing explicit `StateJsonConverter<TRecord,TState>`
factory route and closed JSON metadata on High-stripping IL2CPP as needed; do
not add another registry or reflection framework. Exercise closed value,
reference and reference-containing payloads and applicable MemoryPack formatter
bootstrap on the actual supported host.

## Phases and exit evidence

| Phase                                         | Work                                                                                                                                                                                                                                                                                    | Exit evidence                                                                                                                                                                                                                                                                                       |
| --------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 0 — Contracts and first-slice baselines       | Freeze API/state/lifetime decisions; inspect current callers and serializer paths. Record the paired ownership checkpoint and select concrete scenario inputs/oracles. Compare complete stack and intrusive layouts against existing owners using disposable prototypes.                | Exact inputs/revisions, invariants, state/default/retirement/Clear/capacity rules and supported costs recorded here. One add → lookup/ref update → remove middle → reuse → dense iteration lifecycle passes. External-store boundaries/compatibility reviewed; independent review before expansion. |
| 1 — Dense owner                               | Implement the selected owner, minimum validated raw state/constructor, public APIs, enumeration, guards and cleanup. Raw state makes terminal-generation tests reachable. Add the owning-map scenarios below using public APIs; reuse existing helpers and preserve existing contracts. | Focused lifecycle, stale-ticket, moved-ticket, invalid-input, reference-release and allocation regressions; registration, delayed-work and agent-update scenarios pass; first-slice timing/storage evidence; Standard/Lean and target checks.                                                       |
| 2 — State and serializers                     | Complete persistence hardening, JSON and standard MemoryPack over the Phase 1 raw-state contract, including actual netstandard and AOT paths. Add the snapshot/continuation scenario below.                                                                                             | Malformed-state rejection, shallow ownership, rollback/fork limits and exact future allocation/replay continuation; scenario continuation through raw state, JSON and standard MemoryPack; Standard/Lean parity and relevant serializer/AOT checks.                                                 |
| 3 — Hardening, documentation and verification | Review all prior work for optimization, hardening, reuse and deletion; complete the collection-selection documentation step below, benchmarks, full coverage, API inventory and isolated Unity smoke.                                                                                   | Required matrix passes against final source; guide choices/examples agree with tested contracts and render correctly. Findings resolved and accepted tradeoffs/limits recorded. Archive the completed plan and update the overview.                                                                 |

Keep phase results in this file. Obtain independent source/test review across
the first shared slice and final implementation. New defects introduced by the
feature are resolved here; pre-existing out-of-scope defects and measured
signals belong in their existing trackers. No feature bug ID or release phase is
added.

## Correctness and determinism evidence

Use an independent model of logical allocations and values alongside internal
invariant assertions. Cover empty/default, null/duplicate/mutable payloads,
negative/foreign/stale tickets, removal of first/middle/last, repeated removal,
growth, Clear/reuse and dense moves. Verify surviving tickets after every swap.

Construct small valid public states near `uint.MaxValue` to test last-generation
issuance, retirement, repeated Clear and restore. Do not loop billions of times
or add test-only production controls. Restore after holes, free slots and
retired slots, then run the same future operation stream and compare ticket
issuance, values and dense order. Explicitly test foreign-map numeric aliasing
and rollback rewind behavior without claiming it is prevented.

Exercise mismatched/null/duplicate/overlapping state arrays and each partition
failure; failed state construction must leave any existing owner unaffected.
Verify snapshot array independence, shared reference payload semantics,
values-only CloneTo clearing its destination and exact map reconstruction
through state. Test reference cleanup for reference types and structs containing
references.

Check bounds and alias/lifetime rules for refs, Values, GetTicketAt and both
concrete and interface enumeration. Enumerators must detect supported structural
mutations; do not dereference invalid borrowed refs in tests. Force arithmetic
boundaries through public validation paths, not impractical allocations. Require
100% reported line, branch and executable-method coverage without exclusions,
unreachable guard waivers or weakened CI policy.

CloneTo destination Clear/Add failures can leave partial output; callbacks must
not mutate the source. Avoid forcing process-wide OOM or adding allocator hooks
solely for tests. Audit allocation-before-publication and report any controlled
fault-injection verification limits explicitly.

### Realistic usage scenarios

Maintain small, named xUnit scenarios alongside focused collection tests under
`tests/SwiftCollections.Tests/Collection` and the matching serialization area.
Use public collection APIs, explicit integer tick/input sequences and simple
engine-agnostic payloads. Assertions must check library behavior and observable
results, not just that an example runs. Keep expected values/tickets/order
independent of the implementation; do not compute the oracle with a second copy
of swap-back or allocation code. These are representative usage examples, not
claims that Gravitas or another consumer has migrated.

| Scenario                           | Sequence and observable assertions                                                                                                                                                                                                                                                                                                                                             | When                                                     |
| ---------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | -------------------------------------------------------- |
| Dense domain registrations         | Register three collider-like records carrying separate domain IDs; remove the middle registration, update the moved record through its original ticket and register a replacement. Assert surviving identity/value lookup, the removed ticket's failure, replacement generation and documented dense order. A domain ID in a payload is distinct from its registration ticket. | Phase 1                                                  |
| Delayed work after removal         | Queue an operation containing a ticket, remove that registration and reuse its slot before applying the operation. Assert failed validation/default out value, no mutation of the replacement and continued success for a surviving ticket. Also cover teardown via Clear.                                                                                                     | Phase 1                                                  |
| Deterministic agent updates        | Store mutable agent records with integer position/velocity and an application-owned ID. Run a fixed tick schedule of dense updates, despawn and spawn at explicit boundaries; reacquire views after structural changes. Assert exact final records, ticket/value association and the same ordered trace for repeated runs. Avoid floating-point, clocks and random iteration.  | Phase 1                                                  |
| Snapshot and resumed simulation    | Snapshot after churn leaves live, free and retired slots; execute a fixed continuation, restore and repeat it. Compare exact new tickets, values, dense order and explicit deterministic trace fields through raw state, JSON and standard MemoryPack. Verify shared reference-payload semantics separately; no deep-clone claim.                                              | Phases 1–2; raw state first                              |
| Optional component lifetime        | One agent owner issues tickets used by Position/Velocity/Health stores with differing membership. Remove/re-add Health without retiring the agent; verify the other entries and agent ticket remain valid. Despawn with coordinated cleanup, reuse the slot and reject stale attachment at the issuer boundary; store-level stale removal must not remove the replacement.     | Follow-on implementation using the actual external store |
| Authority and coordinated rollback | Deliberately leave an entry after destroying its agent to show that local membership can remain true while issuer validation fails. Restore a coherent owner/stores snapshot and replay the same structural changes; verify consistent membership/values and discard abandoned-future work. Include foreign-owner numeric aliasing as a documented limitation.                 | Follow-on implementation using the actual external store |

Use the Phase 0 exact inputs and expected outcomes below for the owning
scenarios; retain the external scenarios as follow-on acceptance requirements.
Do not add a fake external collection, ECS framework, scheduler or maintained
prototype to make future scenarios executable now. When that store is
implemented, exercise both real collections together. Carry a small
representative owner lifecycle and serializer continuation into the actual
netstandard/Unity smoke lanes and compare it across Standard/Lean as applicable;
timing/allocation measurements remain separate from assertion-heavy scenario
tests.

## Performance and storage evidence

First use N=128 and 4,096 with deterministic mutation streams and 4-byte values.
Include full occupancy and a peak four times the final live population (75%
holes in the slot-backed baseline), plus a high-water ten-times-live control
where 90% holes are relevant. Freeze seed, actual capacities, operation counts,
removal/reuse order and checksums from the Phase 0 results below.

Fair baselines are the current SwiftTicketMap and an equivalent composition:
SwiftTicketMap<int> owns ticket-to-dense-index mappings, SwiftList<T> stores
values, and SwiftList<SwiftTicket> stores reverse tickets. Swap-back repairs the
moved ticket's index through GetRef. Dictionary<SwiftTicket,T> with preissued
tickets is a useful BCL lookup/removal/enumeration control; exclude ID
generation from that comparison and do not imply equivalent ref-return behavior.
PackedSet/SparseMap alone have different membership/identity contracts.

Extend a small representative selection to N=16,384, 32/128-byte values,
reference payloads and structs containing references, plus low/high churn and
lookup-heavy controls. Avoid an unnecessary full Cartesian benchmark matrix.
Measure construction, reserved Add/reuse, live/stale lookup, ref update,
removal, growth, Clear, dense iteration and an iteration-heavy mixed lifecycle
separately. The existing TicketMapOverheadBenchmarks uses full integer
populations and does not establish a hole-density or large-value benefit by
itself.

Cross-baseline controls compare logical values and allocation events; raw ticket
generations and iteration order can differ by contract. Same-implementation
traces must agree across Standard/Lean, targets, restore continuation and Unity.
Fold explicit ticket/value fields and ordered results deterministically. Compare
like-for-like by-value enumeration/checksum work. Measure span/ref updates
separately, so avoiding a large-value copy is not misreported solely as a
packing benefit.

Report owner/array counts, actual dense/slot/freelist capacities, issued-slot
high-water, live/free/retired counts, metadata widths, reverse indices, retained
references and bytes per live versus historical slot. Contiguous live values do
not imply spare backing arrays shrink after removal. Record net runtime
lines/files and any duplicate machinery removed separately from tests/docs.

Reserved lookup, ref update, concrete/span iteration and bounded remove/reinsert
should allocate zero bytes when the reusable/unissued slot budget covers any
retirements. Measure/document interface-enumerator boxing separately. Budget
cold growth, snapshot and serialization separately. Dense packing trades an
extra lookup hop and metadata for iteration locality; there is no universal
speed or memory win. Use balanced before/after controls, verified loaded
binaries and complete BenchmarkDotNet child/sample results. Resolve introduced
regressions or obtain explicit user disposition; do not chase unattributed tiny
differences or claim equivalence from overlapping timings.

## Documentation hardening and collection selection

Make collection choice an explicit Phase 3 deliverable. Extend the existing
[collections guide](../../api/guides/collections.md), including its decision
diagram, specialized-container table and
[identity ownership examples](../../api/guides/collections.md#who-issues-the-identity).
Keep one canonical explanation there, linked from README and the
[overview](../../api/guides/OVERVIEW.md); avoid another competing selection
guide. Phase 0 outlines these choices alongside the API contract, and
implementation phases add XML contracts as each surface lands.

The hardened guide must let a reader distinguish:

- `SwiftSlotMap<T>`: container-issued reusable integer slots, with caller-owned
  stale-index discipline and slot-range iteration.
- `SwiftTicketMap<T>`: container-issued generation-checked tickets with
  slot-backed values; explain its iteration through holes and existing finite
  generation/default-token behavior accurately.
- `SwiftDenseTicketMap<T>`: container-issued tickets with packed live values,
  swap-back order and the measured lookup/storage tradeoffs. Separate stable
  ticket identity from changing dense positions and borrowed addresses; explain
  this owner's default-token, retirement and restore policy explicitly.
- `SwiftSparseSet` / `SwiftSparseMap<T>`: externally owned compact integer IDs,
  dense membership/value storage and memory proportional to the ID high-water.
  Passing only `ticket.Index` discards generation protection.
- `SwiftPackedSet<T>`: dense unique values with hash-backed membership; compare
  its equality/key-stability requirements with mutable duplicate payloads keyed
  by tickets. Retain list/dictionary alternatives for ordinary or widely sparse
  keys and cases without stable-identity requirements.

Use a comparison table covering who issues the identity, stale-reference
behavior, lookup/iteration layout, ordering, storage growth and borrowing.
Explain when an existing collection remains the better choice; do not imply the
new map supersedes the others or wins every workload. Check claims against
actual source, focused tests and measured results, including relevant removal,
Clear, capacity and state/serialization differences. Do not generalize one
owner's guarantees to every type using `SwiftTicket`.

Walk a small registration/delayed-command scenario from the unit tests through
creation, lookup, removal and reuse, and an agent example through dense updates.
Explain entity, component and component store in plain language. Include when a
single combined agent record is sufficient and when optional independently
stored data helps. Keep future external-store design in this plan; published
usage instructions describe implemented APIs. The follow-on store plan must
extend this same guide with real paired-store examples, membership versus issuer
liveness, component-only removal, coordinated teardown and rollback.

Completion requires source/test-checked snippets and expected outputs,
consistent README/overview/XML links, fresh DocFX output and representative
rendered checks of the decision diagram, tables, examples and API links at
desktop and narrow widths. Preserve existing guide routes/anchors. Record
concrete results or gaps here; updating only XML tags or this plan does not
complete this step.

## Implementation verification matrix

| Lane                                  | Required evidence                                                                                                                                                                                                                                                                                                                                                                                   |
| ------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Release / ReleaseLean, net10.0        | Full solution builds/tests, both runtime test projects and fresh assembly-specific coverage through the unchanged strict gate.                                                                                                                                                                                                                                                                      |
| Debug, net10.0                        | Guard/default/overflow, enumeration and allocation behavior; strict coverage.                                                                                                                                                                                                                                                                                                                       |
| Release / ReleaseLean, netstandard2.1 | Compile both runtime assemblies with C# 11 and matching dependencies; execute the actual netstandard DLLs on a supported host, including state and serializer continuation. netstandard is a contract target.                                                                                                                                                                                       |
| Core package target assets            | Inspect both core package IDs and their four DLL/XML pairs; companion builds/tests remain compatible. Lean retains the expected shim shape without MemoryPack runtime/generator dependencies. These are local build fixtures.                                                                                                                                                                       |
| JSON / MemoryPack                     | JSON owner/state and continuation in both variants; standard generated owner/state/formatter paths, null payloads and closed metadata as applicable. Preserve existing owners' wire formats.                                                                                                                                                                                                        |
| API reference                         | Reconcile every new authored public/protected member, operator, indexer and nested type with emitted XML; summarize internal types. Account separately for synthesis and generated serializers. Fresh DocFX metadata/site, applicable tags, inherited comments, exceptions, borrowed views, links and representative rendered pages; check remaining target/variant XML.                            |
| Collection-selection documentation    | Complete the dedicated hardening step above: existing/new container choices match source and focused tests; realistic snippets agree with scenario results; README/overview routes, selection diagram, tables and examples render with working API links. Future external-store APIs remain in planning records until implemented.                                                                  |
| Unity                                 | Existing SwiftCollections-Unity isolated smoke route with matching Standard/Lean netstandard graphs. Actual Editor/Mono and Windows High-stripping IL2CPP, value/reference payloads, invalidation, retirement, Clear/reuse and JSON/standard MemoryPack restore continuation; compare exact deterministic traces and loaded inputs. No tracked adapter-package replacement or blanket preservation. |

Follow [CONTRIBUTING](../../../CONTRIBUTING.md#local-validation). For
coordinated source builds carry `UseLocalLsfStack=true` and globally
`DisableTransitiveProjectReferences=true` through restore/build/test; serialize
shared outputs and use `-m:1`. Explicitly restore when changing configuration or
dependency mode. Use each project's existing coverlet.runsettings and run
[assert-full-coverage](../../../.assets/scripts/assert-full-coverage.ps1) over
fresh results containing both test projects. Preserve existing CI policy.

One coordinator owns builds, tests, coverage and timing. Reviewers can read in
parallel; performance acceptance has no competing build/test/profile workload.
Keep disposable probes out of maintained tests and preserve portable inputs,
commands, revisions, counts, results and reopening criteria in this plan.

Update README, the collection/API guides, applicable serialization guidance and
directly relevant contributor rules when implemented. Keep current usage guides
evergreen; this plan owns design decisions and historical verification results.

## Decision register and progress

| Decision                 | Disposition                                                                                                               | Reopening / completion criterion                                                                                                                                                                            |
| ------------------------ | ------------------------------------------------------------------------------------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Ownership                | Agreed: complementary owning and externally keyed dense collections; implement collection-issued tickets first.           | Phase 0 records issuer/membership/teardown/rollback boundaries and owner API compatibility. External-store implementation follows in its own concrete caller/acceptance plan.                               |
| Scenario evidence        | Named registration, delayed-work, agent-update and snapshot scenarios complement focused tests.                           | Owning scenarios land in Phases 1–2; actual paired-store scenarios belong to the follow-on implementation, without a substitute store or ECS framework.                                                     |
| Documentation hardening  | Agreed: explicit Phase 3 collection-selection and usage-guide deliverable, beyond XML coverage.                           | One canonical guide distinguishes slot/ticket/dense/sparse/packed choices; examples match tests and rendered checks pass. The external-store follow-on extends that guide when implemented.                 |
| Token policy             | Frozen: existing SwiftTicket shape, fresh generation one, zero invalid/retired, retire rather than wrap.                  | Phase 0 small-state controls pass; repeat against production with full coverage, preserving existing map behavior.                                                                                          |
| Layout                   | Frozen: owner-local intrusive links in eight-byte slot metadata.                                                          | Selected by Phase 0 correctness, retained storage and bounded timing evidence; production gates remain in Phases 1–3. No parallel runtime engines.                                                          |
| State                    | Frozen four-array schema, null-to-empty, strict partition validation and getter-only State; restore into a new owner.     | Phases 1–2 verify raw/JSON/MemoryPack continuation, failure isolation and selected netstandard/Mono/High-stripping IL2CPP closures. Reopen for schema, generator or supported-host changes.                 |
| Ordering and borrowing   | Dense swap-back order; borrowed storage invalidates on structural changes.                                                | Verify/document consumers' separate domain ordering and enumerate/ref/span contracts.                                                                                                                       |
| Capacity and failure     | Separate dense/slot capacity, retirement-aware live reservation and checked growth before publication.                    | Phase 0 freezes 2^30 limit, shared identity-budget check and all-or-nothing storage publication. Phase 1 verifies against production.                                                                       |
| Generated wire tolerance | Retain the existing MemoryPack Object decoder's omitted-field behavior; no custom versioned codec or legacy reader added. | Document null/zero-member/ref-destination differences in the serialization guide. Reopen if strict framing/version compatibility becomes a requirement or an actual interoperability defect is established. |
| Optional APIs            | Sorting, arbitrary-ID insertion on the owning map, owner IDs and trimming deferred.                                       | Reopen only for an actual caller contract with measured benefit; external ticket acceptance belongs to the separately planned store.                                                                        |

Initial planning review used independent contract, persistence and
evidence/performance subagents against current source. It identified the
lifetime, capacity and AOT risks above; that initial review did not execute a
prototype or establish performance budgets. Phase 0 results below record the
subsequent experiments. Only this plan and the feature overview are updated in
Phase 0.

- Phase 0: complete; evidence below.
- Phase 1: complete; evidence below.
- Phase 2: complete; evidence below.
- Phase 3: complete; evidence below.

## Phase 0 results — 2026-11-22

### Baseline and scope

Started clean at SwiftCollections `18097eebe5426e90e54cc7194129cff69193b68c`
(index tree `b7d7482a403d07a0f44ca862396b3dfc2bbbb622`). Sibling source inputs:
Chronicler `0281469ecc40e65a05867f473bc10f030e7f69f8`, FixedMathSharp
`8fdf889322f1bb82501c2afec63d6bb2f304ace2`, both clean. No runtime, maintained
test/benchmark, dependency, CI or installed-skill changes were made. The plan
and feature overview own this phase's durable decisions/evidence; disposable
prototypes are not shipped APIs or maintained fixtures.

Host: Windows 10.0.26300 x64, Intel i7-9700K (8 cores), SDK 10.0.302 / MSBuild
18.6.11, .NET 10.0.10. MemoryPack 1.21.4; System.Text.Json 10.0.9 on
netstandard. Source mode used UseLocalLsfStack=true and global
DisableTransitiveProjectReferences=true, serial builds with -m:1.

Inspected actual collection/state/enumerator/capacity code and focused tests,
Chronicler's IStateBacked and JSON converters, and Gravitas ColliderRegistry.
The latter confirms separate registration identity and dense traversal, plus a
stronger replay-order requirement which swap-back does not replace. No consumer
migration is implied.

### Frozen scenarios and compatibility checkpoint

Tickets below are written `(Index, Generation)`; all arrays show logical order.

| Control                     | Exact inputs and oracle                                                                                                                                                                                                                 | Evidence                                                                                                                                                       |
| --------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Registration / delayed work | Scalar payloads101/202/303 -> (0,1)/(1,1)/(2,1). Remove202; update moved303->333 through (2,1); add404 -> (1,2). Final dense values[101,333,404]. Old(1,1) fails/default without touching404.                                           | Both layouts pass with the update after the dense move, including invalid-ticket exact exceptions. Domain-record IDs remain a separate Phase 1 scenario field. |
| Agent ticks                 | Initial (ID,position,velocity): (101,0,2),(202,10,-1),(303,100,3). Tick1 positions[2,9,103]; despawn202, spawn(404,-5,4), run ticks2–3. Final IDs[101,303,404], positions[6,109,3], tickets[(0,1),(2,1),(1,2)].                         | Scalar-position projection passes both prototypes; full AgentRecord payload/scenario lands in Phase 1.                                                         |
| Clear ordering              | Issue0..3; remove1 then3 -> dense slots[0,2], free[1,3]. Clear -> free[1,3,0,2], all generations2. Next tickets(2,2),(0,2),(3,2),(1,2).                                                                                                 | Both layouts pass; no free-history reset.                                                                                                                      |
| Every state partition       | Values[101,303], dense slots[0,2], generations[1,2,1,0,3], free[1,4]. Add404 -> (4,3); add202 -> (1,2); remove(0,1); add505 -> (0,2). Final values[202,303,404,505], slots[1,2,4,0].                                                    | Exact raw-state reconstruction/continuation passes both; the actual closed Chronicler JSON converter also passes on a getter-only prototype envelope.          |
| Retirement / reservation    | Restore16 live slots0..15, all generationMaxValue; reserve16, remove(0,MaxValue), add replacement -> (16,1), slot storage grows. A one-retired-slot state plus EnsureCapacity(2^30) throws InvalidOperationException before allocation. | Both layouts pass; zero-generation retired slots never return to free ownership.                                                                               |

The proposed Add/IsValid/Values/GetTicketAt/State surface supplies everything
needed at an external-store boundary without exposing metadata. Agreed caller
sequence: validate an attachment with the issuer; attach only within the
structural phase; remove associated entries before issuer.Remove; reuse only
after cleanup. A delayed old ticket fails at the issuer before attachment. Local
store membership alone cannot authenticate it, especially after missed cleanup
or while an empty store accepts a token. Health-only removal leaves the agent
generation unchanged. Matching foreign tokens and coherent rollback remain
caller responsibilities. External duplicate-ticket versus same-index/
different-generation insertion policy and name are deliberately deferred to the
follow-on plan; reopen when that API and real paired-store tests are designed.
No owner API addition, automatic teardown, world registry or generic allocator
was needed by this checkpoint.

### Correctness, build and test results

Two direct Int32 prototypes used identical live/reverse/8-byte metadata arrays;
one linked the actual SwiftIntStack and reserved its full slot capacity, the
other used intrusive links. They cover the complete first lifecycle, growth, raw
snapshot/restore, terminal retirement, malformed state, default state, foreign
numeric aliasing and failure non-mutation. An independent dictionary tracks
membership/values for 10,000 mutations per layout: xorshift32 seed 0x051C4B5D,
shifts13/17/5; operation r%11<5 adds nextRandom&65535, ==10 clears, otherwise
removes a historical ticket selected by nextRandom%history.Count. After every
step assert Count, unique dense tickets, exact set equality and value/ref
association; reconstruct from State every97 steps.

- Final correctness-only processes pass **154,144 assertions each** in Release
  and ReleaseLean, under ordinary tiering and fixed-JIT controls. Exact
  exception types and ParamName are checked. Snapshot/restore arrays are
  independent.
- A fully warmed 100,000-iteration lookup/ref/IsValid/Values/GetTicketAt loop
  reports **0 allocated bytes** for each layout in both variants. Timing batches
  below also report zero allocation. These results concern the tested Int32
  operations, not interface enumeration, snapshots or generic reference cleanup.
- Core builds pass Release and ReleaseLean, each for **netstandard2.1 and
  net10.0**, emitting the four DLL/XML pairs. No build warnings/errors in those
  core lanes. The prototypes execute on net10.0; this does not execute a new
  owner compiled for netstandard or establish Unity compatibility.
- Existing focused SwiftTicketMap/SwiftSlotMap/SwiftSparseMap tests pass: **134
  Release**, **129 ReleaseLean**, no failures/skips. Pre-existing analyzer
  warnings: xUnit2013 at SwiftSlotMap.Tests.cs:138 in both; xUnit1026 at
  SwiftObservableDictionary.Tests.cs:279 in Lean. Those committed files are
  unchanged. No baseline runtime/test failure was observed in these lanes.
- Early disposable-probe setup needed explicit Chronicler references under the
  no-transitive-reference build policy. Review corrected the model's missing
  bijection assertion and overly broad exception assertion. An incomplete warmup
  initially tripped the access-allocation assertion; warming the complete same
  access kernel fixed it in ordinary and fixed tiering. These were probe issues,
  not library defects or runtime fixes.

Repeatable repository controls, for each configuration C=Release/ReleaseLean:

```powershell
dotnet build src/SwiftCollections/SwiftCollections.csproj -c $C -p:UseLocalLsfStack=true -p:DisableTransitiveProjectReferences=true -m:1
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c $C -p:UseLocalLsfStack=true -p:DisableTransitiveProjectReferences=true -m:1 --filter "FullyQualifiedName~SwiftTicketMapTests|FullyQualifiedName~SwiftSlotMapTests|FullyQualifiedName~SwiftSparseMapTests"
```

### Storage and timing comparison

Portable workload recipe: Int32 payloads, live N=128/4096; initial peak=N*factor
for factors1/4/10. Reserve peak in each ticket owner and dense composition;
append values0..peak-1, remove tickets0..peak-N-1 in increasing order. Surviving
values are peak-N..peak-1, with checksum N*(2*peak-N-1)/2. The composition uses
SwiftTicketMap<int> for dense indices, SwiftList<int> for values and
SwiftList<SwiftTicket> for reverse tickets; repair the moved ticket via GetRef.
BCL Dictionary<SwiftTicket,int> holds preissued live tickets, reserved for N;
its churn removes/readds the same external key and excludes ticket issuance. Its
hashing is not used as a deterministic trace oracle.

Each batch has131,072 element operations: passes=131072/N. Scan sums each value
by copy through the available public traversal (old map's struct enumerator;
dense prototypes/composition's spans; dictionary's pair enumerator). Lookup
walks surviving tickets; checksum matches scan. Churn removes/readds every live
ticket, preserving its payload and replacing the saved issued ticket; check
lookup checksums afterwards. Measure Stopwatch ticks and per-thread allocation
around the batch only, consume results outside it, and keep setup/assertion
allocation outside measurement. Warm each kernel12 batches, then collect8
samples with forward/reverse candidate order alternated each round. Four fresh
processes run Standard/Lean/Lean/Standard, no competing build/test/profiler,
DOTNET_TieredCompilation=0 and blocking GC. All four processes complete all90
cells (six workloads × three kernels × five candidates), **2,880 measured
batches total**, with exact checksum controls and **0 B per measured batch**.

Ranges below are min–max of the four process medians, **ns per visited/live
value** for scan/lookup and **ns per remove+add pair** for churn. They retain
adverse results and do not isolate packing from traversal/version-check overhead
or establish a production performance gate.

| Live N | Peak/live | Kernel     | Existing ticket map | Stack layout | Intrusive layout | Composition | BCL external keys |
| ------ | --------- | ---------- | ------------------- | ------------ | ---------------- | ----------- | ----------------- |
| 128    | 1         | scan       | 2.33–2.89           | 0.27–0.28    | 0.27–0.28        | 0.28–0.29   | 2.23–2.69         |
| 128    | 1         | lookup     | 1.17–1.25           | 2.39–2.46    | 2.39–2.45        | 2.15–2.19   | 9.06–12.18        |
| 128    | 1         | remove-add | 8.27–11.45          | 15.70–17.20  | 14.86–16.03      | 35.63–37.48 | 28.28–29.94       |
| 128    | 4         | scan       | 5.70–5.94           | 0.27–0.28    | 0.27–0.28        | 0.28–0.29   | 2.22–2.24         |
| 128    | 4         | lookup     | 1.18–1.35           | 2.39–2.40    | 2.39–2.44        | 2.17–2.18   | 8.64–10.68        |
| 128    | 4         | remove-add | 8.24–9.44           | 16.86–18.07  | 15.11–15.72      | 35.59–37.40 | 25.11–28.27       |
| 128    | 10        | scan       | 12.42–12.97         | 0.27–0.28    | 0.27–0.28        | 0.28–0.29   | 2.22–2.28         |
| 128    | 10        | lookup     | 1.17–1.20           | 2.39–2.54    | 2.39–2.40        | 2.16–2.18   | 8.42–9.98         |
| 128    | 10        | remove-add | 8.22–9.71           | 15.76–17.54  | 15.04–16.26      | 35.27–40.14 | 24.94–31.52       |
| 4,096  | 1         | scan       | 2.25–2.39           | 0.22–0.23    | 0.22–0.23        | 0.27–0.27   | 2.20–2.37         |
| 4,096  | 1         | lookup     | 1.17–1.19           | 2.36–2.49    | 2.36–2.41        | 2.17–2.43   | 14.36–16.68       |
| 4,096  | 1         | remove-add | 8.37–9.90           | 16.49–17.31  | 15.23–16.33      | 36.13–37.83 | 32.60–36.47       |
| 4,096  | 4         | scan       | 5.71–6.67           | 0.22–0.23    | 0.22–0.22        | 0.27–0.27   | 2.20–2.91         |
| 4,096  | 4         | lookup     | 1.18–1.19           | 2.39–2.53    | 2.37–2.42        | 2.15–2.22   | 14.90–15.99       |
| 4,096  | 4         | remove-add | 8.41–10.04          | 16.48–19.50  | 15.31–16.02      | 37.20–39.63 | 34.39–35.93       |
| 4,096  | 10        | scan       | 12.46–12.98         | 0.22–0.23    | 0.22–0.23        | 0.27–0.27   | 2.18–2.20         |
| 4,096  | 10        | lookup     | 1.17–1.19           | 2.36–2.42    | 2.39–2.43        | 2.15–2.21   | 15.18–16.86       |
| 4,096  | 10        | remove-add | 8.29–8.41           | 17.00–18.36  | 15.09–18.27      | 36.39–38.10 | 34.06–39.28       |

Retained array payload at equal allocated capacity C is **20\*C bytes** for the
stack prototype, **16\*C** for intrusive (Int32 values4C + reverse4C +
metadata8C). Both retain peak dense capacity after removals. At N=4096,
factors1/4/10 produce C=4096/16384/65536: stack81,920/327,680/1,310,720 bytes
versus intrusive65,536/262,144/1,048,576 bytes. At N=128 the same factors
produce C=128/512/2048: stack2,560/10,240/40,960 versus
intrusive2,048/8,192/32,768. The saving is **4 bytes per allocated slot**, one
array and the stack object; 20% of these prototype array payloads, not 20% of
every generic owner's heap. Counts/bytes exclude object and array headers. No
retained-memory advantage over existing SwiftTicketMap is claimed: its Int32
entry layout packs differently, and generic payload width/alignment changes the
comparison.

Intrusive is selected for the same logical invariants with fewer owned objects
and no free-stack reservation machinery. Scan/lookup costs between candidates
are similar in these controls; churn generally favors intrusive, with an adverse
18.27ns process median in the large ten-times-peak case. Do not discard that run
or promise a universal timing win. An earlier probe revision also recorded a
19.85ns intrusive median in that case; it is retained as an adverse observation,
not substituted for the final common-budget-validator results above. Compared
with the existing map, both dense layouts intentionally trade slower
lookup/churn for cheap dense traversal. Keep the existing map for suitable
lookup-heavy workloads. This additive design choice changes no existing hot
path; Phase 1 must measure the real owner before claiming production budgets or
deciding any additional optimization.

Loaded core SHA256 controls (source revision above):

| Variant / target                    | SHA256                                                           |
| ----------------------------------- | ---------------------------------------------------------------- |
| Standard net10.0, used by probe     | FC90D22913AA2E2A013B7613171CEA9D0558F466642FA274AD0AEA3FFA7A31CB |
| Lean net10.0, used by probe         | 0D97320C3CB1E9D6707D02DA3A29A0E60FBF52E4CF1F1582A955F3692F506FFB |
| Standard netstandard2.1, build only | C0FC2B41819D76E3ED3076D22505A321A18A2B50AC82884C8633DDCC43A99313 |
| Lean netstandard2.1, build only     | CCDC2FADD3FEB270F50E5ADF73A87E8C41F0E653FFCF0B30E5A618375612B737 |

### Review, exclusions and next phase

Independent contract/persistence reviews and ponytail code review checked the
first disposable slice before expansion. Oracle/exception assertions and the
registration sequence were corrected; large-state budget validation was
consolidated with reservation to avoid impractical coverage-only guards.
Correctness and timings were rerun. The full generic owner, production
enumerator, reference-bearing cleanup, emitted new-member XML, actual new-type
MemoryPack, netstandard execution, strict full-solution coverage and
Mono/High-stripping IL2CPP are deliberately **not claimed** by Phase 0. They
remain the specified implementation gates. The closed JSON envelope probe is not
an AOT or generated serializer result. No separately scoped bug or benchmark
signal was established.

Phase 0 exits with frozen
owner/state/default/retirement/Clear/capacity/borrowing contracts, exact
scenario oracles, a reviewed external-store boundary and a bounded layout
decision. Phase 1 implements only the selected direct owner and minimum
validated state; no stack alternative, prototype framework or automatic ECS
coordination is carried into runtime code. The collection-selection guide step
remains required in Phase 3, and the follow-on store extends that same guide
only when implemented. Runtime growth so far: zero files/lines; maintained
regression/benchmark additions: zero; production callers migrated: zero.

## Phase 1 results — 2026-11-22

### Implementation and reviewed contracts

Started clean at SwiftCollections `5124138c7986b42c45ec17e39635d14457c8c2fe`
(index tree `d2fb2bea9ceac021f8a17ecc4ea2415bb7f720d5`). Chronicler
`0281469ecc40e65a05867f473bc10f030e7f69f8` and FixedMathSharp
`8fdf889322f1bb82501c2afec63d6bb2f304ace2` remain unchanged. Host/tool versions
matched Phase 0. Existing CI and coverage policy were unchanged. No downstream
migration or dependency change was needed.

Implemented SwiftDenseTicketMap<T>, its companion nested enumerator and readonly
four-array State DTO. One direct owner holds values, reverse slot IDs and 8-byte
generation/position metadata; inactive positions hold free links. Reuses
SwiftTicket, ChronicleGuard, SwiftHashTools, IStateBacked/clone contracts and
existing serializer/shim conventions. No allocator framework, runtime stack
alternative, extra helper package or public restore/setter is introduced.

The first registration → delayed lookup → moved ref update → reuse slice was
compiled and tested before independent source review and full verification.
Raw-state validation, all-or-nothing capacity publication, terminal retirement,
Clear free order, versioning, borrowed views and reference-containing cleanup
match the frozen contracts. Snapshot/restore arrays are independent while raw
reference payloads remain shared. The finite mutation version is distinct from
nonwrapping ticket generation retirement.

Two implementation details were simplified during review: state validation
checks its budget then allocates metadata for the issued length, so absent
metadata cannot cause an incidental IndexOutOfRangeException; growth clamps the
existing helper's exact-limit sentinel without a new untestable maximum branch.
The same budget validator serves reservation, Add and state construction. The
state constructor remains cohesive (reviewed complexity10), so it needs no
complexity exception or coverage waiver.

Three maintained test files exercise the public runtime: focused API and
lifetime tests, strict state/continuation tests, and realistic registration and
integer-agent scenarios with the Phase 0 oracles. Churn uses an independent
membership/value model with a complete ticket bijection; it does not duplicate
the allocator. Tests cover null/default/duplicate/mutable payloads, value/ref
updates, moved survivors, invalid and foreign tickets, free order, retired-only
and near-terminal states, malformed partitions, capacity-only changes, concrete
and boxed enumeration, destination callback failures and reference release. The
warmed typed allocation test includes lookup, ref update, dense ticket
projection, concrete/span traversal, removal/reinsertion and Clear/refill. JSON
smoke runs in both variants, outside MemoryPack guards; standard owner and
raw-state MemoryPack smoke compares exact continuation. This is not the full
Phase 2 serializer/AOT hardening pass.

### Verification results

All five lanes below pass the unchanged assert-full-coverage script over fresh,
assembly-specific Coverlet reports. Counts are covered/valid; executable methods
are all covered. There are no skipped tests or excluded new source branches.

| Lane / executed runtime asset                     | Core tests | Companion tests | Core lines | Core branches | Core executable methods | Companion lines / branches / methods |
| ------------------------------------------------- | ---------- | --------------- | ---------- | ------------- | ----------------------- | ------------------------------------ |
| Release, net10.0                                  | 2,188      | 100             | 6453/6453  | 3022/3022     | 1434                    | 205/205 / 86/86 / 67                 |
| ReleaseLean, net10.0                              | 2,079      | 100             | 6257/6257  | 2946/2946     | 1392                    | 205/205 / 86/86 / 67                 |
| Debug, net10.0                                    | 2,186      | 100             | 8479/8479  | 3032/3032     | 1434                    | 257/257 / 86/86 / 67                 |
| Release, actual netstandard2.1 DLLs on .NET10     | 2,187      | 100             | 6453/6453  | 3022/3022     | 1434                    | 205/205 / 86/86 / 67                 |
| ReleaseLean, actual netstandard2.1 DLLs on .NET10 | 2,078      | 100             | 6257/6257  | 2946/2946     | 1392                    | 205/205 / 86/86 / 67                 |

- Full solution builds pass Debug, Release and ReleaseLean, compiling both
  runtime assemblies for netstandard2.1 (C#11) and net10.0. All four package
  variants build; all eight runtime DLL/XML pairs are present. Core package
  build fixtures retain the Lean shim/reference distinction; no package was
  published.
- For actual netstandard execution, disposable .NET10 test hosts compile the
  maintained test sources against snapshot DLL references, not runtime project
  references. Target-framework attributes, runtime invocation and SHA256 checks
  verify both runtime assemblies and LSF/MemoryPack inputs in both variants. The
  host supplies compatible BCL/test dependencies (Json, Encodings.Web,
  Pipelines, Unsafe, and Immutable in Standard); this is supported-host
  netstandard execution, not a Unity or an exact netstandard BCL deployment
  claim. A pre-existing direct-static MemoryPack test helper uses delegates to
  the same public Serialize/Deserialize methods because the older target
  interface lacks static abstract dispatch; assertions are unchanged. The
  one-test target count difference is the existing SwiftTicketMap JSON test
  guarded by NET10_0_OR_GREATER; new dense-map JSON tests execute in both target
  lanes.
- No baseline runtime or test failures were observed. Existing xUnit2013 warning
  in SwiftSlotMap.Tests.cs:138 remains; Lean additionally reports the existing
  xUnit1026 warning in SwiftObservableDictionary.Tests.cs:279. No new analyzer
  warnings remain. A formatter initially loaded an incomplete workspace and
  removed required imports, causing a transient Lean compile failure; imports
  were restored and formatting verification rerun with Configuration=Release,
  TargetFramework=net10.0 and explicit local-source environment flags. All final
  builds/tests above use the corrected source.
- Fresh DocFX2.78.5 metadata/site builds with warnings-as-errors: zero warnings
  or errors. All local links across121 generated HTML pages resolve. Owner,
  enumerator, State and collection-guide HTML checks confirm parameter names,
  return/value text, exceptions, borrowed-view contracts and links.
- All34 new authored XML inventory entries (including partial/nested types and
  hidden explicit interface members) have their applicable summary/param/
  typeparam/returns/value tags in each of the four core target/variant XML
  files. No new operators or indexers exist. Default struct construction is
  synthesized; standard generated MemoryPack plumbing is outside the authored
  inventory and existing generated-code coverage policy. Lean exposes the
  authored API with shim annotations. Full serializer and reference-pattern
  review remains in the later phases.
- Targeted dotnet-format verification, Prettier, whitespace and local Markdown
  links pass. Collection guidance now includes dense-ticket selection, a tested
  moved-ticket example and its parameterless-Clear behavior. Shared SwiftTicket
  comments describe owner-specific wrap/retirement policies. Phase 3 still owns
  the complete selection/documentation hardening step and visual layout checks.

Repository reproduction: for C=Debug/Release/ReleaseLean, build the solution
with the existing coordinated-source flags, run each maintained test project
with its own coverlet.runsettings into a fresh shared coverage directory, then
run the unchanged strict gate. For example:

```powershell
dotnet build SwiftCollections.slnx -c $C -p:UseLocalLsfStack=true -p:DisableTransitiveProjectReferences=true -m:1
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c $C --no-build --no-restore -p:UseLocalLsfStack=true -p:DisableTransitiveProjectReferences=true -m:1 --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.Tests/coverlet.runsettings --results-directory $coverageDirectory/core
dotnet test tests/SwiftCollections.FixedMathSharp.Tests/SwiftCollections.FixedMathSharp.Tests.csproj -c $C --no-build --no-restore -p:UseLocalLsfStack=true -p:DisableTransitiveProjectReferences=true -m:1 --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.FixedMathSharp.Tests/coverlet.runsettings --results-directory $coverageDirectory/companion
./.assets/scripts/assert-full-coverage.ps1 -CoverageDirectory $coverageDirectory
```

To reconstruct the netstandard lane, reference the actual built netstandard DLLs
from isolated .NET10 test-host projects; compile the same maintained tests with
NETSTANDARD2_1/OR_GREATER and the appropriate Lean constant, use the original
runsettings and source-root mapping, then run the same two-assembly gate. Verify
hashes and framework metadata before claiming target execution. Do not add a
maintained alternate test harness or change CI policy for this disposable check.

### First-slice performance and storage

A reviewed disposable runner compares the actual new owner with the existing
ticket map, the Phase 0 composed owner and the BCL external-key control. N=128/
4096 and peak/live1/4/10 use the same reserved fill/remove trace and checksums
as Phase 0. Concrete by-value enumeration is now used for all owner comparisons;
the new Values span is a separate control. Lookup and remove/reinsert cover
131,072 live values/pairs per batch. A mixed tick performs16 sparse updates at
index `(tick*17 + k*29) & (N-1)`, one remove/reinsert at `(tick*13) & (N-1)`,
then a complete concrete value scan. Batch ticks=131072/N. The independent sum
increases16 per tick, and final per-logical-ticket equality checks distribution,
including one correctness batch,12 warmups and8 measured batches.

Eight balanced forward/reverse samples per candidate, four fresh processes in
Standard/Lean/Lean/Standard order, fixed JIT (DOTNET_TieredCompilation=0),
blocking GC, no competing builds/tests/profilers. Loaded core hashes match the
assemblies below. Every process completed102 cells and12,901 checks; all408
cells and **3,264 measured batches** report **0 allocated bytes**. BCL keys are
preissued and churn reuses the same external key; no ticket-issuance equivalence
is claimed. This is bounded first-slice evidence, not publication-grade
universal throughput or a production frame-latency claim.

Ranges below are min–max process medians: scan/lookup ns per live value,
remove-add ns per pair, mixed ns per tick. Adverse ranges are retained.

| Live N | Peak/live | Kernel     | Existing ticket map | Dense owner     | Composition     | BCL external keys |
| ------ | --------- | ---------- | ------------------- | --------------- | --------------- | ----------------- |
| 128    | 1         | scan       | 2.31–2.33           | 0.92–0.92       | 1.17–1.17       | 2.22–2.23         |
| 128    | 1         | lookup     | 1.17–1.18           | 1.54–1.57       | 2.15–2.27       | 9.19–12.54        |
| 128    | 1         | remove-add | 8.33–9.30           | 14.19–15.75     | 35.70–39.58     | 27.47–29.84       |
| 128    | 1         | mixed      | 333.79–348.39       | 173.00–187.55   | 237.45–241.02   | 698.63–752.69     |
| 128    | 4         | scan       | 5.75–6.10           | 0.92–0.94       | 1.17–1.18       | 2.22–2.25         |
| 128    | 4         | lookup     | 1.17–1.21           | 1.54–1.56       | 2.15–2.17       | 8.93–9.59         |
| 128    | 4         | remove-add | 8.41–9.17           | 14.33–16.99     | 36.40–42.38     | 28.92–30.26       |
| 128    | 4         | mixed      | 767.09–795.46       | 174.07–182.91   | 236.77–241.02   | 691.70–708.98     |
| 128    | 10        | scan       | 12.54–13.58         | 0.92–0.92       | 1.17–1.19       | 2.22–2.24         |
| 128    | 10        | lookup     | 1.17–1.18           | 1.54–1.55       | 2.16–2.35       | 9.12–10.19        |
| 128    | 10        | remove-add | 8.47–8.95           | 13.96–14.48     | 37.16–39.61     | 26.15–28.33       |
| 128    | 10        | mixed      | 1621.14–1723.29     | 173.63–225.19   | 240.14–242.63   | 680.27–790.53     |
| 4,096  | 1         | scan       | 2.25–2.38           | 0.86–0.89       | 1.11–1.12       | 2.20–2.56         |
| 4,096  | 1         | lookup     | 1.17–1.18           | 1.51–1.77       | 2.15–2.22       | 14.43–15.77       |
| 4,096  | 1         | remove-add | 8.31–8.79           | 14.15–14.95     | 36.03–41.63     | 34.87–36.03       |
| 4,096  | 1         | mixed      | 9356.25–9846.88     | 3642.19–3701.56 | 4762.50–4942.19 | 9651.56–15839.06  |
| 4,096  | 4         | scan       | 5.70–6.01           | 0.86–0.87       | 1.12–1.13       | 2.20–3.10         |
| 4,096  | 4         | lookup     | 1.18–1.22           | 1.52–1.55       | 2.16–2.26       | 14.46–15.66       |
| 4,096  | 4         | remove-add | 8.34–8.84           | 14.37–15.50     | 38.10–47.58     | 34.24–36.70       |
| 4,096  | 4         | mixed      | 23634.38–24764.06   | 3639.06–3682.81 | 4778.12–4817.19 | 9607.81–10325.00  |
| 4,096  | 10        | scan       | 12.61–13.29         | 0.86–1.07       | 1.12–1.13       | 2.23–2.28         |
| 4,096  | 10        | lookup     | 1.17–1.23           | 1.51–1.53       | 2.16–2.23       | 14.84–15.30       |
| 4,096  | 10        | remove-add | 8.33–8.55           | 14.64–16.01     | 36.63–39.11     | 34.90–37.22       |
| 4,096  | 10        | mixed      | 53871.88–56229.69   | 3646.88–3690.62 | 4804.69–4862.50 | 9668.75–10059.38  |

Separate dense-span traversal measures0.27–0.28ns/value at N128 and0.22–0.24ns
at N4096 across these workloads. The real enumerator includes version checks; it
must not be compared to the Phase 0 prototype's span as though both measured
only packing. Lookup and churn remain slower than the existing slot-backed map,
while concrete iteration and this iteration-heavy mixed lifecycle are faster.
Existing owners retain their runtime behavior; this new choice is not a blanket
replacement or universal memory/speed win.

The new Int32 owner retains three arrays: values4*DenseCapacity, reverse IDs
4*DenseCapacity and metadata8*SlotCapacity bytes, excluding headers. At N4096,
peak/live1/4/10 yields capacities4096/16384/65536 and payload65,536/262,144/
1,048,576 bytes. Reference-bearing removed/cleared slots release references;
capacity does not shrink. Cold construction, snapshot/serialization allocation,
large payloads, alternate CPU/runtime configurations and broader benchmarks
remain later-phase work.

| Variant / target             | Core SHA256                                                      | Companion SHA256                                                 |
| ---------------------------- | ---------------------------------------------------------------- | ---------------------------------------------------------------- |
| Release / net10.0            | E7CFE5B2C62231B7C9D0E70C18402646BC4A7C0E9DCB2C8235F66887D5B0FAF7 | 538BAE3662C23DF7F72A657D51004908FFB53A6E5D6CC1D8B5966337DDFE9368 |
| Release / netstandard2.1     | 76BB529884EDA4068A89A06D2EBA7E6B237824B7BBD291250C8029D44E9B6AC7 | C3502E55B6EA0D6E9DB3BED7AFCD0517ED311F286D8A2B131F46B8FCCE6FFBF3 |
| ReleaseLean / net10.0        | 348E683D7118D5883C15D327B9AEF81309FB5DD3A683F071A69604FF9CF4B17B | D8EF025B243B89234663E6EC932F9FFD75F9E7D3A5DAAF1F1DF9566459CEDEF4 |
| ReleaseLean / netstandard2.1 | FAA08654795F70283761A63BD046DD932FCCD8DAB331C3FC5B24D794C54295DB | 14E1DB75C4668B830BCAB497C46B8245F3AFD02B9ADF6F104FC0F875C6F028EC |

### Review disposition and remaining phases

Independent contract/persistence/test work and ponytail source review found no
remaining actionable issue after the constructor-length correction. The first
working slice was reviewed before full verification; final review included
connected source/tests, caller guidance and measurement-oracle fairness. No new
out-of-scope defect or attributable benchmark signal was established.

Runtime growth: three new source files,553 total lines including143 XML comment
lines;339 nonblank noncomment lines. The existing SwiftTicket edit is comments
only. Three maintained test files were added; no maintained benchmark/harness,
legacy path, allocator abstraction or migrated production caller was added. No
duplication required uplifting into a global helper with only a future user.

Phase 1 exits with the selected direct owner and validated raw state fully
covered in the required lanes. Phase 2 completes serializer/state hardening,
closed generated metadata and actual AOT-host verification. Phase 3 performs the
full optimization/hardening review, broader performance work and final
collection-selection/reference/Unity verification. Those later results, the
external store and any full ECS coordination are not claimed here. The feature
remains active; no release or distribution gate is added.

## Phase 2 results — 2026-11-22

### Scope and implemented persistence contracts

Started clean at SwiftCollections `5da338b1c74d0692707cbbd95ef8c78f61de4918`
(index tree `139acfb2c01b29b0582747638e3ac2c47f77237c`). Chronicler
`0281469ecc40e65a05867f473bc10f030e7f69f8` and FixedMathSharp
`8fdf889322f1bb82501c2afec63d6bb2f304ace2` remain unchanged. Source-mode flags
and host/SDK versions match Phase 1. Unity verification uses installed Editor
6000.3.9f1 and the existing isolated Windows smoke approach, not the adapter's
tracked package files. Staging, dependencies, CI policy and runtime source are
unchanged. No runtime fix, new codec/registry or public API was needed by the
completed checks.

Added **59 Standard persistence cases**: 42 JSON and 17 MemoryPack; the 42 JSON
cases also execute in Lean. The six maintained files contain 800 test/support
lines, sharing three payload DTOs between formats and one generated JSON
context. They test the actual owner/state API, not the disposable host/launcher.

The first schema/continuation slice (three cases) passed before independent
review and expansion. Maintained tests now verify:

- Exact logical owner/raw-state schema and future issuance after holes,
  retirement, Clear and restore. JSON compares literal field order/content;
  binary expected records write the four fields independently of the generated
  State codec, so both sides cannot silently drift together.
- Ordinary factory and explicitly closed, generated-metadata JSON paths,
  including nullable/string/reference/reference-containing payloads, default and
  null records, nested owners, arrays and lists. The generated resolver is the
  entire chain, with no reflection resolver fallback.
- Case-sensitive sole outer State field, naming-option behavior, numeric/shape
  errors and all representative decoded-state partition failures. Nested JSON
  unknown/duplicate fields follow ordinary System.Text.Json behavior, rather
  than a new strict token protocol.
- MemoryPack packable writer/reader routes and target-specific closed bootstrap,
  malformed state, every truncated prefix of the valid control record, oversized
  headers and by-ref replacement. Failure preserves the old owner, arrays,
  ordinary mutable reference payload fields and live ticket; readonly raw-state
  ref decoding also preserves prior arrays on truncation and creates fresh
  arrays on success.
- Raw snapshots share referenced objects; default serialized round trips rebuild
  values without promising original identity or alias preservation. The resumed
  integer-agent scenario reaches positions [6,109,3] with the same surviving
  tickets and dense order.

Generated-reader inspection and focused tests confirm an important contract:
MemoryPack Object decoding accepts omitted fields. A zero-member owner record
into a null destination creates an empty owner, while the same record into an
existing destination reconstructs its previous logical state in a new instance.
A null record sets the destination to null. The readonly State decoder always
uses fresh array reads, and the owner is assigned only after construction. This
behavior is now documented as implemented; no strict-version envelope, legacy
migration or universal schema-evolution guarantee is implied. Reader
position/writer output and custom payload callback side effects are not rolled
back on failure.

The
[serialization guide](../../api/guides/serialization-and-diagnostics.md#preserve-dense-ticket-identity-and-reuse)
owns the evergreen schema, capture synchronization, copy/failure semantics and
closed JSON/MemoryPack bootstrap examples. Explicit converters are registered
before metadata is requested, overriding the owner's reflection-based factory
attribute. Both owner and raw-state metadata, plus concrete payload/container
metadata, are rooted for AOT. No blanket preservation file or adapter change was
introduced.

### Builds, tests and strict coverage

All tests below pass with zero failures/skips. The unchanged
assert-full-coverage gate runs on both fresh assembly-specific reports per lane;
all reported lines, branches and executable methods are covered.

| Lane / executed runtime asset                       | Core tests | Companion tests | Core lines | Core branches | Core executable methods | Companion lines / branches / methods |
| --------------------------------------------------- | ---------- | --------------- | ---------- | ------------- | ----------------------- | ------------------------------------ |
| Release / net10.0                                   | 2,247      | 100             | 6453/6453  | 3022/3022     | 1434                    | 205/205 / 86/86 / 67                 |
| ReleaseLean / net10.0                               | 2,121      | 100             | 6257/6257  | 2946/2946     | 1392                    | 205/205 / 86/86 / 67                 |
| Debug / net10.0                                     | 2,245      | 100             | 8479/8479  | 3032/3032     | 1434                    | 257/257 / 86/86 / 67                 |
| Release / actual netstandard2.1 DLLs on .NET 10     | 2,246      | 100             | 6453/6453  | 3022/3022     | 1434                    | 205/205 / 86/86 / 67                 |
| ReleaseLean / actual netstandard2.1 DLLs on .NET 10 | 2,120      | 100             | 6257/6257  | 2946/2946     | 1392                    | 205/205 / 86/86 / 67                 |

Full solution builds pass all three configurations, compiling both runtime
assemblies for net10.0 and netstandard2.1/C#11. All four package variants and
eight DLL/XML target pairs are checked. Phase 1's repository commands reproduce
these lanes with current tests; no coverage exclusions, thresholds or CI lanes
were changed. The existing reserved typed-operation zero-allocation regression
passes in the full suites. With no runtime-source change, no new hot-path
latency claim or unrelated benchmark investigation is introduced.

Actual netstandard hosts compile the maintained tests against frozen DLL
references, not project references to net10.0 runtime libraries. SHA256 checks,
TargetFrameworkAttribute inspection and invocation verify the copied core and
companion inputs, both test-host copies and the Unity plugin inputs. The
pre-existing static-abstract MemoryPack test helper uses the same narrow older-
target dispatch adaptation described in Phase 1; assertions remain unchanged.
The new packable tests compile their ordinary older-target bootstrap directly.
BCL/test-host overlays remain as documented in Phase 1, so this supported .NET
10 host execution is separate from the actual Unity results below. The one-test
netstandard count difference remains the pre-existing NET10_0_OR_GREATER guard
on the older SwiftTicketMap JSON test; all new dense-map JSON cases run.

No baseline runtime/test failure was observed. Existing xUnit2013 and Lean
xUnit1026 analyzer warnings remain when those projects recompile. Initial new
test compilation assumed public generated methods/GetFormatter on newer
MemoryPack targets; inspection corrected it to the actual APIs before the first
working slice. Final builds have no new warnings/errors. These were test setup
corrections, not defects or fixes in the collection runtime.

### Actual Mono and High-stripping IL2CPP results

Both variants use isolated projects populated with the matching frozen
netstandard2.1 graph and a precompiled C#11 consumer. The consumer sets and
asserts JSON reflection disabled before first serializer use, installs only
closed StateJsonConverter instances and generated metadata, and roots standard
MemoryPack formatters explicitly. The exact tested closures are Int32 owners/
states, a reference payload (Id:Int32, Label:String), a reference-containing
struct (Id:Int32, Resource:reference payload), and a containing record with
owner fields, owner arrays and owner lists. Null/default payloads are included.
Nullable and string-only owners also run in the managed target matrix; arbitrary
application closures are not inferred from this representative AOT set.

| Variant  | Editor Mono                                                 | Windows x64 IL2CPP player                                                    |
| -------- | ----------------------------------------------------------- | ---------------------------------------------------------------------------- |
| Standard | Pass; 143 reported checks; generated JSON + MemoryPack      | Pass; 143 checks; High stripping; Release native compiler; BuildOptions.None |
| Lean     | Pass; 82 reported checks; generated JSON without MemoryPack | Pass; 82 checks; High stripping; Release native compiler; BuildOptions.None  |

Every top-level Editor/player process exits 0 and writes its required PASS
result. Build logs and saved settings confirm IL2CPP/High/Release; the player
also asserts ENABLE_IL2CPP. There is no link.xml or blanket-preservation
workaround. All four routes report the same exact continuation trace:

```text
slots=0,4,1,5;values=101,404,202,505;added=4:3;reused=1:2;fresh=5:1;clear=5:2
```

Portable input: DenseValues=[101,303], DenseSlots=[0,2],
SlotGenerations=[1,2,uint.MaxValue,0,3], FreeSlots=[1,4]. Add 404, remove the
terminal ticket(2,MaxValue), verify the moved survivor, add 202, then add 505.
Assert the shown dense order and both retired slots. Clear, add 9, and assert
(5,2). Execute that stream on raw-state reconstruction and on JSON/standard
MemoryPack restorations; test malformed JSON state and truncated binary ref
restore independently. Reference and struct payload checks verify their values,
null entries and subsequent Remove/Clear paths.

To recreate the host lane, build the actual netstandard assemblies and a C#11
consumer with the guide's closed metadata/converters, preserving matching
Standard/Lean dependencies. Use Unity 6000.3.9f1 with an empty isolated project,
run the consumer in an Editor execute method, then build StandaloneWindows64
with IL2CPP, ManagedStrippingLevel.High, native configuration Release and
BuildOptions.None. Run the same consumer before scene load, assert the backend
and reflection switch, capture the trace and require process/result success.
Compare plugin hashes to the built inputs; do not replace tracked adapter
packages or treat an Editor-only run as native-player evidence.

### Input identities, review and remaining scope

| Variant / target             | Core SHA256                                                      | Companion SHA256                                                 |
| ---------------------------- | ---------------------------------------------------------------- | ---------------------------------------------------------------- |
| Release / net10.0            | DD1E34B69C48710E3AED23F2ABAEA7F99066D9AC1F0488A8E491DD91113BFDED | 767151558CB39735705D111A350E5D458F2980D82121D16586796CC53E56D172 |
| Release / netstandard2.1     | F23BA3DD02CB18397AFEFAB0A82AB665A4BE7DCB1102E339BCFA2EBC5C5FEBC0 | 9877A1E8A999F269653F19486763B3D60EC732FE7711C149C6D221699436D07E |
| ReleaseLean / net10.0        | C3DFE067CA804D159512F76131AF1E010A857BE4F94E2790AF786C9AE0020455 | AE0C5F06E9C10380E470C93B202A1EAEC8468857A97FCB5C25B85727E8A448AD |
| ReleaseLean / netstandard2.1 | B9BB2683C624B0A526E550A3182F0EE9D4AECC7A72F56CB0E7AE76F732F6CA47 | 6176947512A42696235B0A9D0A9923A230D7C74D125C78A1E6313A856E0D3B8C |

Independent ponytail reviews of connected converters/generated readers and the
new tests found two assertion-strength gaps: the shared-codec wire-order oracle
and missing mutable-reference failure case. Both were corrected before full
verification and pass. Final source/test/guide review has no remaining
actionable finding. No confirmed runtime defect or new benchmark signal was
established.

Runtime growth: zero lines/files; public API changes: zero; new maintained
verification: six test/support files; no maintained launcher or alternate
serializer framework. Durable evidence and reopening criteria stay here and in
the evergreen guide, independent of disposable outputs. DocFX with warnings as
errors, local links, emitted XML and desktop/narrow guide rendering are checked
against the final inputs. JSON's reflection-based default factory remains
available for ordinary hosts; the supported closed AOT route is explicit.

Phase 2 is complete for these supported inputs/hosts. Phase 3 still performs the
complete previous-phase optimization/hardening pass, broader performance work,
full collection-selection/reference-pattern review and final supported-host
verification. The future externally keyed store and full ECS coordination are
separate work. No release, publishing or distribution gate is added.

## Phase 3 results — 2026-11-23

### Review, optimization and scope

Started at SwiftCollections `5da338b1c74d0692707cbbd95ef8c78f61de4918` with the
Phase 2 documentation and six persistence test/support files as an additional
patch. Results include that patch and preserve the recorded index tree
`139acfb2c01b29b0582747638e3ac2c47f77237c`. Chronicler
`0281469ecc40e65a05867f473bc10f030e7f69f8` and FixedMathSharp
`8fdf889322f1bb82501c2afec63d6bb2f304ace2` remain unchanged. Host, SDK,
serializer versions and source-mode flags match the preceding phases.

Independent source/test reviews covered the complete owner, readonly state,
enumerator, JSON/generated MemoryPack readers, connected slot/ticket/sparse
owners and the earlier scenarios. No correctness defect was established. The
reviews confirmed identity-budget arithmetic, collision-free negative free-link
encoding, complete state partition validation, atomic reserve publication,
reference cleanup, deterministic retirement/reuse, and documented
finite-version, foreign-ticket and rollback limits. Existing owners have
distinct restore, generation and storage contracts; a generic allocator/storage
abstraction would not consolidate equivalent behavior.

One measured optimization was retained. Add now calls the existing Reserve only
when dense storage is full, or when no reusable slot exists and the issued
boundary reaches metadata capacity. Reserved free/unissued additions avoid
recalculating both growth paths. The condition cannot bypass identity
exhaustion: a free slot already accounts for the requested population, and an
unissued slot exists only below allocated capacity, which never exceeds the
supported limit. Cold growth still uses the same allocation-before-publication
path. Three new behavior cases and a strengthened retirement regression assert
exact tickets, values, both capacities and enumeration invalidation for
dense-only growth, slot-only growth, reserved issuance and reuse at the issued
boundary.

Phase 3 runtime growth is **two net lines in one existing file**, with no new
runtime files, APIs, codec, dependency or shared abstraction. The complete
feature adds 555 runtime lines across three files, including XML/license text.
Phase 3 adds 12 benchmark files (289 lines, one per namespace-level type) and
modifies one maintained test file; Phase 2's six persistence files remain
intact. No maintained launcher, verification-framework tests, ECS prototype or
downstream migration was added.

### Measured workloads and storage

The original Phase 1 composition control was reused: TicketMap<int> owns dense
indices, SwiftList<T> owns values, and SwiftList<SwiftTicket> owns reverse
identities. Its original int N=128/4096 and peak factors 1/4/10 verify
equivalent logical contents before/after scan, lookup, remove/reinsert and mixed
ticks. Each process completes 12,901 checks. A
baseline/candidate/candidate/baseline sequence freezes the old/new DLLs and
reverses execution order. Tier-1 JIT inspection confirms the old Add
unconditionally calls Reserve and the new one branches around it. At N=4096,
balanced reserved remove/reinsert medians improve 8.52→6.85, 8.25→6.68 and
8.47→7.57 ns/pair for factors 1/4/10 (about 11–20%). Every reserved action
allocates zero bytes. Smaller controls also improve but show more
tiering/placement variation; they do not support the largest apparent percentage
as a stable claim. No speed claim is made for untouched paths from these small
before/after differences.

Final Standard/Lean/Lean/Standard replay of that same control passes all logical
checks and zero-allocation assertions against the final variant DLLs. Variant
timing differences are not treated as equivalence proof or a new regression.

The maintained BenchmarkDotNet suite compares the dense owner, current TicketMap
and Dictionary<SwiftTicket,T> with preissued keys. It has seven curated cells:
four-byte values at 128/full, 4096/75% holes and 16384/90% holes; 32-byte,
128-byte, reference and reference-containing payloads at 4096/75% holes. Each
cell runs by-value scan, live lookup, reserved remove/reinsert, and a tick with
16 ref/value updates, one remove/reinsert and a full scan; dense span traversal
is separate. Setup independently creates payloads for each owner and checks
logical sums, churn preservation and the exact mixed +16 result. Dictionary
churn excludes ticket issuance. Selected fields form the 32/128-byte payload
checksums; these are concrete shapes, not every possible struct-copy workload.

Run the built benchmark DLL from the owning repository with:

```powershell
$env:UseLocalLsfStack='true'
$env:DisableTransitiveProjectReferences='true'
dotnet tests/SwiftCollections.Benchmarks/bin/Release/net10.0/SwiftCollections.Benchmarks.dll all --filter '*DenseTicketMap*' --job Short --warmupCount 3 --iterationCount 5 --iterationTime 100 --launchCount 1 --exporters fulljson --keepFiles
```

Both the initial and final file-layout runs complete all **91 cases**, each with
one successful child launch and five actual measurement samples; normal outlier
filtering retains four or five. Every case has valid statistics and 0 B/op.
These are bounded Windows x64/.NET 10 controls, not frame-percentile or Unity
timing claims. The table uses the final run; the earlier run is retained as an
independent short-run control, without pooling or claiming timing equivalence.
Representative means in µs per complete scan or mixed tick:

| Payload / live × high-water factor | Dense scan | Ticket scan | BCL scan | Dense mixed | Ticket mixed | BCL mixed |
| ---------------------------------- | ---------: | ----------: | -------: | ----------: | -----------: | --------: |
| 4 B / 128 × 1                      |      0.085 |       0.152 |    0.117 |       0.129 |        0.188 |     0.319 |
| 4 B / 4096 × 4                     |      2.515 |      13.777 |    3.696 |       2.578 |       16.099 |     4.138 |
| 4 B / 16384 × 10                   |     10.112 |     143.799 |   15.054 |      10.439 |      131.584 |    15.788 |
| 32 B / 4096 × 4                    |      4.478 |      19.428 |    6.290 |       4.664 |       16.853 |     8.046 |
| 128 B / 4096 × 4                   |     10.499 |      29.582 |   33.568 |      10.563 |       29.413 |    36.530 |
| Reference / 4096 × 4               |     10.465 |      31.362 |   13.272 |      10.499 |       32.510 |    12.427 |
| Reference-containing / 4096 × 4    |      5.088 |      19.534 |    4.672 |       5.673 |       20.312 |     5.096 |

The extra lookup hop remains a tradeoff: at 4096/75% holes, four-byte live
lookup batches measure 8.903 µs dense versus 6.483 µs TicketMap; 128-byte
payloads measure 15.554 versus 14.242 µs. The guide recommends workload-based
selection, not replacement of every ticket map. BCL can also lead a concrete
dense scan, as the reference-containing control shows.

The benchmark child DLL hash differs from the canonical build because its build
settings differ (including DebugSymbols). Both have version 0.0.0.0; all 1,710
runtime method IL bodies match in order, with aggregate IL SHA256
`C2E6104FE7FA13FF0BAB9D76FC17F1C2C1515DE754E3A213723F95AF6484A6E6`. The actual
executed core hash is
`4E972D990A6F20D0727417F0E4E8D13EFD67020D4D58308379A2C373D1450BC8`. The balanced
direct controls load the canonical variant hashes listed below.

Storage inspection uses actual managed element widths and array lengths,
excluding object/array headers and payload-object sizes. At 4096 live/16384
issued, both owners retain 16384 value/slot capacity and 12288 free slots, with
no retirement. Dense has one owner and three arrays; TicketMap has its owner,
free-stack owner and two arrays. The composition uses its wrapper plus four
collection owners and four arrays. Dense metadata is 8 bytes per allocated slot,
with 4-byte reverse IDs per dense position and no separate free array. For this
equal-capacity control, dense and TicketMap array payload totals are equal: int
262144 B, 32-byte value 720896 B, 128-byte value 2293760 B, reference 327680 B,
and 16-byte reference-containing struct 458752 B. Bytes per live value are
respectively 64, 176, 560, 80 and 112; bytes per historical slot are 16, 44,
140, 20 and 28. Packed live iteration does not shrink spare arrays. Remove/Clear
reference-release tests and the typed reference workloads pass.

Separate Standard/Lean boundary probes reserve 128/4096 entries, check live and
stale lookups, ref updates, concrete/span scans, remove/reinsert and
Clear/refill. Those typed operations allocate zero bytes. Interface scans
allocate 40 B per enumerator in both variants. Cold reserved construction/fill
allocates 2184/65672 B; growth/fill 4392/131728 B; snapshot 1632/49248 B;
restore 2184/65672 B. JSON encode/decode allocations are 3416/8048 B at 128 and
104840/214744 B at 4096 for the post-churn state. Standard MemoryPack
encode/decode are 3216/3792 B and 98448/114896 B. Snapshot and serializer work
is kept outside the warm allocation claim. The boundary control warms each
action 32 times, samples eight batches, and checks maximum allocated bytes per
call; its short timing samples do not establish serializer throughput
guarantees.

### Required builds, tests and strict coverage

Full source-mode solution builds pass Debug, Release and ReleaseLean, compiling
both runtime assemblies for net10.0 and netstandard2.1/C# 11. Each test project
uses its existing coverlet settings; the unchanged strict gate passes all five
lanes with full line, branch and executable-method coverage. TRX attachment
copies duplicate reports in result folders; the fresh collector reports and
their copies agree. No thresholds, exclusions, dependency pins or CI policy were
changed.

| Executed assets                                     | Core tests | Companion tests | Core lines | Core branches | Core executable methods | Companion lines / branches / methods |
| --------------------------------------------------- | ---------: | --------------: | ---------- | ------------- | ----------------------: | ------------------------------------ |
| Release / net10.0                                   |       2250 |             100 | 6454/6454  | 3028/3028     |                    1434 | 205/205 / 86/86 / 67                 |
| ReleaseLean / net10.0                               |       2124 |             100 | 6258/6258  | 2952/2952     |                    1392 | 205/205 / 86/86 / 67                 |
| Debug / net10.0                                     |       2248 |             100 | 8480/8480  | 3036/3036     |                    1434 | 257/257 / 86/86 / 67                 |
| Release / actual netstandard2.1 DLLs on .NET 10     |       2249 |             100 | 6454/6454  | 3028/3028     |                    1434 | 205/205 / 86/86 / 67                 |
| ReleaseLean / actual netstandard2.1 DLLs on .NET 10 |       2123 |             100 | 6258/6258  | 2952/2952     |                    1392 | 205/205 / 86/86 / 67                 |

All tests pass with no failures/skips; all 123 focused dense-map cases pass.
Actual netstandard test hosts retain the preceding phase's direct frozen-DLL
references, narrow older-target static dispatch adaptation and compatible BCL
host overlays. Target-framework attributes and runtime invocation confirm the
actual netstandard core/companion files. Their hashes, LSF/MemoryPack graph,
both test-host copies and Unity plugin inputs agree. The managed host result is
separate from native/Mono evidence, not an exact netstandard BCL deployment
claim. Existing allocation, replay and serializer regressions all execute.

There was no baseline runtime/test failure. Existing xUnit2013 and Lean
xUnit1026 warnings remain. Early BenchmarkDotNet discovery found two duplicate
project files in ignored historical snapshots and produced no samples; those
attempts are excluded. Temporarily hiding just those ignored project files
allowed the normal child toolchain; both files were restored afterward. A
disposable storage probe initially referenced an internal stack type directly;
it was corrected to read the existing object through reflection. Benchmark-only
line endings were normalized and types split into their own files after format
verification; compilation/discovery and the full timing suite were rechecked.
These setup corrections required no runtime workaround and are distinct from
passed final checks.

### Actual Unity, reference and documentation verification

Unity 6000.3.9f1 isolated projects use the final matching Standard/Lean
netstandard graph and a precompiled C# 11 consumer. Editor Mono and Windows x64
IL2CPP players both pass: **181 checks per Standard route, 120 per Lean route**.
All six top-level build/Editor/player processes exit zero and required results
pass. Native builds use High managed stripping, Release native compilation,
BuildOptions.None and .NET Standard compatibility; players assert IL2CPP.

JSON reflection is disabled before first use. Explicit closed converters,
generated owner/state/payload/container metadata and Standard MemoryPack roots
cover exactly the Int32, reference and reference-containing payload closures
recorded in Phase 2. The expanded smoke adds dense-only/metadata-only growth,
reserved free/unissued Add, enumeration invalidation and valid ref/span
mutation/reacquisition. No blanket preservation or tracked adapter replacement
is used. All four routes retain the exact same continuation:

```text
slots=0,4,1,5;values=101,404,202,505;added=4:3;reused=1:2;fresh=5:1;clear=5:2
```

The guide now compares slot, ticket, dense-ticket, sparse, packed and arbitrary
key choices, including distinct zero/default/wrap/retirement policies, equality
stability, state validation and borrowing. Delayed-command and tuple-agent
snippets execute against the final DLL and assert registrations [101,303,404],
agent IDs [101,303,404], positions [6,109,3] and exact tickets. README, overview
and contributor guidance link the canonical explanation; the future external
store remains outside published usage instructions.

Fresh DocFX metadata/site builds with warnings as errors pass with zero
warnings/errors. All 121 generated HTML pages have resolving local file links
and fragments. The original expanded diagram shrank labels too much; it was
simplified to the owned-identity choice, while the table covers other models.
Scoped styling keeps readable SVG labels in a horizontally scrolling, labeled
keyboard region. Desktop 1264×713 and narrow 390×844 inspection verifies the
final diagram, keyboard horizontal scrolling, contained tables/code, examples
and API links. Main owner, enumerator and state reference pages show the
expected names, returns, property values, exceptions, remarks and inherited
framework members.

All 34 authored XML IDs are reconciled across four core target/variant inputs:
31 are in the site metadata and three explicit interface members are hidden by
its selected metadata policy. There are no authored operators/indexers or
additional nested types to account for. Default struct construction and
inherited ValueType/object members are synthesized/inherited, not authored
omissions. Standard generated wire remarks are accounted separately. Standard
netstandard additionally exposes generated RegisterFormatter/Serialize/
Deserialize for owner and state (six APIs absent from the selected net10 site);
newer-target equivalents implement private static interfaces. Lean has neither
set. All applicable authored summaries, parameters, type parameters, returns and
property values are present and agree; no new inherited contract is needed. All
four local package fixtures match all eight runtime DLL/XML target pairs,
including Lean's shim/dependency distinction.

### Final inputs and disposition

| Variant / target             | Core SHA256                                                      | Companion SHA256                                                 |
| ---------------------------- | ---------------------------------------------------------------- | ---------------------------------------------------------------- |
| Release / net10.0            | DC0B2043FCB8E6B1D9D4A61A0F19C345ECE954718AF76AA054DC09F127545F05 | 767151558CB39735705D111A350E5D458F2980D82121D16586796CC53E56D172 |
| Release / netstandard2.1     | 6A47F2382C4F7757DCA92C4626D673C6030AE9B4FB21607DAEE464F417FD45AF | DCDEA28D883ADC1AB42B6465BC73AF912DC4D4E6AE64A113A3F1B6E6F00A3FC3 |
| ReleaseLean / net10.0        | 022CB80E1E5F6B676503388804BC7B45BE126E81665FEDC7F2C3446F3D1E7CE6 | AE0C5F06E9C10380E470C93B202A1EAEC8468857A97FCB5C25B85727E8A448AD |
| ReleaseLean / netstandard2.1 | 00CCAB94A9F358DD83E232C3AF85927678E98893A34B55FED1E8429A6E030EBC | 63C0A10A97CD6C950C69F9625D11A30A45830ACCF7C4E1CE07CE850B97ACFE7B |

Independent final ponytail reviews have no remaining actionable finding. The
parent executed the gates and verified outputs/inputs; reviewer statements are
not substituted for runs. No confirmed defect or unresolved introduced timing
regression needs a new tracker entry. Full coverage does not include generated
MemoryPack code under the existing policy, but actual serializer and AOT routes
were exercised. Process-wide OOM/allocator fault injection, arbitrary payload
callbacks, all possible AOT closures and other OS/architecture timing remain
explicit verification limits; source inspection covers allocation publication.

**Disposition:** Phases 0–3 completed the owning map. The external-store
boundary was deferred to the
[subsequently completed store plan](2026-11-23-swift-ticket-store-plan.md),
which named its API/issuer authority and exercised the real paired-store
scenarios. Reopen this feature only for a concrete caller contract or verified
correctness/performance issue. No ECS coordination, consumer migration, release
or distribution gate is implied.
