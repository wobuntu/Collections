using BenchmarkDotNet.Attributes;
using Wobuntu.Collections.Benchmarks.BenchmarkData;
using Wobuntu.Collections.Spatial;
using RBush;

namespace Wobuntu.Collections.Benchmarks;

/// <summary>
///   Simulates a viewport update cycle: a tree is pre-built with N items, then N/2 items
///   are pseudo-randomly removed or inserted.<br />
///   Note: NTS and QuadTrees are excluded — NTS is semi-static (no inserts after build),
///   QuadTrees does not support removal.
/// </summary>
[SimpleJob]
[MemoryDiagnoser]
public class MixedBenchmarks
{
  [Params(10_000, 100_000, 500_000)]
  public int N { get; set; }

  private BenchmarkDataset _initialDataset = null!;
  private BenchmarkDataset _incomingDataset = null!;
  private RTree<DataPoint> _ourTree = null!;
  private RBush<RBushItem> _rBushTree = null!;

  [GlobalSetup]
  public void GlobalSetup()
  {
    _initialDataset = new BenchmarkDataset(N, seed: 42);
    // Use a different seed so the incoming items don't overlap with the initial items.
    _incomingDataset = new BenchmarkDataset(N / 2, seed: 0xc0ffee);
  }

  [IterationSetup]
  public void IterationSetup()
  {
    _ourTree = new RTree<DataPoint>(
      _initialDataset.OurData.AsSpan(),
      static item => new RTreeBoundary(item.X, item.Y, 1.0f, 1.0f),
      new RTreeOptions { MaxEntriesPerNode = 12 });

    _rBushTree = new RBush<RBushItem>(maxEntries: 12);
    _rBushTree.BulkLoad(_initialDataset.RBushData);
  }

  [Benchmark(Baseline = true)]
  public void Wobuntu_Mixed()
  {
    var random = new Random(123);
    for (var index = 0; index < N / 2; index++)
    {
      if (random.Next(0, 2) == 0)
      {
        _ourTree.Remove(_initialDataset.OurData[index]);
      }
      else
      {
        _ourTree.Add(_incomingDataset.OurData[index]);
      }
    }
  }

  [Benchmark]
  public void RBush_Mixed()
  {
    var random = new Random(123);
    for (var index = 0; index < N / 2; index++)
    {
      if (random.Next(0, 2) == 0)
      {
        _rBushTree.Delete(_initialDataset.RBushData[index]);
      }
      else
      {
        _rBushTree.Insert(_incomingDataset.RBushData[index]);
      }
    }
  }
}