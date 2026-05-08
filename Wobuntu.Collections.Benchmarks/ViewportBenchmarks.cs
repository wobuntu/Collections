using System.Drawing;
using BenchmarkDotNet.Attributes;
using Wobuntu.Collections.Benchmarks.BenchmarkData;
using Wobuntu.Collections.Observable;
using Wobuntu.Collections.Spatial;
using NetTopologySuite.Index.Strtree;
using QuadTrees;
using RBush;
using NtsEnvelope = NetTopologySuite.Geometries.Envelope;
using RBushEnvelope = RBush.Envelope;

namespace Wobuntu.Collections.Benchmarks;

/// <summary>
///   Compares our incremental viewport maintenance against per-frame spatial queries.
///   Our <see cref="RTree{T}.Viewport"/> automatically keeps <see cref="RTree{T}.ViewportItems" /><br />
///   up to date on pan, zoom, and item mutations; all other libraries require an explicit query each frame
///   and manual synchronization with a viewport collection.
///
///   Benchmark fairness:
///   - All competitor libraries maintain a <see cref="SynchronizedObservableOrderedSet{T}"/> that mirrors
///     the current viewport, diffed via IntersectWith + UnionWith after each query. This reflects the minimum
///     work any real consumer needs to produce ordered, observable, deduplicated viewport results.
///   - Initial viewport state (frame[0]) is established in IterationSetup before measurement, so all
///     benchmark loops measure only incremental delta behavior (frames 1–99).
///   - NTS builds its internal tree on the first query; that first query occurs during IterationSetup
///     and is therefore not included in the measured time.
/// </summary>
[SimpleJob]
[MemoryDiagnoser]
public class ViewportBenchmarks
{
  private const int FrameCount = 100;
  private const float ViewportSize = 1_000f;

  [Params(10_000, 100_000)]
  public int N { get; set; }

  private BenchmarkDataset _initialDataset = null!;
  private BenchmarkDataset _mutationDataset = null!;

  // Separate tree instances per scenario so each can have an independent
  // viewport pre-seeded in IterationSetup without conflicting with other benchmarks.
  private RTree<DataPoint> _ourTreePan = null!;        // Threshold=0, pre-seeded to panBoundaries[0]
  private RTree<DataPoint> _ourTreeZoomExact = null!;  // Threshold=0, pre-seeded to zoomBoundaries[0]
  private RTree<DataPoint> _ourTreeZoomCached = null!; // Default threshold, pre-seeded to zoomBoundaries[0]
  private RTree<DataPoint> _ourTreeMutations = null!;  // Threshold=0, pre-seeded to fixedViewport

  private RBush<RBushItem> _rBushTree = null!;
  private STRtree<NtsItem> _ntsTree = null!;
  private QuadTreeRectF<QuadTreeItem> _quadTree = null!;

  // Observable sets maintained by competitor libraries via per-frame diffing (IntersectWith + UnionWith).
  // This is the minimum overhead any real consumer would pay to achieve the same result as our
  // ViewportItems: ordered, deduplicated, and with CollectionChanged event notifications.
  private SynchronizedObservableOrderedSet<RBushItem> _rBushPanViewportItems = null!;
  private SynchronizedObservableOrderedSet<NtsItem> _ntsPanViewportItems = null!;
  private SynchronizedObservableOrderedSet<QuadTreeItem> _quadPanViewportItems = null!;
  private SynchronizedObservableOrderedSet<RBushItem> _rBushZoomViewportItems = null!;
  private SynchronizedObservableOrderedSet<NtsItem> _ntsZoomViewportItems = null!;
  private SynchronizedObservableOrderedSet<QuadTreeItem> _quadZoomViewportItems = null!;
  private SynchronizedObservableOrderedSet<RBushItem> _rBushMutationsViewportItems = null!;

  private RTreeBoundary _fixedViewport;
  private RBushEnvelope _rBushFixedEnvelope;

  private RTreeBoundary[] _panBoundaries = null!;
  private RBushEnvelope[] _rBushPanEnvelopes = null!;
  private NtsEnvelope[] _ntsPanEnvelopes = null!;
  private RectangleF[] _quadPanRects = null!;

  private RTreeBoundary[] _zoomBoundaries = null!;
  private RBushEnvelope[] _rBushZoomEnvelopes = null!;
  private NtsEnvelope[] _ntsZoomEnvelopes = null!;
  private RectangleF[] _quadZoomRects = null!;

