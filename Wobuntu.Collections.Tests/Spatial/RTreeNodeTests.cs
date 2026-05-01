using Wobuntu.Collections.Spatial;
using Wobuntu.Collections.Tests.Spatial.Helpers;

namespace Wobuntu.Collections.Tests.Spatial;

/// <summary>
///   Tests verifying structural node behaviors through the RTree arena API.
/// </summary>
public class RTreeNodeTests
{
  [Fact]
  public void LeafNode_HasNoChildren()
  {
    // Arrange
    var tree = new RTree<string>(_ => new RTreeBoundary(0, 0, 10, 10)) { "Test" };

    // Assert: root should have been collapsed to a single leaf
    // Actually with a single item, root is a non-leaf with one leaf child.
    // After adding one more and removing it, we get a leaf root.
    var root = tree.CreateTestView(tree.RootIndex);
    Assert.False(root.IsLeaf); // Root is always non-leaf initially
    Assert.Equal(1, root.ChildCount);

    var leaf = root.Child(0);
    Assert.True(leaf.IsLeaf);
    Assert.Equal(0, leaf.ChildCount);
  }

  [Fact]
  public void LeafNode_StoresDataAndBoundary()
  {
    var boundary = new RTreeBoundary(0, 0, 10, 10);
    var tree = new RTree<string>(_ => boundary) { "Test" };

    var leaf = tree.CreateTestView(tree.RootIndex).Child(0);
    Assert.Equal("Test", leaf.Data);
    Assert.Equal(boundary, leaf.Boundary);
    Assert.True(leaf.IsLeaf);
  }

  [Fact]
  public void NonLeafNode_HasCorrectCapacity()
  {
    var options = new RTreeOptions { MaxEntriesPerNode = 16 };
    var tree = new RTree<int>(x => new RTreeBoundary(x, 0, 1, 1), options) { 1 };

    var root = tree.CreateTestView(tree.RootIndex);
    Assert.False(root.IsLeaf);
    Assert.Equal(15, root.RemainingCapacity); // 16 - 1 child
  }

  [Fact]
  public void RemainingCapacity_WithCustomCapacity_CountedCorrectly()
  {
    // Arrange
    var options = new RTreeOptions { MaxEntriesPerNode = 16 };
    var tree = new RTree<int>(x => new RTreeBoundary(x, 0, 1, 1), options);
    var root = tree.CreateTestView(tree.RootIndex);

    // Act/Assert: Add items one by one and check capacity decreases
    for (var index = 1; index <= 16; index++)
    {
      tree.Add(index);
      Assert.Equal(16 - index, root.RemainingCapacity);
    }

    Assert.Equal(0, root.RemainingCapacity);
    Assert.Equal(16, root.ChildCount);
  }

  [Fact]
  public void AddChild_UpdatesParentBoundary()
  {
    // Arrange
    var options = new RTreeOptions { MaxEntriesPerNode = 4 };
    var tree = new RTree<string>(x => x switch
    {
      "A" => new RTreeBoundary(10, 20, 30, 40),
      "B" => new RTreeBoundary(50, 60, 10, 10),
      _ => new RTreeBoundary()
    }, options) { "A" };

    Assert.Equal(new RTreeBoundary(10, 20, 30, 40), tree.Boundary);

    // Act
    tree.Add("B");

    // Assert: root boundary should be the union of both children
    Assert.Equal(new RTreeBoundary(10, 20, 50, 50), tree.Boundary);
  }

  [Fact]
  public void RemoveChild_RecalculatesBoundary()
  {
    // Arrange
    var options = new RTreeOptions { MaxEntriesPerNode = 4 };
    var tree = new RTree<int>(x => new RTreeBoundary(x * 10, x * 10, 10, 10), options);

    tree.Add(1); // boundary: 10,10,10,10
    tree.Add(2); // boundary: 20,20,10,10
    tree.Add(3); // boundary: 30,30,10,10

    var boundaryBefore = tree.Boundary;
    Assert.Equal(new RTreeBoundary(10, 10, 30, 30), boundaryBefore);

    // Act: remove the item that extends the boundary the most
    tree.Remove(3);

    // Assert: boundary should shrink
    Assert.Equal(new RTreeBoundary(10, 10, 20, 20), tree.Boundary);
  }

  [Fact]
  public void InsertParentLayer_PreservesItems()
  {
    // Arrange: use maxEntries=2 so adding a 3rd item forces a parent layer insertion
    var options = new RTreeOptions { MaxEntriesPerNode = 2 };
    var tree = new RTree<int>(x => new RTreeBoundary(x * 10, 10, 10, 10), options);

    tree.Add(1);
    tree.Add(2);
    Assert.Equal(2, tree.CreateTestView(tree.RootIndex).ChildCount);

    // Act: adding a 3rd item forces InsertParentLayer
    tree.Add(3);

    // Assert: all items are still present
    Assert.True(tree.Contains(1));
    Assert.True(tree.Contains(2));
    Assert.True(tree.Contains(3));
    Assert.Equal(3, tree.Count);
  }
}