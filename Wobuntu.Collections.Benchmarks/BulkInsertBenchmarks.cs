using System.Drawing;
using BenchmarkDotNet.Attributes;
using Wobuntu.Collections.Benchmarks.BenchmarkData;
using Wobuntu.Collections.Spatial;
using NetTopologySuite.Index.Strtree;
using QuadTrees;
using RBush;
using NtsEnvelope = NetTopologySuite.Geometries.Envelope;

namespace Wobuntu.Collections.Benchmarks;

/// <summary>
///   Measures how fast each library can construct a tree from N items at once.<br />
///   Our implementation uses the STR (Sort-Tile-Recursive) algorithm via the Span constructor.<br />
///   RBush uses the OMT (Overlap Minimizing Top-down) bulk-load algorithm.<br />
///   NetTopologySuite STRtree builds on first query after all inserts.<br />
///   QuadTrees uses AddRange.
/// </summary>
[SimpleJob]
[MemoryDiagnoser]
public class BulkInsertBenchmarks
{
  [Params(10_000, 100_000, 500_000)]
  public int N { get; set; }

  private BenchmarkDataset _dataset = null!;
  private readonly RTreeOptions _ourOptions = new() { MaxEntriesPerNode = 12 };
  private RTreeOptions _ourOptimizedOptions = null!;

  [GlobalSetup]
  public void Setup()
  {
    _dataset = new BenchmarkDataset(N);
    var estimatedNonLeafNodes = N / 11 + 1;
    _ourOptimizedOptions = new RTreeOptions
    {
      MaxEntriesPerNode = 12,
      InitialNodeCapacity = N + estimatedNonLeafNodes + 1,
      InitialChildBlockCapacity = estimatedNonLeafNodes + 1,
    };
  }

  [Benchmark(Baseline = true)]
  public RTree<DataPoint> Wobuntu_BulkInsert() => new(
    _dataset.OurData.AsSpan(),
    static item => new RTreeBoundary(item.X, item.Y, 1.0f, 1.0f),
    _ourOptions);

  [Benchmark]
  public RTree<DataPoint> Wobuntu_BulkInsert_Optimized() => new(
    _dataset.OurData.AsSpan(),
    static item => new RTreeBoundary(item.X, item.Y, 1.0f, 1.0f),
    _ourOptimizedOptions);

  [Benchmark]
  public RBush<RBushItem> RBush_BulkInsert()
  {
    var tree = new RBush<RBushItem>(maxEntries: 12);
    tree.BulkLoad(_dataset.RBushData);
    return tree;
  }

  [Benchmark]
  public STRtree<NtsItem> NTS_BulkInsert()
  {
    var tree = new STRtree<NtsItem>(12);
    for (var index = 0; index < N; index++)
    {
      var item = _dataset.NtsData[index];
      tree.Insert(item.Envelope, item);
    }

    // STRtree builds on first query; force the build.
    tree.Query(new NtsEnvelope(0, 0, 0, 0));
    return tree;
  }

  [Benchmark]
  public QuadTreeRectF<QuadTreeItem> QuadTree_BulkInsert()
  {
    var tree = new QuadTreeRectF<QuadTreeItem>(new RectangleF(0, 0, 10_001, 10_001));
    tree.AddRange(_dataset.QuadTreeData);
    return tree;
  }
}