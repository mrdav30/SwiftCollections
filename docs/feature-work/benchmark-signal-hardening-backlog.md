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
  `SC-Benchmark-009`.
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
  its results. Exclude incomplete or failed work from comparisons. The launcher
  propagates rejected arguments, validation/report failures and failed child
  executions; successful information commands intentionally have no measurements.
  A zero exit does not establish that every intended case or launch was selected.
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

None.

## Closed Signals

Closed records describe their named source revisions and recorded environments.
Baseline, candidate and selected-current refer to those historical payloads;
their test counts and measurements are not fresh verification of a later
checkout. Reproduce them from the recorded inputs and procedure rather than
depending on retained local captures. Closure establishes the stated fix or
decision, not a package release or a broader performance guarantee.

### SC-Benchmark-008 - Discarded old-state snapshots during ticket-owner replacement

- **Discovered:** 2026-11-23, final SwiftDenseTicketMap/SwiftTicketStore review.
- **Closed:** 2026-11-23; removed the old-owner-dependent allocation from
  complete MemoryPack replacement. Null, omitted-state and failed-replacement
  contracts remain unchanged. No release claim or new completion gate.
- **Owners:** Standard MemoryPack readers for SwiftDenseTicketMap<T> and
  SwiftTicketStore<T>, together with their State getters. Lean is unaffected.
- **Revision/environment:** SwiftCollections
  989a4272754c02f42f86719625db68cd92af8569, clean before review;
  released-package dependencies, MemoryPack 1.21.4 / Chronicler.Core 1.0.0.
  Windows x64, Core i7-9700K, SDK 10.0.302 / .NET 10.0.10, optimized Release.
  The same results occur with actual net10.0 and netstandard2.1 runtime
  assemblies in a .NET 10 host, with selected DLL hashes and target-framework
  attributes checked.
- **Baseline cause:** For a complete one-member owner record and a non-null
  destination, the generated reader evaluates `__State = value.State`, then
  reads into that local. The readonly state reader allocates fresh arrays and
  replaces the DTO, discarding the old snapshot. The owner constructor creates a
  replacement. State snapshots cost three arrays for a store and four for the
  issuer; issuer generation history is copied even when all historical slots are
  retired. Baseline generated output confirmed this path for both owners, not
  only an inferred generator pattern.

The following measured bytes per complete decode replace the destination with an
**empty** snapshot. The old owner remains unchanged. Construction, wire
encoding, formatter warmup and old-owner validation are outside measurement.
Each case warms 32 calls and measures 128 calls using
GC.GetAllocatedBytesForCurrentThread with background GC disabled. No timing or
universal throughput claim is made.

| Old live int entries | Issuer: null destination | Issuer: existing destination | Store: null destination | Store: existing destination |
| -------------------: | -----------------------: | ---------------------------: | ----------------------: | --------------------------: |
|                    0 |                    136 B |                        232 B |                    48 B |                       120 B |
|                  128 |                    136 B |                       1768 B |                    48 B |                      1656 B |
|                 4096 |                    136 B |                      49384 B |                    48 B |                     49272 B |
|                16384 |                    136 B |                     196840 B |                    48 B |                    196728 B |

At 4096 entries, the discarded snapshot alone adds 49248 B for the issuer or
49224 B for the store. An issuer with Count=0 and 4096 retired generations still
uses 16616 B/decode versus 136 B into null (16480 B extra). This tracks logical
state/history, not retained physical capacity. Both target assets give the same
values. Complete incoming empty records are respectively hex
`010400000000000000000000000000000000` and `0103000000000000000000000000`.

**Portable baseline reproducer:** from the recorded baseline checkout, create an
external net10.0 console project and reference the core project, using the
commands below from the repository root. Set
`<ConcurrentGarbageCollection>false</ConcurrentGarbageCollection>` in the
console project. Replace Program.cs with this program; no maintained fixture is
required.

```powershell
dotnet new console --framework net10.0 --output ../SwiftTicketDecodeRepro
dotnet add ../SwiftTicketDecodeRepro/SwiftTicketDecodeRepro.csproj reference src/SwiftCollections/SwiftCollections.csproj
dotnet run --project ../SwiftTicketDecodeRepro/SwiftTicketDecodeRepro.csproj -c Release -p:UseLocalLsfStack=false -p:DisableTransitiveProjectReferences=false
```

```csharp
using System;
using MemoryPack;
using SwiftCollections;

var issuer = new SwiftDenseTicketMap<int>(4096);
var store = new SwiftTicketStore<int>(4096, 4096);
for (int i = 0; i < 4096; i++)
{
    store.Add(issuer.Add(i), i);
}
byte[] issuerBytes = MemoryPackSerializer.Serialize(new SwiftDenseTicketMap<int>(0));
byte[] storeBytes = MemoryPackSerializer.Serialize(new SwiftTicketStore<int>(0, 0));
Console.WriteLine($"issuer null/existing: {Bytes<SwiftDenseTicketMap<int>>(null, issuerBytes)}/{Bytes(issuer, issuerBytes)} B");
Console.WriteLine($"store null/existing: {Bytes<SwiftTicketStore<int>>(null, storeBytes)}/{Bytes(store, storeBytes)} B");
if (issuer.Count != 4096 || store.Count != 4096)
    throw new Exception("Previous owners changed.");

static long Bytes<T>(T? previous, byte[] bytes) where T : class
{
    for (int i = 0; i < 32; i++)
    {
        T? target = previous;
        MemoryPackSerializer.Deserialize(bytes, ref target);
    }
    long start = GC.GetAllocatedBytesForCurrentThread();
    for (int i = 0; i < 128; i++)
    {
        T? target = previous;
        MemoryPackSerializer.Deserialize(bytes, ref target);
        if (target is SwiftDenseTicketMap<int> map ? map.Count != 0
            : target is not SwiftTicketStore<int> components || components.Count != 0)
            throw new Exception("Incoming empty state was not restored.");
        if (ReferenceEquals(previous, target))
            throw new Exception("Expected a replacement owner.");
    }
    return (GC.GetAllocatedBytesForCurrentThread() - start) / 128;
}
```

To check actual netstandard2.1 assets, build the core with
`-c Release -f netstandard2.1` and the same package-mode properties. Bind a
separate .NET 10 host directly to that core DLL and its target dependency
assets, not a multi-target ProjectReference that selects net10.0. Register the
four closed owner/state types with their generated RegisterFormatter methods
before use. The input records and expected allocations above remain the oracle.

**Resolution:** use authored one-State owner readers through the existing
GenerateType.NoGenerate pattern. Both reuse the internal SwiftStateMemoryPack
envelope reader; readonly state DTOs and their generated codecs are unchanged.
The shared reader captures previous.State only for a zero-member owner. Complete
records read fresh state, then each owner validates and constructs its
replacement before assigning the caller's reference. Null and invalid headers
retain their existing behavior. This removes the cause inside the library;
callers can keep using by-reference decoding. The public guide's workaround
paragraph was removed.

