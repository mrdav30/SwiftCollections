# Benchmark Signal Hardening Backlog

## Purpose

Track measured performance, allocation, scaling, and benchmark reliability
signals owned by SwiftCollections or its FixedMathSharp companion. Keep each
signal's evidence, decision, and next experiment here so an ignored report is
not the only record of a finding.

Correctness defects belong in [issue-tracker.md](issue-tracker.md). Promote work
requiring API design or several implementation stages into a focused
feature-work plan and link it from the signal.

## Intake Rules

- Signal IDs use `SC-Benchmark-NNN`. The next available ID is
  `SC-Benchmark-001`.
- Assign an ID at intake and never reuse it, including after closure or
  promotion. Check this file's history before changing the counter.
- Open a signal from an actual benchmark, allocation guard, profiler capture, or
  repeated validation run. Missing coverage and suspected slow paths alone are
  not measured regressions.
- Record the discovery date, owning source and fixture, source/dependency
  revisions, package or local-source mode, configuration, SDK/runtime, host,
  complete command, parameters, and observed values with units.
- Preserve the inputs, baseline, expected output, conclusion, uncertainty, and
  next isolation step in tracked text. Generated logs, traces, and binaries are
  supporting evidence and must not be required to understand or reopen a signal.
- Compare equivalent work and results. State whether construction, growth,
  clearing, warmup, and teardown are timed. Separate cold and warm allocation
  evidence, live count and retained capacity, and integer/string/object keys.
- Inspect every expected BenchmarkDotNet child, launch, and report before using
  its results. Exclude incomplete or failed work from comparisons. The current
  launcher returns zero after `BenchmarkSwitcher.Run`; parent success alone does
  not establish a successful measurement.
- Use the smallest representative experiment first. A short smoke or listing
  checks the harness; it does not establish a performance improvement.
- Close a signal after a verified fix or a documented no-change decision. Keep
  its ID, closure date, brief outcome, evidence link, and reopening condition;
  preserve existing anchors when moving detailed evidence into a completion
  record.

## Baseline Commands

From the solution root, build with published dependencies:

```powershell
dotnet build tests/SwiftCollections.Benchmarks/SwiftCollections.Benchmarks.csproj -c Release -f net10.0 -p:UseLocalLsfStack=false -p:DisableTransitiveProjectReferences=false
```

List selections and inspect a focused set of cases before running it:

```powershell
dotnet tests/SwiftCollections.Benchmarks/bin/Release/net10.0/SwiftCollections.Benchmarks.dll list
dotnet tests/SwiftCollections.Benchmarks/bin/Release/net10.0/SwiftCollections.Benchmarks.dll dictionary-parity --list flat
dotnet tests/SwiftCollections.Benchmarks/bin/Release/net10.0/SwiftCollections.Benchmarks.dll ticket-map-overhead --list flat
```

For example, smoke the explicit retained-reference cleanup cases:

```powershell
dotnet tests/SwiftCollections.Benchmarks/bin/Release/net10.0/SwiftCollections.Benchmarks.dll retained-reference-clear --filter '*ExplicitFullStorageClear*' -j Short
```

For coordinated unreleased sources, follow
[CONTRIBUTING.md](../../CONTRIBUTING.md#coordinated-source-development), carry
`UseLocalLsfStack=true` into child builds through the environment, and pass
`-p:DisableTransitiveProjectReferences=true` globally. Record the selected mode
and inspect the dependencies actually loaded by the benchmark child. Validate
ReleaseLean separately when the affected workload or package variant needs it.

## Existing Evidence

The [issue tracker](issue-tracker.md#resolved-issues) already records resolved
dictionary/set boxing and cold-lookup allocation defects. The
[dictionary allocation review](done/2026-09-10-dictionary-allocation-review.md)
retains historical context and its limits. Those records are not new open
signals or current benchmark baselines; rerun a representative workload before
making a new performance claim.

## Active Signals

None. Creating this tracker does not establish a new regression or measurement.

## Closed Signals

None recorded in this tracker yet.

## Entry Template

```markdown
### SC-Benchmark-NNN - Concise workload or evidence concern

- **Discovered / status:** Date; investigating, planned, or experimental
- **Owner:** Runtime source, benchmark fixture, and downstream consumer if relevant
- **Revisions / environment:** Source and dependency revisions, mode, profile, SDK/runtime, host
- **Workload / command:** Exact case, inputs, count/capacity, timed lifecycle, and command
- **Evidence:** Baseline/current values and units, allocation, complete child/launch counts
- **Correctness control:** Equivalent results and the independent behavior check
- **Interpretation / limits:** What the evidence establishes and what remains unknown
- **Next experiment:** The smallest discriminating measurement or inspection
- **Closure / revisit:** Verified outcome and the condition that would reopen it
```
