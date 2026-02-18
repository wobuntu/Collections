using Wobuntu.Collections.Spatial;

namespace Wobuntu.Collections.Tests.Spatial;

public class RTreeNodeTests
{
  [Fact]
  public void CreateLeaf_ChildListIsNull()
  {
    var leaf = RTreeNode<string>.CreateLeaf("Test", new RTreeBoundary());
    Assert.True(leaf.IsLeaf);
    Assert.Null(leaf.Children);
  }

  [Fact]
  public void CreateLeaf_ReferenceType_DataNotNull()
  {
    var boundary = new RTreeBoundary(0, 0, 10, 10);
    var leaf = RTreeNode<string>.CreateLeaf("Test", boundary);
    Assert.Equal("Test", leaf.Data);
    Assert.Equal(boundary, leaf.Boundary);
    Assert.True(leaf.IsLeaf);
  }

  [Fact]
  public void CreateLeaf_ReferenceType_ThrowsOnNull() =>
    Assert.Throws<ArgumentNullException>(() => RTreeNode<string>.CreateLeaf(null!, new RTreeBoundary()));

  [Fact]
  public void CreateLeaf_ValueType_DataNotDefault()
  {
    var boundary = new RTreeBoundary(0, 0, 10, 10);
    var data = Guid.NewGuid();
    var leaf = RTreeNode<Guid>.CreateLeaf(data, boundary);
    Assert.Equal(data, leaf.Data);
    Assert.Equal(boundary, leaf.Boundary);
    Assert.True(leaf.IsLeaf);
    Assert.Null(leaf.Children);
  }

  [Fact]
  public void CreateLeaf_ValueType_DoesNotThrowOnDefault()
  {
    var leaf = RTreeNode<Guid>.CreateLeaf(default, default);
    Assert.Equal(default, leaf.Data);
    Assert.Equal(default, leaf.Boundary);
    Assert.Null(leaf.Children);
  }

  [Fact]
  public void CreateNonLeaf_MaxEntriesSet_CapacityIsMaxPlus1()
  {
    var node = RTreeNode<Guid>.CreateNonLeaf(16);
    var capacity = ((List<RTreeNode<Guid>>)node.Children!).Capacity;
    Assert.Equal(17, capacity);
    Assert.Equal(16, (int)node.RemainingCapacity);
  }

  [Fact]
  public void CreateNonLeaf_ZeroForMaxEntries_MinCapacityCapped()
  {
    var node = RTreeNode<Guid>.CreateNonLeaf(0);
    var capacity = ((List<RTreeNode<Guid>>)node.Children!).Capacity;
    Assert.Equal(RTreeOptions.MinEntriesPerNodeMinimum + 1, capacity);
    Assert.Equal(RTreeOptions.MinEntriesPerNodeMinimum, (int)node.RemainingCapacity);
  }

  [Fact]
  public void IsUnderFull_OnLeaf_ReturnsFalse()
  {
    var node = RTreeNode<string>.CreateLeaf("Test", new RTreeBoundary());
    Assert.False(node.IsUnderFull);
  }

  [Fact]
  public void IsUnderFull_OnNonLeafLessThanMinChildren_ReturnsTrue()
  {
    // Arrange
    var nodeEmpty = RTreeNode<string>.CreateNonLeaf(0);
    var nodeUnderFull = RTreeNode<string>.CreateNonLeaf(0);

    for (var index = 1; index < RTreeOptions.MinEntriesPerNodeMinimum; index++)
    {
      var child = RTreeNode<string>.CreateLeaf("Test" + index, new RTreeBoundary());
      nodeUnderFull.AddChildDirect(child);
    }

    // Act/Assert
    Assert.True(nodeEmpty.IsUnderFull);
    Assert.True(nodeUnderFull.IsUnderFull);
    Assert.True(nodeUnderFull.Children!.Count < RTreeOptions.MinEntriesPerNodeMinimum);
  }

