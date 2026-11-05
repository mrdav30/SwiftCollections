---
title: Collections and clearing
---

# Collections and clearing

Start with the ownership of IDs and the iteration pattern your workload needs.
The BCL remains a useful baseline; benchmark representative inputs before
choosing a specialized container.

```mermaid
flowchart TD
    Key{What identifies an item?} -->|Container-owned slot| Stale{Detect stale references?}
    Stale -->|Yes| Generation[SwiftTicketMap]
    Stale -->|No| Slot[SwiftSlotMap]
    Key -->|Compact external integer ID| Payload{Attach a value?}
    Payload -->|Yes| Map[SwiftSparseMap]
    Payload -->|No| Set[SwiftSparseSet]
    Key -->|Arbitrary key| Hash[SwiftDictionary or SwiftHashSet]
```

## Core Collections

| Type                               | Primary Role                | Notes                                                                                                              |
| ---------------------------------- | --------------------------- | ------------------------------------------------------------------------------------------------------------------ |
| `SwiftList<T>`                     | Dynamic contiguous list     | Familiar list shape with SwiftCollections growth, span helpers, and explicit `SortInPlace` scratch-buffer sorting. |
| `SwiftQueue<T>`                    | Circular-buffer queue       | Suited for repeated enqueue/dequeue workloads.                                                                     |
| `SwiftStack<T>`                    | Array-backed stack          | Simple LIFO storage for low-overhead stack workloads.                                                              |
| `SwiftSortedList<T>`               | Sorted dynamic collection   | Keeps values ordered for search and ordered iteration.                                                             |
| `SwiftDictionary<TKey, TValue>`    | Hash table key/value lookup | Uses deterministic default string comparers when no comparer is supplied.                                          |
| `SwiftHashSet<T>`                  | Hash table set membership   | Uses deterministic default string comparers when no comparer is supplied.                                          |
| `SwiftBiDictionary<TLeft, TRight>` | Two-way lookup              | Maintains forward and reverse mappings.                                                                            |

`SwiftDictionary<TKey, TValue>` and `SwiftHashSet<T>` are the closest
alternatives to `Dictionary<TKey, TValue>` and `HashSet<T>`. They are not
intended as blind drop-in replacements. Benchmark the workload you care about,
especially when custom comparers, string keys, collision behavior, or
trim/resize patterns are important.

## Specialized Containers

| Use case                                                        | Better fit                                      | Why                                                                             |
| --------------------------------------------------------------- | ----------------------------------------------- | ------------------------------------------------------------------------------- |
| Store this object and give me a stable slot                     | `SwiftSlotMap<T>`                               | The container owns integer slot assignment and can reuse freed slots.           |
| Store this object and protect against stale references          | `SwiftTicketMap<T>`                             | Tickets include index plus generation, so reused slots invalidate old tickets.  |
| I already have a compact non-negative int ID; track membership  | `SwiftSparseSet`                                | Sparse lookup plus dense key storage gives O(1) membership and dense iteration. |
| I already have a compact non-negative int ID; attach a value    | `SwiftSparseMap<T>`                             | Sparse lookup maps external IDs to densely stored values.                       |
| I need dense unique-value iteration with hash-backed membership | `SwiftPackedSet<T>`                             | Values are packed for iteration while membership remains hash-backed.           |
| IDs are arbitrary, huge, or widely sparse                       | `SwiftHashSet<int>` / `SwiftDictionary<int, T>` | Hash tables avoid allocating sparse arrays up to the highest ID.                |

Sparse containers are strongest when IDs are compact, non-negative, and
externally owned by a simulation, ECS, partition, or ticket system. Their memory
usage scales with the highest stored ID, not only with the number of live IDs.

## Who issues the identity?

Both map types issue an identity when you call `Add`. Later lookup and removal
operations take that identity back. The difference is whether it identifies only
a slot or also the particular allocation occupying that slot.

| Container           | Identity supplied to `Add`       | Identity returned by `Add`                      | After removal and slot reuse                            |
| ------------------- | -------------------------------- | ----------------------------------------------- | ------------------------------------------------------- |
| `SwiftSlotMap<T>`   | None                             | An integer slot index                           | The old index can refer to the replacement value        |
| `SwiftTicketMap<T>` | None                             | A `SwiftTicket` containing index and generation | The old ticket is rejected while its generation differs |
| `SwiftSparseMap<T>` | Your externally owned integer ID | None; `TryAdd` reports success                  | The container does not track generations of your ID     |

### Stable slots under your ownership

Use `SwiftSlotMap<T>` when items need stable indices while registered and the
owner can discard every saved index when removing an item. Other live items keep
their indices; removal leaves a reusable slot rather than shifting them.

