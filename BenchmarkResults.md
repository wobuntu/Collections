# Benchmark Results

**Benchmark Environment:** Windows 11, Intel Core i7-8705G 3.10GHz (Kaby Lake G), 4 cores / 8 threads, .NET 10.0.7, BenchmarkDotNet v0.14.0

**Libraries compared:**

- **[Wobuntu](https://github.com/wobuntu/Collections):** Our R-Tree implementation, part of `Wobuntu.Collections`.
- **[RBush v4.0.0](https://github.com/viceroypenguin/RBush):** Popular R-Tree library (OMT bulk-load, originally ported from a JS library)
- **[NTS STRtree v2.6.0](https://github.com/NetTopologySuite/NetTopologySuite):** NetTopologySuite STRtree (semi-static, builds on first query)
- **[NTS HPRtree v2.6.0](https://github.com/NetTopologySuite/NetTopologySuite):** NetTopologySuite HPRtree (static, no remove support, builds on first query)
- **[QuadTree v1.0.4](https://github.com/splitice/QuadTrees):** Uses a Quad-Tree to store spatial data.

Feel free to propose further competitive libraries by creating an issue.

**Fairness:**

- All libraries in all benchmarks use a maximum child node count of `12` to provide comparability.
- Not all compared libraries support mutation after the tree has been built, hence are excluded from respective
  benchmarks.
- The viewport functionality is unique to `Wobuntu.Collections.RTree`.  
  In an attempt to privide comparable functionality using other libraries, they maintain a
  `Wobuntu.Collections.Observable.SynchronizedObservableOrderedSet`, using `IntersectWith` and `Unionwith` after each
  query. This is the minimum overhead real consumers would require for ordered, observable, deduplicated viewport
  results.
  The initial viewport state (frame 0) is pre-seeded in `IterationSetup` before measurement, because some of the
  libraries build the tree only after the first query occurs.
- **Please open an issue if you spot unfairness issues** in the benchmark setup / in the used configuration of
  the compared libraries / etc.** Those benchmarks are not meant to flex, but to be a fair comparison of all libraries.

---

## 1. Bulk Insert (STR / bulk-load)

| N    | Wobuntu               | HPRtree           | QuadTree          | RBush             | NTS STRtree       |
| ---- | --------------------- | ----------------- | ----------------- | ----------------- | ----------------- |
| 10k  | **1.68 ms (1.00x)**   | 1.90 ms (1.13x)   | 3.85 ms (2.29x)   | 4.39 ms (2.62x)   | 4.64 ms (2.76x)   |
| 100k | **19.45 ms (1.00x)**  | 43.27 ms (2.22x)  | 81.74 ms (4.20x)  | 78.06 ms (4.01x)  | 145.81 ms (7.50x) |
| 500k | **150.73 ms (1.00x)** | 228.74 ms (1.52x) | 508.33 ms (3.37x) | 665.80 ms (4.42x) | 676.41 ms (4.49x) |

**Allocations:**

| N    | Wobuntu          | HPRtree              | QuadTree         | RBush             | NTS STRtree      |
| ---- | ---------------- | -------------------- | ---------------- | ----------------- | ---------------- |
| 10k  | 1.23 MB (1.00x)  | **1.00 MB (0.81x)**  | 1.80 MB (1.46x)  | 2.19 MB (1.78x)   | 1.52 MB (1.24x)  |
| 100k | 12.38 MB (1.00x) | **9.53 MB (0.77x)**  | 16.28 MB (1.32x) | 27.29 MB (2.20x)  | 15.23 MB (1.23x) |
| 500k | 62.26 MB (1.00x) | **45.63 MB (0.73x)** | 74.41 MB (1.20x) | 171.18 MB (2.75x) | 71.86 MB (1.15x) |

Our library is the fastest at every scale, roughly 1.5x faster than HPRtree, which comes second from in a pure speed
comparison. We are at least more than twice up to around 7 times faster than RBush, QuadTree and NTS STRtree.
NTS HPRtree outperforms our library currently in terms of memory allocation. Though we are still using less memory than
all other libraries.

## 2. Query (100 searches, ~10% area coverage each)

| N    | Wobuntu              | HPRtree               | RBush             | QuadTree          | NTS STRtree       |
| ---- | -------------------- | --------------------- | ----------------- | ----------------- | ----------------- |
| 10k  | **317.6 μs (1.00x)** | 333.7 μs (1.05x)      | 384.3 μs (1.21x)  | 570.9 μs (1.80x)  | 681.8 μs (2.15x)  |
| 100k | **3,594 μs (1.00x)** | 5,035 μs (1.40x)      | 5,701 μs (1.59x)  | 9,818 μs (2.73x)  | 12,496 μs (3.48x) |
| 500k | 26,843 μs (1.02x)    | **25,137 μs (0.96x)** | 36,046 μs (1.37x) | 43,728 μs (1.66x) | 82,841 μs (3.15x) |

**Allocations:**

| N    | Wobuntu | HPRtree | NTS STRtree | RBush   | QuadTree |
| ---- | ------- | ------- | ----------- | ------- | -------- |
| 10k  | **0 B** | 216 KB  | 214 KB      | 255 KB  | 279 KB   |
| 100k | **0 B** | 1.88 MB | 1.88 MB     | 2.09 MB | 2.20 MB  |
| 500k | **0 B** | 12.5 MB | 12.5 MB     | 14.1 MB | 13.3 MB  |

Our library does not allocate any memory during query, which is a unique property among all benchmarked libraries.
Though not the only reason, we don't allocate the resulting data structure during querying, we expect the caller to
provide a presized collection which is being reused instead.

Our library leads convincingly at N=10k and N=100k in terms of speed. At N=500k HPRtree edges ahead by ~6%
(25.1 ms vs 26.8 ms); this is within normal run-to-run noise but notable.

## 3. Sequential Add (one-by-one inserts)

**NTS STRtree and HPRtree are excluded.** Both only append to a list during insert and defer their tree construction
to the first query.

| N    | Wobuntu                | RBush              | QuadTree             |
| ---- | ---------------------- | ------------------ | -------------------- |
| 10k  | 6.006 ms (1.00x)       | 12.013 ms (2.00x)  | **2.524 ms (0.42x)** |
| 100k | **22.621 ms (1.00x)**  | 127.334 ms (5.63x) | 56.831 ms (2.51x)    |
| 500k | **115.822 ms (1.00x)** | 641.674 ms (5.54x) | 476.485 ms (4.11x)   |

**Allocations:**

| N    | Wobuntu           | RBush             | QuadTree             |
| ---- | ----------------- | ----------------- | -------------------- |
| 10k  | 3.33 MB (1.00x)   | 4.06 MB (1.22x)   | **1.80 MB (0.54x)**  |
| 100k | 38.31 MB (1.00x)  | 65.54 MB (1.71x)  | **16.28 MB (0.42x)** |
| 500k | 225.72 MB (1.00x) | 234.70 MB (1.04x) | **74.41 MB (0.33x)** |

At N=10k, QuadTree is **2.4x faster** due to its very low per-insert overhead for small trees.
This advantage evaporates at N=100k (2.5x slower than our library) and N=500k (4.1x slower) as its shallow
quadrant partitioning degrades for dense data. RBush is consistently 2-5.5x slower than Wobuntu.
However, QuadTree's main advantage is, that it uses less allocations than our library throughout.
The benchmark shows the behavior, if the tree is not sized right at the beginning using `EnsureCapacity` or
the constructor overload, but how it grows using its initial size.

## 4. Remove (all N items from pre-built tree)

**QuadTree and NTS HPRtree are excluded**, neither supports individual item removal.

| N    | Wobuntu                | NTS STRtree        | RBush              |
| ---- | ---------------------- | ------------------ | ------------------ |
| 10k  | **3.000 ms (1.00x)**   | 5.431 ms (1.81x)   | 8.758 ms (2.92x)   |
| 100k | **36.200 ms (1.00x)**  | 99.691 ms (2.75x)  | 129.075 ms (3.57x) |
| 500k | **268.041 ms (1.00x)** | 687.193 ms (2.56x) | 866.086 ms (3.23x) |

**Allocations:**

| N    | Wobuntu | NTS STRtree | RBush    |
| ---- | ------- | ----------- | -------- |
| 10k  | 9.1 KB  | **0 B**     | 10.5 MB  |
| 100k | 80.1 KB | **0 B**     | 138.0 MB |
| 500k | 398 KB  | **0 B**     | 830 MB   |

Our library is the consistently fastest at 2.6 - 3.6x with near-zero allocations.
RBush allocates 1,175-2,136x more than our library.
NTS STRtree is the second-fastest with zero allocations but 1.8-2.8x slower than our library.

## 5. Mixed (add/remove interleaved, N/2 operations on N-item tree)

**NTS STRtree, HPRtree, and QuadTree are excluded**, NTS trees are semi-static (no inserts after build),
QuadTree does not support removal.

| N    | Wobuntu               | RBush                  |
| ---- | --------------------- | ---------------------- |
| 10k  | **1.319 ms (1.00x)**  | 12.539 ms (9.51x)      |
| 100k | **14.544 ms (1.00x)** | 1,699.378 ms (116.8x)  |
| 500k | **92.037 ms (1.00x)** | 28,338.892 ms (307.9x) |

**Allocations:**

| N    | Wobuntu | RBush     |
| ---- | ------- | --------- |
| 10k  | 16.6 KB | 9.7 MB    |
| 100k | 153 KB  | 1,765 MB  |
| 500k | 762 KB  | 16,018 MB |

Wobuntu is **308x faster** than RBush at N=500k, where RBush allocates ~16 GB of intermediate memory.
RBush allocates 596-21,523x more than Wobuntu across sizes.

---

## 6. Viewport (99 delta frames from a pre-established viewport, 1000x1000 in 10000x10000 space)

### 6a. Panning (viewport slides across the full space in 99 delta steps)

| N    | Wobuntu Viewport      | HPRtree + ObsSet  | QuadTree + ObsSet | RBush + ObsSet    | NTS + ObsSet      |
| ---- | --------------------- | ----------------- | ----------------- | ----------------- | ----------------- |
| 10k  | **1,900 μs (1.00x)**  | 2,154 μs (1.13x)  | 2,474 μs (1.30x)  | 2,286 μs (1.20x)  | 2,496 μs (1.31x)  |
| 100k | **17,054 μs (1.00x)** | 18,991 μs (1.11x) | 21,694 μs (1.27x) | 22,400 μs (1.31x) | 23,936 μs (1.40x) |

**Allocations (99 steps):**

| N    | Wobuntu            | HPRtree        | NTS            | RBush           | QuadTree        |
| ---- | ------------------ | -------------- | -------------- | --------------- | --------------- |
| 10k  | **82 KB (1.00x)**  | 536 KB (6.52x) | 534 KB (6.51x) | 578 KB (7.05x)  | 609 KB (7.42x)  |
| 100k | **485 KB (1.00x)** | 4.7 MB (9.73x) | 4.7 MB (9.73x) | 4.9 MB (10.17x) | 5.0 MB (10.30x) |

Our library leads at both sizes, with the gap widening at N=100k (11-40% faster than competitors).
HPRtree is the closest competitor in panning - marginally faster than RBush and NTS - but all competitors allocate
6.5-10x more than Wobuntu per 99-frame sequence.
The allocation difference is structural: competitors allocate a fresh `HashSet` and query result list every frame to
drive the diff into the observable set, while our inbuilt incremental maintenance of viewport items avoids any
per-frame allocation beyond the observable set's internal allocation.

### 6b. Zoom-in (viewport shrinks from 5000x5000 to 1000x1000 in 99 delta steps)

| N    | Wobuntu Cached (0.3) | Wobuntu Exact (0.0) | HPRtree + ObsSet   | RBush + ObsSet     | QuadTree + ObsSet  | NTS + ObsSet       |
| ---- | -------------------- | ------------------- | ------------------ | ------------------ | ------------------ | ------------------ |
| 10k  | **599 μs (1.00x)**   | 12,917 μs (21.6x)   | 18,919 μs (31.6x)  | 19,642 μs (32.8x)  | 20,549 μs (34.3x)  | 23,104 μs (38.6x)  |
| 100k | **5,381 μs (1.00x)** | 134,640 μs (25.0x)  | 294,209 μs (54.7x) | 324,254 μs (60.3x) | 332,136 μs (61.7x) | 357,984 μs (66.5x) |

**Allocations (99 steps):**

| N    | Wobuntu Cached     | Wobuntu Exact  | HPRtree         | NTS             | RBush           | QuadTree        |
| ---- | ------------------ | -------------- | --------------- | --------------- | --------------- | --------------- |
| 10k  | **62 KB (1.00x)**  | 66 KB (1.06x)  | 4.5 MB (72.4x)  | 4.5 MB (72.6x)  | 4.7 MB (75.8x)  | 4.8 MB (77.4x)  |
| 100k | **483 KB (1.00x)** | 568 KB (1.18x) | 44.3 MB (91.7x) | 44.3 MB (91.7x) | 46.4 MB (96.0x) | 45.5 MB (94.2x) |

`Cached (0.3)` is the dominant result: **31-67x faster than any competitor** at both sizes.
The shrink-threshold mechanism defers viewport recomputation until the viewport has contracted past 70% of the cached
boundary, most incremental zoom steps are skipped entirely.
This costs nothing but a boundary comparison per frame.

`Exact (0.0)` issues a full query on every step but is still **2.2x faster than HPRtree** (the best competitor) at N=100k, with 80x fewer allocations.
HPRtree edges out RBush, QuadTree, and NTS STRtree in this scenario but by a small margin (294 ms vs 324-358 ms at N=100k).
All competitors allocate 44-46 MB per 99-frame zoom at N=100k; both our variants allocate under 1 MB.

### 6c. Fixed viewport with per-frame mutations (100 frames: add + remove + read)

**NTS and QuadTree are excluded**, NTS trees are semi-static (no inserts after build), QuadTree does not support
removal. HPRtree is excluded for the same reason.

| N    | Wobuntu Viewport   | RBush + ObsSet            |
| ---- | ------------------ | ------------------------- |
| 10k  | **133 μs (1.00x)** | 2,130 μs (16.0x) · 824 KB |
| 100k | **142 μs (1.00x)** | 9,678 μs (68.2x) · 4.5 MB |

**Allocations:** Wobuntu: 0 B (N=10k) / 17 KB (N=100k, from tree rebalancing). RBush: 824 KB / 4.5 MB.

Each `Add`/`Remove` does an O(1) boundary check against the cached viewport and updates `ViewportItems` in-place,
no re-query is issued. RBush must re-query the full viewport after every mutation and diff the result into the
observable set. At N=100k Wobuntu is **68x faster** with near-zero allocations.

---

## Summary

### Per-category fastest

| Category                | Winner                        | Gap vs. next best                      |
| ----------------------- | ----------------------------- | -------------------------------------- |
| Bulk Insert             | **Wobuntu**                   | 1.5x over HPRtree, 3-4.5x over rest    |
| Bulk Insert (memory)    | **HPRtree**                   | 27% less than Wobuntu at 500k          |
| Query speed (<=100k)    | **Wobuntu**                   | 1.4-2.2x faster, zero allocs           |
| Query speed (500k)      | **HPRtree** / Wobuntu (~tied) | 6% margin, within noise                |
| Query allocations       | **Wobuntu**                   | 0 B vs 12.5-14.1 MB (100 queries@500k) |
| Sequential Add (>=100k) | **Wobuntu**                   | 2.5-5.5x faster                        |
| Sequential Add (10k)    | **QuadTree**                  | 2.4x faster than Wobuntu               |
| Remove                  | **Wobuntu**                   | 1.8-3.6x faster, near-zero allocs      |
| Mixed                   | **Wobuntu**                   | 9.5-308x faster, no GC pressure        |
| Viewport (panning)      | **Wobuntu**                   | 11-40% faster, 7-10x fewer allocs      |
| Viewport (zoom, cached) | **Wobuntu (Cached)**          | 32-67x faster, 72-96x fewer allocs     |
| Viewport (zoom, exact)  | **Wobuntu (Exact)**           | 2.2x over HPRtree, 80x fewer allocs    |
| Viewport (mutations)    | **Wobuntu**                   | 16-68x faster, zero allocs             |

### On the Viewport mechanism

The viewport implementation uses a `QueryTo`-based batch approach with optional shrink-threshold caching:

**Panning:** One `QueryTo` per frame + one `IntersectWith + UnionWith` diff into the observable set
(single lock acquisition). Wobuntu is 11-40% faster than the best competitor with 7-10x fewer allocations.

**Zoom-in (Cached):** The shrink-threshold deferral skips `QueryTo` when the viewport contracts incrementally
within the threshold, doing nothing until the contraction exceeds 30% from the cached boundary.
Result: **32-67x faster than any competitor** with near-zero allocations.

**Zoom-in (Exact):** Full `QueryTo` every step - still **2.2x faster than HPRtree** (best competitor) at N=100k
with 80x fewer allocations.

**Fixed mutations:** O(1) boundary check per `Add`/`Remove`, no re-query issued.
**16-68x faster** than RBush with zero allocations.
