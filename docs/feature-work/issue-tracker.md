# Issue Tracker

## Purpose

Track correctness, determinism, API, documentation, build, tooling and test
reliability concerns owned by SwiftCollections, including its FixedMathSharp
companion, with a compact index of verified resolutions or documented
investigation closures. Keep substantial implementation work in focused
feature-work plans.

## Tracker Rules

- Issue IDs use `SC-Issue-NNN`. The next available ID is `SC-Issue-006`.
- Assign an ID at intake and preserve it through closure; never delete or
  reuse IDs. Check Git history before advancing or repairing the counter.
  Unknown historical IDs stay reserved rather than acquiring an inferred cause.
- Record discovery dates, status, priority, affected owners, reproducible
  evidence, impact and the next useful action. Distinguish confirmed defects
  from unverified concerns.
- Keep active entries focused and self-contained. Link active plans rather
  than duplicating their work.
- After verification, replace the active entry with a compact `Resolved Issues`
  row: the same ID, resolution/closure date, brief outcome and an evidence link.
  Keep detailed root causes, decisions and validation in an existing completion
  record, regression or fix commit rather than duplicating them in this index.
- Distinguish verified fixes, mitigations and investigations closed without a
  reproduced defect. A closed investigation does not establish a runtime fix
  or an attributed cause; preserve its limits and recurrence guidance.
- Resolved means verified in source or closed by documented decision, not
  released. Add release/version claims only when separately confirmed. Retain
  prior evidence when new findings change an issue's status.
- Preserve existing issue anchors when compacting entries so inbound links
  continue to reach their issue. New rows may use a short `sc-issue-nnn` anchor.
- Use repository-relative links and portable commands. Ignored artifacts are
  supporting evidence; continuing an issue must not require their survival.
- Preserve reachable line, branch and method coverage. Do not conceal gaps
  with exclusions, weaker assertions or retries until a flaky result passes.
- Use `UseLocalLsfStack=true` for coordinated unreleased source validation;
  released packages remain the promotion/release gate. Validate Release and
  ReleaseLean and the affected core or companion assembly independently.
- Keep performance claims measured. This tracker does not replace benchmarks,
  implementation plans, tests or release notes.

## Active Issues

None.

## Resolved Issues

Dates reflect recorded resolution/closure, which can precede the commit date.
Evidence links retain the committed root causes, regressions and validation;
they do not claim a fresh rerun or package release.

| Issue | Resolved/closed | Outcome | Evidence |
| --- | --- | --- | --- |
| <a id="sc-issue-005---cold-value-key-lookup-boxes-a-default-key"></a>SC-Issue-005 | 2026-10-30 | Replaced dictionary/set nullability initializers with cached type metadata, removing the 24-byte first integer/enum lookup allocation exposed by Gravitas pair teardown. Reference, nullable and generic-struct semantics are preserved without a warmup workaround. | [Cold lookup and removal regressions](../../tests/SwiftCollections.Tests/Collection/SwiftFirstLookupAllocation.Tests.cs) |
| <a id="sc-issue-004---non-generic-list-enumeration-rejects-the-final-item"></a>SC-Issue-004 | 2026-10-28 | Corrected list/queue final-item guards and reproduced position-contract defects across the other affected collections and dictionary views; preserved generic iteration and full reachable coverage. | [Closeout and regressions](done/2026-10-28-enumerator-contract-closeout.md) |
| <a id="sc-issue-003---hash-set-lookup-boxes-value-keys-in-unoptimized-builds"></a>SC-Issue-003 | 2026-10-26 | Removed value-key boxing from all three hash-set lookup APIs, including Debug; retained null/error contracts and added warmed allocation regressions. | [Closeout](https://github.com/mrdav30/SwiftCollections/blob/2b4894f87980147934b1eb74603029e04fb84ec8/docs/feature-work/issue-tracker.md#sc-issue-003---hash-set-lookup-boxes-value-keys-in-unoptimized-builds) |
| <a id="sc-issue-002---collision-probes-stop-before-reaching-stored-entries"></a>SC-Issue-002 | 2026-10-07 | Completed dictionary/set collision-probe periods for insertion, lookup and removal; deterministic wrapped-chain and trimmed-table regressions passed. | [Closeout](https://github.com/mrdav30/SwiftCollections/blob/5e0c4e0830342508c3fb87c2fe43cf7c30167522/docs/feature-work/issue-tracker.md#sc-issue-002---collision-probes-stop-before-reaching-stored-entries) |
| <a id="sc-issue-001---pool-finalizer-coverage-depends-on-incidental-gc"></a>SC-Issue-001 | 2026-10-07 | Replaced incidental-GC coverage with isolated real-GC, mutation-checked pool-finalizer regressions; closed a test reliability gap without a production cleanup change. | [Closeout](https://github.com/mrdav30/SwiftCollections/blob/5e0c4e0830342508c3fb87c2fe43cf7c30167522/docs/feature-work/issue-tracker.md#sc-issue-001---pool-finalizer-coverage-depends-on-incidental-gc) |

SC-Issue-005 was reproduced on `47a184b1e6dc5480e65295f2228cf8d77b647422`:
construct a fresh `SwiftDictionary<int, PrivateValue>` or
`SwiftHashSet<PrivateEnum>`, populate with `Add`, then measure the first lookup
and removal. Both regressions allocate 24 bytes before the fix and zero after.
The old `default(T) is null` static initializer boxes a nonnullable value when
the first lookup reads it; construction and insertion need not read that flag.
The metadata replacement remains cached and adds no per-lookup reflection or
instance state. Private test types retain a cold initializer independently of
other tests; generic-struct, nullable generic-struct and reference cases also
pass.

Frozen local-stack validation on Windows x64/.NET 10.0.10 passes core Release
1,406, ReleaseLean 1,378 and Debug 1,404 tests, plus all 43 FixedMathSharp
companion tests in each release profile. Raw OpenCover counts retain 100%
reachable line, branch and method coverage in every collected profile. The
existing Gravitas 2D first-owner deactivation guard passes unchanged at zero
bytes. Detailed consumer reproduction and validation are retained under
GRV-Benchmark-032 in Gravitas's benchmark backlog. These are source-validation
results, not a package-release claim.

## Issue Template

```markdown
### SC-Issue-NNN - Concise title

- **Status / priority:** Needs reproduction | Confirmed | Planned | Deferred
- **Discovered:** Date and source revision
- **Affected owners / impact:** Source owner and observable risk
- **Evidence / reproduction:** Focused failing case or portable command
- **Next action:** Focused investigation or fix and required verification
```

