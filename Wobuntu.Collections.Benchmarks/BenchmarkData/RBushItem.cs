using RBush;

namespace Wobuntu.Collections.Benchmarks.BenchmarkData;

/// <summary>
///   RBush requires items to implement ISpatialData with a ref-readonly Envelope field.<br />
///   The Envelope stores (MinX, MinY, MaxX, MaxY); we treat each point as a 1×1 square
///   to match the RTreeBoundary(x, y, 1, 1) used for our tree.
/// </summary>
public sealed class RBushItem(float x, float y) : ISpatialData
{
  private readonly Envelope _envelope = new(x, y, x + 1.0, y + 1.0);
  public ref readonly Envelope Envelope => ref _envelope;
}