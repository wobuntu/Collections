#nullable enable

using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Wobuntu.Collections.Spatial;

// Note the similarity to a normal Rectangle.
// However, this immutable struct was created on purpose as it provides some optimizations for its usage in RTree.
// Benchmark if members added, as it is used in optimized hotpaths and falling out of cache lines may have a
// noticeable impact on performance (also the reason for some common properties to be computed and not members).

public readonly struct RTreeBoundary
  : IEquatable<RTreeBoundary>, IFormattable
{
  public readonly double X;
  public readonly double Y;

  public readonly double Right;
  public readonly double Bottom;

  public RTreeBoundary(double x, double y, double width, double height)
  {
    ThrowIfInfiniteOrNaN(x);
    ThrowIfInfiniteOrNaN(y);
    ThrowIfNegativeInfiniteOrNaN(width);
    ThrowIfNegativeInfiniteOrNaN(height);

    X = x;
    Y = y;
    Right = x + width;
    Bottom = y + height;
  }

  public double Width => Right - X;
  public double Height => Bottom - Y;
  public double CenterX => (X + Right) * .5;
  public double CenterY => (Y + Bottom) * .5;

  public bool IsEmpty => Width <= 0 || Height <= 0;

  public bool Intersects(RTreeBoundary other) =>
    !IsEmpty
    && !other.IsEmpty
    && X <= other.Right
    && Right >= other.X
    && Y <= other.Bottom
    && Bottom >= other.Y;

  public bool Contains(RTreeBoundary other) =>
    !IsEmpty
    && !other.IsEmpty
    && X <= other.X
    && Right >= other.Right
    && Y <= other.Y
    && Bottom >= other.Bottom;

  public bool Contains(double x, double y) =>
    !IsEmpty && x >= X && x <= Right && y >= Y && y <= Bottom;

  internal bool IntersectsUnchecked(in RTreeBoundary other) =>
    X <= other.Right
    && Right >= other.X
    && Y <= other.Bottom
    && Bottom >= other.Y;

  internal bool ContainsUnchecked(in RTreeBoundary other) =>
    X <= other.X
    && Right >= other.Right
    && Y <= other.Y
    && Bottom >= other.Bottom;

  public RTreeBoundary Union(RTreeBoundary other)
  {
    if (IsEmpty)
    {
      return other;
    }

    if (other.IsEmpty)
    {
      return this;
    }

    var left = Math.Min(X, other.X);
    var top = Math.Min(Y, other.Y);
    var right = Math.Max(Right, other.Right);
    var bottom = Math.Max(Bottom, other.Bottom);

    return new RTreeBoundary(left, top, right - left, bottom - top);
  }

  public override bool Equals(object? obj) =>
    obj is RTreeBoundary rectangle
    && Equals(rectangle);

  public bool Equals(RTreeBoundary other) =>
    // ReSharper disable CompareOfFloatsByEqualityOperator : We do it here like .NET does it
    X == other.X
    && Y == other.Y
    && Right == other.Right
    && Bottom == other.Bottom;

  public override int GetHashCode() => HashCode.Combine(X, Y, Right, Bottom);

  public override string ToString() => ConvertToString(null, null);

  public string ToString(IFormatProvider? provider) => ConvertToString(null, provider);

  public string ToString(string? format, IFormatProvider? provider) => ConvertToString(format, provider);

  public static bool operator ==(RTreeBoundary left, RTreeBoundary right) => left.Equals(right);

  public static bool operator !=(RTreeBoundary left, RTreeBoundary right) => !left.Equals(right);

  private string ConvertToString(string? format, IFormatProvider? provider)
  {
    if (IsEmpty)
    {
      return "Empty";
    }

    var separator = ',';
    var formatInfo = NumberFormatInfo.GetInstance(provider);
    if (formatInfo.NumberDecimalSeparator.Length > 0 && separator == formatInfo.NumberDecimalSeparator[0])
    {
      separator = ';';
    }

    var fullFormatString = "{1:" + format + "}{0}{2:" + format + "}{0}{3:" + format + "}{0}{4:" + format + "}";
    return string.Format(provider, fullFormatString, separator, X, Y, Width, Height);
  }

  private static void ThrowIfInfiniteOrNaN(
    double value,
    [CallerArgumentExpression(nameof(value))]
    string? valueExpression = null)
  {
    if (double.IsInfinity(value))
    {
      throw new ArgumentException("The value must not be infinite.", valueExpression);
    }

    if (double.IsNaN(value))
    {
      throw new ArgumentException("The value must be a number.", valueExpression);
    }
  }

  private static void ThrowIfNegativeInfiniteOrNaN(
    double value,
    [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
  {
    if (value < 0)
    {
      throw new ArgumentException("The value must not be less than 0.", valueExpression);
    }

    if (double.IsInfinity(value))
    {
      throw new ArgumentException("The value must not be infinite.", valueExpression);
    }

    if (double.IsNaN(value))
    {
      throw new ArgumentException("The value must be a number.", valueExpression);
    }
  }
}