# SwiftCollections

![SwiftCollections Icon](https://raw.githubusercontent.com/mrdav30/SwiftCollections/main/icon.png)

[![Build](https://github.com/mrdav30/SwiftCollections/actions/workflows/build-and-test.yml/badge.svg)](https://github.com/mrdav30/SwiftCollections/actions/workflows/build-and-test.yml)
[![Coverage](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fmrdav30.github.io%2FSwiftCollections%2FSummary.json&query=%24.summary.linecoverage&suffix=%25&label=coverage&color=brightgreen)](https://mrdav30.github.io/SwiftCollections/)
[![NuGet](https://img.shields.io/nuget/v/SwiftCollections.svg)](https://www.nuget.org/packages/SwiftCollections)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SwiftCollections.svg)](https://www.nuget.org/packages/SwiftCollections)
[![License](https://img.shields.io/github/license/mrdav30/SwiftCollections.svg)](https://github.com/mrdav30/SwiftCollections/blob/main/LICENSE)
[![Frameworks](https://img.shields.io/badge/frameworks-netstandard2.1%20%7C%20net8.0-512BD4.svg)](https://github.com/mrdav30/SwiftCollections)

SwiftCollections is a performance-oriented collection library for .NET systems that care about hot-path cost: simulations, games, spatial queries, deterministic runtimes, and allocation-sensitive tooling.

The standard .NET collections are excellent general-purpose defaults. SwiftCollections is for the places where you need more control over storage layout, pooling, dense iteration, externally owned integer IDs, deterministic string hashing, or broad-phase spatial query structures. It is not meant to replace every `List<T>` or `Dictionary<TKey, TValue>` in an application. It is meant to give performance-critical code a sharper set of tools.

## Why Use It

- Low-allocation collection types for repeated simulation and gameplay loops.
- Hash tables, lists, queues, stacks, buckets, packed sets, sparse sets, and sparse maps with APIs designed for predictable hot-path behavior.
- Dense integer-ID membership and value lookup through `SwiftSparseSet` and `SwiftSparseMap<T>`.
- Stable-handle storage through `SwiftBucket<T>` and generation-checked handles through `SwiftGenerationalBucket<T>`.
- Spatial query structures: BVH, spatial hash, and octree implementations over `System.Numerics` volumes, plus fixed-point companions through `SwiftCollections.FixedMathSharp`.
- Optional MemoryPack serialization in standard packages, with lean variants for projects that want the same core APIs without MemoryPack.
- Benchmarks and high coverage are part of the repo workflow, not an afterthought.

## Packages

Choose the package that matches your runtime and serialization needs.

| Package | Use When |
| --- | --- |
| `SwiftCollections` | You want the core collections with MemoryPack support. |
| `SwiftCollections.Lean` | You want the core collections without the MemoryPack dependency. |
| `SwiftCollections.FixedMathSharp` | You need fixed-point BVH, octree, or spatial hash volume wrappers backed by FixedMathSharp. |
| `SwiftCollections.FixedMathSharp.Lean` | You need the fixed-point companion without MemoryPack. |

```bash
dotnet add package SwiftCollections
dotnet add package SwiftCollections.Lean
dotnet add package SwiftCollections.FixedMathSharp
dotnet add package SwiftCollections.FixedMathSharp.Lean
```

The standard and lean variants expose the same core collection APIs. The difference is whether MemoryPack is included. If you are targeting Unity Burst AOT or already have a serializer pipeline, prefer the lean variants.

Unity package support lives in a separate repository: [SwiftCollections-Unity](https://github.com/mrdav30/SwiftCollections-Unity).

## Picking A Container

| Use case | Better fit |
| --- | --- |
| General key/value lookup with arbitrary keys | `SwiftDictionary<TKey, TValue>` |
| General unique values | `SwiftHashSet<T>` |
| Fast list, queue, stack, or sorted-list operations | `SwiftList<T>`, `SwiftQueue<T>`, `SwiftStack<T>`, `SwiftSortedList<T>` |
| Store objects and receive stable integer slots | `SwiftBucket<T>` |
| Store objects and guard against stale handles | `SwiftGenerationalBucket<T>` |
| Track membership for compact externally owned integer IDs | `SwiftSparseSet` |
| Attach values to compact externally owned integer IDs | `SwiftSparseMap<T>` |
| Store densely iterated unique values with hash-backed membership checks | `SwiftPackedSet<T>` |
| Arbitrary, huge, or widely sparse integer IDs | `SwiftHashSet<int>` or `SwiftDictionary<int, T>` |
| Broad-phase spatial queries | `SwiftBVH<TKey>`, `SwiftSpatialHash<TKey>`, `SwiftOctree<TKey>` |

More detail is available in the [library overview](https://github.com/mrdav30/SwiftCollections/blob/main/docs/OVERVIEW.md).

## Quick Examples

### Sparse Membership For External IDs

```csharp
using SwiftCollections;

var activeBodies = new SwiftSparseSet();

activeBodies.Add(42);
activeBodies.Add(128);

if (activeBodies.Contains(42))
{
    activeBodies.Remove(42);
}

foreach (int bodyId in activeBodies)
{
    // Dense iteration over active IDs.
}

var sortedBodyIds = new SwiftList<int>(activeBodies.Count);
activeBodies.CopySortedKeysTo(sortedBodyIds);
```

### Sparse Values For External IDs

```csharp
using System.Numerics;
using SwiftCollections;

var positions = new SwiftSparseMap<Vector3>();

positions[128] = new Vector3(10, 0, 4);

if (positions.TryGetValue(128, out Vector3 position))
{
    positions[128] = position + new Vector3(1, 0, 0);
}

var sortedPositionIds = new SwiftList<int>(positions.Count);
positions.CopySortedKeysTo(sortedPositionIds);
```

### Scratch List Sorting

```csharp
using SwiftCollections;

var keys = new SwiftList<int> { 12, 4, 8 };
keys.SortInPlace();

// keys: 4, 8, 12
```

### Stable Handles

```csharp
using System;
using SwiftCollections;

var bodies = new SwiftGenerationalBucket<string>();

SwiftHandle handle = bodies.Add("player");

if (bodies.TryGet(handle, out string body))
{
    Console.WriteLine(body);
}

bodies.Remove(handle);
```

### Spatial Queries

```csharp
using System.Numerics;
using SwiftCollections;
using SwiftCollections.Query;

var bvh = new SwiftBVH<int>(capacity: 128);

bvh.Insert(
    1,
    new BoundVolume(
        new Vector3(0, 0, 0),
        new Vector3(1, 1, 1)));

var results = new SwiftList<int>();
bvh.Query(
    new BoundVolume(
        new Vector3(-1, -1, -1),
        new Vector3(2, 2, 2)),
    results);
```

## Serialization And Diagnostics

Most core types expose state-backed serialization support. Standard packages include MemoryPack support; lean packages compile the same public collection surface without taking a MemoryPack dependency. `net8.0` builds use System.Text.Json converter implementations where supported, while older targets use compatibility shims.

Diagnostics are opt-in through `SwiftCollections.Diagnostics`. Disabled diagnostic writes are designed to avoid doing formatting work when the requested level is below the channel minimum, and guard helpers use lazy exception-message formatting for condition-gated throws.

## Development

Build the solution:

```bash
dotnet build SwiftCollections.slnx -c Debug
```

Run the unit tests:

```bash
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build
```

Run coverage with the shared runsettings:

```bash
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.Tests/coverlet.runsettings
```

Run benchmarks:

```bash
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- list
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- dictionary
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- query --list flat
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- all --list flat
```

## Compatibility

- Library targets: `netstandard2.1` and `net8.0`
- Test target: `net8.0`
- Benchmark target: `net8`
- CI covers `Release` and `ReleaseLean` on Windows and Linux

## Community And License

Questions and discussion are welcome in the [Discord community](https://discord.gg/mhwK2QFNBA). Bug reports and feature requests should be opened as GitHub issues.

SwiftCollections is licensed under the MIT License. See `LICENSE`, `NOTICE`, and `COPYRIGHT` for license, branding, and authorship details.
