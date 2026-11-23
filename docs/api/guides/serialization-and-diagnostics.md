---
title: Serialization and diagnostics
---

# Serialization and diagnostics

## Serialization

The standard packages include MemoryPack support where the source type is marked
with `[MemoryPackable]`. Lean packages compile the same collection APIs without
the MemoryPack dependency by using compatibility shims and excluding
MemoryPack-specific source.

Package variants:

- `SwiftCollections`
- `SwiftCollections.Lean`
- `SwiftCollections.FixedMathSharp`
- `SwiftCollections.FixedMathSharp.Lean`

State-backed collection types expose explicit state structs such as
`SwiftArrayState<T>`, `SwiftSlotMapState<T>`,
`SwiftDictionaryState<TKey, TValue>`, `SwiftTicketMapState<T>`,
`SwiftDenseTicketMapState<T>`, `SwiftTicketStoreState<T>`, and
`SwiftSparseMapState<T>`.

State-backed owners implement `Chronicler.IStateBacked<TState>`. Most use
`Chronicler.Serialization.StateJsonConverterFactory` for JSON: it writes a
`State` property and invokes the owner's explicit state constructor when
restoring it. SortedList, Dictionary, HashSet, BiDictionary and
ObservableDictionary instead use the versioned comparer-aware contracts below.
State validation belongs to the owner; this is distinct from populating an
existing `IRecordable` runtime shell.

Readonly state structs are not deeply immutable. Their constructors retain
supplied arrays; some normalize null arrays to empty arrays, while default
struct values bypass those constructors. Collection `State` getters allocate
shallow snapshots, so their array storage is independent but referenced elements
remain shared. Validate externally supplied dimensions, IDs and free-slot
metadata against the concrete collection's restore contract.

Dimensional arrays validate restored shapes before copying data into
independently owned storage. Each dimension must be non-negative, and its exact
product must fit in `Int32` and equal the data length. Any zero axis gives an
empty shape, including when the other dimensions are large, and requires empty
data. Negative dimensions throw `ArgumentOutOfRangeException`, an overflowing
product throws `OverflowException`, and a length mismatch throws
`ArgumentException`. Null data retains the existing exception distinction: 2D
arrays throw `ArgumentNullException`; 3D arrays throw `NullReferenceException`.

`Resize` rejects negative dimensions and overflowing products before changing
the array. Failed resize or validation/copying by the dimensional owner's state
setter preserves its shape, stored values and backing array. Ordinary 2D
dimensional constructors, including the bool and short specializations, retain
their negative-to-zero clamping behavior; 3D dimensional constructors reject
negative dimensions. These checks do not change the passive `Array2DState<T>`
and `Array3DState<T>` constructors, which retain supplied data without
validation or copying.

