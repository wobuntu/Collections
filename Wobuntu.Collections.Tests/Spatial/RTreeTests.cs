using System.Runtime.InteropServices;
using Wobuntu.Collections.Spatial;
using Wobuntu.Collections.Tests.Spatial.Helpers;

namespace Wobuntu.Collections.Tests.Spatial;

public class RTreeTests
{
  [Fact]
  public void Ctor_NullBoundarySelector_Throws()
  {
    Assert.Throws<ArgumentNullException>(() => new RTree<int>(null!));
  }

  [Fact]
  public void Ctor_WithKnownDataMaxNodeSize2_InitialTreeStructureExpected()
  {
    // Arrange
    var items = Enumerable.Range(0, 8).ToArray();
    var options = new RTreeOptions { MaxEntriesPerNode = 2 };

    RTreeBoundary BoundarySelector(int item)
      => item switch
      {
        0 => new RTreeBoundary(20, 80, 10, 10),
        1 => new RTreeBoundary(-30, 80, 10, 10),
        2 => new RTreeBoundary(60, 60, 10, 10),
        3 => new RTreeBoundary(-70, 60, 10, 10),
        4 => new RTreeBoundary(30, 40, 10, 10),
        5 => new RTreeBoundary(-40, 40, 10, 10),
        6 => new RTreeBoundary(90, 30, 10, 10),
        7 => new RTreeBoundary(-100, 30, 10, 10),
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
      };

    // Act
    var tree = new RTree<int>(items, BoundarySelector, options);
    var root = tree.CreateTestView(tree.RootIndex);

    Assert.False(root.IsLeaf);
    Assert.Equal(1, root.ChildCount);

    var wrapper = root.Child(0);
    Assert.False(wrapper.IsLeaf);
    Assert.Equal(2, wrapper.ChildCount);
    Assert.Equal(new RTreeBoundary(-100, 30, 200, 60), wrapper.Boundary);

    // Left side
    var left = wrapper.Child(0);
    Assert.False(left.IsLeaf);
    Assert.Equal(2, left.ChildCount);
    Assert.Equal(new RTreeBoundary(-100, 30, 80, 60), left.Boundary);

    var leftUpper = left.Child(0);
    Assert.False(leftUpper.IsLeaf);
    Assert.Equal(2, leftUpper.ChildCount);
    Assert.Equal(new RTreeBoundary(-100, 30, 70, 40), leftUpper.Boundary);

    var leftUpperInner = leftUpper.Child(0);
    Assert.False(leftUpperInner.IsLeaf);
    Assert.Equal(2, leftUpperInner.ChildCount);
    Assert.Equal(new RTreeBoundary(-100, 30, 70, 20), leftUpperInner.Boundary);
    Assert.True(leftUpperInner.Child(0).IsLeaf); // 7
    Assert.True(leftUpperInner.Child(1).IsLeaf); // 5

    var leftUpperLower = leftUpper.Child(1);
    Assert.False(leftUpperLower.IsLeaf);
    Assert.Equal(1, leftUpperLower.ChildCount);
    Assert.True(leftUpperLower.Child(0).IsLeaf); // 3

    var leftLower = left.Child(1);
    Assert.False(leftLower.IsLeaf);
    Assert.Equal(1, leftLower.ChildCount);

    var leftLowerInner = leftLower.Child(0);
    Assert.False(leftLowerInner.IsLeaf);
    Assert.Equal(1, leftLowerInner.ChildCount);
    Assert.True(leftLowerInner.Child(0).IsLeaf); // 1

    // Right side
    var right = wrapper.Child(1);
    Assert.False(right.IsLeaf);
    Assert.Equal(1, right.ChildCount);
    Assert.Equal(new RTreeBoundary(20, 30, 80, 60), right.Boundary);

    var rightInner = right.Child(0);
    Assert.False(rightInner.IsLeaf);
    Assert.Equal(2, rightInner.ChildCount);

    var rightUpper = rightInner.Child(0);
    Assert.False(rightUpper.IsLeaf);
    Assert.Equal(2, rightUpper.ChildCount);
    Assert.True(rightUpper.Child(0).IsLeaf); // 6
    Assert.True(rightUpper.Child(1).IsLeaf); // 2

    var rightLower = rightInner.Child(1);
    Assert.False(rightLower.IsLeaf);
    Assert.Equal(2, rightLower.ChildCount);
    Assert.True(rightLower.Child(0).IsLeaf); // 4
    Assert.True(rightLower.Child(1).IsLeaf); // 0
  }

