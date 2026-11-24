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
FixedMathSharp dependencies. The current companion requires core `9.0.0` or
newer and FixedMathSharp `9.0.0`; see the
[migration guide](https://github.com/mrdav30/SwiftCollections/blob/main/docs/MIGRATION.md#fixedmathsharp-v9-dependency-alignment-unreleased)
when updating an existing dependency graph.

## 2. Use generation-checked handles

The map owns slot allocation. Removal invalidates the current ticket; ordinary
slot reuse advances its generation:

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

`SwiftTicketMap<T>` uses finite generations that can wrap. For packed live-value
iteration and retirement of exhausted slots instead of wrap, choose
`SwiftDenseTicketMap<T>`. Both tickets remain scoped to their issuing owner;
see [identity ownership](collections.md#who-issues-the-identity) before retaining
them across restores or mixing owners.

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
