# Contributing

Focused fixes and documentation improvements can go directly through a pull
request. For substantial public API, architecture, serialization, or performance
changes, open an issue or discussion first so the intended contract and evidence
bar are clear.

Please follow the [Code of Conduct](CODE_OF_CONDUCT.md). See
[Support](.github/SUPPORT.md) for help and [Security](SECURITY.md) for private
vulnerability reports.

## Source of truth

When generated docs, package metadata, examples, and behavior disagree, prefer
the source projects, tests, benchmarks, and workflows. Keep user-facing
documentation aligned when a change affects the public API, package variants,
serialization, or development workflow.

Standard and Lean variants must remain version-aligned within each package
family:

- `SwiftCollections` with `SwiftCollections.Lean`
- `SwiftCollections.FixedMathSharp` with `SwiftCollections.FixedMathSharp.Lean`

Standard includes the MemoryPack runtime. Lean exposes the same collection
surface without that runtime dependency. The FixedMathSharp companion family
declares its minimum compatible published SwiftCollections version separately,
so the two families can be released independently.

Record reproducible concerns outside the current change in the
[issue tracker](docs/feature-work/issue-tracker.md). Preserve issue IDs, source
revisions and verification evidence; track each concern in its owning
repository.

## Pull request checklist

1. Keep the change focused and preserve public contracts unless the proposal
   intentionally changes them.
2. Add or update tests for behavior changes, including edge cases and
   serialization state when relevant.
3. Benchmark performance-sensitive changes against the current implementation.
4. Add concise XML documentation for new public APIs and update the overview,
   README, or generated API landing pages when behavior changes.
5. Do not commit `bin/`, `obj/`, test results, coverage reports, NuGet packages,
   or BenchmarkDotNet artifacts.

Default lockstep versions are derived through GitVersion; scoped releases take
their version from an explicit argument or release tag. Do not manually bump
example or README versions for ordinary pull requests.

## Coordinated source development

Place Chronicler and FixedMathSharp checkouts beside SwiftCollections, then set
`$env:UseLocalLsfStack='true'` in PowerShell before building or testing the
solution. This selects source Chronicler and FixedMathSharp, including the Lean
shim. Validate both configurations:

```powershell
$env:UseLocalLsfStack='true'
dotnet build SwiftCollections.slnx -c Release -p:DisableTransitiveProjectReferences=true -m:1
dotnet test SwiftCollections.slnx -c Release --no-build -p:DisableTransitiveProjectReferences=true
dotnet build SwiftCollections.slnx -c ReleaseLean -p:DisableTransitiveProjectReferences=true -m:1
dotnet test SwiftCollections.slnx -c ReleaseLean --no-build -p:DisableTransitiveProjectReferences=true
```

Use `-m:1` for builds, including `dotnet test` when it builds, to avoid
overlapping writes to shared local project outputs. `Directory.Build.props` owns
the source-build identities and shared runtime version selection. Both
SwiftCollections assemblies use `0.0.0-sc-local-lsf` with assembly/file version
`0.0.0.0`, even when GitVersion values are present. Sibling references use the
same policy: Chronicler/core/shim/diagnostics use `0.0.0-chronicler-local-lsf`,
and FixedMathSharp assemblies use `0.0.0-fms-local-lsf`, all with numeric
assembly/file version `0.0.0.0`. These identities do not change for releases.
Local builds use explicit references at library, test, and benchmark boundaries
and pass `-p:DisableTransitiveProjectReferences=true` globally on restore, build
and test so sibling projects also preserve those explicit identities.

Package mode remains the default release-validation path. Clear the environment
variable before validating published dependencies; source-mode packages are not
release artifacts.

## Releases

The existing local command builds all four packages with the version derived by
GitVersion:

```powershell
.\.assets\scripts\set-version-and-build.ps1
```

For an independent package-family release, provide the family and stable
three-part version explicitly:

```powershell
.\.assets\scripts\set-version-and-build.ps1 `
    -PackageFamily SwiftCollections.FixedMathSharp `
    -Version 7.1.0
```

GitHub release tags select the packages published to NuGet:

| Tag                                      | Packages                                                                     |
| ---------------------------------------- | ---------------------------------------------------------------------------- |
| `SwiftCollections/v7.0.1`                | `SwiftCollections` and `SwiftCollections.Lean`                               |
| `SwiftCollections.FixedMathSharp/v7.1.0` | `SwiftCollections.FixedMathSharp` and `SwiftCollections.FixedMathSharp.Lean` |
| `v7.1.0`                                 | All four packages at one lockstep version                                    |