  [Fact]
  public void Ctor_WithKnownDataMaxNodeSize3_InitialTreeStructureExpected()
  {
    // Arrange
    var items = Enumerable.Range(0, 8).ToArray();
    var options = new RTreeOptions { MaxEntriesPerNode = 3 };

    RTreeBoundary BoundarySelector(int item)
      => item switch
      {
        0 => new RTreeBoundary(20, 80, 10, 10),
        1 => new RTreeBoundary(-30, 80, 10, 10),
        2 => new RTreeBoundary(60, 60, 10, 10),
        3 => new RTreeBoundary(-70, 60, 10, 10),
        4 => new RTreeBoundary(30, 40, 10, 10),
        5 => new RTreeBoundary(-40, 40, 10, 10),
        6 => new RTreeBoundary(90, 30, 10, 10),
        7 => new RTreeBoundary(-100, 30, 10, 10),
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
      };

    // Act
    var tree = new RTree<int>(items, BoundarySelector, options);
    var root = tree.CreateTestView(tree.RootIndex);

    Assert.False(root.IsLeaf);
    Assert.Equal(2, root.ChildCount);

    // Left side: contains 7,5,3 (upper-left) and 1,0 (lower-left)
    var left = root.Child(0);
    Assert.False(left.IsLeaf);
    Assert.Equal(2, left.ChildCount);
    Assert.Equal(new RTreeBoundary(-100, 30, 130, 60), left.Boundary);

    var leftUpper = left.Child(0);
    Assert.False(leftUpper.IsLeaf);
    Assert.Equal(3, leftUpper.ChildCount);
    Assert.Equal(new RTreeBoundary(-100, 30, 70, 40), leftUpper.Boundary);
    Assert.True(leftUpper.Child(0).IsLeaf);
    Assert.True(leftUpper.Child(1).IsLeaf);
    Assert.True(leftUpper.Child(2).IsLeaf);

    var leftLower = left.Child(1);
    Assert.False(leftLower.IsLeaf);
    Assert.Equal(2, leftLower.ChildCount);
    Assert.Equal(new RTreeBoundary(-30, 80, 60, 10), leftLower.Boundary);
    Assert.True(leftLower.Child(0).IsLeaf);
    Assert.True(leftLower.Child(1).IsLeaf);

    // Right side: single group containing 6,4,2
    var right = root.Child(1);
    Assert.False(right.IsLeaf);
    Assert.Equal(1, right.ChildCount);
    Assert.Equal(new RTreeBoundary(30, 30, 70, 40), right.Boundary);

    var rightGroup = right.Child(0);
    Assert.False(rightGroup.IsLeaf);
    Assert.Equal(3, rightGroup.ChildCount);
    Assert.Equal(new RTreeBoundary(30, 30, 70, 40), rightGroup.Boundary);
    Assert.True(rightGroup.Child(0).IsLeaf);
    Assert.True(rightGroup.Child(1).IsLeaf);
    Assert.True(rightGroup.Child(2).IsLeaf);
  }

  [Fact]
  public void Ctor_WithKnownDataMaxNodeSize4_InitialTreeStructureExpected()
  {
    // Arrange
    var items = Enumerable.Range(0, 8).ToArray();
    var options = new RTreeOptions { MaxEntriesPerNode = 4 };

    RTreeBoundary BoundarySelector(int item)
      => item switch
      {
        0 => new RTreeBoundary(20, 80, 10, 10),
        1 => new RTreeBoundary(-30, 80, 10, 10),
        2 => new RTreeBoundary(60, 60, 10, 10),
        3 => new RTreeBoundary(-70, 60, 10, 10),
        4 => new RTreeBoundary(30, 40, 10, 10),
        5 => new RTreeBoundary(-40, 40, 10, 10),
        6 => new RTreeBoundary(90, 30, 10, 10),
        7 => new RTreeBoundary(-100, 30, 10, 10),
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
      };

    // Act
    var tree = new RTree<int>(items, BoundarySelector, options);
    var root = tree.CreateTestView(tree.RootIndex);

    Assert.False(root.IsLeaf);
    Assert.Equal(3, root.ChildCount);

    var left = root.Child(0);
    Assert.False(left.IsLeaf);
    Assert.Equal(4, left.ChildCount);
    Assert.Equal(new RTreeBoundary(-100, 30, 80, 60), left.Boundary);
    Assert.True(left.Child(0).IsLeaf);
    Assert.True(left.Child(1).IsLeaf);
    Assert.True(left.Child(2).IsLeaf);
    Assert.True(left.Child(3).IsLeaf);

    var middle = root.Child(1);
    Assert.False(middle.IsLeaf);
    Assert.Equal(1, middle.ChildCount);
    Assert.Equal(new RTreeBoundary(20, 80, 10, 10), middle.Boundary);
    Assert.True(middle.Child(0).IsLeaf);

    var right = root.Child(2);
    Assert.False(right.IsLeaf);
    Assert.Equal(3, right.ChildCount);
    Assert.Equal(new RTreeBoundary(30, 30, 70, 40), right.Boundary);
    Assert.True(right.Child(0).IsLeaf);
    Assert.True(right.Child(1).IsLeaf);
    Assert.True(right.Child(2).IsLeaf);
  }

  [Fact]
  public void Count_OnAddRemoveAndClear_UpdatesCorrectly()
  {
    // Arrange
    // ReSharper disable once UseObjectOrCollectionInitializer
    var tree = new RTree<int>(_ => default);

    // Act
    tree.Add(1);
    var count1 = tree.Count;

    tree.AddRange([2, 3, 4, 5]);
    var count5 = tree.Count;

    tree.Remove(3);
    var count4 = tree.Count;

    tree.RemoveRange([1, 2, 3]);
    var count2 = tree.Count;

    tree.Clear();
    var count0 = tree.Count;

    // Assert
    Assert.Equal(1, count1);
    Assert.Equal(5, count5);
    Assert.Equal(4, count4);
    Assert.Equal(2, count2);
    Assert.Equal(0, count0);
  }

