SwiftCollections
==============

![SwiftCollections Icon](https://raw.githubusercontent.com/mrdav30/SwiftCollections/main/icon.png)

**SwiftCollections** is a high-performance, memory-efficient library of collections designed for game development, simulations, and other performance-critical applications.

---

## 🛠️ Key Features

- **Optimized for Performance**: Designed for low time complexity and minimal memory allocations.
- **Versatile Use Cases**: Suitable for data structures in 3D environments and complex spatial queries.
- **Unity-Compatible**: Fully functional within Unity's ecosystem.
- **Customizable**: Use pre-built implementations or extend functionality with custom interfaces.

---

## Dependencies

- Requires [FixedMathSharp](https://github.com/mrdav30/FixedMathSharp).

---

## 🚀 Installation

Clone the repository and add it to your project:

### Non-Unity Projects

1. **Install via NuGet**:
   - Add SwiftCollections to your project using the following command:
   
     ```bash
     dotnet add package SwiftCollections
     ```

2. **Or Download/Clone**:
   - Clone the repository or download the source code.
   
     ```bash
     git clone https://github.com/mrdav30/SwiftCollections.git
     ```

3. **Add to Project**:
   - Include the `SwiftCollections` project or its DLLs in your build process.

### Unity

SwiftCollections is now maintained as a separate Unity package.For Unity-specific implementations, refer to:

🔗 [SwiftCollection-Unity Repository](https://github.com/mrdav30/SwiftCollections-Unity).

---

## 📦 Library Overview

### Core Data Structures

- **SwiftDictionary**: A high-performance dictionary optimized for O(1) operations and minimal memory usage.
- **SwiftBiDictionary**: A bidirectional dictionary for efficient forward and reverse lookups in O(1).
- **SwiftHashSet**: An optimized set for unique values with fast operations.
- **SwiftBucket**: High-performance collection for O(1) addition and removal with stable indexing.
- **SwiftQueue**: Circular-buffer-based queue for ultra-low-latency operations.
- **SwiftList**: A dynamic list optimized for speed-critical applications.
- **SwiftSortedList**: Dynamically sorted collection with O(log n) operations.
- **SwiftStack**: Fast array-based stack with O(1) operations.
- **SwiftArray2D / SwiftArray3D**: Efficient, flat-mapped arrays for 2D and 3D data.
- **SwiftBVH**: A Bounding Volume Hierarchy optimized for spatial queries.

### Pools

- **SwiftObjectPool**: Thread-safe generic object pooling for improved memory usage and performance.
- **SwiftArrayPool**: Array-specific pool for efficient reuse of arrays.
- **SwiftCollectionPool**: Pool for reusable collection instances (e.g., List, HashSet).

### Spatial Data Structures

- **SwiftBVH**: Bounding Volume Hierarchy for efficient spatial queries.

### Observable Collections

- **ObservableArray / ObservableSwiftList / ObservableDictionary**: Reactive, observable collections with property and collection change notifications. 


## 📖 Usage Examples

### SwiftBVH for Spatial Queries

```csharp
var bvh = new SwiftBVH<int>(100);
var volume = new BoundingVolume(new Vector3(0, 0, 0), new Vector3(1, 1, 1));
bvh.Insert(1, volume);

var results = new List<int>();
bvh.Query(new BoundingVolume(new Vector3(0, 0, 0), new Vector3(2, 2, 2)), results);
Console.WriteLine(results.Count); // Output: 1
```

### SwiftArray2D

```csharp
var array2D = new Array2D<int>(10, 10);
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

## 🧪 Testing and Validation

SwiftCollections includes a comprehensive suite of xUnit tests for validation.

### Running Unit Tests

To execute all unit tests, use the following command:

```bash
dotnet test -c debug
```

### Running Benchmarks

The library includes benchmarks to measure the performance of its collections. Benchmarks are powered by BenchmarkDotNet and can be run as follows:

1. Open the SwiftCollections.Benchmarks project.

2. Modify the Program.cs file to select the desired benchmark. Uncomment the relevant BenchmarkRunner lines.
	- Example Program.cs setup:

```csharp
using BenchmarkDotNet.Running;

namespace SwiftCollections.Benchmarks
{
    class Program
    {
        static void Main(string[] args)
        {
            // Uncomment the benchmark you want to run:
            // var ListIntegerBenchmarksSummary = BenchmarkRunner.Run<ListIntegerBenchmarks>();
            var StackIntegerBenchmarksSummary = BenchmarkRunner.Run<StackIntegerBenchmarks>();
            // var DictionaryStringBenchmarksSummary = BenchmarkRunner.Run<DictionaryStringBenchmarks>();
        }
    }
}
```

3. Build and run the project. The results will be displayed in the console and saved to a results file for further analysis.

## 🛠️ Compatibility

- **.NET Framework** 4.7.2+
- **Unity 2020+** (via - [SwiftCollection-Unity](https://github.com/mrdav30/SwiftCollections-Unity).)
- **Cross-Platform Support** (Windows, Linux, macOS)
- **Query Collections Precision**: Supports both System.Numerics and FixedMathSharp.

## 📄 License

This project is licensed under the MIT License - see the `LICENSE` file
for details.

---

## 👥 Contributors

- **mrdav30** - Lead Developer
- Contributions are welcome! Feel free to submit pull requests or report issues.

---

## 📧 Contact

For questions or support, reach out to **mrdav30** via GitHub or open an issue in the repository.

---