# Comparer Persistence Plan

- **Status:** Completed implementation and validation, 2026-11-20. All five
  families pass the required local, actual Mono/IL2CPP and hosted build/test
  gates. SC-Benchmark-006 subsequently closed by a
  [bounded no-source-change decision](../benchmark-signal-hardening-backlog.md#sc-benchmark-006---unattributed-native-exit-during-persistence-measurement);
  its historical native failure remains unexplained. No package
  release or universal runtime/AOT guarantee is claimed.
- **Created:** 2026-11-19.
- **Owner:** SwiftCollections; coordinate with Chronicler only where its shared
  serialization boundary needs an extension.
- **Tracking:** [Feature work overview](../feature-work-overview.md#comparer-persistence).

> Archived completion record: intermediate phase gates, next actions and
> measurements describe their recorded checkpoints. The final closeout and
> linked tracker dispositions supersede earlier open or blocked statuses.

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

Source owners are under [Collection](../../../src/SwiftCollections/Collection),
[Observable](../../../src/SwiftCollections/Observable),
[EqualityComparer](../../../src/SwiftCollections/Comparer), and
[Serialization/State](../../../src/SwiftCollections/Serialization/State).
[SwiftHashTools](../../../src/SwiftCollections/Utility/SwiftHashTools.cs) owns Swift
default selection and fallback comparer creation. Existing coverage includes
[collection tests](../../../tests/SwiftCollections.Tests/Collection) and
[Chronicler state JSON tests](../../../tests/SwiftCollections.Tests/Serialization/ChroniclerStateJsonConverter.Tests.cs).

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
implemented for sorted lists, dictionaries, hash sets, bidictionaries and
observable dictionaries; comparerless owners retain their formats.

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

Public design surface, implemented and reviewed for ordering and equality:

- `SwiftComparerRegistryBuilder` registers exact ordering instances,
  stable codec identity, bounded encoder and typed reconstruction factory;
  `Build()` returns `SwiftComparerRegistry` with immutable definitions.
  A custom decode factory returns the comparer and consumed-byte count; the
  framework requires that count to equal the complete configuration length.
  Built-ins validate their fixed lengths directly. This needs no general
  parsing framework or per-comparison callback. `RegisterEquality<T>` serves the
  equality roles; ordering and equality have separate exact typed identities
  even when one instance implements both interfaces.
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
ordinary hot methods stay unchanged. Phase 2 verifies the fresh-table invariant
and measures its cost; this is not a reason to introduce a delegate-driven
table abstraction.

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
writers. Legacy JSON is an outer `State` object; legacy
MemoryPack payloads have no comparer metadata. An old payload cannot reveal
whether its source used a custom comparer. A new reader must not claim to
recover that missing information.

| Writer/input | Reader | Disposition / evidence |
| --- | --- | --- |
| Legacy owner, no descriptors | Legacy reader | Existing behavior, including comparer loss; 52 frozen direct/nested payloads checked. |
| Legacy non-null owner | New strict reader | Reject for all five owners; independently checked with actual historical writers. No legacy reader or migration helper is provided. |
| Selected v1 framing | Legacy direct/nested reader | Reject; observed for JSON and MemoryPack with actual old readers. |
| V1 writer | New strict reader | All five owners checked across both target assets, Standard/Lean JSON and Windows/Linux. Representative generated JSON and standard MemoryPack cases pass actual Unity 6000.3.9f1 Mono and Windows64 High-stripping IL2CPP in both package graphs. |
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
- [x] Measure boundary cost and raw/hot-path controls. Obtain independent
  `ponytail-review` before expanding the pattern; resolve or obtain explicit user
  disposition of measured overhead. See the 2026-11-20 closeout below.

### 2. Complete hash and inherited families

- [x] Dictionary and HashSet: exact typed equality/hash policy, custom-distinct
  default-equal items, null handling, collisions, seeded/fallback variants.
- [x] BiDictionary: both roles independently and together, effective reverse
  comparer capture after collision fallback without losing configured reverse
  equality, uniqueness rejection before publication, reverse lookups.
- [x] ObservableDictionary: inherited state, ordinary nested serialization,
  subscribers/events and base/interface dispatch within existing contracts.
- [x] Verify PackedSet's fixed-equality continuation and every comparerless
  family as unchanged-format controls. Account for companion wrappers and all
  four package IDs; do not add a custom comparer API where none exists.

### 3. Compatibility, performance and closeout

- [x] Complete old/new fixture matrix, independent-reader checks, cross-TFM
  semantic round trips and actual supported Unity Mono validation.
- [x] Complete actual supported IL2CPP/High-stripping player validation for
  standard and Lean; install the native toolchain and execute both players.
- [x] Run full standard/Lean/Debug builds, behavior tests, independent assembly
  coverage, package checks and reference documentation checks.
- [x] Publish concise per-phase results and unresolved limits here. Update
  evergreen serialization guidance, authored XML and migration notes only for
  the implemented contract; preserve CI selection/publication policy.
- [x] Close SC-Issue-021 only after all in-scope owners and required gates are
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

Use the existing [contributor commands](../../../CONTRIBUTING.md) and
[validation guidance](../../../AGENTS.md#validation-commands). In package mode:

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

Use existing [benchmark fixtures](../../../tests/SwiftCollections.Benchmarks) for
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
hot-path allocations, and no repeatable, meaningful hot-path regression
attributable to the implementation under agreed runtime settings and equivalent
caller inputs without explicit user disposition. Record one-time
descriptor costs and bounded metadata growth; establish numeric boundary budgets
from the first slice rather than inventing a zero-overhead claim. Any measured
tradeoff requires user disposition before expansion or completion. Validate
equivalent outputs, loaded dependencies and every benchmark child/report, run
without competing instrumentation, and retain adverse/inconclusive evidence.

The rejected scan/re-sort timings in
[SC-Benchmark-004](../benchmark-signal-hardening-backlog.md#sc-benchmark-004---sorted-state-order-validation)
are historical context, not this feature's baseline or an accepted budget.

## Decision log

| Decision | Disposition / reopening criteria |
| --- | --- |
| Phase 1 performance | Accepted by the user on 2026-11-20: fixed boundary costs and remaining native-context timing uncertainty; SC-Benchmark-005 closes with no actionable hot-path regression identified. Preserve adverse measurements; no equal-cycle or universal-equivalence claim. Reopen for an attributable, repeatable meaningful caller regression, hot allocation or behavioral defect. Later families require their own evidence. |
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
[executable legacy-rejection tests](../../../tests/SwiftCollections.Tests/Serialization/SortedListComparerPersistence.Tests.cs).
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
[behavior tests](../../../tests/SwiftCollections.Tests/Serialization/SortedListComparerPersistence.Tests.cs);
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

**Disposition at the 2026-11-19 checkpoint: Phase 1 performance acceptance
remained open.** Superseded by the 2026-11-20 closeout below. Preserve adverse
and inconclusive results; do not infer neutrality from range overlap or add
speculative padding/alignment changes. The next causal experiment and durable
inputs are tracked in
[SC-Benchmark-005](../benchmark-signal-hardening-backlog.md#sc-benchmark-005---comparer-persistence-hot-timing-acceptance).
At that checkpoint the user's fixed-boundary-cost approval did not cover this
condition, and the final Phase 1 checkbox remained open.

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

The review introduced no CI/dependency change, new coverage
exclusion or final build/test failure. At this review
checkpoint SC-Benchmark-005 remained open; the later user disposition is
recorded in the closeout below.

Phase 2, full cross-runtime/reader interoperability, hosted CI, other OS/CPU
architectures, trimming and actual IL2CPP were unclaimed at this checkpoint.
SC-Issue-021 was still open pending all owners and later gates; the final
disposition appears in the Phase 3 closeout below.

### Hot-path isolation follow-up — 2026-11-19

At committed `cbd9a734082d7a002699766937b79dc547a8cebb`,
[SC-Benchmark-005](../benchmark-signal-hardening-backlog.md#isolation-pass--2026-11-19)
records the corrected optimized direct-caller experiment, input shapes, exact
core hashes, all results and exclusions. All 504 main intervals, 112 longer
churn intervals, 56 JIT-policy controls and 126 optimized same-core observer
intervals complete with correct behavior and zero timed allocations/GCs.

The Standard token-pair comparison changes from -6.1% to +9.4% thread-accounted
cycles when only the runtime alignment policy changes; separate disassembly
confirms native placement changes. Optimized Lean is about -0.1% under either
policy. Startup allocations independently affect fixed-binary Contains timing,
but the actual baseline/current DLL-size difference is not an established cause.
Longer Standard ordinal-string churn retains +3.4% cycles despite -1.3% wall
time. This establishes execution-context sensitivity without attributing the
responsible hardware events or proving equivalence.

Fresh package-mode Release and ReleaseLean solution builds pass both frameworks
with zero warnings/errors; tests pass 1704/1635 core and 43 companion per variant.
No runtime, API, maintained test, CI or dependency change is justified by the
investigation. Hardware tracing is unavailable under the current profiling
policy. At this checkpoint the final Phase 1 checkbox and SC-Benchmark-005
remained open for profiling or explicit user performance disposition. The
2026-11-20 decision below supersedes that next action.

### Phase 1 closeout — 2026-11-20

The user explicitly accepts the bounded investigation's remaining hot-timing
uncertainty and authorizes closing Phase 1. Together with the earlier acceptance
of fixed persistence-boundary costs, this completes the final Phase 1 checkbox.
[SC-Benchmark-005](../benchmark-signal-hardening-backlog.md#sc-benchmark-005---comparer-persistence-hot-timing-acceptance)
is closed as **no actionable hot-path regression identified**, with no runtime
fix and no claim of equal cycles or universal timing equivalence.

The disposition retains all adverse observations, including the Standard
token-pair sign reversal with JIT policy and +3.4% string-churn cycles despite
lower wall time. No repeatable meaningful caller elapsed-time regression
attributable to the feature, new warm allocation or added algorithmic work in
the inspected hot methods was established. Hardware tracing is no longer a
Phase 1 closure requirement; its unavailable evidence remains an explicit limit.
Prioritize reproducible costs in implemented persistence or representative
callers rather than tuning around an isolated native layout.

This documentation-only closeout relies on the recorded 2026-11-19 validation
of implementation `cbd9a734082d7a002699766937b79dc547a8cebb`: Release and
ReleaseLean core/companion builds at both TFMs with zero warnings/errors;
1704/1635 core and 43 companion tests per variant; the earlier post-review
Debug, full reachable coverage, all package/XML/reference checks and actual
Unity Mono smoke 26/25. The 798 corrected hot/control intervals passed their
behavior, zero-allocation and no-GC checks. These are dated prior results, not
new test runs for this closeout.

All Phase 1 tasks were complete at this checkpoint. SC-Issue-021 was still open; Phase 2 hash/inherited
families and Phase 3 full interoperability, supported IL2CPP, broader runtime
and release gates were not yet checked. This Phase 1 closeout did not include
Phase 2 implementation or a release. Reopening criteria are recorded with SC-Benchmark-005.

## Phase 2 execution record — 2026-11-20

Starting revision was clean `4fedfa1c3f3a1dc61e730dbda4ed1507046a7381`;
results include the Phase 2 implementation and test patch described below.
Published dependency mode,
SDK 10.0.302/runtime 10.0.10, Windows x64 build 26300. No dependency, CI policy,
raw DTO schema or ordinary lookup/insertion algorithm changes are included.

### Implementation and reuse

Dictionary, HashSet, BiDictionary and ObservableDictionary now use the same
strict-v1 owner protocol as SortedList. `RegisterEquality<T>` shares the existing
ordering codec/instance-binding lifecycle through an exact comparer-interface
key. One registration can serve all equality roles without crossing into
ordering registrations. Canonical aliases, configuration isolation, immutable
factory-product recapture and callback-outside-lock contracts are preserved.
Built-ins preserve signed Int32 seeds and all Int64 entropy bits; known object
comparers also work contravariantly on reference-type owners.

One internal JSON adapter serves the four hash families. JSON metadata helpers
and binary header/descriptor helpers also serve SortedList; the ordering-only
binary wrappers are deleted. A fixed tuple carries up to three descriptors,
removing measured per-operation descriptor-array allocations of 48 bytes for
single-role owners and 96 bytes for bidictionaries. Dictionary reconstruction
is reused by the inherited owners. Only the two small fresh-table loops remain
separate because their Entry layouts and null contracts differ; capacity sizing
is shared. No generic probing delegate or new collection instance field is added.
Authored binary entry points are required to cover generated direct/packable
dispatch as well as provider dispatch in both target APIs.

Hash restoration sizes once, resolves comparison before population, rejects
equal duplicates and publishes privately constructed tables without sampling
entropy. BiDictionary resolves roles 2/4/5 and admits identical reverse policies
or recognized Swift fallback-equivalent pairs; it preserves configured reverse
equality independently of effective hashing. Base Dictionary serialization of
a BiDictionary is rejected before its payload prefix. Observable restoration
raises no events and carries no subscribers; ref-existing success replaces the
owner and failure preserves the previous reference. Callback side effects on
shared application objects are not rolled back.

**Null-state disposition:** HashSet keeps its state-path null-item skipping
behavior in the new envelope. It rejects equal non-null duplicates. Enumerable
construction and ordinary Add still reject null. Dictionary keys and reverse
BiDictionary keys remain non-null. This implements the plan's state/enumerable
distinction; the envelope is not a general item-canonicalization guarantee.

### Behavior, compatibility and coverage

Four initial regressions fail on the starting implementation because ordinary
JSON/binary saves silently accept unregistered custom equality. They pass after
the change. Tests cover custom-distinct/default-equal entries, exact typed
registration, configured lambda recapture, wrong/missing/duplicate roles,
unknown metadata on empty owners, malformed configuration, duplicate entries,
legacy rejection, source preservation and exception/failure publication.
Independent handwritten envelopes test decoder rules separately from writers.

Collision-heavy fresh restoration preserves its recorded deterministic seed
without fallback. Actual ordinary BiDictionary additions exercise reverse-map
collision fallback before capture; round trips retain its configured seed and
exact effective entropy, then verify forward/reverse lookup and mutation.
Generated nested JSON, explicit closed converter creators, MemoryPack direct
packable methods, arrays/lists, formatter bootstrap, nulls and ref-existing
paths agree. Explicit host raw-state converter overrides remain a documented
bypass; these adapters do not control arbitrary host converters.

Fourteen comparerless families were compared against the frozen Phase 1 core:
list, queue, stack, PackedSet, sparse set/map, slot/ticket maps, 2D/3D arrays,
bool/short 2D specializations, observable list/array. Inputs use integers 3/1/2,
sparse IDs 7/2, a freed slot/ticket, or one assigned dimensional cell. All 28
Standard JSON/binary and 14 Lean JSON payloads are byte-identical. Existing
PackedSet continuation/fallback and companion suites pass; no comparer API is
added to those owners.

Final package-mode Release, ReleaseLean and Debug solution builds pass without
warnings/errors. Core tests pass 1789/1687/1787 respectively; companion tests
pass 43 each. Coverage uses the existing assembly-specific runsettings. Raw core
line/branch counts are 6073/6075 and 2884/2886 in Release, 5879/5881 and 2808/2810
in Lean, 8018/8020 and 2892/2894 in Debug. The only misses are the two local
probe-exhaustion throw lines and their branches; all reachable lines, branches
and methods are covered. The arrays are sized above count and probing visits
every slot within 2N attempts, so those guards require impossible local-table
corruption to fire. Retaining these gaps was subsequently corrected in the
[full coverage correction](#full-coverage-correction--2026-11-21); it was not a
waiver of the coverage gate. Companion coverage is complete
(121/121 Release/Lean lines, 155/155 Debug; 62/62 branches each).

Both runtime assemblies build at netstandard2.1 and net10.0 in both variants.
All eight assembly/XML pairs match the four package outputs byte-for-byte.
The complete authored core XML inventory checks 946 Standard / 931 Lean member
IDs at each TFM with no missing applicable tags; the emitted XML files contain
1179/1164 entries including selective internal comments. Generated/synthesized
APIs are outside the authored-member count. The four new owners replace their
generated packable methods with authored entry points; all five families have
15 authored binary members excluded from Lean. Clean generated DocFX metadata
and site pass with warnings as errors; all 104 rendered HTML pages pass local
path/fragment checks. Rendered registry, converter creators and all four owner
pages show the parameter, return, failure and inherited/raw-state contracts.

Actual Unity 6000.3.9f1 Editor Mono smoke passes 29 Standard / 27 Lean cases with
the netstandard2.1 plugin graphs, including new generated nested hash-owner
JSON, custom equality recapture and Standard nested binary cases. Isolated
smoke hosts are used; tracked SwiftCollections-Unity packages remain unchanged.
Actual IL2CPP/stripping and full cross-runtime wire interoperability remain
Phase 3 gates, not implied by these checks.

### Boundary cost and remaining limits

Freeze the Phase 1 core (`4fedfa1`, same runtime source as `cbd9a73`) and compile
one optimized net10 caller per flavor against it. Copy the dependency graph and
replace only the candidate core. Verify loaded hashes, caller identity,
optimizer and MemoryPack flavor. Run serial ABBA processes on CPU2, normal
priority, `DOTNET_TieredCompilation=0`, with no competing tests/builds/Unity.
For each of four families use default integer equality, counts 0/1/8/128/4096/
65536, keys/items 0..N-1, values `3*k+1`, and validate every lookup, reverse
lookup, absent key and count outside timing. Measure JSON encode/decode and
Standard binary encode/decode, three intervals each, with
`clamp(65536/(N+1),2,1000)` repetitions. Full setup GC precedes 20 warm calls
(two at N>=4096); record allocations, bytes, elapsed time, GC and checksums.
Provenance IO occurs after timing. All eight final processes / 1728 intervals
complete with correct results and matching graphs. Tiny intervals are descriptive,
not a renewed nanosecond equivalence gate.

Default-int single-role owners add 103 JSON / 50 binary bytes independent of
count, matching the Phase 1 descriptor cost. JSON encode/decode add 96–104/80
allocated bytes (payload alignment varies); binary adds 48/80. BiDictionary's
three roles add 245/138 encoded bytes, 240–248/136 encode allocations, and 88/88
decode allocations for an empty owner. At 65536 entries its fresh reverse-table
construction saves 2,621,376 allocated bytes per decode in both transports
against the valid default-int legacy baseline. These are boundary costs;
ordinary lookup/mutation has no registry or transport work. Timings are mixed
at larger counts and do not establish a universal throughput claim.

Final core measurement hashes: Standard
`E571396299470DB9A39817BC85A15FD06CEBE8E8E3C0D06A770F81BF04133286`, Lean
`47C8227CAA468FDD0BFCD79B7F1F1B72557136002CB7C8AA64921F2A65C7210E`;
baselines `319BD4D15FAD879D836F38AAAD8721820738909FDC7EE162914CD7BB20E95C1B`
and `84F570D6986947319B3433CEEF79444FDF86AB2CAD7C47372481C22503814C4F`.
An earlier descriptor-array snapshot had one native measurement-process exit;
the failed run and diagnostic timings are excluded. It was not reproduced in
bounded identical-core replays or the final matrix. Its cause remains
unattributed in [SC-Benchmark-006](../benchmark-signal-hardening-backlog.md#sc-benchmark-006---unattributed-native-exit-during-persistence-measurement);
the allocation optimization is not a claimed native-crash fix.

Scoped error-level C# formatting checks pass with explicit Release/net10.0 and
published-dependency selection. The final import-order/unused-import cleanup
changes no runtime logic; builds and both test projects pass again afterward.
Reviewed core DLL hashes after that cleanup are Standard
`BAE679CB7276D410900758CF4362119D8C9376685231BDAD0B3A1B1697445AFD` and Lean
`0802D3F984DA94BCA55A8945E824FFCD062D8736E832E5E05172C97DDA8F8702`.
Package/DLL/XML matches were checked again on those reviewed outputs; the
measurement hashes above deliberately identify the earlier measured binaries.

Independent reviews cover the shared transport/owner logic and registry from
different authors; the confirmed callback-exception contract gap was fixed and
regressed before final validation. Net runtime growth is 933 lines (672 nonblank,
noncomment), ten new files and one removed file: four owner reconstruction
partials, four required binary partials, one shared JSON adapter and the generic
registration replacing its ordering-only predecessor. Every helper has current
production callers; no maintained probe framework or speculative later-family
abstraction is added.
At the Phase 2 checkpoint, SC-Issue-021 was still open for Phase 3 and
SC-Benchmark-005 was closed under its existing user disposition. That checkpoint
did not claim a release or full-feature completion.


## Phase 3 execution record — 2026-11-20

Initial checkpoint, superseded by the [Phase 3 closeout](#phase-3-closeout--2026-11-20).

**Checkpoint disposition:** Local verification and the Phase 1–2 hardening review
were complete. The feature was kept open because actual IL2CPP players did not
build without native prerequisites, SC-Benchmark-006 was unattributed, and hosted
CI/release promotion had not been executed. This initial checkpoint claimed no
release, universal AOT support or universal timing equivalence. The Phase 3
closeout below supersedes this disposition.

### Optimization and independent review

Start from clean revision `b6c1953d2a81570bae8f97ab9c53a64e0edb5d0a` with
published dependencies. Independent reviews covered typed registration and weak
recapture, callback/configuration ownership, strict bounded metadata, all five
owner transports, duplicate/null/collision behavior, private publication and
post-load mutation. No new confirmed implementation defect was found. The
rendered review found one incorrect private-field link in `HashSet.SetComparer`;
its summary now describes stored entries directly.

BiDictionary now constructs its reverse table directly from the original state
snapshot, eliminating the projected `KeyValuePair<T2,T1>[]`. Dictionary retains
one population loop, with direct/reversed spans and pair selection before any
comparer callback. No new fields, public APIs, per-item delegates, normal
lookup/mutation work, raw-state changes or fallback algorithms are introduced.
The new behavior test covers empty/257-entry heterogeneous custom rules,
reverse aliases/uniqueness, removal, growth across another capacity boundary,
post-load recapture and unchanged source owners in JSON and standard MemoryPack.
This is an allocation optimization, so there is no claimed failing correctness
regression before the change.

A first shared-loop version saved the array but cost about 6.4% on large plain
Dictionary binary restore in four balanced processes per version. Keep that
adverse result. Selecting a pair once was insufficient. Final construction
specializes the shared loop inside the two persistence constructors, with the
constructors retained as call boundaries. JIT inspection confirms removal of
the direct loop's direction work without copying population into every
serializer. In the observed `int/int` specialization, the direct/reverse bodies
are 941/1028 native bytes; the sampled JSON/binary restoration call graph grows
about 940 native bytes overall. An intermediate unbounded-inline variant
expanded serializer bodies and was rejected. These are compiler observations
for this profile, not requirements on another JIT or Mono.

Use the Phase 2 default-int four-owner protocol, keys `0..N-1`, values `3*k+1`,
`N=0,1,8,128,4096,65536`, same optimized caller and matching dependency graph
with only the core DLL exchanged. Windows x64 build 26300, i7-9700K, SDK
10.0.302/runtime 10.0.10, CPU2, normal priority, tiering disabled, no competing
build/test/editor/profiler. Final ABBA per variant completes eight processes /
1728 checked intervals. The removed allocation is exactly `24+8*N` bytes for
this `int/int` pair on this host: 24/32/88/1048/32792/524312 bytes at the six
counts, for either decode transport. Wire lengths, encoder allocation and
ordinary non-bidirectional allocation remain unchanged. Tiny boundary timings
are mixed: standard BiDictionary binary medians are 279.3→283.75 ns empty,
290.65→302.65 ns at one item, and 371.2→376.9 ns at eight. These short intervals
do not establish a fixed latency penalty or zero overhead.

A separate binary-only ABBAABBA comparison at 65536 items uses eight warmup
calls, five samples of 32 decodes per process and four complete processes per
version, with full mapping checks outside timing. All 80 rows complete:

| Owner | Baseline process-median ns (range) | Final process-median ns (range) | Allocated bytes, before → after |
| --- | ---: | ---: | ---: |
| Dictionary | 555955 (549916–568053) | 542983 (540303–547869) | 2621664 → 2621664 |
| BiDictionary | 925641 (924459–934259) | 868688 (854906–886725) | 5243432 → 4719120 |

Measured core SHA256s are standard
`DCEFAE713786E479C0A4EB9D6358DABF76CE82F3BC0906CF44F2170549D0699A`
and Lean `C5EBC940F23FFD9396B941199CA69A15DE70070BC95DC9B3F867030B1B913DF6`;
baselines are `BAE679CB7276D410900758CF4362119D8C9376685231BDAD0B3A1B1697445AFD`
and `0802D3F984DA94BCA55A8945E824FFCD062D8736E832E5E05172C97DDA8F8702`.
Later line-ending/XML cleanup is followed by fresh full builds/tests; metadata
inspection verifies identical executable IL, signatures and method attributes
for the measured/final implementations (1535 standard / 1412 Lean bodies on
net10.0, and 1607/1418 in the actual netstandard Mono plugin graphs).
No maintained benchmark infrastructure or dependency change is added. Runtime
algorithm growth is 17 lines across two existing files; the HashSet XML correction
removes one documentation line, tests add 52 lines, and no maintained file is new.
Revisit specialization if a supported compiler loses the measured benefit or
code size/representative restore cost becomes material.

### Compatibility and determinism

Four independently selected complete graphs combine Standard/Lean and actual
`net10.0`/`netstandard2.1` core assets. Standard runs use the matching net8 or
netstandard MemoryPack asset respectively; the isolated execution host is
CoreCLR 10.0.10, not a claim that netstandard is a runtime. JSON uses the host's
System.Text.Json 10.0.10. Each writer captures five families × null/empty/one/three
items × direct/generated-nested shapes: 80 records standard, 40 Lean. Custom
ordering includes ties; typed modulo equality exercises both BiDictionary roles.
Readers check rules, full contents, lookup/add/remove continuation and observable
notifications. All 16 Windows reader/writer combinations pass.

Ubuntu 24.04.1 x64 under WSL runs an isolated portable, SHA512-verified official
.NET 10.0.10 runtime; there is no global install. All 16 Windows→Linux and
16 Linux→Windows reader combinations pass. Two fresh captures per graph per OS
agree byte-for-byte for this configured integer fixture, and canonical semantic
traces agree across all 48 combinations. This does not promise hash iteration
or universal hash-byte determinism. Built-in/default/seed/full-entropy,
noncomparable, callback and invalid-input coverage remains in maintained tests
and the Unity smoke controls.

Independent historical readers/writers use `bf69e21` for SortedList and
`4fedfa1` for hash families. Their own 16/64-record controls pass; historical
readers reject all current non-null direct/nested envelopes, and all four current
graphs reject their old non-null records. Null stays null. Final captures match
the initial independent-reader inputs. Raw DTO/comparerless wire contracts
remain unchanged; the Phase 2 exact-byte controls remain applicable.

Actual Unity 6000.3.9f1 Mono passes with matching netstandard plugins for both
variants. The subsequent XML-only correction leaves all executable IL identical;
final builds, tests, packages and references are checked again. With JSON reflection disabled before use, generated contexts and
explicit closed creators exercise all five owners, custom ordering/equality
factories, scalar/array/list/nested shapes, recapture and continuation. Standard
also executes explicitly registered MemoryPack owners, generated nesting,
truncation rejection and unchanged existing reference on failure. These composed
runtime checks supplement the earlier 29/27 EditMode suite results.

Both actual Windows64 IL2CPP Release/High-stripping build attempts fail before
producing a player. Unity reports no compatible installation with C++ tools and
no Windows SDK 10.0.19041.0 or newer; installed Visual Studio 2026 instances lack
MSVC. The editor's IL2CPP modules alone are insufficient. Install the native
prerequisites, then build and execute both stripped players to close this gate.
Native Unity field/JsonUtility formats remain explicitly separate adapter scope.
The Unity package checkout has no tracked edits.

### Build, coverage, packages and reference

Package mode (`UseLocalLsfStack=false`) is used throughout. The existing solution
build/test commands pass Release, ReleaseLean and Debug with zero build warnings
or errors. Final core test totals are 1791/1688/1789 respectively, companion 43
each; no skips. Final error-level format verification and `git diff --check`
pass. Full reachable coverage is preserved without new exclusions:

| Configuration | Core lines | Core branches | Core executable methods | Companion lines / branches / methods |
| --- | ---: | ---: | ---: | ---: |
| Release | 6081/6083 | 2886/2888 | 1261/1261 | 121/121; 62/62; 35/35 |
| ReleaseLean | 5887/5889 | 2810/2812 | 1220/1220 | 121/121; 62/62; 35/35 |
| Debug | 8027/8029 | 2894/2896 | 1261/1261 | 155/155; 62/62; 35/35 |

The only raw gaps are the two proven unreachable fresh-table exhaustion throws,
now Dictionary.Persistence line 68 and HashSet.Persistence line 38. The
[full coverage correction](#full-coverage-correction--2026-11-21)
supersedes their retention and preserves the complete capacity/probe proof.

All four package IDs contain byte-equal built DLL/XML pairs for both TFMs:
`SwiftCollections.dll/.xml` and `SwiftCollections.FixedMathSharp.dll/.xml`,
Release/ReleaseLean × net10.0/netstandard2.1, eight assembly/XML pairs in total.
Standard/Lean companion dependencies select their matching core and FixedMathSharp
families; Lean has no MemoryPack runtime dependency (its Chronicler shim remains).
No package, target or CI selection/publication policy changes.

The complete authored audit finds 946 core members standard, 931 Lean and 30
companion members per target, with zero missing applicable tags or reconciliation
gaps. Compiled core inventories are 1392/1311 IDs: 218 accessors, 126 authored
explicit-interface implementations and 36 synthesized members are accounted
separately; standard adds 66 generated serializer IDs (explicit-interface forms
on net10.0, public generated forms on netstandard2.1). Fifteen authored MemoryPack
members are excluded in Lean. Companion has 36 IDs: 30 authored, five accessors
and one synthesized. Operators, indexers, nested types and partials are reconciled.
Adjacent XML has 1179/1164 core entries and 30 companion entries per target;
internal comments are not miscounted as public API. The site selects standard
net10.0, so the other assets are checked through emitted XML/metadata separately.
Fresh DocFX generation passes with zero warnings; all 104 HTML pages and 1626
local paths/fragments resolve. Ninety-four assertions inspect representative
rendered owner, registry, creator, property, exception and inherited contracts. External Chronicler `IStateBacked` and
MemoryPack interface/reader/writer/serialization-exception labels have no xref maps
and remain plain text; local canonical-interface links resolve. Inherited inventory
is nine CloneTo members plus BoundVolume.Union/BoundsEquals, each in four target
combinations. Contributor guidance now reflects both JSON targets and the
separate Mono/IL2CPP gates.

### Failures, limits and next action

Two larger decode-batch processes fail on JSON in the committed baseline before
the candidate executes: one native access violation and one wrapped reader
NullReferenceException. The frozen caller's Main line 51 is BiDictionary, after
Dictionary JSON/binary work; the earlier dictionary-before-binary interpretation
is corrected by the full capture in the closeout. Their observed nested reader path, revisions, inputs,
controls and exclusions are preserved in
[SC-Benchmark-006](../benchmark-signal-hardening-backlog.md#sc-benchmark-006---unattributed-native-exit-during-persistence-measurement).
A debugger replay, plain-BCL/raw-state/owner controls and two additional pure-BCL
nested-reader controls (200 decodes each) pass without proving a fix or ownership.
An interrupted disposable host copy was rebuilt and verified before acceptance;
its incomplete launch and diagnostic runs are excluded. No speculative runtime
workaround or maintained regression for an unconfirmed cause is added.

No confirmed defect or test failure was attributable to the selected optimization;
the baseline reader failures were unattributed. Actual IL2CPP/stripping, native
reliability disposition, hosted CI and authorized release promotion were pending
at this checkpoint. SC-Issue-021 and the required completion checkboxes were kept
open for evidence or explicit disposition, subsequently recorded in the Phase 3
closeout below. The accepted SC-Benchmark-005 timing decision and trusted-state
SC-Issue-010 contract were retained without a compatibility reader. Further
timing investigation required a representative attributable regression rather
than another small context-sensitive microbenchmark alone.


## Phase 3 closeout — 2026-11-20

**Disposition:** Implementation acceptance and SC-Issue-021 are complete. The
user authorized pushing the five existing commits through
`cab616d0b741afae56ed207261683881254fcc23` after local checks, and delegated
judgment on the remaining reliability gate. SC-Benchmark-006 was initially kept
open as a separate release-reliability follow-up: no attributable collection defect was
established, no speculative workaround is justified, and passing current
controls do not prove the intermittent baseline crash fixed. Review that risk
before release promotion. Reopen implementation acceptance for a confirmed
attributable defect or failed supported consumer contract.

**Subsequent SC-Benchmark-006 disposition — 2026-11-20:** The requested bounded
source-focused investigation is complete. Isolated BiDictionary JSON reproduces
the frozen baseline's native CLR failure without preceding binary IO. Both
current Standard and both current Lean processes pass 201 decodes each, with
five validated samples per process. Source review finds no attributable memory
or lifetime defect; no production change is justified. Close the signal by a
documented no-source-change decision, with its historical evidence and reopening
criteria retained in the [benchmark backlog](../benchmark-signal-hardening-backlog.md#sc-benchmark-006---unattributed-native-exit-during-persistence-measurement).
This supersedes the initial open-follow-up status above. The cause remains
unresolved, no crash fix or source exoneration is claimed, and its uncertainty
remains visible in release review.

### Native toolchain and actual players

Modified the existing Visual Studio Build Tools 2026 installation
18.7.11925.98 with only the Desktop development with C++ workload's required
components plus explicit MSVC x64/x86 tools and Windows 11 SDK selections:

- `Microsoft.VisualStudio.Workload.VCTools`.
- `Microsoft.VisualStudio.Component.VC.Tools.x86.x64`.
- `Microsoft.VisualStudio.Component.Windows11SDK.26100`.

The selected SDK meets Unity 6000.3's Windows IL2CPP minimum of 10.0.19041.0.
Installed MSVC directory is 14.51.36231; compiler file version 19.51.36248.0.
The SDK library version is 10.0.26100.0. Installer completion and instance
inspection show success, complete installation and no required reboot. The
existing IDE, .NET SDK selection, CI policy and profiler/security permissions
are unchanged.

Fresh netstandard2.1 helper/dependency graphs build without warnings in Release
and ReleaseLean. Actual Unity 6000.3.9f1 Editor Mono and Windows64 IL2CPP players
pass both graphs. The IL2CPP builder reads back and asserts backend IL2CPP and High managed
stripping; its logged settings confirm Release native compilation. Player startup asserts
`ENABLE_IL2CPP` and `JsonSerializer.IsReflectionEnabledByDefault == false`
before calling serialization; each process exits zero with a passing result.
Native code and IL2CPP metadata are emitted. No blanket linker-preservation file
or runtime workaround is supplied.

The expanded representative slice covers all five owners with generated JSON
metadata/closed creators, custom ordering/equality factories, scalar/array/list
and generated nested shapes, weak-registry recapture, continuation and failure
publication. It also covers default-int ordering/equality for all five, nullable-int
nulls/duplicates, enum ordering/duplicates and ordinal string ordering. Sorted
custom JSON recapture checks zero ordering calls during restore. Standard also
uses explicit closed MemoryPack registration and exercises the same additional
rules, binary nesting, truncation and ref-existing publication failure. Lean
executes JSON with its matching shim graph. Both variants preserve source owners.
The earlier plugin/runtime tests and full inventory remain applicable.

### Local and hosted checks

On the selected committed implementation, fresh package-mode solution builds
and tests pass Release, ReleaseLean and Debug: core 1791/1688/1789 and companion
43 in each, no skips and zero build warnings/errors. An initial Debug
`--no-restore` invocation reused pre-existing source-mode assets and reported
236 missing-dependency compiler errors. A fresh explicit package-mode Debug
restore corrected the graph; build/tests then pass. This was an intermediate
cache/mode mismatch, not a source fix. Contributor guidance now calls for restore
after a dependency-mode change. No runtime or maintained test source changed in
this closeout; the prior coverage, XML/member inventory, package and reference
results remain valid for the same implementation.

The authorized push advanced develop through `cab616d`. Hosted
[build-and-test run 44807840605](https://github.com/mrdav30/SwiftCollections/actions/runs/44807840605)
passes all four jobs on that exact commit: Ubuntu/Windows × Release/ReleaseLean.
Each standard job executes 1791 core plus 43 companion tests; each Lean job
executes 1688 plus 43. Logs confirm actual test totals, no failures and zero build
warnings/errors. Existing develop policy selects immutable Chronicler
`7b90d5923d4bb6e0e42c033ec697b9678d6b8ece` and FixedMathSharp
`3da3a8ad9964d3e0413080737fec12ba107b48e4` source checkouts, serial source builds
and global `DisableTransitiveProjectReferences=true`. That policy is preserved.
Released-provider package validation is the local graph; no new upstream API
or dependency revision is introduced. Tagging, publishing and other OS/CPU/Unity
profiles remain separately scoped release work.

### Reliability correction and remaining risk

The calibrated collector observes an actual native null-field read, captures
full memory, then lets the CLR catch NRE and complete. On the exact frozen
65536-entry caller/runtime 10.0.10, two baseline processes complete 20 rows each;
a third supplies a full first-chance AV snapshot. Replay stops on that useful
capture. Two selected-current replays complete 20 rows each with no AV or
unhandled CLR event. Thirty million framework-only 112-byte managed-reference
copies also pass; none loads SwiftCollections, Chronicler or MemoryPack.

The full snapshot identifies BiDictionary JSON sample three after Dictionary
JSON **and binary** operations, correcting the earlier dictionary-before-binary
interpretation. Faulting CLR card marking targets stack memory below heap bounds;
the captured flag/reference state is inconsistent with the expected copy path.
This does not distinguish earlier corruption, context/control-flow trouble or a
runtime defect, and preceding MemoryPack cannot be excluded. Source/host or a
common cause for every incident is not established. The exact inputs, hashes,
calibration, native facts, limits and next isolated control are retained in
[SC-Benchmark-006](../benchmark-signal-hardening-backlog.md#sc-benchmark-006---unattributed-native-exit-during-persistence-measurement).
No maintained reproducer or production change for an unconfirmed cause is added.

All required implementation gates are accounted for by results or the delegated
reliability disposition. Preserve SC-Issue-010's trusted-state history and
SC-Benchmark-005's accepted timing decision. No NuGet package or documentation
site publication was performed. The completed plan is archived by repository
convention; evergreen instructions stay in the contributor and serialization guides.

## Full coverage correction — 2026-11-21

The earlier Phase 2/3 decision to retain two uncovered probe-exhaustion guards
did not meet the repository's 100% coverage requirement. That decision is
superseded. Remove the two impossible throws and their limits from fresh
Dictionary and HashSet restoration. Preserve capacity rejection, null and
duplicate handling, comparer resolution, table population and publication.
The complexity register contains only justified methods above its complexity
threshold; it provides no coverage waiver. No coverage exclusion, fabricated
local-table corruption or runtime workaround is added.

### Probe and arithmetic proof

Both callers use a minimum capacity of eight and load percentages 82 or 85.
Input count `c` is a non-negative array/span length. For `c < 8`, minimum
capacity already exceeds count. For `c >= 8`, `floor(100*c/p) >= c+1` for
either load percentage `p`. Upward power-of-two rounding preserves a spare
slot. HashSet null filtering can only reduce occupancy. At most one entry is
inserted per input; duplicates reject rather than consume another slot.

The sizing multiplication uses Int64 and cannot overflow for an Int32 count.
Requested sizes at or above `2^30` reject before allocation. Smaller requests
can round **up to** `N = 2^30`; that is the largest possible table, not `2^29`.
The first rejected counts are 880468296 for 82% and 912680551 for 85%; each
preceding count returns `2^30` without allocating a table in the boundary test.

Let `P(k) = sum(j*j, j=1..k)`, with the initial probe at `P(0)=0` plus the
masked starting hash. For power-of-two `N >= 2`,
`P(k+N)-P(k) = N/2 (mod N)`. By induction, the first `N` probes cover every
residue modulo `N/2`; the next `N` toggle the high bit, covering every slot
modulo `N`. Thus the first `2N` probes visit every slot.

The last probe in that bound repeats the first:
`P(2N-1) = N*(2N-1)*(4N-1)/3 = 0 (mod N)`. The odd product is divisible by
three, so all slots are already covered by `P(2N-2)`. A free slot therefore
appears with `step <= 2N-1 <= Int32.MaxValue`, even at `N=2^30`.
`step++` cannot overflow before success. The square and index addition remain
explicitly unchecked; Int32 truncation followed by `& (N-1)` equals mathematical
modulo `N`, since `N` divides `2^32`.

The fresh entry arrays remain local until population completes. Comparer
callbacks receive elements, not those arrays; changing input values cannot
change the captured length or insert more than one entry per iteration.
The same argument covers reversed BiDictionary restoration and inherited
ObservableDictionary restoration. Unsafe corruption of local implementation
state is not a supported input or a reason to retain unreachable branches.
Revisit this proof if sizing, probing, arithmetic or publication ownership changes.

Independent review and disposable checks cover exhaustive probe recurrence
for `N=2^0..2^18`, masked arithmetic samples through `2^30`, and sizing
boundaries. No giant tables are allocated. Maintained tests exercise actual
JSON/MemoryPack restoration at count/capacity pairs 7/8, 13/16, 26/32 and
52/64, with wrapped forward/reverse collisions, complete mappings, removal,
addition and recapture. Capacity tests check the exact accepted/rejected limits.
These preserve existing valid behavior; removing dead branches has no failing
behavioral regression to manufacture.

### Coverage and CI acceptance

Package-mode solution builds pass Release, ReleaseLean and Debug with zero
warnings/errors. Core tests pass 1818/1714/1816 respectively; companion tests
pass 43 each, with no skips. Initial new-test compilation/analyzer issues were
corrected before these final checks, without adding an API.

| Configuration | Core lines | Core branches | Core executable methods | Companion lines / branches / executable methods |
| --- | ---: | ---: | ---: | ---: |
| Release | 6080/6080 | 2884/2884 | 1261/1261 | 121/121; 62/62; 35/35 |
| ReleaseLean | 5886/5886 | 2808/2808 | 1220/1220 | 121/121; 62/62; 35/35 |
| Debug | 8026/8026 | 2892/2892 | 1261/1261 | 155/155; 62/62; 35/35 |

Both runtime assemblies build at netstandard2.1 and net10.0 in standard/Lean;
all four package IDs contain the matching eight DLL/XML pairs. Public and
serialization formats are unchanged.

The old strict gate existed only in the successful-main-push Pages workflow,
so green ordinary build/test jobs did not establish full coverage. Both that
workflow and every existing Windows/Linux Release/ReleaseLean build/test lane
now call [one fail-closed assertion](../../../.assets/scripts/assert-full-coverage.ps1).
It inspects all fresh Coverlet reports, requires both exact runtime assembly
names and positive line/branch/executable-method data, and rejects any gap.
Executable methods have source sequence points; methods without sequence
points are not executable-method coverage entries. Native test failures stop
the Windows/Linux test step before another test call can mask its exit code.
Pages triggers/deployment, dependency selection, source pins and the four-lane
matrix remain intact.

The exact script passes all three configurations and rejects the historical
6084/6086-line, 2886/2888-branch report. Disposable controls also accept a full
two-assembly report and reject line-only, branch-only and method-only gaps,
missing counts, zero counts, missing methods/assemblies, unrelated assembly
reports, empty results, malformed XML and a missing results directory. These
are script/CI checks, not maintained tests of test infrastructure. No hosted
workflow or branch-protection change was included. The correction preserved the
separate SC-Issue-012 work present at that checkpoint.

## Historical Feature Tracking Closeout

The feature was originally tracked as SC-Issue-021. Its former tracker record
is retained below as historical evidence; current feature status is indexed
through the feature work overview. These are the original execution results,
not fresh validation. The [full coverage correction](#full-coverage-correction--2026-11-21)
supersedes the earlier reachable-coverage wording and guard gaps.

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
  [SC-Benchmark-005](../benchmark-signal-hardening-backlog.md#sc-benchmark-005---comparer-persistence-hot-timing-acceptance)
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
  [2026-11-20 closeout](#phase-1-closeout--2026-11-20).
  This documentation-only decision reuses the recorded validation of
  `cbd9a734082d7a002699766937b79dc547a8cebb`; it adds no runtime changes or new
  validation runs. It preserved the benchmark implementation at that checkpoint.
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
  [Phase 2 record](#phase-2-execution-record--2026-11-20)
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
  See the [Phase 3 record](#phase-3-execution-record--2026-11-20)
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
  cause or ownership. [SC-Benchmark-006](../benchmark-signal-hardening-backlog.md#sc-benchmark-006---unattributed-native-exit-during-persistence-measurement)
  retains the evidence and subsequent no-source-change closure. Its binary-free
  BiDictionary control reproduces the frozen baseline's native CLR failure;
  both current Standard and both current Lean processes pass. Source review
  finds no attributable defect. No crash fix or source exoneration is claimed.
- **Next action:** No implementation or investigation work remains queued.
  Keep SC-Benchmark-006's historical uncertainty visible in release review and
  reopen for a current-code recurrence, concrete source defect or changed risk
  evidence under its recorded criteria. Implementation acceptance is complete;
  no package release or universal runtime/architecture/AOT guarantee is claimed.
- **Plan / acceptance:** [Comparer persistence plan](#outcome)
  owns the inventory, decisions, phases, validation and performance gates.
  The former plan-only fixture directory is removed; maintained tests consume
  the required legacy sorted-list literals directly.
