# SwiftCollections Migration Guide

## Migrating from v8 to v9

### FixedMathSharp v9 dependency alignment

The companion also requires the matching Standard or Lean SwiftCollections core
package at `9.0.0` or newer. Publish the required core packages before releasing
the companion; an older core does not provide all query support used by the
current companion.

The FixedMathSharp companion now requires `FixedMathSharp` 9.0.0, or
`FixedMathSharp.Lean` 9.0.0 for Lean. Its 2D bounds adapter uses the exact
`FixedBoundArea.Area` and `GetAreaExpansionCost` APIs introduced in v9. The core
SwiftCollections package still has no FixedMathSharp dependency.

Update the matching Standard or Lean family and rebuild consumers against the
resolved package graph. `UseLocalLsfStack=true` selects sibling source projects;
a passing source build does not validate the released-package references. Keep
source and package validation separate, including netstandard2.1 assets.

FixedMathSharp v9 removes `ToDirection`, rejects zero rotation inputs and
corrects geometry/numeric edge cases. Review its
[migration guide](https://github.com/mrdav30/FixedMathSharp/blob/v9.0.0/docs/MIGRATION.md#migrating-from-v8x-to-v9x)
and verify saved-state/replay expectations at the owning consumer. This
dependency alignment does not change SwiftCollections collection state schemas
or imply a downstream release. Update CI source pins after the reviewed consumer
commits land.

### BVH key ownership

`SwiftBVH<TKey, TVolume>`, `SwiftBVH<TKey>` and `SwiftFixedBVH<TKey>` now match
spatial hash and octree key ownership. `Insert` returns `true` only for a new
key. Reinserting an equal key updates its bounds and returns `false`; it retains
the original key object and leaf index and leaves `Count` unchanged. Queries no
longer emit multiple leaves for that key, and one successful removal removes it.
Use distinct stable keys when you need multiple independently indexed entries.
Null insertion throws `ArgumentNullException` before changing the tree, matching
lookup, update and removal. Callers relying on duplicate leaves or an
always-true return must adapt. No signatures or persistence formats change; no
release is implied by this entry.

### Collection comparison metadata

<a id="sorted-list-comparison-metadata-unreleased"></a>

`SwiftSortedList<T>`, `SwiftDictionary<TKey, TValue>`, `SwiftHashSet<T>`,
`SwiftBiDictionary<T1, T2>` and `SwiftObservableDictionary<TKey, TValue>` now
write a version-one comparison envelope through normal JSON and standard
MemoryPack serialization. Old state-only payloads are rejected by the new normal
readers; old readers also reject the new envelope. Upgrade writers and readers
together. The library supports only the new owner format; there is no legacy
reader, fallback or migration helper. No package release is implied by this
entry.

Eligible runtime-default ordering, runtime equality, ordinal string and known
Swift seeded/randomized equality rules need no host registration. Custom
comparers, including lambdas, require a stable typed codec and reconstruction
factory. Saving an unsupported rule now fails instead of silently losing it.
Supply the registry through JSON options or MemoryPack's per-operation service
provider, including for nested collections. Lean supports the same JSON contract
and registry without MemoryPack.

Previously saved state-only owner records cannot be passed to the new collection
serializers. The raw `SwiftArrayState<T>` and
`SwiftDictionaryState<TKey, TValue>` schemas and explicit state constructors
remain unchanged trusted-state APIs; they are not compatibility readers for old
owner records. Comparerless owners keep their existing formats. Persist a
bidictionary through its concrete contract: a Dictionary base contract rejects
it rather than losing reverse comparison rules. New hash-owner reads reject
duplicate equal keys/items/reverse values under restored rules; HashSet retains
its state-path behavior of skipping null items.

See
[hash equality persistence](api/guides/serialization-and-diagnostics.md#preserve-hash-equality-during-serialization)
and
[sorted-list persistence](api/guides/serialization-and-diagnostics.md#preserve-sorted-list-ordering-during-serialization)
for registration, closed JSON metadata, formatter bootstrap and failure
contracts.

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
