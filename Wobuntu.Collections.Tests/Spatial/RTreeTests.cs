using System.Runtime.InteropServices;
using Wobuntu.Collections.Spatial;

namespace Wobuntu.Collections.Tests.Spatial;

public class RTreeTests
{
  [Fact]
  public void Count_OnAddRemoveAndClear_UpdatesCorrectly()
  {
    // Arrange
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
  public void Query_ForBoundaryWithIntersectingData_ReturnsExpectedNodes(int nodeCapacity)
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
    var query0And2 = tree.Query(new RTreeBoundary(25, 65, 40, 20)).Order().ToArray();
    var query5And7 = tree.Query(new RTreeBoundary(-95, 35, 60, 10)).Order().ToArray();

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
  public void Query_ForBoundaryWithContainingData_ReturnsExpectedNodes(int nodeCapacity)
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
    var query2To5 = tree.Query(new RTreeBoundary(-75, 35, 150, 40)).Order().ToArray();

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
  public void Query_WithEmptyBoundaries_EmptyAreNotIncludedInResultSet(int nodeCapacity)
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
    var queryWithout8Or9 = tree.Query(new RTreeBoundary(-500, -500, 1000, 1000)).Order().ToArray();

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
  public void Constructor_WithKnownDataMaxNodeSize2_InitialTreeStructureExpected()
  {
    // Arrange
    var items = Enumerable.Range(0, 8).ToArray();
    var options = new RTreeOptions { MaxEntriesPerNode = 2 };

    RTreeBoundary BoundarySelector(int item)
      => item switch
      {
        // Approximate layout and expected separation of data below:
        // 7.........|.........6
        // ......5...|...4......
        // ----------|----------
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
      };

    // Act
    var tree = new RTree<int>(items, BoundarySelector, options);

    var root = tree.Root;

    // Act
    Assert.False(root.IsLeaf);
    Assert.Equal(2, root.Children.Count);

    var l11 = root.Children[0];
    Assert.False(l11.IsLeaf);
    Assert.Equal(2, l11.Children.Count);
    Assert.Equal(new RTreeBoundary(-100, 30, 80, 60), l11.Boundary); // Left side

    var l21 = l11.Children[0];
    Assert.False(l21.IsLeaf);
    Assert.Equal(2, l21.Children.Count);
    Assert.Equal(new RTreeBoundary(-100, 30, 70, 20), l21.Boundary); // Upper left quadrant

    var l31 = l21.Children[0];
    Assert.True(l31.IsLeaf);
    var l32 = l21.Children[1];
    Assert.True(l32.IsLeaf);

    var l22 = l11.Children[1];
    Assert.False(l22.IsLeaf);
    Assert.Equal(2, l22.Children.Count);
    Assert.Equal(new RTreeBoundary(-70, 60, 50, 30), l22.Boundary); // Lower left quadrant

    var l33 = l22.Children[0];
    Assert.True(l33.IsLeaf);
    var l34 = l22.Children[1];
    Assert.True(l34.IsLeaf);

    var l12 = root.Children[1];
    Assert.False(l12.IsLeaf);
    Assert.Equal(2, l12.Children.Count);
    Assert.Equal(new RTreeBoundary(20, 30, 80, 60), l12.Boundary); // right side

    var l23 = l12.Children[0];
    Assert.False(l23.IsLeaf);
    Assert.Equal(2, l23.Children.Count);
    Assert.Equal(new RTreeBoundary(30, 30, 70, 20), l23.Boundary); // Upper right quadrant

    var l35 = l23.Children[0];
    Assert.True(l35.IsLeaf);
    var l36 = l23.Children[1];
    Assert.True(l36.IsLeaf);

    var l24 = l12.Children[1];
    Assert.False(l24.IsLeaf);
    Assert.Equal(2, l24.Children.Count);
    Assert.Equal(new RTreeBoundary(20, 60, 50, 30), l24.Boundary); // Lower right quadrant

    var l37 = l24.Children[0];
    Assert.True(l37.IsLeaf);
    var l38 = l24.Children[1];
    Assert.True(l38.IsLeaf);
  }

  [Fact]
  public void Constructor_WithKnownDataMaxNodeSize3_InitialTreeStructureExpected()
  {
    // Arrange
    var items = Enumerable.Range(0, 8).ToArray();
    var options = new RTreeOptions { MaxEntriesPerNode = 3 };

    RTreeBoundary BoundarySelector(int item)
      => item switch
      {
        // Approximate layout and expected separation of data below:
        // 7.........|.........6
        // ......5...|...4......
        // ..........|..........
        // ...3......|......2...
        // ----------|----------
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
      };

    // Act
    var tree = new RTree<int>(items, BoundarySelector, options);

    var root = tree.Root;

    // Act
    Assert.False(root.IsLeaf);
    Assert.Equal(2, root.Children.Count);

    var l11 = root.Children[0];
    Assert.False(l11.IsLeaf);
    Assert.Equal(2, l11.Children.Count);
    Assert.Equal(new RTreeBoundary(-100, 30, 80, 60), l11.Boundary); // Left side (2 container nodes)

    var l21 = l11.Children[0];
    Assert.False(l21.IsLeaf);
    Assert.Equal(3, l21.Children.Count);
    Assert.Equal(new RTreeBoundary(-100, 30, 70, 40), l21.Boundary); // Upper left quadrant (Nodes 7,5,3)

    var l31 = l21.Children[0]; // Node 7
    Assert.True(l31.IsLeaf);
    var l32 = l21.Children[1]; // Node 5
    Assert.True(l32.IsLeaf);
    var l33 = l21.Children[2]; // Node 3
    Assert.True(l33.IsLeaf);

    var l22 = l11.Children[1];
    Assert.False(l22.IsLeaf);
    Assert.Single(l22.Children);
    Assert.Equal(new RTreeBoundary(-30, 80, 10, 10), l22.Boundary); // Lower left quadrant (Node 1)

    var l34 = l22.Children[0]; // Node 1
    Assert.True(l34.IsLeaf);

    var l12 = root.Children[1];
    Assert.False(l12.IsLeaf);
    Assert.Equal(2, l12.Children.Count);
    Assert.Equal(new RTreeBoundary(20, 30, 80, 60), l12.Boundary); // right side (2 container nodes)

    var l23 = l12.Children[0];
    Assert.False(l23.IsLeaf);
    Assert.Equal(3, l23.Children.Count);
    Assert.Equal(new RTreeBoundary(30, 30, 70, 40), l23.Boundary); // Upper right quadrant (Nodes 6,4,2)

    var l35 = l23.Children[0];
    Assert.True(l35.IsLeaf);
    var l36 = l23.Children[1];
    Assert.True(l36.IsLeaf);
    var l37 = l23.Children[2];
    Assert.True(l37.IsLeaf);

    var l24 = l12.Children[1];
    Assert.False(l24.IsLeaf);
    Assert.Single(l24.Children);
    Assert.Equal(new RTreeBoundary(20, 80, 10, 10), l24.Boundary); // Lower right quadrant (Node 0)

    var l38 = l24.Children[0];
    Assert.True(l38.IsLeaf);
  }

  [Fact]
  public void Constructor_WithKnownDataMaxNodeSize4_InitialTreeStructureExpected()
  {
    // Arrange
    var items = Enumerable.Range(0, 8).ToArray();
    var options = new RTreeOptions { MaxEntriesPerNode = 4 };

    RTreeBoundary BoundarySelector(int item)
      => item switch
      {
        // Approximate layout and expected separation of data below:
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
      };

    // Act
    var tree = new RTree<int>(items, BoundarySelector, options);

    var root = tree.Root;

    // Act
    Assert.False(root.IsLeaf);
    Assert.Equal(2, root.Children.Count);

    var l11 = root.Children[0];
    Assert.False(l11.IsLeaf);
    Assert.Equal(4, l11.Children.Count);
    Assert.Equal(new RTreeBoundary(-100, 30, 80, 60), l11.Boundary); // Left side (no container nodes, just leafs)

    var l21 = l11.Children[0]; // Node 7
    Assert.True(l21.IsLeaf);
    var l22 = l11.Children[1]; // Node 5
    Assert.True(l22.IsLeaf);
    var l23 = l11.Children[2]; // Node 3
    Assert.True(l23.IsLeaf);
    var l24 = l11.Children[3]; // Node 1
    Assert.True(l24.IsLeaf);

    var l12 = root.Children[1];
    Assert.False(l12.IsLeaf);
    Assert.Equal(4, l12.Children.Count);
    Assert.Equal(new RTreeBoundary(20, 30, 80, 60), l12.Boundary); // right side (no container nodes, just leafs)

    var l25 = l11.Children[0]; // Node 7
    Assert.True(l25.IsLeaf);
    var l26 = l11.Children[1]; // Node 5
    Assert.True(l26.IsLeaf);
    var l27 = l11.Children[2]; // Node 3
    Assert.True(l27.IsLeaf);
    var l28 = l11.Children[3]; // Node 1
    Assert.True(l28.IsLeaf);
  }

  [Fact]
  public void Add_EmptyViewport_ViewportItemsEmpty()
  {
    // Arrange
    // Arrange
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
        2 => new RTreeBoundary(100, 240, 10, 20), // Going to be inserted later
        3 => new RTreeBoundary(100, 440, 10, 20), // Going to be inserted later
        _ => throw new ArgumentOutOfRangeException(nameof(item), item, null)
      };

    // Act
    var tree = new RTree<int>(BoundarySelector, options);

    // Act / Assert
    tree.Add(0);
    tree.Add(1);
    Assert.Equal(new RTreeBoundary(100, 200, 10, 220), tree.Boundary);
    Assert.Equal(2, tree.Root.Children!.Count);
    Assert.True(tree.Root.Children![0].IsLeaf);
    Assert.True(tree.Root.Children![1].IsLeaf);

    tree.Add(2);
    Assert.Equal(new RTreeBoundary(100, 200, 10, 220), tree.Boundary);
    Assert.Equal(2, tree.Root.Children!.Count);
    Assert.False(tree.Root.Children![0].IsLeaf);
    Assert.True(tree.Root.Children![0].Children![0].IsLeaf);
    Assert.True(tree.Root.Children![0].Children![1].IsLeaf);
    Assert.True(tree.Root.Children![1].IsLeaf);

    tree.Add(3);
    Assert.Equal(new RTreeBoundary(100, 200, 10, 220), tree.Boundary);
    Assert.Equal(2, tree.Root.Children!.Count);
    Assert.False(tree.Root.Children![0].IsLeaf);
    Assert.True(tree.Root.Children![0].Children![0].IsLeaf);
    Assert.True(tree.Root.Children![0].Children![1].IsLeaf);
    Assert.False(tree.Root.Children![1].IsLeaf);
    Assert.True(tree.Root.Children![0].Children![0].IsLeaf);
    Assert.True(tree.Root.Children![0].Children![1].IsLeaf);
  }