  [Fact]
  public void Boundary_OnAddRemoveAndClear_UpdatesCorrectly()
  {
    // Arrange
    var items = Enumerable.Range(0, 5).ToArray();
    var tree = new RTree<int>(items, item => item switch
    {
      // Approximate layout of data below, expecting separation here at x=0 drawn as a line:
      // 7.........|.........6
      // ......5...|...4......
      // ..........|..........
      // ...3......|......2...
      // ..........|..........
      // .......1..|..0.......
      0 => new RTreeBoundary(20, 80, 10, 10),   // --> 20,80,10,10
      1 => new RTreeBoundary(-30, 80, 10, 10),  // --> -30,80,60,10
      2 => new RTreeBoundary(60, 60, 10, 10),   // --> -30,60,100,30
      3 => new RTreeBoundary(-70, 60, 10, 10),  // --> -70,60,140,30
      4 => new RTreeBoundary(30, 40, 10, 10),   // --> -70,40,140,50
      5 => new RTreeBoundary(-40, 40, 10, 10),  // --> -70,40,140,50
      6 => new RTreeBoundary(90, 30, 10, 10),   // --> -70,30,170,60
      7 => new RTreeBoundary(-100, 30, 10, 10), // --> -100,30,200,60
      _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
    });

    // Act
    var boundary0To4 = tree.Boundary;

    tree.AddRange([5, 6]);
    var boundary0To6 = tree.Boundary;

    tree.Add(7);
    var boundary0To7 = tree.Boundary;

    tree.Remove(6);
    var boundary0To7No6 = tree.Boundary;

    tree.RemoveRange([4, 5, 7]);
    var boundary0To3 = tree.Boundary;

    tree.Clear();
    var boundaryEmpty = tree.Boundary;

    // Assert
    Assert.Equal(new RTreeBoundary(-70, 40, 140, 50), boundary0To4);
    Assert.Equal(new RTreeBoundary(-70, 30, 170, 60), boundary0To6);
    Assert.Equal(new RTreeBoundary(-100, 30, 200, 60), boundary0To7);
    Assert.Equal(new RTreeBoundary(-100, 30, 170, 60), boundary0To7No6);
    Assert.Equal(new RTreeBoundary(-70, 60, 140, 30), boundary0To3);
    Assert.Equal(default, boundaryEmpty);
  }

  [Theory]
  [InlineData(16)]
  [InlineData(2)]
  public void Query_ForBoundaryWithIntersectingData_ReturnsExpectedNodes(ushort nodeCapacity)
  {
    // Arrange
    var items = Enumerable.Range(0, 8).ToArray();
    var tree = new RTree<int>(items, item => item switch
    {
      // Approximate layout of data below, expecting separation here at x=0 drawn as a line:
      // 7.........|.........6
      // ......5...|...4......
      // ..........|..........
      // ...3......|......2...
      // ..........|..........
      // .......1..|..0.......
      0 => new RTreeBoundary(20, 80, 10, 10),
      1 => new RTreeBoundary(-30, 80, 10, 10),
      2 => new RTreeBoundary(60, 60, 10, 10),
      3 => new RTreeBoundary(-70, 60, 10, 10),
      4 => new RTreeBoundary(30, 40, 10, 10),
      5 => new RTreeBoundary(-40, 40, 10, 10),
      6 => new RTreeBoundary(90, 30, 10, 10),
      7 => new RTreeBoundary(-100, 30, 10, 10),
      _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
    }, new RTreeOptions
    {
      MaxEntriesPerNode = nodeCapacity
    });

    // Act
    var target = new List<int>();

    tree.QueryTo(new RTreeBoundary(25, 65, 40, 20), target);
    var query0And2 = target.Order().ToArray();
    target.Clear();

    tree.QueryTo(new RTreeBoundary(-95, 35, 60, 10), target);
    var query5And7 = target.Order().ToArray();
    target.Clear();

    // Assert
    Assert.Equal(2, query0And2.Length);
    Assert.Equal(0, query0And2[0]);
    Assert.Equal(2, query0And2[1]);
    Assert.Equal(2, query5And7.Length);
    Assert.Equal(5, query5And7[0]);
    Assert.Equal(7, query5And7[1]);
  }

  [Theory]
  [InlineData(16)]
  [InlineData(2)]
  public void Query_ForBoundaryWithContainingData_ReturnsExpectedNodes(ushort nodeCapacity)
  {
    // Arrange
    var items = Enumerable.Range(0, 8).ToArray();
    var tree = new RTree<int>(items, item => item switch
    {
      // Approximate layout of data below, expecting separation here at x=0 drawn as a line:
      // 7.........|.........6
      // ......5...|...4......
      // ..........|..........
      // ...3......|......2...
      // ..........|..........
      // .......1..|..0.......
      0 => new RTreeBoundary(20, 80, 10, 10),
      1 => new RTreeBoundary(-30, 80, 10, 10),
      2 => new RTreeBoundary(60, 60, 10, 10),
      3 => new RTreeBoundary(-70, 60, 10, 10),
      4 => new RTreeBoundary(30, 40, 10, 10),
      5 => new RTreeBoundary(-40, 40, 10, 10),
      6 => new RTreeBoundary(90, 30, 10, 10),
      7 => new RTreeBoundary(-100, 30, 10, 10),
      _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
    }, new RTreeOptions
    {
      MaxEntriesPerNode = nodeCapacity
    });

    // Act
    var target = new List<int>();
    tree.QueryTo(new RTreeBoundary(-75, 35, 150, 40), target);
    var query2To5 = target.Order().ToArray();

    // Assert
    Assert.Equal(4, query2To5.Length);
    Assert.Equal(2, query2To5[0]);
    Assert.Equal(3, query2To5[1]);
    Assert.Equal(4, query2To5[2]);
    Assert.Equal(5, query2To5[3]);
  }