  [Fact]
  public void IsUnderFull_OnNonLeafExactlyMinChildren_ReturnsFalse()
  {
    // Arrange
    var nodeUnderFull = RTreeNode<string>.CreateNonLeaf(0);

    for (var index = 0; index < RTreeOptions.MinEntriesPerNodeMinimum; index++)
    {
      var child = RTreeNode<string>.CreateLeaf("Test" + index, new RTreeBoundary());
      nodeUnderFull.AddChildDirect(child);
    }

    // Act/Assert
    Assert.False(nodeUnderFull.IsUnderFull);
    Assert.Equal(RTreeOptions.MinEntriesPerNodeMinimum, nodeUnderFull.Children!.Count);
  }

  [Fact]
  public void RemainingCapacity_WithCustomCapacity_CountedCorrectlyNeverNegative()
  {
    // Arrange
    const int capacity = 16;

    var exceedingCapacity = RTreeNode<string>.CreateNonLeaf(capacity);
    var exceedingChildren = Enumerable.Range(0, capacity + 1)
      .Select(x => RTreeNode<string>.CreateLeaf("Test" + x, new RTreeBoundary()))
      .ToArray();

    exceedingCapacity.AddChildrenDirect(exceedingChildren.AsSpan());

    var filledUp = RTreeNode<string>.CreateNonLeaf(capacity);

    // Act/Assert
    for (var index = 1; index <= capacity; index++)
    {
      filledUp.AddChildDirect(RTreeNode<string>.CreateLeaf("Test" + index, new RTreeBoundary()));
      Assert.Equal(capacity - index, (int)filledUp.RemainingCapacity);
    }

    Assert.Equal(0, (int)filledUp.RemainingCapacity);
    Assert.Equal(capacity, filledUp.Children!.Count);
    Assert.Equal(0, (int)exceedingCapacity.RemainingCapacity);
    Assert.Equal(capacity + 1, exceedingCapacity.Children!.Count);
  }

  [Fact]
  public void AddChildDirect_UpdatesChildParent()
  {
    // Arrange
    var parent = RTreeNode<string>.CreateNonLeaf(0);
    var childLeaf = RTreeNode<string>.CreateLeaf("Test", new RTreeBoundary());
    var childNonLeaf = RTreeNode<string>.CreateNonLeaf(0);

    // Act
    parent.AddChildDirect(childLeaf);
    parent.AddChildDirect(childNonLeaf);

    // Assert
    Assert.Equal(parent, childLeaf.Parent);
    Assert.Equal(parent, childNonLeaf.Parent);
  }

  [Fact]
  public void AddChildDirect_OneWithOneWithoutBoundary_UpdatesParentBoundaryCorrectly()
  {
    // Arrange
    var parent = RTreeNode<string>.CreateNonLeaf(0);

    var firstChildBoundary = new RTreeBoundary(10, 20, 30, 40);
    var firstChild = RTreeNode<string>.CreateLeaf("Test", firstChildBoundary);

    var secondChild = RTreeNode<string>.CreateNonLeaf(0); // Boundary is here (0,0,0,0)

    // Act
    parent.AddChildDirect(firstChild);
    parent.AddChildDirect(secondChild);

    // Assert
    Assert.Equal(default, secondChild.Boundary);
    Assert.Equal(firstChildBoundary, firstChild.Boundary);
    Assert.Equal(firstChildBoundary, parent.Boundary);
  }

