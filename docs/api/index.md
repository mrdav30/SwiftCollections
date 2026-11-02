---
title: SwiftCollections
description: API reference and guides for specialized, low-allocation .NET collections and spatial query structures.
---

<div class="sc-hero">
  <p class="sc-kicker">SPECIALIZED COLLECTIONS FOR .NET HOT PATHS</p>
  <h1>Put your data where the work is.</h1>
  <p>SwiftCollections gives simulations, games, spatial systems, and
  allocation-sensitive tools tighter control over storage, dense iteration,
  handles, pooling, and broad-phase queries.</p>
  <div class="sc-actions">
    <a href="xref:SwiftCollections">Browse the API</a>
    <a href="guides/OVERVIEW.md">Choose a collection</a>
  </div>
</div>

## Start with the workload

<div class="sc-card-grid">
  <div class="sc-card">
    <h3><a href="xref:SwiftCollections.SwiftSparseSet">Compact integer IDs</a></h3>
    <p>Use sparse sets and maps for constant-time lookup plus dense iteration
    when your system already owns small non-negative IDs.</p>
  </div>
  <div class="sc-card">
    <h3><a href="xref:SwiftCollections.SwiftGenerationalBucket`1">Stable handles</a></h3>
    <p>Use buckets for reusable slots, or generation-checked buckets when stale
    references must stop resolving after a slot is reused.</p>
  </div>
  <div class="sc-card">
    <h3><a href="xref:SwiftCollections.Query.SwiftBVH`1">Spatial queries</a></h3>
    <p>Choose BVH, spatial hash, or octree structures for broad-phase searches
    over floating-point or FixedMathSharp-backed bounds.</p>
  </div>
</div>

## Pick a package family

<div class="sc-card-grid">
  <div class="sc-card">
    <h3>Standard</h3>
    <p><code>SwiftCollections</code> includes the core collections and
    MemoryPack support.</p>
  </div>
  <div class="sc-card">
    <h3>Lean</h3>
    <p><code>SwiftCollections.Lean</code> keeps the collection APIs without the
    MemoryPack runtime dependency.</p>
  </div>
  <div class="sc-card">
    <h3><a href="xref:SwiftCollections.Query.FixedBoundVolume">FixedMathSharp</a></h3>
    <p>Add the matching <code>SwiftCollections.FixedMathSharp</code> companion
    for deterministic fixed-point query volumes.</p>
  </div>
  <div class="sc-card">
    <h3><a href="https://github.com/mrdav30/SwiftCollections-Unity">Unity packages</a></h3>
    <p>Use the maintained Unity host for serializable authoring adapters,
    <code>Bounds</code> conversion, GameObject pooling, and samples.</p>
  </div>
</div>

## Guides

- [Get started](guides/getting-started.md) with packages and a first container.
- [Choose collections and clearing policies](guides/collections.md) for the
  ownership model.
- [Query spatial bounds](guides/spatial-queries.md), including fixed-point
  volumes.
- [Manage rentals and notifications](guides/pools-and-observables.md) at host
  boundaries.
- [Serialize state and configure diagnostics](guides/serialization-and-diagnostics.md).

## Part of the LSF ecosystem

SwiftCollections provides specialized collections and spatial indexes for
libraries, games, simulations, and tools throughout the Lockstep Simulation
Framework ecosystem:

- [Chronicler](https://github.com/mrdav30/Chronicler)
- [FixedMathSharp](https://github.com/mrdav30/FixedMathSharp)
- [GridForge](https://github.com/mrdav30/GridForge)
- [Gravitas](https://github.com/mrdav30/Gravitas)
- [Trailblazer](https://github.com/mrdav30/Trailblazer)

## Resources

- [Library overview and container guide](guides/OVERVIEW.md)
- [Source, issues, and releases](https://github.com/mrdav30/SwiftCollections)
- [Unity packages](https://github.com/mrdav30/SwiftCollections-Unity)
- [Test coverage](https://mrdav30.github.io/SwiftCollections/coverage/)

The API reference is generated from the source XML documentation. The overview
explains tradeoffs, ID density constraints, serialization state, spatial query
semantics, and the development workflow.
