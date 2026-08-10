# Contributing

Focused fixes and documentation improvements can go directly through a pull
request. For substantial public API, architecture, serialization, or
performance changes, open an issue or discussion first so the intended contract
and evidence bar are clear.

Please follow the code of conduct in all project interactions.

## Source of truth

When generated docs, package metadata, examples, and behavior disagree, prefer
the source projects, tests, benchmarks, and workflows. Keep user-facing
documentation aligned when a change affects the public API, package variants,
serialization, or development workflow.

The package families must remain paired:

- `SwiftCollections` with `SwiftCollections.FixedMathSharp`
- `SwiftCollections.Lean` with `SwiftCollections.FixedMathSharp.Lean`

Standard includes the MemoryPack runtime. Lean exposes the same collection
surface without that runtime dependency.

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

Versions are derived through GitVersion during release packaging. Do not
manually bump example or README versions for ordinary pull requests.

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

## Code of Conduct

### Our Pledge

In the interest of fostering an open and welcoming environment, we as
contributors and maintainers pledge to making participation in our project and
our community a harassment-free experience for everyone, regardless of age, body
size, disability, ethnicity, gender identity and expression, level of
experience, nationality, personal appearance, race, religion, or sexual identity
and orientation.

### Our Standards

Examples of behavior that contributes to creating a positive environment
include:

- Using welcoming and inclusive language
- Being respectful of differing viewpoints and experiences
- Gracefully accepting constructive criticism
- Focusing on what is best for the community
- Showing empathy towards other community members

Examples of unacceptable behavior by participants include:

- The use of sexualized language or imagery and unwelcome sexual attention or
  advances
- Trolling, insulting/derogatory comments, and personal or political attacks
- Public or private harassment
- Publishing others' private information, such as a physical or electronic
  address, without explicit permission
- Other conduct which could reasonably be considered inappropriate in a
  professional setting

### Our Responsibilities

Project maintainers are responsible for clarifying the standards of acceptable
behavior and are expected to take appropriate and fair corrective action in
response to any instances of unacceptable behavior.

Project maintainers have the right and responsibility to remove, edit, or reject
comments, commits, code, wiki edits, issues, and other contributions that are
not aligned to this Code of Conduct, or to ban temporarily or permanently any
contributor for other behaviors that they deem inappropriate, threatening,
offensive, or harmful.

### Scope

This Code of Conduct applies both within project spaces and in public spaces
when an individual is representing the project or its community. Examples of
representing a project or community include using an official project e-mail
address, posting via an official social media account, or acting as an appointed
representative at an online or offline event. Representation of a project may be
further defined and clarified by project maintainers.

### Enforcement

Instances of abusive, harassing, or otherwise unacceptable behavior may be
reported by contacting the project team at `david.oravsky@gmail.com`. All
complaints will be reviewed and investigated and will result in a response that
is deemed necessary and appropriate to the circumstances. The project team is
obligated to maintain confidentiality with regard to the reporter of an
incident. Further details of specific enforcement policies may be posted
separately.

Project maintainers who do not follow or enforce the Code of Conduct in good
faith may face temporary or permanent repercussions as determined by other
members of the project's leadership.

### Attribution

This Code of Conduct is adapted from the [Contributor Covenant][homepage],
version 1.4, available at [http://contributor-covenant.org/version/1/4][version]

[homepage]: http://contributor-covenant.org
[version]: http://contributor-covenant.org/version/1/4/
