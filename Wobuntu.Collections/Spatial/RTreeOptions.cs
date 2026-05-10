using System;

namespace Wobuntu.Collections.Spatial;

public class RTreeOptions
{
  private const string MaxEntriesPerNodeMustBeAtLeast2 = "A maximum entry capacity of at least 2 is required.";
  private const string PercentageOutOfRange = "The value must be a number between 0 and 1.";
  private const string InitialCapacityMustBeBigger0 = "The initial capacity must be at least 1.";

  public const byte DefaultMaxEntriesPerNode = 12;
  public const byte MinEntriesPerNodeMinimum = 2;
  public const int DefaultInitialQueryStackCapacity = 64;
  public const int DefaultInitialViewportItemsCapacity = 64;

  internal const float MinEntriesRatio = 0.4f;

  public byte MaxEntriesPerNode
  {
    get;
    init
    {
      if (value < MinEntriesPerNodeMinimum)
      {
        throw new ArgumentOutOfRangeException(nameof(value), value, MaxEntriesPerNodeMustBeAtLeast2);
      }

      field = value;
    }
  } = DefaultMaxEntriesPerNode;

  public byte MinEntriesPerNode => DeriveMinEntriesFromMaxEntriesPerNode(MaxEntriesPerNode);

  /// <summary>
  ///   Gets or sets the threshold used on shrinking the <see cref="RTree{T}.Viewport"/>, which determines
  ///   if the <see cref="RTree{T}.ViewportItems"/> shall be updated or if the cached values can be kept.<br />
  ///   The value only applies, if the new viewport is fully contained in the old view.<br />
  ///   Use <c>0</c> to always update the <see cref="RTree{T}.ViewportItems"/> on shrinking.<br />
  ///   Use <c>1</c> to never update the <see cref="RTree{T}.ViewportItems"/> on shrinking.
  /// </summary>
  public double UpdateViewportItemsOnShrinkThreshold
  {
    get;
    init
    {
      if (value is < 0 or > 1)
      {
        throw new ArgumentOutOfRangeException(nameof(value), value, PercentageOutOfRange);
      }

      field = value;
    }
  } = .3;

  public int InitialQueryStackCapacity
  {
    get;
    init
    {
      if (value < 1)
      {
        throw new ArgumentOutOfRangeException(nameof(value), value, InitialCapacityMustBeBigger0);
      }

      field = value;
    }
  } = DefaultInitialQueryStackCapacity;

  public int InitialViewportItemsCapacity
  {
    get;
    init
    {
      if (value < 1)
      {
        throw new ArgumentOutOfRangeException(nameof(value), value, InitialCapacityMustBeBigger0);
      }

      field = value;
    }
  } = DefaultInitialViewportItemsCapacity;

  internal static byte DeriveMinEntriesFromMaxEntriesPerNode(int maxEntries)
  {
    if (maxEntries < MinEntriesPerNodeMinimum)
    {
      maxEntries = MinEntriesPerNodeMinimum;
    }

    var minEntries = Math.Max(MinEntriesPerNodeMinimum, (byte)(maxEntries * MinEntriesRatio));
    return minEntries;
  }
}