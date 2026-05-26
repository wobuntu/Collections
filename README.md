# Wobuntu.Collections

A growing collection of convenient, high-performance data structures for .NET 10+.

## Data Structures

- **[`RTree<T>`](#rtree):** Fully mutable STR R-Tree with built-in viewport caching. Accepts any `T` via a boundary selector -- no wrapper types required. Faster than any popular competitors.
- **[`SynchronizedObservableOrderedSet<T>`](#synchronizedobservableorderedset):** Thread-safe, insertion-ordered set with O(1) lookup and `INotifyCollectionChanged` support.

---

## TL;DR

```csharp
// RTree: Compatible with any type, just supply a boundary selector
var tree = new RTree<City>(city => new RTreeBoundary(city.X, city.Y, city.X, city.Y));
tree.AddRange(cities); // bulk STR load

// Range query, caller-owned collection, zero allocations
var results = new List<City>();
tree.QueryTo(new RTreeBoundary(10, 10, 200, 200), results);

// Viewport for data binding (WPF, MVVM, etc.)
tree.Viewport = new RTreeBoundary(0, 0, 800, 600);
myListBox.ItemsSource = tree.ViewportItems; // SynchronizedObservableOrderedSet<City>
// ViewportItems updates automatically on Add/Remove via O(1) boundary check, without re-query.
```

```csharp
// SynchronizedObservableOrderedSet: Thread-safe, O(1) operations, observable
var set = new SynchronizedObservableOrderedSet<string>();
set.UnionWith(new[] { "a", "b", "c" });
set.IntersectThenUnionWith(existing, incoming); // atomic diff
```

---

## RTree

Implements `ICollection<T>`. Takes any `T` with a `Func<T, RTreeBoundary>` boundary selector.

### Construction

```csharp
// Empty
new RTree<T>(x => GetBoundary(x));

// Pre-sized, avoids reallocation during sequential inserts
new RTree<T>(capacity: 50_000, x => GetBoundary(x));

// Bulk initialize, fastest, uses STR algorithm
new RTree<T>(items.AsSpan(), x => GetBoundary(x));
```

### API

| Member                              | Notes                                                  |
| ----------------------------------- | ------------------------------------------------------ |
| `Add(item)`                         | Single insert                                          |
| `AddRange(IEnumerable<T>)`          | Bulk STR insert, `ArrayPool`-backed                    |
| `Remove(item)`                      | Single remove                                          |
| `RemoveRange(IEnumerable<T>)`       | Batch remove                                           |
| `QueryTo(boundary, ICollection<T>)` | Range query, zero allocations                          |
| `EnsureCapacity(int)`               | Pre-allocate nodes                                     |
| `Boundary`                          | MBR of the entire tree                                 |
| `Viewport`                          | Active viewport boundary (modifiable)                  |
| `ViewportItems`                     | `SynchronizedObservableOrderedSet<T>`, auto-maintained |

### Options

```csharp
new RTreeOptions
{
    MaxEntriesPerNode = 12,                      // Default 12, min 2
    UpdateViewportItemsOnShrinkThreshold = 0.3f  // Re-query only when shrink exceeds 30%
}
```

### Viewport mechanism

Setting `Viewport` issues a range query and synchronizes `ViewportItems` via an incremental diff
(`IntersectWith` + `UnionWith`, single lock). Subsequent `Add`/`Remove` calls update `ViewportItems`
in-place via an O(1) boundary check, no re-query is issued.

On zoom-in, re-queries are deferred until the viewport has contracted past the shrink threshold
relative to the cached boundary (default: skip if still within 70% of the cached size). This makes
incremental zoom essentially free.

`ViewportItems` implements `INotifyCollectionChanged` and binds directly to an WPF `ItemsControl`.

### Implementation notes

- **Insertion**:
  Minimizes center-to-center distance with a `1 + fullness^2` penalty to prevent single-branch deepening.
- **Bulk load**:
  STR, sort by X, tile into columns, sort each column by Y, build bottom-up.
  Temporaries use `ArrayPool<T>` or `stackalloc` (up to 512 bytes to fit L1 cache).
- **Query traversal**:
  Iterative (stack-based), no recursion.
- **Node layout**:
  Flat arrays with free-list management.
  Child boundaries are cached in `RTreeNodeReference` structs to reduce cache misses during node selection.

---

## SynchronizedObservableOrderedSet

Thread-safe, insertion-ordered set.

- `ReaderWriterLockSlim`-based; reads and writes are lock-separated
- O(1) `Contains`, `Add`, `Remove` via internal `HashSet<T>`
- Stable insertion order via internal `List<T>`
- Implements `INotifyCollectionChanged` + `INotifyPropertyChanged`
- Atomic set operations: `IntersectWith`, `UnionWith`, `ExceptWith`, `SymmetricExceptWith`, `IntersectThenUnionWith`
- Read lock is released before firing `CollectionChanged` to prevent WPF dispatcher deadlocks

---

## Performance

Fastest in its class across all mutable operations, benchmarked against RBush, NTS STRtree/HPRtree,
and QuadTree on .NET 10. Full tables and methodology: [BenchmarkResults.md](BenchmarkResults.md)

| Category               | Result                                                            |
| ---------------------- | ----------------------------------------------------------------- |
| Bulk insert            | Fastest at all sizes; up to 7x over RBush/NTS                     |
| Query                  | Only library with **0 B allocations**; up to 2.2x faster          |
| Remove                 | Up to 3.6x faster; near-zero allocations                          |
| Mixed mutations        | Up to **308x faster** than RBush; RBush allocates 16 GB at N=500k |
| Viewport pan           | Up to 40% faster; 7-10x fewer allocations per frame               |
| Viewport zoom (cached) | **32-67x faster** than any competitor; 72-96x fewer allocations   |
| Viewport mutations     | Up to **68x faster**; O(1) per mutation, 0 B allocated            |
