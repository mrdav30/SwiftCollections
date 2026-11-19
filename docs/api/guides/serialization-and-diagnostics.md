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

These types implement `Chronicler.IStateBacked<TState>` and use
`Chronicler.Serialization.StateJsonConverterFactory` for JSON. The converter
writes a `State` property and invokes the collection's explicit state
constructor when restoring it. State validation belongs to that collection; this
is distinct from populating an existing `IRecordable` runtime shell.

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

The factory discovers the declared state contract with reflection and compiles a
constructor delegate. A state value is an explicit schema, not an arbitrary
object-graph discovery mechanism. See the
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

### Trusted state and comparers

State is an explicit snapshot schema. Restore performs the concrete collection's
documented reconstruction and checks; it is not a general repair operation for
arbitrary payloads. Keep the required ordering, uniqueness, ID and free-slot
invariants when producing or editing state. Validate external data before using
it as trusted state. The same contracts apply to explicit state constructors,
JSON restoration and standard-build MemoryPack restoration.

Comparers are not stored in collection state. Keep the application's comparer
choice alongside the snapshot when custom ordering or equality is required.
The constructor selected by automatic deserialization cannot infer that choice
from the items. Configuring an existing instance before deserialization is not
a way to inject it: the state path selects defaults, and a serializer may create
a replacement instance. Do not use runtime hash codes as a persisted identity.

| Collection family | Restoration contract |
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
