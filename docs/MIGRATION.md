# SwiftCollections Migration Guide

## Migrating from v7 to v8

v8 updates the Chronicler and FixedMathSharp dependencies, moves shared
diagnostics into Chronicler, renames the slot-based collections, and adds
explicit retained-reference clearing. These changes introduce new public type
identities, so rebuild consumers and adapters against the updated packages.

### Package and framework changes

Update the packages you use to `8.0.0`, keeping standard and Lean families
consistent:

| Package                                | Dependency changes                                                                       |
| -------------------------------------- | ---------------------------------------------------------------------------------------- |
| `SwiftCollections`                     | `Chronicler.Core` and `Chronicler.Diagnostics` v1.0.0                                    |
| `SwiftCollections.Lean`                | `Chronicler.Core.Lean`, `Chronicler.MemoryPackShim`, and `Chronicler.Diagnostics` v1.0.0 |
| `SwiftCollections.FixedMathSharp`      | SwiftCollections v8.0.0 and FixedMathSharp v8.0.0                                        |
| `SwiftCollections.FixedMathSharp.Lean` | Matching Lean dependencies at v8.0.0                                                     |

Installing a companion package brings in its matching core and math
dependencies. Review the upstream
[Chronicler migration guide](https://github.com/mrdav30/Chronicler/blob/main/docs/MIGRATION.md)
and, for fixed-point consumers, the
[FixedMathSharp migration guide](https://github.com/mrdav30/FixedMathSharp/blob/main/docs/MIGRATION.md)
when updating code that uses those libraries directly.

Both runtime libraries now target `netstandard2.1` and `net10.0`; the dedicated
`net8.0` asset has been replaced by `net10.0`. The `netstandard2.1` asset
remains available for compatible runtimes. Building this repository requires the
.NET 10 SDK, and its tests and benchmarks run on .NET 10. This does not require
package consumers to use C# 14.

### Slot maps and tickets

The slot-based collections now use names that describe their ownership model:

| v7 API                                                         | v8 API                                       |
| -------------------------------------------------------------- | -------------------------------------------- |
| `SwiftBucket<T>`                                               | `SwiftSlotMap<T>`                            |
| `SwiftGenerationalBucket<T>`                                   | `SwiftTicketMap<T>`                          |
| `SwiftHandle`                                                  | `SwiftTicket`                                |
| `SwiftBucketState<T>`                                          | `SwiftSlotMapState<T>`                       |
| `SwiftGenerationalBucketState<T>`                              | `SwiftTicketMapState<T>`                     |
| `SwiftBucket<T>.SwiftBucketEnumerator`                         | `SwiftSlotMap<T>.SwiftSlotMapEnumerator`     |
| `SwiftGenerationalBucket<T>.SwiftGenerationalBucketEnumerator` | `SwiftTicketMap<T>.SwiftTicketMapEnumerator` |

`SwiftSlotMap<T>.Add` returns an integer slot. `SwiftTicketMap<T>.Add` returns a
`SwiftTicket` containing an index and generation; pass that ticket back for
lookup and removal. Both maps issue their own identities. Use
`SwiftSparseMap<T>` when another system already owns the integer IDs. See the
[selection guide and examples](api/guides/collections.md#who-issues-the-identity).

Replace the old names and rebuild consumers and adapters; there are no aliases
or forwarding wrappers. Ticket-map methods now name their identity parameter
`ticket` instead of `handle`, so update named arguments too.
`SwiftTicket.ToString()` returns `Ticket(index:generation)`.

State fields and their order are unchanged. Regression fixtures captured from
the published v7 package verify typed JSON and MemoryPack collection payloads,
including free-slot reuse and generation validation after restore. When a host
stores CLR type names or maintains serializer registrations, migrate those names
and registrations separately. Tickets still contain `Index` and `Generation`,
carry no collection identity, and must only be used with their originating map.

### Chronicler imports

Recording contracts such as `IStateBacked<TState>` remain in `Chronicler`. State
JSON converters now use `Chronicler.Serialization`. Add or update that import
when referring to converter types such as `StateJsonConverterFactory`:

```csharp
using Chronicler;
using Chronicler.Serialization;
```

### Shared diagnostics and guards

Shared logging and exception guards now come from the independent
`Chronicler.Diagnostics` package (v1.0.0), in both standard and Lean builds.
Update shared diagnostic types and guards as follows:

| v7 API                                                       | v8 API                                                  |
| ------------------------------------------------------------ | ------------------------------------------------------- |
| Shared `Diagnostic*` types in `SwiftCollections.Diagnostics` | Corresponding types in `Chronicler.Diagnostics`         |
| `SwiftThrowHelper`                                           | `Chronicler.Diagnostics.ChronicleGuard`                 |
| `SwiftThrowInterpolatedStringHandler`                        | `Chronicler.Diagnostics.ThrowInterpolatedStringHandler` |

`SwiftCollectionDiagnostics.Shared` stays in `SwiftCollections.Diagnostics`.
Keep that import when configuring the library's channel, alongside the new
shared namespace:

```csharp
using Chronicler.Diagnostics;
using SwiftCollections.Diagnostics;
```

Rebuild consumers and adapters together: moved types have new CLR identities,
including types exposed in logger signatures. There are no forwarding wrappers.
This extraction does not change recorded fields or record-hash identities.

### Clearing retained references

Core collections now expose `Clear(bool clearRetainedReferences)`. Existing
`Clear()` calls retain their normal behavior. At an ownership boundary, use
`Clear(clearRetainedReferences: true)` to release reference-containing values
throughout retained backing storage while preserving capacity:

```csharp
scratch.FastClear(); // On collections that support it, references remain stored.
scratch.Clear(clearRetainedReferences: true); // Also clear references beyond Count.
```

Full cleanup can take time proportional to retained capacity. It does not
promise to zero unused numeric storage. `FastClear()` remains a count-only
operation on types that support it.

`SwiftTicketMap<T>` also gains `Clear()` and the explicit cleanup overload.
Clearing invalidates live tickets and preserves generation history; saving and
restoring a cleared map preserves subsequent ticket allocation. See
[Collections and clearing](api/guides/collections.md#clearing-and-retained-references)
for the complete ownership and cost guidance.

### Enumerator corrections

Affected handwritten non-generic enumerators now reject `Current` before the
first successful `MoveNext()` and after enumeration ends. Fixes also cover the
last valid item, sparse map slots, valid null values, and mutation detection on
ticket-map `Reset()`.

Code that reads `IEnumerator.Current` outside a valid position should instead
check `MoveNext()` before each read. Ordinary `foreach` usage needs no change;
the corrections preserve the generic iteration sequence.

### Coordinated source builds

`UseLocalLsfStack=true` uses evergreen per-library build identities defined in
`Directory.Build.props`: `0.0.0-sc-local-lsf`, `0.0.0-fms-local-lsf`, and
`0.0.0-chronicler-local-lsf`, with assembly/file version `0.0.0.0`. Do not bump
these identities for releases or restore the previous numeric source overrides.

Use matching sibling revisions and the commands in
[CONTRIBUTING.md](../CONTRIBUTING.md#coordinated-source-development). Package
mode remains the release-validation path; source-mode packages are development
artifacts.
