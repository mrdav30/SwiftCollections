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
  `SC-Benchmark-003`.
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

### SC-Benchmark-002 - Exact Int16 normalization

- **Discovered / closed:** 2026-11-18; verified during
  [SC-Issue-007](issue-tracker.md#sc-issue-007---int16-normalization-fails-on-constant-input).
- **Owner / revisions:** `SwiftShortArray2D.Normalize`; baseline
  `0ffc9d76dc0ed3bfbf485cc7491c4cd66f01501e` versus the SC-Issue-007 integer
  interpolation. Published dependencies (`UseLocalLsfStack=false`), Release/net10.0,
  SDK 10.0.302, runtime 10.0.10, Windows x64, Intel Core i7-9700K.
- **Workload:** Normalize 32 retained square arrays per timed batch. Varying
  inputs repeat `[-1024,-512,0,512,1024]` and map to `[-100,100]`; constant inputs
  contain 5 and map to `[0,100]`. These are equal-output controls on this host;
  all output cells were checked against the five exact values or zero. The
  constant control uses min=0 so the old NaN conversion happens to agree with
  the corrected policy on the recorded runtime. It does not validate the old
  policy on other platforms.
- **Measurement:** Four complete processes per payload in ABBAABBA order,
  with loaded assembly hashes checked. Before cases, warm 100,000 alternating
  normalizations of the five-value identity map and a constant 8x8 array.
  Each case then warms 32 batches. Copy the original input into each backing
  array before each batch, outside the timer. Time only the 32 Normalize calls
  using Stopwatch. Timed batches are 20,000 for empty input; otherwise
  `Clamp(4000000 / (32 * cellCount), 32, 20000)`. Forced GC occurs before timing.
  `GC.GetAllocatedBytesForCurrentThread` reported **0 B/call** for every case.

| Side / input | Baseline median (range), ns/call | Integer median (range), ns/call |
| --- | --- | --- |
| 0 / target `[-100,100]` | 12.12 (12.11–12.17) | 13.73 (13.62–13.84) |
| 0 / target `[0,100]` | 11.34 (11.31–11.36) | 12.80 (12.74–12.91) |
| 8 / varying | 941.63 (938.50–944.45) | 667.82 (667.14–675.36) |
| 8 / constant | 929.36 (925.68–932.18) | 470.48 (469.80–489.82) |
| 64 / varying | 12,739.89 (12,685.55–12,780.08) | 12,422.41 (12,389.84–12,474.80) |
| 64 / constant | 12,704.06 (10,620.61–12,768.36) | 10,003.91 (5,844.63–10,082.71) |
| 256 / varying | 155,058.25 (144,043.75–155,569.63) | 155,213.38 (154,727.05–169,129.10) |
| 256 / constant | 155,024.60 (143,949.22–155,663.18) | 63,102.00 (63,001.95–63,416.21) |

- **Disposition:** Keep the Int32 weighted interpolation and existing in-place
  Fill for constant input. It removes floating-point errors without Int64
  division, allocations, dependencies or new helpers. Large varying-map medians
  are close; noisy outliers prevent a universal equivalence claim. Constant-map
  throughput improves, while empty calls pay roughly 1–2 ns for the new policy
  check. An explicit early-empty return improved empty-call timing but changed
  JIT layout and slowed larger varying maps by roughly 9–10% in its balanced
  comparison; it was removed. The selected payload matched the original fixed
  payload byte for byte and was measured again. The adverse guard result is
  retained here; it is not part of the selected implementation.
- **Reopen / limits:** These are local normalization microbenchmarks, not a
  cross-platform or whole-heightmap-pipeline guarantee. Reopen for a repeatable
  material regression on representative maps or a supported JIT/architecture.
  Keep the exact arithmetic and rounding oracle when testing alternatives.

For a focused rerun, use the console setup in the
[issue tracker](issue-tracker.md#reproducing-active-issues) against each source
version, with the fixtures and batch procedure above:

```powershell
dotnet run --project ../SwiftCollectionsRepro/SwiftCollectionsRepro.csproj -c Release -p:UseLocalLsfStack=false
```

### SC-Benchmark-001 - Dimensional resize validation code generation

- **Discovered / closed:** 2026-11-18; fixed during
  [SC-Issue-006](issue-tracker.md#sc-issue-006---dimensional-arrays-accept-inconsistent-shapes).
- **Owner / revisions:** `SwiftArray3D<T>.Resize`; unfixed source
  `9ccb87724652516232322f3fe5ab0b173b493c4b` versus the SC-Issue-006 changes.
  Published dependency mode (`UseLocalLsfStack=false`), Release/net10.0,
  SDK 10.0.302, runtime 10.0.10, Windows x64, Intel Core i7-9700K.
- **Workload:** A pair of resizes, from `(s,s,4)` to `(s,s+1,4)` and back.
  Each process first warmed 100,000 mixed small/empty resize cycles, then each
  timed case had 20,000 warmup pairs. Empty/small cases timed 1,000,000 pairs;
  the larger case timed 20,000. Stopwatch elapsed time and
  `GC.GetAllocatedBytesForCurrentThread` excluded setup, warmup and forced GC.
  Four complete processes per version ran in balanced order
  baseline/minimal/final/final/minimal/baseline, repeated twice. The surrounding
  probe also exercised 2D/3D construction and state restore at sides 0, 8 and 32;
  constructed/restored objects escaped to a static sink. Loaded assembly hashes
  distinguished all three payloads.

| Resize pair | Unfixed median (range), ns | Initial validation median (range), ns | Final median (range), ns | Bytes/pair, all versions |
| --- | --- | --- | --- | --- |
| `s=0` | 39.42 (37.78–40.02) | 47.51 (47.32–47.94) | 48.83 (46.56–53.40) | 48 |
| `s=8` | 595.82 (587.47–604.58) | 733.38 (707.27–741.65) | 595.97 (588.05–600.67) | 2,224 |
| `s=32` | 9,361.13 (9,302.24–9,678.95) | 10,832.10 (10,754.15–10,901.15) | 9,127.72 (9,036.97–9,157.62) | 33,328 |

- **Diagnosis / disposition:** Inlining the new guards into Resize changed JIT
  register allocation and destination-length loads in the per-cell loop. Warmed
  Tier1 disassembly retained the same guarded `GetIndex` devirtualization;
  lost devirtualization was ruled out. A separate non-inlined validation helper
  recovered nonempty copy throughput while retaining the original allocation
  expression and copy semantics. The unchecked allocation product is safe only
  after exact checked validation: a zero axis gives zero despite intermediate
  wrap; otherwise every positive intermediate fits Int32. Keep validation before
  allocation. No span or aggressive-inlining experiment remains in production.
- **Correctness control:** The dimensional regression suite verifies overflow,
  negative and zero axes, coordinate layout, serialization, ownership and failure
  atomicity. Timed resize fixtures used default-initialized data and returned to
  their original shape; allocation matched across the constructor, restore and
  resize cases. Empty constructor, restore and resize calls still pay constant
  validation costs; the empty resize
  pair above adds about 9 ns. This is not a zero-overhead claim.
- **Limits / reopen:** Earlier probes that primed tiering only on empty shapes
  and did not force constructed objects to escape gave inconsistent timing and
  allocation results; they were not accepted as equivalence evidence. The
  mixed/escaping comparison still found the initial nonempty resize regression
  and verified the selected correction. These are local microbenchmarks, not
  cross-platform guarantees. Reopen if a supported JIT/architecture changes the
  code generation or representative nonempty workloads regress. Huge empty
  shapes were checked for shape integrity, not for enumeration/Shift throughput;
  their existing nested-coordinate traversal remains outside SC-Issue-006.

For a focused rerun, use the console setup in the
[issue tracker](issue-tracker.md#reproducing-active-issues), reference each source
version in turn, and run with the same published-dependency command:

```powershell
dotnet run --project ../SwiftCollectionsRepro/SwiftCollectionsRepro.csproj -c Release -p:UseLocalLsfStack=false
```

Use `SwiftArray3D<int>` with the shapes, iteration counts and warmup above;
measure both resizes together, retain the instance, and check its final shape and
cell values outside the timed loop. Preserve every run and reverse version order.
Warm both nonempty and empty paths before timing so the first empty case does
not define the entire tiered profile. For exact-type JIT inspection, the runtime's
`DOTNET_JitDisasm=*Resize` setting captures Tier0 and warmed Tier1 code.

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
