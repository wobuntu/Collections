using System.Drawing;
using BenchmarkDotNet.Attributes;
using Wobuntu.Collections.Benchmarks.BenchmarkData;
using Wobuntu.Collections.Spatial;
using NetTopologySuite.Index.Strtree;
using QuadTrees;
using RBush;
using NtsEnvelope = NetTopologySuite.Geometries.Envelope;
using RBushEnvelope = RBush.Envelope;

namespace Wobuntu.Collections.Benchmarks;

/// <summary>
///   Measures query throughput: 100 searches per benchmark call, each over a 1000x1000 window
///   in a 10000x10000 space (roughly 10% area coverage per query).
/// </summary>
[SimpleJob]
[MemoryDiagnoser]
public class QueryBenchmarks
{
  private const int QueryCount = 100;

  [Params(10_000, 100_000, 500_000)]
  public int N { get; set; }

  private RTree<DataPoint> _ourTree = null!;
  private RBush<RBushItem> _rbushTree = null!;
  private STRtree<NtsItem> _ntsTree = null!;
  private QuadTreeRectF<QuadTreeItem> _quadTree = null!;

  private RTreeBoundary[] _ourQueryBoundaries = [];
  private RBushEnvelope[] _rbushQueryEnvelopes = [];
  private NtsEnvelope[] _ntsQueryEnvelopes = [];
  private RectangleF[] _quadTreeQueryRects = [];

  // Reused across calls; QueryTo does not clear it so we do it manually.
  private List<DataPoint> _queryResultBuffer = [];

  [GlobalSetup]
  public void Setup()
  {
    var dataset = new BenchmarkDataset(N);

    _queryResultBuffer.EnsureCapacity(N);
    _ourTree = new RTree<DataPoint>(
      dataset.OurData.AsSpan(),
      static p => new RTreeBoundary(p.X, p.Y, 1.0f, 1.0f),
      new RTreeOptions { MaxEntriesPerNode = 12 });

    _rbushTree = new RBush<RBushItem>(maxEntries: 12);
    _rbushTree.BulkLoad(dataset.RBushData);

    _ntsTree = new STRtree<NtsItem>(12);
    for (var index = 0; index < N; index++)
    {
      var item = dataset.NtsData[index];
      _ntsTree.Insert(item.Envelope, item);
    }
    // NTS does not build the tree until the first query, so we force it here.
    _ntsTree.Query(new NtsEnvelope(0, 0, 0, 0));

    _quadTree = new QuadTreeRectF<QuadTreeItem>(new RectangleF(0, 0, 10_001, 10_001));
    _quadTree.AddRange(dataset.QuadTreeData);

    // Fixed query windows; generated once so time is not spent in setup.
    var random = new Random(99);
    _ourQueryBoundaries = new RTreeBoundary[QueryCount];
    _rbushQueryEnvelopes = new RBushEnvelope[QueryCount];
    _ntsQueryEnvelopes = new NtsEnvelope[QueryCount];
    _quadTreeQueryRects = new RectangleF[QueryCount];

    for (var index = 0; index < QueryCount; index++)
    {
      // Window is 1000x1000 inside a 10000x10000 space -> ~10% area hit rate.
      var x = (float)random.NextDouble() * 9_000;
      var y = (float)random.NextDouble() * 9_000;
      _ourQueryBoundaries[index] = new RTreeBoundary(x, y, 1_000, 1_000);
      _rbushQueryEnvelopes[index] = new RBushEnvelope(x, y, x + 1_000, y + 1_000);
      _ntsQueryEnvelopes[index] = new NtsEnvelope(x, x + 1_000, y, y + 1_000);
      _quadTreeQueryRects[index] = new RectangleF(x, y, 1_000, 1_000);
    }
  }

  [Benchmark(Baseline = true)]
  public int Wobuntu_Query()
  {
    var total = 0;
    for (var index = 0; index < QueryCount; index++)
    {
      total += _ourTree.QueryTo(_ourQueryBoundaries[index], _queryResultBuffer);
      // Our QueryTo does not allocate a collection, instead it requires a target to populate.
      // Hence, clear after each round.
      _queryResultBuffer.Clear();
    }

    return total;
  }

  [Benchmark]
  public int RBush_Query()
  {
    var total = 0;
    for (var index = 0; index < QueryCount; index++)
    { 
      total += _rbushTree.Search(in _rbushQueryEnvelopes[index]).Count;
    }

    return total;
  }

  [Benchmark]
  public int NTS_Query()
  {
    var total = 0;
    for (var index = 0; index < QueryCount; index++)
    {
      total += _ntsTree.Query(_ntsQueryEnvelopes[index]).Count;
    }

    return total;
  }

  [Benchmark]
  public int QuadTree_Query()
  {
    var total = 0;
    for (var index = 0; index < QueryCount; index++)
    {
      total += _quadTree.GetObjects(_quadTreeQueryRects[index]).Count;
    }

    return total;
  }
}