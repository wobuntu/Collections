using BenchmarkDotNet.Attributes;
using Wobuntu.Collections.Spatial;
using RBush;

namespace Wobuntu.Collections.Benchmarks;

/// <summary>
/// Measures how fast each library can construct a tree from N items at once.
/// Our implementation uses the STR (Sort-Tile-Recursive) algorithm via the Span constructor.
/// RBush uses the OMT (Overlap Minimizing Top-down) bulk-load algorithm.
/// </summary>
[SimpleJob]
[MemoryDiagnoser]
public class BulkInsertBenchmarks
{
  [Params(1_000, 10_000, 100_000)]
  public int N { get; set; }

  private BenchmarkDataset _dataset = null!;

  [GlobalSetup]
  public void Setup() => _dataset = new BenchmarkDataset(N);

  [Benchmark(Baseline = true)]
  public RTree<DataPoint> Wobuntu_BulkInsert()
    => new(_dataset.OurData.AsSpan(), static p => new RTreeBoundary(p.X, p.Y, 1.0f, 1.0f));

  [Benchmark]
  public RBush<RBushItem> RBush_BulkInsert()
  {
    var tree = new RBush<RBushItem>(maxEntries: 12);
    tree.BulkLoad(_dataset.RbushData);
    return tree;
  }
}
