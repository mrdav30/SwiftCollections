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
`SwiftDictionaryState<TKey, TValue>`, `SwiftTicketMapState<T>`, and
`SwiftSparseMapState<T>`.

State-backed owners implement `Chronicler.IStateBacked<TState>`. Most use
`Chronicler.Serialization.StateJsonConverterFactory` for JSON: it writes a
`State` property and invokes the owner's explicit state constructor when
restoring it. `SwiftSortedList<T>` instead uses the versioned comparer-aware
contract described below. State validation belongs to the owner; this is
distinct from populating an existing `IRecordable` runtime shell.

Readonly state structs are not deeply immutable. Their constructors retain
supplied arrays; some normalize null arrays to empty arrays, while default
struct values bypass those constructors. Collection `State` getters allocate
shallow snapshots, so their array storage is independent but referenced elements
remain shared. Validate externally supplied dimensions, IDs and free-slot
metadata against the concrete collection's restore contract.

Dimensional arrays validate restored shapes before copying data into independently
owned storage. Each dimension must be non-negative, and its exact product must
fit in `Int32` and equal the data length. Any zero axis gives an empty shape,
including when the other dimensions are large, and requires empty data. Negative
dimensions throw `ArgumentOutOfRangeException`, an overflowing product throws
`OverflowException`, and a length mismatch throws `ArgumentException`. Null data
retains the existing exception distinction: 2D arrays throw
`ArgumentNullException`; 3D arrays throw `NullReferenceException`.

`Resize` rejects negative dimensions and overflowing products before changing the
array. Failed resize or validation/copying by the dimensional owner's state
setter preserves its shape, stored values and backing array. Ordinary 2D
dimensional constructors, including the bool and short specializations, retain
their negative-to-zero clamping behavior;
3D dimensional constructors reject negative dimensions. These checks do not
change the passive `Array2DState<T>` and `Array3DState<T>` constructors, which
retain supplied data without validation or copying.

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
lowercase ASCII letters, digits, dots, underscores, hyphens or slashes; `lsf.` is
reserved for built-ins. Configurations contain at most 4,096 decoded bytes.
Malformed, unknown or unsupported metadata fails before an owner is published;
normal readers reject state-only legacy payloads. Null owners retain the normal
serializer null representation, while non-null empty owners still need a rule.

#### Closed JSON construction and generated metadata

The normal factory creates generic converters through reflection. For a closed
serialization contract, register `SwiftCollectionJsonConverters.ForSortedList<T>`
instead. Options converters take precedence over the owner's converter
attribute, including when ordinary generated container metadata visits nested
lists. Supply metadata for both the owner and its unchanged raw state, plus
element and containing types:

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
supports exact closed `SwiftSortedList<T>` types. Explicitly serializing a derived
instance as that base type records its base items and ordering only; it restores
a base owner and omits derived fields. Full derived and interface-typed contracts
need explicit host integration; do not assume they select the same converter.

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
`SwiftComparerRegistry` service into it, preserving its other services. A missing
service selects built-ins only; it does not recover unregistered custom rules.
Keep compatible element formatters and options, including string encoding, on
both sides. Explicit `RegisterFormatter()` avoids relying on reflective cold
discovery. Modern and `netstandard2.1` MemoryPack binary APIs require matching
dependency graphs; do not swap only one generated DLL between them. Validate the
actual host's stripping and AOT profile. Lean retains the registry and JSON APIs
without these MemoryPack entry points or dependencies.

Unsupported capture throws `NotSupportedException`. Invalid comparison metadata
is reported as `JsonException` or `MemoryPackSerializationException` when reading
the corresponding format. A failed binary overwrite leaves the existing owner
reference unchanged. Factory or element callback side effects are not rolled
back, and a later serialization failure can leave partial writer output.

#### Version-one records only

Sorted-list owner persistence supports only the version-one envelope. Old
state-only JSON and MemoryPack owner records are rejected; there is no legacy
reader, fallback or migration helper. This is an intentional breaking format
change, so upgrade writers and readers together and use version-one records.

