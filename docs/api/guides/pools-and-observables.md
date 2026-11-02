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
