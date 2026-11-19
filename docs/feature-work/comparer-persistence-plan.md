# Comparer Persistence Plan

- **Status:** Planned; implementation has not started.
- **Created:** 2026-11-19.
- **Owner:** SwiftCollections; coordinate with Chronicler only where its shared
  serialization boundary needs an extension.
- **Tracking:** [SC-Issue-021](issue-tracker.md#sc-issue-021).

## Outcome

Persist a collection's comparison rules with its contents so JSON and standard
MemoryPack restoration establish those rules before inserting or searching any
items. Unsupported comparers must produce an actionable failure instead of
silently becoming defaults. JSON and the shared persistence contract must work
in both standard and Lean packages, at `netstandard2.1` and `net10.0`.

Keep ordinary collection operations direct: no registry lookup, descriptor
allocation, wrapper comparer, new lock, or serialization branch in lookup,
comparison, insertion, removal or iteration. Resolve comparison metadata at
serialization boundaries. Keep the existing trusted raw-state path and its
copying costs intact.

This plan does not implement the feature. All future acceptance checks below
are pending, not results inherited from the documentation pass.

## Source baseline and scope

Inspected SwiftCollections revision
`31cd3eec80dff86b95f8cab621cc7ca281b4e7cc` plus the staged SC-Issue-010 contract
documentation/tests, and clean sibling Chronicler revision
`3c1cfb536fb009e43d4667158607611dd94ba854`. Before implementation, record the
actual starting revisions and any local patches; these observations are not
release or upstream API availability claims.

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

## Proposed contract

### Separate trusted raw state from persisted comparison metadata

Prefer a versioned persistence envelope containing the current raw item state
and typed comparer descriptors. Do not change shared raw DTOs or raw-state
constructors merely to carry serialization metadata. The exact public type
names and adapter signatures are an implementation design gate, not new APIs
promised by this document.

The target experience is ordinary collection JSON/MemoryPack persistence that
preserves comparers, including nested fields and collections. A standalone
opt-in wrapper is useful for compatibility/prototyping, but is not sufficient
to close this feature while ordinary serialization silently loses comparers.
Choose and record how the adapters become the normal serialization path before
expanding beyond the first family. Preserve comparerless formats.

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
| Swift randomized collision fallback | Preserve or explicitly reject effective configuration; never silently pretend it is the original default. Resolve the seed/fallback policy in the decision log before implementation expands. |

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

Resolve whether exact effective seed reconstruction is supported without fresh
entropy. Restoration must not sample time, process identity or randomness to
replace a persisted supported comparer. Keeping a seed still does not freeze
future fallback transitions or bucket order. Record which deterministic replay
scenarios are supported, test collision-heavy continuation, and explicitly
exclude unsupported randomization/collation scenarios. Do not disable existing
collision hardening or claim cross-platform iteration guarantees incidentally.
Reinsertion can itself reach a fallback threshold: the supported persistence
path must not silently generate new entropy during construction either. Prove
how it preserves the selected effective policy or rejects that scenario.

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

Produce an explicit old/new reader/writer table for each serializer before
choosing the emitted format. Existing JSON is an outer `State` object; existing
MemoryPack payloads have no comparer metadata. An old payload cannot reveal
whether its source used a custom comparer. A new reader must not claim to
recover that missing information.

Recommended migration: new comparer-aware writers emit an identifiable version;
strict reads require descriptors. Provide an explicitly selected legacy path
with caller-supplied comparison rules, or an explicitly acknowledged default
policy. Do not guess from array order. Existing raw state APIs remain available.
Default legacy acceptance versus a breaking strict default remains a recorded
release decision. Old readers must reject new formats rather than silently
ignore comparison metadata; verify this with actual fixtures for nested values.

Keep old JSON text and MemoryPack bytes as small tracked compatibility fixtures
with producing revision/configuration and expected outcomes. Test direct and
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

- [ ] Capture current ordinary JSON/MemoryPack behavior and old fixtures before
  changing schemas. Include the SC-Issue-010 descending example and the custom
  equality cases where default restore collapses entries.
- [ ] Settle the decision log below, the built-in identifier table, strict/legacy
  read rules, public entry points and ordinary nested serialization integration.
- [ ] Establish baselines for raw state, ordinary serialization, reconstruction
  and hot collection operations with existing package dependencies.
- [ ] Verify the `netstandard2.1` MemoryPack/Unity adapter approach before using
  it as the common design. Record the first supported Unity version and profile.

### 1. Prove one complete caller-facing family

- [ ] Implement SortedList first: built-in default plus a registered, configured
  deterministic custom ordering, including a type with no default comparison.
- [ ] Cover ordinary direct and nested JSON in standard/Lean and MemoryPack in
  standard, on both TFMs' compatible dependency graphs. Confirm comparer
  resolution precedes use and custom data never takes a default-comparer pass.
- [ ] Verify MemoryPack top-level, generated direct methods, nested members,
  packable arrays/lists and supported base/interface paths all use the same
  envelope or reject unsupported dispatch explicitly. Do not accept only a
  global formatter registration control while generated paths bypass it.
- [ ] Demonstrate valid lookup/add/remove continuation, unknown-codec failure,
  legacy rejection/migration, private construction and unchanged source storage.
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
| Preserve raw DTOs and trusted-state APIs | Planning direction: keep their schema/behavior; reconsider only if the first serializer slice demonstrates an unavoidable contract conflict and a migration is agreed. |
| Ordinary serialization versus opt-in | Target: normal direct/nested JSON and standard MemoryPack preserve supported comparers or fail. Exact adapter/default rollout remains pending Phase 0; an opt-in prototype alone cannot satisfy completion. |
| Descriptor ownership | SwiftCollections owns collection roles and comparer codecs; reuse Chronicler's general boundary where compatible. A Chronicler extension needs a real shared consumer and provider-first validation. No new package is presumed. |
| Custom activation | Explicit typed registrations and stable semantic versions. No arbitrary CLR type activation, automatic delegate capture, or global order-dependent IDs. Reopen only with a concrete unsupported caller requirement. |
| Legacy input | Recommended strict new persistence with explicit legacy migration. Default legacy acceptance/release compatibility is pending; no automatic inference of lost comparers. |
| Hash fallback and culture | Pending Phase 0: settle effective seed reconstruction, supported deterministic continuation and explicit rejection limits. Do not silently normalize randomized comparers or promise culture portability. |
| Binary format and Unity | Pending first-slice proof: choose versioned layout and generated/registered adapter supported by pinned MemoryPack and Unity. Member custom-formatters or modern static-abstract APIs alone are insufficient evidence. |
| Related defects | SC-Issue-013 base mutation/comparer replacement and SC-Issue-014 malformed PackedSet state stay separately scoped. If they block valid feature inputs, resolve their owning issue or record the concrete dependency before claiming this feature complete. |

## Planning verification and execution record

- Source inventory, project conditions, shared DTO identities and serializer
  boundaries inspected with independent subagents on 2026-11-19.
- Independent plan reviews completed; incorporated the distinct bidictionary
  reverse configuration and generated MemoryPack dispatch gates. Local relative
  links and whitespace checks pass. Existing staging is preserved; this plan
  and its tracker intake are unstaged.
- Runtime changes, new tests, schema changes, benchmarks, package publication
  and Unity validation have not been performed for this feature plan.
- Keep future concise results in this section: revision/dependency mode,
  framework/variant, command/workload, counts/units, observed failures, disposition
  and next gate. Supporting generated output must not be the only evidence.