  [Fact]
  public void Add_ExceedingNodeSize_NewlyCreatedLayersReuseExistingNodes()
  {
    // Arrange
    var options = new RTreeOptions { MaxEntriesPerNode = 2 };
    var tree = new RTree<int>(value => new RTreeBoundary(value * 10, 10, 10, 10), options) { 1, 5 };

    var root1 = tree.Root;
    var item11 = root1.Children![0];
    var item12 = root1.Children![1];

    // Act
    tree.Add(2);
    var root2 = tree.Root;
    var layer21 = root2.Children![0];
    var item21 = layer21.Children![0];
    var item22 = root2.Children![1];

    tree.Add(6);
    var root3 = tree.Root;
    var layer31 = root3.Children![0];
    var item31 = layer31.Children![0];
    var layer32 = root3.Children![1];
    var item32 = layer32.Children![0];

    // Assert
    // Check exact same initial item nodes are still in use
    Assert.True(ReferenceEquals(item11, item21));
    Assert.True(ReferenceEquals(item12, item22));
    Assert.True(ReferenceEquals(item21, item31));
    Assert.True(ReferenceEquals(item22, item32));

    // Check root node was reused while inserting items (movement happened first during add of "2")
    Assert.True(ReferenceEquals(root1, root2));
    Assert.True(ReferenceEquals(root2, root3));
  }