  [Fact]
  public void AddChildDirect_TwoWithSetBoundary_UpdatesParentBoundaryCorrectly()
  {
    // Arrange
    var parent = RTreeNode<string>.CreateNonLeaf(0);

    var firstChildBoundary = new RTreeBoundary(10, 20, 30, 40);
    var firstChild = RTreeNode<string>.CreateLeaf("Test", firstChildBoundary);

    var secondChild = RTreeNode<string>.CreateNonLeaf(0); // Boundary is here (0,0,0,0)

    var thirdChildBoundary = new RTreeBoundary(25, 10, 30, 40);
    var thirdChild = RTreeNode<string>.CreateLeaf("Test", thirdChildBoundary);

    // Act
    parent.AddChildDirect(firstChild);
    secondChild.AddChildDirect(thirdChild);
    parent.AddChildDirect(secondChild);

    // Assert
    Assert.Equal(firstChildBoundary, firstChild.Boundary);
    Assert.Equal(thirdChildBoundary, thirdChild.Boundary);
    Assert.Equal(thirdChildBoundary, secondChild.Boundary);
    Assert.Equal(new RTreeBoundary(10, 10, 45, 50), parent.Boundary);
  }

  [Fact]
  public void RemoveChildDirect_ChildWithNonEmptyBoundary_RecalculatesOwnBoundary()
  {
    // Arrange
    var parent = RTreeNode<int>.CreateNonLeaf(3);

    var children = Enumerable
      .Range(1, 3)
      .Select(x => RTreeNode<int>.CreateLeaf(x, new RTreeBoundary(x * 10, x * 10, 10, 10)))
      .ToArray()
      .AsSpan();

    parent.AddChildrenDirect(children);
    var previousBoundary = parent.Boundary;

    // Act
    parent.RemoveChildDirect(children[^1]);

    // Assert
    Assert.Equal(new RTreeBoundary(10, 10, 30, 30), previousBoundary);
    Assert.Equal(2, parent.Children!.Count);
    Assert.Equal(new RTreeBoundary(10, 10, 20, 20), parent.Boundary);
  }

  [Fact]
  public void InsertParentLayer_CreatesCorrectRelationships()
  {
    // Arrange
    var boundary = new RTreeBoundary(10, 20, 30, 40);
    var leaf = RTreeNode<string>.CreateLeaf("Test", boundary);
    var oldRoot = RTreeNode<string>.CreateNonLeaf(0);
    oldRoot.AddChildDirect(leaf);

    // Act
    var insertedAboveLeaf = leaf.InsertParentLayer(0);
    var newRoot = oldRoot.InsertParentLayer(0); // Handles case if parent is null.

    // Assert
    Assert.Null(newRoot.Parent);
    Assert.Equal(newRoot, oldRoot.Parent!);
    Assert.Equal(oldRoot, insertedAboveLeaf.Parent!);
    Assert.Equal(insertedAboveLeaf, leaf.Parent!);

    Assert.Equal(boundary, insertedAboveLeaf.Boundary);
    Assert.Equal(boundary, oldRoot.Boundary);
    Assert.Equal(boundary, newRoot.Boundary);
  }

