# Cyclomatic Complexity Exception Register

This document records methods that intentionally exceed the configured
cyclomatic complexity review threshold.

## Policy

- Review threshold: cyclomatic complexity greater than 10.
- Register only methods above that threshold. Entries justify complexity, never
  uncovered code or an exception to the coverage gate.
- Risk threshold: CRAP score greater than 30 requires immediate test hardening
  or refactoring.
- Coverage evidence is generated output and is not checked in. Recreate it with
  the repository coverage command and calculate CRAP scores from the resulting
  Cobertura XML. Use each test project's assembly-specific runsettings and
  combine the results when reviewing both runtime assemblies.
- Registered methods must retain 100% line and branch coverage. Re-evaluate an
  exception whenever fresh coverage falls below that requirement.

Complexity exceptions are acceptable when the method is a hot hash-table probe
path, an allocation-sensitive sparse collection routine, fixed-shape spatial
partitioning code, or a bounded serialization boundary where decomposition
would obscure validation and ownership without reducing real maintenance risk.
These exceptions should be revisited when coverage drops,
behavior changes, or a shared zero-allocation helper can reduce duplicated logic
without slowing the hot path.

## Exception Register

| Module                   | Method                                                                                                      | Complexity | Coverage                | Rationale                                                                                                                                                                                                                                        | Revisit if                                                                                                                   |
| ------------------------ | ----------------------------------------------------------------------------------------------------------- | ---------: | ----------------------- | ------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------ | ---------------------------------------------------------------------------------------------------------------------------- |
| `SwiftCollections`       | `SwiftHashSet<T>.InsertIfNotExists(T)`                                                                      |         26 | 100% line / 100% branch | Core quadratic-probing insert path with tombstone reuse, resize retry, and randomized-comparer escalation. Keeping the probe state local preserves the hot O(1)-expected path.                                                                   | Collision fixtures cover the randomized-comparer branch, insertion policy changes, or a benchmarked helper split is neutral. |
| `SwiftCollections`       | `SwiftDictionary<TKey, TValue>.InsertIfNotExist(TKey, TValue)`                                              |         26 | 100% line / 100% branch | Dictionary insert mirrors the hash-set probe invariant while also storing key/value payloads. The branch count comes from collision, tombstone, resize, and comparer-escalation paths.                                                           | Hash probing is centralized with no delegate cost, or collision behavior changes.                                            |
| `SwiftCollections`       | `SwiftSlotMap<T>.IndexOf(T)`                                                                                |         18 | 100% line / 100% branch | Sparse slot-map lookup scans allocated slots directly and avoids iterator allocation or dense side structures.                                                                                                                                   | The slot map gains a dense live-index view, or branch coverage reveals a reachable equality edge case.                       |
| `SwiftCollections`       | `SwiftDictionary<TKey, TValue>.Remove(TKey)`                                                                |         18 | 100% line / 100% branch | Type-aware null rejection avoids Debug value-key boxing. The hot tombstone-removal path follows the same quadratic probe sequence as lookup and insertion; keeping delete, count, and last-index updates together makes the invariant auditable. | Null-key or deletion semantics change, tombstone cleanup is introduced, or probing is safely shared.                         |
| `SwiftCollections`       | `SwiftHashSet<T>.FindEntry(T)`                                                                              |         16 | 100% line / 100% branch | Type-aware null rejection avoids Debug value-key boxing. The hot lookup path terminates on match, live miss, tombstone continuation, or full probe exhaustion; splitting would obscure probe termination rules.                                  | Null-key behavior changes, additional collision tests expose uncovered reachable branches, or lookup probing is centralized. |
| `SwiftCollections`       | `SwiftSparseMap<T>.TrimExcess()`                                                                            |         14 | 100% line / 100% branch | Compacts sparse and dense storage while preserving key-to-dense-index invariants without allocating an intermediate map.                                                                                                                         | Sparse/dense storage layout changes or trim logic is shared with resize.                                                     |
| `SwiftCollections`       | `SwiftDictionary<TKey, TValue>.FindEntry(TKey)`                                                             |         16 | 100% line / 100% branch | Type-aware null rejection avoids Debug value-key boxing. Direct dictionary lookup keeps hash, tombstone, and quadratic-probe state in a compact hot path.                                                                                        | Null-key behavior changes, or probing is unified with insert/remove without measurable overhead.                             |
| `SwiftCollections`       | `SwiftSparseMap<T>.set_State(SwiftSparseMapState<T>)`                                                       |         14 | 100% line / 100% branch | State restore validates sparse/dense shape and reconstructs indexing invariants. Keeping the validation sequence local makes malformed state handling clear.                                                                                     | State format changes or validation helpers become shared with another sparse type.                                           |
| `SwiftCollections`       | `SwiftHashSet<T>.Remove(T)`                                                                                 |         14 | 100% line / 100% branch | Hot tombstone-removal path keeps match detection, tombstone marking, and count/last-index updates together.                                                                                                                                      | Tombstone cleanup changes or probing is safely shared with dictionary removal.                                               |
| `SwiftCollections`       | `SwiftDictionary<TKey, TValue>.TrimExcess()`                                                                |         12 | 100% line / 100% branch | Rehashes live entries into a smaller power-of-two table while preserving quadratic-probe placement and adaptive resize state.                                                                                                                    | Resize and trim can share a zero-overhead rehash helper.                                                                     |
| `SwiftCollections.Query` | `BoundVolumeOctreePartitioner.CreateChildBounds(BoundVolume, int)`                                          |         12 | 100% line / 100% branch | Fixed 3-axis octant bounds construction uses explicit component selection to avoid temporary arrays or delegates.                                                                                                                                | Generated or shared octant code becomes available without allocations.                                                       |
| `SwiftCollections.Query` | `BoundVolumeOctreePartitioner.TryGetContainingChildIndex(BoundVolume, BoundVolume, out int)`                |         12 | 100% line / 100% branch | Axis-by-axis containment classification has clear early exits for bounds that straddle an octant plane.                                                                                                                                          | Additional bound shapes are added or a shared allocation-free axis classifier is introduced.                                 |
| `SwiftCollections.Query` | `FixedBoundVolumeOctreePartitioner.CreateChildBounds(FixedBoundVolume, int)`                                |         12 | 100% line / 100% branch | Fixed-point octant bounds construction mirrors the numerics path while preserving deterministic arithmetic and avoiding temporaries.                                                                                                             | Generated or shared octant code becomes available without allocations.                                                       |
| `SwiftCollections.Query` | `FixedBoundVolumeOctreePartitioner.TryGetContainingChildIndex(FixedBoundVolume, FixedBoundVolume, out int)` |         12 | 100% line / 100% branch | Fixed-point axis classification is deterministic, explicit, and branchy by shape; extraction would not reduce the underlying decision count.                                                                                                     | Fixed and numerics partitioners gain a shared zero-allocation classifier.                                                    |
| `SwiftCollections`       | `SwiftHashSet<T>.TrimExcess()`                                                                              |         12 | 100% line / 100% branch | Rehashes live entries into a smaller power-of-two table while retaining quadratic-probe placement and adaptive resize state.                                                                                                                     | Resize and trim can share a zero-overhead rehash helper.                                                                     |