  [Fact]
  public void Remove_CausingUnderfullNode_SiblingsMovedOneLevelUpIfParentHasSpace()
  {
    // Arrange
    var options = new RTreeOptions { MaxEntriesPerNode = 3 };
    var tree = new RTree<int>(value => new RTreeBoundary(value * 10, 10, 10, 10), options) { 1, 5, 2, 6, 0, 3, 4, 7 };

    var root = tree.Root;

    // Though nodes store clusters based on spacial locality,
    // they may not be ordered and be created based on initial data.
    var orderedRootChildren = root.Children!.OrderBy(x => x.Boundary.CenterX).ToList();
    var layer11 = orderedRootChildren[0];
    var layer12 = orderedRootChildren[1];
    var layer13 = orderedRootChildren[2];

    var orderedLayer11Children = layer11.Children!.OrderBy(x => x.Boundary.CenterX).ToList();
    var layer21 = orderedLayer11Children[0];
    var layer22 = orderedLayer11Children[1];

    var orderedLayer12Children = layer12.Children!.OrderBy(x => x.Boundary.CenterX).ToList();
    var layer23 = orderedLayer12Children[0];
    var layer24 = orderedLayer12Children[1];
    var layer25 = orderedLayer12Children[2];

    var orderedLayer13Children = layer13.Children!.OrderBy(x => x.Boundary.CenterX).ToList();
    var layer26 = orderedLayer13Children[0];
    var layer27 = orderedLayer13Children[1];
    var layer28 = orderedLayer13Children[2];

    // Assert expected structure first, so this test makes sense at all:
    Assert.False(layer11.IsUnderFull);
    Assert.False(layer12.IsUnderFull);
    Assert.False(layer13.IsUnderFull);

    Assert.Equal(0, layer21.Data);
    Assert.Equal(1, layer22.Data);
    Assert.Equal(2, layer23.Data);
    Assert.Equal(3, layer24.Data);
    Assert.Equal(4, layer25.Data);
    Assert.Equal(5, layer26.Data);
    Assert.Equal(6, layer27.Data);
    Assert.Equal(7, layer28.Data);

    // Assert: Removal of a node with one item left, will properly replace itself with the remaining child.
    tree.Remove(0);
    Assert.Equal(3, root.Children!.Count);

    orderedRootChildren = root.Children!.OrderBy(x => x.Boundary.CenterX).ToList();
    layer11 = orderedRootChildren[0];
    layer12 = orderedRootChildren[1];
    layer13 = orderedRootChildren[2];

    // layer11 was replaced with layer22, because its own removal fits the one remaining child
    Assert.True(layer11.IsLeaf);
    Assert.False(layer12.IsLeaf);
    Assert.False(layer13.IsLeaf);

    Assert.Equal(1, layer11.Data);
    Assert.Equal(layer22, layer11); // No new node created, instead moved

    // Assert: Removal of a node with more than one item, will also properly move its children up:
    //         Root has 1 slot free, layer12 has 2 children after the remove below, by replacing itself
    //         with a child and filling the free slot, it can balance the tree:
    tree.Remove(1); // Ensure one slot free in root
    tree.Remove(5);
    Assert.Equal(3, root.Children!.Count);

    orderedRootChildren = root.Children!.OrderBy(x => x.Boundary.CenterX).ToList();
    layer11 = orderedRootChildren[0];
    layer12 = orderedRootChildren[1];
    layer13 = orderedRootChildren[2];

    Assert.False(layer11.IsLeaf);
    Assert.Equal(3, layer11.Children.Count); // 2, 3, 4
    Assert.Equal(6, layer12.Data);
    Assert.Equal(7, layer13.Data);
  }

