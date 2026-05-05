using System.Drawing;
using BenchmarkDotNet.Attributes;
using Wobuntu.Collections.Benchmarks.BenchmarkData;
using Wobuntu.Collections.Spatial;
using QuadTrees;
using RBush;

namespace Wobuntu.Collections.Benchmarks;

/// <summary>
///   Measures how fast each library can insert N items one at a time into an initially empty tree.
///   NTS is excluded — STRtree only appends to a list during Insert and defers tree construction
///   to the first query, making sequential insert timings not comparable.
/// </summary>
[SimpleJob]
[MemoryDiagnoser]
public class SequentialAddBenchmarks
{
  [Params(10_000, 100_000, 500_000)]
  public int N { get; set; }

  private BenchmarkDataset _dataset = null!;
  private RTree<DataPoint> _ourTree = null!;
  private RTree<DataPoint> _ourOptimizedTree = null!;
  private RBush<RBushItem> _rbushTree = null!;
  private QuadTreeRectF<QuadTreeItem> _quadTree = null!;

  [GlobalSetup]
  public void GlobalSetup() => _dataset = new BenchmarkDataset(N);

  [IterationSetup]
  public void IterationSetup()
  {
    _ourTree = new RTree<DataPoint>(
      static p => new RTreeBoundary(p.X, p.Y, 1.0f, 1.0f),
      new RTreeOptions { MaxEntriesPerNode = 12 });

    var estimatedNonLeafNodes = N / 11 + 1;
    _ourOptimizedTree = new RTree<DataPoint>(
      static p => new RTreeBoundary(p.X, p.Y, 1.0f, 1.0f),
      new RTreeOptions
      {
        MaxEntriesPerNode = 12,
        InitialNodeCapacity = N + estimatedNonLeafNodes + 1,
        InitialChildBlockCapacity = estimatedNonLeafNodes + 1,
      });

    _rbushTree = new RBush<RBushItem>(maxEntries: 12);
    _quadTree = new QuadTreeRectF<QuadTreeItem>(new RectangleF(0, 0, 10_001, 10_001));
  }

  [Benchmark(Baseline = true)]
  public void Wobuntu_SequentialAdd()
  {
    for (var index = 0; index < N; index++)
    {
      _ourTree.Add(_dataset.OurData[index]);
    }
  }

  [Benchmark]
  public void Wobuntu_SequentialAdd_Optimized()
  {
    for (var index = 0; index < N; index++)
    {
      _ourOptimizedTree.Add(_dataset.OurData[index]);
    }
  }

  [Benchmark]
  public void RBush_SequentialAdd()
  {
    for (var index = 0; index < N; index++)
    {
      _rbushTree.Insert(_dataset.RBushData[index]);
    }
  }

  [Benchmark]
  public void QuadTree_SequentialAdd()
  {
    for (var index = 0; index < N; index++)
    {
      _quadTree.Add(_dataset.QuadTreeData[index]);
    }
  }
}