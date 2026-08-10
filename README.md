# SwiftCollections

![SwiftCollections icon](https://raw.githubusercontent.com/mrdav30/SwiftCollections/main/icon.png)

[![Build](https://github.com/mrdav30/SwiftCollections/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/mrdav30/SwiftCollections/actions/workflows/build-and-test.yml)
[![Branch Coverage](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fmrdav30.github.io%2FSwiftCollections%2Fcoverage%2FSummary.json&query=%24.summary.branchcoverage&suffix=%25&label=branch%20coverage&color=brightgreen)](https://mrdav30.github.io/SwiftCollections/coverage/)
[![NuGet](https://img.shields.io/nuget/v/SwiftCollections.svg)](https://www.nuget.org/packages/SwiftCollections)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SwiftCollections.svg)](https://www.nuget.org/packages/SwiftCollections)
[![License](https://img.shields.io/github/license/mrdav30/SwiftCollections.svg)](https://github.com/mrdav30/SwiftCollections/blob/main/LICENSE)
[![API](https://img.shields.io/badge/docs-API-f4511e)](https://mrdav30.github.io/SwiftCollections/)
[![Discord](https://img.shields.io/badge/discord-join%20community-5865F2?logo=discord&logoColor=white)](https://discord.gg/mhwK2QFNBA)

**Specialized .NET collections for hot paths that need tighter control over
storage, allocation, iteration, and spatial queries.**

The BCL collections are excellent defaults. SwiftCollections is for the code
where profiling says the default is no longer enough: simulation loops, games,
spatial indexing, deterministic runtimes, and allocation-sensitive tooling.

## Why SwiftCollections?

- Low-allocation lists, queues, stacks, hash tables, buckets, packed sets,
  sparse sets, and sparse maps.
- Dense iteration and stable or generation-checked handles for entity-style
  workloads.
- BVH, spatial hash, and octree queries over `System.Numerics` volumes.
- Fixed-point query companions powered by
  [FixedMathSharp](https://github.com/mrdav30/FixedMathSharp).
- Explicit state-backed serialization, with Standard and Lean package variants.
- Benchmarks and full reachable coverage enforced as part of the repository
  workflow.

## Install

Choose one package family:

| Need                                                          | Package                                |
| ------------------------------------------------------------- | -------------------------------------- |
| Core collections with MemoryPack                              | `SwiftCollections`                     |
| Core collections without the MemoryPack runtime               | `SwiftCollections.Lean`                |
| FixedMathSharp spatial queries with MemoryPack                | `SwiftCollections.FixedMathSharp`      |
| FixedMathSharp spatial queries without the MemoryPack runtime | `SwiftCollections.FixedMathSharp.Lean` |

```bash
dotnet add package SwiftCollections
```

Or install a companion package when you need fixed-point query volumes. NuGet
brings in the matching SwiftCollections and FixedMathSharp packages:

```bash
dotnet add package SwiftCollections.FixedMathSharp
```

Use the matching `.Lean` packages as a pair. Lean keeps the collection APIs but
removes the MemoryPack runtime; it is useful when your application owns its
serialization stack or wants the smaller dependency surface.

## A quick taste

`SwiftGenerationalBucket<T>` gives you dense storage with handles that stop
resolving after their slot is reused:

```csharp
using System;
using SwiftCollections;

var actors = new SwiftGenerationalBucket<string>();
SwiftHandle player = actors.Add("player");

if (actors.TryGet(player, out string actor))
{
    Console.WriteLine(actor);
}

actors.Remove(player);
```

For externally owned compact integer IDs, start with `SwiftSparseSet` or
`SwiftSparseMap<T>`. For broad-phase queries, start with `SwiftBVH<TKey>`,
`SwiftSpatialHash<TKey>`, or `SwiftOctree<TKey>`.

## Learn more

- [Library overview and container guide](https://mrdav30.github.io/SwiftCollections/guides/OVERVIEW.html)
- [Core API reference](https://mrdav30.github.io/SwiftCollections/api/SwiftCollections.html)
- [Spatial query API](https://mrdav30.github.io/SwiftCollections/api/SwiftCollections.Query.html)
- [Coverage report](https://mrdav30.github.io/SwiftCollections/coverage/)
- [Contributing and local validation](CONTRIBUTING.md)

Using Unity? The maintained
[SwiftCollections-Unity packages](https://github.com/mrdav30/SwiftCollections-Unity)
add Unity-serializable adapters, `Bounds` conversions, GameObject pooling, and
FixedMathSharp integration.

## Compatibility

The libraries target `netstandard2.1` and `net8.0`. CI validates Standard and
Lean builds on Windows and Linux.

Questions and discussion are welcome in the
[Discord community](https://discord.gg/mhwK2QFNBA). SwiftCollections is
available under the [MIT License](LICENSE).