MemoryPack 1.21.4's
[tagged emitter](https://github.com/Cysharp/MemoryPack/blob/1.21.4/src/MemoryPack.Generator/MemoryPackGenerator.Emitter.cs)
does not offer an overwrite-suppression switch for this generated class path.
The authored boundary preserves the Object bytes, target-specific interfaces,
netstandard public methods, private net10 explicit methods, owner-array
registration and preservation attributes. No factories, delegates, owner fields,
reflection, registry or ordinary collection-operation work were added. Three
Standard-only files add 225 authored lines including headers, XML, conditional
compatibility wrappers and the 32-line shared helper; they replace generated
owner plumbing. Lean excludes those files through its existing build policy.

Three maintained regressions compare 64 warmed replacements into null versus a
4096-entry prior owner (or 4096 retired issuer generations), resetting the old
destination before every call. They check exact old state, empty replacement and
independent future insertion without hardcoded object sizes. All three failed on
the unfixed implementation for the expected allocation difference, then passed.
Existing literal-wire, null/omitted/partial-state, mutable-payload, truncation
and replacement-failure tests remain intact.

### Allocation and bounded timing verification

The baseline reproducer now prints issuer **136/136 B** and store **48/48 B**.
Before/final captures bind frozen optimized Standard graphs for both actual
targets, with identical selected dependencies and verified core hashes. Incoming
counts 0/128 and old counts 0/4096/16384 have exact ticket/value oracles; old
owners remain unchanged. The complete replacement results are:

| Prior int state                          | Issuer before / final | Store before / final |
| ---------------------------------------- | --------------------: | -------------------: |
| Empty owner                              |           232 / 136 B |           120 / 48 B |
| 4096 live entries, incoming empty        |         49384 / 136 B |         49272 / 48 B |
| 16384 live entries, incoming empty       |        196840 / 136 B |        196728 / 48 B |
| 4096 live entries, incoming 128          |        53040 / 3792 B |       54536 / 5312 B |
| 4096 retired generations, incoming empty |         16616 / 136 B |       Not applicable |

Null-destination allocations are unchanged. Zero-member-owner controls also
retain their intentional snapshot/reconstruction cost: 114920 B for the issuer
and 164032 B for the store at 4096 entries (store indices are 3*i). For live int
entries the removed allocation is exactly 12*N+96 B / 12*N+72 B respectively;
retired issuer history removes 4*N+96 B. No universal zero-allocation decode is
claimed; incoming state and the replacement still own storage.

Timing uses independent ABBA-ordered processes, 32 initial warm calls, seven
128-call batches and exact validation outside the clock. Four before/final
processes per target cover 19 rows each. Default-tiered 4096-entry-to-empty
medians improve from issuer 12.99-14.24 to 0.356-0.370 microseconds and store
11.74-14.48 to 0.206-0.308 on net10.0. Corresponding netstandard ranges are
13.09-15.17 to 0.355-0.391 and 13.71-14.55 to 0.219-0.227. All allocation counts
agree across processes and targets. Construction, encoding, registration warmup
and validation are outside measurement; decoding allocations/GC are inside.

Retain the adverse controls: short fresh-destination issuer/128-entry captures
were slower after the change on both targets: default tiering about 3.95-4.39 to
5.75-14.45 microseconds on net10.0 and 4.12-4.65 to 5.49-5.89 on netstandard.
Short tiering-disabled captures also showed the same direction (0.895-1.287 to
2.263-2.883 and 0.787-0.964 to 2.002-2.697). These are not discarded as outliers
or attributed to tiering alone.

A bounded isolated control removes preceding large-state cases, warms each
fresh-destination 128-entry case for 200 ms, and measures seven 2048-call
batches. Four balanced before/final processes per target with
DOTNET_TieredCompilation=0 give issuer 1.043-1.132 / 1.027-1.081 microseconds
(net10.0) and 1.043-1.128 / 1.008-1.052 (netstandard); store 0.957-1.127 /
0.962-1.035 and 0.905-0.985 / 0.935-1.050. Default tiering/PGO-on controls with
the same isolated warmup remain variable: issuer 1.09-3.70 / 0.78-3.46 and
3.34-3.80 / 3.49-3.71; store 2.44-3.00 / 2.37-2.75 and 2.97-3.01 / 2.71-3.11.
Allocations remain equal. The short-case direction is not a consistent fully
warmed issuer regression; its timing attribution remains unresolved. These
checks establish neither universal timing parity nor an accepted new cost
budget. No additional source tuning was justified by these
warmup/order-sensitive controls.

The complete measurements contain 672 verified rows: 304 initial default-tiered,
304 short tiering-disabled, 32 isolated default-tiered and 32 isolated
tiering-disabled. No build, test, profiler or Unity run overlapped timings. The
allocation fix, rather than an unqualified timing claim, closes this signal.

### Compatibility, coverage and host verification

All seven final lanes pass the unchanged full line/branch/executable-method gate
for both runtime assemblies, with no failures or skips:

| Executed lane                                      | Core / companion tests | Core lines, branches, methods   | Companion lines, branches, methods |
| -------------------------------------------------- | ---------------------: | ------------------------------- | ---------------------------------- |
| Debug net10.0, source                              |             2423 / 100 | 8710/8710, 3136/3136, 1475/1475 | 257/257, 86/86, 67/67              |
| Release net10.0, package and source separately     |        2425 / 100 each | 6634/6634, 3128/3128, 1475/1475 | 205/205, 86/86, 67/67              |
| ReleaseLean net10.0, package and source separately |        2270 / 100 each | 6407/6407, 3028/3028, 1425/1425 | 205/205, 86/86, 67/67              |
| Actual netstandard2.1 Release, package             |             2424 / 100 | 6638/6638, 3128/3128, 1479/1479 | 205/205, 86/86, 67/67              |
| Actual netstandard2.1 ReleaseLean, package         |             2269 / 100 | 6407/6407, 3028/3028, 1425/1425 | 205/205, 86/86, 67/67              |

Commands are the existing CONTRIBUTING build/test/coverlet commands and
assert-full-coverage.ps1. Source restore/build/test passes UseLocalLsfStack=true
and global DisableTransitiveProjectReferences=true with serial -m:1 builds;
package mode sets both false and uses a separate cache. Actual netstandard tests
bind frozen target DLLs in a .NET 10 host, with the existing five BCL overlays
and narrow hash-comparer test dispatch adaptation. Existing
release-only/NET10-only cases explain count differences. New authored
serializers are covered, including registration and compatibility paths; no
exclusion, threshold or complexity exception changed. Existing xUnit2013 / Lean
xUnit1026 warnings remain. The only expected failing tests were the three new
regressions before correction.

Four closed-JSON writer hosts and twelve readers pass target/variant exchanges,
five payload families, nested arrays/lists and the issuer/component replay with
JSON reflection disabled before first use. Standard exchanges binary too. Both
target assets also pass old-reader/new-writer and new-reader/old-writer checks
using frozen unfixed owners. Literal control bytes and selected aggregate bytes
are unchanged. This is evidence for the selected records, not universal schema
evolution. All four NuGet identities contain both target DLL/XML pairs matching
the final package graph: SwiftCollections, SwiftCollections.Lean,
SwiftCollections.FixedMathSharp and SwiftCollections.FixedMathSharp.Lean.

Unity 6000.3.9f1 Windows x64 passes Mono (236 Standard / 149 Lean checks) and
nondevelopment High-stripping, Release-compiler IL2CPP players (221 / 137).
Actual netstandard2.1/C# 11 helpers cover closed payload codecs, by-ref
complete, omitted, null and truncated records, unchanged old owners, exact
tickets/orders and continuation. No authored link.xml or blanket preservation is
used. Mono verifies loaded managed-file hashes; Editor validates every input
before build; native results report the verified compiled-input manifest and
assert IL2CPP. Standard core SHA-256 is
A040679B7EE3A8B1D4F0841366EC994120F911E50A07ABA1BF91F22BAE314481; Lean is
F4B19888735BEF7A0B4E089CB06F25D4FD2123206259490B7FEE7AC566EDB572. Both backends
produce the same explicit continuation:

```text
agents=0:1=101,2:1=303,1:2=404
positions=0:1=6,2:1=109,1:2=3
velocities=2:1=3,0:1=2,1:2=4
health=2:1=50,0:1=80,1:2=25
```

Fresh DocFX 2.78.5 generation and local file links pass with zero
warnings/errors. Source/metadata/XML audit accounts for 82 authored APIs in
Standard, including six owner serializer methods replacing generated methods,
six remaining generated state serializer methods and five synthesized struct
constructors. Lean remains 76 authored APIs, no generated serializers and five
synthesized constructors. Both internal helpers have summaries. Before/final
metadata comparisons preserve public/protected signatures, generic constraints
and explicit-interface contracts in both targets; no net10 public serializer API
was added. The site's selected net10 target continues to hide explicit methods;
netstandard public bootstrap/ serializer methods remain documented in emitted
XML.

Implementation started clean at e4835b0c66d082c7921f85ddb2decf26fdd97dd6, index
af940027315836005b4126fb9d55b03248859f9a. Source providers remain unchanged:
Chronicler 0281469ecc40e65a05867f473bc10f030e7f69f8 and FixedMathSharp
8fdf889322f1bb82501c2afec63d6bb2f304ace2. Local source results do not claim a
hosted CI run against a different Chronicler pin. No provider, adapter,
dependency pin, CI policy or package version changed. Physical maximum arrays,
forced OOM, other host platforms and universal timing/codec guarantees remain
untested. Independent source/test and evidence reviews found no remaining
correctness issue.

**Reopen when:** previous-owner-dependent allocation returns, a consuming
restore budget shows a repeatable attributable regression, or
wire/zero-member/failure/ AOT compatibility changes. No issue-tracker bug ID is
assigned.

### SC-Benchmark-007 - Value-key spatial hash insertion boxing

- **Discovered / closed:** 2026-11-22; fixed and verified during 2D query parity
  Phase 4. No remaining performance gate.
- **Owner:** SpatialHashCore.Insert, shared by Numerics/fixed 2D and 3D facades.
- **Reproducer:** SwiftCollections baseline
  dc4af55d4fbfeb1c7ebf08e4a3d26264bc5709e9, FixedMathSharp 8fdf889 and
  Chronicler 0281469, source mode; Windows x64, SDK 10.0.302/.NET 10.0.10.
  Establish int key 1 at bounds [0,2] per axis, capacity 16/cell size one, warm
  1,000 duplicate Insert calls, then measure another 1,000 using
  GC.GetAllocatedBytesForCurrentThread. Allocate the Action before measurement.
  Assert all return false, Count stays one and bounds agree.
- **Observed / expected:** 24,000 B / 0 B in each of the four public facade
  regressions. The object null guard boxes int in the tested JIT tier. An
  optimizing JIT may eliminate the old box; the evidence is tier-specific.
- **Resolution:** Use existing ChronicleGuard.ThrowIfNullGeneric as the first
  statement, retaining null exception, parameter name and pre-mutation ordering.
  All four regressions now measure zero bytes across Standard/Lean and both
  library targets; Debug and strict coverage also pass. Tests are named
  WarmTyped2DUpserts_AllocateNothing and WarmTyped3DUpserts_AllocateNothing in
  each query test project. Build with source-mode globals, then run each project
  with --no-build and --filter FullyQualifiedName~Upserts_AllocateNothing.
- **Evidence / reopening:**
  [Phase 4 record](done/2026-11-22-2d-query-parity-plan.md#phase-4--complete-review-hardening-and-feature-verification).
  Reopen for a reproduced allocation on unchanged-bounds typed upserts, or a
  null/order contract regression. Fresh insertions and changed cell membership
  can allocate storage and are outside this zero-allocation assertion.

### SC-Benchmark-006 - Unattributed native exit during persistence measurement

- **Discovered / closed:** 2026-11-20 / 2026-11-20; closed by a bounded
  no-source-change decision. The frozen baseline failure reproduces, but its
  cause remains unresolved; current Standard/Lean controls pass and review finds
  no attributable source defect. This is not a crash fix, exoneration of earlier
  IO or a reopened SC-Benchmark-005 timing gate. Closure evidence and reopening
  criteria follow the historical checkpoints below.
- **Source / environment:** Phase 2 working tree over `4fedfa1` before replacing
  the shared JSON descriptor arrays with a fixed tuple. Standard net10.0 core
  SHA256 `EDB71A257E5097496762F1BF9225A6DE8405C1069AA4A2F3445738419EA1DCEC`;
  caller `A567E0C95959E24C40F731EEB2B85C2B3BB66077F71811F621E92095D6685A1F`.
  Published Chronicler 1.0.0 / MemoryPack 1.21.4, SDK 10.0.302, runtime 10.0.10,
  Windows x64 build 26300, i7-9700K, CPU2, normal priority,
  `DOTNET_TieredCompilation=0`.
- **Portable reproducer / observed result:** Use the four-family default-int
  boundary protocol in the
  [Phase 2 record](done/2026-11-20-comparer-persistence-plan.md#phase-2-execution-record--2026-11-20).
  Build one optimized caller against the Phase 1 core, copy its graph and
  replace only the core DLL; launch `dotnet <caller.dll> <fresh-output.json>`.
  One current-core process in the initial ABBA attempt printed
  `Fatal error. 0xC0000005` and produced no completed matrix. Expected: normal
  exit and 288 checked rows. Rows/provenance are written only at completion, so
  this does not localize failure to serialization, validation or final IO.
- **Bounded controls:** The exact same pre-optimization current core completed
  four other 288-row processes, including a CDB replay with first-chance AV
  logging configured. Three baseline processes also completed. The final
  implementation subsequently completed all eight Standard/Lean comparison
  processes / 1728 rows. Exclude the failed run and all diagnostic replay
  timings from final comparisons. Passing replays do not prove a native fix.
- **Recovered crash evidence:** Windows retained a minidump for PID11984; its
  command line identifies the exact failed current-core invocation. CDB shows
  fatal CLR exception handling through `ProcessCLRException` /
  `EEPolicy::HandleFatalError`. The original exception record reports a read
  from address 0x48 in unresolved code, with System.Text.Json frames below
  exception dispatch. This does not identify the originating managed method or
  establish whether it was an owner operation, validation or final JSON output.
- **Phase 3 controls — 2026-11-20:** A larger decode-only batch also failed on
  the committed Phase 2 baseline `b6c1953d2a81570bae8f97ab9c53a64e0edb5d0a`,
  before executing the candidate optimization. Core SHA256
  `BAE679CB7276D410900758CF4362119D8C9376685231BDAD0B3A1B1697445AFD`, optimized
  caller `64D160A5CE8ED6639D386FA8FE6AC68419EAAA42ADB3CF1FB1561897EB2267ED`. Use
  the same default-int dictionary/bidictionary inputs, fix count at 65536,
  measure decode only, warm eight calls after each forced full GC, and perform
  five samples of 32 calls. Validate every mapping outside timing, retain the
  decoded owner, and keep the original JSON array captured by the decode
  delegate. Keep CPU2, normal priority and disabled tiering. One process failed
  with an access violation in `Utf8JsonReader.ConsumeString`; a later fresh
  baseline process failed with a wrapped `NullReferenceException` in
  `Utf8JsonReader.Read` during warmup. The latter stack identifies the nested
  `JsonSerializer.Deserialize<TState>(ref reader, options)` call, through the
  parameterized key/value constructor, before collection population. Correction:
  its frozen caller's `Program.Main` line 51 calls BiDictionary, after the
  Dictionary JSON and binary sequence. Earlier binary IO cannot be excluded as a
  contributor. The first short fatal trace alone does not identify its family.
  This is not evidence that both incidents share a root cause, or that the
  allocation optimization fixes either failure. A CDB replay completed all 20
  batch rows without capturing an AV. Separate plain-BCL pair-array, raw-state
  and baseline-owner drivers each completed 200 decodes with full-content checks
  and GC boundaries every 40 operations. Two additional framework-only
  nested-reader controls, using the same ref-reader delegation with no
  Swift/Chronicler/MemoryPack references, completed 200 decodes each. These
  controls do not reliably reproduce or attribute the fault. ClrMD 3.1 with the
  matching DAC could not resolve the original small dump's code address. Exclude
  failures and diagnostic runs from timings; passing controls do not establish a
  fix. The final balanced binary-only comparison completed eight processes/80
  rows, and the final full boundary matrix completed eight processes/1728 rows.
  The focused JSON batch remains an unresolved reliability limit rather than an
  accepted performance sample.
- **Calibrated closeout capture — 2026-11-20:** After installing MSVC/SDK, pin
  the original caller to runtime 10.0.10 (`dotnet --fx-version 10.0.10`),
  tiering disabled, CPU2, normal priority, workstation/nonconcurrent GC. The
  collector first captured a native read at address 0x8 in a non-inlined null
  field read, saved full memory, then allowed the CLR to catch NRE and complete.
  Use `sxe av` with `.exr -1; .ecxr; kb; .dump /ma /u <fresh-name>; gn` and an
  unhandled CLR/AV capture; parent debugger success is insufficient. Two frozen
  baseline processes completed 20/20 rows each; the third reproduced an AV and
  supplied a full first-chance snapshot. Replay stopped there rather than
  completing the fourth planned baseline. Two selected-current processes
  completed 20/20 rows each with the same caller and no AV or unhandled CLR
  event; current core hash
  `807CDB2EAB4B463B2BF9924A2AB224509E28495500DF695249343856DAC2529E`.
- **Operation and native evidence:** The full snapshot has twelve completed
  samples and a 65536-entry BiDictionary in the retained sink: BiDictionary JSON
  sample three, after all five Dictionary JSON and five Dictionary binary
  samples. The fault is `coreclr!Buffer::BulkMoveWithWriteBarrier+0x100`, inside
  card marking under `System.Text.Json.ReadStack.Pop`. Its 112-byte destination
  `0xa69d77de28` is writable stack below heap-low `0x1e4a1400000`, yet the
  helper's not-in-heap flag is zero. The fault address `0x1e4583f873b` equals
  card-table base `0x1e4436bd840` plus `(destination >> 11)` and is unallocated.
  Offline exception/default contexts agree; 576 captured helper-code bytes match
  the installed runtime and the call target is its proper entry. ReadStack has a
  valid four-frame array/count two; Current differs from its expected source
  frame and contains an invalid `PolymorphicJsonTypeInfo=0x1`. These
  observations do not distinguish earlier corruption, context/control-flow
  trouble or a runtime defect, and do not establish one cause for all incidents.
- **Independent copy control:** Three fresh framework-only processes completed
  ten million 112-byte managed-reference frame copies each, checking all
  fourteen fields. Use a heap `Frame[]`, local stack Frame, and
  `array.AsSpan(index,1).CopyTo(MemoryMarshal.CreateSpan(ref local,1))`;
  allocate 64 KiB every 4096 iterations and force full GC every 200000. JIT
  output confirms the same BCL bulk-write-barrier helper with length 112. No
  SwiftCollections, Chronicler or MemoryPack is loaded; all thirty million
  copies pass. This bounds the investigation but does not reproduce or exonerate
  the earlier IO.
- **Initial Phase 3 disposition (superseded below):** The user delegated final
  judgment on the Phase 3 reliability gate. Close feature implementation
  acceptance after its required checks, and retain SC-Benchmark-006 as this
  separate open release-reliability follow-up. No crash fix or confirmed
  runtime/environment defect is claimed. No production workaround, unsafe-code
  suppression, maintained probe tests or profiler-policy changes were added.
  Reopen feature acceptance for a confirmed attributable defect; review this
  open risk before release promotion.
- **Initial attribution / planned isolation:** No concrete driver/owner memory
  misuse was found in independent review. The synchronous managed-array driver
  uses no manual readers, unsafe pointers or borrowed buffers. Do not attribute
  this to the tuple allocation improvement, infer that the original Phase 2
  incident was a known baseline failure from the later Phase 3 controls, or add
  speculative runtime changes. On recurrence collect first-chance context and
  operation/progress before assigning ownership or promoting a confirmed
  implementation defect. The next discriminating slice is BiDictionary JSON
  without preceding binary IO, followed by a matching first-chance capture if it
  fails. Keep this unresolved risk in release review; passing selected-current
  and independent controls do not prove a fix.

- **Signal closeout — 2026-11-20:** The user requested a source-focused, bounded
  investigation. Starting checkout was clean at
  `21b85acdd19edaa7b2ab9e3a3958cdc6e7deb3fb`; runtime/test source is unchanged
  from `cab616d0b741afae56ed207261683881254fcc23`. Reuse the 65536-entry
  protocol above in a disposable optimized net10.0 caller, limiting construction
  and serialization to BiDictionary JSON. Remove every MemoryPack IO call and
  all other owner workloads; keep default serializer options and the captured
  managed byte array alive in the decode delegate. Serialize once, validate an
  initial decode, then run five samples of eight warmups and 32 decodes with the
  same forced-GC boundaries and forward/reverse checks. Expected: five completed
  rows and normal exit per process, 201 decodes per completed process. Check
  each warmup count and each measured batch's count checksum; check all
  key/value mappings and reverse lookups on the retained warmup/sample result
  and unchanged source. This is a modified isolation caller, not the identical
  historical replay.
- **Recreation / identities:** Build that caller against the frozen `b6c1953`
  Standard core, then copy the entire execution graph and replace only the core
  DLL with selected-current Standard. Build Lean separately with
  `SWIFTCOLLECTIONS_DISABLE_MEMORYPACK` and the matching Lean dependencies.
  Commands are
  `dotnet build <probe.csproj> -c Release -p:UseLocalLsfStack=false -p:CoreAssemblyPath=<frozen-core.dll> -o <baseline-graph>`
  and the corresponding `ReleaseLean` build against current Lean. Run each fresh
  process with `$env:DOTNET_TieredCompilation='0'` and
  `cdb -logo <fresh-log> -cf <collector-commands> dotnet --fx-version 10.0.10 <caller.dll> <fresh-result.json>`.
  Use the calibrated AV/CLR commands above; add
  `sxe -c ".echo PROCESS_EXIT; .lastevent; q" epr` before `g` to capture the
  target exit code. Retain CPU2, normal priority and workstation/nonconcurrent
  GC. SDK/runtime, Windows build and published dependency versions match the
  recorded environment above. Standard caller SHA256 is
  `6F8AFE96BDE6BD71B5A163CAC429667AC88E0623FF03B6C97F0AD29B7AA05025`; Lean
  caller is `C8728641E399027CD272D9E878A22EB825E8897614DB48C18792A623150F87F9`.
  Standard baseline/current core hashes remain `BAE679CB…5AFD` / `807CDB2E…529E`
  as recorded in full above; current Lean core is
  `B634802D6B0847A2095F8B67F212EA2711B58981293F3CC440F91642CBEFB75D`. Inspect
  loaded caller/core hashes, runtime, complete results and target exits;
  debugger success alone is insufficient. Lean must not load MemoryPack.Core.
- **Bounded results:** Execute two frozen-baseline, two current Standard and two
  current Lean processes, stopping baseline repetition on the useful second-run
  failure. Baseline first run passes five rows; second captures a first-chance
  native AV and exits `0x80131506` (internal CLR error), with no completed
  result. Both current Standard and both current Lean processes exit zero and
  pass five rows each: 20 current rows / 804 current decodes. Across all
  completed processes, 25 rows pass; the failed baseline's partial work is not
  accepted. All six target exits and collector output were inspected, with no AV
  or unhandled CLR event in the passing processes. Both disposable caller builds
  finish with zero warnings/errors. No diagnostic timing is used as performance
  evidence. No additional replay, runtime/CPU sweep or stress loop is needed for
  this disposition.
- **New capture / limits:** This recurrence needs no preceding Dictionary or
  MemoryPack serialization/deserialization. The Standard graph still contains
  MemoryPack; this does not prove its initialization or earlier historical IO
  harmless, or assign one cause to every incident. The first-chance snapshot
  again faults at `Buffer::BulkMoveWithWriteBarrier+0x100`, this time under
  `System.Text.Json.ReadStack.Push` while reading nested key/value state, before
  this decode's collection restoration. Destination `0xc07d57cd80` is writable
  stack below heap-low `0x1ebe1800000` (high `0x1fbd9400000`), length `0x70`,
  yet the not-in-heap flag is zero. The invalid read `0x1eb5a937b39` equals
  card-table base `0x1eb4283d040` plus `(destination >> 11)`. Offline exception
  and default contexts agree. These inconsistent native facts do not identify
  the cause or prove a runtime/environment defect.
- **Source review / decision:** Independent review plus coordinator checks find
  no concrete misuse in synchronous ref-reader delegation, bounded JSON metadata
  stack buffers, managed-array forward/reverse reconstruction or authored
  MemoryPack state IO. The eight-byte comparer header view is consumed
  immediately after `GetSpanReference(8)` and is not retained or written; the
  [MemoryPack 1.21.4 reader](https://github.com/Cysharp/MemoryPack/blob/1.21.4/src/MemoryPack.Core/MemoryPackReader.cs#L68-L137)
  supplies the requested contiguous bytes or throws. This fixture does not use
  unsafe string hashing. The review does not audit the entire CLR or provider
  implementation or exclude an intermittent defect. Close as a documented
  no-source-change decision under the user's bounded scope. No production
  workaround, API/dependency/CI change, maintained probe or regression test is
  justified without a source defect. Current controlled executions pass; they do
  not establish that the historical failure is fixed.
- **Reopening / next action:** No further investigation or implementation work
  is queued. Reopen for a supported current-code recurrence with exact input,
  loaded identities and useful original context, a concrete source
  memory/lifetime defect, or evidence that changes release-risk assessment.
  Promote a confirmed collection/provider defect to its owning issue tracker and
  add a failing production-behavior regression before fixing it. Keep the
  unexplained historical failure visible in release review; closure ends this
  investigation, not the underlying uncertainty.

### SC-Benchmark-005 - Comparer-persistence hot timing acceptance

- **Discovered / closed:** 2026-11-19 / 2026-11-20; no actionable hot-path
  regression identified. The user explicitly accepted the remaining timing
  uncertainty and closed
  [SC-Issue-021 Phase 1](done/2026-11-20-comparer-persistence-plan.md#phase-1-execution-record--2026-11-19)
  performance acceptance after the bounded investigation below. This is a
  documented no-change decision, not a collection algorithm fix or a claim of
  universal timing equivalence. Subsequent implementation and verification are
  recorded in the completed comparer-persistence plan linked above.
- **Source / environment:** Clean baseline
  `bf69e2152dff02f5f9f3ad335afcddf7560b4e81` versus its uncommitted Phase 1
  sorted-list persistence implementation; published dependencies,
  Release/ReleaseLean net10.0, SDK 10.0.302 / runtime 10.0.10, Windows x64,
  Intel Core i7-9700K. Disable tiering with `DOTNET_TieredCompilation=0`, use
  normal process priority and pin logical CPU2. No competing builds, tests,
  Unity editor or profiler runs during timings.
- **Portable workload:** Build a caller once per package flavor against the
  baseline, then use the exact same caller and dependency graph with only the
  candidate core DLL substituted. Verify actual loaded hashes against the
  intended source output before interpreting results. Measure an Add/Remove pair
  for an empty default-int list (temporary 1), an ordinal string list of 128
  preallocated `k00000000` through `k00000127` values (temporary `z-temporary`),
  and 4096 noncomparable reference tokens with integer keys 0–4095 and a
  capturing descending comparer (temporary key 4097). Construct through the real
  enumerable constructor, then reserve Count+1 capacity. A control performs
  Contains(32768)+Contains(-1) on integers 0–65535. Assert unchanged full
  contents/order/count after every interval and successful operation results. No
  setup, growth or correctness assertion is timed.
- **Controls / completeness:** Each flavor's four-process A/B/B/A comparison
  uses five balanced samples, 100000 warmup pairs and 2000000 measured pairs per
  interval: 80 intervals. Additional same-process controls load baseline and
  candidate into isolated assembly contexts, invoke the unchanged caller's
  operation delegates, alternate A/B interval order and reverse load order in
  fresh processes. Twenty samples x four cases x two variants = 160 intervals
  per process, with 100000 warmup and 1000000 measured pairs. All completed
  accepted intervals allocate zero bytes and validate behavior. An earlier
  disposable Lean hot-control build resolved a Standard DLL from copied files
  before its correct HintPath; exclude those runs. Fresh explicit-input probe
  builds and loaded-hash checks correct that setup error. The full serializer
  measurements and shipped build/Unity checks used the correct graphs.
- **Standard observations:** With identical caller binaries, separate-process
  median ratios span 0.956–1.090 across these cases. Paired token mutation is
  101.45 -> 106.37 ns in A-first loading (+4.9%) and 101.69 -> 109.68 ns in
  B-first loading (+7.9%). The Contains control changes from 79.94 -> 79.60 ns
  to 74.54 -> 86.18 ns (+15.6%) with load order; all paired ratios in that
  adverse process exceed 1.07. Retain these adverse executions rather than
  averaging them into a neutral claim.
- **Corrected Lean observations:** Paired token mutation is 146.52 -> 141.85 ns
  (-3.2%) in A-first loading and 143.08 -> 147.84 ns (+3.3%) in B-first loading.
  Contains is 83.73 -> 77.14 ns (-7.9%) and 80.43 -> 84.22 ns (+4.7%). Hashes
  match the intended Lean outputs and no MemoryPack runtime is loaded. These
  valid replacements demonstrate sensitivity without establishing equivalence.
- **Identical-core calibration:** Four additional processes load baseline twice
  or candidate twice, with both load orders: 640 complete/zero-allocation paired
  intervals. Same-core medians vary by +6.7%/-6.1% between contexts. Baseline
  Contains alone changes 75.44 -> 80.22 ns (+6.3%) and 78.02 -> 74.05 ns (-5.1%)
  with load order. This establishes context-position sensitivity in the
  experiment, not a feature-equivalence result; do not subtract these values
  from adverse candidate observations to declare a pass.
- **Attribution / limits:** Source adds no owner fields, registry access, lock,
  wrapper or branch in ordinary lookup/mutation. Separate FullOpts JIT capture
  (``DOTNET_JitDisasm='SwiftCollections.SwiftSortedList`1:*'`` and
  `DOTNET_JitStdOutFile=<file>`) shows equal integer Search/Insert/RemoveAt and
  reference Insert/RemoveAt instruction streams after normalizing ASLR
  addresses. Shared-reference Search becomes 193 -> 186 bytes through a shorter
  generic-dictionary displacement (0x90 -> 0x70) and branch. These observations
  rule out added algorithmic instructions in those methods; they do not prove
  identical cycle cost or establish the timing cause. Range overlap alone is
  insufficient to close the signal.

#### Isolation pass — 2026-11-19

**Result:** Reproduced the timing sensitivity with direct callers. A controlled
JIT-policy change reverses the Standard token-pair comparison while caller,
library, inputs and storage shape stay fixed. No collection algorithm defect or
justified runtime correction was found. The precise hardware mechanism and
hot-path acceptance remained unresolved at the 2026-11-19 checkpoint; neither a
speedup nor equivalence is claimed. The 2026-11-20 disposition below closes the
gate without requiring hardware attribution.

**Frozen inputs.** Baseline remains `bf69e2152dff02f5f9f3ad335afcddf7560b4e81`;
current is committed `cbd9a734082d7a002699766937b79dc547a8cebb`. Published
dependencies are Chronicler.Core/Core.Lean and Diagnostics 1.0.0, plus
MemoryPack.Core 1.21.4 only for Standard. Host, runtime, CPU2 affinity, normal
priority and disabled tiering are unchanged from the original record. Core
net10.0 DLL SHA256 and byte lengths:

| Configuration | Baseline                                                                    | Current                                                                     |
| ------------- | --------------------------------------------------------------------------- | --------------------------------------------------------------------------- |
| Release       | `CF36150682C069F579ED75B9F74CF081E3889735118F91D6245AD9C4145AFC9D` / 192000 | `319BD4D15FAD879D836F38AAAD8721820738909FDC7EE162914CD7BB20E95C1B` / 208384 |
| ReleaseLean   | `7DBAEC5BFDA34A76B8BD7AD085F02EEBA296655E1AA75ECDC418616DC9D27B1A` / 170496 | `84F570D6986947319B3433CEEF79444FDF86AB2CAD7C47372481C22503814C4F` / 184832 |

**Measurement corrections.** The historical probe did `File.ReadAllBytes` to
hash each DLL before constructing owners, then forced GC after warmup. That
observer allocation changes the startup heap; rewarming after GC was missing. It
also invoked an `Action` per operation. Earlier Lean callers, including the
first direct-caller draft, lacked explicit compiler optimization for the custom
configuration. Retain their results as unoptimized-caller diagnostics, not
optimized Lean acceptance. This is separate from the previously excluded wrong
DLL graph. The maintained benchmark project already enables optimization.

The corrected disposable caller uses direct concrete typed loops, explicit
`Optimize=true`, explicit compile/reference inputs, no assembly-load contexts or
per-operation `Action` wrapper, and provenance IO only after all timing. It
performs one setup GC, then warmup, and no scheduled GC between intervals.
Read-only owners keep natural Count/Capacity 65536/Offset 0 for integers and
4096/4096/0 for strings/tokens; mutation owners reserve Count+1 before warmup.
The first draft unnecessarily doubled read-only capacity and is excluded from
the corrected storage-shape comparison. The token mutation shape is unchanged
between drafts: 4096 items, Capacity 8192, Offset 2048.

**Portable experiment.** Build one net10.0 optimized caller per flavor against
the exact baseline DLL, using matching published dependencies. Copy its whole
graph twice and replace only the candidate core; verify hashes before launch and
actual loaded hashes after measurement. Use the pair cases above and these
additional workloads:

- Search every key through the existing benchmark's seed123 shuffled range:
  65536 integers, 4096 ordinal strings, or 4096 descending reference tokens. Sum
  returned ranks; require `N*(N-1)/2` per batch and separately verify every rank
  and full ordered contents.
- Churn four of 128 ordinal strings or 128 of 4096 descending tokens. Remove
  each selected original, insert a preallocated replacement above the original
  key range, then remove replacements and restore originals in reverse order.
  Each batch restores all logical contents and performs four mutations per key.
  No sorting, allocation, growth or setup is timed.

Use a no-inline whole-loop method, such as
`for (int i=0;i<repeats;i++) { owner.Add(temporary); score += owner.Remove(temporary) ? 1 : 0; }`.
Require successful results and unchanged complete contents outside timing. Run
each case in a fresh process, baseline/current/current/baseline order, seven
intervals each, with `DOTNET_TieredCompilation=0`. Pair repetitions are 8M for
empty integers and 2M otherwise; full-search repetitions are 32 integer batches
or 512 string/token batches. Warmups are 100000 pairs, four integer search
batches or 16 string/token search batches. Per-interval validation makes these
warm-cache workloads. Reject timed allocations, any GC, wrong CPU, changed
owner/lookup-array addresses or capacity, and failed checksums/statuses.

All **72 processes / 504 intervals** complete with those checks. Each flavor has
one identical caller hash across its two cores; Lean loads no MemoryPack
runtime. Churn initially ran only about 23–46ms per interval. Lengthening it
eightfold, without rebuilding callers, gives another **16 processes / 112
intervals**, all passing the same checks. Longer churn uses 262144 string or
2048 token batches, with 1024/8 warmup batches respectively. Those longer
results supersede the short churn estimates below. Values are percentage changes
in the mean of the two process medians, current versus baseline:

| Direct caller                 | Standard wall ns | Standard thread cycles | Lean wall ns | Lean thread cycles |
| ----------------------------- | ---------------: | ---------------------: | -----------: | -----------------: |
| Empty integer Add/Remove      |            +3.8% |                  -0.6% |        +1.0% |              -0.3% |
| Integer Contains hit+miss     |            -5.6% |                  +0.8% |        +0.4% |               0.0% |
| Ordinal string Add/Remove     |            +1.7% |                   0.0% |        +2.1% |              -0.9% |
| Descending token Add/Remove   |            -7.9% |                  -6.1% |        -2.1% |              -0.1% |
| Full shuffled integer Search  |            -1.1% |                  -0.3% |        -1.0% |               0.0% |
| Full shuffled ordinal Search  |            +0.6% |                  -0.2% |        +2.8% |              +0.4% |
| Full shuffled token Search    |            -2.9% |                  +0.5% |        +0.3% |              -0.1% |
| Longer ordinal string churn   |            -1.3% |              **+3.4%** |        -7.2% |             -12.0% |
| Longer descending token churn |            -1.5% |                  -1.1% |        -4.0% |              -0.1% |

Thread-accounted user+kernel cycles use
[QueryThreadCycleTime](https://learn.microsoft.com/en-us/windows/win32/api/realtimeapiset/nf-realtimeapiset-querythreadcycletime);
they are not retired instructions, branch/cache counters, or a direct frequency
measurement. Wall time and cycles can move differently. In particular, the
Standard string-churn cycle increase persists in both longer candidate processes
(baseline 275.29/275.69 versus current 286.71/282.87 cycles per mutation) even
though wall time improves. Do not erase it with aggregate or overlapping
wall-time results.

**JIT-policy counterfactual.** With the exact corrected caller/core graphs, set
only `DOTNET_JitAlignLoops=0` and repeat token-pair ABBA for each flavor: **8
processes / 56 intervals**, all checks pass. Standard changes from 397.51 ->
373.10 cycles/pair (-6.1%) under default policy to 366.94 -> 401.26 (+9.4%) with
alignment disabled; wall time changes from 140.15 -> 129.07ns (-7.9%) to 126.57
-> 140.55ns (+11.1%). Lean changes from 397.45 -> 397.20 cycles to 398.02 ->
397.82 (about -0.1% in either policy). The optimized Standard first draft had
instead measured 372.01 -> 400.15 cycles (+7.6%); those adverse executions
remain evidence of context sensitivity.

Separate FullOpts captures set `DOTNET_JitDisasm='*RunTokenPair* *Search*'`,
`DOTNET_JitDisasmWithCodeBytes=1`, `DOTNET_JitDisasmWithAlignmentBoundaries=1`
and `DOTNET_JitStdOutFile=<file>`, once under each policy and graph. Within each
graph, normalized instructions and method-relative offsets are unchanged: caller
143 bytes, Standard Search 193 baseline/186 current, Lean Search 186 both. The
Standard generic-dictionary displacement is now 0x88 -> 0x68, again shortening
the initial branch. Boundary annotations move by 16 modulo 32 for the Standard
baseline caller, Standard current Search, and both Lean Search captures. This
verifies different native placement in those diagnostic executions. Other
methods can move too; separate diagnostic/timing launches do not establish a
particular responsible branch, cache conflict or Intel JCC-erratum mechanism. No
runtime alignment option is proposed for consumers.

**Observer counterfactual.** Hold one current core and optimized caller fixed,
then vary only a touched startup byte array, retained through construction of
the natural-capacity integer Contains owner and released afterward. Use zero,
baseline DLL length and current DLL length, then reverse size order; seven
2M-pair intervals per process. Standard repeats with and without 100000-pair
post-GC rewarming (12 processes/84 intervals); optimized Lean uses rewarming
(6/42). All rows pass behavior/zero-allocation/no-GC/CPU/address checks.
Standard zero allocation usually costs about 327 cycles/pair (one rewarm process
346), either DLL-sized allocation about 300–305. Lean is about 326 versus
298–301. Thus setup allocation affects this fixed-binary experiment, but the two
actual DLL sizes behave similarly: **their size difference is not established as
the original regression cause**. Virtual addresses do not prove physical cache
sets.

**Verification and limits.** Fresh package-mode solution builds and tests run
serially with
`dotnet build SwiftCollections.slnx -c <Release|ReleaseLean> -p:UseLocalLsfStack=false`,
then
`dotnet test SwiftCollections.slnx -c <configuration> --no-build -p:UseLocalLsfStack=false`.
Both builds have zero warnings/errors, compile core and companion at
netstandard2.1 and net10.0, and produce all four package variants. Standard
tests pass 1704 core/43 companion; Lean passes 1635/43. No runtime or maintained
test changes were needed. Independent reviewers checked the driver, matrix
provenance and interpretation. `wpr -start CPU -filemode -instancename SC005`
fails with 0xc5585011, "Failed to enable the policy to profile system
performance"; status confirms no recording. No profiling policy was changed.
There is no branch/cache trace, settled default-tiered-PGO, other CPU/OS or
Unity timing claim.

`git diff --check` passes. Whole-file Prettier checks fail for all four changed
documentation files at both committed HEAD and the working tree; broad
historical reflow is outside this change. No introduced build or test failure
remains.

**Closure disposition — 2026-11-20:** The user explicitly accepts the remaining
native-context timing uncertainty and closes the Phase 1 performance gate. The
fixed persistence-boundary cost was already accepted; this additional
disposition covers the retained adverse hot-control observations, including the
Standard token-pair reversal and +3.4% string-churn cycles despite -1.3% wall
time. No stable, meaningful caller elapsed-time regression attributable to the
implementation was established. No added algorithmic work in inspected
lookup/mutation methods, warm allocation or behavioral failure was found. The
complete evidence and exclusions remain above; this decision does not establish
equal cycles, a hardware cause or a universal speedup.

Stop hardware-counter escalation and broad microbenchmark reruns for this
signal. No source padding, forced alignment, comparer replacement or Search
restructuring is justified. Further optimization should target reproducible cost
in the implemented persistence boundary or representative callers. See the
[Phase 1 closeout](done/2026-11-20-comparer-persistence-plan.md#phase-1-closeout--2026-11-20).

**Reopening criteria:** Reopen for a repeatable, meaningful regression in a
representative caller under recorded runtime settings, equivalent inputs and
verified package graphs, attributable to this implementation; or a newly
demonstrated hot allocation, behavioral defect or added algorithmic work. A
changed runtime/package/comparison implementation warrants fresh validation, but
a different isolated timing or native layout alone does not invalidate this
decision.

If callers or libraries are rebuilt, first reproduce the adverse signal in a
fresh same-caller ABBA comparison before profiling. Recreating the logical
workload alone does not preserve the native placement of these disposable
binaries.

### SC-Benchmark-004 - Sorted-state order validation

- **Discovered / closed:** 2026-11-18 / 2026-11-19; automatic validation and
  sorting rejected by contract decision for
  [SC-Issue-010](issue-tracker.md#resolved-issues).
- **Owner / environment:** `SwiftSortedList<int>.State`; baseline
  `31cd3eec80dff86b95f8cab621cc7ca281b4e7cc` versus the rejected SC-Issue-010
  prototypes. Published dependencies (`UseLocalLsfStack=false`),
  Release/net10.0, SDK 10.0.302, runtime 10.0.10, Windows x64, Intel Core
  i7-9700K.
- **Workload:** Construct a sorted list from an ascending item state containing
  integers 0..Count-1. Compared payloads are the original unchecked copy,
  unconditional sorting of the copied live segment, and the proposed adjacent
  order check with sorting only on inversion. The source state is reused;
  constructed objects escape to a static sink. Four complete processes per
  payload run in baseline/sort/checked/checked/sort/baseline order, twice.
- **Procedure:** Prime state and enumerable constructors for 500 ms before
  cases; warm each case for 2,000 calls. Time 100,000 calls for counts 0/1/8,
  20,000 for 128, and 2,000 for 4,096, excluding setup and forced GC. Stopwatch
  measures elapsed time; current-thread allocated bytes measure allocation.
  Loaded assembly hashes distinguish payloads. The probe also covers reversed
  state and the existing correct enumerable constructor. Old reversed-state
  restore is omitted from latency comparisons because it does not produce the
  default-ordered output required by the sorting controls.

| Ordered item count | Old unchecked median (range), ns | Unconditional sort median (range), ns | Proposed check median (range), ns | Bytes/call, all |
| ------------------ | -------------------------------- | ------------------------------------- | --------------------------------- | --------------- |
| 8                  | 40.37 (39.24–42.37)              | 52.36 (51.86–64.62)                   | 35.78 (30.95–36.70)               | 112             |
| 128                | 63.34 (62.99–70.77)              | 324.06 (320.19–356.54)                | 102.08 (95.64–103.17)             | 592             |
| 4,096              | 827.00 (817.70–834.60)           | 17,976.53 (17,770.85–18,689.55)       | 2,256.15 (2,171.35–2,277.50)      | 16,464          |

- **Rejected behavior / cost:** The validation prototype performs Count-1
  adjacent comparisons on ordered nonempty state and sorts on inversion. It
  avoids an unconditional full sort, but retains about 1.43 microseconds of
  validation cost for 4,096 integers compared with the old unchecked copy.
  Allocation is unchanged. Reversed 4,096-item prototype restore measured median
  34,990.05 ns (34,177.90–35,116.00), versus 35,063.70 ns (34,466.00–35,208.65)
  for the existing correct enumerable constructor in the same payload. Both
  allocate 16,464 bytes and produce ascending values.
- **Decision / limits:** Preserve the original copy-only trusted-state restore;
  neither prototype is part of the final change. The original reversed payload
  is incompatible with default binary lookup, rather than an input restore must
  normalize. Callers choose
  `new SwiftSortedList<T>(state.Items, desiredComparer)` when ordering must be
  established. The prototype's comparison-failure atomicity and validation
  promises are also withdrawn. Measurements describe local integer
  microbenchmarks, not other types, comparer costs, JITs, architectures or full
  deserialization pipelines. No new timing claim is made for the
  documentation-only disposition.
- **Reopen:** An agreed requirement to accept unordered state, persist comparer
  identity, or validate an external trust boundary warrants separately scoped
  API and performance work. There is no pending acceptance of validation cost.

For a focused rerun, use the console setup in the
[issue tracker](issue-tracker.md#reproducing-active-issues) against each source
version with the inputs and procedure above:

```powershell
dotnet run --project ../SwiftCollectionsRepro/SwiftCollectionsRepro.csproj -c Release -p:UseLocalLsfStack=false
```

### SC-Benchmark-003 - List-to-list copy capacity

- **Discovered / closed:** 2026-11-18; fixed and verified during
  [SC-Issue-009](issue-tracker.md#resolved-issues).
- **Owner / environment:** `SwiftList<int>.CopyTo(SwiftList<int>)`; baseline
  `63c6124572b703680e797770ed0d93e0eb88bea0` versus SC-Issue-009. Published
  dependencies (`UseLocalLsfStack=false`), Release/net10.0, SDK 10.0.302,
  runtime 10.0.10, Windows x64, Intel Core i7-9700K.
- **Measured controls:** Sources contain ascending integers from zero. Reuse a
  target at Capacity 128 for Count 100, or an exact Capacity 20 target for
  Count 17. The growth control creates a Capacity 64 target and copies Count 100
  on every invocation, retaining the resulting object in a static sink. It times
  both target construction and copying. All final counts, capacities and values
  were checked. These inputs complete correctly in both versions; the broken
  Count 100 to Capacity 0 case is a correctness regression, not a latency
  baseline.
- **Procedure:** Warm all three controls for 20,000 calls each; measure reuse
  controls for 1,000,000 calls and growth for 100,000. Setup and forced GC are
  outside the timer. Use Stopwatch and current-thread allocated bytes. Four
  complete processes per version ran in ABBAABBA order, with loaded assembly
  hashes distinguishing the payloads.

| Control                                | Baseline median (range), ns/call | Fixed median (range), ns/call | Bytes/call, both |
| -------------------------------------- | -------------------------------- | ----------------------------- | ---------------- |
| Reuse Count 100 / Capacity 128         | 18.70 (18.66–18.94)              | 18.96 (18.71–19.15)           | 0                |
| Reuse Count 17 / Capacity 20           | 10.08 (9.93–10.88)               | 10.12 (9.75–10.37)            | 0                |
| Construct Capacity 64 + copy Count 100 | 95.02 (86.92–100.47)             | 98.16 (93.59–101.31)          | 856              |

- **Disposition / limits:** Guard insufficient capacity before requesting the
  full source count through EnsureCapacity. This obtains sufficient capacity in
  one growth and preserves fitting non-power-of-two buffers. Exact-fit,
  self-copy and empty-to-empty regressions also verify storage reuse. These
  local timings show close control medians and unchanged allocation; they do not
  prove zero instruction cost or cross-platform throughput equivalence. Reopen
  for a repeatable material regression on representative copies or a supported
  JIT/architecture. No new runtime helper or dependency was added.

For a focused rerun, use the console setup in the
[issue tracker](issue-tracker.md#reproducing-active-issues) against each source
version, with the controls and counts above:

```powershell
dotnet run --project ../SwiftCollectionsRepro/SwiftCollectionsRepro.csproj -c Release -p:UseLocalLsfStack=false
```

### SC-Benchmark-002 - Exact Int16 normalization

- **Discovered / closed:** 2026-11-18; verified during
  [SC-Issue-007](issue-tracker.md#resolved-issues).
- **Owner / revisions:** `SwiftShortArray2D.Normalize`; baseline
  `0ffc9d76dc0ed3bfbf485cc7491c4cd66f01501e` versus the SC-Issue-007 integer
  interpolation. Published dependencies (`UseLocalLsfStack=false`),
  Release/net10.0, SDK 10.0.302, runtime 10.0.10, Windows x64, Intel Core
  i7-9700K.
- **Workload:** Normalize 32 retained square arrays per timed batch. Varying
  inputs repeat `[-1024,-512,0,512,1024]` and map to `[-100,100]`; constant
  inputs contain 5 and map to `[0,100]`. These are equal-output controls on this
  host; all output cells were checked against the five exact values or zero. The
  constant control uses min=0 so the old NaN conversion happens to agree with
  the corrected policy on the recorded runtime. It does not validate the old
  policy on other platforms.
- **Measurement:** Four complete processes per payload in ABBAABBA order, with
  loaded assembly hashes checked. Before cases, warm 100,000 alternating
  normalizations of the five-value identity map and a constant 8x8 array. Each
  case then warms 32 batches. Copy the original input into each backing array
  before each batch, outside the timer. Time only the 32 Normalize calls using
  Stopwatch. Timed batches are 20,000 for empty input; otherwise
  `Clamp(4000000 / (32 * cellCount), 32, 20000)`. Forced GC occurs before
  timing. `GC.GetAllocatedBytesForCurrentThread` reported **0 B/call** for every
  case.

| Side / input            | Baseline median (range), ns/call   | Integer median (range), ns/call    |
| ----------------------- | ---------------------------------- | ---------------------------------- |
| 0 / target `[-100,100]` | 12.12 (12.11–12.17)                | 13.73 (13.62–13.84)                |
| 0 / target `[0,100]`    | 11.34 (11.31–11.36)                | 12.80 (12.74–12.91)                |
| 8 / varying             | 941.63 (938.50–944.45)             | 667.82 (667.14–675.36)             |
| 8 / constant            | 929.36 (925.68–932.18)             | 470.48 (469.80–489.82)             |
| 64 / varying            | 12,739.89 (12,685.55–12,780.08)    | 12,422.41 (12,389.84–12,474.80)    |
| 64 / constant           | 12,704.06 (10,620.61–12,768.36)    | 10,003.91 (5,844.63–10,082.71)     |
| 256 / varying           | 155,058.25 (144,043.75–155,569.63) | 155,213.38 (154,727.05–169,129.10) |
| 256 / constant          | 155,024.60 (143,949.22–155,663.18) | 63,102.00 (63,001.95–63,416.21)    |

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
  [SC-Issue-006](issue-tracker.md#resolved-issues).
- **Owner / revisions:** `SwiftArray3D<T>.Resize`; unfixed source
  `9ccb87724652516232322f3fe5ab0b173b493c4b` versus the SC-Issue-006 changes.
  Published dependency mode (`UseLocalLsfStack=false`), Release/net10.0, SDK
  10.0.302, runtime 10.0.10, Windows x64, Intel Core i7-9700K.
- **Workload:** A pair of resizes, from `(s,s,4)` to `(s,s+1,4)` and back. Each
  process first warmed 100,000 mixed small/empty resize cycles, then each timed
  case had 20,000 warmup pairs. Empty/small cases timed 1,000,000 pairs; the
  larger case timed 20,000. Stopwatch elapsed time and
  `GC.GetAllocatedBytesForCurrentThread` excluded setup, warmup and forced GC.
  Four complete processes per version ran in balanced order
  baseline/minimal/final/final/minimal/baseline, repeated twice. The surrounding
  probe also exercised 2D/3D construction and state restore at sides 0, 8 and
  32; constructed/restored objects escaped to a static sink. Loaded assembly
  hashes distinguished all three payloads.

| Resize pair | Unfixed median (range), ns   | Initial validation median (range), ns | Final median (range), ns     | Bytes/pair, all versions |
| ----------- | ---------------------------- | ------------------------------------- | ---------------------------- | ------------------------ |
| `s=0`       | 39.42 (37.78–40.02)          | 47.51 (47.32–47.94)                   | 48.83 (46.56–53.40)          | 48                       |
| `s=8`       | 595.82 (587.47–604.58)       | 733.38 (707.27–741.65)                | 595.97 (588.05–600.67)       | 2,224                    |
| `s=32`      | 9,361.13 (9,302.24–9,678.95) | 10,832.10 (10,754.15–10,901.15)       | 9,127.72 (9,036.97–9,157.62) | 33,328                   |

- **Diagnosis / disposition:** Inlining the new guards into Resize changed JIT
  register allocation and destination-length loads in the per-cell loop. Warmed
  Tier1 disassembly retained the same guarded `GetIndex` devirtualization; lost
  devirtualization was ruled out. A separate non-inlined validation helper
  recovered nonempty copy throughput while retaining the original allocation
  expression and copy semantics. The unchecked allocation product is safe only
  after exact checked validation: a zero axis gives zero despite intermediate
  wrap; otherwise every positive intermediate fits Int32. Keep validation before
  allocation. No span or aggressive-inlining experiment remains in production.
- **Correctness control:** The dimensional regression suite verifies overflow,
  negative and zero axes, coordinate layout, serialization, ownership and
  failure atomicity. Timed resize fixtures used default-initialized data and
  returned to their original shape; allocation matched across the constructor,
  restore and resize cases. Empty constructor, restore and resize calls still
  pay constant validation costs; the empty resize pair above adds about 9 ns.
  This is not a zero-overhead claim.
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
[issue tracker](issue-tracker.md#reproducing-active-issues), reference each
source version in turn, and run with the same published-dependency command:

```powershell
dotnet run --project ../SwiftCollectionsRepro/SwiftCollectionsRepro.csproj -c Release -p:UseLocalLsfStack=false
```

Use `SwiftArray3D<int>` with the shapes, iteration counts and warmup above;
measure both resizes together, retain the instance, and check its final shape
and cell values outside the timed loop. Preserve every run and reverse version
order. Warm both nonempty and empty paths before timing so the first empty case
does not define the entire tiered profile. For exact-type JIT inspection, the
runtime's `DOTNET_JitDisasm=*Resize` setting captures Tier0 and warmed Tier1
code.

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
