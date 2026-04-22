# SwiftCollections

![SwiftCollections Icon](https://raw.githubusercontent.com/mrdav30/SwiftCollections/main/icon.png)

[![.NET CI](https://github.com/mrdav30/SwiftCollections/actions/workflows/dotnet.yml/badge.svg)](https://github.com/mrdav30/SwiftCollections/actions/workflows/dotnet.yml)
[![Coverage](https://img.shields.io/badge/dynamic/json?url=https%3A%2F%2Fmrdav30.github.io%2FSwiftCollections%2FSummary.json&query=%24.summary.linecoverage&suffix=%25&label=coverage&color=brightgreen)](https://mrdav30.github.io/SwiftCollections/)
[![NuGet](https://img.shields.io/nuget/v/SwiftCollections.svg)](https://www.nuget.org/packages/SwiftCollections)
[![NuGet Downloads](https://img.shields.io/nuget/dt/SwiftCollections.svg)](https://www.nuget.org/packages/SwiftCollections)
[![License](https://img.shields.io/github/license/mrdav30/SwiftCollections.svg)](https://github.com/mrdav30/SwiftCollections/blob/main/LICENSE)
[![Frameworks](https://img.shields.io/badge/frameworks-netstandard2.1%20%7C%20net8.0-512BD4.svg)](https://github.com/mrdav30/SwiftCollections)

**SwiftCollections** is a high-performance collection library for performance-sensitive .NET workloads, including game systems, simulations, and spatial queries.

---

## 🛠️ Key Features

- **Optimized for Performance**: Designed for low time complexity and minimal memory allocations.
- **Framework Agnostic** : Works with .NET, Unity, and other game engines.
- **Full Serialization Support**: Out-of-the-box round-trip serialization via MemoryPack across most core collections, with System.Text.Json constructor support on .NET 8+.
- **Fast core collections**: `SwiftDictionary`, `SwiftHashSet`, `SwiftList`, `SwiftQueue`, `SwiftStack`, `SwiftSortedList`
- **Specialized containers**: `SwiftBucket`, `SwiftGenerationalBucket`, `SwiftPackedSet`, `SwiftSparseMap`, `SwiftBiDictionary`
- **Flat 2D/3D storage**: `SwiftArray2D`, `SwiftArray3D`, `SwiftBoolArray2D`, `SwiftShortArray2D`
- **Pools**: `SwiftObjectPool`, `SwiftArrayPool`, `SwiftCollectionPool`, and typed pool helpers
- **Observable collections** for change-tracking scenarios
- **Spatial queries** via typed `SwiftBVH<TKey, TVolume>`, `SwiftSpatialHash<TKey, TVolume>`, and `SwiftOctree<TKey, TVolume>` plus default numerics wrappers
- **Lightweight diagnostics** via `SwiftCollections.Diagnostics` for opt-in low-level log/event routing

---

## 🚀 Installation

### NuGet

```bash
dotnet add package SwiftCollections
```

### NuGet (Fixed-Point Companion)

```bash
dotnet add package SwiftCollections.FixedMathSharp
```

### Source

```bash
git clone https://github.com/mrdav30/SwiftCollections.git
```

Then reference `src/SwiftCollections/SwiftCollections.csproj` or build the package locally.

### Unity

Unity support is maintained separately:

[SwiftCollections-Unity](https://github.com/mrdav30/SwiftCollections-Unity)

---

## 🧩 Dependencies

- Core package dependency: [MemoryPack](https://github.com/Cysharp/MemoryPack)
- Optional fixed-point companion: [FixedMathSharp](https://github.com/mrdav30/FixedMathSharp) via `SwiftCollections.FixedMathSharp`

---

## 📦 Library Overview

### Core Data Structures

- **SwiftDictionary**: A high-performance dictionary optimized for O(1) operations and minimal memory usage.
- **SwiftBiDictionary**: A bidirectional dictionary for efficient forward and reverse lookups in O(1).
- **SwiftHashSet**: An optimized set for unique values with fast operations.
- **SwiftBucket**: High-performance collection for O(1) addition and removal with stable indexing.
- **SwiftGenerationalBucket**: A bucket variant that tracks generations to prevent stale references.
- **SwiftPackedSet**: A compact set implementation for dense integer keys.
- **SwiftSparseMap**: A memory-efficient map for sparse key distributions.
- **SwiftQueue**: Circular-buffer-based queue for ultra-low-latency operations.
- **SwiftList**: A dynamic list optimized for speed-critical applications.
- **SwiftSortedList**: Dynamically sorted collection with O(log n) operations.
- **SwiftStack**: Fast array-based stack with O(1) operations.
- **SwiftArray2D / SwiftArray3D**: Efficient, flat-mapped arrays for 2D and 3D data.
- **SwiftBVH**: Bounding Volume Hierarchy for broad-phase spatial queries.
- **SwiftSpatialHash**: Spatial hash for high-churn, uniform-size, and sparse huge-world scenes.
- **SwiftOctree**: Hierarchical octree for dynamic scenes with uneven density.

`SwiftDictionary<TKey, TValue>` and `SwiftHashSet<T>` use deterministic default comparers for `string` keys when no comparer is supplied. `object` keys also get a SwiftCollections default comparer that hashes strings deterministically, but non-string object-key determinism still depends on the underlying key type. Custom comparers are still supported.

### Pools

- **SwiftObjectPool**: Thread-safe generic object pooling for improved memory usage and performance.
- **SwiftArrayPool**: Array-specific pool for efficient reuse of arrays.
- **SwiftCollectionPool**: Pool for reusable collection instances (e.g., List, HashSet).
- **Default Collection Pools**: Ready-to-use pools are available for `SwiftList`, `SwiftQueue`, `SwiftHashSet`, `SwiftDictionary`, `SwiftStack`, `SwiftPackedSet`, and `SwiftSparseMap`.

### Spatial Data Structures

- **SwiftBVH**: Bounding Volume Hierarchy for broad-phase queries with mixed or extreme object-size variance.
- **SwiftSpatialHash**: Spatial hash for sparse huge-world needle queries and uniform-size high-churn workloads.
- **SwiftOctree**: Hierarchical octree for dynamic scenes, uneven density, and repeated region queries.

Use them by workload:

- **SwiftBVH** is the best fit for scenes with mixed or extreme object-size variance (e.g. tiny units alongside large terrain pieces), large churning objects, and general broad-phase intersection queries over heterogeneous populations. It is not thread-safe; synchronize access externally if needed. Avoid it for dense same-size clustered scenes and for sparse huge-world needle (tiny query window) lookups.
- **SwiftSpatialHash** is the best fit for sparse, huge-world scenes where small query windows rarely overlap many cells (O(1) bucket lookup dominates), and for high-frequency updates with mostly uniform-size objects. Performance degrades when object sizes vary widely, since a fixed cell size becomes either too coarse or too fine.
- **SwiftOctree** is the strongest all-around performer for dynamic scenes with uniform or small objects, mixed broad-phase, and repeated regional queries over uneven distributions. Prefer it when most objects are similar in size or when queries target specific spatial sub-regions repeatedly.

### Observable Collections

- **SwiftObservableArray / SwiftObservableList / SwiftObservableDictionary**: Reactive, observable collections with property and collection change notifications.

### Diagnostics

- **DiagnosticChannel / DiagnosticEvent / DiagnosticLevel**: Lightweight diagnostics primitives for routing informational, warning, or error events without coupling the library to a higher-level logging framework.
- **SwiftCollectionDiagnostics.Shared**: Ready-to-use shared channel for library-wide diagnostics.

Diagnostics are opt-in and disabled by default until you configure a minimum level and sink.

## 📖 Usage Examples

### SwiftBVH for Spatial Queries

```csharp
var bvh = new SwiftBVH<int>(100);
var volume = new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
bvh.Insert(1, volume);

var results = new SwiftList<int>();
bvh.Query(new BoundVolume(new Vector3(0, 0, 0), new Vector3(2, 2, 2)), results);
Console.WriteLine(results.Count); // Output: 1
```

### SwiftBVH with Custom Typed Volumes

```csharp
var typedBvh = new SwiftBVH<int, BoundVolume>(100);
typedBvh.Insert(1, new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1)));
```

### SwiftSpatialHash for Broad-Phase Cell Queries

```csharp
var spatialHash = new SwiftSpatialHash<int>(64, 2f);
spatialHash.Insert(1, new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1)));

var nearby = new List<int>();
spatialHash.QueryNeighborhood(
    new BoundVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1)),
    nearby);
```

### SwiftOctree for Hierarchical Region Queries

```csharp
var worldBounds = new BoundVolume(new Vector3(0, 0, 0), new Vector3(64, 64, 64));
var octree = new SwiftOctree<int>(
    worldBounds,
    new SwiftOctreeOptions(maxDepth: 6, nodeCapacity: 8),
    minNodeSize: 1f);

octree.Insert(1, new BoundVolume(new Vector3(2, 2, 2), new Vector3(4, 4, 4)));

var visible = new List<int>();
octree.Query(new BoundVolume(new Vector3(0, 0, 0), new Vector3(8, 8, 8)), visible);
```

### Fixed-Point SwiftBVH (Companion Package)

```csharp
var fixedBvh = new SwiftFixedBVH<int>(100);
fixedBvh.Insert(1, new FixedBoundVolume(new Vector3d(0, 0, 0), new Vector3d(1, 1, 1)));
```

### SwiftArray2D

```csharp
var array2D = new SwiftArray2D<int>(10, 10);
array2D[3, 4] = 42;
Console.WriteLine(array2D[3, 4]); // Output: 42
```

### SwiftQueue

```csharp
var queue = new SwiftQueue<int>(10);
queue.Enqueue(5);
Console.WriteLine(queue.Dequeue()); // Output: 5
```

### Populating Arrays

```csharp
var array = new int[10].Populate(() => new Random().Next(1, 100));
```

### Diagnostic Example

```csharp
using System;
using SwiftCollections.Diagnostics;

DiagnosticChannel diagnostics = SwiftCollectionDiagnostics.Shared;
diagnostics.MinimumLevel = DiagnosticLevel.Warning;
diagnostics.Sink = static (in DiagnosticEvent diagnostic) =>
{
    Console.WriteLine($"[{diagnostic.Channel}] {diagnostic.Level}: {diagnostic.Message} ({diagnostic.Source})");
};

diagnostics.Write(DiagnosticLevel.Info, "Skipped because the minimum level is Warning.", "Bootstrap");
diagnostics.Write(DiagnosticLevel.Error, "Pool allocation failed.", "Bootstrap");
```

## 🧪 Development

Build the solution:

```bash
dotnet build SwiftCollections.slnx -c Debug
```

Run the unit tests:

```bash
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build
```

Run benchmarks:

```bash
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8
```

Useful benchmark runner commands:

```bash
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- list
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- dictionary
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- query --list flat
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- hashset --filter "*Contains*"
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- all --list flat
```

With no extra arguments, BenchmarkDotNet's default switcher behavior is used. Leading non-option arguments are treated as benchmark selection aliases, and any remaining arguments are forwarded to BenchmarkDotNet.

## 🛠️ Compatibility

- `netstandard2.1`
- `net8.0`
- Windows, Linux, and macOS

Fixed-point BVH support is provided by the separate `SwiftCollections.FixedMathSharp` companion package.

---

## 🤝 Contributing

We welcome contributions! Please see our [CONTRIBUTING](https://github.com/mrdav30/SwiftCollections/blob/main/CONTRIBUTING.md) guide for details on how to propose changes, report issues, and interact with the community.

---

## 👥 Contributors

- **mrdav30** - Lead Developer
- Contributions are welcome! Feel free to submit pull requests or report issues.

---

## 💬 Community & Support

For questions, discussions, or general support, join the official Discord community:

👉 **[Join the Discord Server](https://discord.gg/mhwK2QFNBA)**

For bug reports or feature requests, please open an issue in this repository.

We welcome feedback, contributors, and community discussion across all projects.

---

## 📄 License

This project is licensed under the MIT License.

See the following files for details:

- LICENSE – standard MIT license
- NOTICE – additional terms regarding project branding and redistribution
- COPYRIGHT – authorship information
