# SwiftTicketStore Plan

- **Status:** Phase 0 complete. Contracts, scalar state shape and the dense-ticket
  storage/reuse direction are frozen. Phase 1 production implementation is next;
  the SwiftTicketStore runtime API is not yet implemented.
- **Created:** 2026-11-23.
- **Owner:** SwiftCollections core.
- **Tracking:**
  [Feature work overview](feature-work-overview.md#swiftticketstore).
- **Planning baseline:** SwiftCollections
  `5b637b897db1af832effff66443be8c26937d1ae`, initially clean; index tree
  `a57f95974ea3bd0d8785974eb92887c47ea83ee1`.
- **Predecessor:**
  [Completed dense ticket map plan](done/2026-11-23-swift-dense-ticket-map-plan.md#paired-ownership-direction).

## Outcome and boundaries

Add `SwiftTicketStore<T>`, a dense value store keyed by tickets issued
elsewhere. SwiftDenseTicketMap issues tickets; the new store attaches data to
them. A store owns its entries and backing storage, not ticket allocation or
issuer lifetime. Duplicate, mutable, null and default payloads are allowed;
membership compares ticket fields, never payload equality or hashing.

The shorter name does not weaken the dense storage contract. Live values occupy
one packed prefix without holes, `Values` is a contiguous Span<T> of Count
entries, and removal uses swap-back while preserving surviving ticket identity.
A future non-dense collection would need an explicit separate API decision;
optimizations in this feature must preserve these guarantees.

An agent can have Position and Velocity entries and optional Health. Removing
Health leaves its agent and other entries alive. A physics or navigation
registration can use the same primitive for optional attached records without
adopting an ECS or a common allocator across unrelated domains.

```text
SwiftDenseTicketMap<AgentRecord>  -- issues agent tickets
    |-- SwiftTicketStore<Position>  -- local membership/values
    |-- SwiftTicketStore<Velocity>
    `-- SwiftTicketStore<Health>    -- optional, independent membership
```

Implement in `SwiftCollections` and `SwiftCollections.Lean` for `net10.0` and
`netstandard2.1`/C# 11. Include raw state, JSON in both variants, standard
MemoryPack, full coverage, and actual isolated Unity Mono/High-stripping IL2CPP
verification. Build/test the unchanged FixedMathSharp companion for solution
compatibility. No new provider API or engine dependency is expected.

This is storage infrastructure. ECS scheduling, automatic joins, archetypes,
entity allocators, global/world IDs, component-instance handles, issuer
callbacks, automatic teardown, transactional multi-store mutation, concurrent
mutation, consumer migrations and release/publication are outside scope.
Completion ends at implementation, documentation, hardening and verification. If
cross-repo changes become necessary, validate the intended source graph and
leave a friendly reminder to update CI build/test pins after upstream changes
land; pin updates and releases are not feature completion gates.

## Authority and identity

Associate each store with one issuer/world by caller convention. The store does
not retain an issuer, consult a delegate or global registry, or prove liveness
on lookup. `ContainsKey(ticket)` means the exact ticket is locally stored. An
orphan can remain locally present after the issuer destroys its agent. An empty
or cleared store cannot distinguish a stale/forged ticket from a live one.

Before attachment, validate with the issuer and attach within the same
caller-controlled structural phase or synchronization boundary. Remove related
components before issuer removal; allow issuer reuse only after that cleanup.
Neither owner is thread-safe. A separated check and attachment can race if the
caller permits issuer mutation between them.

The frozen membership rules are:

- At most one entry per **index**, not one per complete pair. Reads, updates and
  removal require exact Index and Generation equality.
- TryAdd rejects both exact duplicates and a different generation occupying the
  same index, without mutation or replacement. No "newer wins" comparison:
  finite wrap and rollback do not provide an authority order.
- Remove of a mismatched generation returns false and cannot remove a current
  entry. Replacing a generation requires explicit removal of the known stored
  ticket, issuer validation, then attachment of the desired ticket.
- All UInt32 generations, including zero and MaxValue, are valid key data.
  Existing `SwiftTicketMap<T>` can issue `(0,0)`, so default cannot be a
  universal absence sentinel. The dense issuer still rejects default under its
  own rules.
- Component removal/Clear never increments generations or retires tickets.
  Removing and reattaching Health to one live agent uses the same agent ticket.
  Delayed work needing Health-incarnation protection requires an application
  revision or another explicit contract.
- SwiftTicket contains no issuer/world/timeline identity. Foreign numeric
  aliases and abandoned-future tickets cannot be distinguished locally. Preserve
  both fields; passing only Index loses generation protection.

Multi-store attachment may partially succeed if a later store allocation fails.
Optional component membership is independent; required-component rollback or
publication policy belongs to the caller. Do not promise an aggregate
transaction or add a coordination framework to disguise that boundary.

## Frozen caller surface

The accepted type names are SwiftTicketStore<T> and SwiftTicketStoreState<T>.
Phase 0 froze the following member signatures and exception parameters before
production implementation. The sealed partial owner implements
`IStateBacked<SwiftTicketStoreState<T>>` and `IEnumerable<T>`.

| Member                                                | Frozen contract                                                                                                                                                                       |
| ----------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Default constructor                                   | Empty store; reserve eight sparse and eight dense positions without accepting any key.                                                                                                  |
| Constructor `(int sparseCapacity, int denseCapacity)` | Separate lookup-length and live-entry reservations; zero uses empty storage. Positive capacities round to powers of two with minimum eight.                                             |
| Constructor `(SwiftTicketStoreState<T> state)`        | Validate and shallow-copy logical state; rebuild sparse lookup. No issuer/liveness validation.                                                                                          |
| Count                                                 | Number of locally stored components.                                                                                                                                                    |
| SparseCapacity / DenseCapacity                        | Actual lookup length and allocated live-value capacity; neither is issuer population or liveness.                                                                                       |
| Values                                                | Writable Span of Count live payloads in dense order; no writable key/metadata array.                                                                                                    |
| GetTicketAt(int denseIndex)                           | Copied external ticket at a live dense position; allocation-free. Structural mutation invalidates the position association, not the copied ticket value.                                                                               |
| ContainsKey(SwiftTicket ticket)                       | Exact local membership; false for absent, negative, unsupported or mismatched keys.                                                                                                     |
| TryAdd(SwiftTicket ticket, T value)                   | True only for insertion into an unoccupied index. False for either occupancy conflict, preserving contents, storage and enumeration. Invalid index and allocation failures still throw. |
| Add(SwiftTicket ticket, T value)                      | Throwing insertion using the same rule; no implicit upsert.                                                                                                                             |
| TryGet(SwiftTicket ticket, out T value)               | True with the shallow stored value on an exact match; otherwise false and default(T), without mutation.                                                                                 |
| GetRef(SwiftTicket ticket)                            | Writable borrowed ref on an exact match. Ref writes replace/mutate values without changing key ownership or structural version.                                                         |
| Remove(SwiftTicket ticket)                            | True on exact removal; otherwise false without mutation. Swap-back repairs the moved entry's lookup. No issuer mutation.                                                                |
| Clear()                                               | Remove local entries, release references and retain capacity; no issuer/history mutation. Empty Clear is a no-op.                                                                       |
| EnsureCapacity(int sparseCapacity, int denseCapacity) | Reserve both axes together without shrinking. Sparse capacity means largest accepted index plus one, not Count.                                                                         |
| State                                                 | Independent shallow snapshots of the three logical arrays in exact dense order.                                                                                                         |
| Concrete/interface enumeration                        | Values in dense order; concrete struct enumeration avoids boxing, interface enumeration is budgeted separately.                                                                         |

Frozen exceptions: insertion/reservation rejects out-of-range arguments with
ArgumentOutOfRangeException and the owning parameter name; Add rejects an
occupied index with ArgumentException (`ticket`); GetRef throws
KeyNotFoundException for every nonmatching key; GetTicketAt rejects an invalid
dense index with ArgumentOutOfRangeException (`denseIndex`). Malformed state
throws ArgumentException (`state`). Reconcile checked and throwing forms in
tests; preserve every existing collection's exception/upsert behavior.

Frozen cap: each array capacity is at most `2^30`, with ticket indices in
`[0, 2^30)`. Check bounds and Index+1 before arithmetic/growth. An index can
require enormous sparse storage even when Count is one; callers must budget
compact issuer IDs. Allocation can fail below supported limits. Hash-based
collections remain appropriate for huge or widely sparse external keys.

Allocation of every required replacement array completes before publishing
storage or membership. Failure preserves this store's contents, capacities,
borrowed storage and version. Actual growth and successful Add/Remove/Clear
invalidate enumeration, positions and borrows; no-op reservation and failed key
operations preserve them. Value/ref writes bypass version checks. Versions are
finite fail-fast checks, not lifetime identities. Never dereference an invalid
borrow in a test. Dense order is deterministic for the same initial state and
ordered mutations, not insertion-stable or canonical agent order.

State reconstruction applies the same zero/positive rounding policy to Count
and maximum stored index plus one; normalized empty state has zero capacities.
The default reservation is internal policy, not additional public constants.
Capacity arguments are validated sparse-first, then dense, before allocation;
invalid insertion reports `ticket` with its integer Index as the actual value.
State-constructor structural errors use `ArgumentException(state)`; JSON and
MemoryPack envelope/reader errors retain their codec-specific exceptions. This
does not promise malformed-state diagnosis before every possible allocation
failure.

The concrete `GetEnumerator()` returns public nested struct
`SwiftTicketStoreEnumerator : IEnumerator<T>`; generic and non-generic enumerable
implementations are explicit. Follow the dense issuer's established semantics:
MoveNext/Reset check structural version, generic Current returns its last
shallow cached value without a version check, and non-generic Current validates
position. Enumerator copies advance independently. Interface use boxes; default
construction is not an initialized enumerator. Production implementation and
its maintained regressions remain Phase 1.

Omit an upserting indexer, by-index mutation/removal, automatic replacement,
issuer binding, sorting/trimming, values-only CloneTo and a separate ticket span
initially. Values plus GetTicketAt provides paired iteration without exposing
mutable identities. Reopen extra APIs for an actual caller need.

## Existing owners, storage and reuse

| Existing code                                                                                                                                                                             | Reuse/comparison and boundary                                                                                                                                                                                                  |
| ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ |
| [SwiftSparseMap](../../src/SwiftCollections/Collection/SwiftSparseMap.cs)                                                                                                                 | Main shared-mechanics candidate: sparse lookup, packed values, swap-back, reference cleanup and capacity. Its Add is an upsert, keys are integers, state differs and it exposes writable key arrays; preserve those contracts. |
| [SwiftDenseTicketMap](../../src/SwiftCollections/Collection/SwiftDenseTicketMap.cs)                                                                                                       | Issuer for real scenarios; borrowing, reservation publication and value enumeration patterns. Keep issuance, free links, retirement and generation history here.                                                               |
| [SwiftTicketMap](../../src/SwiftCollections/Collection/SwiftTicketMap.cs)                                                                                                                 | Legacy zero-generation issuer and compatibility control; preserve its wrapping policy and slot-backed layout.                                                                                                                  |
| [SwiftTicket](../../src/SwiftCollections/Collection/Support/SwiftTicket.cs)                                                                                                               | Existing readonly token and exact equality. Do not change its CLR shape, hash or global serialization contract.                                                                                                                |
| [Sparse state](../../src/SwiftCollections/Serialization/State/SwiftSparseMapState.cs) and [dense owner state](../../src/SwiftCollections/Serialization/State/SwiftDenseTicketMapState.cs) | Explicit logical snapshots, readonly DTOs and existing serializer patterns. Do not reuse an incompatible schema just to share a type.                                                                                          |
| Guards, reference detection and hash/capacity utilities                                                                                                                                   | Reuse ChronicleGuard, RuntimeHelpers reference detection and suitable arithmetic helpers after checking their exact limit/sentinel behavior.                                                                                   |

Gravitas's `ColliderRegistry<TCollider>` demonstrates a separate dense list,
stable ID lookup and swap-back repair, plus independently owned replay order.
GridForge voxel storage uses externally owned integer IDs in SwiftSparseMap;
those are not automatically tickets. These are source analogues, not claimed
store consumers or authorized migrations. The requested public collection is the
planned production capability; realistic tests use real library owners/stores.

The selected dense runtime layout is:

```text
int[] sparse               index -> dense position + 1; zero means absent
SwiftTicket[] denseTickets exact external identities at live positions
T[] denseValues           packed payloads
Count, structural version
```

There is no issuer counter, free list or authoritative generation/retirement
history for absent components. Approximate metadata payload in bytes, excluding
array/owner headers and the T array:

```text
4 * sparseCapacity + 8 * denseCapacity
```

Sparse storage follows issuer index high-water; dense storage follows peak
component population. Removing entries does not shrink retained arrays. Clear
should reset only live lookup entries, O(Count), rather than scan the entire
sparse range.

Required invariants: sparse and reverse keys agree; each live position has a
unique in-range index and its exact generation; value/key dense prefixes have
equal Count; no key or removed reference is exposed outside that live range.
Swap-back changes positions, never the surviving external ticket.

Phase 0 compared both dense layouts and reviewed the narrow sharing boundary,
then measured an existing-collection composition control: SwiftSparseMap<int> for index-to-dense
position, SwiftList<T> for values, and SwiftList<SwiftTicket> for reverse keys.
The latter checks full tickets, repairs moved indices and privately rebuilds
all backing owners on growth. It is equivalent for the measured inputs; the
existing owners' exact-2^30 sentinel prevents shipping it unchanged under the
new capacity contract. A thin wrapper around
SwiftSparseMap<{Generation,T}> is only a by-value control: current SparseMap
offers neither public ref lookup nor dense-index lookup, and interleaving T with
generations loses a contiguous Span<T>.

Do the reuse review before expanding the first slice. Map equivalent lookup,
growth, swap-back and cleanup code, then select the smallest measured design. A
shared helper/core must have both real production consumers in this change,
preserve existing public/state contracts, and publish all arrays atomically. Do
not add delegates, per-operation boxing, generic allocator machinery or
runtime-dependent static-abstract interfaces to the netstandard runtime. If an
embedded mutable core is justified, prevent defensive copies and stale captured
copies, following the existing shared-query ownership lessons.

Do not mandate either a copied owner or a universal core in advance. Record
concrete duplication removed, retained distinct invariants and measured
old-owner costs. Small direct loops can remain when extraction adds machinery
without equivalent behavior; a broad refactor needs evidence and independent
review.

## Persistence and coordinated restore

The readonly partial `SwiftTicketStoreState<T>` has this frozen order:

1. `DenseValues : T[]`
2. `DenseSlots : int[]`
3. `DenseGenerations : uint[]`

SwiftTicket's public readonly fields lack JsonInclude/JsonConstructor. Scalar
parallel arrays avoid changing that default JSON contract or requiring
IncludeFields/global converters. The token is not MemoryPackable, but that alone
does not establish unsupported binary serialization: MemoryPack may use an
unmanaged representation. Verify the actual binary path separately. Confirm the
selected runtime representation and scalar wire shape with a disposable Phase 0
probe before freezing them.

The raw DTO retains arrays without validation/copying and normalizes constructor
nulls to empty; default fields can be null. The store accepts normalized default
as empty, validates equal lengths, unique indices, bounds and capacity
arithmetic, then copies values and rebuilds private lookup in locals. Different
generations at one index are still duplicate indices. All UInt32 generations are
permitted. Referenced objects remain shared through raw snapshots/restores.
Capture under external synchronization and do not mutate input arrays during
reconstruction.

Persist only current entries, preserving exact dense order. No issuer reference,
derived sparse table, spare capacity, free order, retired history, callback or
registry belongs in the state. Reconstructed capacity may differ. Structural
validation cannot establish that tickets are alive at the issuer.

Reuse IStateBacked, getter-only State, existing StateJsonConverterFactory for
ordinary hosts and explicitly closed StateJsonConverter plus generated metadata
for AOT. Standard uses generated MemoryPack Object paths; Lean retains JSON/raw
state without MemoryPack runtime APIs. Inspect actual newer-target versus
netstandard formatter bootstrap and generated reader semantics. No new comparer
registry, legacy migration or strict/versioned envelope is needed initially.

Test literal JSON field order and a binary oracle that independently writes the
owner/state headers and three arrays. Cover null/default and nullable/reference/
reference-containing payloads, nested fields/arrays/lists, malformed partitions,
truncation, oversized headers, omitted fields, zero-member records and by-ref
destination behavior. Require failed replacement to preserve the previous store,
associations and ordinary mutable payloads. Describe implemented
generated-reader behavior accurately; custom callback side effects, reader
position and partial writer output are not automatically transactional.

Exchange at least one control record between net10.0 and the actual
netstandard2.1 graph in both directions, comparing logical fields and replay.
Exchange JSON between Standard and Lean as well; binary support is Standard
only. Do not infer cross-target interoperability solely from separate same-host
round trips or a matching state type name.

Snapshot the issuer and every participating store at one structural boundary.
Reconstruct into locals and cross-check stored tickets with the restored issuer
before publishing the aggregate. Individually valid but mismatched snapshots can
be incoherent. Restore all participants and discard abandoned-future commands.
Use a test aggregate DTO for this scenario, not a new public rollback/aggregate
serialization framework. Aliasing/deep cloning and universal schema evolution
are not promised.

## Real paired-store scenarios and oracles

Maintain focused xUnit behavior/state/serialization tests in the existing areas.
Use actual SwiftDenseTicketMap and the new stores, integer inputs and an
independent logical membership/value model. Do not add a fake ECS or tests of
verification launchers. Fold explicit Index, Generation and payload fields for
traces: SwiftTicket.GetHashCode uses HashCode.Combine and is not a portable
deterministic trace; dictionary enumeration is not the oracle.

The first complete scenario is deliberately ordered differently across stores:

1. Issue A/B/C with domain IDs 101/202/303, tickets `(0,1)/(1,1)/(2,1)`.
   Position attaches A=0, B=10, C=100 in that order. Velocity attaches C=3, A=2,
   B=-1 in that order. Health attaches A=100, C=50.
2. Tick by iterating Position and looking up Velocity by GetTicketAt, never
   zipping dense positions. Position becomes `[2,9,103]`.
3. Remove A's Health and reattach 80 under the same agent ticket. Health order
   is `[C,A]`; issuer, Position and Velocity are unaffected.
4. Remove B's components before issuer.Remove(B). Spawn D=404, obtaining
   `(1,2)`; attach Position=-5, Velocity=4 and Health=25. Delayed removal using
   `(1,1)` fails in each applicable store and cannot affect D.
5. Two more ticks produce Position tickets `[A,C,D]` and values `[6,109,3]`;
   Velocity order is `[C,A,D]` and Health order `[C,A,D]`, values `[50,80,25]`.
   Compare explicit domain IDs, tickets and values, not only counts/checksums.

Snapshot after step 3, run steps 4-5, reconstruct issuer plus all stores and run
the same continuation through raw state, JSON and Standard MemoryPack. Require
the same tickets, per-store dense orders and explicit trace. No API allocates a
new agent ticket when only Health is removed/re-added.

Additional required counterexamples:

- Leave C's component while destroying C only at the issuer. Local membership
  remains true but issuer.IsValid is false. A different-generation insertion
  fails unchanged; a mismatched removal cannot silently clean it. Explicitly
  remove the known orphan, validate the replacement at the issuer, then attach.
- After replacement, old-generation lookup/GetRef/removal cannot access it.
  Conversely, an unguarded empty store can accept a dead key: demonstrate the
  authority limit and the correct guarded attachment in separate assertions.
- A delayed Health-instance command using only A's agent ticket can match
  reattached Health. Document the need for an application incarnation revision.
- Actual SwiftTicketMap-issued `(0,0)`, nonzero-index zero generation,
  UInt32.MaxValue, dense-issuer retirement, legacy wrapping and foreign aliases.
  Store operations never advance the issuer's generation policy.
- First/middle/last removal, independent dense/sparse growth, zero reservations,
  invalid/maximum index arithmetic, occupied-index false without allocation or
  version change, reference cleanup and structs containing references.
- Duplicate, null, default and mutable payloads, including payload equality/hash
  methods that throw if invoked, proving membership never consults value
  equality.
- Default/malformed state, duplicate indices with different generations, shallow
  array ownership, borrow invalidation, no-op preservation, copied and
  concrete/interface enumeration, and partially restored incoherent aggregates.

Do not allocate billions of entries or force process-wide OOM. Audit
allocation-before-publication and report fault-injection limits. Use reachable
arithmetic boundaries and disposable probes where necessary. Prove/remove truly
unreachable guards; do not waive coverage or put coverage gaps in the complexity
register.

## Performance and storage acceptance

Start with 128 and 4096 components, then one 16384 control and selected existing
32/128-byte/reference/reference-containing payload shapes. Avoid a full
Cartesian matrix. Distinguish **optional membership** (attach N components
spread across P issuer slots, with P chosen from N, 4N and 10N, without ever
filling P components) from **component churn** (fill P components then remove
down to N, retaining high-water capacity).

Compare the selected production design and the full-ticket composition with
appropriate BCL controls. Dictionary<SwiftTicket,T> with preissued keys is an
exact-key control for nonconflicting streams; it can hold two generations at one
index. For equivalent one-index conflict behavior, use a Dictionary<int,
ticket/value entry> with generation checks included in measured work. Do not
smuggle an O(N) occupancy scan into a baseline or move its policy checks outside
timing. Plain SparseMap without generation checks is not equivalent either.
Distinguish ref-return capabilities and include any secondary-index storage
cost.

Measure live/stale lookup, failed conflicting insertion, reserved attach/detach,
ref update, values-only and ticket/value iteration, Clear/refill, and the
complete joined Position/Velocity tick with optional Health churn. Separate
construction, growth, snapshot, restore and serializer costs. Successful and
nonthrowing typed reserved operations should allocate zero exact batch bytes; budget interface boxing and
cold work separately. Report owner/array counts, actual capacities, index
high-water, retained references and bytes per live component versus issuer slot.
Dense capacity need not equal sparse capacity; packed iteration does not imply
arrays shrink.

Freeze inputs, source graph, runtime, checksums and full operation counts. Use
balanced run order, verified loaded binaries and complete child/sample results,
not the benchmark launcher's exit alone. A reuse refactor must preserve the
existing sparse-map and dense-issuer warm baselines. Phase 0 establishes
practical time/storage budgets; do not invent a universal faster-than-BCL
requirement. Resolve introduced regressions here or obtain the user's explicit
disposition; uncertainty proves neither regression nor zero overhead.

### Lookup cost and optimization investigation

Closing the lookup gap is an explicit Phase 0 goal and a final hardening check.
First measure/profile the complete lookup path; distinguish metadata validation,
dependent array loads, bounds checks, call overhead and payload copying. Measure
ContainsKey, TryGet and GetRef separately with identical full-ticket validation,
including hits, absent indices and mismatched generations. Use both sequential
and deterministic shuffled access, component densities from the controls above,
and the actual joined tick. Report absolute ns/operation and full-tick cost, not
only percentages from a tiny hot-cache kernel.

Compare these dense representations before selecting one:

| Candidate                                           | Lookup and tradeoff to verify                                                                                                                                                                                                                                                                                                          |
| --------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Sparse position plus dense tickets                  | Baseline three-array layout above: find the dense position, then validate its ticket generation. Metadata payload is `4*S + 8*D` for sparse/dense capacities S/D.                                                                                                                                                                      |
| Fused sparse generation/position plus dense indices | Store `{Generation, DensePositionPlusOne}` together per sparse slot, with a reverse dense index array and packed T values. Position zero marks absence independently of generation zero. This can avoid loading dense ticket metadata during validation; measure actual code and cache behavior. Metadata payload becomes `8*S + 4*D`. |

The fused layout can cost more at low optional-component density, and
GetTicketAt must recover the generation through sparse metadata. Measure that
cost in paired ticket/value iteration, joined updates, swap-back, Clear, growth
and state projection as well as lookup. Absent metadata bits are not issuer
history or authority. Keep the same three-array logical state and every
membership rule.

Use actual rounded capacities for this comparison. The metadata difference is
`4*(S-D)` bytes: at S=16384 and D=4096, fusion adds 49152 bytes, or 50% metadata
over the baseline; at equal retained S/D capacities the metadata payload is
equal. Count the T array, headers and any shared-core storage separately. This
is a candidate tradeoff to measure and resolve, not an accepted memory increase.

Use a disposable direct-index value/metadata control to estimate the cost of
avoiding value indirection, retaining presence/generation checks and equivalent
input streams. SwiftTicketMap lookup with preissued tickets is another layout
control, not an equivalent store lifecycle. A direct slot-backed layout is not a
selectable shipping design if it violates packed iteration or the contiguous
Values span. Qualify its high-water storage and hole-scanning costs.

After attribution, test the smallest useful changes: reuse validated positions,
avoid repeated metadata loads or redundant validation, inspect inlining/bounds
checks where evidence points to them, and keep reserved paths direct. Each API
validates its key without forcing a redundant ContainsKey before TryGet or
GetRef. Reuse helpers only when their actual call cost and ownership remain
sound. Do not skip generation/safety checks, expose unchecked positions, or add
lookup caches and invalidation machinery merely to win a microbenchmark.

Keep the investigation bounded. Begin with the first paired slice, both dense
candidates and direct-index controls; use a focused code/JIT inspection before
adding another variant. Start with four-byte values at 128/4096 entries; expand
payloads or candidate combinations only to answer a concrete remaining question.
Select on the complete workload and storage budget, record all
adverse/inconclusive results, and recheck selected hot paths in Standard/Lean
and actual netstandard assets. Establish the remaining lookup delta and explain
any representation cost. Aim to reduce it without promising parity that
measurements do not establish, weakening the dense contract, or trading
unbounded sparse storage for a small timing gain. Resolve introduced regressions
and obtain explicit user disposition for material tradeoffs.

## Phases and exit evidence

| Phase                                | Work                                                                                                                                                                                                                                                  | Required exit evidence                                                                                                                                                                                                                                         |
| ------------------------------------ | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| 0 - Contracts and first slice        | Freeze remaining API, index/generation/conflict/default policies, scalar wire schema and restore rules. Profile lookup and compare dense storage/reuse candidates with the complete paired lifecycle and joined tick.                                 | Exact source graph, portable inputs/oracles, warm allocation/time and storage budgets, including lookup delta and joined-tick cost; explicit reuse decision with existing owners protected; serializer feasibility probe; independent review before expansion. |
| 1 - Dense store                      | Implement the selected store, minimum validated raw state/constructor, guards, borrow/version behavior and real paired scenarios. Consolidate only justified equivalent mechanics.                                                                    | Focused membership/conflict, zero-generation, swap-back, capacity, reference-release, invalidation and allocation tests; independent logical model and exact joined scenario; Standard/Lean and actual target coverage.                                        |
| 2 - Persistence                      | Harden JSON and Standard MemoryPack, closed metadata/bootstrap, coherent aggregate restore and all failure paths.                                                                                                                                     | Literal/independent wire oracles; full raw/serialized paired replay; malformed/truncated/by-ref tests; representative value/reference/AOT paths with actual netstandard assets.                                                                                |
| 3 - Full hardening and documentation | Review every prior phase for correctness, optimization, reuse, deletion and assertion strength. Recheck lookup improvements and residual costs; finish broader benchmarks, selection/lifecycle guides, API inventory and final supported-host checks. | Complete matrix below against final source; all findings resolved or explicit accepted limits/tradeoffs; readable checked guides/snippets; archive completed plan and update overview.                                                                         |

Keep design, decisions and concise phase results here. Use independent
ponytail-review at the first shared production boundary and before handoff.
Track runtime lines/files, helpers/consumers and duplication removed separately
from tests/docs/benchmarks. Complexity exceptions require rationale and full
coverage; they cannot exempt guards. Record confirmed out-of-scope defects in
the bug tracker and measured performance signals in the benchmark backlog;
feature scope stays in this plan/overview. No separate phase reports or links to
disposable outputs are needed.

## Implementation verification matrix

| Lane                                     | Required evidence                                                                                                                                                                                                                                                                                                                                                                                                                                           |
| ---------------------------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Release and ReleaseLean / net10.0        | Full solution builds, both test projects and fresh assembly-specific reports through the unchanged 100% line/branch/executable-method gate.                                                                                                                                                                                                                                                                                                                 |
| Debug / net10.0                          | Guard/default/index arithmetic, enumeration, allocation and scenario behavior; strict full coverage.                                                                                                                                                                                                                                                                                                                                                        |
| Release and ReleaseLean / netstandard2.1 | Both runtime assemblies compile with C# 11 and matching dependencies. Execute actual netstandard DLLs on a supported host; inspect framework attributes/bindings and report any BCL host overlays. Require full reported coverage in the managed host lanes.                                                                                                                                                                                                |
| Packages and XML                         | Both core IDs and their four target DLL/XML pairs; unchanged companion and matching companion variants remain compatible. Inspect all local package inputs and Lean's shim/no-MemoryPack shape without introducing publication gates.                                                                                                                                                                                                                       |
| Serializers and Unity                    | JSON in both variants; Standard generated MemoryPack. Actual isolated Editor Mono and Windows x64 High-stripping IL2CPP, with closed issuer/store/state/payload/aggregate metadata and formatter roots. Disable JSON reflection before first use; test value/reference/reference-containing payloads, conflict/default/growth and replay. Verify loaded graph hashes and identical explicit traces; no tracked adapter replacement or blanket preservation. |
| Public reference                         | Reconcile every authored public/protected type/member, partial, nested type and applicable tag with emitted XML for all targets/variants. Account separately for generated/synthesized and hidden explicit-interface APIs; summarize internal types. Fresh DocFX metadata/site, links/fragments, parameters, returns, property values, inherited comments and exceptions.                                                                                   |
| Collection-selection guidance            | Extend the existing collections guide rather than add a competing guide. Explain issuer versus store, ContainsKey versus liveness, all generation policies, one-index conflict, optional membership, joined iteration, component-incarnation/foreign/rollback limits and sparse storage budgets. Compile/execute paired examples; update README/overview/contributor routes as relevant and inspect desktop/narrow rendered diagrams/tables/code/API links. |

Use [CONTRIBUTING](../../CONTRIBUTING.md#local-validation) and each test
project's existing coverlet settings. Carry `UseLocalLsfStack=true` and global
`DisableTransitiveProjectReferences=true` through source-mode
restore/build/test, with serial `-m:1` builds and configuration-specific
restores. Preserve package mode and existing CI policy. One coordinator owns
shared build/coverage/timing outputs; reviewers can read in parallel. Record
actual provider revisions and compare the selected source graph with current CI
pins at each phase; do not treat local sibling tips as evidence for a different
pinned build.

## Decision dispositions and planning evidence

| Decision            | Disposition                                                                                                                                                 | Reopen when                                                                                                         |
| ------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------- |
| Public names        | Accepted by the user: SwiftTicketStore<T> and SwiftTicketStoreState<T>. Dense live iteration, swap-back and contiguous Values remain contracts.             | Only through an explicit separate API decision; lookup tuning must retain the dense guarantees.                     |
| Authority           | Local exact membership, explicit issuer validation at structural boundaries; no retained issuer/delegate.                                                   | A real consumer requires another authority model in separately scoped work.                                         |
| Generation/conflict | Accept every UInt32 generation; one index per store; reject all occupied-index additions without implicit replacement.                                      | A concrete consumer needs simultaneous generations or replacement, with stale-command/rollback semantics specified. |
| Storage/reuse       | Selected sparse positions plus dense tickets/values. Uplift only the shared cold growth policy with SwiftDenseTicketMap in Phase 1; keep identity/storage mutation owner-specific. | A real complete-workload profile justifies another representation or a simpler shared boundary without changing contracts/storage budgets. |
| State/wire          | Three parallel scalar/value arrays; no SwiftTicket codec or issuer/history fields.                                                                          | Actual codec/AOT probes establish a blocking defect or a concrete interoperability requirement.                     |
| Extra APIs          | Defer upsert, ticket span, trimming, component-instance identity and ECS coordination.                                                                      | An actual caller demonstrates the need and ownership/performance contract.                                          |

Planning inspected current source, state/serializer conventions, existing paired
ownership guidance, focused scenarios/benchmarks and CI/project shape. Three
independent read-only reviews covered API/storage, persistence/AOT, and
scenario/performance evidence. Their recommendations surfaced generation-zero
compatibility, occupied-index rejection, raw-ticket codec limitations, missing
public SparseMap ref lookup, and nonportable ticket hashing for trace oracles;
those are reflected above. This initial planning evidence preceded Phase 0; it
was not an executed prototype, benchmark, implementation build, coverage result
or Unity run. No runtime, test, dependency, CI or installed-skill changes
belonged to that
planning pass; executed Phase 0 evidence follows.

- Phase 0: complete; executed evidence and limits below.
- Phase 1: not started.
- Phase 2: not started.
- Phase 3: not started.


## Phase 0 results — contracts and first slice

Completed 2026-11-23. Maintained changes are this plan and the feature overview.
Runtime growth: zero lines, files, public APIs, helpers or new production
consumers. No maintained tests/benchmarks, dependency pins or CI policy changed.
Disposable comparison code supplied evidence without installing a prototype as
the Phase 1 implementation.

### Source graph and baseline checks

The starting SwiftCollections revision was
`41a2f770aed17f5af388ba3f4b3f769769ada7c6`, with clean worktree and index tree
`0cb5302f5fe7f3d0e84d5f20e8ead4d9aea70e32`. Source builds used Chronicler
`0281469ecc40e65a05867f473bc10f030e7f69f8` and FixedMathSharp
`8fdf889322f1bb82501c2afec63d6bb2f304ace2` (v9.0.0). The latter matches build CI;
Chronicler CI remains pinned to `7b90d5923d4bb6e0e42c033ec697b9678d6b8ece`.
Local results do not establish a run of that older pinned graph or hosted CI.
No new provider API was required and no provider or pin was changed.

Windows x64, Intel Core i7-9700K, SDK 10.0.302, runtime 10.0.10. All source
restore/build/test commands carried `UseLocalLsfStack=true` and global
`DisableTransitiveProjectReferences=true`; builds were serialized with `-m:1`.
The baseline commands were:

```powershell
dotnet build SwiftCollections.slnx -c <Release|ReleaseLean> -p:UseLocalLsfStack=true -p:DisableTransitiveProjectReferences=true -m:1
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c <configuration> --no-build --no-restore -p:UseLocalLsfStack=true -p:DisableTransitiveProjectReferences=true -m:1 --filter "FullyQualifiedName~SwiftSparseMap|FullyQualifiedName~SwiftDenseTicketMap|FullyQualifiedName~SwiftTicketMap"
dotnet test tests/SwiftCollections.FixedMathSharp.Tests/SwiftCollections.FixedMathSharp.Tests.csproj -c <configuration> --no-build --no-restore -p:UseLocalLsfStack=true -p:DisableTransitiveProjectReferences=true -m:1
```

Both solution builds passed for both runtime assemblies and both target
frameworks. Existing owner tests passed 195 Standard / 174 Lean; unchanged
companion tests passed 100 in each variant. Existing xUnit analyzer warnings
remained. This phase collected no new coverage: the full implementation
coverage matrix above remains required when production code is added.

### First-slice behavior and persistence

All three operational candidates executed the specified A/B/C-to-D scenario
with the real SwiftDenseTicketMap issuer, including a raw snapshot/restore
continuation of issuer plus all stores. Exact final Position tickets/values
were `[A,C,D] / [6,109,3]`; Velocity and Health independently used `[C,A,D]`,
with Health `[50,80,25]`. Per-index membership/value models checked 6000 seeded
mutations per candidate, including unique dense indices and exact generations.
The deterministic xorshift32 model seed was `0x0C013A55`.

Checks also passed for zero reservations, real legacy `(0,0)` issuance,
UInt32.MaxValue key data, occupied-generation no-ops, stale removal/ref failure,
private growth, copied raw arrays, null/duplicate payloads and payload
Equality/GetHashCode methods that throw if called. NoInlining weak-reference
setup plus collection while keeping the owner alive verified Remove/Clear
release reference payloads and structs containing references. The actual timed
joined kernels additionally checked every ticket's expected value outside the
clock; aggregate sums alone were explicitly insufficient to detect a wrong
positional join.

Serializer-shape libraries compiled as net10.0 and C# 11/netstandard2.1 in
Standard/Lean. Selected scalar, nullable, string/null, reference and
reference-containing payloads passed ordinary-factory JSON and closed generated
JSON with reflection disabled before first use. The default JSON oracle was:

```json
{"State":{"DenseValues":[101,202,303],"DenseSlots":[0,7,3],"DenseGenerations":[0,4294967295,9]}}
```

This freezes the default field/order convention, not arbitrary caller JSON
naming/options. Standard MemoryPack matched an independent writer: owner header
1, state header 3, then the three arrays, totaling 50 bytes for that record.
SwiftTicket alone emitted 8 unmanaged bytes and retained both fields; its default
JSON was `{}` and lost both fields. This confirms scalar state without changing
the existing token codec. Generated readonly-state readers allocate decoded
arrays rather than reusing existing mutable payload arrays. Selected duplicate,
negative-index, same-length mutable-payload and truncation failures preserved
the prior by-ref owner/payload. A zero-member by-ref owner record constructed a
new owner preserving the previous logical state; do not describe it as clearing.

Eight exchange directions passed: net10.0 ↔ netstandard2.1 in each variant,
and Standard ↔ Lean JSON at each target. Standard cross-target exchange also
checked binary fields. These execute actual target DLLs under .NET 10; selected
host BCL overlays were System.Text.Json, Encodings.Web, IO.Pipelines, Unsafe and
Collections.Immutable. DLL hashes and target-framework attributes were checked
for the candidate and owning libraries. They are not Unity/AOT executions.

### Storage choice, lookup attribution and budgets

Select **int[] sparse positions + SwiftTicket[] dense tickets + T[] values**.
Keep exact local generation checks and a single validated-position lookup per
operation. This preserves the dense span while avoiding per-issuer-slot
metadata growth for optional components. No adaptive layout or cache is needed.

Balanced forward/reverse/reverse/forward independent processes each completed
812 rows, seven measured batches after 35 warm batches. Main comparison used
fully optimized JIT code with tiering disabled; a separate warmed default-tiered
control and selected paths were checked in Standard/Lean for both actual target
assets. These are bounded local stopwatch measurements, not universal latency
or statistical-equivalence claims.

Inputs: int payloads; N=128/4096 with issuer spreads 1/4/10, plus a 16384-entry,
spread-four control. Insert key `i*spread`, generation one, value `index+1`;
velocity `(i % 5)-2` in reverse attachment order; Health every fourth key at 100.
Sequential and xorshift32-shuffled lookup/churn inputs used shuffle seed
`0x00A55A13`. Each batch processed 65536 components/keys. Joined measurements
used the resulting post-churn dense order, not a claimed sequential dense join.
Spread-one absence was outside sparse capacity; other spreads used internal
holes. Conflicts retained the occupied index with generation two. Joins used
exact tickets and included position writes; optional joins also looked up
Health and removed/reattached one Health entry per tick.

Representative N=4096, spread four, shuffled-input results below are ranges of
four process medians after the same inlining hint on all dense candidates.
Lookup is ns/call; joined rows are microseconds per complete 4096-entry tick.

| Work | Dense tickets, selected | Fused sparse generation/position | Existing-collection composition |
| --- | ---: | ---: | ---: |
| TryGet | 1.69–1.73 ns | 1.54–1.99 ns | 2.88–2.95 ns |
| GetRef | 1.65–1.93 ns | 1.64–1.75 ns | 2.85–2.98 ns |
| Joined Position/Velocity tick | 11.31–11.59 µs | 13.78–14.23 µs | 17.90–20.62 µs |
| Tick with optional Health/churn | 21.03–21.63 µs | 28.79–29.75 µs | 30.88–33.89 µs |

The one-index Dictionary<int, generation/value> control included generation
checks: TryGet 4.97–5.55 ns. Dictionary<SwiftTicket,int> was only a nonconflicting
exact-key control, 13.63–14.95 ns; it is not equivalent for occupied-index
conflicts and its process-scoped hashing is not the replay oracle. SparseMap and
the dense issuer supplied layout/old-owner baselines, not equivalent lifecycles.
No existing owner implementation changed in this phase.

Focused JIT inspection found the fused GetTicketAt call was initially not
inlined while the direct form was. Applying the same AggressiveInlining hint
removed that call and reduced fused joined cost from about 6.02 to 3.41
ns/component. The remaining reverse-index/generation loads are representation
work. Do not attribute the original entire gap to storage alone. A prototype
Lean project initially disabled optimization by SDK default; its roughly
9 ns lookup results were invalid performance controls, retained and excluded.
The corrected optimized Lean rechecks passed.

Across final selected-path FullOpts checks, the direct representation's TryGet
was 1.66–1.73 ns versus direct-index control 1.54–1.58 ns. Warmed default-tiered
checks were 2.34–2.38 versus 1.55–1.58 ns: residual indirection remains and no
parity is claimed. Direct joined ticks remained 10.8–11.8 µs. Optional-Health
default results varied 20.71–28.46 µs; one net10.0 sample slightly favored fusion
for that optional workload. That adverse result is retained, not described as
an across-the-board timing win. Fusion requires more metadata at optional
component density and does not justify it for the selected workload.

At N=4096 with reserved S=16384/D=4096, measured int construction allocated
114808 bytes selected, 163960 fused and 147760 composition. Metadata alone was
98304 / 147456 / 131072 bytes; fusion adds 49152 bytes (50%) over selected.
Selected uses one owner/three arrays; composition four owners/five arrays.
At spread ten, rounding gives S=65536 and selected/fused construction was
311416 / 557176 bytes. Sparse reservations follow high-water indices, not Count.
The direct-index control used 196696 bytes at spread four and cannot supply the
packed Values contract after removal; it is not a selectable implementation.

Phase 1's storage/allocation budgets are one owner, three arrays,
`4*S + 8*D + sizeof(T)*D` array payload, and zero exact current-thread allocated
bytes for successful/nonthrowing typed reserved operations. Every accepted
measured batch reported zero bytes; interface, cold, snapshot/restore and generic-payload costs are
separate. The timing envelopes above are the reference-host comparison budget:
recheck the same full workload, retain old-owner baselines and investigate a
repeatable regression rather than accepting a coarse faster-than-BCL threshold.
The 128-entry spread-four selected joined tick was about 0.293 µs; the 16384
control about 89.5 µs, illustrating why no single universal ns budget is promised.

### Reuse disposition and next phase

Reuse ChronicleGuard without boxing tickets on successful guards, reference
containment detection, IStateBacked, existing state converters/closed metadata
and generated MemoryPack conventions. In Phase 1, lift the dense issuer's
seven-line cold growth policy into a small internal helper used by both the
existing issuer and new store. Preserve issuer retirement/free-list arithmetic
and SparseMap's existing sentinel/upsert/state contracts. Do not create a
helper until both production consumers arrive in that same change.

The review ruled out a universal sparse core before implementing another copy:
sharing the direct ticket metadata with SparseMap would split identities into
more arrays or require generic projection/metadata machinery. Their writable-key,
upsert and independent-growth contracts differ. The measured composition reuses
actual public owners but costs two additional arrays and three additional owner
objects; its private atomic growth also rebuilds the three owners. Its existing
exact-2^30 sentinel prevents an unchanged implementation of the frozen maximum.
Retain the small generation-aware lookup/swap-back/Clear loops where their
metadata invariants differ. Reopen broader sharing only for a concrete smaller
boundary with both real consumers, preserved public contracts and measured
neutrality or improvement.

Independent first-slice and measurement reviews found oracle gaps (Health ticket
order, duplicate dense model indices and aggregate sums masking bad joins).
Those were corrected in the disposable verification and re-executed before the
results above. No confirmed production defect or benchmark regression was
created or discovered, so no bug/signal entry was added.

Phase 1 implements the selected owner, validated raw state, enumeration and
maintained real-scenario regressions. Full coverage, every failure/codec path,
large/generic payload performance, Debug, fresh API rendering and actual Unity
Mono/High-stripping IL2CPP remain their listed later-phase gates. Physical
2^30 allocations and forced OOM were not attempted; publication safety was
source-audited, not established by OOM injection. This closes Phase 0 only.