  [Fact]
  public void Remove_Until1Item_CausesRootToBecomeLeaf()
  {
    // Arrange
    var tree = new RTree<int>(value => new RTreeBoundary(value * 10, 10, 10, 10)) { 1, 2 };

    // Act
    tree.Remove(1);

    // Assert
    Assert.True(tree.Root.IsLeaf);
    Assert.Equal(2, tree.Root.Data);
  }

  [Fact]
  public void Remove_UntilNothingLeft_CausesEmptyRootNode()
  {
    var tree = new RTree<int>(value => new RTreeBoundary(value * 10, 10, 10, 10)) { 1, 2 };

    // Act
    tree.Remove(1);
    tree.Remove(2);

    // Assert
    Assert.NotNull(tree.Root);
    Assert.False(tree.Root.IsLeaf);
  }

  [Fact]
  public void RemoveThenAdd_OfSameItem_Works()
  {
    // Arrange
    var tree = new RTree<int>(value => new RTreeBoundary(value * 10, 10, 10, 10)) { 1, 2 };

    // Act / Assert
    tree.Remove(1);
    Assert.False(tree.Contains(1));
    Assert.Single(tree);

    tree.Add(1);
    Assert.True(tree.Contains(1));
    Assert.Equal(2, tree.Count);
  }

