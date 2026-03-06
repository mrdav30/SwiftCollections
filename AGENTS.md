# AGENTS.md

## Scope

This repository contains the `SwiftCollections` .NET library, its unit tests, and its benchmarks. Use this file as the repo-local operating guide for future agent work.

## Source Of Truth

- Trust `SwiftCollections.sln` and the `*.csproj` files over `README.md` or CI snippets when they disagree.
- The current compiled projects are:
  - Library: `src/SwiftCollections/SwiftCollections.csproj`
  - Unit tests: `tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj`
  - Benchmarks: `tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj`
- Current target frameworks:
  - Library: `netstandard2.1;net8.0`
  - Unit tests: `net8.0`
  - Benchmarks: `net8`
- README examples and the GitHub Actions workflow may have some drift from the current codebase. Verify names, targets, and commands against source before changing docs or build logic.

## Repository Layout

- `SwiftCollections.sln`: root solution containing the library, tests, and benchmarks.
- `src/SwiftCollections/`: library source and NuGet packaging metadata.
- `src/SwiftCollections/Collection/`: core collections such as `SwiftDictionary`, `SwiftList`, `SwiftQueue`, and `SwiftHashSet`.
- `src/SwiftCollections/Dimension/`: 2D/3D array types and default typed arrays.
- `src/SwiftCollections/Observable/`: observable collection variants and support types.
- `src/SwiftCollections/Pool/`: object, array, and collection pooling types.
- `src/SwiftCollections/Query/BoundingVolume/`: BVH implementation and bound volume types.
- `src/SwiftCollections/Serialization/`: JSON/state serialization helpers and compatibility shims.
- `src/SwiftCollections/Utility/`: shared helpers and extensions.
- `tests/SwiftCollections.Tests/`: xUnit v3 unit tests, organized to mirror library areas.
- `tests/SwiftCollections.Benchmarks/`: BenchmarkDotNet benchmarks plus the benchmark entry point.
- `.assets/scripts/`: PowerShell release/versioning helpers based on GitVersion.

## Important Caveats

- `src/SwiftCollections/Query/Octree/**` and `src/SwiftCollections/Query/SpatialHash/**` exist on disk but are explicitly removed from compilation in `src/SwiftCollections/SwiftCollections.csproj`. Do not assume edits there affect the shipped package.
- The library uses `MemoryPack`. Types marked `[MemoryPackable]` are partial and depend on source generators. Build after modifying serialized fields, `State` types, or serialization constructors.
- JSON serialization support is conditional:
  - `net8.0` uses `System.Text.Json` converter implementations.
  - Older targets rely on shim types under `SerializationAttributes.Shim.cs`.
- The repo has very little ignore coverage. Build and test outputs can dirty the working tree easily. Do not commit generated `bin/`, `obj/`, or `TestResults/` content unless explicitly asked.

## Code Style And Conventions

- Library and tests use file-scoped namespaces.
- `ImplicitUsings` is disabled. Add explicit `using` directives.
- `Nullable` is disabled. Do not introduce piecemeal nullable-annotation churn unless requested.
- Public API files often include XML documentation and, in larger collection types, `#region` blocks. Preserve the local style of the file you are editing.
- Avoid renaming files or normalizing type names just for style.
- Minimize formatting churn in project files and source files. Existing XML files mix tabs and spaces.

## Working Rules

- If you change behavior in `src/SwiftCollections/Collection/`, update or add the matching tests under `tests/SwiftCollections.Tests/Collection/`.
- If you change behavior in `Dimension`, `Observable`, `Pool`, or `Query/BoundingVolume`, keep tests in the corresponding folder aligned.
- If you add a public API, add XML docs unless the surrounding file clearly does not use them.
- If you introduce target-specific behavior, guard it with the existing preprocessor pattern instead of silently breaking `netstandard2.1`.
- Prefer focused changes. Do not modernize unrelated files while working on a localized fix.
- Unity-specific work does not belong in this repo unless the user explicitly asks for it; the README points Unity consumers to a separate repository.

## Validation Commands

Use the solution root as the working directory.

- Build everything:
  - `dotnet build SwiftCollections.sln -c Debug`
- Run unit tests:
  - `dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Debug --no-build`
- Run benchmarks:
  - Select the benchmark in `tests/SwiftCollections.Benchmarks/Program.cs`
  - `dotnet run --project tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release`

These commands were verified successfully in this repository during AGENTS creation.

## Docs And Packaging

- NuGet package metadata lives in `src/SwiftCollections/SwiftCollections.csproj`.
- Packaging includes `README.md`, `LICENSE.md`, and `icon.png` from the repo root.
- Version metadata is populated from GitVersion environment variables when available, with fallback values if GitVersion is absent.
- PowerShell release helpers in `.assets/scripts/` are Windows-oriented and assume `dotnet-gitversion` is available.
- When changing public API, installation instructions, target frameworks, or benchmark instructions, update `README.md` to match the actual code. Do not preserve stale examples if they no longer compile.

## Quick Checklist

- Confirm the file you are editing is part of the compiled project.
- Preserve compatibility for both library target frameworks.
- Add or update targeted tests for behavior changes.
- Build and run the relevant tests before finishing.
- Check whether docs or packaging metadata also need updates.
