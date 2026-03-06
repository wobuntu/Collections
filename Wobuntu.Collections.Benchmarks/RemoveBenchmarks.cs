using BenchmarkDotNet.Attributes;
using Wobuntu.Collections.Spatial;
using RBush;

namespace Wobuntu.Collections.Benchmarks;

/// <summary>
/// Measures removal of all N items from a pre-built tree.
/// [IterationSetup] reconstructs both trees via bulk load before each measured iteration,
/// so the benchmark covers only the N Remove/Delete calls.
/// </summary>
[SimpleJob]
[MemoryDiagnoser]
public class RemoveBenchmarks
{
  [Params(1_000, 10_000)]
  public int N { get; set; }

  private BenchmarkDataset _dataset = null!;
  private RTree<DataPoint> _ourTree = null!;
  private RBush<RBushItem> _rbushTree = null!;

  [GlobalSetup]
  public void GlobalSetup() => _dataset = new BenchmarkDataset(N);

  [IterationSetup]
  public void IterationSetup()
  {
    _ourTree = new RTree<DataPoint>(_dataset.OurData.AsSpan(), static p => new RTreeBoundary(p.X, p.Y, 1.0, 1.0));
    _rbushTree = new RBush<RBushItem>(maxEntries: 12);
    _rbushTree.BulkLoad(_dataset.RbushData);
  }

  [Benchmark(Baseline = true)]
  public void Wobuntu_Remove()
  {
    for (var index = 0; index < N; index++)
      _ourTree.Remove(_dataset.OurData[index]);
  }

  [Benchmark]
  public void RBush_Remove()
  {
    for (var index = 0; index < N; index++)
      _rbushTree.Delete(_dataset.RbushData[index]);
  }
}