  [GlobalSetup]
  public void GlobalSetup()
  {
    _initialDataset = new BenchmarkDataset(N, seed: 42);
    _mutationDataset = new BenchmarkDataset(FrameCount, seed: 0xABCD);

    const float space = 10_000f;
    var fy = (space - ViewportSize) / 2;

    _fixedViewport = new RTreeBoundary(0, fy, ViewportSize, ViewportSize);
    _rBushFixedEnvelope = new RBushEnvelope(0, fy, ViewportSize, fy + ViewportSize);

    _panBoundaries = new RTreeBoundary[FrameCount];
    _rBushPanEnvelopes = new RBushEnvelope[FrameCount];
    _ntsPanEnvelopes = new NtsEnvelope[FrameCount];
    _quadPanRects = new RectangleF[FrameCount];

    for (var index = 0; index < FrameCount; index++)
    {
      var x = (float)index / (FrameCount - 1) * (space - ViewportSize);
      _panBoundaries[index] = new RTreeBoundary(x, fy, ViewportSize, ViewportSize);
      _rBushPanEnvelopes[index] = new RBushEnvelope(x, fy, x + ViewportSize, fy + ViewportSize);
      _ntsPanEnvelopes[index] = new NtsEnvelope(x, x + ViewportSize, fy, fy + ViewportSize);
      _quadPanRects[index] = new RectangleF(x, fy, ViewportSize, ViewportSize);
    }

    _zoomBoundaries = new RTreeBoundary[FrameCount];
    _rBushZoomEnvelopes = new RBushEnvelope[FrameCount];
    _ntsZoomEnvelopes = new NtsEnvelope[FrameCount];
    _quadZoomRects = new RectangleF[FrameCount];

    const float cx = space / 2;
    const float cy = space / 2;
    const float startSize = 5_000f;

    for (var index = 0; index < FrameCount; index++)
    {
      var t = (float)index / (FrameCount - 1);
      var size = startSize - t * (startSize - ViewportSize);
      var x = cx - size / 2;
      var y = cy - size / 2;
      _zoomBoundaries[index] = new RTreeBoundary(x, y, size, size);
      _rBushZoomEnvelopes[index] = new RBushEnvelope(x, y, x + size, y + size);
      _ntsZoomEnvelopes[index] = new NtsEnvelope(x, x + size, y, y + size);
      _quadZoomRects[index] = new RectangleF(x, y, size, size);
    }
  }