  [Fact]
  public void Split_AlternatingPositionsExceedsCapacityForInlineSplit_CreatesCutoffAndCorrectRegions()
  {
    // Arrange
    var root = RTreeNode<int>.CreateNonLeaf(2);
    var node = RTreeNode<int>.CreateNonLeaf(7);
    root.AddChildDirect(RTreeNode<int>.CreateNonLeaf(2)); // Occupy the full capacity of the root

    // Approximate layout of data below, expecting separation here at x=0 drawn as a line:
    // 7.........|.........6
    // ......5...|...4......
    // ..........|..........
    // ...3......|......2...
    // ..........|..........
    // .......1..|..0.......
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(0, new RTreeBoundary(20, 80, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(1, new RTreeBoundary(-30, 80, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(2, new RTreeBoundary(60, 60, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(3, new RTreeBoundary(-70, 60, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(4, new RTreeBoundary(30, 40, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(5, new RTreeBoundary(-40, 40, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(6, new RTreeBoundary(90, 30, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(7, new RTreeBoundary(-100, 30, 10, 10)));

    root.AddChildDirect(node);
    var boundaryBeforeSplit = node.Boundary;

    // Act
    var cutoffBranch = node.SplitAndGetCutoffBranch();

    // Assert
    Assert.Equal(new RTreeBoundary(-100, 30, 200, 60), boundaryBeforeSplit);

    Assert.NotNull(cutoffBranch); // Created as there is no capacity in the parent of "node" left.
    Assert.Null(cutoffBranch.Parent);
    Assert.Equal(new RTreeBoundary(-100, 30, 80, 60), cutoffBranch.Boundary); // All with negative X

    Assert.Equal(2, root.Children!.Count);
    Assert.Equal(4, node.Children!.Count);
    Assert.Equal(new RTreeBoundary(20, 30, 80, 60), node.Boundary); // All with positive X
  }

  [Fact]
  public void Split_AlternatingPositionsNoParent_CreatesCutoffAndCorrectRegions()
  {
    // Arrange
    var node = RTreeNode<int>.CreateNonLeaf(7);

    // Approximate layout of data below, expecting separation here at x=0 drawn as a line:
    // 7.........|.........6
    // ......5...|...4......
    // ..........|..........
    // ...3......|......2...
    // ..........|..........
    // .......1..|..0.......
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(0, new RTreeBoundary(20, 80, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(1, new RTreeBoundary(-30, 80, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(2, new RTreeBoundary(60, 60, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(3, new RTreeBoundary(-70, 60, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(4, new RTreeBoundary(30, 40, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(5, new RTreeBoundary(-40, 40, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(6, new RTreeBoundary(90, 30, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(7, new RTreeBoundary(-100, 30, 10, 10)));

    var boundaryBeforeSplit = node.Boundary;

    // Act
    var cutoffBranch = node.SplitAndGetCutoffBranch();

    // Assert
    Assert.Equal(new RTreeBoundary(-100, 30, 200, 60), boundaryBeforeSplit);

    Assert.NotNull(cutoffBranch); // Created as there is no capacity in the parent of "node" left.
    Assert.Null(cutoffBranch.Parent);
    Assert.Equal(new RTreeBoundary(-100, 30, 80, 60), cutoffBranch.Boundary); // All with negative X

    Assert.Equal(4, node.Children!.Count);
    Assert.Equal(new RTreeBoundary(20, 30, 80, 60), node.Boundary); // All with positive X
  }

  [Fact]
  public void Split_AlternatingPositionsSufficientCapacityForInlineSplit_DoesInlineSplitWithCorrectRegions()
  {
    // Arrange
    var root = RTreeNode<int>.CreateNonLeaf(2);
    var node = RTreeNode<int>.CreateNonLeaf(7);

    // Approximate layout of data below, expecting separation here at x=0 drawn as a line:
    // 7.........|.........6
    // ......5...|...4......
    // ..........|..........
    // ...3......|......2...
    // ..........|..........
    // .......1..|..0.......
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(0, new RTreeBoundary(20, 80, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(1, new RTreeBoundary(-30, 80, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(2, new RTreeBoundary(60, 60, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(3, new RTreeBoundary(-70, 60, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(4, new RTreeBoundary(30, 40, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(5, new RTreeBoundary(-40, 40, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(6, new RTreeBoundary(90, 30, 10, 10)));
    node.AddChildDirect(RTreeNode<int>.CreateLeaf(7, new RTreeBoundary(-100, 30, 10, 10)));

    root.AddChildDirect(node);
    var nodeBoundaryBeforeSplit = node.Boundary;

    // Act
    var cutoffBranch = node.SplitAndGetCutoffBranch();

    // Assert
    Assert.Null(cutoffBranch);

    Assert.Equal(new RTreeBoundary(-100, 30, 200, 60), nodeBoundaryBeforeSplit);
    Assert.Equal(nodeBoundaryBeforeSplit, root.Boundary);

    Assert.Equal(4, node.Children!.Count);
    Assert.Equal(new RTreeBoundary(20, 30, 80, 60), node.Boundary); // All with positive X

    Assert.Equal(2, root.Children!.Count);
    Assert.Equal(new RTreeBoundary(-100, 30, 80, 60), root.Children[1].Boundary); // All with negative X
  }
}