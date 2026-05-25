using BenchmarkDotNet.Attributes;
using Wobuntu.Collections.Benchmarks.BenchmarkData;
using Wobuntu.Collections.Spatial;
using NetTopologySuite.Index.Strtree;
using RBush;
using NtsEnvelope = NetTopologySuite.Geometries.Envelope;

namespace Wobuntu.Collections.Benchmarks;

/// <summary>
///   Measures removal of all N items from a pre-built tree.
///   Note: QuadTrees and NTS HPRtree are excluded — neither supports individual item removal.
/// </summary>
[SimpleJob]
[MemoryDiagnoser]
public class RemoveBenchmarks
{
  [Params(10_000, 100_000, 500_000)]
  public int N { get; set; }

  private BenchmarkDataset _dataset = null!;
  private RTree<DataPoint> _ourTree = null!;
  private RBush<RBushItem> _rbushTree = null!;
  private STRtree<NtsItem> _ntsTree = null!;

  [GlobalSetup]
  public void GlobalSetup() => _dataset = new BenchmarkDataset(N);

  [IterationSetup]
  public void IterationSetup()
  {
    _ourTree = new RTree<DataPoint>(
      _dataset.OurData.AsSpan(),
      static p => new RTreeBoundary(p.X, p.Y, 1.0f, 1.0f),
      new RTreeOptions { MaxEntriesPerNode = 12 });

    _rbushTree = new RBush<RBushItem>(maxEntries: 12);
    _rbushTree.BulkLoad(_dataset.RBushData);

    _ntsTree = new STRtree<NtsItem>(12);
    for (var index = 0; index < N; index++)
    {
      var item = _dataset.NtsData[index];
      _ntsTree.Insert(item.Envelope, item);
    }
    _ntsTree.Query(new NtsEnvelope(0, 0, 0, 0)); // Force build
  }

  [Benchmark(Baseline = true)]
  public void Wobuntu_Remove()
  {
    for (var index = 0; index < N; index++)
    {
      _ourTree.Remove(_dataset.OurData[index]);
    }
  }

  [Benchmark]
  public void RBush_Remove()
  {
    for (var index = 0; index < N; index++)
    {
      _rbushTree.Delete(_dataset.RBushData[index]);
    }
  }

  [Benchmark]
  public void NTS_Remove()
  {
    for (var index = 0; index < N; index++)
    {
      var item = _dataset.NtsData[index];
      _ntsTree.Remove(item.Envelope, item);
    }
  }
}