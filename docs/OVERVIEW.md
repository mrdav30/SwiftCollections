# SwiftCollections Overview

SwiftCollections provides specialized .NET collections for hot paths where storage layout, allocation behavior, deterministic hashing, dense iteration, pooling, or spatial query performance matter more than broad general-purpose ergonomics.

Use the standard .NET collections first when the workload is ordinary. Reach for SwiftCollections when the code is already performance-sensitive, when a benchmark points at collection cost, or when the data model naturally fits one of the specialized containers below.

## Design Goals

- Keep common operations low time complexity.
- Prefer contiguous storage and dense iteration where it improves cache behavior.
- Avoid hidden allocations in simulation and query hot paths.
- Keep APIs framework-agnostic and usable from .NET, game engines, tools, and servers.
- Preserve deterministic-friendly behavior where the collection owns hashing or ordering choices.
- Keep serialization explicit through state-backed types and package variants.

## Core Collections

| Type | Primary Role | Notes |
| --- | --- | --- |
| `SwiftList<T>` | Dynamic contiguous list | Familiar list shape with SwiftCollections growth and helper behavior. |
| `SwiftQueue<T>` | Circular-buffer queue | Suited for repeated enqueue/dequeue workloads. |
| `SwiftStack<T>` | Array-backed stack | Simple LIFO storage for low-overhead stack workloads. |
| `SwiftSortedList<T>` | Sorted dynamic collection | Keeps values ordered for search and ordered iteration. |
| `SwiftDictionary<TKey, TValue>` | Hash table key/value lookup | Uses deterministic default string comparers when no comparer is supplied. |
| `SwiftHashSet<T>` | Hash table set membership | Uses deterministic default string comparers when no comparer is supplied. |
| `SwiftBiDictionary<TLeft, TRight>` | Two-way lookup | Maintains forward and reverse mappings. |

`SwiftDictionary<TKey, TValue>` and `SwiftHashSet<T>` are the closest alternatives to `Dictionary<TKey, TValue>` and `HashSet<T>`. They are not intended as blind drop-in replacements. Benchmark the workload you care about, especially when custom comparers, string keys, collision behavior, or trim/resize patterns are important.

## Specialized Containers

| Use case | Better fit | Why |
| --- | --- | --- |
| Store this object and give me a stable slot | `SwiftBucket<T>` | The container owns integer slot assignment and can reuse freed slots. |
| Store this object and protect against stale references | `SwiftGenerationalBucket<T>` | Handles include index plus generation, so reused slots invalidate old handles. |
| I already have a compact non-negative int ID; track membership | `SwiftSparseSet` | Sparse lookup plus dense key storage gives O(1) membership and dense iteration. |
| I already have a compact non-negative int ID; attach a value | `SwiftSparseMap<T>` | Sparse lookup maps external IDs to densely stored values. |
| I need dense unique-value iteration with hash-backed membership | `SwiftPackedSet<T>` | Values are packed for iteration while membership remains hash-backed. |
| IDs are arbitrary, huge, or widely sparse | `SwiftHashSet<int>` / `SwiftDictionary<int, T>` | Hash tables avoid allocating sparse arrays up to the highest ID. |

Sparse containers are strongest when IDs are compact, non-negative, and externally owned by a simulation, ECS, partition, or handle system. Their memory usage scales with the highest stored ID, not only with the number of live IDs.

## Spatial Query Structures

The query namespace provides mutable broad-phase structures over typed bounds:

| Type | Best Fit |
| --- | --- |
| `SwiftBVH<TKey>` / `SwiftBVH<TKey, TVolume>` | Mixed-size objects, broad intersection queries, and heterogeneous scenes. |
| `SwiftSpatialHash<TKey>` / `SwiftSpatialHash<TKey, TVolume>` | High-churn scenes with mostly uniform object sizes and sparse query windows. |
| `SwiftOctree<TKey>` / `SwiftOctree<TKey, TVolume>` | Dynamic scenes with uneven density and repeated regional queries. |

The default wrappers use `System.Numerics` `BoundVolume`. The `SwiftCollections.FixedMathSharp` package adds fixed-point wrappers for deterministic simulations:

- `SwiftFixedBVH<TKey>`
- `SwiftFixedSpatialHash<TKey>`
- `SwiftFixedOctree<TKey>`
- `FixedBoundVolume`

Query structures are mutable runtime indexes. Treat them as single-owner structures unless you add synchronization externally.

## Pools

SwiftCollections includes general and typed pool helpers:

- `SwiftObjectPool<T>`
- `SwiftArrayPool<T>`
- `SwiftCollectionPool<TCollection, TItem>`
- typed pool helpers for `SwiftList<T>`, `SwiftQueue<T>`, `SwiftHashSet<T>`, `SwiftDictionary<TKey, TValue>`, `SwiftStack<T>`, `SwiftPackedSet<T>`, and `SwiftSparseMap<T>`

Pools are useful when collection instances are repeatedly rented, cleared, and reused across simulation frames or query batches.

## Observable Collections

Observable collections are intended for tooling, editor, diagnostics, or host-facing change tracking:

- `SwiftObservableArray<T>`
- `SwiftObservableList<T>`
- `SwiftObservableDictionary<TKey, TValue>`
- `SwiftObservableProperty<TValue>`

Avoid using observable notifications in authoritative per-frame simulation paths unless the ordering and notification cost are explicitly tested and benchmarked.

## Serialization

The standard packages include MemoryPack support where the source type is marked with `[MemoryPackable]`. Lean packages compile the same collection APIs without the MemoryPack dependency by using compatibility shims and excluding MemoryPack-specific source.

Package variants:

- `SwiftCollections`
- `SwiftCollections.Lean`
- `SwiftCollections.FixedMathSharp`
- `SwiftCollections.FixedMathSharp.Lean`

State-backed collection types expose explicit state structs such as `SwiftArrayState<T>`, `SwiftBucketState<T>`, `SwiftDictionaryState<TKey, TValue>`, `SwiftGenerationalBucketState<T>`, and `SwiftSparseMapState<T>`.

## Diagnostics

`SwiftCollections.Diagnostics` provides a small opt-in diagnostic surface:

- `DiagnosticChannel`
- `DiagnosticEvent`
- `DiagnosticLevel`
- `SwiftCollectionDiagnostics.Shared`
- interpolated diagnostic handlers that avoid formatting when disabled

Diagnostics are disabled by default until a minimum level and sink are configured.

## Development And Verification

Build:

```bash
dotnet build SwiftCollections.slnx -c Debug
```

Run tests:

```bash
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build
dotnet test tests/SwiftCollections.FixedMathSharp.Tests/SwiftCollections.FixedMathSharp.Tests.csproj -c Debug --no-build
```

Run coverage:

```bash
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.Tests/coverlet.runsettings
dotnet test tests/SwiftCollections.FixedMathSharp.Tests/SwiftCollections.FixedMathSharp.Tests.csproj -c Debug --no-build --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.Tests/coverlet.runsettings
```

List benchmark aliases:

```bash
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- list
```

Run selected benchmarks:

```bash
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- dictionary
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- query --list flat
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- all --list flat
```
