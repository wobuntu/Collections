using BenchmarkDotNet.Attributes;
using Wobuntu.Collections.Spatial;
using RBush;

namespace Wobuntu.Collections.Benchmarks;

/// <summary>
/// Measures query throughput: 100 searches per benchmark call, each over a 1000×1000 window
/// in a 10000×10000 space (roughly 10% area coverage per query).
/// <br/>
/// Key asymmetry highlighted here:
///   Our QueryTo reuses a pre-allocated List and does not allocate per query.
///   RBush.Search allocates a new IReadOnlyList per call.
/// The memory diagnoser will make this visible in the Allocated column.
/// </summary>
[SimpleJob]
[MemoryDiagnoser]
public class QueryBenchmarks
{
  private const int QueryCount = 100;

  [Params(1_000, 10_000, 100_000)]
  public int N { get; set; }

  private RTree<DataPoint> _ourTree = null!;
  private RBush<RBushItem> _rbushTree = null!;
  private RTreeBoundary[] _ourQueryBoundaries = [];
  private Envelope[] _rbushQueryEnvelopes = [];

  // Reused across calls; QueryTo does not clear it so we do it manually.
  private readonly List<DataPoint> _queryResultBuffer = new(256);

  [GlobalSetup]
  public void Setup()
  {
    var dataset = new BenchmarkDataset(N);
    _ourTree = new RTree<DataPoint>(dataset.OurData.AsSpan(), static p => new RTreeBoundary(p.X, p.Y, 1.0f, 1.0f));
    _rbushTree = new RBush<RBushItem>(maxEntries: 12);
    _rbushTree.BulkLoad(dataset.RbushData);

    // Fixed query windows; generated once so time is not spent in setup.
    var rng = new Random(99);
    _ourQueryBoundaries = new RTreeBoundary[QueryCount];
    _rbushQueryEnvelopes = new Envelope[QueryCount];

    for (var index = 0; index < QueryCount; index++)
    {
      // Window is 1000×1000 inside a 10000×10000 space → ~10% area hit rate.
      var x = (float)rng.NextDouble() * 9_000;
      var y = (float)rng.NextDouble() * 9_000;
      _ourQueryBoundaries[index] = new RTreeBoundary(x, y, 1_000, 1_000);
      _rbushQueryEnvelopes[index] = new Envelope(x, y, x + 1_000, y + 1_000);
    }
  }

  /// <summary>
  /// Our QueryTo writes into a caller-supplied List; zero allocation per query.
  /// </summary>
  [Benchmark(Baseline = true)]
  public int Wobuntu_Query()
  {
    var total = 0;
    for (var index = 0; index < QueryCount; index++)
    {
      total += _ourTree.QueryTo(_ourQueryBoundaries[index], _queryResultBuffer);
      _queryResultBuffer.Clear();
    }

    return total;
  }

  /// <summary>
  /// RBush.Search returns a new IReadOnlyList per call, allocating on each query.
  /// </summary>
  [Benchmark]
  public int RBush_Query()
  {
    var total = 0;
    for (var index = 0; index < QueryCount; index++)
      total += _rbushTree.Search(in _rbushQueryEnvelopes[index]).Count;

    return total;
  }
}
