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
  public readonly float X;
  public readonly float Y;

  public readonly float Right;
  public readonly float Bottom;

  public readonly float CenterX;
  public readonly float CenterY;

  public RTreeBoundary(float x, float y, float width, float height)
  {
    ThrowIfInfiniteOrNaN(x);
    ThrowIfInfiniteOrNaN(y);
    ThrowIfNegativeInfiniteOrNaN(width);
    ThrowIfNegativeInfiniteOrNaN(height);

    X = x;
    Y = y;
    Right = x + width;
    Bottom = y + height;
    CenterX = (X + Right) * .5f;
    CenterY = (Y + Bottom) * .5f;
  }

  public float Width => Right - X;
  public float Height => Bottom - Y;

  public bool IsEmpty
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => X >= Right || Y >= Bottom;
  }

  public bool Intersects(in RTreeBoundary other) =>
    !IsEmpty
    && !other.IsEmpty
    && X <= other.Right
    && Right >= other.X
    && Y <= other.Bottom
    && Bottom >= other.Y;

  public bool Contains(in RTreeBoundary other) =>
    !IsEmpty
    && !other.IsEmpty
    && X <= other.X
    && Right >= other.Right
    && Y <= other.Y
    && Bottom >= other.Bottom;

  public bool Contains(float x, float y) =>
    !IsEmpty && x >= X && x <= Right && y >= Y && y <= Bottom;

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal bool IntersectsUnchecked(in RTreeBoundary other) =>
    X <= other.Right
    && Right >= other.X
    && Y <= other.Bottom
    && Bottom >= other.Y;

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal bool ContainsUnchecked(in RTreeBoundary other) =>
    X <= other.X
    && Right >= other.Right
    && Y <= other.Y
    && Bottom >= other.Bottom;

  public RTreeBoundary Union(in RTreeBoundary other)
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
    && Equals(in rectangle);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool Equals(in RTreeBoundary other) =>
    // ReSharper disable CompareOfFloatsByEqualityOperator : We do it here like .NET does it
    X == other.X
    && Y == other.Y
    && Right == other.Right
    && Bottom == other.Bottom;

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool operator ==(in RTreeBoundary left, in RTreeBoundary right) => left.Equals(in right);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool operator !=(in RTreeBoundary left, in RTreeBoundary right) => !left.Equals(in right);

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
    float value,
    [CallerArgumentExpression(nameof(value))]
    string? valueExpression = null)
  {
    if (float.IsInfinity(value))
    {
      throw new ArgumentException("The value must not be infinite.", valueExpression);
    }

    if (float.IsNaN(value))
    {
      throw new ArgumentException("The value must be a number.", valueExpression);
    }
  }

  private static void ThrowIfNegativeInfiniteOrNaN(
    float value,
    [CallerArgumentExpression(nameof(value))] string? valueExpression = null)
  {
    if (value < 0)
    {
      throw new ArgumentException("The value must not be less than 0.", valueExpression);
    }

    if (float.IsInfinity(value))
    {
      throw new ArgumentException("The value must not be infinite.", valueExpression);
    }

    if (float.IsNaN(value))
    {
      throw new ArgumentException("The value must be a number.", valueExpression);
    }
  }
}