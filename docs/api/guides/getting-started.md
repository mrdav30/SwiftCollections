---
title: Getting started
---

# Getting started

SwiftCollections is a set of specialized containers for workloads that benefit
from explicit storage, iteration, and ownership choices. See the
[collection guide](collections.md) before replacing a general-purpose BCL type.

## 1. Choose a package family

```bash
dotnet add package SwiftCollections
```

| Need                                             | Package                                |
| ------------------------------------------------ | -------------------------------------- |
| Core collections with MemoryPack                 | `SwiftCollections`                     |
| Core collections without the MemoryPack runtime  | `SwiftCollections.Lean`                |
| Fixed-point spatial queries                      | `SwiftCollections.FixedMathSharp`      |
| Fixed-point queries on the Lean dependency graph | `SwiftCollections.FixedMathSharp.Lean` |

All runtime packages target `netstandard2.1` and `net10.0`. Pair standard
companions with standard dependencies, and Lean companions with Lean
dependencies. The companion brings the matching core collection and
FixedMathSharp dependencies.

## 2. Use generation-checked handles

The bucket owns slot allocation. A handle stops resolving when its entry is
removed; reusing that slot does not make the old handle valid again:

```csharp
using System;
using SwiftCollections;

var actors = new SwiftTicketMap<string>();
SwiftTicket player = actors.Add("player");

if (actors.TryGet(player, out string actor))
    Console.WriteLine(actor);

actors.Remove(player);
bool staleHandleStillResolves = actors.TryGet(player, out _); // false
```

For externally owned compact integer IDs, use `SwiftSparseSet` or
`SwiftSparseMap<T>` instead. Their lookup storage scales with the highest ID;
large sparse IDs are better served by a hash table.

## 3. Choose the next guide

- [Collections and clearing](collections.md): container tradeoffs, retained
  references, and ordering.
- [Spatial queries](spatial-queries.md): broad-phase structures and the
  fixed-point companion.
- [Pools and observable collections](pools-and-observables.md): rentals, return
  boundaries, and notifications.
- [Serialization and diagnostics](serialization-and-diagnostics.md): explicit
  state and host-owned output.
- [Contributing](https://github.com/mrdav30/SwiftCollections/blob/main/CONTRIBUTING.md):
  source builds, tests, coverage, cleanup, and releases.

Unity integration remains in the separate
[SwiftCollections-Unity repository](https://github.com/mrdav30/SwiftCollections-Unity).
