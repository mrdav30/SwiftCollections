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

Sorted-list, dictionary, hash-set, bidictionary and observable-dictionary JSON
and standard MemoryPack preserve supported comparison rules. Register custom
factories and use version-one owner records; old state-only owner records are
rejected. See
[comparer persistence](serialization-and-diagnostics.md#preserve-hash-equality-during-serialization)
and
[sorted ordering](serialization-and-diagnostics.md#preserve-sorted-list-ordering-during-serialization).

## Choose a guide

| Task                                                                                      | Guide                                                             |
| ----------------------------------------------------------------------------------------- | ----------------------------------------------------------------- |
| Install a package and run a first example                                                 | [Getting started](getting-started.md)                             |
| Choose a container, distinguish slots/handles/external IDs, and clear retained references | [Collections and clearing](collections.md)                        |
| Index and query spatial bounds                                                            | [Spatial queries](spatial-queries.md)                             |
| Rent collections and manage host notifications                                            | [Pools and observable collections](pools-and-observables.md)      |
| Save explicit state and configure diagnostic output                                       | [Serialization and diagnostics](serialization-and-diagnostics.md) |

For reusable registrations or agent records, start with
[identity ownership](collections.md#who-issues-the-identity) and the
[delayed-work and dense-update examples](collections.md#delayed-work-and-dense-agent-updates).
The guide distinguishes slots, generation-checked tickets, dense ticket owners,
external IDs and value-based membership, including storage and borrowing costs.
`SwiftTicketStore<T>` attaches packed values to external tickets with exact
generation checks; its local membership does not establish issuer liveness. See
[optional components under one issuer](collections.md#optional-components-under-one-issuer)
for the paired-store example and teardown boundary.

For planar broad-phase queries, use `SwiftBVH2D<TKey>`,
`SwiftSpatialHash2D<TKey>`, or `SwiftQuadtree<TKey>` with `BoundVolume2D`. The
companion offers `SwiftFixedBVH2D<TKey>`, `SwiftFixedSpatialHash2D<TKey>`, and
`SwiftFixedQuadtree<TKey>` with `FixedBoundVolume2D` for deterministic
fixed-point inputs. See
[2D bounds and BVH queries](spatial-queries.md#2d-bounds-and-bvh-queries),
[2D spatial hash queries](spatial-queries.md#2d-spatial-hash-queries), and
[2D quadtree queries](spatial-queries.md#2d-quadtree-queries) for ownership,
buffer, mapping and ordering contracts.

## Clearing And Retained References

Use `Clear(clearRetainedReferences: true)` when an ownership boundary must
release references retained outside the live range while preserving capacity.
See
[Clearing and retained references](collections.md#clearing-and-retained-references)
for the differences between normal clearing, full reference cleanup, and
`FastClear()`. `SwiftDenseTicketMap<T>` and `SwiftTicketStore<T>` always release
discarded references through parameterless Clear; they have no retained-reference
clearing overload.

## Development And Verification

See
[Contributing](https://github.com/mrdav30/SwiftCollections/blob/main/CONTRIBUTING.md)
for builds, tests, per-assembly coverage, documentation generation, formatting,
benchmarks, and package-family releases. Generated coverage describes the tested
commit; use the current reports when assessing a release candidate.
