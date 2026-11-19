# Legacy comparer-persistence inputs

`legacy-v0.json` freezes 13 input families, each as direct and nested JSON and
MemoryPack: 52 exact payloads. It records the producing source revision, loaded
assembly hashes, configuration, runtime, serializer version, sizes, SHA-256
digests, source comparison rule and observed legacy result. Binary fields are
base64; JSON strings contain the exact original UTF-8 text without a BOM.

`v1-framing.json` is a separate Phase 0 protocol specimen. Its three-member
envelope was checked against actual legacy direct/nested readers, which reject
it in both formats. It is not output from an implemented production v1 writer
or a proof of complete comparer reconstruction.

These are compatibility inputs for the comparer-persistence plan, not examples
of safe custom-comparer restoration. Current default restore can change equality
or leave sorted data incompatible with its installed comparer. Future strict
readers must reject metadata-free owners; explicit migration must select the
comparison rule before reconstruction. Raw DTOs and comparerless controls retain
their existing contracts. Null owners remain distinct from empty owners.

## Input schema and rules

The direct type and source/observed values are recorded per case. Descending
integer order uses `(x, y) => y.CompareTo(x)`. Identity cases use reference
equality and a constant zero hash on separately allocated equal-text strings;
they do not encode object addresses. The seeded dictionary uses the public Swift
string comparer with signed seed `-1234567`. Its source/restored sample hashes
are in the case record. Hash-table order and bytes describe this producing
snapshot, not canonical iteration across runtimes or insertion histories.

Two fixture-only schemas are needed for custom/nested inputs. Preserve member
order for MemoryPack; both are ordinary default object generation:

```csharp
[MemoryPackable]
public partial class OrderToken
{
    public int Key { get; set; }
    public string Name { get; set; } = "";
}

[MemoryPackable]
public partial class FixtureEnvelope<T>
{
    public string Label { get; set; } = "";
    public T Value { get; set; } = default!;
}
```

`OrderToken` deliberately has no default comparison. Its source comparer orders
by descending `Key`. `FixtureEnvelope<T>.Label` is the literal `fixed-envelope`
and its `Value` is the direct object. These helper CLR names are not encoded as type
names in either payload. See `payloads.json`/`payloads.nestedJson` in each case
for complete values, including token names, pair values and sequence order.

## Reading and checking an input

For example, in a .NET console referencing the producing core and MemoryPack
1.21.4, deserialize the `sorted-descending` case as follows:

```csharp
using var fixture = JsonDocument.Parse(File.ReadAllText(fixturePath));
var item = fixture.RootElement.GetProperty("cases").EnumerateArray()
    .Single(x => x.GetProperty("id").GetString() == "sorted-descending");
var payloads = item.GetProperty("payloads");
byte[] binary = Convert.FromBase64String(
    payloads.GetProperty("memoryPackBase64").GetString()!);
var fromJson = JsonSerializer.Deserialize<SwiftSortedList<int>>(
    payloads.GetProperty("json").GetString()!);
var fromBinary = MemoryPackSerializer.Deserialize<SwiftSortedList<int>>(binary);
// Legacy readers copy [13, 0, -7] and select Comparer<int>.Default.
// Inspect storage here; do not use binary lookup until ordering is established.
```

Use explicit `System`, `System.IO`, `System.Linq`, `System.Text.Json`,
`System.Collections.Generic`, `SwiftCollections` and `MemoryPack` imports.
Compare decoded binary SHA-256 with the corresponding digest. Nested payloads
use `FixtureEnvelope<T>`. Every direct and nested legacy load was checked during
capture before these inputs were frozen. The old nested schema is test data,
not a new supported production envelope API.

When adding migration regressions, exercise production readers against these
fixed inputs. Do not regenerate expected bytes to make a reader pass. Any new
capture needs its own producer identity and explicit compatibility disposition.
Lean exercises JSON only; MemoryPack inputs require a compatible standard
dependency graph. A modern host loading the standard-2.1 collection DLL against
modern MemoryPack interfaces is not such a graph.
