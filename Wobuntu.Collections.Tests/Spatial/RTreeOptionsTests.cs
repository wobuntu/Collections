using Wobuntu.Collections.Spatial;

namespace Wobuntu.Collections.Tests.Spatial;

public class RTreeOptionsTests
{
  [Fact]
  public void Init_MaxEntriesPerNodeSmaller2_Throws()
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => new RTreeOptions { MaxEntriesPerNode = 1 });

    // Must not throw:
    _ = new RTreeOptions { MaxEntriesPerNode = 2 };
    _ = new RTreeOptions { MaxEntriesPerNode = byte.MaxValue };
  }

  [Fact]
  public void Init_UpdateViewportItemsOnShrinkThresholdSmaller0OrLarger1_Throws()
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => new RTreeOptions { UpdateViewportItemsOnShrinkThreshold = -.0001 });
    Assert.Throws<ArgumentOutOfRangeException>(() => new RTreeOptions { UpdateViewportItemsOnShrinkThreshold = 1.0001 });

    // Must not throw:
    _ = new RTreeOptions { UpdateViewportItemsOnShrinkThreshold = 0 };
    _ = new RTreeOptions { UpdateViewportItemsOnShrinkThreshold = 1 };
  }

  [Fact]
  public void Init_InitialNodeCapacitySmaller1_Throws()
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => new RTreeOptions { InitialNodeCapacity = 0 });

    // Must not throw:
    _ = new RTreeOptions { InitialNodeCapacity = 1 };
    _ = new RTreeOptions { InitialNodeCapacity = int.MaxValue };
  }

  [Fact]
  public void Init_InitialChildBlockCapacitySmaller1_Throws()
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => new RTreeOptions { InitialChildBlockCapacity = 0 });

    // Must not throw:
    _ = new RTreeOptions { InitialChildBlockCapacity = 1 };
    _ = new RTreeOptions { InitialChildBlockCapacity = int.MaxValue };
  }

  [Fact]
  public void Init_InitialQueryStackCapacitySmaller1_Throws()
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => new RTreeOptions { InitialChildBlockCapacity = 0 });

    // Must not throw:
    _ = new RTreeOptions { InitialChildBlockCapacity = 1 };
    _ = new RTreeOptions { InitialChildBlockCapacity = int.MaxValue };
  }

  [Fact]
  public void Init_InitialViewportItemsCapacitySmaller1_Throws()
  {
    Assert.Throws<ArgumentOutOfRangeException>(() => new RTreeOptions { InitialQueryStackCapacity = 0 });

    // Must not throw:
    _ = new RTreeOptions { InitialQueryStackCapacity = 1 };
    _ = new RTreeOptions { InitialQueryStackCapacity = int.MaxValue };
  }

  [Theory]
  [InlineData(2, 2)]
  [InlineData(8, 3)]
  [InlineData(10, 4)]
  [InlineData(13, 5)]
  [InlineData(15, 6)]
  [InlineData(18, 7)]
  public void Init_MinEntriesPerNode_CorrectlyDerivedFromMaxEntriesPerNode(byte maxEntries, ushort expectedMinEntries)
  {
    var options = new RTreeOptions { MaxEntriesPerNode = maxEntries };
    Assert.Equal(expectedMinEntries, options.MinEntriesPerNode);
  }
}