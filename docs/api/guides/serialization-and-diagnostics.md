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

For dictionaries and sets, comparers are not serialized. Restoring state selects
the collection's default comparer; reapply a custom comparer explicitly when
required. Do not use runtime hash codes as a persisted identity.

`SwiftSortedList<T>` also restores the default comparer, but does not re-sort its
stored items. Restore only default-ordered items, or install the original custom
comparer before performing ordering-dependent operations. A descending snapshot
restored under the default ascending comparer can otherwise fail lookup.

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