  [Theory]
  [InlineData(16)]
  [InlineData(2)]
  public void Query_WithEmptyBoundaries_EmptyAreNotIncludedInResultSet(ushort nodeCapacity)
  {
    // Arrange
    var items = Enumerable.Range(0, 8).ToArray();
    var tree = new RTree<int>(items, item => item switch
    {
      // Approximate layout of data below, expecting separation here at x=0 drawn as a line:
      // 7.........|.........6
      // ......5...|...4......
      // ..........|..........
      // ...3......|......2...
      // ..........|..........
      // .......1..|..0.......
      0 => new RTreeBoundary(20, 80, 10, 10),
      1 => new RTreeBoundary(-30, 80, 10, 10),
      2 => new RTreeBoundary(60, 60, 10, 10),
      3 => new RTreeBoundary(-70, 60, 10, 10),
      4 => new RTreeBoundary(30, 40, 10, 10),
      5 => new RTreeBoundary(-40, 40, 10, 10),
      6 => new RTreeBoundary(90, 30, 10, 10),
      7 => new RTreeBoundary(-100, 30, 10, 10),
      8 => new RTreeBoundary(),
      9 => new RTreeBoundary(),
      _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
    }, new RTreeOptions
    {
      MaxEntriesPerNode = nodeCapacity
    });

    // Act
    var target = new List<int>();
    tree.QueryTo(new RTreeBoundary(-500, -500, 1000, 1000), target);
    var queryWithout8Or9 = target.Order().ToArray();

    // Assert
    Assert.Equal(8, queryWithout8Or9.Length);
    Assert.Equal(0, queryWithout8Or9[0]);
    Assert.Equal(1, queryWithout8Or9[1]);
    Assert.Equal(2, queryWithout8Or9[2]);
    Assert.Equal(3, queryWithout8Or9[3]);
    Assert.Equal(4, queryWithout8Or9[4]);
    Assert.Equal(5, queryWithout8Or9[5]);
    Assert.Equal(6, queryWithout8Or9[6]);
    Assert.Equal(7, queryWithout8Or9[7]);
  }

  [Fact]
  public void Contains_WithBothAvailableAndUnavailableData_ReturnsExpected()
  {
    // Arrange
    var tree = new RTree<int>([0, 2, 4], _ => new RTreeBoundary());

    // Act
    var allExpectedContained = tree.Contains(0) && tree.Contains(2) && tree.Contains(4);
    var allExpectedNotContained = tree.Contains(1) || tree.Contains(3);

    tree.Add(1);
    allExpectedContained = tree.Contains(1) && allExpectedContained;

    tree.Remove(2);
    allExpectedNotContained = tree.Contains(2) || allExpectedNotContained;

    // Assert
    Assert.True(allExpectedContained);
    Assert.False(allExpectedNotContained);
  }

  [Fact]
  public void GetEnumerator_HappyPath_AllItemsAreEnumerated()
  {
    // Arrange
    var data = Enumerable.Range(0, 20).ToList();
    var tree = new RTree<int>(CollectionsMarshal.AsSpan(data), _ => new RTreeBoundary());

    // Act
    var sumDataAll = data.Sum();
    var sumTreeAll = tree.Sum();

    data.Remove(5);
    tree.Remove(5);

    var sumDataWithout5 = data.Sum();
    var sumTreeWithout5 = tree.Sum();

    data.Add(100);
    tree.Add(100);

    var sumDataWithout5With100 = data.Sum();
    var sumTreeWithout5With100 = tree.Sum();

    // Assert
    Assert.Equal(sumDataAll, sumTreeAll);
    Assert.Equal(sumDataWithout5, sumTreeWithout5);
    Assert.Equal(sumDataWithout5With100, sumTreeWithout5With100);
  }

  [Fact]
  public void Add_EmptyViewport_ViewportItemsEmpty()
  {
    // Arrange
    // ReSharper disable once UseObjectOrCollectionInitializer
    var tree = new RTree<int>(_ => default);

    // Act
    tree.Add(1);
    tree.Add(2);
    tree.Add(3);
    tree.Add(4);

    // Assert
    Assert.Empty(tree.ViewportItems);
    Assert.Equal(4, tree.Count);
  }

  [Fact]
  public void Add_ExceedingNodeSize_CreatesNewLayer()
  {
    // Arrange
    var options = new RTreeOptions { MaxEntriesPerNode = 2 };

    RTreeBoundary BoundarySelector(int item)
      => item switch
      {
        0 => new RTreeBoundary(100, 200, 10, 20),
        1 => new RTreeBoundary(100, 400, 10, 20),
        2 => new RTreeBoundary(100, 240, 10, 20),
        3 => new RTreeBoundary(100, 440, 10, 20),
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
      };

    // Act
    // ReSharper disable once UseObjectOrCollectionInitializer
    var tree = new RTree<int>(BoundarySelector, options);

    // Act / Assert: After 2 items, root has 2 leaf children
    tree.Add(0);
    tree.Add(1);
    Assert.Equal(new RTreeBoundary(100, 200, 10, 220), tree.Boundary);

    var root = tree.CreateTestView(tree.RootIndex);
    Assert.Equal(2, root.ChildCount);
    Assert.True(root.Child(0).IsLeaf);
    Assert.True(root.Child(1).IsLeaf);

    // After 3 items, a new layer is inserted. Root has 2 children:
    // one non-leaf wrapping 0+1, and item 2 as a leaf.
    tree.Add(2);

    root = tree.CreateTestView(tree.RootIndex);
    Assert.Equal(new RTreeBoundary(100, 200, 10, 220), tree.Boundary);
    Assert.Equal(2, root.ChildCount);

    var firstChild = root.Child(0);
    var secondChild = root.Child(1);
    // One child is non-leaf (wrapping the original 2 items), other is a leaf
    Assert.True(firstChild.IsLeaf != secondChild.IsLeaf);

    var nonLeafChild = firstChild.IsLeaf ? secondChild : firstChild;
    Assert.Equal(2, nonLeafChild.ChildCount);
    Assert.True(nonLeafChild.Child(0).IsLeaf);
    Assert.True(nonLeafChild.Child(1).IsLeaf);

    // After 4 items, another layer is inserted for item 3.
    tree.Add(3);
    Assert.Equal(4, tree.Count);

    root = tree.CreateTestView(tree.RootIndex);
    Assert.Equal(2, root.ChildCount);
  }

