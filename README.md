# SwiftCollections

![SwiftCollections icon](https://raw.githubusercontent.com/mrdav30/SwiftCollections/main/icon.png)

[![Build](https://github.com/mrdav30/SwiftCollections/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/mrdav30/SwiftCollections/actions/workflows/build-and-test.yml)
[![Coverage](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fmrdav30.github.io%2FSwiftCollections%2Fcoverage%2FSummary.json&query=%24.summary.branchcoverage&suffix=%25&label=coverage&color=brightgreen)](https://mrdav30.github.io/SwiftCollections/coverage/)
[![NuGet](https://img.shields.io/nuget/v/SwiftCollections.svg)](https://www.nuget.org/packages/SwiftCollections)
[![License](https://img.shields.io/github/license/mrdav30/SwiftCollections.svg)](https://github.com/mrdav30/SwiftCollections/blob/main/LICENSE)
[![API](https://img.shields.io/badge/docs-API-f4511e)](https://mrdav30.github.io/SwiftCollections/)
[![Discord](https://img.shields.io/badge/discord-join%20community-5865F2?logo=discord&logoColor=white)](https://discord.gg/mhwK2QFNBA)

**Specialized .NET collections for games, simulations, and tools where data
layout and predictable performance matter.**

Iterate packed agent data, keep generation-checked references to live objects,
query a 2D or 3D world, and reuse storage between updates. SwiftCollections gives
you control over allocation, identity, and iteration in an engine-agnostic
library built for demanding runtime workloads.

[Get started](https://mrdav30.github.io/SwiftCollections/guides/getting-started.html)
· [Choose a collection](https://mrdav30.github.io/SwiftCollections/guides/collections.html)
· [Explore the API](https://mrdav30.github.io/SwiftCollections/api/SwiftCollections.html)

## Why SwiftCollections?

- **Keep hot data close.** Lists, hash tables, dense sets, sparse maps, and
  ticket-based containers offer storage choices for lookup and packed iteration.
- **Give identities a clear owner.** Stable slots, generation-checked tickets,
  and externally keyed stores support registrations, agent records, and optional
  component data.
- **Find nearby objects.** BVH, spatial hash, quadtree, and octree indexes cover
  2D and 3D broad-phase queries with caller-owned result buffers.
- **Build deterministic worlds.** Fixed-point spatial queries powered by
  [FixedMathSharp](https://github.com/mrdav30/FixedMathSharp) complement the
  core `System.Numerics` backends.
- **Reuse storage deliberately.** Collection pools and explicit clearing
  controls help manage allocations, retained references, and ownership boundaries.
- **Persist the rules with the data.** State-backed JSON and MemoryPack
  serialization include comparer persistence for supported collection owners;
  Lean packages keep JSON support without the MemoryPack runtime.

Benchmarks and enforced 100% line, branch, and executable-method coverage support
the implementation. Use the BCL collections for general-purpose code, and choose
SwiftCollections when its storage or ownership model fits your workload.

## Install

Start with the core package:

```bash
dotnet add package SwiftCollections
```

Choose the package family that fits your application:

| Need                                                          | Package                                |
| ------------------------------------------------------------- | -------------------------------------- |
| Core collections with MemoryPack                              | `SwiftCollections`                     |
| Core collections without the MemoryPack runtime               | `SwiftCollections.Lean`                |
| FixedMathSharp spatial queries with MemoryPack                | `SwiftCollections.FixedMathSharp`      |
| FixedMathSharp spatial queries without the MemoryPack runtime | `SwiftCollections.FixedMathSharp.Lean` |

For deterministic fixed-point spatial queries, install the companion. NuGet
brings in the matching SwiftCollections and FixedMathSharp packages:

```bash
dotnet add package SwiftCollections.FixedMathSharp
```

Use the matching `.Lean` packages as a pair. Lean keeps the collection APIs but
removes the MemoryPack runtime; it is useful when your application owns its
serialization stack or wants the smaller dependency surface.

## A quick taste

`SwiftTicketMap<T>` gives you stable slots with generation-checked tickets.
Removal invalidates the current ticket, and ordinary slot reuse advances its
generation:

```csharp
using System;
using SwiftCollections;

var actors = new SwiftTicketMap<string>();
SwiftTicket player = actors.Add("player");

if (actors.TryGet(player, out string actor))
{
    Console.WriteLine(actor);
}

actors.Remove(player);
bool stillRegistered = actors.TryGet(player, out _); // false
```

Explore packed updates and optional components in the
[collection guide](https://mrdav30.github.io/SwiftCollections/guides/collections.html#who-issues-the-identity).
It shows how dense ticket maps and externally keyed stores fit those workloads,
with examples and the ownership, storage, and lifetime contracts for each choice.

## Learn more

The [documentation site](https://mrdav30.github.io/SwiftCollections/) brings
together working examples, collection tradeoffs, and the full API reference:

| Explore | Start here |
| --- | --- |
| Storage, identity, dense iteration, and clearing | [Collections guide](https://mrdav30.github.io/SwiftCollections/guides/collections.html) |
| 2D/3D indexing and deterministic fixed-point queries | [Spatial queries](https://mrdav30.github.io/SwiftCollections/guides/spatial-queries.html) |
| Rentals, reuse, and change notifications | [Pools and observables](https://mrdav30.github.io/SwiftCollections/guides/pools-and-observables.html) |
| State, comparer persistence, and host diagnostics | [Serialization and diagnostics](https://mrdav30.github.io/SwiftCollections/guides/serialization-and-diagnostics.html) |

For existing projects, see the
[migration guide](https://github.com/mrdav30/SwiftCollections/blob/main/docs/MIGRATION.md).
For benchmarks, coverage, and local development, see
[Contributing](CONTRIBUTING.md) and the
[coverage report](https://mrdav30.github.io/SwiftCollections/coverage/).

Using Unity? The maintained
[SwiftCollections-Unity packages](https://github.com/mrdav30/SwiftCollections-Unity)
add Unity-serializable adapters, `Bounds` conversions, GameObject pooling, and
FixedMathSharp integration.

## Compatibility

The libraries target `netstandard2.1` and `net10.0`. CI validates Standard and
Lean builds on Windows and Linux.

Questions and discussion are welcome in the
[Discord community](https://discord.gg/mhwK2QFNBA). SwiftCollections is
available under the [MIT License](LICENSE). See [Support](.github/SUPPORT.md),
the [Code of Conduct](CODE_OF_CONDUCT.md), and [Security](SECURITY.md) for help
and reporting guidance.
