# SwiftDenseTicketMap Plan

- **Status:** Planned. Owning and externally keyed collections are
  complementary; implement the owning map first. Detailed contracts and layout
  are proposals to freeze in Phase 0, including the external-store design
  checkpoint below. No implementation or performance result is claimed by this
  planning pass.
- **Created:** 2026-11-22.
- **Owner:** SwiftCollections core.
- **Tracking:**
  [Feature work overview](feature-work-overview.md#swiftdenseticketmap).
- **Planning baseline:** SwiftCollections
  `adfb709a92f3840853b6f38e3940e0c43557dac9`, initially clean. Record the actual
  revisions, dependencies and local changes at each implementation phase.

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
Its name and API remain undecided. It must not issue tickets, advance the
agent's generation when a component is removed, or use value equality for
membership. Both support mutable, duplicate values.

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

Phase 0 records the following boundaries and open API choices before freezing
the owning surface:

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

| Owner                                                                           | Reuse or comparison                                                       | Boundary to preserve                                                                                           |
| ------------------------------------------------------------------------------- | ------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------- |
| [SwiftTicket](../../src/SwiftCollections/Collection/Support/SwiftTicket.cs)     | Existing Index/Generation token shape.                                    | No owner or timeline identity; existing users and CLR identity stay intact.                                    |
| [SwiftTicketMap](../../src/SwiftCollections/Collection/SwiftTicketMap.cs)       | Ticket checks, explicit state and borrowing conventions; direct baseline. | Slot-order iteration, existing default-token/wrap behavior and lenient state restore are not silently changed. |
| [SwiftSparseMap](../../src/SwiftCollections/Collection/SwiftSparseMap.cs)       | Dense values, reverse slot IDs and swap-back repair.                      | Its integer IDs are externally owned and it has no generation allocator.                                       |
| [SwiftPackedSet](../../src/SwiftCollections/Collection/SwiftPackedSet.cs)       | Dense iteration and removal mechanics.                                    | Value uniqueness and a hash lookup are unnecessary for this new owner.                                         |
| [SwiftIntStack](../../src/SwiftCollections/Collection/Support/SwiftIntStack.cs) | Existing LIFO storage candidate.                                          | Reserve enough storage before mutations; Push must not unexpectedly allocate during removal.                   |
| Existing guards, capacity helpers and serializer patterns                       | Reuse applicable behavior and Standard/Lean conventions.                  | Check capacity sentinels and arithmetic; do not copy a helper call without checking its contract.              |

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

## Proposed storage and invariants

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

Phase 0 compares two complete owner-local representations:

1. Slot metadata, `T[]`, reverse `int[]` and the existing `SwiftIntStack`, with
   free storage reserved during construction/growth.
2. The same slot/value/reverse layout with an owner-local free head and free
   links encoded in inactive slot metadata. This can avoid a stack object and
   array; prove sentinel arithmetic and measure the benefit before selecting it.

Use the simpler complete representation supported by measurements. A compact
freelist does not justify a new generic framework. State must not expose this
runtime representation choice.

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

## Proposed caller contract

Freeze exact signatures, exception types/parameter names, limits and synthesized
versus authored APIs in Phase 0. Avoid adding convenience APIs without a caller
or parity need.

| Surface                                        | Proposed behavior                                                                                                                                                                                                                                           |
| ---------------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Default/capacity constructors                  | Empty owner; nonnegative capacity, with zero and default allocation policy frozen in Phase 0.                                                                                                                                                               |
| State constructor; `State`                     | Explicit shallow snapshot/restore through `IStateBacked<SwiftDenseTicketMapState<T>>`; strict structural validation below. Minimum validated DTO/constructor lands with Phase 1; internal setter only if required by the established serialization pattern. |
| `Count`, `DenseCapacity`, `SlotCapacity`       | Live count and separate allocated storage capacities; slot history can outlive the live population.                                                                                                                                                         |
| `Add(T value)`                                 | Retain/copy the value without cloning; issue a ticket for a free slot or a new slot and append the value.                                                                                                                                                   |
| `TryGet(ticket, out value)`, `IsValid(ticket)` | Invalid, negative, out-of-range, removed and retired tickets return false; TryGet writes default on failure.                                                                                                                                                |
| `GetRef(ticket)`                               | Writable borrowed ref for a validated live ticket; freeze checked/throwing distinction and exceptions together.                                                                                                                                             |
| `Remove(ticket)`                               | False and no mutation for invalid tickets; otherwise invalidate that allocation, swap back and return true.                                                                                                                                                 |
| `Values`                                       | Writable `Span<T>` over the live dense prefix, without allocating or exposing writable slot metadata.                                                                                                                                                       |
| `GetTicketAt(int denseIndex)`                  | Construct the current live ticket for a valid dense position without an allocating ticket projection. Invalid dense indices throw.                                                                                                                          |
| `EnsureCapacity(int capacity)`                 | Reserve for a requested live population, accounting for retired slots and available reusable IDs; distinguish dense and slot capacities.                                                                                                                    |
| `Clear()`                                      | Invalidate live tickets, retain identity history and reusable capacity; define reuse order below.                                                                                                                                                           |
| Struct enumeration; `CloneTo(ICollection<T>)`  | Enumerate/copy values in dense order. CloneTo follows the existing clear-destination convention and is not a ticket-preserving map clone.                                                                                                                   |

Do not expose mutable slot/reverse arrays, value-membership interfaces,
arbitrary ID insertion, an allocating Tickets property, sorting, a full-capacity
cleanup overload or TrimExcess in the first surface. A future trim operation
cannot discard issued-slot history and then reissue those IDs as fresh slots.

### Tickets, ordering and lifetime

Recommended new-owner rules, without changing existing `SwiftTicketMap` rules:

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
promise stable addresses. Structural enumerator version checks follow existing
patterns; direct value writes through refs/spans bypass those checks.

### Capacity and failure mutation

Freeze maximum supported capacity and checked arithmetic before implementation.
Share a reachable validation/growth path between Add and EnsureCapacity; test
negative, zero, overflowing and sentinel requests without allocating enormous
arrays. Do not retain unreachable maximum guards as coverage gaps or add
test-only public seams.

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
Phase 0 must specify whether successful preliminary capacity growth can remain
after an allocation failure and consequently invalidate borrowed storage.

Remove and Clear must not need allocation after mutation starts. Pre-reserve
freelist capacity or use the validated intrusive representation. Clear/remove
release discarded reference-bearing values and leave spare value slots free of
retained references. A full-capacity cleanup overload requires a concrete legal
retention path before it is added. Values are copied shallowly.

## Persistence and restore

Proposed `SwiftDenseTicketMapState<T>` logical schema:

| Field                    | Meaning                                                                                            |
| ------------------------ | -------------------------------------------------------------------------------------------------- |
| `T[] DenseValues`        | Live values in exact dense order.                                                                  |
| `int[] DenseSlots`       | Parallel live slot IDs.                                                                            |
| `uint[] SlotGenerations` | Every historically issued slot; length defines the issued-slot boundary. Zero marks retired slots. |
| `int[] FreeSlots`        | Exact reuse stack order, bottom to top; the last element is reused first.                          |

Rebuild slot-to-dense positions. Avoid redundant serialized allocation flags,
slot positions or runtime capacities. Spare capacities need not survive restore;
logical order, identity history and subsequent issuance sequence must survive.

Recommended default-state policy: normalize null arrays to empty, then validate
all invariants, making all-default state a valid empty state consistently with
the existing ticket map. Missing metadata for nonempty contents is invalid.
Freeze DTO field/constructor names, null behavior and serialization order before
shipping the first schema. DTOs retain input arrays; owner snapshots and restore
copy their array storage. Referenced payload objects remain shared.

Reject mismatched dense lengths, duplicate/out-of-range dense or free IDs,
live/free overlap, zero-generation live/free IDs and any nonzero-generation slot
that is neither live nor free. Every zero-generation issued slot is retired and
appears in neither list. Validate arithmetic before allocation. Structural
validation cannot authenticate caller-forged generation history.

Validate, allocate/copy and reconstruct in local storage before publishing a
restore. Validation/allocation failure leaves an existing owner's arrays,
contents, generations, tickets and reuse state intact. This does not roll back
mutations performed by arbitrary payload serializers or shared reference
objects.

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
failure; invalid restore must preserve an existing owner. Verify snapshot array
independence, shared reference payload semantics, values-only CloneTo clearing
its destination and exact map reconstruction through state. Test reference
cleanup for reference types and structs containing references.

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

Freeze exact inputs and expected outcomes in Phase 0 for the owning scenarios;
record the external scenarios as follow-on acceptance requirements. Do not add a
fake external collection, ECS framework, scheduler or maintained prototype to
make future scenarios executable now. When that store is implemented, exercise
both real collections together. Carry a small representative owner lifecycle and
serializer continuation into the actual netstandard/Unity smoke lanes and
compare it across Standard/Lean as applicable; timing/allocation measurements
remain separate from assertion-heavy scenario tests.

## Performance and storage evidence

First use N=128 and 4,096 with deterministic mutation streams and 4-byte values.
Include full occupancy and a peak four times the final live population (75%
holes in the slot-backed baseline), plus a high-water ten-times-live control
where 90% holes are relevant. Freeze seed, actual capacities, operation counts,
removal/reuse order and checksums in Phase 0.

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
[collections guide](../api/guides/collections.md), including its decision
diagram, specialized-container table and
[identity ownership examples](../api/guides/collections.md#who-issues-the-identity).
Keep one canonical explanation there, linked from README and the
[overview](../api/guides/OVERVIEW.md); avoid another competing selection guide.
Phase 0 outlines these choices alongside the API contract, and implementation
phases add XML contracts as each surface lands.

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

Follow [CONTRIBUTING](../../CONTRIBUTING.md#local-validation). For coordinated
source builds carry `UseLocalLsfStack=true` and globally
`DisableTransitiveProjectReferences=true` through restore/build/test; serialize
shared outputs and use `-m:1`. Explicitly restore when changing configuration or
dependency mode. Use each project's existing coverlet.runsettings and run
[assert-full-coverage](../../.assets/scripts/assert-full-coverage.ps1) over
fresh results containing both test projects. Preserve existing CI policy.

One coordinator owns builds, tests, coverage and timing. Reviewers can read in
parallel; performance acceptance has no competing build/test/profile workload.
Keep disposable probes out of maintained tests and preserve portable inputs,
commands, revisions, counts, results and reopening criteria in this plan.

Update README, the collection/API guides, applicable serialization guidance and
directly relevant contributor rules when implemented. Keep current usage guides
evergreen; this plan owns design decisions and historical verification results.

## Decision register and progress

| Decision                | Disposition                                                                                                      | Reopening / completion criterion                                                                                                                                                            |
| ----------------------- | ---------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Ownership               | Agreed: complementary owning and externally keyed dense collections; implement collection-issued tickets first.  | Phase 0 records issuer/membership/teardown/rollback boundaries and owner API compatibility. External-store implementation follows in its own concrete caller/acceptance plan.               |
| Scenario evidence       | Named registration, delayed-work, agent-update and snapshot scenarios complement focused tests.                  | Owning scenarios land in Phases 1–2; actual paired-store scenarios belong to the follow-on implementation, without a substitute store or ECS framework.                                     |
| Documentation hardening | Agreed: explicit Phase 3 collection-selection and usage-guide deliverable, beyond XML coverage.                  | One canonical guide distinguishes slot/ticket/dense/sparse/packed choices; examples match tests and rendered checks pass. The external-store follow-on extends that guide when implemented. |
| Token policy            | Proposed: existing SwiftTicket shape, fresh generation one, zero invalid/retired, retire rather than wrap.       | Freeze in Phase 0 with default, retirement, Clear and rollback/foreign-token tests; preserve existing map behavior.                                                                         |
| Layout                  | Compare reserved SwiftIntStack with owner-local intrusive free links.                                            | Select from the complete first lifecycle's correctness, memory and timing evidence; no parallel runtime engines remain.                                                                     |
| State                   | Proposed four-array logical schema, null-to-empty then strict partition validation; transactional owner restore. | Freeze in Phase 0 and verify exact continuation plus actual serializer/AOT paths.                                                                                                           |
| Ordering and borrowing  | Dense swap-back order; borrowed storage invalidates on structural changes.                                       | Verify/document consumers' separate domain ordering and enumerate/ref/span contracts.                                                                                                       |
| Capacity and failure    | Separate dense/slot capacity, retirement-aware live reservation and checked growth before publication.           | Freeze exact limits/exceptions and failure capacity/borrowing effects in Phase 0.                                                                                                           |
| Optional APIs           | Sorting, arbitrary-ID insertion on the owning map, owner IDs and trimming deferred.                              | Reopen only for an actual caller contract with measured benefit; external ticket acceptance belongs to the separately planned store.                                                        |

Planning review used independent contract, persistence and evidence/performance
subagents against current source. It identified the lifetime, capacity and AOT
risks above; it did not execute a prototype or establish performance budgets.
Only this plan and the feature overview are added/updated in this pass.

- Phase 0: not started.
- Phases 1–3: not started.
