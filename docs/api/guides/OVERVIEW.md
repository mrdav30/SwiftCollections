---
title: SwiftCollections overview
---

# SwiftCollections Overview

SwiftCollections provides specialized .NET collections for hot paths where
storage layout, allocation behavior, deterministic hashing, dense iteration,
pooling, or spatial query performance matter more than broad general-purpose
ergonomics.

Use the standard .NET collections first when the workload is ordinary. Reach for
SwiftCollections when the code is already performance-sensitive, when a
benchmark points at collection cost, or when the data model naturally fits one
of the specialized containers below.

Browse the [documentation site](https://mrdav30.github.io/SwiftCollections/),
[core API reference](https://mrdav30.github.io/SwiftCollections/api/SwiftCollections.html),
[spatial query API, including FixedMathSharp integrations](https://mrdav30.github.io/SwiftCollections/api/SwiftCollections.Query.html),
or [coverage report](https://mrdav30.github.io/SwiftCollections/coverage/).

## Design Goals

- Keep common operations low time complexity.
- Prefer contiguous storage and dense iteration where it improves cache
  behavior.
- Avoid hidden allocations in simulation and query hot paths.
- Keep APIs framework-agnostic and usable from .NET, game engines, tools, and
  servers.
- Preserve deterministic-friendly behavior where the collection owns hashing or
  ordering choices.
- Keep serialization explicit through state-backed types and package variants.

## Choose a guide

| Task                                                                                      | Guide                                                             |
| ----------------------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| Install a package and run a first example                                                 | [Getting started](getting-started.md)                             |
| Choose a container, distinguish slots/handles/external IDs, and clear retained references | [Collections and clearing](collections.md)                        |
| Index and query spatial bounds                                                            | [Spatial queries](spatial-queries.md)                             |
| Rent collections and manage host notifications                                            | [Pools and observable collections](pools-and-observables.md)      |
| Save explicit state and configure diagnostic output                                       | [Serialization and diagnostics](serialization-and-diagnostics.md) |

## Clearing And Retained References

Use `Clear(clearRetainedReferences: true)` when an ownership boundary must
release references retained outside the live range while preserving capacity.
See
[Clearing and retained references](collections.md#clearing-and-retained-references)
for the differences between normal clearing, full reference cleanup, and
`FastClear()`.

## Development And Verification

See
[Contributing](https://github.com/mrdav30/SwiftCollections/blob/main/CONTRIBUTING.md)
for builds, tests, per-assembly coverage, documentation generation, formatting,
benchmarks, and package-family releases. Generated coverage describes the tested
commit; use the current reports when assessing a release candidate.
