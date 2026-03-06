using BenchmarkDotNet.Attributes;
using Wobuntu.Collections.Spatial;
using RBush;

namespace Wobuntu.Collections.Benchmarks;

/// <summary>
/// Simulates a viewport update cycle: a tree is pre-built with N items, then N/2 items
/// are removed (items that left the visible area) followed by N/2 new items being added
/// (items that entered the visible area). This is the primary hot path in the intended
/// UI/mapping use case.
/// </summary>
[SimpleJob]
[MemoryDiagnoser]
public class MixedBenchmarks
{
  [Params(1_000, 10_000)]
  public int N { get; set; }

  private BenchmarkDataset _initialDataset = null!;
  private BenchmarkDataset _incomingDataset = null!;
  private RTree<DataPoint> _ourTree = null!;
  private RBush<RBushItem> _rbushTree = null!;

  [GlobalSetup]
  public void GlobalSetup()
  {
    _initialDataset = new BenchmarkDataset(N, seed: 42);
    // Use a different seed so the incoming items don't overlap with the initial items.
    _incomingDataset = new BenchmarkDataset(N / 2, seed: 1337);
  }

  [IterationSetup]
  public void IterationSetup()
  {
    _ourTree = new RTree<DataPoint>(_initialDataset.OurData.AsSpan(), static p => new RTreeBoundary(p.X, p.Y, 1.0f, 1.0f));
    _rbushTree = new RBush<RBushItem>(maxEntries: 12);
    _rbushTree.BulkLoad(_initialDataset.RbushData);
  }

  [Benchmark(Baseline = true)]
  public void Wobuntu_Mixed()
  {
    // Remove the first half of the initial items.
    for (var index = 0; index < N / 2; index++)
      _ourTree.Remove(_initialDataset.OurData[index]);

    // Add the incoming items.
    for (var index = 0; index < N / 2; index++)
      _ourTree.Add(_incomingDataset.OurData[index]);
  }

  [Benchmark]
  public void RBush_Mixed()
  {
    for (var index = 0; index < N / 2; index++)
      _rbushTree.Delete(_initialDataset.RbushData[index]);

    for (var index = 0; index < N / 2; index++)
      _rbushTree.Insert(_incomingDataset.RbushData[index]);
  }
}