The unchanged raw `SwiftArrayState<T>` and explicit state constructor serve the
trusted-state contract below. They do not make old serialized owner records
compatible with the new format.

### Trusted state and comparers

State is an explicit snapshot schema. Restore performs the concrete collection's
documented reconstruction and checks; it is not a general repair operation for
arbitrary payloads. Keep the required ordering, uniqueness, ID and free-slot
invariants when producing or editing state. Validate external data before using
it as trusted state. Explicit state constructors keep these raw-state contracts.
Sorted-list versioned JSON and MemoryPack additionally restore the recorded
comparer; other owners retain their existing state-only restoration behavior.

Comparers are not stored in raw collection state. Outside the sorted-list
versioned format, keep the application's comparer choice alongside the snapshot
when custom ordering or equality is required. State-only deserialization cannot
infer that choice from the items. Configuring an existing instance first does
not inject it: the state path selects defaults, and a serializer may create a
replacement instance. Do not use runtime hash codes as a persisted identity.

| Collection family | Raw-state restoration contract |
| ----------------- | -------------------- |
| `SwiftList<T>`, `SwiftObservableList<T>`, `SwiftObservableArray<T>` | Copy item order; there is no stored comparer. Observable restoration does not restore event subscribers. |
| `SwiftQueue<T>`, `SwiftStack<T>` | Copy FIFO order or bottom-to-top storage order respectively; there is no stored comparer. |
| `SwiftSortedList<T>` | Copy item order without comparisons or sorting, and select `Comparer<T>.Default`. Items must already be ordered under the active comparer before ordered operations. |
| `SwiftDictionary<TKey, TValue>`, `SwiftObservableDictionary<TKey, TValue>` | Reinsert pairs using Swift default key equality. The first mapping for an equal key survives. |
| `SwiftHashSet<T>` | Reinsert non-null items using Swift default equality. The first equal item survives; null state items are skipped. |
| `SwiftBiDictionary<T1, T2>` | Reinsert with Swift default equality in both directions. A pair is skipped if its key or reverse value already exists. |
| `SwiftPackedSet<T>` | Copy dense items and rebuild the lookup with Swift default equality. Supply distinct non-null items; duplicate state is not repaired. There is no custom comparer API. |
| `SwiftSparseSet`, `SwiftSparseMap<T>` | Rebuild sparse membership from dense IDs, rejecting duplicates and invalid IDs; map key/value lengths must agree. Equality is integer identity. |
| `SwiftSlotMap<T>`, `SwiftTicketMap<T>` | Copy slot/free-index metadata and normalize the high-water mark. Free indices must be unique and disjoint from live entries; these two invariants are not validated. Ticket state also retains generation history. There is no stored comparer. |

Dimensional array shape checks are described above. `SwiftList<T>.SortInPlace`
uses a comparer only for that call; a list does not retain it or acquire a sorted
invariant. Pool types and spatial indexes do not expose collection state with a
caller-selected comparer.

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

Hash collections rebuild their lookup storage under default equality rather
than retaining serialized buckets. Their lookups are therefore consistent with
that default, but the application's equality semantics can change. Distinct
items under a custom comparer may collapse during default restore. Applying a
comparer afterward cannot recover discarded items or mappings.

Construct directly from the snapshot with the desired equality:

```csharp
var dictionary = new SwiftDictionary<TKey, TValue>(dictionaryState.Items, keyComparer);
var set = new SwiftHashSet<T>(setState.Items, itemComparer);
```

For `SwiftBiDictionary<T1, T2>`, create an empty instance with both comparers and
add the snapshot pairs. For `SwiftObservableDictionary<TKey, TValue>`, create an
empty instance, call `SetComparer(keyComparer)`, then add the snapshot pairs.
Check the Boolean `Add` result when duplicate rejection matters. Avoid first
building an intermediate dictionary with different equality, which can discard
pairs before the desired comparers see them. Enumerable hash-set construction
rejects null elements, unlike its state path, so supply non-null items.

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