  [Fact]
  public void Add_ExceedingNodeSize_AllItemsStillQueryable()
  {
    // Arrange
    var options = new RTreeOptions { MaxEntriesPerNode = 2 };
    var tree = new RTree<int>(value => new RTreeBoundary(value * 10, 10, 10, 10), options) { 1, 5 };

    // Act: Adding items triggers parent layer insertions
    tree.Add(2);
    tree.Add(6);

    // Assert: All 4 items are present and queryable
    Assert.Equal(4, tree.Count);
    Assert.Contains(1, tree);
    Assert.Contains(5, tree);
    Assert.Contains(2, tree);
    Assert.Contains(6, tree);

    var results = new List<int>();
    tree.QueryTo(new RTreeBoundary(0, 0, 100, 30), results);
    Assert.Equal(4, results.Count);
  }

  [Fact]
  public void Remove_CausingUnderfullNode_SiblingsMovedOneLevelUpIfParentHasSpace()
  {
    // Arrange
    var options = new RTreeOptions { MaxEntriesPerNode = 3 };
    var tree = new RTree<int>(value => new RTreeBoundary(value * 10, 10, 10, 10), options) { 1, 5, 2, 6, 0, 3, 4, 7 };

    Assert.Equal(8, tree.Count);

    // Verify all items are queryable before removals
    var allItems = new List<int>();
    tree.QueryTo(new RTreeBoundary(-10, 0, 200, 30), allItems);
    Assert.Equal(8, allItems.Count);

    // Remove items and verify tree integrity at each step
    tree.Remove(0);
    Assert.Equal(7, tree.Count);
    Assert.DoesNotContain(0, tree);

    allItems.Clear();
    tree.QueryTo(new RTreeBoundary(-10, 0, 200, 30), allItems);
    Assert.Equal(7, allItems.Count);
    Assert.DoesNotContain(0, allItems);

    // Remove more items — this triggers underfull node handling
    tree.Remove(1);
    Assert.Equal(6, tree.Count);
    tree.Remove(5);
    Assert.Equal(5, tree.Count);

    // Verify remaining items are still correctly queryable
    allItems.Clear();
    tree.QueryTo(new RTreeBoundary(-10, 0, 200, 30), allItems);
    var sorted = allItems.Order().ToArray();
    Assert.Equal([2, 3, 4, 6, 7], sorted);
  }

  [Fact]
  public void Remove_Until1Item_CausesRootToBecomeLeaf()
  {
    // Arrange
    var tree = new RTree<int>(value => new RTreeBoundary(value * 10, 10, 10, 10)) { 1, 2 };

    // Act
    tree.Remove(1);

    // Assert
    var root = tree.CreateTestView(tree.RootIndex);
    Assert.True(root.IsLeaf);
    Assert.Equal(2, root.Data);
  }

  [Fact]
  public void Remove_UntilNothingLeft_CausesEmptyRootNode()
  {
    var tree = new RTree<int>(value => new RTreeBoundary(value * 10, 10, 10, 10)) { 1, 2 };

    // Act
    tree.Remove(1);
    tree.Remove(2);

    // Assert
    var root = tree.CreateTestView(tree.RootIndex);
    Assert.False(root.IsLeaf);
  }

  [Fact]
  public void RemoveThenAdd_OfSameItem_Works()
  {
    // Arrange
    var tree = new RTree<int>(value => new RTreeBoundary(value * 10, 10, 10, 10)) { 1, 2 };

    // Act / Assert
    tree.Remove(1);
    Assert.DoesNotContain(1, tree);
    Assert.Single(tree);

    tree.Add(1);
    Assert.Contains(1, tree);
    Assert.Equal(2, tree.Count);
  }

  [Fact]
  public void ChooseInsertParent_NoMatchingChildBoundariesRootCapacityAvailable_ReturnsRootNode()
  {
    // Arrange
    var options = new RTreeOptions { MaxEntriesPerNode = 3 };
    var tree = new RTree<int>(BoundarySelector, options) { 10, 50 };

    // Act
    var result1 = tree.ChooseInsertParent(BoundarySelector(90));
    var result2 = tree.ChooseInsertParent(new RTreeBoundary());
    var result3 = tree.ChooseInsertParent(BoundarySelector(-10));

    // Assert
    Assert.Equal(tree.RootIndex, result1);
    Assert.Equal(tree.RootIndex, result2);
    Assert.Equal(tree.RootIndex, result3);
    return;

    static RTreeBoundary BoundarySelector(int value) => new(value, value, 10, 10);
  }

  [Fact]
  public void ChooseInsertParent_NoMatchingChildBoundariesRootNoMoreCapacity_ReturnsClosestChild()
  {
    // Arrange
    var options = new RTreeOptions { MaxEntriesPerNode = 2 };
    var tree = new RTree<int>(BoundarySelector, options) { 10, 50 };

    // Act
    var result1 = tree.ChooseInsertParent(BoundarySelector(90));
    var result2 = tree.ChooseInsertParent(new RTreeBoundary());
    var result3 = tree.ChooseInsertParent(BoundarySelector(-10));

    // Assert
    var root = tree.CreateTestView(tree.RootIndex);
    Assert.Equal(root.Child(1).Index, result1);
    Assert.Equal(root.Child(0).Index, result2);
    Assert.Equal(root.Child(0).Index, result3);
    return;

    static RTreeBoundary BoundarySelector(int value) => new(value, value, 10, 10);
  }

  [Fact]
  public void Viewport_SetToAreaContainingItems_ViewportItemsPopulated()
  {
    // Arrange
    var tree = new RTree<int>(
      [1, 2, 3, 4, 5],
      x => new RTreeBoundary(x * 20, 0, 10, 10))
    {
      // Act
      Viewport = new RTreeBoundary(0, -5, 70, 20) // covers items 1,2,3
    };

    // Assert
    var viewportItems = tree.ViewportItems.Order().ToArray();
    Assert.Equal([1, 2, 3], viewportItems);
  }

