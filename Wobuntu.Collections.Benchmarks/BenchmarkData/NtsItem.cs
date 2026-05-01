using NetTopologySuite.Geometries;

namespace Wobuntu.Collections.Benchmarks.BenchmarkData;

/// <summary>
///   NetTopologySuite STRtree stores items with separate Envelope keys,
///   so this is just a simple wrapper to hold the envelope alongside the data.
/// </summary>
public sealed class NtsItem(float x, float y)
{
  public readonly Envelope Envelope = new(x, x + 1.0, y, y + 1.0);
}
