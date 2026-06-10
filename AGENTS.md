# SwiftCollections Contributor Guide

## Purpose

SwiftCollections is a framework-agnostic .NET collection library for performance-sensitive code: simulations, games, spatial queries, deterministic runtimes, pooling-heavy systems, and tooling that needs predictable data-structure behavior.

The standard .NET collections remain the right default for ordinary application code. This repository exists for the places where hot-path cost, storage layout, dense iteration, deterministic hashing, pooling, or specialized integer-ID ownership justify a sharper collection.

Current priorities:

1. Prefer optimized, low time-complexity implementations. No band-aid solutions.
2. Preserve public API behavior and exception contracts unless a breaking change is intentional.
3. Keep hot paths allocation-conscious and benchmarkable.
4. Keep the core library engine-agnostic; Unity-specific packaging belongs in the separate Unity repository.
5. Maintain high test coverage, especially for new public APIs, serialization state, and edge-case branches.
6. Keep standard and lean package variants aligned.

## Start Here

Read these before making non-trivial changes:

1. `README.md` for the public front door and package orientation.
2. `docs/OVERVIEW.md` for the collection, spatial query, serialization, and diagnostics map.
3. `SwiftCollections.slnx` and the relevant `*.csproj` files. They are the source of truth for compiled projects, package shape, and target frameworks.
4. The source folder under `src/SwiftCollections` or `src/SwiftCollections.FixedMathSharp` that owns the change.
5. The matching tests under `tests/SwiftCollections.Tests`.
6. Benchmarks under `tests/SwiftCollections.Benchmarks` when a change touches hot-path behavior or a performance claim.
7. `docs/complexity-exceptions.md` before refactoring high-complexity methods or adding a new intentional exception.

## Source Of Truth

When docs, workflows, and project files disagree, trust the project files and source code first, then update the docs.

Current compiled projects:

| Project | Path | Target Frameworks |
| --- | --- | --- |
| Core library | `src/SwiftCollections/SwiftCollections.csproj` | `netstandard2.1;net8.0` |
| FixedMathSharp companion | `src/SwiftCollections.FixedMathSharp/SwiftCollections.FixedMathSharp.csproj` | `netstandard2.1;net8.0` |
| Core tests | `tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj` | `net8.0` |
| Benchmarks | `tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj` | `net8` |

Keep these aligned whenever behavior, public API, package variants, or workflow expectations change:

- `README.md`
- `docs/OVERVIEW.md`
- `docs/complexity-exceptions.md`
- relevant tests and benchmarks
- `.github/workflows/build-and-test.yml`
- `.github/workflows/coverage.yml`
- `.github/workflows/publish-nuget.yml`

## Repository Map

| Path | Purpose | Notes |
| --- | --- | --- |
| `src/SwiftCollections/Collection` | Core collection types | Includes dictionary, hash set, list, queue, stack, bucket, packed set, sparse set, and sparse map. |
| `src/SwiftCollections/Dimension` | Flat 2D/3D arrays and typed array helpers | Preserve index math and bounds behavior. |
| `src/SwiftCollections/EqualityComparer` | SwiftCollections comparers | String default comparers are deterministic-oriented. |
| `src/SwiftCollections/Observable` | Observable collection variants | Prefer for tooling/host-facing notifications, not simulation hot paths without evidence. |
| `src/SwiftCollections/Pool` | Object, array, and collection pools | Watch ownership and dispose/release contracts. |
| `src/SwiftCollections/Query` | BVH, spatial hash, octree, bounds, and query helpers | Compiled in the core package. Keep mutable indexes single-owner unless synchronized externally. |
| `src/SwiftCollections/Serialization` | State structs and JSON/state converter support | Build after changing state shape or serialization constructors. |
| `src/SwiftCollections/Support` | Compatibility shims | Includes MemoryPack and interpolated-string-handler shims for target/package variants. |
| `src/SwiftCollections/Utility` | Shared helpers, hashing, diagnostics, extensions | Preserve helper exception contracts. |
| `src/SwiftCollections.FixedMathSharp` | Fixed-point spatial query companion | Depends on FixedMathSharp or FixedMathSharp.Lean based on package variant. |
| `tests/SwiftCollections.Tests` | xUnit v3 core tests | Mirror source areas. |
| `tests/SwiftCollections.Benchmarks` | BenchmarkDotNet benchmarks | Alias-based runner supports `list`, `dictionary`, `query`, `all`, and other selections. |
| `.assets/scripts` | Windows-oriented release/version helpers | Assumes GitVersion tooling. |

Ignore generated output when reviewing structure:

- `bin/`
- `obj/`
- `TestResults/`
- `artifacts/`
- `BenchmarkDotNet.Artifacts/`

## Package Variants And Serialization

The repo builds four NuGet package IDs:

- `SwiftCollections`
- `SwiftCollections.Lean`
- `SwiftCollections.FixedMathSharp`
- `SwiftCollections.FixedMathSharp.Lean`

`ReleaseLean` sets `SWIFTCOLLECTIONS_DISABLE_MEMORYPACK`. In lean builds, MemoryPack-specific source is excluded or shimmed and package references switch to lean dependencies where applicable.

Serialization guidance:

- Types marked `[MemoryPackable]` are partial and depend on source generators in standard builds.
- State-backed collections should preserve state constructors and state structs.
- `net8.0` builds use System.Text.Json converter implementations where supported.
- Compatibility shims under `src/SwiftCollections/Support` exist for package/target variants. Follow existing preprocessor patterns instead of introducing a new conditional style.
- Build both standard and lean configurations after changing serialized fields, state structs, MemoryPack attributes, JSON converter behavior, or constructor signatures.

## Collection Design Rules

Use the right container for the ownership model:

| Use case | Preferred container |
| --- | --- |
| General key/value lookup with arbitrary keys | `SwiftDictionary<TKey, TValue>` |
| General unique values | `SwiftHashSet<T>` |
| Stable integer slots owned by the container | `SwiftBucket<T>` |
| Stable handles with stale-reference protection | `SwiftGenerationalBucket<T>` |
| Membership for compact externally owned non-negative integer IDs | `SwiftSparseSet` |
| Values attached to compact externally owned non-negative integer IDs | `SwiftSparseMap<T>` |
| Dense unique-value iteration with hash-backed membership | `SwiftPackedSet<T>` |
| Arbitrary, huge, or widely sparse integer IDs | `SwiftHashSet<int>` or `SwiftDictionary<int, T>` |

Sparse containers allocate lookup storage based on the highest stored ID. Do not use them for arbitrary large IDs unless a benchmark and memory budget justify it.

Keep hot-path collection code direct and auditable:

- Avoid LINQ, iterator allocations, delegates, closures, and boxing in hot loops.
- Prefer contiguous storage and simple loops when they preserve semantics.
- Preserve swap-back, tombstone, probing, freelist, and generation invariants.
- Do not split high-complexity probe or mutation methods merely to lower a metric if the split adds indirection or obscures invariants.
- When complexity remains intentionally high, update `docs/complexity-exceptions.md` with coverage and rationale.

## Spatial Query Rules

The query structures are runtime indexes, not passive data bags:

- `SwiftBVH<TKey, TVolume>`: best for heterogeneous broad-phase queries and mixed object sizes.
- `SwiftSpatialHash<TKey, TVolume>`: best for high-churn, mostly uniform object sizes and sparse query windows.
- `SwiftOctree<TKey, TVolume>`: best for uneven density and repeated regional queries.

Preserve these expectations:

- Keep query result behavior deterministic and duplicate-safe.
- Keep mutable query structures single-owner unless the caller synchronizes access externally.
- Keep all-hit query APIs writing into caller-owned collections.
- Avoid hidden allocations in query traversal, candidate gathering, and duplicate filtering.
- For deterministic fixed-point consumers, prefer the `SwiftCollections.FixedMathSharp` wrappers over `System.Numerics` query helpers.

## Code Style And Conventions

- Library and tests use file-scoped namespaces.
- `ImplicitUsings` is disabled. Add explicit `using` directives.
- Nullable settings differ by project:
  - `src/SwiftCollections` has nullable enabled.
  - `src/SwiftCollections.FixedMathSharp`, tests, and benchmarks have nullable disabled.
  - Do not introduce broad nullable churn outside the touched area.
- Public APIs should include XML docs unless the surrounding file clearly does not.
- Larger collection types often use `#region` blocks; preserve local style.
- Avoid renaming files, reshaping namespaces, or normalizing formatting purely for style.
- Minimize XML/project-file formatting churn. Existing XML files mix tabs and spaces.
- Keep comments useful and sparse. Explain invariants, not obvious assignments.

## Testing And Coverage

Full coverage is an explicit repo priority.

- If you change behavior in `src/SwiftCollections/Collection`, update matching tests under `tests/SwiftCollections.Tests/Collection`.
- If you change `Dimension`, `Observable`, `Pool`, `Query`, `Serialization`, `Utility`, or diagnostics behavior, update the corresponding test area.
- If you change `src/SwiftCollections.FixedMathSharp`, update `tests/SwiftCollections.Tests`.
- New public APIs should have focused behavior, edge-case, serialization/state, and invalid-input tests where applicable.
- Coverage should stay flat or improve. Prefer closing touched areas to full coverage when practical.
- Use `tests/SwiftCollections.Tests/coverlet.runsettings` for coverage. The FixedMathSharp test project intentionally points at the same runsettings so generated MemoryPack files stay excluded.

## Validation Commands

Use the solution root as the working directory.

Build everything:

```bash
dotnet build SwiftCollections.slnx -c Debug
```

Run unit tests:

```bash
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build
```

Run coverage for both test projects:

```bash
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.Tests/coverlet.runsettings
```

Run benchmark selections:

```bash
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- list
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- dictionary
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- query --list flat
dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net8 -- all --list flat
```

For release-sensitive or package-variant changes, also verify `Release` and `ReleaseLean`:

```bash
dotnet build SwiftCollections.slnx -c Release
dotnet test SwiftCollections.slnx -c Release --no-build
dotnet build SwiftCollections.slnx -c ReleaseLean
dotnet test SwiftCollections.slnx -c ReleaseLean --no-build
```

## CI And Release Notes

- `build-and-test.yml` runs `Release` and `ReleaseLean` on Ubuntu and Windows.
- `coverage.yml` publishes the GitHub Pages coverage report from the core test project.
- `publish-nuget.yml` runs on GitHub releases, validates the release tag against GitVersion, builds standard and lean packages, verifies all 8 `.nupkg`/`.snupkg` artifacts, runs Release tests, and publishes to NuGet.
- Package metadata lives in the project files. Packaging includes `README.md`, `LICENSE`, `NOTICE`, `COPYRIGHT`, and `icon.png` from the repo root.
- The repository has limited ignore coverage. Build, test, coverage, and benchmark output can dirty the working tree. Do not commit generated output unless explicitly asked.

## Quick Checklist

- Confirm the file being edited is part of the compiled project.
- Preserve compatibility for `netstandard2.1` and `net8.0`.
- Preserve standard and lean package behavior.
- Preserve public API and exception contracts unless a breaking change is intentional.
- Add or update targeted tests for behavior changes.
- Run relevant build/tests before finishing.
- Run benchmarks when changing a measured hot path or making a performance claim.
- Update README, `docs/OVERVIEW.md`, package metadata, or complexity exceptions when the public API, package shape, workflow, or performance story changes.
