using System;

namespace Wobuntu.Collections.Spatial;

public class RTreeOptions
{
  private const string PercentageOutOfRange = "The value must be a number between 0 and 1.";

  public const int DefaultMaxEntriesPerNode = 12;
  public const int MinEntriesPerNodeMinimum = 2;

  internal const float MinEntriesRatio = 0.4f;

  public int MaxEntriesPerNode
  {
    get;
    init
    {
      if (value < MinEntriesPerNodeMinimum)
      {
        throw new ArgumentOutOfRangeException(nameof(value), value, "A maximum entry capacity of at least 2 is required.");
      }

      field = value;
    }
  } = DefaultMaxEntriesPerNode;

  public int MinEntriesPerNode => DeriveMinEntriesFromMaxEntriesPerNode(MaxEntriesPerNode);

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

  /// <summary>
  ///   Specifies the number (default: 64) of previously removed leaf nodes, which are kept in memory for reuse.<br />
  ///   Set 0 or a negative number to disable leaf node recycling.
  /// </summary>
  public int RecycledLeafNodeCapacity { get; set; } = 64;

  /// <summary>
  ///   Specifies the number (default: 32) of previously removed leaf nodes, which are kept in memory for reuse.<br />
  ///   Set 0 or a negative number to disable leaf node recycling.
  /// </summary>
  public int RecycledNonLeafNodeCapacity { get; set; } = 32;

  internal static int DeriveMinEntriesFromMaxEntriesPerNode(int maxEntries)
  {
    if (maxEntries < MinEntriesPerNodeMinimum)
    {
      maxEntries = MinEntriesPerNodeMinimum;
    }

    var minEntries = Math.Max(MinEntriesPerNodeMinimum, (int)(maxEntries * MinEntriesRatio));
    return minEntries;
  }
}