  [Fact]
  public void Viewport_SetToEmpty_ClearsViewportItems()
  {
    // Arrange
    var tree = new RTree<int>(
      [1, 2, 3],
      x => new RTreeBoundary(x * 20, 0, 10, 10))
    {
      Viewport = new RTreeBoundary(0, -5, 100, 20)
    };

    Assert.NotEmpty(tree.ViewportItems);

    // Act
    tree.Viewport = default;

    // Assert
    Assert.Empty(tree.ViewportItems);
  }

  [Fact]
  public void Viewport_AddItemInsideViewport_ViewportItemsUpdated()
  {
    // Arrange
    var tree = new RTree<int>(x => new RTreeBoundary(x * 20, 0, 10, 10))
    {
      1,
      2
    };

    tree.Viewport = new RTreeBoundary(0, -5, 100, 20);

    // Act
    tree.Add(3); // inside viewport

    // Assert
    Assert.Contains(3, tree.ViewportItems);
  }

  [Fact]
  public void Viewport_AddItemOutsideViewport_ViewportItemsNotUpdated()
  {
    // Arrange
    var tree = new RTree<int>(x => new RTreeBoundary(x * 20, 0, 10, 10)) { 1 };
    tree.Viewport = new RTreeBoundary(0, -5, 50, 20);

    // Act
    tree.Add(10); // x=200, outside viewport

    // Assert
    Assert.DoesNotContain(10, tree.ViewportItems);
  }

  [Fact]
  public void Viewport_RemoveItemInsideViewport_ViewportItemsUpdated()
  {
    // Arrange
    var tree = new RTree<int>(x => new RTreeBoundary(x * 20, 0, 10, 10))
    {
      1,
      2,
      3
    };

    tree.Viewport = new RTreeBoundary(0, -5, 100, 20);
    Assert.Contains(2, tree.ViewportItems);

    // Act
    tree.Remove(2);

    // Assert
    Assert.DoesNotContain(2, tree.ViewportItems);
  }

  [Fact]
  public void Viewport_ShrinkWithinThreshold_KeepsCachedItems()
  {
    // Arrange: threshold is 0.3 by default, so shrinking by less than 30% keeps the cached viewport items
    var tree = new RTree<int>(
      Enumerable.Range(0, 20).ToArray(),
      x => new RTreeBoundary(x * 10, 0, 5, 5))
    {
      Viewport = new RTreeBoundary(0, -5, 200, 15) // covers all 20 items
    };

    var countBefore = tree.ViewportItems.Count;

    // Act: shrink viewport by ~10% (well within 30% threshold) — cached items should remain
    tree.Viewport = new RTreeBoundary(10, -5, 180, 15);

    // Assert: ViewportItems still contains items from the larger cached viewport
    Assert.Equal(countBefore, tree.ViewportItems.Count);
  }

  [Fact]
  public void Viewport_MoveOutside_UpdatesViewportItems()
  {
    // Arrange
    var tree = new RTree<int>(
      [1, 2, 3, 4, 5],
      x => new RTreeBoundary(x * 100, 0, 10, 10))
    {
      Viewport = new RTreeBoundary(90, -5, 30, 20) // covers item 1
    };

    Assert.Contains(1, tree.ViewportItems);
    Assert.DoesNotContain(5, tree.ViewportItems);

    // Act: move viewport to item 5's area
    tree.Viewport = new RTreeBoundary(490, -5, 30, 20);

    // Assert
    Assert.Contains(5, tree.ViewportItems);
    Assert.DoesNotContain(1, tree.ViewportItems);
  }

  [Fact]
  public void Viewport_ViewportItemsCountReflectsVisibleNotTotal()
  {
    // Arrange
    var tree = new RTree<int>(
      Enumerable.Range(0, 100).ToArray(),
      x => new RTreeBoundary(x * 10, 0, 5, 5))
    {
      // Act: boundary from x=0 to x=45 (width=45), items at x=0,10,20,30,40 have width 5
      // so items 0..4 are fully within [0,45], item 5 starts at x=50 which is outside
      Viewport = new RTreeBoundary(0, -5, 45, 15)
    };

    // Assert: ViewportItems count should be much less than total count
    Assert.Equal(100, tree.Count);
    Assert.Equal(5, tree.ViewportItems.Count);
  }

  [Fact]
  public void AddRange_OnNonEmptyTree_AddsAllItems()
  {
    // Arrange
    var tree = new RTree<int>(x => new RTreeBoundary(x, 0, 1, 1))
    {
      1,
      2
    };

    // Act
    var added = tree.AddRange([3, 4, 5]);

    // Assert
    Assert.Equal(3, added);
    Assert.Equal(5, tree.Count);
    Assert.Contains(3, tree);
    Assert.Contains(5, tree);
  }

  [Fact]
  public void AddRange_WithDuplicates_SkipsDuplicates()
  {
    // Arrange
    var tree = new RTree<int>(x => new RTreeBoundary(x, 0, 1, 1)) { 1 };

    // Act
    var added = tree.AddRange([1, 2, 2, 3]);

    // Assert
    Assert.Equal(2, added); // only 2 and 3 are new
    Assert.Equal(3, tree.Count);
  }

  [Fact]
  public void Clear_AfterAddingItems_ResetsTreeCompletely()
  {
    // Arrange
    var tree = new RTree<int>(x => new RTreeBoundary(x, 0, 1, 1));
    for (var index = 0; index < 50; index++)
    {
      tree.Add(index);
    }

    tree.Viewport = new RTreeBoundary(0, -1, 100, 3);
    Assert.NotEmpty(tree.ViewportItems);

    // Act
    tree.Clear();

    // Assert
    Assert.Equal(default, tree.Boundary);
    Assert.Empty(tree.ViewportItems);
    Assert.Empty(tree);
  }

