# Issue Tracker

## Purpose

Track correctness, determinism, API, documentation, build, tooling and test
reliability concerns owned by SwiftCollections, including its FixedMathSharp
companion, with a compact index of verified resolutions or documented
investigation closures. Keep substantial implementation work in focused
feature-work plans.

## Tracker Rules

- Issue IDs use `SC-Issue-NNN`. The next available ID is `SC-Issue-005`.
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
| <a id="sc-issue-004---non-generic-list-enumeration-rejects-the-final-item"></a>SC-Issue-004 | 2026-10-28 | Corrected list/queue final-item guards and reproduced position-contract defects across the other affected collections and dictionary views; preserved generic iteration and full reachable coverage. | [Closeout and regressions](done/2026-10-28-enumerator-contract-closeout.md) |
| <a id="sc-issue-003---hash-set-lookup-boxes-value-keys-in-unoptimized-builds"></a>SC-Issue-003 | 2026-10-26 | Removed value-key boxing from all three hash-set lookup APIs, including Debug; retained null/error contracts and added warmed allocation regressions. | [Closeout](https://github.com/mrdav30/SwiftCollections/blob/2b4894f87980147934b1eb74603029e04fb84ec8/docs/feature-work/issue-tracker.md#sc-issue-003---hash-set-lookup-boxes-value-keys-in-unoptimized-builds) |
| <a id="sc-issue-002---collision-probes-stop-before-reaching-stored-entries"></a>SC-Issue-002 | 2026-10-07 | Completed dictionary/set collision-probe periods for insertion, lookup and removal; deterministic wrapped-chain and trimmed-table regressions passed. | [Closeout](https://github.com/mrdav30/SwiftCollections/blob/5e0c4e0830342508c3fb87c2fe43cf7c30167522/docs/feature-work/issue-tracker.md#sc-issue-002---collision-probes-stop-before-reaching-stored-entries) |
| <a id="sc-issue-001---pool-finalizer-coverage-depends-on-incidental-gc"></a>SC-Issue-001 | 2026-10-07 | Replaced incidental-GC coverage with isolated real-GC, mutation-checked pool-finalizer regressions; closed a test reliability gap without a production cleanup change. | [Closeout](https://github.com/mrdav30/SwiftCollections/blob/5e0c4e0830342508c3fb87c2fe43cf7c30167522/docs/feature-work/issue-tracker.md#sc-issue-001---pool-finalizer-coverage-depends-on-incidental-gc) |

## Issue Template

```markdown
### SC-Issue-NNN - Concise title

- **Status / priority:** Needs reproduction | Confirmed | Planned | Deferred
- **Discovered:** Date and source revision
- **Affected owners / impact:** Source owner and observable risk
- **Evidence / reproduction:** Focused failing case or portable command
- **Next action:** Focused investigation or fix and required verification
```

