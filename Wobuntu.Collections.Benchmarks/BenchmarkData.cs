using Wobuntu.Collections.Spatial;
using RBush;

namespace Wobuntu.Collections.Benchmarks;

// A minimal 2D point used as the item type in our RTree benchmarks.
// Proper Equals/GetHashCode overrides ensure correct Dictionary<T,V> behavior
// inside RTree and produce numbers representative of real usage.
public readonly struct DataPoint(float x, float y) : IEquatable<DataPoint>
{
  public readonly float X = x;
  public readonly float Y = y;

  public bool Equals(DataPoint other) => X == other.X && Y == other.Y;
  public override bool Equals(object? obj) => obj is DataPoint other && Equals(other);
  public override int GetHashCode() => HashCode.Combine(X, Y);
}

// RBush requires items to implement ISpatialData with a ref-readonly Envelope field.
// The Envelope stores (MinX, MinY, MaxX, MaxY); we treat each point as a 1×1 square
// to match the RTreeBoundary(x, y, 1, 1) used for our tree.
public sealed class RBushItem(float x, float y) : ISpatialData
{
  private readonly Envelope _envelope = new(x, y, x + 1.0, y + 1.0);

  public ref readonly Envelope Envelope => ref _envelope;
}

// Pre-generated data shared across benchmark iterations within one [GlobalSetup].
public sealed class BenchmarkDataset
{
  public readonly DataPoint[] OurData;
  public readonly RBushItem[] RbushData;

  public BenchmarkDataset(int n, int seed = 42)
  {
    var rng = new Random(seed);
    OurData = new DataPoint[n];
    RbushData = new RBushItem[n];

    for (var index = 0; index < n; index++)
    {
      var x = (float)rng.NextDouble() * 10_000;
      var y = (float)rng.NextDouble() * 10_000;
      OurData[index] = new DataPoint(x, y);
      RbushData[index] = new RBushItem(x, y);
    }
  }
}
