# Dictionary allocation review

This record preserves the review dated 2026-09-10. Its original source revision
and full environment were not recorded; the totals below are historical
evidence, not a claim about a current release candidate. Recreate coverage for
the checkout under review with each test project's own runsettings.

`GF-Benchmark-003` is owned by the GridForge benchmark-signal backlog. Its
upstream fix caches whether a key type can be null, so non-nullable value keys
skip boxing in lookup/removal, and invokes the typed indexer's object-based
error helper only on a miss. Probing, comparer dispatch for valid keys,
exceptions, instance layout and serialized state are unchanged.

The register retains Coverlet's exported `complexity` convention for comparison
with its existing entries. That export is not the same as a source decision
count. A fresh source-level review gives:

| Method               | Source decisions (plus base 1) | Source complexity / CRAP at 100% coverage | Coverlet export |
| -------------------- | ------------------------------ | ----------------------------------------: | --------------: |
| `Remove(TKey)`       | 4 `if`, 1 `while`, 4 `&&`      |                                   10 / 10 |              18 |
| `FindEntry(TKey)`    | 3 `if`, 1 `while`, 4 `&&`      |                                     9 / 9 |              16 |
| Typed indexer getter | 1 `if`                         |                                     2 / 2 |               2 |

All three methods retain 100% line and branch coverage in Debug, Release and
ReleaseLean. Using the register's exported values instead also leaves CRAP below
its risk threshold (18, 16 and 2). Keep the probe state local; the added type
guard does not justify decomposing the hash-table algorithm.

Coverage was collected separately for both test projects in every configuration:

```powershell
dotnet test tests/SwiftCollections.Tests/SwiftCollections.Tests.csproj -c ReleaseLean --no-build -p:UseLocalLsfStack=true -p:DisableTransitiveProjectReferences=true --collect:"XPlat Code Coverage" --settings tests/SwiftCollections.Tests/coverlet.runsettings
```

Repeat for Debug/Release and the companion project's matching runsettings. The
final combined core/companion totals are 7,527 lines / 2,530 branches / 1,278
fully covered methods in Debug; 5,676 / 2,522 / 1,278 in Release; and 5,664 /
2,522 / 1,272 in ReleaseLean, all exact 100%. Pre-existing Lean
enumeration/state gaps (two lines and three branches) were reproduced on
unchanged production source and closed with behavior tests independent of
MemoryPack. No exclusions or allocation tolerances were added. Local evidence is
retained in the Trailblazer checkout under `artifacts/gf-benchmark003`.
