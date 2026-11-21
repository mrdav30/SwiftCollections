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
4. Document authored public/protected APIs, including applicable parameters,
   generic parameters, returns, property values, lifecycle and failure contracts.
   Verify inherited comments in the rendered reference. Update conceptual guides
   or authored landing-page overrides when behavior changes; do not edit generated
   API pages.
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

For assembly timing comparisons, compile the caller once per package flavor with
optimization explicitly enabled, including custom configurations such as
`ReleaseLean`. Check the actual loaded assembly hashes and dependency flavor.
Collect provenance after timing: reading a DLL into a byte array before owner
construction can change the measured storage placement. Warm the workload after
any setup GC, keep diagnostic captures separate from timings, and verify every
expected result and interval. A timing reversal after changing caller or JIT
placement is evidence of sensitivity, not proof of equal performance. Keep
reproducible inputs, results and limits in the owning benchmark signal.

Restore and build both package variants:

```bash
dotnet restore SwiftCollections.slnx --property:Configuration=Release
dotnet build SwiftCollections.slnx --configuration Release --no-restore
dotnet restore SwiftCollections.slnx --property:Configuration=ReleaseLean
dotnet build SwiftCollections.slnx --configuration ReleaseLean --no-restore
```

After changing `UseLocalLsfStack`, restore again with the selected mode and
configuration before using `--no-restore`; existing intermediate assets can still
refer to the other dependency graph.

Run both test configurations:

```bash
dotnet test SwiftCollections.slnx --configuration Release --no-build
dotnet test SwiftCollections.slnx --configuration ReleaseLean --no-build
```

Require 100% reported line, branch and executable-method coverage for both
runtime assemblies. Use a fresh results directory for each run and each test
project's runsettings, then run the same assertion used by CI:

```powershell
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c Release --no-build --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.Tests/coverlet.runsettings --results-directory artifacts/coverage/Release/core
dotnet test tests/SwiftCollections.FixedMathSharp.Tests/SwiftCollections.FixedMathSharp.Tests.csproj -c Release --no-build --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.FixedMathSharp.Tests/coverlet.runsettings --results-directory artifacts/coverage/Release/fixedmathsharp
./.assets/scripts/assert-full-coverage.ps1 -CoverageDirectory artifacts/coverage/Release
```

Repeat for `ReleaseLean`, replacing `Release` in the configuration and result
paths. Build each configuration before collecting coverage. The gate requires
both assembly reports, meaningful coverage data and no uncovered lines, branches
or executable methods. Every Windows/Linux standard/Lean build/test job runs it;
the existing post-main Pages workflow also enforces it before publication.
Unreachable runtime branches must be verified and removed, not accepted through
the complexity register or coverage exclusions. That register is reserved for
justified methods above the complexity threshold.

Comparer persistence changes must cover sorted, dictionary, hash-set,
bidictionary and observable-dictionary normal direct/generated nested JSON,
standard MemoryPack's formatter and generated packable paths, legacy-input
rejection, and unchanged raw-state/comparerless formats. Exercise equality
before insertion, duplicate rejection, configured/effective reverse roles,
collision-heavy continuation, and failed ref-existing loads. Keep shared codec
and wire validation in their existing helpers; keep owner-specific table
invariants in the owner. Supply a registry through JSON options or MemoryPack's
per-operation services; keep lookup and mutation free of persistence work.
Use matching `netstandard2.1` plugin dependencies in the existing
SwiftCollections-Unity smoke-test host. Check Standard/Lean JSON interchange
and semantic wire interchange between supported target frameworks. Use
independent old/new reader controls to verify the intended format break.
Validate actual Unity Mono and IL2CPP separately; Mono success does not
establish stripping or AOT support. On Windows, install Desktop development
with C++ (Visual Studio or Build Tools), the MSVC x64/x86 compiler and a Windows
SDK meeting the selected Unity version's requirements. Verify the actual player
backend and stripping level, process exit and result, and disable/assert JSON
reflection before serializer initialization when testing generated metadata.
See the [serialization guide](docs/api/guides/serialization-and-diagnostics.md)
for codecs, closed JSON metadata and explicit formatter bootstrap.

Build the API site after a Release build, using the pinned tool manifest at the
repository root (`dotnet-tools.json`):

```bash
dotnet tool restore
dotnet tool run docfx docs/api/docfx.json --warningsAsErrors
```

DocFX reads `SwiftCollections.dll` / `SwiftCollections.xml` and
`SwiftCollections.FixedMathSharp.dll` / `SwiftCollections.FixedMathSharp.xml`
from each project's `bin/Release/net10.0` directory. It does not select Lean or
`netstandard2.1` metadata. Both projects emit identically named DLL/XML files in
`bin/{Release,ReleaseLean}/{netstandard2.1,net10.0}`; check all eight assembly/XML
pairs when changing shared API documentation. Standard-only generated
MemoryPack APIs are not part of the Lean surface.

For an inventory or reference check, start with empty generated directories
`docs/api/obj/api` and `docs/api/obj/_site`; move previous output aside if it must
be retained. DocFX rebuilding alone can leave pages for removed APIs. Inspect
representative generated HTML for each family and comment pattern, and run the
local-link and reference smoke checks in `.github/workflows/coverage.yml`.
That workflow's publication policy remains the source of truth.

Reconcile authored symbols (including protected members, partial declarations,
operators, indexers and nested types) with generated metadata and emitted XML.
Account separately for explicit interface members hidden by DocFX, synthesized
constructors and generated serializer APIs. A successful build with no XML
warnings does not prove every applicable tag or contract is present. Keep
documentation-only investigations separate from runtime fixes and maintained
regression tests; preserve portable reproducers, observed behavior and contract
decisions directly in the relevant issue-tracker entries.

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