  [IterationSetup]
  public void IterationSetup()
  {
    // Wobuntu trees
    _ourTreePan = new RTree<DataPoint>(
      _initialDataset.OurData.AsSpan(),
      static p => new RTreeBoundary(p.X, p.Y, 1f, 1f),
      new RTreeOptions { MaxEntriesPerNode = 12, UpdateViewportItemsOnShrinkThreshold = 0 });

    _ourTreeZoomExact = new RTree<DataPoint>(
      _initialDataset.OurData.AsSpan(),
      static p => new RTreeBoundary(p.X, p.Y, 1f, 1f),
      new RTreeOptions { MaxEntriesPerNode = 12, UpdateViewportItemsOnShrinkThreshold = 0 });

    _ourTreeZoomCached = new RTree<DataPoint>(
      _initialDataset.OurData.AsSpan(),
      static p => new RTreeBoundary(p.X, p.Y, 1f, 1f),
      new RTreeOptions { MaxEntriesPerNode = 12 });

    _ourTreeMutations = new RTree<DataPoint>(
      _initialDataset.OurData.AsSpan(),
      static p => new RTreeBoundary(p.X, p.Y, 1f, 1f),
      new RTreeOptions { MaxEntriesPerNode = 12, UpdateViewportItemsOnShrinkThreshold = 0 });

    // Competitor trees
    _rBushTree = new RBush<RBushItem>(maxEntries: 12);
    _rBushTree.BulkLoad(_initialDataset.RBushData);

    _ntsTree = new STRtree<NtsItem>(12);
    for (var index = 0; index < N; index++)
    {
      var item = _initialDataset.NtsData[index];
      _ntsTree.Insert(item.Envelope, item);
    }

    _quadTree = new QuadTreeRectF<QuadTreeItem>(new RectangleF(0, 0, 10_001, 10_001));
    _quadTree.AddRange(_initialDataset.QuadTreeData);

    // Pre-seed Wobuntu viewports (frame[0] performed here; benchmark loops start at index 1).
    _ourTreePan.Viewport = _panBoundaries[0];
    _ourTreeZoomExact.Viewport = _zoomBoundaries[0];
    _ourTreeZoomCached.Viewport = _zoomBoundaries[0];
    _ourTreeMutations.Viewport = _fixedViewport;

    // Pre-seed competitor panning sets.
    // The first NTS query also triggers the deferred STRtree build.
    var rBushPanInitial = _rBushTree.Search(in _rBushPanEnvelopes[0]);
    _rBushPanViewportItems = new SynchronizedObservableOrderedSet<RBushItem>(rBushPanInitial.Count);
    _rBushPanViewportItems.AddRange(rBushPanInitial);

    var ntsPanInitial = _ntsTree.Query(_ntsPanEnvelopes[0]);
    _ntsPanViewportItems = new SynchronizedObservableOrderedSet<NtsItem>(ntsPanInitial.Count);
    _ntsPanViewportItems.AddRange(ntsPanInitial);

    var quadPanInitial = _quadTree.GetObjects(_quadPanRects[0]);
    _quadPanViewportItems = new SynchronizedObservableOrderedSet<QuadTreeItem>(quadPanInitial.Count);
    _quadPanViewportItems.AddRange(quadPanInitial);

    // Pre-seed competitor zoom sets.
    var rBushZoomInitial = _rBushTree.Search(in _rBushZoomEnvelopes[0]);
    _rBushZoomViewportItems = new SynchronizedObservableOrderedSet<RBushItem>(rBushZoomInitial.Count);
    _rBushZoomViewportItems.AddRange(rBushZoomInitial);

    var ntsZoomInitial = _ntsTree.Query(_ntsZoomEnvelopes[0]);
    _ntsZoomViewportItems = new SynchronizedObservableOrderedSet<NtsItem>(ntsZoomInitial.Count);
    _ntsZoomViewportItems.AddRange(ntsZoomInitial);

    var quadZoomInitial = _quadTree.GetObjects(_quadZoomRects[0]);
    _quadZoomViewportItems = new SynchronizedObservableOrderedSet<QuadTreeItem>(quadZoomInitial.Count);
    _quadZoomViewportItems.AddRange(quadZoomInitial);

    // Pre-seed competitor fixed-viewport mutations set.
    var rBushMutationsInitial = _rBushTree.Search(in _rBushFixedEnvelope);
    _rBushMutationsViewportItems = new SynchronizedObservableOrderedSet<RBushItem>(rBushMutationsInitial.Count);
    _rBushMutationsViewportItems.AddRange(rBushMutationsInitial);
  }

  // Scenario: Panning
  // Viewport slides from left to right in FrameCount steps (~9% step-to-viewport overlap per step).
  // Our tree removes items that left the strip, adds items that entered.
  // Competitors query in full each step, then diff into their observable set.
  // All implementations start from the pre-seeded frame[0] state; loops cover frames 1–99.

  [Benchmark(Baseline = true)]
  public int Wobuntu_Viewport_Panning()
  {
    var total = 0;
    for (var index = 1; index < FrameCount; index++)
    {
      _ourTreePan.Viewport = _panBoundaries[index];
      total += _ourTreePan.ViewportItems.Count;
    }
    return total;
  }

  [Benchmark]
  public int RBush_Panning()
  {
    var total = 0;
    for (var index = 1; index < FrameCount; index++)
    {
      var newItems = _rBushTree.Search(in _rBushPanEnvelopes[index]);
      SyncViewportSet(_rBushPanViewportItems, newItems);
      total += _rBushPanViewportItems.Count;
    }
    return total;
  }

  [Benchmark]
  public int NTS_Panning()
  {
    var total = 0;
    for (var index = 1; index < FrameCount; index++)
    {
      var newItems = _ntsTree.Query(_ntsPanEnvelopes[index]);
      SyncViewportSet(_ntsPanViewportItems, newItems);
      total += _ntsPanViewportItems.Count;
    }
    return total;
  }

  [Benchmark]
  public int QuadTree_Panning()
  {
    var total = 0;
    for (var index = 1; index < FrameCount; index++)
    {
      var newItems = _quadTree.GetObjects(_quadPanRects[index]);
      SyncViewportSet(_quadPanViewportItems, newItems);
      total += _quadPanViewportItems.Count;
    }
    return total;
  }

