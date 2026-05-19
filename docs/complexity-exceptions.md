# Cyclomatic Complexity Exception Register

This document records methods that intentionally exceed the current cyclomatic complexity review threshold.

## Policy

- Review threshold: cyclomatic complexity greater than 10.
- Risk threshold: CRAP score greater than 30 requires immediate test hardening or refactoring.
- Current status: the fresh coverage/CRAP report generated on 2026-05-18 has no methods above CRAP 30.
- Source report: `tests/TestResults/coverage-analysis/reports/Summary.txt`.
- Raw coverage inputs:
  - `tests/TestResults/coverage-analysis/final4/core/a0deca13-90da-429e-8859-871baffee7f0/coverage.cobertura.xml`
  - `tests/TestResults/coverage-analysis/final4/fixed/115eac48-cc53-4d8b-a8f8-eaa86c0a5537/coverage.cobertura.xml`

Complexity exceptions are acceptable when the method is a hot hash-table probe path, an allocation-sensitive sparse collection routine, or fixed-shape spatial partitioning code where decomposition would add indirection without reducing real maintenance risk. These exceptions should be revisited when coverage drops, behavior changes, or a shared zero-allocation helper can reduce duplicated logic without slowing the hot path.

## Exception Register

| Module | Method | Complexity | Coverage | Rationale | Revisit if |
| --- | --- | ---: | --- | --- | --- |
| `SwiftCollections` | `SwiftHashSet<T>.InsertIfNotExists(T)` | 24 | 93.2% line / 47.9% branch | Core quadratic-probing insert path with tombstone reuse, resize retry, and randomized-comparer escalation. Keeping the probe state local preserves the hot O(1)-expected path. | Collision fixtures cover the randomized-comparer branch, insertion policy changes, or a benchmarked helper split is neutral. |
| `SwiftCollections` | `SwiftDictionary<TKey, TValue>.InsertIfNotExist(TKey, TValue)` | 24 | 93.3% line / 79.2% branch | Dictionary insert mirrors the hash-set probe invariant while also storing key/value payloads. The branch count comes from collision, tombstone, resize, and comparer-escalation paths. | Hash probing is centralized with no delegate cost, or collision behavior changes. |
| `SwiftCollections` | `SwiftBucket<T>.IndexOf(T)` | 18 | 96.0% line / 44.4% branch | Sparse bucket lookup scans allocated slots directly and avoids iterator allocation or dense side structures. | The bucket gains a dense live-index view, or branch coverage reveals a reachable equality edge case. |
| `SwiftCollections` | `SwiftDictionary<TKey, TValue>.Remove(TKey)` | 16 | 100% line / 50.0% branch | Hot tombstone-removal path follows the same quadratic probe sequence as lookup and insertion; keeping delete, count, and last-index updates together makes the invariant auditable. | Deletion semantics change, tombstone cleanup is introduced, or probing is safely shared. |
| `SwiftCollections` | `SwiftHashSet<T>.FindEntry(T)` | 14 | 94.1% line / 46.4% branch | Hot lookup path terminates on either match, live miss, tombstone continuation, or full probe exhaustion. Splitting would obscure probe termination rules. | Additional collision tests expose uncovered reachable branches, or lookup probing is centralized. |
| `SwiftCollections.Query` | `SwiftBVH<TKey, TVolume>.UpdateEntryBounds(TKey, TVolume)` | 14 | 96.3% line / 85.7% branch | BVH updates need null/key validation, missing-node handling, no-op bounds checks, and upward parent-bound propagation in one flow. | Update behavior starts rebalancing nodes, or parent refresh logic becomes shared elsewhere. |
| `SwiftCollections` | `SwiftSparseMap<T>.TrimExcess()` | 14 | 100% line / 46.4% branch | Compacts sparse and dense storage while preserving key-to-dense-index invariants without allocating an intermediate map. | Sparse/dense storage layout changes or trim logic is shared with resize. |
| `SwiftCollections` | `SwiftDictionary<TKey, TValue>.FindEntry(TKey)` | 14 | 100% line / 92.9% branch | Direct dictionary lookup keeps hash, tombstone, and quadratic-probe state in a compact hot path. | Probe behavior is unified with insert/remove without measurable overhead. |
| `SwiftCollections` | `SwiftSparseMap<T>.set_State(SwiftSparseSetState<T>)` | 14 | 100% line / 39.3% branch | State restore validates sparse/dense shape and reconstructs indexing invariants. Keeping the validation sequence local makes malformed state handling clear. | State format changes or validation helpers become shared with another sparse type. |
| `SwiftCollections` | `SwiftHashSet<T>.Remove(T)` | 14 | 100% line / 46.4% branch | Hot tombstone-removal path keeps match detection, tombstone marking, and count/last-index updates together. | Tombstone cleanup changes or probing is safely shared with dictionary removal. |
| `SwiftCollections` | `SwiftDictionary<TKey, TValue>.TrimExcess()` | 12 | 100% line / 50.0% branch | Rehashes live entries into a smaller power-of-two table while preserving quadratic-probe placement and adaptive resize state. | Resize and trim can share a zero-overhead rehash helper. |
| `SwiftCollections.Query` | `BoundVolumeOctreePartitioner.CreateChildBounds(BoundVolume, int)` | 12 | 100% line / 50.0% branch | Fixed 3-axis octant bounds construction uses explicit component selection to avoid temporary arrays or delegates. | Generated or shared octant code becomes available without allocations. |
| `SwiftCollections.Query` | `BoundVolumeOctreePartitioner.TryGetContainingChildIndex(BoundVolume, BoundVolume, out int)` | 12 | 100% line / 50.0% branch | Axis-by-axis containment classification has clear early exits for bounds that straddle an octant plane. | Additional bound shapes are added or a shared allocation-free axis classifier is introduced. |
| `SwiftCollections.Query` | `FixedBoundVolumeOctreePartitioner.CreateChildBounds(FixedBoundVolume, int)` | 12 | 100% line / 100% branch | Fixed-point octant bounds construction mirrors the numerics path while preserving deterministic arithmetic and avoiding temporaries. | Generated or shared octant code becomes available without allocations. |
| `SwiftCollections.Query` | `FixedBoundVolumeOctreePartitioner.TryGetContainingChildIndex(FixedBoundVolume, FixedBoundVolume, out int)` | 12 | 100% line / 100% branch | Fixed-point axis classification is deterministic, explicit, and branchy by shape; extraction would not reduce the underlying decision count. | Fixed and numerics partitioners gain a shared zero-allocation classifier. |
| `SwiftCollections` | `SwiftHashSet<T>.TrimExcess()` | 12 | 100% line / 50.0% branch | Rehashes live entries into a smaller power-of-two table while retaining quadratic-probe placement and adaptive resize state. | Resize and trim can share a zero-overhead rehash helper. |

## Review Notes

- The refactor pass reduced methods above the complexity threshold from 30 to 16.
- The fresh report has 98.1% line coverage and 92.6% branch coverage across the two covered assemblies.
- Methods below the complexity threshold can still appear high in CRAP ranking if coverage is low. Those should be handled with focused tests, not complexity exceptions.
- Re-run coverage and CRAP analysis after touching any method listed here, then update this register.
