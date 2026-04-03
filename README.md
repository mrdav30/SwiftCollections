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
- **Spatial queries** via `SwiftBVH` with both `System.Numerics` and `FixedMathSharp` bounds

---

## 🚀 Installation

### NuGet

```bash
dotnet add package SwiftCollections
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

- [FixedMathSharp](https://github.com/mrdav30/FixedMathSharp)

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
- **SwiftBVH**: A Bounding Volume Hierarchy optimized for spatial queries.

`SwiftDictionary<TKey, TValue>` and `SwiftHashSet<T>` use deterministic default comparers for `string` keys when no comparer is supplied. `object` keys also get a SwiftCollections default comparer that hashes strings deterministically, but non-string object-key determinism still depends on the underlying key type. Custom comparers are still supported.

### Pools

- **SwiftObjectPool**: Thread-safe generic object pooling for improved memory usage and performance.
- **SwiftArrayPool**: Array-specific pool for efficient reuse of arrays.
- **SwiftCollectionPool**: Pool for reusable collection instances (e.g., List, HashSet).
- **Default Collection Pools**: Ready-to-use pools are available for `SwiftList`, `SwiftQueue`, `SwiftHashSet`, `SwiftDictionary`, `SwiftStack`, `SwiftPackedSet`, and `SwiftSparseMap`.

### Spatial Data Structures

- **SwiftBVH**: Bounding Volume Hierarchy for efficient spatial queries.

### Observable Collections

- **SwiftObservableArray / SwiftObservableList / SwiftObservableDictionary**: Reactive, observable collections with property and collection change notifications.

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

## 🧪 Development

Build the solution:

```bash
dotnet build SwiftCollections.sln -c Debug
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
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- hashset --filter "*Contains*"
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- all --list flat
```

With no extra arguments, BenchmarkDotNet's default switcher behavior is used. Leading non-option arguments are treated as benchmark selection aliases, and any remaining arguments are forwarded to BenchmarkDotNet.

## 🛠️ Compatibility

- `netstandard2.1`
- `net8.0`
- Windows, Linux, and macOS

`FixedMathSharp` is used for the fixed-point BVH path.

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