  [Fact]
  public void QueryTo_OnEmptyTree_ReturnsZero()
  {
    // Arrange
    var tree = new RTree<int>(_ => default);
    var results = new List<int>();

    // Act
    var count = tree.QueryTo(new RTreeBoundary(0, 0, 100, 100), results);

    // Assert
    Assert.Equal(0, count);
    Assert.Empty(results);
  }

  [Fact]
  public void QueryTo_WithNonIntersectingBoundary_ReturnsZero()
  {
    // Arrange
    var tree = new RTree<int>(
      [1, 2, 3],
      x => new RTreeBoundary(x * 10, 0, 5, 5));
    var results = new List<int>();

    // Act: query area far away from any items
    var count = tree.QueryTo(new RTreeBoundary(500, 500, 10, 10), results);

    // Assert
    Assert.Equal(0, count);
    Assert.Empty(results);
  }

  [Fact]
  public void AddRange_BulkVsManualAdd_SameQueryResults()
  {
    // Arrange
    static RTreeBoundary Selector(int x) => new(x * 10, x * 5, 8, 8);
    var items = Enumerable.Range(0, 50).ToArray();

    var bulkTree = new RTree<int>(items, Selector);
    var manualTree = new RTree<int>(Selector);
    foreach (var item in items)
    {
      manualTree.Add(item);
    }

    // Act: query a large boundary that covers everything
    var bulkResults = new List<int>();
    var manualResults = new List<int>();
    var queryBoundary = new RTreeBoundary(-10, -10, 600, 300);
    bulkTree.QueryTo(queryBoundary, bulkResults);
    manualTree.QueryTo(queryBoundary, manualResults);

    // Assert: same items queryable from both trees
    Assert.Equal(bulkResults.Order(), manualResults.Order());
    Assert.Equal(50, bulkResults.Count);
  }

  [Theory]
  [InlineData(2)]
  [InlineData(3)]
  [InlineData(12)]
  public void Query_AllItems_ReturnsAllRegardlessOfNodeSize(ushort nodeCapacity)
  {
    // Arrange
    var items = Enumerable.Range(0, 30).ToArray();
    var tree = new RTree<int>(
      items,
      x => new RTreeBoundary(x * 10, x * 5, 8, 8),
      new RTreeOptions { MaxEntriesPerNode = nodeCapacity });

    // Act
    var results = new List<int>();
    tree.QueryTo(new RTreeBoundary(-10, -10, 500, 300), results);

    // Assert
    Assert.Equal(items.Order(), results.Order());
  }

  [Fact]
  public void RemoveRange_MultipleItems_AllRemoved()
  {
    // Arrange
    var tree = new RTree<int>(
      Enumerable.Range(0, 10).ToArray(),
      x => new RTreeBoundary(x * 10, 0, 5, 5));

    // Act
    tree.RemoveRange([2, 4, 6, 8]);

    // Assert
    Assert.Equal(6, tree.Count);
    Assert.DoesNotContain(2, tree);
    Assert.DoesNotContain(4, tree);
    Assert.DoesNotContain(6, tree);
    Assert.DoesNotContain(8, tree);
    Assert.Contains(0, tree);
    Assert.Contains(1, tree);
  }

  [Fact]
  public void Add_Null_ReturnsFalse()
  {
    // Arrange
    var tree = new RTree<string>(_ => default);

    // Act / Assert
    Assert.False(tree.Add(null!));
    Assert.Empty(tree);
  }

  [Fact]
  public void Remove_NonExistingItem_ReturnsFalse()
  {
    // Arrange
    var tree = new RTree<int>(x => new RTreeBoundary(x, 0, 1, 1)) { 1 };

    // Act / Assert
    Assert.False(tree.Remove(99));
    Assert.Single(tree);
  }

  [Theory]
  [InlineData(2)]
  [InlineData(3)]
  [InlineData(12)]
  public void Add_WithMinimalCapacity_ForcesResizeAndAllItemsPresent(ushort maxEntries)
  {
    // Arrange: minimal initial capacities force array resizes on nearly every allocation
    var options = new RTreeOptions
    {
      MaxEntriesPerNode = maxEntries,
      InitialNodeCapacity = 2,
      InitialChildBlockCapacity = 1,
    };
    var tree = new RTree<int>(x => new RTreeBoundary(x * 10, x * 5, 8, 8), options);

    // Act
    for (var index = 0; index < 200; index++)
    {
      tree.Add(index);
    }

    // Assert
    Assert.Equal(200, tree.Count);
    for (var index = 0; index < 200; index++)
    {
      Assert.Contains(index, tree);
    }

    var results = new List<int>();
    tree.QueryTo(new RTreeBoundary(-10, -10, 3000, 1500), results);
    Assert.Equal(200, results.Count);
  }

  [Theory]
  [InlineData(2)]
  [InlineData(3)]
  [InlineData(12)]
  public void AddRange_WithMinimalCapacity_ForcesResizeAndAllItemsPresent(ushort maxEntries)
  {
    // Arrange
    var options = new RTreeOptions
    {
      MaxEntriesPerNode = maxEntries,
      InitialNodeCapacity = 2,
      InitialChildBlockCapacity = 1,
    };
    var items = Enumerable.Range(0, 200).ToArray();
    var tree = new RTree<int>(items, x => new RTreeBoundary(x * 10, x * 5, 8, 8), options);

    // Assert
    Assert.Equal(200, tree.Count);

    var results = new List<int>();
    tree.QueryTo(new RTreeBoundary(-10, -10, 3000, 1500), results);
    Assert.Equal(200, results.Count);
  }

