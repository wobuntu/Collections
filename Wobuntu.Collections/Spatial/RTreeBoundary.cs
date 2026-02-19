#nullable enable

using System;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace Wobuntu.Collections.Spatial;

// Note the similarity to a normal Rectangle.
// However, this immutable struct was created on purpose as it provides some optimizations for its usage in RTree.

public readonly struct RTreeBoundary
  : IEquatable<RTreeBoundary>
{
  public readonly double X;
  public readonly double Y;
  public readonly double Width;
  public readonly double Height;

  public RTreeBoundary(double x, double y, double width, double height)
  {
    ThrowIfInfiniteOrNaN(x);
    ThrowIfInfiniteOrNaN(y);
    ThrowIfNegativeInfiniteOrNaN(width);
    ThrowIfNegativeInfiniteOrNaN(height);

    X = x;
    Y = y;
    Width = width;
    Height = height;
  }

  public double Left => X;
  public double Top => Y;
  public double Right => X + Width;
  public double Bottom => Y + Height;
  public double CenterX => X + Width / 2;
  public double CenterY => Y + Height / 2;
  public double Area => Width * Height;

  public bool IsEmpty => Width <= 0 || Height <= 0;

  public bool Intersects(RTreeBoundary other) =>
    !IsEmpty
    && !other.IsEmpty
    && Left <= other.Right
    && Right >= other.Left
    && Top <= other.Bottom
    && Bottom >= other.Top;

  public bool Contains(RTreeBoundary other) =>
    !IsEmpty
    && !other.IsEmpty
    && Left <= other.Left
    && Right >= other.Right
    && Top <= other.Top
    && Bottom >= other.Bottom;

  public bool Contains(double x, double y) =>
    !IsEmpty && x >= Left && x <= Right && y >= Top && y <= Bottom;

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

    var left = Math.Min(Left, other.Left);
    var top = Math.Min(Top, other.Top);
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
    && Width == other.Width
    && Height == other.Height;

  public override int GetHashCode() => HashCode.Combine(X, Y, Width, Height);

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