### Comparer persistence boundaries

The comparer-persistence methods below retain their trust-boundary decisions
together. Complexity counts include short-circuit conditions, loops, catches
and conditional expressions; all reported lines and branches remain covered
in standard, Lean and Debug builds.

| Module | Method | Complexity | Coverage | Rationale | Revisit if |
| --- | --- | ---: | --- | --- | --- |
| `SwiftCollections.Serialization` | `SwiftComparerRegistry.RestoreCustom<TComparer>` | 15 | 100% line / 100% branch | Ordering and equality share typed resolution, isolated factory input, singleton/configuration checks and weak binding publication. Callbacks stay outside the association lock. | Weak-binding semantics change or a real consumer needs another comparer contract. |
| `SwiftCollections.Serialization` | `SwiftComparerRegistry.TryCaptureBuiltInEquality<T>` | 11 | 100% line / 100% branch | Recognize canonical public instances and exact Swift algorithms, including contravariant object comparers, without private inspection or application Equals calls. | Built-in algorithms or declared-type eligibility change. |
| `SwiftCollections.Serialization` | `SwiftComparerRegistry.RestoreBuiltInEquality<T>` | 18 | 100% line / 100% branch | Validate exact type, version and configuration length before reconstructing the fixed set of equality/hash policies without fresh entropy. | Another built-in policy is supported or the frozen configuration schema changes. |
| `SwiftCollections.Serialization` | `SwiftComparerRegistry.ValidateReverseEquality<T>` | 19 | 100% line / 100% branch | Admit identical reverse descriptors or recognized equality-preserving Swift hash fallback pairs; reject contradictory custom/default policies before population. | Reverse-map fallback or equality semantics change. |
| `SwiftCollections.Serialization` | `SwiftComparerRegistry.ValidateId` | 11 | 100% line / 100% branch | Validate the fixed ASCII identifier alphabet and length before registration; preserve argument-specific failures. | The wire alphabet or identifier bounds change. |
| `SwiftCollections.Serialization` | `SwiftComparerRegistry.DefaultOrdering<T>.IsEligible` | 12 | 100% line / 100% branch | Classify a declared default once, including nullable enums and comparable interfaces, without reflective generic activation. | Default eligibility or supported AOT profiles change. |
| `SwiftCollections.Serialization` | `SwiftComparerJsonMetadata.ReadId` | 11 | 100% line / 100% branch | Bound escaped/segmented text before allocation, then validate ASCII before materializing the identifier. | The alphabet/bounds change or a shared bounded decoder reduces work. |
| `SwiftCollections.Serialization` | `SwiftComparerJsonMetadata.ReadNonEmptyConfiguration` | 11 | 100% line / 100% branch | Bound and decode nonempty configuration, enforcing exact base64 length and canonical unused bits before invoking an application factory. The separate empty path avoids reserving maximum scratch storage. | Configuration limits or encoding change. |

## Review Guidance

### Ongoing Review

- The exception register permits justified complexity; it does not waive the
  CRAP risk threshold or coverage requirement.
- Methods below the complexity threshold can still appear high in CRAP ranking
  if coverage is low. Those should be handled with focused tests, not complexity
  exceptions.
- Re-run coverage and CRAP analysis after touching any method listed here, then
  update its complexity, coverage, rationale, and revisit condition.
