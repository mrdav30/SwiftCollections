---
title: Pools and observable collections
---

# Pools and observable collections

## Pools

SwiftCollections includes general and typed pool helpers:

- `SwiftObjectPool<T>`
- `SwiftArrayPool<T>`
- `SwiftCollectionPool<TCollection, TItem>`
- typed pool helpers for `SwiftList<T>`, `SwiftQueue<T>`, `SwiftHashSet<T>`,
  `SwiftDictionary<TKey, TValue>`, `SwiftStack<T>`, `SwiftPackedSet<T>`, and
  `SwiftSparseMap<T>`

Pools are useful when collection instances are repeatedly rented, cleared, and
reused across simulation frames or query batches.

## Scope a rental

```csharp
using SwiftCollections;
using SwiftCollections.Pool;

using var pool = new SwiftListPool<int>();
using (pool.Get(out SwiftList<int> scratch))
{
    scratch.Add(42);
    // Use the list during this lease. Disposal returns it to the pool.
}
```

Stop using a rented object after returning it, and return each lease only once.
The lease's disposal runs even if the body throws. Do not dispose a shared pool
while other owners still use it. Pooling does not make the rented collection
safe for concurrent mutation.

`SwiftObjectPool<T>.Clear()` and collection-pool `Clear()` remove only cached
inactive objects. Outstanding rentals remain valid and counted by the same
backing pool. Each object removed by object-pool clearing is deducted from
`CountAll` before its destroy callback; if that callback throws, earlier removals
remain completed and the remaining cached objects and rentals stay counted.

Complete rentals before `Flush` or disposal, and before clearing a
`SwiftArrayPool<T>`: array-pool clearing discards its length buckets, so later
returns do not necessarily reach their original backing pool. Concurrent backing
storage does not make counters, callbacks, capacity checks or lifecycle changes
atomic; synchronize those operations externally. Lease disposal attempts a return
at most once; if the release callback throws, a later disposal does not retry it.
Object-pool counters are diagnostics, not ownership validation. After disposal,
`CountAll` is retained while cached storage is emptied, so `CountActive` no longer
represents live rentals.

Collection-pool disposal permanently closes that owner, including access through
`Get` and `CollectionPool`. The six typed collection wrappers and the sparse-map
wrapper follow the same lifetime rules. `Clear` is reusable and does nothing
after disposal. The collection-base family's `Flush` replaces an initialized
backing pool for reuse; it does not initialize an unused pool or revive a disposed
owner. Finish leases before flushing, since they still target the old backing
pool. Disposing a `Shared` singleton affects every consumer; it is not recreated.

Typed wrappers ignore null returns while usable and reject them after disposal.
Custom subclasses of `SwiftCollectionPool<TCollection, TItem>` retain the generic
base's null-return rejection. Collection clearing occurs once in the backing
pool's release callback and preserves reusable capacity.
Custom subclasses that provide their own disposal must also call `base.Dispose()`
to close the shared owner. An existing custom disposal implementation does not
automatically gain that cleanup merely because the base now implements `IDisposable`.

Pools use their normal clearing policies. If a reference-containing collection
has retained values after `FastClear()`, explicitly call `Clear(true)` before
returning it when your ownership contract requires full reference release. See
[retained-reference cleanup](collections.md#clearing-and-retained-references).

## Observable Collections

Observable collections are intended for tooling, editor, diagnostics, or
host-facing change tracking:

- `SwiftObservableArray<T>`
- `SwiftObservableList<T>`
- `SwiftObservableDictionary<TKey, TValue>`
- `SwiftObservableProperty<TValue>`

Avoid using observable notifications in authoritative per-frame simulation paths
unless the ordering and notification cost are explicitly tested and benchmarked.

Keep event subscription lifetimes explicit. Unsubscribe handlers when a host or
view is released, and avoid changing authoritative simulation decisions from
notifications whose call order the host has not defined.
Notifications run synchronously after mutation. A handler exception propagates
without rolling the mutation back and can prevent later notifications. Only the
observable mutation members emit events. Dictionary indexer assignments use the
observable override through base, generic dictionary and non-generic dictionary
references. The observable list indexer remains hidden; base-typed list indexer
writes, direct storage writes and inherited non-overridden mutators can bypass
notifications.
