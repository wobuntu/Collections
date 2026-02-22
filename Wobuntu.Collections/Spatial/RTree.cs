#nullable enable

using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;

// Disabling some resharper suggestions as they are hurting performance in hot paths of this file:
// ReSharper disable ForeachCanBePartlyConvertedToQueryUsingAnotherGetEnumerator
// ReSharper disable ForCanBeConvertedToForeach
// ReSharper disable LoopCanBeConvertedToQuery

// TLDR;
// Data is laid out in RTreeNode<T> objects, starting with a root node and variable amounts of branches.
// A node can either be a leaf or not.
// If a node is a leaf, it always holds data but never has children.
// If a node is not a leaf, it never holds data but can hold children.
// Nodes store data which has similar spacial locality and each depth layer gets more granular.

namespace Wobuntu.Collections.Spatial;

/// <summary>
///   Represents a modified sort-tile-recursive rectangle tree (STR-RTree).<br />
///   Though the concept remains the same, this RTree implementation differs from a classical implementation by
///   optimizing on rectangle center distances over rectangle sizes and builds its tree not recursively, but
///   iteratively.<br />
///   Use this data structure to organize spatial data, which does not overlap heavily, is unevenly distributed,
///   and varies in geometric size. Lookups and inserts for spatial regions is fast, removals are slightly
///   slower.<br />
///   The tree is constructed bottom-up, starting at the data leafs. Initially, a data leaf is created for each
///   object, which also holds a boundary describing a rectangle in a geometric space. The boundary is used initially
///   to sort the nodes according to their X/Y coordinates. Based on a limit of maximum children of a parent node and
///   the geometric closeness of nodes, they are organized together in further parent nodes, describing rectangles
///   enclosing their children. This is repeated until all nodes are contained in a common root, from which each
///   object can be reached.
/// </summary>
/// <typeparam name="T">The type of data to contain within this tree.</typeparam>
public class RTree<T>
  : IEnumerable<T>
  where T : notnull
{
  // Capacity constants to avoid unnecessary resize, especially during first queries:
  private const int DefaultQueryResultMinCapacity = 64;
  private const int DefaultQueryStackMinCapacity = 64;

  // Cached delegates to avoid a heap allocation per Sort call in BuildSortTileRecursiveTree.
  // Static lambdas on a generic class are per-instantiation, which is exactly what we need here.
  private static readonly Comparison<RTreeNode<T>> CompareByCenterX =
    static (left, right) => left.Boundary.CenterX.CompareTo(right.Boundary.CenterX);

  private static readonly Comparison<RTreeNode<T>> CompareByCenterY =
    static (left, right) => left.Boundary.CenterY.CompareTo(right.Boundary.CenterY);

  private readonly Stack<RTreeNode<T>> _queryStack;

  private readonly Dictionary<T, RTreeNode<T>> _itemToNode;

  private List<RTreeNode<T>>? _recycledLeafNodes;
  private List<RTreeNode<T>>? _recycledNonLeafNodes;

  private readonly Func<T, RTreeBoundary> _boundarySelector;
  private readonly int _maxEntriesPerNode;
  private readonly double _updateViewportItemsOnShrinkThreshold;
  private readonly double _leafNodeVirtualFullness;

  private RTreeBoundary _viewport;
  private RTreeBoundary _actualViewport;
  private readonly SynchronizedObservableOrderedSet<T> _viewportItems;

  internal RTreeNode<T> Root; // Internal for access in UTests

  public RTree(Func<T, RTreeBoundary> boundarySelector, RTreeOptions? options = null)
    : this(options ?? new RTreeOptions(), boundarySelector) { }

  public RTree(Span<T> items, Func<T, RTreeBoundary> boundarySelector, RTreeOptions? options = null)
    : this(options ?? new RTreeOptions(), boundarySelector)
  {
    BulkInitialize(items);
  }

  private RTree(RTreeOptions options, Func<T, RTreeBoundary> boundarySelector)
  {
    ArgumentNullException.ThrowIfNull(boundarySelector);

    _boundarySelector = boundarySelector;
    _maxEntriesPerNode = options.MaxEntriesPerNode;
    _updateViewportItemsOnShrinkThreshold = options.UpdateViewportItemsOnShrinkThreshold;

    // The following will choose a virtual fullness of leaf nodes, somewhere between the minimum and maximum amount
    // of nodes. This will make choosing a node for dynamic insert (= not during bulk initialize) fairer. If we treated
    // leaf nodes as being empty, they would always be picked if on the same level as non-leaf nodes for insert, which
    // would cause massive overlaps of RTreeNodes.
    _leafNodeVirtualFullness = (_maxEntriesPerNode - options.MinEntriesPerNode) / (double)_maxEntriesPerNode;

    _itemToNode = new Dictionary<T, RTreeNode<T>>();
    _queryStack = new Stack<RTreeNode<T>>(DefaultQueryStackMinCapacity);
    _viewportItems = new SynchronizedObservableOrderedSet<T>(DefaultQueryResultMinCapacity);

    Root = RTreeNode<T>.CreateNonLeaf(_maxEntriesPerNode);
  }

  public int Count => _itemToNode.Count;

  public RTreeBoundary Boundary => Root.Boundary;

  public RTreeBoundary Viewport
  {
    get => _viewport;
    set
    {
      if (_viewport == value)
      {
        return;
      }

      if (value.IsEmpty && !_actualViewport.IsEmpty)
      {
        ResetViewportItems();
        _actualViewport = new RTreeBoundary();
        _viewport = value;
        return;
      }

      // If the new viewport is contained in the current viewport, then we can just keep the cached
      // data as is. This means the viewport items may contain additional elements, which are currently
      // outside the viewport, however, this should be beneficial for performance during zooming and panning.
      // The threshold for an actual resize can be influenced by the options passed to the RTree, which
      // also allows to completely disable caching.
      if (_actualViewport.Contains(value))
      {
        var width = _actualViewport.Width;
        var deltaWidth = (width - value.Width) / width;

        var height = _actualViewport.Height;
        var deltaHeight = (height - value.Height) / height;

        var deltaMax = Math.Max(deltaWidth, deltaHeight);

        if (deltaMax < _updateViewportItemsOnShrinkThreshold)
        {
          _viewport = value;
          return;
        }
      }

      var oldViewport = _actualViewport;
      UpdateViewportItems(oldViewport, value);
      _actualViewport = _viewport = value;
    }
  }

  public IReadOnlyCollection<T> ViewportItems => _viewportItems;

  public bool Add(T item)
  {
    if (item == null!)
    {
      return false;
    }

    var node = CreateLeaf(item, _boundarySelector(item));
    if (!_itemToNode.TryAdd(item, node))
    {
      return false;
    }

    InsertNode(node);
    return true;
  }

  public int AddRange(Span<T> items)
  {
    if (Count == 0 && items.Length > _maxEntriesPerNode)
    {
      return BulkInitialize(items);
    }

    var previousCount = _itemToNode.Count;
    for (var index = 0; index < items.Length; index++)
    {
      var item = items[index];

      if (item == null!)
      {
        continue;
      }

      var node = CreateLeaf(item, _boundarySelector(item));

      if (!_itemToNode.TryAdd(item, node))
      {
        continue;
      }

      InsertNode(node);
    }

    return _itemToNode.Count - previousCount;
  }

  public bool Remove(T item)
  {
    if (item == null! || !_itemToNode.TryGetValue(item, out var node))
    {
      return false;
    }

    if (node == Root)
    {
      _viewportItems.Remove(node.Data!);
      _itemToNode.Remove(item);

      var oldRoot = Root;
      Root = CreateNonLeaf();

      oldRoot.Reset();
      _recycledNonLeafNodes ??= [];
      _recycledNonLeafNodes.Add(oldRoot);
      return true;
    }

    if (!RemoveNode(node))
    {
      return false;
    }

    _itemToNode.Remove(item);

    if (Root is { Children.Count: 1, IsLeaf: false })
    {
      var oldRoot = Root;
      Root = Root.Children[0];
      Root.Parent = null;

      oldRoot.Reset();
      _recycledNonLeafNodes ??= [];
      _recycledNonLeafNodes.Add(oldRoot);
    }

    return true;
  }

  public void RemoveRange(IEnumerable<T> items)
  {
    ArgumentNullException.ThrowIfNull(items);
    foreach (var item in items)
    {
      if (item == null! || !_itemToNode.TryGetValue(item, out var node))
      {
        continue;
      }

      if (node == Root)
      {
        _viewportItems.Remove(node.Data!);
        _itemToNode.Remove(item);
        Root = CreateNonLeaf();
        continue;
      }

      if (!RemoveNode(node))
      {
        continue;
      }

      _itemToNode.Remove(item);

      if (Root is { Children.Count: 1, IsLeaf: false })
      {
        Root = Root.Children[0];
        Root.Parent = null;
      }
    }
  }

  public void Clear()
  {
    // Note that items removed via Clear are not recycled, to not give up O(1) complexity for caching old
    Root = CreateNonLeaf();
    _itemToNode.Clear();
    ResetViewportItems();
  }

  /// <summary>
  ///   Queries the specified boundary for intersecting or contained items.<br />
  ///   Items with empty boundaries are ignored and will not be part of the result set.<br />
  ///   The result will be populated into the <paramref name="targetCollection"/>, which is not cleared by this method.
  /// </summary>
  /// <param name="searchBoundary">
  ///   The boundary for which intersecting or contained items shall be added to the
  ///   <paramref name="targetCollection"/>.
  /// </param>
  /// <param name="targetCollection">
  ///   The target collection, to which the items of the query result are being added.<br />
  ///   The collection is not cleared by this method.
  /// </param>
  public int QueryTo(RTreeBoundary searchBoundary, ICollection<T> targetCollection)
  {
    ArgumentNullException.ThrowIfNull(targetCollection);

    if (!Root.Boundary.Intersects(searchBoundary))
    {
      return 0;
    }

    var count = 0;
    _queryStack.Push(Root);

    // The stack will only hold nodes, which are within the search boundary.
    while (_queryStack.Count > 0)
    {
      var current = _queryStack.Pop();
      if (current.IsLeaf)
      {
        targetCollection.Add(current.Data);
        count++;
        continue;
      }

      for (var index = 0; index < current.Children.Count; index++)
      {
        var child = current.Children[index];
        var childBoundary = child.Boundary;

        if (searchBoundary.Contains(childBoundary))
        {
          // In case of containment, add children directly without bound checks.
          var offset = _queryStack.Count;
          _queryStack.Push(child);

          while (_queryStack.Count > offset)
          {
            var contained = _queryStack.Pop();
            if (contained.IsLeaf)
            {
              targetCollection.Add(contained.Data);
              count++;
              continue;
            }

            for (var containedIndex = 0; containedIndex < contained.Children.Count; containedIndex++)
            {
              var containedChild = contained.Children[containedIndex];
              _queryStack.Push(containedChild);
            }
          }

          continue;
        }

        if (searchBoundary.Intersects(childBoundary))
        {
          _queryStack.Push(child);
        }
      }
    }

    return count;
  }

  public bool Contains(T item) => item != null! && _itemToNode.ContainsKey(item);

  public IEnumerator<T> GetEnumerator()
  {
    // ReSharper disable once ForeachCanBeConvertedToQueryUsingAnotherGetEnumerator
    foreach (var item in _itemToNode)
    {
      yield return item.Value.Data!;
    }
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  internal RTreeNode<T> ChooseInsertParent(RTreeBoundary itemBoundary)
  {
    // Note: Internal for tests
    var node = Root;
    if (node.RemainingCapacity > 0)
    {
      // If the root has capacity, always insert into it.
      return node;
    }

    var (centerX, centerY) = (itemBoundary.CenterX, itemBoundary.CenterY);

    // Iterate over all children of the current node, find the best one, then repeat using the selected node again.
    // Do so, until the best candidate for insert is found.
    var bestCandidate = node;
    var totalMinDistance = double.MaxValue;

    while (true)
    {
      if (node.IsLeaf)
      {
        break;
      }

      RTreeNode<T>? closestChild = null;
      var currentMinDistance = double.MaxValue;

      for (var index = 0; index < node.Children.Count; index++)
      {
        var child = node.Children[index];
        var boundary = child.Boundary;
        var (childX, childY) = (boundary.CenterX, boundary.CenterY);

        var diffX = centerX - childX;
        var diffY = centerY - childY;

        var distanceSquared = diffX * diffX + diffY * diffY;
        var fullness = child.IsLeaf ? _leafNodeVirtualFullness : (float)child.Children.Count / _maxEntriesPerNode;
        // The non-linear penalty factor for fullness of nodes below causes better distribution of leafs across
        // branches. If we would not take this into account, inserting new nodes at a similar location would
        // form single but very deep branches, as always just the nearest is selected and a new parent would be
        // inserted.
        var penalty = 1 + fullness * fullness;
        var weightedDistance = (distanceSquared + double.Epsilon) * penalty; // Epsilon: Handle 0 distance edge case

        if (weightedDistance < currentMinDistance)
        {
          currentMinDistance = weightedDistance;
          closestChild = child;
        }
      }

      if (closestChild == null)
      {
        Debug.Fail("It should always be possible to select a node for insertion, could only happen if a node without "
                   + "any children was found, which should be impossible for IsLeaf = false (except for Root, which"
                   + " also should not reach this.");
        continue;
      }

      if (closestChild.RemainingCapacity > 0)
      {
        // Insert early, don't iterate down the bottom.
        return closestChild;
      }

      if (currentMinDistance < totalMinDistance)
      {
        totalMinDistance = currentMinDistance;
        bestCandidate = closestChild;
      }

      node = closestChild;
    }

    return bestCandidate;
  }

  private void InsertNode(RTreeNode<T> item)
  {
    var targetNode = ChooseInsertParent(item.Boundary);
    if (targetNode.IsLeaf)
    {
      if (targetNode == Root)
      {
        targetNode = targetNode.InsertParentLayer(_maxEntriesPerNode);
        Root = targetNode;
      }
      else
      {
        var parent = targetNode.Parent!;
        if (parent.RemainingCapacity > 0)
        {
          targetNode = parent;
        }
        else
        {
          RebalanceBranch(parent);

          // After rebalancing, check for a more suitable candidate (e.g. a node which has only a few items)
          targetNode = ChooseInsertParent(item.Boundary);
          if (targetNode.IsLeaf)
          {
            // If a leaf is again returned, then most likely the same node had been returned and the parent of
            // it has still no capacity. In this case, fall back to inserting a direct parent layer.
            targetNode = targetNode.InsertParentLayer(_maxEntriesPerNode);
          }
        }
      }
    }
    else if (targetNode != Root)
    {
      Debug.Assert(
        targetNode.RemainingCapacity > 0,
        "ChooseInsertParent should have returned a node with capacity available.");
    }

    targetNode.AddChildDirect(item);

    if (!_actualViewport.IsEmpty)
    {
      AddNewIntersectingViewportItems(_actualViewport, item);
    }

    // Note: Classical RTree implementations would now check if the node is overfull, split it if necessary and
    // start to rebalance the tree. This is not necessary in this implementation, as unlike a classical RTree,
    // it can work with leaf nodes on multiple depth layers and inserts parent layers on demand.
    // Here, nodes can never become overfull.
  }

  private bool RemoveNode(RTreeNode<T> toRemove)
  {
    var parent = toRemove.Parent;
    if (parent == null)
    {
      Debug.Fail("This could only occur if Remove is called on a detached node or the root.");
      return false;
    }

    if (parent.IsLeaf)
    {
      Debug.Fail("A node holding other children must not be marked as a data leaf.");
      return false;
    }

    parent.RemoveChildDirect(toRemove);

    if (toRemove.IsLeaf)
    {
      if (toRemove.IsVisibleInViewport)
      {
        var actuallyRemoved = _viewportItems.Remove(toRemove.Data);
        Debug.Assert(actuallyRemoved);
      }

      parent = MoveChildrenToParentIfCapacityAvailable(parent);
      RemoveUnderfullFromAncestorNodes(parent);

      toRemove.Reset();
      _recycledLeafNodes ??= [];
      _recycledLeafNodes.Add(toRemove);

      return true;
    }

    if (parent.RemainingCapacity >= toRemove.Children.Count)
    {
      for (var index = 0; index < toRemove.Children.Count; index++)
      {
        var child = toRemove.Children[index];
        parent.AddChildDirect(child);
      }

      toRemove.Reset();
      _recycledNonLeafNodes ??= [];
      _recycledNonLeafNodes.Add(toRemove);

      return true;
    }

    for (var index = 0; index < toRemove.Children.Count; index++)
    {
      var child = toRemove.Children[index];
      if (parent.RemainingCapacity > 0)
      {
        parent.AddChildDirect(child);
      }
      else
      {
        InsertNode(child);
      }
    }

    toRemove.Reset();
    _recycledNonLeafNodes ??= [];
    _recycledNonLeafNodes.Add(toRemove);

    RemoveUnderfullFromAncestorNodes(parent);
    return true;
  }

  private void RemoveUnderfullFromAncestorNodes(RTreeNode<T> node)
  {
    var current = node;
    while (current is { IsUnderFull: true, Parent: { } parent })
    {
      Debug.Assert(!current.IsLeaf);
      Debug.Assert(!parent.IsLeaf);

      parent.RemoveChildDirect(current); // This could cause the parent to become underfull as well, hence the loop.

      if (parent.RemainingCapacity >= current.Children.Count)
      {
        for (var index = 0; index < current.Children.Count; index++)
        {
          var child = current.Children[index];
          parent.AddChildDirect(child);
        }
      }
      else
      {
        for (var index = 0; index < current.Children.Count; index++)
        {
          var child = current.Children[index];
          if (parent.RemainingCapacity > 0)
          {
            parent.AddChildDirect(child);
          }
          else
          {
            InsertNode(child);
          }
        }
      }

      current.Reset();
      _recycledNonLeafNodes ??= [];
      _recycledNonLeafNodes.Add(current);

      current = parent;
    }
  }

  private int BulkInitialize(Span<T> items)
  {
    if (items.Length == 0)
    {
      return 0;
    }

    var array = ArrayPool<RTreeNode<T>>.Shared.Rent(items.Length);

    try
    {
      var skipped = 0;
      for (var index = 0; index < items.Length; index++)
      {
        var item = items[index];
        if (item == null!)
        {
          skipped++;
          continue;
        }

        var node = CreateLeaf(item, _boundarySelector(item));

        if (!_itemToNode.TryAdd(item, node))
        {
          skipped++;
          continue;
        }

        array[index - skipped] = node;
      }

      Root = CreateNonLeaf();
      var length = items.Length - skipped;
      BuildSortTileRecursiveTree(Root, array.AsSpan()[..length]);

      return length;
    }
    finally
    {
      ArrayPool<RTreeNode<T>>.Shared.Return(array, true);
    }
  }

  private void RebalanceBranch(RTreeNode<T> branchRoot)
  {
    Debug.Assert(!branchRoot.IsLeaf, "Must not be called on data leafs.");

    // Only collect direct children — do NOT recurse into sub-branches.
    // Recursing would make each rebalance O(subtree size), causing O(n^2) total cost on sequential inserts.
    var originalChildren = branchRoot.Children;
    var originalLength = originalChildren.Count;
    var nodes = ArrayPool<RTreeNode<T>>.Shared.Rent(originalLength);

    var originalAsSpan = CollectionsMarshal.AsSpan(originalChildren);
    originalAsSpan.CopyTo(nodes);

    // For restoring the parent relationship after Reset().
    var parent = branchRoot.Parent;

    // Setting the property before Reset(), to notify the parent as well.
    // The correct number will be restored by BuildSortTileRecursiveTree().
    branchRoot.ChildrenVisibleInViewport = 0; 
    
    branchRoot.Reset();
    branchRoot.Parent = parent;

    BuildSortTileRecursiveTree(branchRoot, nodes);
  }

  private void BuildSortTileRecursiveTree(RTreeNode<T> root, Span<RTreeNode<T>> nodes)
  {
    Debug.Assert(!root.IsLeaf, "Must not be called on data leafs.");
    Debug.Assert(root.Children.Count == 0, "The passed root must be empty.");

    while (true)
    {
      if (nodes.Length + 1 <= _maxEntriesPerNode) // +1 to ensure at least one extra item can fit (for rebalance).
      {
        root.AddChildrenDirect(nodes);
        return;
      }

      // An attempt of explaining what is going on below with an example:
      // - E.g. 4000 source nodes
      // - E.g. max. 12 children per node allowed
      // - ceil(4000 / 12) = 334 parent nodes which we would need in total if the maximum capacity is utilized and if
      //   we would consider just one dimension.
      // - ceil(sqrt(334)) = 19 slices for better X/Y distribution.
      //   If data is arranged perfectly squared in an X/Y grid, this number would give us the amount of nodes
      //   making up one side of the square. It attempts to evenly distribute the nodes across two dimensions.
      // - ceil(4000 / 19) = 211 nodes per slice.
      // - Sort nodes based on their centers in X direction, allowing us to directly grab the previously calculated
      //   amount of nodes per slice from the sorted data:
      //   - Take nodes 0-210 (211 nodes)
      //     - Sort this subset by the center in Y direction
      //     - From the current slice (index 0-210), take the maximum amount of children per node (0-11)
      //       - Create a parent node and add the selected nodes as children. Remember this new parent node.
      //       - Repeat with the next 12 nodes (=maximum per node, 12-23, 24-35, etc.) until all nodes of the slice have
      //         been consumed. In our example: ceil(211 / 12) = 18 created parents for this slice.
      //         So the tree is built recursively from the ground up (hence also the name Sort-Tile-Recursive).
      //     - Repeat with nodes 211-421, then 422-633, etc. until all nodes have been consumed.
      //       In our example: ceil(4000 / 211) = 19 times
      //   - Check the amount of created parent nodes. In our example: 19 * 18 = 342.
      //     - Less than the maximum amount: create a root node, push them as children in there
      //     - Otherwise: Start over but use this time the created parent nodes to create parents for them

      double nodesLength = nodes.Length + 1; // Just to ensure at least one extra item can already fit (for rebalance).
      var parentNodeCount = (int)Math.Ceiling(nodesLength / _maxEntriesPerNode);
      var sliceCount = (int)Math.Ceiling(Math.Sqrt(parentNodeCount));
      var sliceSize = (int)Math.Ceiling(nodesLength / sliceCount);

      nodes.Sort(CompareByCenterX);
      var parentNodes = new List<RTreeNode<T>>(parentNodeCount);

      for (var nodeIndex = 0; nodeIndex < nodes.Length; nodeIndex += sliceSize)
      {
        var slice = nodes.Slice(nodeIndex, Math.Min(sliceSize, nodes.Length - nodeIndex));
        slice.Sort(CompareByCenterY);

        for (var sliceIndex = 0; sliceIndex < slice.Length; sliceIndex += _maxEntriesPerNode)
        {
          var sliceNodes = slice.Slice(sliceIndex, Math.Min(_maxEntriesPerNode, slice.Length - sliceIndex));
          var sliceParent = CreateNonLeaf();

          sliceParent.AddChildrenDirect(sliceNodes);
          parentNodes.Add(sliceParent);
        }
      }

      nodes = CollectionsMarshal.AsSpan(parentNodes);
    }
  }

  private void UpdateViewportItems(RTreeBoundary oldViewport, RTreeBoundary newViewport)
  {
    if (newViewport.IsEmpty)
    {
      if (!oldViewport.IsEmpty)
      {
        ResetViewportItems();
      }

      return;
    }

    if (oldViewport.IsEmpty)
    {
      AddNewIntersectingViewportItems(newViewport);
      return;
    }

    if (oldViewport.Contains(newViewport))
    {
      // Hot path for zooming in/out and panning.
      // No need to check for new items, but remove no longer contained viewport items.
      RemoveNoLongerIntersectingViewportItems(newViewport);
      return;
    }

    if (oldViewport.Intersects(newViewport))
    {
      // Hot path for intersections (very likely, e.g. on panning), remove items from the old viewport, which are no
      // longer part of it, then add items from the overall item list which are new.
      RemoveNoLongerIntersectingViewportItems(newViewport);
    }
    else
    {
      // At this point: the new viewport does not intersect with the old viewport at all,
      // all existing items can be cleared.
      ResetViewportItems();
    }

    AddNewIntersectingViewportItems(newViewport);
  }

  private void ResetViewportItems()
  {
    Debug.Assert(_queryStack.Count == 0);
    _queryStack.Push(Root);

    var markedForRemoval = 0;

    while (_queryStack.Count > 0)
    {
      var current = _queryStack.Pop();
      if (current.IsLeaf)
      {
        if (current.IsVisibleInViewport)
        {
          markedForRemoval++;
        }

        current.IsVisibleInViewport = false;
        continue;
      }

      if (current.ChildrenVisibleInViewport <= 0)
      {
        // No children have been in the viewport yet, hence no need to iterate them again.
        continue;
      }

      for (var index = 0; index < current.Children.Count; index++)
      {
        var child = current.Children[index];
        _queryStack.Push(child);
      }
    }

    Debug.Assert(_viewportItems.Count == markedForRemoval);
    _viewportItems.Clear();
  }

  private void RemoveNoLongerIntersectingViewportItems(RTreeBoundary viewport)
  {
    Debug.Assert(
      !viewport.IsEmpty,
      $"Should not be called if the new viewport is empty, instead {nameof(ResetViewportItems)} " +
      $"should have been called directly.");

    if (_viewport.IsEmpty)
    {
      Debug.Assert(_viewportItems.Count == 0);
      return;
    }

    if (_viewportItems.Count == 0)
    {
      return;
    }

    Debug.Assert(_queryStack.Count == 0);
    _queryStack.Push(Root);

    while (_queryStack.Count > 0)
    {
      var current = _queryStack.Pop();

      if (current.IsLeaf)
      {
        if (!current.IsVisibleInViewport)
        {
          // Not in viewport, nothing to remove.
          Debug.Assert(!_viewportItems.Contains(current.Data));
          continue;
        }

        if (current.Boundary.Intersects(viewport))
        {
          // Not yet part of viewport items, but as adding/removing is done in separate methods,
          // this node may just not yet have been added by the AddNewIntersectingViewportItems method.
          continue;
        }

        current.IsVisibleInViewport = false;
        var actuallyRemoved = _viewportItems.Remove(current.Data);

        Debug.Assert(actuallyRemoved);
        continue;
      }

      if (current.ChildrenVisibleInViewport <= 0)
      {
        // No need to iterate children if none of them are in the viewport.
        continue;
      }

      if (viewport.Contains(current.Boundary))
      {
        // All items in this subtree are still within the new viewport, nothing to remove.
        Debug.Assert(current.Children.Count == current.ChildrenVisibleInViewport);
        continue;
      }

      for (var index = 0; index < current.Children.Count; index++)
      {
        var child = current.Children[index];
        _queryStack.Push(child);
      }
    }
  }

  private void AddNewIntersectingViewportItems(RTreeBoundary viewport, RTreeNode<T>? startAtNode = null)
  {
    Debug.Assert(
      !viewport.IsEmpty,
      $"Should not be called if the new viewport is empty, instead {nameof(ResetViewportItems)} " +
      $"should have been called directly.");

    if (viewport.IsEmpty)
    {
      return;
    }

    Debug.Assert(_queryStack.Count == 0);
    _queryStack.Push(startAtNode ?? Root);

    while (_queryStack.Count > 0)
    {
      var current = _queryStack.Pop();

      if (current.IsLeaf)
      {
        var wasInViewport = current.IsVisibleInViewport;
        if (wasInViewport)
        {
          // Already part of the viewport.
          Debug.Assert(_viewportItems.Contains(current.Data));
          continue;
        }

        if (!current.Boundary.Intersects(viewport))
        {
          // Note that the item could still exist in the _viewportItems, which is valid,
          // e.g. due to viewport caching (see usage of _actualViewport/_viewport).
          continue;
        }

        current.IsVisibleInViewport = true;

        var actuallyAdded = _viewportItems.Add(current.Data);
        Debug.Assert(wasInViewport != actuallyAdded);

        continue;
      }

      // This is a non-leaf node.
      if (!viewport.Intersects(current.Boundary))
      {
        // Note that the item could still exist in the _viewportItems, which is valid,
        // e.g. due to viewport caching (see usage of _actualViewport/_viewport).
        continue;
      }

      // Iterate backwards for pushing to the stack, since this will result in the same order in _viewportItems as in
      // current.Children. There should not be any advantages regarding memory layout, but it is just a bit easier to
      // debug and compare items when viewed in the debugger.
      for (var index = current.Children.Count - 1; index >= 0; index--)
      {
        var child = current.Children[index];
        _queryStack.Push(child);
      }
    }
  }

  private void RecycleNode(RTreeNode<T> node)
  {
    node.Reset();
    if (node.IsLeaf)
    {
      _recycledLeafNodes ??= [];
      _recycledLeafNodes.Add(node);
      return;
    }

    _recycledNonLeafNodes ??= [];
    _recycledNonLeafNodes.Add(node);
  }

  private RTreeNode<T> CreateLeaf(T data, RTreeBoundary boundary)
  {
    if (_recycledLeafNodes == null || _recycledLeafNodes.Count == 0)
    {
      return RTreeNode<T>.CreateLeaf(data, boundary);
    }

    var index = _recycledLeafNodes.Count - 1;
    var node = _recycledLeafNodes[index];
    _recycledLeafNodes.RemoveAt(index);

    node.Data = data;
    node.Boundary = boundary;

    return node;
  }

  private RTreeNode<T> CreateNonLeaf()
  {
    if (_recycledNonLeafNodes == null || _recycledNonLeafNodes.Count == 0)
    {
      return RTreeNode<T>.CreateNonLeaf(_maxEntriesPerNode);
    }

    var index = _recycledNonLeafNodes.Count - 1;
    var node = _recycledNonLeafNodes[index];

    _recycledNonLeafNodes.RemoveAt(index);

    return node;
  }

  private RTreeNode<T> MoveChildrenToParentIfCapacityAvailable(RTreeNode<T> node)
  {
    Debug.Assert(!node.IsLeaf, "Must not be called on data leafs.");

    var parent = node.Parent;
    if (parent == null)
    {
      return node;
    }

    if (parent.RemainingCapacity < node.Children.Count - 1) // -1: For the node which moves its children up
    {
      return node;
    }

    parent.RemoveChildDirect(node);

    for (var index = 0; index < node.Children.Count; index++)
    {
      var child = node.Children[index];
      parent.AddChildDirect(child);
    }

    node.Reset();
    _recycledNonLeafNodes ??= [];
    _recycledNonLeafNodes.Add(node);

    return parent;
  }
}