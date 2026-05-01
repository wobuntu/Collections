using System;

namespace Wobuntu.Collections.Benchmarks.BenchmarkData;

/// <summary>
///   Pre-generated data shared across benchmark iterations within one [GlobalSetup].
/// </summary>
public sealed class BenchmarkDataset
{
  public readonly DataPoint[] OurData;
  public readonly RBushItem[] RBushData;
  public readonly QuadTreeItem[] QuadTreeData;
  public readonly NtsItem[] NtsData;

  public BenchmarkDataset(int n, int seed = 42)
  {
    var random = new Random(seed);

    OurData = new DataPoint[n];
    RBushData = new RBushItem[n];
    QuadTreeData = new QuadTreeItem[n];
    NtsData = new NtsItem[n];

    for (var index = 0; index < n; index++)
    {
      var x = (float)random.NextDouble() * 10_000;
      var y = (float)random.NextDouble() * 10_000;
      
      OurData[index] = new DataPoint(x, y);
      RBushData[index] = new RBushItem(x, y);
      QuadTreeData[index] = new QuadTreeItem(x, y);
      NtsData[index] = new NtsItem(x, y);
    }
  }
}