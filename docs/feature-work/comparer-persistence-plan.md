# Comparer Persistence Plan

- **Status:** Phase 1 pre-commit hardening review; version-one-only owner persistence; hot-path performance acceptance remains open.
- **Created:** 2026-11-19.
- **Owner:** SwiftCollections; coordinate with Chronicler only where its shared
  serialization boundary needs an extension.
- **Tracking:** [SC-Issue-021](issue-tracker.md#sc-issue-021).

## Outcome

Persist a collection's comparison rules with its contents so JSON and standard
MemoryPack restoration establish those rules before inserting or searching any
items. Unsupported comparers in supported Swift adapter paths must produce an
actionable failure instead of silently becoming defaults. JSON and the shared
persistence contract must work
in both standard and Lean packages, at `netstandard2.1` and `net10.0`.

Keep ordinary collection operations direct: no registry lookup, descriptor
allocation, wrapper comparer, new lock, or serialization branch in lookup,
comparison, insertion, removal or iteration. Resolve comparison metadata at
serialization boundaries. Keep the existing trusted raw-state path and its
copying costs intact.

Phase 0 froze the intended contract and recorded compatibility inputs, baselines
and transport experiments. Phase 1 implements the sorted-list family. Hash and
inherited families and the remaining compatibility gates stay in later phases.

## Source baseline and scope

Inspected SwiftCollections revision
`31cd3eec80dff86b95f8cab621cc7ca281b4e7cc` plus the staged SC-Issue-010 contract
documentation/tests, and clean sibling Chronicler revision
`3c1cfb536fb009e43d4667158607611dd94ba854`. Before implementation, record the
actual starting revisions and any local patches; these observations are not
release or upstream API availability claims.

Phase 0 executes from clean SwiftCollections
`a5371dc1caa9e1b81b631ac9ce88d5f03933ebc2`, with released Chronicler 1.0.0 and
MemoryPack 1.21.4 dependencies. SwiftCollections-Unity starts clean at
`6252fd5cbdade58e0019217449dcec62b5e72967`; its checked-in packages are not
replaced. Candidate plugin graphs are staged in isolated CI-template projects.

| Family | Comparer persistence work |
| --- | --- |
| `SwiftSortedList<T>` | Persist ordering comparer; restore directly with it. Preserve duplicates; no new stable tie-order guarantee. |
| `SwiftDictionary<TKey, TValue>` | Persist key equality/hash comparer; select it before any pair is inserted. |
| `SwiftHashSet<T>` | Persist element equality/hash comparer before insertion; preserve the documented difference between null state items and enumerable construction. |
| `SwiftBiDictionary<T1, T2>` | Persist both forward-key and reverse-value comparer roles, including effective reverse-map configuration. Construct both maps before population. |
| `SwiftObservableDictionary<TKey, TValue>` | Cover inherited key equality and direct/nested serialization; subscribers remain runtime-only. |
| `SwiftPackedSet<T>` | No custom comparer API. Verify its fixed equality and internal hash fallback still produce valid membership; do not add comparer injection to this type. |
| `SwiftList<T>`, queue, stack, observable list/array | No retained comparer. Keep existing formats and sequence order unchanged; `SortInPlace` comparison is operation-local. |
| Sparse set/map, slot/ticket map, 2D/3D arrays and bool/short specializations | No configurable retained comparer. Preserve ID, generation, free-slot and dimensional contracts and existing state formats. |
| Pools, views, enumerators, query indexes and FixedMathSharp companion wrappers | No independent public comparer-state contract. Verify inherited/container use and package compatibility; do not introduce new persistence APIs for these owners. |

Source owners are under [Collection](../../src/SwiftCollections/Collection),
[Observable](../../src/SwiftCollections/Observable),
[EqualityComparer](../../src/SwiftCollections/EqualityComparer), and
[Serialization/State](../../src/SwiftCollections/Serialization/State).
[SwiftHashTools](../../src/SwiftCollections/Utility/SwiftHashTools.cs) owns Swift
default selection and fallback comparer creation. Existing coverage includes
[collection tests](../../tests/SwiftCollections.Tests/Collection) and
[Chronicler state JSON tests](../../tests/SwiftCollections.Tests/Serialization/ChroniclerStateJsonConverter.Tests.cs).

### Existing boundary constraints

- `SwiftArrayState<T>` is shared by sorted lists, sets and comparerless
  collections. `SwiftDictionaryState<TKey, TValue>` is shared by dictionary,
  bidictionary and observable dictionary. Both currently contain only `Items`.
  Adding comparer fields would change unrelated consumers' schemas too.
- The Chronicler JSON factory requires exactly one `IStateBacked<TState>` and a
  matching public single-state constructor. Its converter accepts exactly one
  outer `State` property. An extra outer comparer property requires an explicit
  adapter or shared boundary change; a second state interface is not a shortcut.
- JSON attributes are compiled for both TFMs. Core `netstandard2.1` references
  System.Text.Json 10.0.9; sibling Chronicler references 10.0.7 there. JSON is
  not restricted to `net10.0`.
- Standard currently uses MemoryPack 1.21.4. Lean excludes MemoryPack source and
  uses Chronicler compatibility attributes. Lean must not acquire a MemoryPack
  runtime, generator, formatter interface or package dependency.
- The pinned MemoryPack format is positional, with limited default schema
  evolution. Its modern and `netstandard2.1` generated APIs differ; Unity also
  has custom-formatter and binary-layout constraints. Do not assume that adding
  fields, swapping generated DLLs or using member formatter attributes preserves
  compatibility. See the pinned upstream [format/version guidance](https://github.com/Cysharp/MemoryPack/blob/1.21.4/README.md#version-tolerant),
  [target guidance](https://github.com/Cysharp/MemoryPack/blob/1.21.4/README.md#target-framework-dependency)
  and [Unity limitations](https://github.com/Cysharp/MemoryPack/blob/1.21.4/README.md#unity).
- A registered MemoryPack formatter alone is not proof of interception. Modern
  generated packable paths can call the type's generated methods directly,
  while the standard-2.1 path uses provider dispatch. Verify generated methods,
  nested members and packable arrays/lists as well as top-level calls. See the
  pinned [writer implementation](https://raw.githubusercontent.com/Cysharp/MemoryPack/0f2fe0827907940768293b7bb81b9605a7d8cba8/src/MemoryPack.Core/MemoryPackWriter.cs).

## Persistence contract

### Separate trusted raw state from persisted comparison metadata

Use a versioned persistence envelope containing the current raw item state
and typed comparer descriptors. Do not change shared raw DTOs or raw-state
constructors merely to carry serialization metadata. The Phase 0 protocol is
implemented for the sorted-list family; remaining owner adapters follow later.

The target experience is ordinary collection JSON/MemoryPack persistence that
preserves comparers, including nested fields and collections. A standalone
opt-in wrapper is useful for compatibility/prototyping, but is not sufficient
to close this feature while ordinary serialization silently loses comparers.
New ordinary persistence uses only the strict envelope. Reject old state-only
owner records; do not add legacy readers, fallback or migration helpers.
Preserve comparerless formats. This is an intentional serialization format
change requiring a breaking-format release and upgrade guidance.

The guaranteed paths are concrete Swift collection roots and nested members,
including supported source-generated metadata. Version 1 rejects a BiDictionary
written through a SwiftDictionary base converter instead of dropping reverse
roles. General BCL interface/object polymorphic round trips require host-selected
concrete converters/type metadata; a runtime-dispatched object write alone is
not a supported polymorphic round-trip promise. `IStateBacked<TState>` remains
the raw-state contract. Host converters or type-erased contracts that bypass the
Swift adapter retain their own behavior; arbitrary serializer calls cannot be
guaranteed to throw. Test these boundary/bypass controls in the first slice.

Conceptual descriptor fields are a stable identifier, codec version, comparer
role and explicitly encoded configuration. Identifiers refer to registered
implementations, not assembly-qualified CLR names. JSON and MemoryPack encode
the same semantic fields without embedding JSON in the binary path. Do not
serialize executable code, delegates, arbitrary object graphs or type-activation
instructions.

Construct the intended owner once after resolving all descriptors. Do not first
restore using defaults and then call `SetComparer`: hash entries may already
have been discarded, while sorted items may pay for ordering twice. The
existing comparer-aware enumerable constructors are starting points, not a
requirement to repeat copying or change null/duplicate contracts.

For persisted sorted snapshots, copy the already ordered items with the resolved
comparer without scanning or sorting them. A minimal owner reconstruction hook
may be needed; keep it at the boundary. The explicit enumerable constructor
remains the caller's copy-and-establish-order path for arbitrary input. Contents
of trusted snapshots must obey their descriptor; descriptor validation does not
prove arbitrary item order. Any external-data ordering validator is an explicit
separately costed operation, not hidden in restore.

### Supported comparison rules

| Category | Proposed disposition |
| --- | --- |
| Swift seeded string/object equality | Recognize exact supported implementations; persist algorithm identity/version and signed seed. Object non-string values retain their own equality/hash portability limits. |
| Runtime default equality/ordering | Encode a supported role and exact element/key type's default policy. Culture-sensitive defaults follow the explicit culture policy below. Other types implement their own rules; no universal determinism claim. |
| Ordinal and ordinal-ignore-case string comparers | Recognize well-known instances without reflection over private runtime types. Distinguish equality semantics from a runtime's hash implementation; do not promise cross-runtime hash or Unicode behavior solely from the identifier. |
| Application custom comparer | Explicit registration associates a stable identifier/version and exact typed role with a small configuration codec and factory. Restore its configuration before owner population. |
| Delegates, closures, mutable/external dependencies | Reject unless the application supplies an explicit codec with a supported reconstruction contract. Do not infer equivalence by serializing captured fields or calling user `Equals`. |
| Culture-dependent comparison | No implicit current-culture recreation. Reject portable persistence unless an explicit supported codec/policy describes its runtime/collation compatibility limits. A culture name alone is not a frozen algorithm. |
| Swift randomized collision fallback | Persist its full signed 64-bit entropy and restore it without fresh randomness. Use the boundary-only fresh-table path below; preserve future ordinary mutation policy without promising identical future bucket traces. |

Custom registration must not affect record identity based on registration order.
Reject duplicate identifiers/versions/roles and incompatible decoded types.
Configure an explicit registry/context before use and make it immutable during
serialization. Avoid ambient mutable global registration or assembly scanning.
Prove that the chosen context can reach both JSON and standard MemoryPack paths,
including nested/closed generic types, without per-item registry work. Use
existing serializer configuration hooks where possible; only add shared APIs
for a demonstrated collection consumer.

Registration is not a proof of deterministic behavior. The application owns a
custom algorithm's contract and must change its semantic version when behavior
changes. Codecs must produce bounded configuration, avoid environment-derived
defaults, and encode equivalent configuration consistently. Define concrete
size/depth limits and overflow handling before accepting external descriptors.

Capture items and comparer configuration from one stable owner state. The caller
must prevent concurrent collection/comparer mutation during capture. Define
whether a codec produces a new comparer or a safe immutable singleton; do not
retain a caller's mutable configuration buffer as decoded owner state.

### Delegate-backed comparers

Lambda-backed comparison is supported at runtime; persistence requires an
explicit registered reconstruction rule. A noncapturing lambda is not
automatically a portable identifier. For example, an application can register a
particular `Comparer<int>.Create(static (x, y) => y.CompareTo(x))` instance under
its own stable descending-order identifier/version and provide a factory that
recreates it with no configuration payload. Resolve that factory once during
load; ordinary comparisons continue using the resulting comparer directly.

For capturing lambdas, encode explicit immutable configuration and let the
registered factory recreate the delegate from those values. Do not serialize
compiler-generated closure fields, method names, delegate targets or executable
code. A capture of mutable live state or services is unsupported unless the
application supplies a stable snapshot/reconstruction contract.

Bind registrations to an explicit comparer instance or supported descriptor
carrier; delegate wrapper type alone cannot identify a rule, since unrelated
comparisons can share that type. Do not infer lambda equivalence or inspect
private delegate storage. Verify unregistered lambdas fail on save, registered
noncapturing and configured capturing rules round-trip on both target graphs,
and distinct lambdas of the same wrapper type cannot select each other's codec.
This applies to ordering and equality/hash delegate rules. Unknown identifiers
still fail on load, including empty collections; no silent default fallback.

### Frozen Phase 0 protocol and entry points

Protocol version 1 has three ordered envelope members: `FormatVersion`,
`Comparers`, then `State`. JSON writes exactly those case-sensitive properties
in that order; the streaming reader requires metadata before state. A descriptor
has ordered `Role`, `Id`, `Version`, `Config` members. `Config` is base64 of the
codec's canonical bytes, including an empty string for no configuration.

MemoryPack writes object-header member count 3, UInt16 format version, a
descriptor array, and the unchanged raw DTO. Each descriptor has object-header
count 4, byte role, string ID, UInt16 codec version, then a byte-array config.
Null owners retain serializer null representation; non-null empty owners still
require descriptors. Supported binary profiles are little-endian, matching
MemoryPack's format requirements. Strings use the pinned serializer's string
encoding rules; equivalent options are necessary for byte-identity comparisons.

The caller's closed collection/element types supply the schema. This is not a
self-describing CLR type system. A codec is resolved for its exact closed type
and role; no payload-supplied type name can activate code.

| Role byte | Meaning / required owners |
| --- | --- |
| 1 | Sorted element ordering: SortedList. |
| 2 | Forward-key equality/hash: Dictionary, ObservableDictionary, BiDictionary. |
| 3 | Element equality/hash: HashSet. |
| 4 | Configured reverse-value equality: BiDictionary. |
| 5 | Effective reverse-map equality/hash: BiDictionary, even if equal to role 4. |

Write roles in ascending numeric order. Require exactly the owner's roles;
reject duplicates, missing/unexpected roles and descriptor counts above 3.
Codec versions start at 1. IDs are 1–128 lowercase ASCII characters from
`a-z`, `0-9`, `.`, `_`, `-`, `/`; `lsf.` is reserved. Limit each configuration to
4,096 decoded bytes, checking encoded length/overflow before allocation or codec
invocation. Require exact configuration consumption; do not accept trailing
configuration data. These are protocol bounds, not measured overhead budgets.
Enforce every metadata bound before allocation: inspect JSON raw token/escape
lengths before allocating decoded IDs/config, and binary string/array headers
before `ReadString`/`ReadArray`. Read at most the owner's expected descriptor
count and reject an extra element before allocating it. Checked prefix/length
arithmetic must reject negative, overflowing or impossible declarations.

| Built-in ID, codec version 1 | Roles / canonical configuration |
| --- | --- |
| `lsf.runtime-ordering-default` | Role 1; empty. Eligible comparable/enum types and supported nullable counterparts only; culture-sensitive string default is rejected. Application-defined default behavior remains the application's responsibility. |
| `lsf.runtime-equality-default` | Equality roles; empty. Represents the runtime policy, not reproducible runtime hash seeds. |
| `lsf.swift-string-seeded` | String equality roles; signed Int32 seed, little-endian. |
| `lsf.swift-object-seeded` | Object equality roles; signed Int32 seed, little-endian; non-string values retain their own hash limits. |
| `lsf.swift-string-randomized` | String equality roles; full signed Int64 entropy, little-endian. |
| `lsf.swift-object-randomized` | Object equality roles; full signed Int64 entropy, little-endian. |
| `lsf.string-ordinal` | Supported string ordering/equality roles; empty. |
| `lsf.string-ordinal-ignore-case` | Supported string ordering/equality roles; empty; runtime/Unicode compatibility limits remain explicit. |

Capture the effective owned Swift comparer rather than writing an ambiguous
"Swift default" alias. In particular, an explicit seed must not disappear when
the implementation also serves as a default. Preserve all entropy bits: the
current randomized comparer's own equality compares all 64 bits, although its
hash algorithm uses the low 31 bits. Culture-sensitive built-ins are rejected;
an application must register an explicit codec and own its compatibility limits.

Default-ordering eligibility uses public interface assignability, including
inherited/contravariant `IComparable<T>` and non-generic `IComparable`; nullable
classification follows its underlying type. Reject polymorphic `object` and
known noncomparable defaults even for empty owners unless explicitly registered
with an application codec. Do not scan items to infer support. Eligibility is
not proof that user comparisons cannot throw or depend on mutable state.

Public design surface, implemented and reviewed for ordering in Phase 1:

- `SwiftComparerRegistryBuilder` registers exact ordering instances,
  stable codec identity, bounded encoder and typed reconstruction factory;
  `Build()` returns `SwiftComparerRegistry` with immutable definitions.
  A custom decode factory returns the comparer and consumed-byte count; the
  framework requires that count to equal the complete configuration length.
  Built-ins validate their fixed lengths directly. This needs no general
  parsing framework or per-comparison callback. Equality registrations follow
  with the Phase 2 owner adapters.
- `SwiftCollectionJsonConverterFactory(registry)` supports ordinary options
  registration; a parameterless attribute path uses built-ins only and fails
  unknown custom rules. Typed closed-converter creators are also required for
  AOT/source-generated hosts, avoiding reflective generic factory discovery.
- Standard MemoryPack authored packable methods obtain that registry from
  `options.ServiceProvider.GetService(typeof(SwiftComparerRegistry))`. A registry
  can serve itself for a simple host; hosts with an existing service provider
  compose that service explicitly. Never overwrite unrelated host services.
  A null/missing service selects the immutable built-ins-only registry, matching
  JSON's parameterless path. Custom rules then fail clearly; ordinary supported
  built-in persistence does not require service-provider setup. A service of an
  incompatible type is an error, not silent fallback.
- Preserve concrete typed `RegisterFormatter()` bootstrap entry points for
  supported closed types. This avoids relying on reflective cold discovery;
  merely running a successful cold decode does not establish AOT readiness.
- No legacy owner entry points are exposed. Normal readers accept only the
  versioned contract; the raw-state APIs retain their separate trusted-state
  purpose and do not provide serializer fallback.

SwiftCollections owns these collection adapters; no new Chronicler hook or
package is required by the successful transport approach. JSON option converters
override the owner attribute; ordinary source-generated nested metadata must
select the same converter. Standard MemoryPack uses authored `NoGenerate`
packable entry points plus a formatter delegating to those methods, so generated
direct/nested paths cannot bypass the envelope. Lean excludes those binary
interfaces and files while retaining the registry and JSON contract.

Registration definitions are immutable, but factory-created comparers need a
recapture association. Use a registry-local `ConditionalWeakTable` keyed by the
produced comparer reference, with exact type/role, codec identity and an owned
immutable copy of canonical config. This supports capturing-lambda decode →
save → decode without collection fields, private inspection or comparer wrappers.
Version 1 requires immutable snapshot semantics for factory-produced comparers,
including delegates. Do not mutate captures then reuse the saved weak binding.
An explicitly registered source instance may encode its current public settings
at capture, but the resulting factory product is an immutable snapshot. A change
needs a new comparer/explicit binding; automatic mutable-product recapture is
outside version 1 rather than silently reusing stale cached bytes.
Reject conflicting same-instance/type/role bindings; never use registration
order or user `Equals` to resolve them. Run codecs outside association locks.
Association synchronization and allocation happen only at boundaries; weak keys
must not keep otherwise unreachable comparers alive. Another registry needs an
explicit registration/rebinding; no ambient cross-context identity is inferred.
Factories return a fresh comparer or an explicitly prebound immutable singleton.
They must not return an unregistered ambient shared instance and let a decode
cache implicitly claim it; that would change capture eligibility by operation
order. General freshness is a factory contract, not a private-state inspection.
Supported built-in instances have reserved canonical descriptors: reject
application aliases at registration and custom factory return before publishing
an association. Otherwise decoding an application alias of a shared default
singleton could change later captures according to operation order. Rejected
nonportable defaults have no supported canonical descriptor and can still use
the explicit-codec escape hatch. Reused custom singletons require identical
typed-role/codec/config bindings; contradictions fail.

### Determinism boundaries

Preserving comparison rules is a required guarantee. Identical hash slots,
enumeration, serialized bytes and future mutation traces are separate claims:
current raw state omits capacity, holes and insertion history. Do not derive
those guarantees from comparer identity alone or introduce canonical sorting
of hash snapshots as hidden extra work.

Dictionary and HashSet can replace recognized Swift comparers with randomized
collision-hardening variants. Capture the effective comparer, not only the one
originally requested. The bidictionary's configured reverse comparer can differ
from its reverse map's effective comparer after that transition. PackedSet has
the same internal hash concern despite lacking a custom comparer API.
For BiDictionary this can mean three configurations: forward comparison,
configured reverse equality used by owner operations, and a distinct effective
reverse-map hash comparer. Preserve their distinction, or explicitly reject that
case; encoding only two comparer instances can lose relevant behavior.

Restore exact effective seed configuration without fresh entropy.
Restoration must not sample time, process identity or randomness to
replace a persisted supported comparer. Keeping a seed still does not freeze
future fallback transitions or bucket order. Record which deterministic replay
scenarios are supported, test collision-heavy continuation, and explicitly
exclude unsupported randomization/collation scenarios. Do not disable existing
collision hardening or claim cross-platform iteration guarantees incidentally.
Reinsertion can itself reach a fallback threshold: the supported persistence
path must not silently generate new entropy during construction either. Prove
how it preserves the selected effective policy or rejects that scenario.

The selected implementation direction is owner-local fresh-table bulk
reconstruction for Dictionary and HashSet: size storage once using existing
capacity rules, install the resolved comparer, then place entries into the new
empty table using the existing hash mask and cumulative-square probe sequence.
Check duplicate equality during placement and bound probes by `2 * capacity`.
Duplicate rejection assumes a valid equality/hash contract; do not add an
all-pairs scan to repair a comparer whose equal values have different hashes.
Publish coherent count, mask, last-index, version and probe metadata only after
success. Do not call normal Add/Insert, resize, SetComparer or randomization.
The small fresh-table loops have no tombstones, notifications or fallback;
ordinary hot methods stay unchanged. This is a design commitment, not a proven
production implementation or a reason to introduce a delegate-driven table
abstraction. Phase 1 must prove the invariant and measured cost.

BiDictionary builds both maps through that path. Its configured/effective
reverse descriptors must match or form a recognized Swift fallback-equivalent
pair; reject contradictory arbitrary rules. Observable reconstruction uses the
base bulk path without restoration events. Future ordinary mutations retain
existing collision policy; canonical future iteration remains outside scope.

### Failures and ownership

- Reject unsupported comparers on save before publishing a completed payload.
  Define whether caller-owned writers can retain a partial prefix on failure;
  do not claim transactional streams without buffering and measuring that cost.
- On load, validate version, role, identifier and configuration and resolve
  both public bidirectional roles plus any distinct effective reverse-map hash
  configuration before population. Unknown/malformed data
  must fail, never fall back to a default.
- New-instance load must not return a partially populated owner. For supported
  overwrite/ref-existing paths, construct privately and publish on success;
  state whether instance identity changes and which old views become detached.
  Comparer callbacks may still mutate shallow referenced application objects;
  no deep rollback is promised.
- Enforce key/item and reverse-value uniqueness under the restored equality.
  A new-format payload claiming a valid collection must not silently discard
  collisions. Keep legacy/raw duplicate behavior distinct and explicit.
- Preserve null versus empty, shallow reference ownership, mutation-version,
  observable event/subscriber and sorted equal-item contracts. Do not add
  general `SetComparer` deduplication or fix unrelated collection defects here.

## Compatibility and migration

Use the following compatibility matrix when implementing production readers and
writers. Existing JSON is an outer `State` object; existing
MemoryPack payloads have no comparer metadata. An old payload cannot reveal
whether its source used a custom comparer. A new reader must not claim to
recover that missing information.

| Writer/input | Reader | Disposition / evidence |
| --- | --- | --- |
| Legacy owner, no descriptors | Legacy reader | Existing behavior, including comparer loss; 52 frozen direct/nested payloads checked. |
| Legacy non-null owner | New strict reader | Reject; implemented for SortedList. No legacy reader or migration helper is provided. |
| Selected v1 framing | Legacy direct/nested reader | Reject; observed for JSON and MemoryPack with actual old readers. |
| V1 writer | New strict reader | Supported-rule round trip implemented for SortedList; remaining families pending. |
| Comparerless owner or raw DTO | Existing matching reader | Format unchanged; no new metadata contract. |
| Null owner | Matching nullable reader | Null remains null; no comparison rule exists to restore. |

Selected breaking change: new comparer-aware writers emit protocol version 1;
strict reads require descriptors. Do not offer legacy owner readers, migration
helpers or an implicit/default-comparer fallback. Do not guess rules from array
order. Existing raw-state APIs remain available for their trusted-state contract,
not as old owner-record readers. Upgrade writers and readers together. This
breaking-format default needs release/upgrade notes before promotion. Old
readers must reject new formats rather than silently ignore comparison metadata;
Phase 0 verified actual legacy direct/nested JSON and binary reader rejection
of the selected framing. This does not establish the complete new writer/reader.

Keep old JSON text and MemoryPack bytes as small literals in maintained
rejection tests, with producing revision/configuration and expected outcomes. Test direct and
nested collections, state DTOs, empty owners and ref-existing loads. Append-only
MemoryPack behavior or switching to a version-tolerant layout needs measured
fixtures; neither establishes new-writer/old-reader compatibility by assumption.

Use `netstandard2.1`/C# 11-compatible primitives for shared descriptors, codecs
and contexts. Do not require modern-only collection helpers, runtime code
generation or static-abstract runtime support. JSON and binary adapters can
have conditional implementations while retaining the same semantic contract.

Compilation is only one compatibility gate. Current Chronicler factory uses
reflection, `MakeGenericType`, activation and expression compilation. Reuse
explicit typed construction where practical; prove trimming/closed-generic
metadata and Unity Mono/IL2CPP registration rather than inferring AOT support
from a successful .NET build. Unity-specific harnesses stay outside core.

The sibling SwiftCollections-Unity project currently contains standard and Lean
plugin graphs including JSON; its editor is 6000.6.8f1 and package baseline is
6000.3. Reconfirm those versions at execution. Validate core JSON/MemoryPack in
the supported actual Unity consumers. Native Unity `[SerializeField]` and
`JsonUtility` wrappers persist their own item fields; they do not automatically
inherit this metadata. Extending those native formats is separate adapter work,
not a promised outcome of this core JSON/MemoryPack feature.

Use SwiftCollections-Unity as the existing development smoke-test host. Start
from its `Tests/EditMode/SwiftCollectionsPluginSmokeTests.cs` and existing
`build-and-test` workflow, extending coverage of real core JSON/MemoryPack calls
with the candidate `netstandard2.1` plugins. Exercise standard and Lean in their
supported separate package graphs and identify the loaded dependency revisions.
Reuse that repository's package sync and validation workflow; do not build a
second Unity harness in core. Editor smoke tests are the early feedback gate;
the supported IL2CPP consumer remains a separate acceptance gate.

## Delivery phases

### 0. Freeze the wire and comparison decisions

- [x] Capture current ordinary JSON/MemoryPack behavior and old fixtures before
  changing schemas. Include the SC-Issue-010 descending example and the custom
  equality cases where default restore collapses entries.
- [x] Settle the decision log below, the built-in identifier table, strict versioned
  read rules, public entry points and ordinary nested serialization integration.
- [x] Establish baselines for raw state, ordinary serialization, reconstruction
  and hot collection operations with existing package dependencies.
- [x] Verify the `netstandard2.1` MemoryPack/Unity adapter approach before using
  it as the common design. Record the first supported Unity version and profile.

### 1. Prove one complete caller-facing family

- [x] Implement SortedList first: built-in default plus a registered, configured
  deterministic custom ordering, including a type with no default comparison.
- [x] Cover ordinary direct and nested JSON in standard/Lean and MemoryPack in
  standard, on both TFMs' compatible dependency graphs. Confirm comparer
  resolution precedes use and custom data never takes a default-comparer pass.
- [x] Verify MemoryPack top-level, generated direct methods, nested members,
  packable arrays/lists and supported base/interface paths all use the same
  envelope or reject unsupported dispatch explicitly. Do not accept only a
  global formatter registration control while generated paths bypass it.
- [x] Demonstrate valid lookup/add/remove continuation, unknown-codec failure,
  legacy rejection, private construction and unchanged source storage.
  Persisted sorted restore must invoke no ordering comparison or re-sort.
- [ ] Measure boundary cost and raw/hot-path controls. Obtain independent
  `ponytail-review` before expanding the pattern; resolve introduced overhead.

### 2. Complete hash and inherited families

- [ ] Dictionary and HashSet: exact typed equality/hash policy, custom-distinct
  default-equal items, null handling, collisions, seeded/fallback variants.
- [ ] BiDictionary: both roles independently and together, effective reverse
  comparer capture after collision fallback without losing configured reverse
  equality, uniqueness rejection before publication, reverse lookups.
- [ ] ObservableDictionary: inherited state, ordinary nested serialization,
  subscribers/events and base/interface dispatch within existing contracts.
- [ ] Verify PackedSet's fixed-equality continuation and every comparerless
  family as unchanged-format controls. Account for companion wrappers and all
  four package IDs; do not add a custom comparer API where none exists.

### 3. Compatibility, performance and closeout

- [ ] Complete old/new fixture matrix, independent-reader checks, cross-TFM
  semantic round trips and actual supported Unity Mono/IL2CPP validation.
- [ ] Run full standard/Lean/Debug builds, behavior tests, independent assembly
  coverage, package checks and reference documentation checks.
- [ ] Publish concise per-phase results and unresolved limits here. Update
  evergreen serialization guidance, authored XML and migration notes only for
  the implemented contract; preserve CI selection/publication policy.
- [ ] Close SC-Issue-021 only after all in-scope owners and required gates are
  accounted for. Keep SC-Issue-010's historical trusted-state decision intact.

## Acceptance matrix and evidence

| Axis | Required checks |
| --- | --- |
| Framework/package | Core and companion `netstandard2.1`/`net10.0`; `SwiftCollections`, `.Lean`, `.FixedMathSharp`, `.FixedMathSharp.Lean`. JSON works in both variants; MemoryPack runtime functionality only in standard. |
| Behavior | Default/custom, empty/single/many, duplicates/ties/nulls, noncomparable items, both bidirectional roles, seeded/hash fallback, lookup/mutation after load, direct/nested/base/interface shapes. |
| Failure | Unknown/missing/wrong-role identifier, unsupported version/type, invalid/oversized config, duplicate registration, encoder/factory/comparer exceptions, malformed/truncated payload, rollback/publication and writer-prefix contracts. |
| Compatibility | Old/new JSON and binary reader/writer fixtures; same/cross-TFM supported payloads; Lean-standard JSON interchange; closed-generic/AOT metadata; actual Mono/IL2CPP runs. |
| Determinism | Fixed-input repeated processes and supported Windows/Linux/Unity runs; comparison/equality/configuration and declared replay traces match. Byte identity and hash iteration are tested only where explicitly promised. |
| Docs/packages | Adjacent DLL/XML in all eight configuration/TFM assembly outputs, package dependencies and contents, generated serializer API changes, rendered constructors/properties/exceptions/inheritance and local links. |

Tests currently run at `net10.0`, so default project references alone cannot
exercise the `netstandard2.1` assembly. Validate that asset with an isolated
compatible runtime/dependency graph and the actual Unity consumer. In particular,
do not copy only the standard-2.1 collection DLL into a modern MemoryPack host;
generated interface differences can invalidate that experiment. Identify the
loaded assembly and MemoryPack assets. Cross-framework wire interchange is a
separate test from loading different binary APIs in one process.

Use the existing [contributor commands](../../CONTRIBUTING.md) and
[validation guidance](../../AGENTS.md#validation-commands). In package mode:

```powershell
dotnet build SwiftCollections.slnx -c Release -p:UseLocalLsfStack=false
dotnet test SwiftCollections.slnx -c Release --no-build -p:UseLocalLsfStack=false
dotnet build SwiftCollections.slnx -c ReleaseLean -p:UseLocalLsfStack=false
dotnet test SwiftCollections.slnx -c ReleaseLean --no-build -p:UseLocalLsfStack=false
```

Also run Debug, each affected test project's existing coverlet settings, and
DocFX with warnings as errors. Preserve all reachable line/branch/method
coverage. Tests exercise production codecs and collections, not a duplicate
algorithm or a maintained harness testing its own project wiring.

If Chronicler needs a new shared hook, land it in the owning repository first.
Record coordinated source validation, hosted develop pin readiness and released
standard/Lean package readiness separately. Carry `UseLocalLsfStack=true` and
global `DisableTransitiveProjectReferences=true` through restore/build/test and
benchmark child builds; serialize source builds with `-m:1`. Source-mode success
does not establish published-package availability. Do not update dependency pins
or CI branch policy as a side effect of this planning pass.

### Performance acceptance

Use existing [benchmark fixtures](../../tests/SwiftCollections.Benchmarks) for
lookup/mutation controls and add only production persistence workloads needed
by the implementation. Separate cold registration/metadata setup from warm
operations; record allocations, encoded bytes, elapsed time and sample ranges.
Measure counts 0, 1, 8, 128, 4,096 and 65,536, integer/string/custom elements, configured
custom comparers and collision-heavy tables. Time encode, decode, reconstruction
and full round trip separately; identify snapshot copies, sorting and rehashes.
Hash reconstruction baselines must use the same intended comparer and retain
the same entries. Default restoration that loses custom-distinct entries is not
equivalent work and cannot be the performance baseline.

Requirements: no added per-item descriptor lookup, no default-then-custom
reconstruction, no additional raw-state comparisons/sorting, no new collection
hot-path allocations, and no measurable hot-path regression. Record one-time
descriptor costs and bounded metadata growth; establish numeric boundary budgets
from the first slice rather than inventing a zero-overhead claim. Any measured
tradeoff requires user disposition before expansion or completion. Validate
equivalent outputs, loaded dependencies and every benchmark child/report, run
without competing instrumentation, and retain adverse/inconclusive evidence.

The rejected scan/re-sort timings in
[SC-Benchmark-004](benchmark-signal-hardening-backlog.md#sc-benchmark-004---sorted-state-order-validation)
are historical context, not this feature's baseline or an accepted budget.

## Decision log

| Decision | Disposition / reopening criteria |
| --- | --- |
| Preserve raw DTOs and trusted-state APIs | Frozen Phase 0: keep schema/behavior, with boundary-only owner reconstruction hooks. Reopen only if the first production slice proves a concrete conflict and migration is agreed. |
| Ordinary serialization versus opt-in | Frozen: normal direct/nested JSON and standard MemoryPack use strict v1 metadata or fail. This requires a breaking-format release; opt-in-only wrappers cannot close the feature. Reopen for a demonstrated unsupported dispatch/compatibility requirement before promotion. |
| Descriptor ownership | Frozen: SwiftCollections owns roles, codecs and adapters. Existing JSON options and MemoryPack service-provider hooks suffice for the model; no Chronicler change or new package is selected. Reopen only for a missing real consumer boundary. |
| Custom activation | Frozen: explicit typed registrations, consumed-byte-checked codecs, stable semantic versions and registry-local weak produced-instance associations. Factory products have immutable snapshot semantics. No CLR type activation or automatic delegate capture; reopen for a concrete unsupported caller need. |
| Legacy input | Supersedes the Phase 0 migration-helper decision: the user selected version-one-only persistence and an intentional breaking change during pre-commit review. No legacy owner reader, helper or fallback. Reopen only for an explicit compatibility requirement with a supported format and acceptance evidence. |
| Hash fallback and culture | Frozen: exact full Swift seed/entropy config; fresh-table reconstruction without entropy; preserve future ordinary mutation policy and exclude canonical bucket traces. Reject known culture-sensitive built-ins; explicit custom codec owns compatibility limits. Reopen only on concrete algorithm/profile incompatibility or agreed broader determinism guarantees. |
| Binary format and Unity | Frozen v1 three-member framing and authored packable/provider convergence. Disposable model passes both ABI graphs and actual Unity 6000.3.9f1 Mono standard/Lean. Production integration and IL2CPP remain later gates; reopen the mechanism if either disproves feasibility. |
| Related defects | SC-Issue-013 base mutation/comparer replacement and SC-Issue-014 malformed PackedSet state stay separately scoped. If they block valid feature inputs, resolve their owning issue or record the concrete dependency before claiming this feature complete. |

## Phase 0 execution record — 2026-11-19

Phase 0 completed investigation and froze the initial direction; the legacy
helper decision was later superseded in the decision log above. No shipped
runtime source, public API, schema, dependency pin, CI policy or maintained
behavior test was changed. Only this plan, the tracker and compatibility fixture
data changed at that checkpoint. The production implementation follows in the Phase 1 record.

### Legacy compatibility inputs

The investigation captured 52 exact payloads across thirteen legacy families:
direct/nested JSON and MemoryPack. Capture asserted every direct and nested load
before recording values, sizes, digests and source/observed contracts. Its
manifest and protocol specimen were investigation material, not a maintained
fixture format. Phase 1 removes that packaging from the test tree and retains
the sorted-list legacy bytes directly in
[executable legacy-rejection tests](../../tests/SwiftCollections.Tests/Serialization/SortedListComparerPersistence.Tests.cs).
Those literals were captured at `a5371dc1caa9e1b81b631ac9ce88d5f03933ebc2`,
Release with published dependencies; its legacy runtime is unchanged at the
Phase 1 starting revision. Later families
must carry their own executable compatibility cases when implemented. The
nested capture label was the literal `fixed-envelope`.

Confirmed legacy observations: descending `[13,0,-7]` and noncomparable token
payloads retain item order but lose their comparer; dictionary/set identity
cases and either bidictionary identity direction collapse Count 2 to Count 1;
seed `-1234567` is lost (`alpha` hash 1208186387 becomes 1073536057). Null,
empty, normal list and default/raw DTO inputs are separately represented.
These are old-reader observations, not intended v1 behavior.

A separate 17-assertion framing probe wrote the selected three-member v1 layout,
read its exact state/configuration fields, and verified that actual current
SwiftSortedList direct/nested JSON and MemoryPack readers reject the new
records. The specimen records its exact JSON and binary bytes. It proves the
framing break is visible; it does not claim a complete production v1 codec.

### Transport and Unity feasibility

A disposable model owner contains tokens with no default comparison and an
explicit direction codec. Its authored MemoryPack `NoGenerate` methods and
formatter converge on one illustrative six-member envelope. This model layout
is deliberately distinct from the selected three-member production framing;
the experiment proves interception/construction mechanisms, not final wire
compatibility or integration into SwiftSortedList.

The model passes 111 assertions on each standard target graph and 52 on each
Lean target graph: direct, generated nested, packable array/list paths, automatic
source-generated JSON container dispatch, actual per-operation codec context,
unknown/missing context or version rejection, failed ref-existing publication,
configured lambda recapture and zero ordering comparisons during restore.
Exact nested context encode/decode counts establish selection of the supplied
JSON converter. Lean has no MemoryPack package/interface/formatter path.

Separate fresh processes pass cold decode and explicit-bootstrap decode on both
standard ABI graphs: one factory/context decode and zero restore comparisons.
Automatic discovery can use upstream reflection. Explicit concrete registration
is the selected bootstrap route; no claim of full reflection-free/AOT execution
is inferred from the cold process controls.

| Execution gate | Observed result |
| --- | --- |
| Model Release `net10.0`, modern MemoryPack ABI | 111 assertions pass; static-abstract marker present. |
| Model Release `netstandard2.1`, isolated modern host with full standard-2.1 MemoryPack graph | 111 assertions pass; static-abstract marker absent. |
| Model ReleaseLean both target-compiled assemblies | 52 assertions each pass; JSON/context retained, binary excluded. |
| Actual Unity 6000.3.9f1 Editor Mono, standard CI-template host | 25/25 EditMode cases pass: existing 21 plus four Phase 0 controls. |
| Actual Unity 6000.3.9f1 Editor Mono, Lean CI-template host | 24/24 EditMode cases pass: existing 21 plus three Phase 0 controls. |

Unity controls assert the loaded core and candidate adapter target
`.NETStandard,Version=v2.1`, exercise current core JSON, explicit comparer-aware
reconstruction, identity loss, compatible standard MemoryPack and the model's
transport checks. They use the existing Unity repository template and tests,
separate copied package graphs and fresh core plugins. Existing tracked packages
and native serialization adapters are untouched. The first verified Unity
profile is 6000.3.9f1, Editor Mono with standard-2.1 plugin graphs; IL2CPP and
other profiles remain Phase 3 acceptance gates, not implied successes.

Portable reproduction uses SwiftCollections-Unity's existing
`.assets/ci/scripts/prepare-unity-editmode-test-project.sh` twice with
`--test-family base` and `--dependency-flavor standard|lean`. Select that flavor's
base package/runtime assembly and stage matching core standard-2.1 plugins in a
copied package, then run Unity with `-batchmode -nographics -runTests
-testPlatform EditMode -assemblyNames SwiftCollections.Unity.Tests.EditMode
-runSynchronously -testResults <results.xml> -logFile <editor.log>` and inspect
XML counts. Omit `-quit` for test execution. The model controls and final wire
reader must become production tests in Phase 1; the disposable probe is not a
new maintained Unity harness.

### Baseline builds, dependency readiness and limits

Published-package-mode solution Release/ReleaseLean builds pass with zero
warnings/errors, compiling both runtime assemblies at both TFMs. Full net10
suites pass: Release core 1578 / companion 43; ReleaseLean core 1543 / companion
43. Model builds also pass without warnings after correcting prototype scoped
ref/nullability annotations; no warning suppression was added. No baseline
build/test failure or introduced shipped-runtime failure was observed.

Chronicler remains unchanged. The model uses existing released JSON and
MemoryPack hooks; no new upstream API, local-source dependency or pin is needed
for this direction. Hosted CI, production integration, new-format compatibility
regressions, coverage of new code, package promotion, actual IL2CPP and the full
all-family acceptance matrix remain later-phase work. No new runtime code
exists to cover or reference-render in this phase. Local fixture/link/whitespace
checks apply to this change; the previous documentation pass is not reused as
feature implementation evidence.

### Performance baseline procedure and interpretation

Host: Windows x64 build 26300, Intel Core i7-9700K, SDK 10.0.302, runtime
10.0.10, invariant culture, workstation/nonconcurrent GC. Starting source is the
clean revision above with published dependencies. Standard loaded core SHA-256
`E861146868CD22834FA5CE2FE48B9A675242CC44282D655DD4663152A1B83900`;
Lean `9A8593F84F013FBCDC8659E7BCE744ADFA2FE00D294217D784425DE4531537BC`.
The legacy fixture records MemoryPack/Chronicler versions and assembly hashes.

Eight successful processes ran without concurrent builds/tests/Unity/profilers:
forward/reverse/reverse/forward per variant. Forward traverses increasing counts
and workload order, reverse traverses both oppositely. Each process records five
samples per workload/count, alternating workload order. Counts are 0, 1, 8, 128,
4096 and 65536. Standard: 30 workloads, 900 samples/process, 3600 total. Lean:
22 workloads, 660/process, 2640 total. Every expected count/workload/sample is
present; no failed process or adverse sample is excluded.

Reproduction workload: sorted integers `0..Count-1` in default and descending
order; integer dictionaries map `i` to `3*i`; seeded string dictionaries map
`key-` plus invariant six-digit `i` to `i`, using seed `-1234567`. Snapshot inputs
and source collections are prebuilt. Correct reconstruction uses the existing
enumerable constructor and the SAME comparer. Dictionary hot controls reserve
`max(8, 2*Count)` capacity; sorted controls reserve spare capacity. Hot Contains
performs one hit plus one miss (two misses at Count 0); mutation performs one
successful Add/Remove pair. Strings are preallocated. Validate retained counts,
lookup and successful mutation outside the timed loop.

Boundary iterations are `clamp(262144 / (Count+1), 4, 20000)`. Hot paired
operations use 100000 iterations. Warm each workload for up to 256 invocations;
force collection before each sample, then use Stopwatch and current-thread
allocated bytes. Action dispatch/static-sink costs are included; typed state
sinks avoid struct boxing. Payload byte counts use UTF-8 JSON and MemoryPack byte
arrays; capture/setup and forced GC are outside warm timings.

The 384 standard / 192 Lean first-call setup records are NOT independent fully
cold observations: types share initialization across counts. In the two forward
processes whose first count is zero, first target JSON encode took 18.163/23.118
ms standard and 18.200/23.881 ms Lean; first target MemoryPack encode took
5.578/6.860 ms. Reverse-process Count-0 first-call rows are already initialized.
Do not average them into a cold-start claim.

Complete warm medians follow. `LEGACY-DEFAULTS` means the current serializer
loses custom ordering/hash configuration; those rows are historical work, not
equivalent correct-persistence baselines. `contains` and `add-remove` values are
per PAIR. Timings are calibration, not numeric acceptance budgets or a claim
of variant performance parity. Broad ranges (including roughly fivefold spread
in default dictionary reconstruction) require paired first-slice measurements
with stable warmup before any regression/overhead conclusion. All tested hot
pairs allocate zero bytes after warmup across the matrix.

#### Standard warm medians (microseconds per invocation)

| Workload | 0 | 1 | 8 | 128 | 4,096 | 65,536 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `sorted.default.state-snapshot` | 0.011 | 0.011 | 0.013 | 0.043 | 1.059 | 16.925 |
| `sorted.default.raw-state-restore` | 0.011 | 0.021 | 0.021 | 0.052 | 1.137 | 17.925 |
| `sorted.default.correct-enumerable-restore` | 0.017 | 0.022 | 0.037 | 0.355 | 16.370 | 325.900 |
| `sorted.descending.correct-enumerable-restore` | 0.017 | 0.021 | 0.042 | 0.985 | 53.534 | 1152.750 |
| `sorted.default.json-encode` | 0.272 | 0.261 | 0.309 | 1.286 | 35.976 | 711.175 |
| `sorted.default.json-decode` | 0.466 | 0.520 | 0.804 | 5.685 | 168.140 | 3117.512 |
| `sorted.default.memorypack-encode` | 0.056 | 0.057 | 0.062 | 0.133 | 2.314 | 45.287 |
| `sorted.default.memorypack-decode` | 0.031 | 0.049 | 0.050 | 0.111 | 2.329 | 33.575 |
| `sorted.descending.LEGACY-DEFAULTS.json-encode` | 0.340 | 0.260 | 0.311 | 1.230 | 37.683 | 635.413 |
| `sorted.descending.LEGACY-DEFAULTS.json-decode` | 0.554 | 0.543 | 0.815 | 5.728 | 169.820 | 2901.650 |
| `sorted.descending.LEGACY-DEFAULTS.memorypack-encode` | 0.073 | 0.058 | 0.063 | 0.134 | 2.342 | 45.300 |
| `sorted.descending.LEGACY-DEFAULTS.memorypack-decode` | 0.039 | 0.057 | 0.052 | 0.140 | 2.383 | 34.663 |
| `sorted.default.contains` | 0.004 | 0.007 | 0.014 | 0.028 | 0.044 | 0.057 |
| `sorted.default.add-remove` | 0.012 | 0.013 | 0.023 | 0.036 | 0.056 | 0.074 |
| `dictionary.int.state-snapshot` | 0.005 | 0.020 | 0.033 | 0.293 | 6.201 | 115.537 |
| `dictionary.int.correct-enumerable-restore` | 0.098 | 0.055 | 0.105 | 1.126 | 48.286 | 455.500 |
| `dictionary.int.json-encode` | 0.330 | 0.372 | 0.872 | 9.636 | 340.307 | 5474.663 |
| `dictionary.int.json-decode` | 0.567 | 0.821 | 2.785 | 35.906 | 1181.639 | 19946.775 |
| `dictionary.int.memorypack-encode` | 0.062 | 0.065 | 0.101 | 0.408 | 9.429 | 204.975 |
| `dictionary.int.memorypack-decode` | 0.117 | 0.128 | 0.175 | 1.360 | 56.486 | 441.900 |
| `dictionary.int.contains` | 0.009 | 0.008 | 0.008 | 0.008 | 0.008 | 0.008 |
| `dictionary.int.add-remove` | 0.014 | 0.013 | 0.013 | 0.013 | 0.013 | 0.013 |
| `dictionary.seeded-string.state-snapshot` | 0.006 | 0.019 | 0.049 | 0.841 | 39.992 | 748.675 |
| `dictionary.seeded-string.correct-enumerable-restore` | 0.124 | 0.080 | 0.208 | 2.726 | 137.213 | 2365.725 |
| `dictionary.seeded-string.LEGACY-DEFAULTS.json-encode` | 0.340 | 0.457 | 1.090 | 13.020 | 490.238 | 8767.050 |
| `dictionary.seeded-string.LEGACY-DEFAULTS.json-decode` | 0.585 | 0.908 | 3.214 | 42.123 | 1821.031 | 27120.125 |
| `dictionary.seeded-string.LEGACY-DEFAULTS.memorypack-encode` | 0.068 | 0.134 | 0.443 | 5.850 | 212.673 | 3695.725 |
| `dictionary.seeded-string.LEGACY-DEFAULTS.memorypack-decode` | 0.181 | 0.222 | 0.458 | 6.014 | 306.671 | 4561.125 |
| `dictionary.seeded-string.contains` | 0.019 | 0.023 | 0.024 | 0.025 | 0.025 | 0.023 |
| `dictionary.seeded-string.add-remove` | 0.030 | 0.028 | 0.027 | 0.030 | 0.028 | 0.033 |

At count 4,096, representative full sample ranges and allocations:

| Workload | Min–max microseconds | Bytes/invocation | Payload bytes |
| --- | ---: | ---: | ---: |
| `sorted.default.raw-state-restore` | 0.792–1.405 | 16464–16464 | 0 |
| `sorted.descending.correct-enumerable-restore` | 47.246–57.675 | 16464–16464 | 0 |
| `sorted.default.json-decode` | 143.090–195.776 | 66520–66520 | 19391 |
| `dictionary.int.correct-enumerable-restore` | 17.984–88.765 | 131192–131224 | 0 |
| `dictionary.seeded-string.correct-enumerable-restore` | 77.190–162.894 | 196728–196754 | 0 |
| `sorted.default.contains` | 0.041–0.057 | 0–0 | 0 |
| `dictionary.int.add-remove` | 0.013–0.014 | 0–0 | 0 |
| `dictionary.seeded-string.contains` | 0.024–0.029 | 0–0 | 0 |
| `sorted.default.memorypack-encode` | 1.657–3.286 | 32824–32824 | 16390 |
| `sorted.default.memorypack-decode` | 1.776–3.386 | 32872–32872 | 16390 |
| `dictionary.seeded-string.LEGACY-DEFAULTS.memorypack-decode` | 185.537–372.060 | 458896–458896 | 90118 |

#### Lean warm medians (microseconds per invocation)

| Workload | 0 | 1 | 8 | 128 | 4,096 | 65,536 |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `sorted.default.state-snapshot` | 0.024 | 0.019 | 0.021 | 0.053 | 1.288 | 23.538 |
| `sorted.default.raw-state-restore` | 0.030 | 0.029 | 0.028 | 0.061 | 1.280 | 21.062 |
| `sorted.default.correct-enumerable-restore` | 0.029 | 0.031 | 0.044 | 0.367 | 19.952 | 384.212 |
| `sorted.descending.correct-enumerable-restore` | 0.026 | 0.030 | 0.070 | 2.308 | 149.383 | 3490.675 |
| `sorted.default.json-encode` | 0.438 | 0.303 | 0.363 | 1.394 | 39.000 | 716.500 |
| `sorted.default.json-decode` | 0.723 | 0.603 | 0.957 | 6.990 | 211.907 | 3496.500 |
| `sorted.descending.LEGACY-DEFAULTS.json-encode` | 0.454 | 0.342 | 0.386 | 1.485 | 41.387 | 696.375 |
| `sorted.descending.LEGACY-DEFAULTS.json-decode` | 0.793 | 0.626 | 1.003 | 7.189 | 208.364 | 3465.650 |
| `sorted.default.contains` | 0.020 | 0.017 | 0.022 | 0.041 | 0.059 | 0.075 |
| `sorted.default.add-remove` | 0.033 | 0.022 | 0.028 | 0.041 | 0.063 | 0.081 |
| `dictionary.int.state-snapshot` | 0.012 | 0.027 | 0.032 | 0.341 | 8.859 | 174.950 |
| `dictionary.int.correct-enumerable-restore` | 0.124 | 0.088 | 0.177 | 1.723 | 46.627 | 537.575 |
| `dictionary.int.json-encode` | 0.410 | 0.439 | 1.039 | 11.283 | 377.527 | 6245.225 |
| `dictionary.int.json-decode` | 0.654 | 0.996 | 3.398 | 42.801 | 1475.590 | 22583.875 |
| `dictionary.int.contains` | 0.023 | 0.016 | 0.016 | 0.016 | 0.017 | 0.016 |
| `dictionary.int.add-remove` | 0.037 | 0.026 | 0.025 | 0.026 | 0.026 | 0.026 |
| `dictionary.seeded-string.state-snapshot` | 0.015 | 0.034 | 0.079 | 1.077 | 61.990 | 848.025 |
| `dictionary.seeded-string.correct-enumerable-restore` | 0.164 | 0.156 | 0.282 | 3.599 | 168.248 | 4986.550 |
| `dictionary.seeded-string.LEGACY-DEFAULTS.json-encode` | 0.446 | 0.513 | 1.298 | 15.534 | 565.631 | 12860.150 |
| `dictionary.seeded-string.LEGACY-DEFAULTS.json-decode` | 0.704 | 1.148 | 3.902 | 49.765 | 2229.358 | 32921.575 |
| `dictionary.seeded-string.contains` | 0.032 | 0.037 | 0.038 | 0.038 | 0.041 | 0.039 |
| `dictionary.seeded-string.add-remove` | 0.048 | 0.041 | 0.041 | 0.043 | 0.040 | 0.047 |

At count 4,096, representative full sample ranges and allocations:

| Workload | Min–max microseconds | Bytes/invocation | Payload bytes |
| --- | ---: | ---: | ---: |
| `sorted.default.raw-state-restore` | 0.817–2.186 | 16464–16464 | 0 |
| `sorted.descending.correct-enumerable-restore` | 126.721–183.565 | 16464–16464 | 0 |
| `sorted.default.json-decode` | 182.167–239.432 | 66520–66520 | 19391 |
| `dictionary.int.correct-enumerable-restore` | 22.862–98.852 | 131192–131224 | 0 |
| `dictionary.seeded-string.correct-enumerable-restore` | 106.697–783.925 | 196760–196760 | 0 |
| `sorted.default.contains` | 0.050–0.078 | 0–0 | 0 |
| `dictionary.int.add-remove` | 0.023–0.047 | 0–0 | 0 |
| `dictionary.seeded-string.contains` | 0.037–0.054 | 0–0 | 0 |

## Phase 1 execution record — 2026-11-19

Implementation starts from clean SwiftCollections
`bf69e2152dff02f5f9f3ad335afcddf7560b4e81`. No upstream API, dependency pin, CI policy or release version is
changed. Existing released Chronicler 1.0.0 and MemoryPack 1.21.4 provide the
required hooks; package-mode validation below does not claim hosted CI or a
coordinated-source-stack run.

### Implemented boundary and compatibility

`SwiftSortedList<T>` now owns ordinary strict-v1 JSON and standard MemoryPack
serialization. The production registry supports built-in ordering and exact
instance registrations with bounded, consumed-byte-checked configuration
factories. The first real consumer implements ordering only; equality codecs
and hash-owner adapters are not speculative scaffolding in this slice.

Both transports resolve comparison metadata before reading raw items and publish
an owner only after copying trusted order. Tests verify no comparisons or sort
during restoration, noncomparable elements, configured capturing lambdas,
load/save/load recapture, independent registries, aliases, conflicting factory
products, invalid metadata and failed ref-existing publication. Generated JSON
containers include explicit raw-state metadata. Standard binary tests cover
normal formatter dispatch, authored packable entry points, generated containing
members and packable arrays/lists.

An explicitly selected `SwiftSortedList<T>` base contract projects a derived
instance's base items and comparison role into a new base owner; it does not
promise derived fields. Exact derived/interface contracts require host
integration. General BCL interface projections, custom host converters and
Chronicler raw-state contracts do not acquire a new owner-persistence guarantee.

Normal readers reject legacy input. The initial implementation included an
explicit migration helper, which the user removed during pre-commit review in
favor of version-one-only persistence. No legacy reader or fallback replaces it.
The raw state DTO, state constructor, collection fields and ordinary
lookup/mutation algorithms retain their contracts. Unsupported custom capture
fails even for empty owners.

The former `Serialization/Fixtures/ComparerPersistence` directory is removed.
The exact legacy descending JSON and binary literals are consumed by maintained
[behavior tests](../../tests/SwiftCollections.Tests/Serialization/SortedListComparerPersistence.Tests.cs);
there is no plan-manifest fixture format or documentation dependency on temporary
captures. Other families need executable compatibility cases when implemented.

### Initial verification before pre-commit review

The results in this subsection describe the initial implementation, including
the subsequently removed migration helper. They remain historical evidence,
not validation of the changed public surface. Post-review validation is recorded
separately below.

Initial tests against the unchanged committed runtime fail twice as intended:
unregistered JSON and MemoryPack custom-comparer capture both succeed and lose
the rule instead of throwing. Those regressions pass with the implementation.
No baseline build or existing-test failure was found. Intermediate new-test API
and exact-exception/encoding assumptions were corrected; no final failure,
warning suppression, coverage exclusion or weakened runtime assertion remains.

Published-package mode (`-p:UseLocalLsfStack=false`): build the solution, then run
each test project with `--no-build`, `--collect:"XPlat Code Coverage"` and its
existing `coverlet.runsettings`.

| Configuration | Core tests | Companion tests | Core lines / branches / methods | Companion lines / branches / methods |
| --- | ---: | ---: | --- | --- |
| Release | 1702 | 43 | 5718/5718; 2686/2686; 1195/1195 | 121/121; 62/62; 35/35 |
| ReleaseLean | 1632 | 43 | 5623/5623; 2628/2628; 1178/1178 | 121/121; 62/62; 35/35 |
| Debug | 1700 | 43 | 7585/7585; 2694/2694; 1195/1195 | 155/155; 62/62; 35/35 |

All solution builds have zero warnings/errors and compile core plus companion
at `netstandard2.1` and `net10.0`. Checked all eight DLL/XML pairs and matching
payloads in `SwiftCollections`, `SwiftCollections.Lean`,
`SwiftCollections.FixedMathSharp` and `SwiftCollections.FixedMathSharp.Lean`.
Lean depends on the compatibility shim, not the MemoryPack runtime package.

With previous generated metadata/site directories moved aside,
`dotnet tool run docfx docs/api/docfx.json --warningsAsErrors` passes. The updated
family inventory reconciles 67 authored public/protected IDs in each standard
target and 63 in each Lean target, including 24/20 from new files. All applicable
summary/param/typeparam/returns/value tags and cref links pass. The builder's
implicit constructor is synthesized; the partial class shares its main type
documentation; private formatter overrides are excluded. The three authored
binary entry points replace generated interface implementations. Both companion
XML files remain covered in the eight-pair check.

Rendered registry, builder, JSON converter/factory/closed creator, the initial
migration helper and sorted-list pages showed correct parameters, returns, values,
exceptions and links, including inherited `CloneTo(output)`. The guide passes
desktop and 390-pixel layout inspection. All 105 generated HTML pages pass the
existing CI reference/local-path checks and an additional local-fragment check.
The five cohesive parser/registry complexity exceptions retain full reachable
line and branch coverage and are recorded in the evergreen complexity register.

Actual SwiftCollections-Unity smoke runs use clean sibling revision
`6252fd5cbdade58e0019217449dcec62b5e72967`, its existing CI staging template,
Unity 6000.3.9f1 Editor Mono and separate copied package graphs with freshly
built standard-2.1 plugins. Standard passes **26/26** EditMode cases; Lean
passes **25/25**. Each includes the existing 21 cases plus actual production
custom JSON, generated nested JSON, strict/raw controls and legacy migration;
standard also checks generated nested/packable binary, recapture and failed
overwrite. The controls assert the loaded target framework. Checked-in Unity
packages/adapters remain clean; no native Unity serialization-format change is
claimed. Run with the existing prepare script and `-batchmode -nographics
-runTests -testPlatform EditMode -assemblyNames
SwiftCollections.Unity.Tests.EditMode -runSynchronously`, inspect the result XML,
and omit `-quit`.

### Boundary measurements and disposition

Windows x64, SDK 10.0.302 / runtime 10.0.10, invariant culture, published
dependencies. The baseline is an isolated `git archive` of the starting HEAD;
candidate runs load this implementation. Identified actual core/dependency
hashes, validated ordered contents and continuation before timing, and required
every process to complete its expected rows. Do not compare legacy custom
serialization that loses its comparer as equivalent work.

The first four standard processes completed 4920 samples but exposed tiered-JIT
warmup drift. Retain them as inconclusive timing evidence, not selected medians.
The controlled comparison disables tiering, uses A/B/B/A process order and five
alternating-order samples at counts 0, 1, 8, 128, 4096 and 65536: **4920 standard
+ 3480 Lean samples**, all complete. Boundary iterations are
`clamp(262144/(count+1),4,20000)`, hot intervals 100000 pairs. Full GC precedes
each interval; setup and correctness checks are outside it. First-call setup
observations share generic initialization and are not independent cold budgets.

Default integer metadata adds **103 encoded JSON bytes / 50 binary bytes per
owner**, independent of item count. Allocations add **104/80 bytes** for JSON
encode/decode and **48/80 bytes** for binary encode/decode. At counts 0–128,
observed median additions are roughly **0.3–1.1 microseconds** for JSON and
**40–80 nanoseconds** for binary operations. The user explicitly accepted this
fixed boundary cost, conditional on passing hot-path checks. This disposition
does not approve per-item work or a lookup/mutation regression.

Measured custom empty binary decoding initially allocated roughly 752–769 bytes
per call. Attribution to each produced comparer's minimum-capacity metadata map
led to one weak table per registry/declared type with a small descriptor holder.
The same workload now uses roughly 369 bytes, including the created comparer,
configuration ownership and weak-table maintenance. Built-in capture bypasses
registry maps/cache locks. There are no extra owner fields or comparer wrappers.

Independent first-slice and handoff reviews found no remaining correctness
issue. Runtime growth is 11 new files / 951 lines (619 nonblank, noncomment
lines), plus the seven-line trusted reconstruction hook and adapter attributes
in the existing owner. Each new helper has the real sorted-list consumer;
there is no unused equality-family infrastructure or maintained probe framework.

Hot controls use byte-identical caller binaries within each flavor, CPU2
affinity, normal priority, zero-allocation assertions and checked continuation.
Four-process A/B/B/A runs complete 80 intervals per flavor; paired same-process
AB/BA loading completes 320 intervals per flavor. The first Lean hot/paired
controls are excluded: the disposable probe's default candidate-file discovery
selected a copied Standard DLL ahead of its correct Lean HintPath. Explicit
probe inputs and loaded-hash checks corrected that setup error; corrected Lean
controls complete with baseline hash `7DBAEC5BFDA34A76B8BD7AD085F02EEBA296655E1AA75ECDC418616DC9D27B1A`
and no MemoryPack runtime dependency. This does not invalidate the earlier full
Lean serializer measurements, builds, tests or Unity checks.

The accepted controls still do not establish unchanged timing. Standard paired
token mutation is +4.9%/+7.9%; Contains changes from -0.4% to +15.6% with load
order. Corrected Lean token mutation changes -3.2%/+3.3% and Contains
-7.9%/+4.7% with load order. All accepted intervals allocate zero bytes and pass
content/result checks. FullOpts JIT comparison finds equal integer
Search/Insert/RemoveAt and reference Insert/RemoveAt instruction streams;
reference Search is seven bytes shorter through generic-dictionary displacement
and branch encoding changes. That does not establish equal cycle cost.

Four final identical-core counterfactual processes (baseline/baseline and
candidate/candidate, both load orders) complete another 640 paired intervals.
The same code measures up to +6.7%/-6.1% between contexts; for example, baseline
Contains is 75.44 -> 80.22 ns in one load order and 78.02 -> 74.05 ns in the
other. This confirms that the measurement arrangement can create timing
differences without a feature change. It does not cancel the adverse candidate
results or prove equivalence.

**Disposition: Phase 1 performance acceptance remains open.** Preserve adverse
and inconclusive results; do not infer neutrality from range overlap or add
speculative padding/alignment changes. The next causal experiment and durable
inputs are tracked in
[SC-Benchmark-005](benchmark-signal-hardening-backlog.md#sc-benchmark-005---comparer-persistence-hot-timing-acceptance).
The user's fixed-boundary-cost approval does not waive this condition, and the
final Phase 1 checkbox stays open.

### Pre-commit review disposition — 2026-11-19

The user selected an intentional breaking format and removed
`SwiftSortedListLegacyMigration`. Its JSON and binary entry points, maintained
migration tests and public guidance are removed together. Legacy payload literals
remain only as strict-reader rejection controls. No alternate legacy reader,
default fallback or automatic migration is introduced. Raw-state contracts remain
unchanged. This supersedes the initial Phase 0/helper design; reopen only for an
explicit new compatibility requirement and agreed acceptance evidence.

Independent registry and transport reviews found no remaining correctness
defect. Added behavior tests prove that exact-instance registration never calls
application object equality/hashing and that mutating factory-visible bytes
cannot change recapture metadata. Two segmented-JSON cases protect empty and
nonempty configuration handling. Former migration cases are replaced by strict
legacy rejection, including unchanged binary ref-existing ownership.

The review also removes common-case stack work in JSON configuration decoding.
`ReadConfiguration` checks the raw string/sequence length before calling the
separate bounded nonempty decoder. FullOpts JIT inspection confirms that the
old path probed and cleared 10,928 scratch bytes before noticing an empty value;
the new empty path reserves only a small frame and does not reach that buffer.
Invalid tokens, bounds and canonical base64 rules retain their original checks.

The focused comparison uses byte-identical callers, .NET 10.0.10 Windows x64,
CPU2 affinity, tiering disabled, A/B/B/A process order and seven balanced samples
per case. Both baseline and candidate already exclude the migration helper;
only the empty-configuration optimization differs. Input is normal serialized
default integer owners at Count 0 and 128, plus a registered one-byte descending
rule at Count 128. Warm 20000 decodes per case; measure 100000 decodes per empty
interval and 10000 otherwise, with full GC before each interval and Count/lookup
validation outside it. All four processes complete their 21 intervals.

| JSON decode case | Before median / full range, ns | After median / full range, ns | Allocated bytes per decode |
| --- | --- | --- | --- |
| Default, Count 0 | 1483.8 / 1447.9–1521.7 | 1306.3 / 1292.6–1450.9 | 792 before/after |
| Default, Count 128 | 11277.2 / 11029.3–11404.8 | 11009.7 / 10818.3–11756.6 | 3016 before/after |
| Custom nonempty, Count 128 | 11933.0 / 11780.1–12957.9 | 11898.3 / 11830.0–15055.5 | 3240.6 before/after, including weak-table maintenance |

The empty-owner median improves about 12%; the larger/default and custom
controls overlap and support no broader throughput claim. Recorded core hashes
are `54DB022F4151819677A33C71A4861716B263841B04D1CFC2916725F3073189CF`
before and `03B0B6F7A23AC3D6D5E741C08DA761DCDB63679816F1FAC5FB284BAEA66E2031`
after. This is a serializer-boundary result, not a resolution of SC-Benchmark-005.

Current validation uses the existing published-package-mode solution builds,
test projects and assembly-specific coverlet settings:

| Configuration | Core / companion tests passed | Core lines / branches / methods | Companion lines / branches / methods |
| --- | --- | --- | --- |
| Release | 1704 / 43 | 5688/5688; 2668/2668; 1194/1194 | 121/121; 62/62; 35/35 |
| ReleaseLean | 1635 / 43 | 5606/5606; 2620/2620; 1178/1178 | 121/121; 62/62; 35/35 |
| Debug | 1702 / 43 | 7551/7551; 2676/2676; 1194/1194 | 155/155; 62/62; 35/35 |

All final builds pass without warnings/errors and compile both assemblies at
both target frameworks. All eight DLL/XML pairs match the four package outputs.
The current changed-family inventory is 64 authored public/protected IDs per
standard target (21 from new files) and 61 per Lean target (18 from new files),
with no missing applicable XML tags. The removed helper has no emitted XML or
generated reference page. Clean-generated DocFX passes with warnings as errors;
all 104 HTML pages pass local-path/fragment checks. Rendered guide and API pages
show the v1-only breaking contract at desktop and narrow widths.

Updated actual Unity 6000.3.9f1 Editor Mono smoke runs pass 26/26 standard and
25/25 Lean cases using rebuilt standard-2.1 plugins. The former migration smoke
now requires legacy owner rejection; standard verifies that failed binary reads
preserve the original owner. Existing tracked Unity packages remain clean.

The user's staged Phase 1 snapshot is preserved byte-for-byte; this review's
edits remain unstaged and uncommitted. No CI/dependency change, new coverage
exclusion or final introduced build/test failure remains. SC-Benchmark-005 stays
open; this review does not settle its timing question.

Phase 2, full cross-runtime/reader interoperability, hosted CI, other OS/CPU
architectures, trimming and actual IL2CPP remain unclaimed. SC-Issue-021 stays
open until all owners and later gates are complete.