Chronicler's state factory discovers the declared state contract with reflection
and compiles a constructor delegate. A state value is an explicit schema, not an
arbitrary object-graph discovery mechanism. See the
[migration guide](https://github.com/mrdav30/SwiftCollections/blob/main/docs/MIGRATION.md)
when upgrading existing consumers.

### Round-trip collection state through JSON

This example works in standard and Lean builds:

```csharp
using System.Text.Json;
using SwiftCollections;

var source = new SwiftList<int>();
source.Add(42);
string json = JsonSerializer.Serialize(source);
SwiftList<int> restored = JsonSerializer.Deserialize<SwiftList<int>>(json)!;
// restored.Count is 1 and restored[0] is 42.
```

### Preserve dense ticket identity and reuse

`SwiftDenseTicketMap<T>` persists its logical state in standard and Lean JSON,
and in standard-build MemoryPack. It records live `DenseValues` and parallel
`DenseSlots`, `SlotGenerations` for every issued slot (zero means retired), and
`FreeSlots` in bottom-to-top reuse order. Capacities, borrowed spans and reverse
lookup positions are excluded. Restoring preserves dense order, surviving
tickets and subsequent issuance, including retirement and Clear history.

```csharp
var source = new SwiftDenseTicketMap<int>();
SwiftTicket live = source.Add(10);
SwiftTicket removed = source.Add(20);
source.Remove(removed);

string json = JsonSerializer.Serialize(source);
var restored = JsonSerializer.Deserialize<SwiftDenseTicketMap<int>>(json)!;
int value = restored.GetRef(live); // 10
SwiftTicket replacement = restored.Add(30); // Index 1, Generation 2
bool staleFound = restored.TryGet(removed, out _); // false
```

The getter-only `State` returns independent shallow array snapshots; its DTO
retains supplied arrays without validating them. Synchronize mutation during
capture/serialization and do not mutate DTO arrays during reconstruction.
Reconstruct a new owner from state to validate lengths, ranges, uniqueness,
live/free separation and complete issued-slot ownership. Malformed decoded state
throws `ArgumentException` (parameter `state`); unsupported identity budgets
throw `InvalidOperationException`. JSON envelope/number/shape failures instead
throw `JsonException`. The JSON owner has exactly one case-sensitive outer
`State` property; nested fields use ordinary System.Text.Json options, including
unknown/duplicate-field handling.

Raw snapshots share referenced payload objects. Default serialization rebuilds
payload values without promising original object identity or alias preservation.
Rollback can revive numeric tickets, so discard commands/handles from abandoned
future timelines. Tickets carry no world or owner identity.

#### Closed dense-ticket JSON metadata

The default state converter factory uses reflection and constructor-delegate
compilation. For AOT hosts, register each closed converter before requesting
metadata and supply source-generated metadata without a reflection fallback:

```csharp
using Chronicler.Serialization;
using System.Text.Json.Serialization;

var options = new JsonSerializerOptions {
    TypeInfoResolver = DenseTicketJsonContext.Default
};
options.Converters.Add(
    new StateJsonConverter<SwiftDenseTicketMap<int>, SwiftDenseTicketMapState<int>>(
        static state => new SwiftDenseTicketMap<int>(state)));
options.Converters.Add(
    new StateJsonConverter<SwiftTicketStore<int>, SwiftTicketStoreState<int>>(
        static state => new SwiftTicketStore<int>(state)));
string denseJson = JsonSerializer.Serialize(source, options);

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SwiftDenseTicketMap<int>))]
[JsonSerializable(typeof(SwiftDenseTicketMapState<int>))]
[JsonSerializable(typeof(SwiftTicketStore<int>))]
[JsonSerializable(typeof(SwiftTicketStoreState<int>))]
internal partial class DenseTicketJsonContext : JsonSerializerContext { }
```

Include the actual payload types and nested owners used by the application. The
map accepts any T, but persistence requires payload support from the chosen
serializer. Validate with JSON reflection disabled before first use on the
actual host; generated metadata alone does not establish stripping/AOT
compatibility.

#### Dense-ticket MemoryPack behavior

Standard builds use the generated Object format: one owner State field and the
four state fields above. Explicit bootstrap for closed types uses
`MemoryPackFormatterProvider.Register<SwiftDenseTicketMap<int>>()` and the
corresponding state type on net10.0. The generated netstandard2.1 APIs instead
expose `SwiftDenseTicketMap<int>.RegisterFormatter()` and
`SwiftDenseTicketMapState<int>.RegisterFormatter()`. Root payload formatters as
required; Lean has no MemoryPack runtime or generated entry points.

The generated decoder accepts omitted fields; this is not a strict versioned
envelope. In particular, a zero-member owner header creates an empty owner when
the destination is null, but reconstructs the prior logical state when reading
into an existing destination. A null owner header sets the destination to null.
Normal writers emit the complete schema; future schema changes require an
explicit compatibility decision.

Successful owner reads create a replacement instance. For this readonly DTO,
generated reads use fresh arrays; truncation and structural rejection preserve
the previous owner reference, its arrays and ordinary payload objects. The raw
DTO is still passive: decode it first, then construct an owner to validate it.
Failures may advance the reader or leave partial writer output. Custom payload
serializer side effects are not rolled back. Use matching Standard/Lean
dependency graphs and verify closed types on the target AOT host.

### Persist externally issued ticket entries

`SwiftTicketStore<T>` saves entries attached to another owner's tickets. Its
state has three parallel arrays in exact dense order: `DenseValues`,
`DenseSlots` (external ticket indices), and `DenseGenerations`. Every UInt32
generation, including zero, is key data. It saves no issuer, liveness information,
spare capacity or generation history. Ordinary JSON uses the same strict
one-property `State` envelope as the dense issuing map:

```json
{"State":{"DenseValues":[101,202,303],"DenseSlots":[0,7,3],"DenseGenerations":[0,4294967295,9]}}
```

Raw state retains supplied arrays. The owner validates equal lengths, supported
index bounds and unique indices, then owns shallow copies and rebuilt lookup.
Duplicate indices fail even when their generations differ. Structural rejection
throws `ArgumentException` with parameter `state`; JSON shape and numeric
failures throw `JsonException`. Default/null arrays normalize to empty when
constructing an owner. Raw snapshots share referenced payload objects; ordinary
serialization rebuilds payload values without promising object identity or
alias preservation.

For closed JSON, register the store and state converter/metadata alongside the
issuer as shown above. Include actual payloads and nested store/aggregate types;
register closed converters before requesting metadata. Verify the closure with
JSON reflection disabled before first use. Supported payloads remain the chosen
serializer's responsibility.

Standard MemoryPack writes one owner member containing the three-member state,
followed by the arrays. Bootstrap each closed store and state with the same
net10.0 provider registration or netstandard2.1 generated RegisterFormatter
pattern described above. Lean retains JSON/raw state and has no MemoryPack
runtime. The generated binary reader's replacement and failure rules above
apply to the store too. An explicit empty or null *nested binary state* creates
an empty store, whereas JSON `"State": null` rejects. A zero-member binary
*owner* read into an existing destination instead reconstructs its prior logical
state in a new owner. These are distinct records; normal writers emit all fields.
Failed binary replacement preserves the previous owner, associations and
ordinary mutable payloads; reader position, writer output and custom payload
callback side effects are not transactional.

Capture issuer plus component stores at one caller-controlled structural
boundary. Restore every participant into locals, check each component ticket
with the restored issuer, then publish the candidate aggregate. Stores do not
coordinate capture, remove orphans, or validate issuer liveness themselves.
An IsValid cross-check catches dead generations but cannot establish a shared
world or snapshot boundary when numeric tickets alias. Keep that boundary
explicit and discard commands from abandoned future timelines. The
[collection guide](collections.md#who-issues-the-identity) explains identity
ownership and joining stores whose dense orders differ.

### Preserve sorted-list ordering during serialization

Normal JSON and standard-build MemoryPack serialization of `SwiftSortedList<T>`
records both its comparison rule and raw items in a version-one envelope.
Reading resolves the comparer before reading items, then copies trusted item
order into a new owner without comparisons or sorting. Equal items have no
stable tie-order guarantee. The raw `SwiftArrayState<T>` schema and explicit
state constructor remain unchanged.

Built-ins work without registration: eligible `Comparer<T>.Default` rules and
the string `StringComparer.Ordinal` and `StringComparer.OrdinalIgnoreCase`
instances. Culture-dependent default string ordering, polymorphic `object`
default ordering and known noncomparable defaults require an explicit codec,
including for empty lists. Application-defined comparison behavior remains the
application's responsibility; peers must use compatible comparison and Unicode
semantics.

For a custom rule, register its exact source instance and a codec: a stable
identifier, configuration encoder and reconstruction factory. This example
captures descending order as one configuration byte and recreates a capturing
lambda from that immutable value:

```csharp
using System;
using System.Collections.Generic;
using System.Text.Json;
using SwiftCollections;
using SwiftCollections.Serialization;

static IComparer<int> CreateOrdering(bool reverse)
    => Comparer<int>.Create((left, right) => reverse
        ? right.CompareTo(left) : left.CompareTo(right));

IComparer<int> descending = CreateOrdering(true);
SwiftComparerRegistry registry = new SwiftComparerRegistryBuilder()
    .RegisterOrdering<int>("game.int-order", 1, descending,
        _ => new byte[] { 1 },
        config =>
        {
            if (config.Length != 1 || config.Span[0] > 1)
                throw new ArgumentException("Invalid ordering configuration.");
            return (CreateOrdering(config.Span[0] == 1), config.Length);
        })
    .Build();

var source = new SwiftSortedList<int>(new[] { 1, 2, 3 }, descending);
var jsonOptions = new JsonSerializerOptions();
jsonOptions.Converters.Add(new SwiftCollectionJsonConverterFactory(registry));
string json = JsonSerializer.Serialize(source, jsonOptions);
var restored = JsonSerializer.Deserialize<SwiftSortedList<int>>(json, jsonOptions)!;
// restored contains 3, 2, 1; Contains and later mutations use descending order.
```

The registry copies captured configuration and requires the factory to consume
every byte. A factory-produced comparer receives a weak, registry-local binding
so it can be saved again with that configuration. Keep its behavior immutable;
changing captured state would make the saved description stale. A different
registry needs explicit registration or reconstruction. Another lambda does not
become registered merely because it shares the same runtime wrapper type.

JSON writes ordered, case-sensitive `FormatVersion`, `Comparers`, then `State`
properties. The one sorted-list descriptor has ordered `Role`, `Id`, `Version`,
then base64 `Config` properties, with ordering role 1. Identifiers contain 1–128
lowercase ASCII letters, digits, dots, underscores, hyphens or slashes; `lsf.`
is reserved for built-ins. Configurations contain at most 4,096 decoded bytes.
Malformed, unknown or unsupported metadata fails before an owner is published;
normal readers reject state-only legacy payloads. Null owners retain the normal
serializer null representation, while non-null empty owners still need a rule.

#### Closed JSON construction and generated metadata

The normal factory creates generic converters through reflection. For a closed
serialization contract, use the matching `SwiftCollectionJsonConverters`
creator: `ForSortedList<T>`, `ForDictionary<TKey, TValue>`, `ForHashSet<T>`,
`ForBiDictionary<TKey, TValue>` or `ForObservableDictionary<TKey, TValue>`.
Options converters take precedence over the owner's converter attribute,
including when ordinary generated container metadata visits nested lists. Supply
metadata for both the owner and its unchanged raw state, plus element and
containing types:

```csharp
using System.Text.Json.Serialization;

var options = new JsonSerializerOptions();
options.Converters.Add(SwiftCollectionJsonConverters.ForSortedList<int>(registry));
var context = new SortedSnapshotJsonContext(options);
string json = JsonSerializer.Serialize(source, context.SortedIntegers);
var restored = JsonSerializer.Deserialize(json, context.SortedIntegers)!;

[JsonSourceGenerationOptions(GenerationMode = JsonSourceGenerationMode.Metadata)]
[JsonSerializable(typeof(SwiftSortedList<int>), TypeInfoPropertyName = "SortedIntegers")]
[JsonSerializable(typeof(SwiftArrayState<int>))]
internal partial class SortedSnapshotJsonContext : JsonSerializerContext { }
```

This closes converter construction; the host must also preserve its concrete
factories and metadata when trimming or compiling ahead of time. The factory
supports exact closed types from the five comparer-aware families. Hash owners
also require `SwiftDictionaryState<TKey, TValue>` or `SwiftArrayState<T>`
metadata and their key/value types. Explicitly serializing a derived sorted list
as its base type records base items and ordering only; it restores a base owner
and omits derived fields. A bidictionary through the Dictionary base contract is
rejected because its reverse roles would be lost. Full derived and
interface-typed contracts need explicit host integration; do not assume they
select the same converter.

#### MemoryPack options and bootstrap

In standard builds, supply the same registry through MemoryPack's per-operation
service provider and register the concrete formatter during host bootstrap:

```csharp
using MemoryPack;

SwiftSortedList<int>.RegisterFormatter();
var memoryPackOptions = MemoryPackSerializerOptions.Default with
{
    ServiceProvider = registry
};
byte[] bytes = MemoryPackSerializer.Serialize(source, memoryPackOptions);
var restored = MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(
    bytes, memoryPackOptions)!;
```

A host with an existing service provider should compose a
`SwiftComparerRegistry` service into it, preserving its other services. A
missing service selects built-ins only; it does not recover unregistered custom
rules. Keep compatible element formatters and options, including string
encoding, on both sides. Each comparer-aware owner has a closed
`RegisterFormatter()` entry point. Explicit registration avoids relying on
reflective cold discovery. Modern and `netstandard2.1` MemoryPack binary APIs
require matching dependency graphs; do not swap only one generated DLL between
them. Validate the actual host's stripping and AOT profile. Lean retains the
registry and JSON APIs without these MemoryPack entry points or dependencies.

Unsupported capture throws `NotSupportedException`. Invalid comparison metadata
is reported as `JsonException` or `MemoryPackSerializationException` when
reading the corresponding format. A failed binary overwrite leaves the existing
owner reference unchanged. Factory or element callback side effects are not
rolled back, and a later serialization failure can leave partial writer output.

#### Version-one records only

Sorted-list owner persistence supports only the version-one envelope. Old
state-only JSON and MemoryPack owner records are rejected; there is no legacy
reader, fallback or migration helper. This is an intentional breaking format
change, so upgrade writers and readers together and use version-one records.

The unchanged raw `SwiftArrayState<T>` and explicit state constructor serve the
trusted-state contract below. They do not make old serialized owner records
compatible with the new format.

### Preserve hash equality during serialization

Normal JSON in standard/Lean and standard MemoryPack preserve the effective
equality/hash policy of `SwiftDictionary<TKey, TValue>`, `SwiftHashSet<T>`,
`SwiftBiDictionary<T1, T2>` and `SwiftObservableDictionary<TKey, TValue>`. They
use the same version-one envelope, registry configuration, metadata bounds, null
owner handling and strict legacy rejection described above.

Built-ins include runtime `EqualityComparer<T>.Default`, the two ordinal string
comparers, and known Swift string/object comparers with their signed Int32 seed
or full signed Int64 collision entropy. Known Swift object comparers can also
serve reference key/element types through contravariance. No new entropy is
sampled while restoring a recorded policy. Runtime defaults, object values and
ordinal-ignore-case Unicode behavior retain their own platform/runtime limits;
preserving a descriptor does not promise portable runtime hash codes.

```csharp
using System;
using System.Text.Json;
using SwiftCollections;

var source = new SwiftDictionary<string, int>(8, StringComparer.OrdinalIgnoreCase);
source.Add("Player", 7);
string json = JsonSerializer.Serialize(source);
var restored = JsonSerializer.Deserialize<SwiftDictionary<string, int>>(json)!;
bool found = restored.ContainsKey("player"); // true; equality is restored first.
```

Register custom equality/hash rules with
`SwiftComparerRegistryBuilder.RegisterEquality<T>(id, version, comparer, encode, decode)`.
It has the same exact-instance, bounded configuration, immutable factory-product
and weak recapture contracts as `RegisterOrdering<T>`. One typed equality codec
serves dictionary keys, set elements and reverse-value roles; ordering and
equality registrations remain separate even if one object implements both. Use
the same JSON option factory or MemoryPack registry service shown above.
Culture-sensitive and unregistered custom equality rules require an explicit
codec; empty owners do not bypass this requirement.

| Owner                            | Required roles                                                                                  |
| -------------------------------- | ----------------------------------------------------------------------------------------------- |
| Dictionary, ObservableDictionary | 2: effective forward-key equality/hash.                                                         |
| HashSet                          | 3: effective element equality/hash.                                                             |
| BiDictionary                     | 2: effective forward-key rule; 4: configured reverse-value rule; 5: effective reverse-map rule. |

All rules resolve before item population. Hash owners build unpublished fresh
tables using the recorded policies, without ordinary Add/SetComparer, collision
fallback or restoration notifications. Duplicate equal dictionary keys, set
elements or bidirectional reverse values fail instead of silently collapsing.
HashSet continues skipping null state items; its enumerable constructor still
rejects null items. Dictionary keys and bidictionary reverse values reject null.
An equality codec must provide a valid equality/hash contract; restoration does
not scan every pair to repair equal values with different hashes.

A bidictionary's configured and effective reverse rules must describe the same
policy or a recognized Swift collision-hardening transition. Contradictory
custom rules fail. Persist through the concrete bidictionary contract so all
three roles survive. Observable restoration creates a new owner with no event
subscribers and emits no restoration events; later ordinary mutations retain
their existing notification behavior.

Snapshots are shallow, and the caller must synchronize owner/comparer mutation
during capture. Fresh table capacity, hash slots and enumeration can differ from
the source because raw state omits holes and insertion history. Future normal
mutations retain ordinary resizing and collision-hardening behavior and can
sample new entropy when a fallback occurs. Neither byte identity, cross-runtime
enumeration nor identical future table traces follow from comparer persistence.

### Trusted state and comparers

State is an explicit snapshot schema. Restore performs the concrete collection's
documented reconstruction and checks; it is not a general repair operation for
arbitrary payloads. Keep the required ordering, uniqueness, ID and free-slot
invariants when producing or editing state. Validate external data before using
it as trusted state. Explicit state constructors keep these raw-state contracts.
Versioned JSON and MemoryPack for the five comparer-aware families additionally
restore recorded comparison rules; comparerless owners keep their formats.

Comparers are not stored in raw collection state. When using a raw DTO and
explicit state constructor, keep the application's comparer choice alongside the
snapshot when custom ordering or equality is required. State-only
deserialization cannot infer that choice from the items. Configuring an existing
instance first does not inject it: the state path selects defaults, and a
serializer may create a replacement instance. Do not use runtime hash codes as a
persisted identity.

| Collection family                                                          | Raw-state restoration contract                                                                                                                                                                                                                                                               |
| -------------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| `SwiftList<T>`, `SwiftObservableList<T>`, `SwiftObservableArray<T>`        | Copy item order; there is no stored comparer. Observable restoration does not restore event subscribers.                                                                                                                                                                                     |
| `SwiftQueue<T>`, `SwiftStack<T>`                                           | Copy FIFO order or bottom-to-top storage order respectively; there is no stored comparer.                                                                                                                                                                                                    |
| `SwiftSortedList<T>`                                                       | Copy item order without comparisons or sorting, and select `Comparer<T>.Default`. Items must already be ordered under the active comparer before ordered operations.                                                                                                                         |
| `SwiftDictionary<TKey, TValue>`, `SwiftObservableDictionary<TKey, TValue>` | Reinsert pairs using Swift default key equality. The first mapping for an equal key survives.                                                                                                                                                                                                |
| `SwiftHashSet<T>`                                                          | Reinsert non-null items using Swift default equality. The first equal item survives; null state items are skipped.                                                                                                                                                                           |
| `SwiftBiDictionary<T1, T2>`                                                | Reinsert with Swift default equality in both directions. A pair is skipped if its key or reverse value already exists.                                                                                                                                                                       |
| `SwiftPackedSet<T>`                                                        | Copy items in dense order and rebuild the lookup with Swift default equality. Null arrays/items throw `ArgumentNullException`; equal duplicates throw `ArgumentException`. Copying and indexing finish before owner storage changes. There is no custom comparer API.                        |
| `SwiftSparseSet`, `SwiftSparseMap<T>`                                      | Rebuild sparse membership from dense IDs, rejecting duplicates and invalid IDs; map key/value lengths must agree. Equality is integer identity.                                                                                                                                              |
| `SwiftSlotMap<T>`, `SwiftTicketMap<T>`                                     | Copy slot/free-index metadata and normalize the high-water mark. Free indices must be unique and disjoint from live entries; these two invariants are not validated. Ticket state also retains generation history. There is no stored comparer.                                              |
| `SwiftDenseTicketMap<T>`                                                   | Strictly validate dense/slot lengths, ID ranges, uniqueness and live/free/retired ownership; copy arrays and rebuild lookup. Preserve dense order, every issued generation and exact free order. Default/null arrays normalize to empty; reference payloads remain shared through raw state. |
| `SwiftTicketStore<T>` | Validate parallel value/index/generation lengths and supported unique indices; preserve dense order, copy arrays and rebuild private lookup. All generations are key data. Default/null arrays normalize to empty. Restore does not consult an issuer or restore spare capacity. |

Dimensional array shape checks are described above. `SwiftList<T>.SortInPlace`
uses a comparer only for that call; a list does not retain it or acquire a
sorted invariant. Pool types and spatial indexes do not expose collection state
with a caller-selected comparer.

#### Establish sorted ordering explicitly

The sorted-list state path trusts the payload's existing order. It does not
check whether the source used the default comparer, and even multiple items
whose type cannot be compared by default can be copied. Until a suitable
ordering is established, binary-search lookup and sorted mutation have no valid
contract for those items.

Use the existing enumerable constructor to copy and establish the intended
ordering once:

```csharp
using System.Collections.Generic;

var source = new SwiftSortedList<int>(new[] { 1, 2, 3 },
    Comparer<int>.Create((left, right) => right.CompareTo(left)));
SwiftArrayState<int> state = source.State; // Items: 3, 2, 1.
var restored = new SwiftSortedList<int>(state.Items, source.Comparer);
bool containsOne = restored.Contains(1); // true, using the selected comparer.
```

For independently restored snapshots, supply the application's saved comparer
choice instead of relying on a live source instance. The constructor copies the
array and sorts the owned copy; referenced elements remain shared. Equal items
have no stable tie-order guarantee. Use consistent comparison semantics across
simulation peers, including culture-independent ordering where required.

Alternatively, call `SetComparer(customComparer)` immediately after raw state
restoration, before lookup or mutation. It installs a different comparer and
sorts in place. Passing the current comparer instance is a no-op, so
`SetComparer(Comparer<T>.Default)` cannot repair unordered default state.
Configuring a comparer before assigning state also does not work: restoration
resets it. Comparer failures during sorting can leave partial changes; no
rollback is promised.

#### Select hash equality before inserting

Raw hash-state constructors rebuild lookup storage under default equality rather
than retaining serialized buckets. Their lookups are therefore consistent with
that default, but the application's equality semantics can change. Distinct
items under a custom comparer may collapse during default restore. Applying a
comparer afterward cannot recover discarded items or mappings.

Construct directly from the snapshot with the desired equality:

```csharp
var dictionary = new SwiftDictionary<TKey, TValue>(dictionaryState.Items, keyComparer);
var set = new SwiftHashSet<T>(setState.Items, itemComparer);
```

For `SwiftBiDictionary<T1, T2>`, create an empty instance with both comparers
and add the snapshot pairs. For `SwiftObservableDictionary<TKey, TValue>`,
create an empty instance, call `SetComparer(keyComparer)`, then add the snapshot
pairs. Check the Boolean `Add` result when duplicate rejection matters. Avoid
first building an intermediate dictionary with different equality, which can
discard pairs before the desired comparers see them. Enumerable hash-set
construction rejects null elements, unlike its state path, so supply non-null
items.

Changing a populated hash collection's comparer must preserve distinction among
its existing keys or items, and among reverse values for a bidictionary. The
rehash path does not provide general duplicate normalization; comparer failures
can leave partial changes. `SwiftPackedSet<T>` always uses its built-in default
lookup equality and offers no comparer override.

## Diagnostics

`Chronicler.Diagnostics` supplies the shared opt-in diagnostic APIs.
SwiftCollections retains `SwiftCollectionDiagnostics.Shared` in
`SwiftCollections.Diagnostics` for its own named channel:

- `DiagnosticChannel`
- `DiagnosticEvent`
- `DiagnosticLevel`
- `SwiftCollectionDiagnostics.Shared`
- interpolated diagnostic handlers that avoid formatting when disabled
- `ChronicleGuard` guard methods with caller-expression names, canonical default
  messages, and lazy interpolated exception messages for condition-gated throws

Diagnostics are disabled by default until a minimum level and sink are
configured.

### Configure the library channel

```csharp
using System;
using Chronicler.Diagnostics;
using SwiftCollections.Diagnostics;

var channel = SwiftCollectionDiagnostics.Shared;
channel.MinimumLevel = DiagnosticLevel.Warning;
channel.Sink = WriteDiagnostic;
channel.Warn("Capacity budget reached.");

static void WriteDiagnostic(in DiagnosticEvent diagnostic)
    => Console.WriteLine($"[{diagnostic.Channel}] {diagnostic.Message}");
```

Hosts own the sink and level. Logging callbacks are observations, not recorded
state or simulation scheduling. With C# 10 or later, handler overloads skip
interpolated expressions when disabled. Older compilers can use string overloads
and explicit `IsEnabled` checks to avoid eager formatting.