```csharp
using SwiftCollections;

var registrations = new SwiftSlotMap<string>();
int firstSlot = registrations.Add("first");
registrations.TryRemoveAt(firstSlot);

int replacementSlot = registrations.Add("replacement");
bool sameSlot = firstSlot == replacementSlot; // true: the freed slot was reused.
bool found = registrations.TryGetValue(firstSlot, out string value);
// found is true and value is "replacement". The integer cannot detect staleness.
```

This fits a tightly owned registry whose removal path also clears its callers'
stored IDs. `InsertAt` can explicitly choose or replace a slot, but adds no
generation validation. Use a sparse map when attaching values to existing
external IDs is the main job.

### A ticket for one allocation

Use `SwiftTicketMap<T>` when a saved identity can outlive removal, such as a
queued command or delayed lookup. Keep the ticket returned by `Add` and pass it
back to `TryGet`, `Remove`, or `IsValid`:

```csharp
using SwiftCollections;

var actors = new SwiftTicketMap<string>();
SwiftTicket oldTicket = actors.Add("first");
actors.Remove(oldTicket);

SwiftTicket currentTicket = actors.Add("replacement");
bool oldFound = actors.TryGet(oldTicket, out _); // false: generation changed.
bool currentFound = actors.TryGet(currentTicket, out string actor);
// currentFound is true and actor is "replacement".
```

A ticket identifies one allocation: its index chooses the slot and its
generation distinguishes allocations of that slot. Preserve both fields when
storing a ticket. Tickets belong to their originating collection; they carry no
collection identity and are not globally unique IDs. Generations are finite
`uint` counters, so this is not an unlimited uniqueness guarantee. Neither
bucket compacts live values on removal; iteration can scan holes in the
allocated slot range instead of visiting only live values.

### Values attached to existing IDs

Use `SwiftSparseMap<T>` when another system already owns compact integer IDs:

```csharp
using SwiftCollections;

int actorId = 42; // Issued by the owning system.
var labels = new SwiftSparseMap<string>();
labels.Add(actorId, "player");
bool found = labels.TryGetValue(actorId, out string label);
// found is true and label is "player"; the map did not issue actorId.
```

Choose `SwiftSparseSet` for membership without a payload. If IDs can be very
large or widely scattered, use a hash-based collection instead of allocating
sparse lookup storage up to the highest ID. Choose `SwiftList<T>` when indices
need no stability across structural edits, or `SwiftPackedSet<T>` when dense
unique-value iteration and membership matter more than assigned slots.

`SwiftSparseSet` and `SwiftSparseMap<T>` can copy dense keys into caller-owned
`SwiftList<int>` buffers, including sorted ascending export through
`CopySortedKeysTo(...)`. Use that path for deterministic per-frame scratch
ordering instead of building a persistent `SwiftSortedList<T>` solely to sort
transient keys.

## Clearing And Retained References

Core collections expose `Clear(bool clearRetainedReferences)` alongside their
existing parameterless clearing operations. Use the explicit option at an
ownership boundary when old backing slots must release their references:

```csharp
using SwiftCollections;

var scratch = new SwiftList<object>(1024);
scratch.Add(new object());
scratch.FastClear(); // Count becomes zero; the object remains in backing storage.
scratch.Clear(clearRetainedReferences: true); // Release retained references; keep capacity.
```

- `Clear()` and `Clear(false)` preserve the collection's normal clearing cost
  and behavior. They need not inspect retained slots outside the live range.
- `Clear(true)` also releases reference-containing payloads outside that range,
  including tails left by a previous `FastClear()` and smaller refill. Its cost
  can scale with retained capacity. It preserves capacity and normal collection
  policies such as comparers, sparse membership and notifications.
- `FastClear()` remains a count-only operation on the types that support it. It
  does not release stored references.

The explicit option is available on all core collection types. Bidirectional and
observable collections inherit it through their existing virtual clearing
behavior. Reference-containing structs receive the same cleanup as reference
types. Numeric-only payloads require no extra reference scan; `SwiftSparseSet`
therefore accepts the option without additional work. Full reference cleanup
does not promise to zero unused numeric storage.

`SwiftTicketMap<T>.Clear()` invalidates live tickets while preserving generation
history. Both clearing modes preserve subsequent ticket allocation when the
cleared map is saved and restored. Fixed-size arrays, pools and spatial indexes
retain their separate clearing/lifecycle APIs.

## Make ordering an explicit contract

A deterministic string comparer does not make every key type deterministic.
Custom comparers and non-string keys retain their own hashing/equality
contracts. Hash-backed containers also do not promise a canonical sorted order.
When replay or output requires ascending integer IDs, copy and sort them through
`CopySortedKeysTo(...)` rather than depending on incidental storage order.

Continue with [Pools and observable collections](pools-and-observables.md) when
collection instances themselves are rented and reused.
