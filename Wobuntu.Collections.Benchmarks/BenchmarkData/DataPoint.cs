namespace Wobuntu.Collections.Benchmarks.BenchmarkData;

/// <summary>
///   A minimal 2D point used as the item type in our RTree benchmarks.<br />
///   Proper Equals/GetHashCode overrides ensure correct Dictionary&lt;T,V&gt; behavior
///   inside RTree and produce numbers representative of real usage.
/// </summary>
public readonly struct DataPoint(float x, float y) : IEquatable<DataPoint>
{
  public readonly float X = x;
  public readonly float Y = y;

  // ReSharper disable CompareOfFloatsByEqualityOperator
  public bool Equals(DataPoint other) => X == other.X && Y == other.Y;
  // ReSharper restore CompareOfFloatsByEqualityOperator

  public override bool Equals(object? obj) => obj is DataPoint other && Equals(other);
  public override int GetHashCode() => HashCode.Combine(X, Y);

  public static bool operator ==(DataPoint left, DataPoint right) => left.Equals(right);
  public static bool operator !=(DataPoint left, DataPoint right) => !left.Equals(right);
}