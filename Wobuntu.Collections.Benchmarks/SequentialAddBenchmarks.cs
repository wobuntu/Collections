using BenchmarkDotNet.Attributes;
using Wobuntu.Collections.Spatial;
using RBush;

namespace Wobuntu.Collections.Benchmarks;

/// <summary>
/// Measures how fast each library can insert N items one at a time into an initially empty tree.
/// This exercises the single-item insertion path, which differs fundamentally from bulk loading:
/// our tree uses center-distance node selection without splits; RBush uses R*-tree reinsertion.
/// <br/>
/// [IterationSetup] resets both trees to empty before each measured iteration so that no item
/// is present at the start and the measurement covers only the N insertions.
/// </summary>
[SimpleJob]
[MemoryDiagnoser]
public class SequentialAddBenchmarks
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
    _ourTree = new RTree<DataPoint>(static p => new RTreeBoundary(p.X, p.Y, 1.0f, 1.0f));
    _rbushTree = new RBush<RBushItem>(maxEntries: 12);
  }

  [Benchmark(Baseline = true)]
  public void Wobuntu_SequentialAdd()
  {
    for (var index = 0; index < N; index++)
      _ourTree.Add(_dataset.OurData[index]);
  }

  [Benchmark]
  public void RBush_SequentialAdd()
  {
    for (var index = 0; index < N; index++)
      _rbushTree.Insert(_dataset.RbushData[index]);
  }
}