  // Scenario: Zoom-in
  // Viewport shrinks from 5000×5000 to 1000×1000 (center-pinned) over FrameCount steps.
  // Wobuntu_ZoomIn_Cached (default threshold 0.3): skips updates for small incremental shrinks.
  // Wobuntu_ZoomIn_Exact (threshold 0): updates on every shrink step.
  // Competitors query in full each step, then diff into their observable set.
  // All implementations start from the pre-seeded frame[0] state; loops cover frames 1–99.

  [Benchmark]
  public int Wobuntu_ZoomIn_Cached()
  {
    var total = 0;
    for (var index = 1; index < FrameCount; index++)
    {
      _ourTreeZoomCached.Viewport = _zoomBoundaries[index];
      total += _ourTreeZoomCached.ViewportItems.Count;
    }
    return total;
  }

  [Benchmark]
  public int Wobuntu_ZoomIn_Exact()
  {
    var total = 0;
    for (var index = 1; index < FrameCount; index++)
    {
      _ourTreeZoomExact.Viewport = _zoomBoundaries[index];
      total += _ourTreeZoomExact.ViewportItems.Count;
    }
    return total;
  }

  [Benchmark]
  public int RBush_ZoomIn()
  {
    var total = 0;
    for (var index = 1; index < FrameCount; index++)
    {
      var newItems = _rBushTree.Search(in _rBushZoomEnvelopes[index]);
      SyncViewportSet(_rBushZoomViewportItems, newItems);
      total += _rBushZoomViewportItems.Count;
    }
    return total;
  }

  [Benchmark]
  public int NTS_ZoomIn()
  {
    var total = 0;
    for (var index = 1; index < FrameCount; index++)
    {
      var newItems = _ntsTree.Query(_ntsZoomEnvelopes[index]);
      SyncViewportSet(_ntsZoomViewportItems, newItems);
      total += _ntsZoomViewportItems.Count;
    }
    return total;
  }

  [Benchmark]
  public int QuadTree_ZoomIn()
  {
    var total = 0;
    for (var index = 1; index < FrameCount; index++)
    {
      var newItems = _quadTree.GetObjects(_quadZoomRects[index]);
      SyncViewportSet(_quadZoomViewportItems, newItems);
      total += _quadZoomViewportItems.Count;
    }
    return total;
  }

  // Scenario: Fixed viewport with per-frame mutations
  // Items are inserted and removed while the viewport stays fixed.
  // Our tree auto-maintains ViewportItems during each Add/Remove call.
  // Competitors re-query after every mutation, then diff into their observable set.
  // NTS (read-only after build) and QuadTree (no removal support) are excluded.
  // Initial viewport state pre-seeded in IterationSetup; all FrameCount mutations are measured.

  [Benchmark]
  public int Wobuntu_FixedWithMutations()
  {
    var total = 0;
    for (var index = 0; index < FrameCount; index++)
    {
      _ourTreeMutations.Add(_mutationDataset.OurData[index]);
      _ourTreeMutations.Remove(_initialDataset.OurData[index]);
      total += _ourTreeMutations.ViewportItems.Count;
    }
    return total;
  }

  [Benchmark]
  public int RBush_FixedWithMutations()
  {
    var total = 0;
    for (var index = 0; index < FrameCount; index++)
    {
      _rBushTree.Insert(_mutationDataset.RBushData[index]);
      _rBushTree.Delete(_initialDataset.RBushData[index]);
      var newItems = _rBushTree.Search(in _rBushFixedEnvelope);
      SyncViewportSet(_rBushMutationsViewportItems, newItems);
      total += _rBushMutationsViewportItems.Count;
    }
    return total;
  }

  /// <summary>
  ///   Updates <paramref name="set"/> to exactly match <paramref name="newItems"/> using a single diff pass:
  ///   removes items that are no longer in the result, then adds newly visible items.
  ///   Fires two batched CollectionChanged events per call (one Remove or Clear, one Add).
  /// </summary>
  private static void SyncViewportSet<T>(SynchronizedObservableOrderedSet<T> set, IEnumerable<T> newItems)
    where T : notnull
  {
    var newSet = new HashSet<T>(newItems);
    set.IntersectWith(newSet); // removes items no longer in viewport; fires one Remove event
    set.UnionWith(newSet);     // adds newly visible items; fires one Add event
  }
}