  [Fact]
  public void ChooseInsertLeaf_NoMatchingChildBoundariesRootCapacityAvailable_ReturnsRootNode()
  {
    // Arrange
    var options = new RTreeOptions { MaxEntriesPerNode = 3 };
    var tree = new RTree<int>(BoundarySelector, options) { 10, 50 };

    // Act
    var result1 = tree.ChooseInsertLeaf(BoundarySelector(90));
    var result2 = tree.ChooseInsertLeaf(new RTreeBoundary());
    var result3 = tree.ChooseInsertLeaf(BoundarySelector(-10));

    // Assert
    Assert.Equal(tree.Root, result1);
    Assert.Equal(tree.Root, result2);
    Assert.Equal(tree.Root, result3);
    return;

    static RTreeBoundary BoundarySelector(int value) => new(value, value, 10, 10);
  }

  [Fact]
  public void ChooseInsertLeaf_NoMatchingChildBoundariesRootNoMoreCapacity_ReturnsClosestChild()
  {
    // Arrange
    var options = new RTreeOptions { MaxEntriesPerNode = 2 };
    var tree = new RTree<int>(BoundarySelector, options) { 10, 50 };

    // Act
    var result1 = tree.ChooseInsertLeaf(BoundarySelector(90));
    var result2 = tree.ChooseInsertLeaf(new RTreeBoundary());
    var result3 = tree.ChooseInsertLeaf(BoundarySelector(-10));

    // Assert
    Assert.Equal(tree.Root.Children![1], result1);
    Assert.Equal(tree.Root.Children![0], result2);
    Assert.Equal(tree.Root.Children![0], result3);
    return;

    static RTreeBoundary BoundarySelector(int value) => new(value, value, 10, 10);
  }

  // TODO: Test Viewport + ViewportItems
  // - Size equals user size, not actual size
  // - Update (add/remove) updates viewport items accordingly
  // - All cases of hot paths
  // TODO: Test RebalanceAncestorNodes
  // TODO: test against insert nodes with max entries per node = 2, so that a new layer is inserted
  // TODO: @ InsertNode & ChooseInsertLeaf
  // TODO: Skip viewport stuff if viewport empty (e.g. RemoveNoLongerIntersectingViewportItems)? Now also running on add, etc.
  // TODO: All the viewport stuff
  // TODO: Test against equality of initial distribution vs adding items manually via add, should be similar
}