  [Theory]
  [InlineData(2)]
  [InlineData(3)]
  [InlineData(12)]
  public void Remove_WithMinimalCapacity_ForcesResizeAndRemainingItemsPresent(ushort maxEntries)
  {
    // Arrange
    var options = new RTreeOptions
    {
      MaxEntriesPerNode = maxEntries,
      InitialNodeCapacity = 2,
      InitialChildBlockCapacity = 1,
    };
    var tree = new RTree<int>(x => new RTreeBoundary(x * 10, x * 5, 8, 8), options);

    for (var index = 0; index < 100; index++)
    {
      tree.Add(index);
    }

    // Act: remove half, forcing underfull handling and re-insertions which trigger resizes
    for (var index = 0; index < 100; index += 2)
    {
      tree.Remove(index);
    }

    // Assert
    Assert.Equal(50, tree.Count);
    for (var index = 1; index < 100; index += 2)
    {
      Assert.Contains(index, tree);
    }

    var results = new List<int>();
    tree.QueryTo(new RTreeBoundary(-10, -10, 1500, 750), results);
    Assert.Equal(50, results.Count);
  }

  [Theory]
  [InlineData(2)]
  [InlineData(3)]
  [InlineData(12)]
  public void AddRemoveAdd_WithMinimalCapacity_ForcesResizeAndAllItemsPresent(ushort maxEntries)
  {
    // Arrange
    var options = new RTreeOptions
    {
      MaxEntriesPerNode = maxEntries,
      InitialNodeCapacity = 2,
      InitialChildBlockCapacity = 1,
    };
    var tree = new RTree<int>(x => new RTreeBoundary(x * 10, x * 5, 8, 8), options);

    // Act: add, remove, re-add to exercise free-list recycling with minimal arena sizes
    for (var index = 0; index < 50; index++)
    {
      tree.Add(index);
    }

    for (var index = 0; index < 50; index += 2)
    {
      tree.Remove(index);
    }

    for (var index = 50; index < 100; index++)
    {
      tree.Add(index);
    }

    // Assert
    Assert.Equal(75, tree.Count);

    var results = new List<int>();
    tree.QueryTo(new RTreeBoundary(-10, -10, 1500, 750), results);
    Assert.Equal(75, results.Count);
  }

  [Theory]
  [InlineData(2)]
  [InlineData(3)]
  [InlineData(12)]
  public void Viewport_WithMinimalCapacity_ForcesResizeAndViewportCorrect(ushort maxEntries)
  {
    // Arrange
    var options = new RTreeOptions
    {
      MaxEntriesPerNode = maxEntries,
      InitialNodeCapacity = 2,
      InitialChildBlockCapacity = 1,
      InitialQueryStackCapacity = 1,
      InitialViewportItemsCapacity = 1,
    };
    var tree = new RTree<int>(x => new RTreeBoundary(x * 10, 0, 8, 8), options);

    for (var index = 0; index < 100; index++)
    {
      tree.Add(index);
    }

    // Act: set viewport covering items 0-9
    tree.Viewport = new RTreeBoundary(-5, -5, 100, 15);

    // Assert
    Assert.Equal(10, tree.ViewportItems.Count);

    // Act: add more items inside viewport
    tree.Add(1000); // at x=10000, outside viewport
    Assert.Equal(10, tree.ViewportItems.Count);

    // Act: remove an item inside viewport
    tree.Remove(5);
    Assert.Equal(9, tree.ViewportItems.Count);
    Assert.DoesNotContain(5, tree.ViewportItems);
  }

  [Fact]
  public void CopyTo_CopiesAllItemsIntoArray()
  {
    // Arrange
    var tree = new RTree<int>([1, 2, 3], x => new RTreeBoundary(x, 0, 1, 1));
    var array = new int[3];

    // Act
    tree.CopyTo(array, 0);

    // Assert
    Assert.Equal([1, 2, 3], [.. array.Order()]);
  }

  [Fact]
  public void CopyTo_WithOffset_PlacesItemsAtCorrectPosition()
  {
    // Arrange
    var tree = new RTree<int>([1, 2, 3], x => new RTreeBoundary(x, 0, 1, 1));
    var array = new int[5];

    // Act
    tree.CopyTo(array, 2);

    // Assert
    Assert.Equal(0, array[0]);
    Assert.Equal(0, array[1]);
    Assert.Equal([1, 2, 3], [.. array[2..].Order()]);
  }

  [Fact]
  public void CopyTo_ArrayTooSmall_ThrowsArgumentException()
  {
    // Arrange
    var tree = new RTree<int>([1, 2, 3], x => new RTreeBoundary(x, 0, 1, 1));

    // Act / Assert
    Assert.Throws<ArgumentException>(() => tree.CopyTo(new int[2], 0)); // Array itself too small
    Assert.Throws<ArgumentException>(() => tree.CopyTo(new int[4], 2)); // Offset leaves insufficient space
  }

  [Fact]
  public void CopyTo_NegativeArrayIndex_ThrowsArgumentOutOfRangeException()
  {
    // Arrange
    var tree = new RTree<int>([1, 2, 3], x => new RTreeBoundary(x, 0, 1, 1));

    // Act / Assert
    Assert.Throws<ArgumentOutOfRangeException>(() => tree.CopyTo(new int[10], -1));
  }

  [Fact]
  public void ICollection_Add_Duplicate_DoesNotThrowAndCountUnchanged()
  {
    // Arrange
    var tree = new RTree<int>(x => new RTreeBoundary(x, 0, 1, 1)) { 1, 2 };
    ICollection<int> collection = tree;

    // Act
    collection.Add(1); // duplicate — void Add must not throw

    // Assert
    Assert.Equal(2, tree.Count);
  }
}