Scoped companion releases compile and pack against the published minimum core
version in `SwiftCollectionsPackageVersion` in
`src/SwiftCollections.FixedMathSharp/SwiftCollections.FixedMathSharp.csproj`.
Bump that property only when the companion requires a newer core package.

NuGet publication is not transactional across Standard and Lean. If a network
failure publishes only part of a validated family, rerun the same GitHub release
workflow; publication uses `--skip-duplicate` so already-published members are
skipped while missing members are retried.

## Documentation and cleanup

Conceptual guides live in `docs/api/guides` and publish alongside the API
reference and coverage through the existing Pages workflow. Keep guides
evergreen; version-specific upgrade steps belong in
[Migration](docs/MIGRATION.md). Preserve completed design and validation records
as history. Generated output under `docs/api/obj` is ignored.

Give each namespace-level type or delegate its own file. Public/internal nested
types use companion files while preserving containing types, generic
constraints, and CLR identities; private nested helpers may stay with their
owner. Authored C# files under `src` carry the current filename and MIT license
header. Test and benchmark files under `tests` have no license header.

Run cleanup explicitly before review using the SDK from `global.json` and
Node.js/npm for Prettier:

```powershell
$env:TargetFramework='net10.0'
dotnet format SwiftCollections.slnx --severity error
Remove-Item Env:TargetFramework
npx --yes prettier@3.9.5 --write .
```

Select one target framework for formatting so the SDK does not merge competing
fixes into files shared by multiple targets. Clear that environment selection
before normal multi-target builds; those builds still validate both frameworks.
If workspace loading reports unmatched project/metadata references, stop
semantic fixes and clean/rebuild the selected configuration before retrying.
Always validate the result with both target frameworks.

The .NET formatter follows `.editorconfig`, applying whitespace and error-level
style/analyzer fixes. It does not reproduce a Visual Studio cleanup profile.
Prettier is pinned in the command and may download on first use;
`.prettierignore` excludes generated output. No commit hook rewrites or stages
files. Use the existing environment flags for coordinated-source formatting when
that dependency mode is needed.

Check without rewriting files:

```powershell
$env:TargetFramework='net10.0'
dotnet format SwiftCollections.slnx --severity error --verify-no-changes
Remove-Item Env:TargetFramework
npx --yes prettier@3.9.5 --check .
```

## Local validation

Restore and build both package variants:

```bash
dotnet restore SwiftCollections.slnx --property:Configuration=Release
dotnet build SwiftCollections.slnx --configuration Release --no-restore
dotnet restore SwiftCollections.slnx --property:Configuration=ReleaseLean
dotnet build SwiftCollections.slnx --configuration ReleaseLean --no-restore
```

Run both test configurations:

```bash
dotnet test SwiftCollections.slnx --configuration Release --no-build
dotnet test SwiftCollections.slnx --configuration ReleaseLean --no-build
```

Build the API site after a Release build:

```bash
dotnet tool restore
dotnet tool run docfx docs/api/docfx.json --warningsAsErrors
```

See [AGENTS.md](AGENTS.md) for repository architecture, testing patterns,
serialization boundaries, and benchmark guidance.

Track measured performance, allocation, scaling, and benchmark reliability
concerns in the
[benchmark signal backlog](docs/feature-work/benchmark-signal-hardening-backlog.md).
It contains baseline commands and evidence requirements. Keep correctness
defects in the [issue tracker](docs/feature-work/issue-tracker.md).

## Branch-Specific CI Dependencies

Develop pushes and PRs targeting `develop` use `UseLocalLsfStack=true` and
immutable sibling source revisions in `build-and-test.yml`. Main and other
branches use released packages with no sibling checkouts; a `develop` -> `main`
PR validates packages. Release upstream dependencies before promotion and update
source pins deliberately as the coordinated stack changes. Source/package NuGet
caches are separated, and source graphs build serially to avoid shared-output
races. Restore, build and test also pass
`-p:DisableTransitiveProjectReferences=${{ env.UseLocalLsfStack }}` globally, so
inferred references in sibling projects cannot discard explicit assembly
identities. Serialization alone does not preserve those identities. Package mode
sets the flag to false. Both Release/Lean Windows/Linux lanes remain required.
