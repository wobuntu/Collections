using Wobuntu.Collections.Observable;
using System;
using System.Buffers;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

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
///   Though the concept remains the same, this RTree implementation differs from classical implementations by
///   optimizing on rectangle center distances over rectangle sizes and builds its tree not recursively, but
///   iteratively.<br />
///   Use this data structure to organize spatial data, which does not overlap heavily, is unevenly distributed,
///   and varies in geometric size.<br />
///   The tree is constructed bottom-up, starting at the data leafs. Initially, a data leaf is created for each
///   object, which also holds a boundary describing a rectangle in a geometric space. The boundary is used initially
///   to sort the nodes according to their X/Y coordinates. Based on a limit of maximum children of a parent node and
///   the geometric closeness of nodes, they are organized together in further parent nodes, describing rectangles
///   enclosing their children. This is repeated until all nodes are contained in a common root, from which each
///   object can be reached.
/// </summary>
/// <typeparam name="T">The type of data to contain within this tree.</typeparam>
public class RTree<T>
  : ICollection<T>
  where T : notnull
{
  // ReSharper disable once CommentTypo
  // We use stackalloc for int / float arrays, hence the division by 4. The stack buffer should usually
  // be 1MB on windows (https://learn.microsoft.com/en-us/windows/win32/procthread/thread-stack-size),
  // 8MB on linux ("ulimit -s"), less on WASM. Seems like dotnet uses similar sizes in the BCL, hence
  // trying to be conservative here and assuming 512 bytes to be OK.
  private const int StackAllocLimit = 512 / 4;

  private readonly Dictionary<T, int> _itemToNodeIndex;
  private readonly Stack<int> _queryStack;

  private readonly Func<T, RTreeBoundary> _boundarySelector;

  private readonly double _updateViewportItemsOnShrinkThreshold;
  private readonly double _leafNodeVirtualFullness;

  private readonly SynchronizedObservableOrderedSet<T> _viewportItems;
  private readonly HashSet<T> _viewportUpdateCache;

  private int _nodeCount;

  private int _childReferencesCount;
  private int _freeChildBlockHead = RTreeNode<T>.NullIndex;

  private RTreeBoundary _viewport;
  private RTreeBoundary _actualViewport;

  internal int RootIndex; // Internal for tests.
  internal readonly int MaxEntriesPerNode;
  internal readonly int MinEntriesPerNode;
  internal int FreeNodeHead = RTreeNode<T>.NullIndex;
  internal RTreeNode<T>[] Nodes;
  internal RTreeNodeReference[] ChildReferences;

  public RTree(Func<T, RTreeBoundary> boundarySelector, RTreeOptions? options = null)
    : this(0, options ?? new RTreeOptions(), boundarySelector) { }

  public RTree(int capacity, Func<T, RTreeBoundary> boundarySelector, RTreeOptions? options = null)
    : this(capacity, options ?? new RTreeOptions(), boundarySelector) { }

  public RTree(ReadOnlySpan<T> items, Func<T, RTreeBoundary> boundarySelector, RTreeOptions? options = null)
    : this(items.Length, options ?? new RTreeOptions(), boundarySelector)
  {
    BulkInitialize(items, items.Length);
  }

  public RTree(int capacity, ReadOnlySpan<T> items, Func<T, RTreeBoundary> boundarySelector, RTreeOptions? options = null)
    : this(Math.Max(capacity, items.Length), options ?? new RTreeOptions(), boundarySelector)
  {
    BulkInitialize(items, items.Length);
  }

  private RTree(int capacity, RTreeOptions options, Func<T, RTreeBoundary> boundarySelector)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 0);
    ArgumentNullException.ThrowIfNull(boundarySelector);

    _boundarySelector = boundarySelector;
    MaxEntriesPerNode = options.MaxEntriesPerNode;
    MinEntriesPerNode = options.MinEntriesPerNode;
    _updateViewportItemsOnShrinkThreshold = options.UpdateViewportItemsOnShrinkThreshold;

    // The following will choose a virtual fullness of leaf nodes, somewhere between the minimum and maximum amount
    // of nodes. This will make choosing a node for dynamic insert (= not during bulk initialize) fairer. If we treated
    // leaf nodes as being empty, they would always be picked if on the same level as non-leaf nodes for insert, which
    // would cause massive overlaps of RTreeNodes.
    _leafNodeVirtualFullness = (MaxEntriesPerNode - MinEntriesPerNode) / (double)MaxEntriesPerNode;

    // Capacities for the following ensured in EnsureCapacity.
    _itemToNodeIndex = new Dictionary<T, int>();
    _queryStack = new Stack<int>(options.InitialQueryStackCapacity);
    _viewportItems = new SynchronizedObservableOrderedSet<T>(options.InitialViewportItemsCapacity);
    _viewportUpdateCache = new HashSet<T>(options.InitialViewportItemsCapacity);

    EnsureCapacity(capacity);
    Debug.Assert(Nodes.Length > 0, "Must be bigger 0, even at capacity 0 to account for the root.");

    RootIndex = RTreeNode<T>.AllocateNonLeaf(this).OwnIndex;
  }

  public int Count => _itemToNodeIndex.Count;

  public RTreeBoundary Boundary => Nodes[RootIndex].Boundary;

  public RTreeBoundary Viewport
  {
    get => _viewport;
    set
    {
      if (_viewport.Equals(in value))
      {
        return;
      }

      if (value.IsEmpty && !_actualViewport.IsEmpty)
      {
        _viewportItems.Clear();
        _actualViewport = new RTreeBoundary();
        _viewport = value;
        return;
      }

      // If the new viewport is contained in the current viewport, then we can just keep the cached
      // data as is. This means the viewport items may contain additional elements, which are currently
      // outside the viewport, however, this should be beneficial for performance during zooming and panning.
      // The threshold for an actual resize can be influenced by the options passed to the RTree, which
      // also allows to completely disable caching.
      if (_actualViewport.Contains(in value))
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

  [MemberNotNull(
    nameof(Nodes),
    nameof(ChildReferences))]
  public void EnsureCapacity(int capacity)
  {
    ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 0);

    var (estimatedNodeCount, estimatedChildBlockCount) = EstimateCapacities(capacity, MaxEntriesPerNode);
    if (Nodes == null!)
    {
      Nodes = new RTreeNode<T>[estimatedNodeCount];
    }
    else if (estimatedNodeCount > Nodes.Length)
    {
      Array.Resize(ref Nodes, estimatedNodeCount);
    }

    if (ChildReferences == null!)
    {
      ChildReferences = new RTreeNodeReference[estimatedChildBlockCount * MaxEntriesPerNode];
    }
    else if (estimatedChildBlockCount > ChildReferences.Length / MaxEntriesPerNode)
    {
      Array.Resize(ref ChildReferences, estimatedChildBlockCount * MaxEntriesPerNode);
    }

    _itemToNodeIndex.EnsureCapacity(capacity);

    if (!_actualViewport.IsEmpty)
    {
      _viewportItems.EnsureCapacity(capacity);
    }
  }

  public bool Add(T item)
  {
    if (item == null!)
    {
      return false;
    }

    var boundary = _boundarySelector(item);
    ref var node = ref RTreeNode<T>.AllocateLeaf(this, item, boundary);

    if (!_itemToNodeIndex.TryAdd(item, node.OwnIndex))
    {
      RTreeNode<T>.Free(this, ref node);
      return false;
    }

    InsertNode(ref node);

    if (_actualViewport.Intersects(in node.Boundary))
    {
      var actuallyAdded = _viewportItems.Add(item);
      Debug.Assert(actuallyAdded);
    }

    return true;
  }

  public int AddRange(ReadOnlySpan<T> items)
  {
    if (items.Length == 0)
    {
      return 0;
    }

    if (Count == 0 && items.Length > MaxEntriesPerNode)
    {
      var result = BulkInitialize(items, items.Length);

      if (!_actualViewport.IsEmpty
          && _actualViewport.IntersectsUnchecked(in Nodes[RootIndex].Boundary))
      {
        // Not directly querying to _viewPortItems to avoid unnecessary locks/collection changed events.
        Debug.Assert(_viewportUpdateCache.Count == 0);
        QueryTo(in _actualViewport, _viewportUpdateCache);
        _viewportItems.AddRange(_viewportUpdateCache);
        _viewportUpdateCache.Clear();
      }

      return result;
    }

    var shouldCheckViewportItems = !_actualViewport.IsEmpty;
    var addedBoundary = new RTreeBoundary();
    var previousCount = _itemToNodeIndex.Count;

    for (var index = 0; index < items.Length; index++)
    {
      var item = items[index];

      if (item == null!)
      {
        continue;
      }

      var boundary = _boundarySelector(item);
      ref var node = ref RTreeNode<T>.AllocateLeaf(this, item, boundary);

      if (!_itemToNodeIndex.TryAdd(item, node.OwnIndex))
      {
        RTreeNode<T>.Free(this, ref node);
        continue;
      }

      InsertNode(ref node);

      if (shouldCheckViewportItems)
      {
        addedBoundary = addedBoundary.Union(in node.Boundary);
      }
    }

    if (!addedBoundary.IsEmpty)
    {
      Debug.Assert(_viewportUpdateCache.Count == 0);
      addedBoundary = _actualViewport.Intersect(in addedBoundary);
      QueryTo(in addedBoundary, _viewportUpdateCache);
      _viewportItems.AddRange(_viewportUpdateCache);
      _viewportUpdateCache.Clear();
    }

    return _itemToNodeIndex.Count - previousCount;
  }

  public bool Remove(T item)
  {
    if (item == null! || !_itemToNodeIndex.TryGetValue(item, out var nodeIndex))
    {
      return false;
    }

    if (nodeIndex == RootIndex)
    {
      ref var oldRoot = ref Nodes[nodeIndex];
      _viewportItems.Remove(oldRoot.Data!);
      _itemToNodeIndex.Remove(item);
      RTreeNode<T>.Free(this, ref oldRoot);

      ref readonly var newRoot = ref RTreeNode<T>.AllocateNonLeaf(this);
      RootIndex = newRoot.OwnIndex;

      return true;
    }

    if (!_actualViewport.IsEmpty)
    {
      ref readonly var node = ref Nodes[nodeIndex];
      if (_actualViewport.IntersectsUnchecked(in node.Boundary))
      {
        var actuallyRemoved = _viewportItems.Remove(item);
        Debug.Assert(actuallyRemoved || node.Boundary.IsEmpty);
      }
    }

    if (!RemoveNode(nodeIndex))
    {
      return false;
    }

    _itemToNodeIndex.Remove(item);

    ref var oldRootNode = ref Nodes[RootIndex];
    if (oldRootNode.ChildrenCount != 1)
    {
      return true;
    }

    // Remove the current root layer and replace it by its child.
    ref readonly var newRootReference = ref ChildReferences[oldRootNode.FirstChildReferenceIndex];
    RootIndex = newRootReference.NodeIndex;

    ref var newRootNode = ref Nodes[RootIndex];
    newRootNode.ParentIndex = RTreeNode<T>.NullIndex;

    RTreeNode<T>.Free(this, ref oldRootNode);
    return true;
  }

  public void RemoveRange(IEnumerable<T> items)
  {
    ArgumentNullException.ThrowIfNull(items);

    var shouldCheckViewportItems = !_actualViewport.IsEmpty;
    if (shouldCheckViewportItems)
    {
      if (items is not IList<T>
          or not IReadOnlyList<T>
          or not IList
          or not ICollection<T>
          or not IReadOnlyCollection<T>
          or not ICollection)
      {
        // Avoid duplicate enumerable evaluation for deleting items from the viewport (see end).
        // We do this separately, because this will be faster on the observable ordered set than using
        // single calls, especially due to reduced collection changed events on the consumer side.
        items = items.ToArray();
      }
    }

    var removalBoundary = new RTreeBoundary();

    // ReSharper disable once PossibleMultipleEnumeration : Intended, see comment above.
    foreach (var item in items)
    {
      if (item == null! || !_itemToNodeIndex.TryGetValue(item, out var nodeIndex))
      {
        continue;
      }

      if (nodeIndex == RootIndex)
      {
        ref var node = ref Nodes[nodeIndex];
        Debug.Assert(node.IsLeaf);

        _viewportItems.Remove(node.Data!);
        _itemToNodeIndex.Remove(item);
        RTreeNode<T>.Free(this, ref node);

        RootIndex = RTreeNode<T>.AllocateNonLeaf(this).OwnIndex;

        Debug.Assert(_itemToNodeIndex.Count == 0, "If the root is a data leaf, then no more data can exist.");
        continue;
      }

      if (shouldCheckViewportItems)
      {
        ref readonly var node = ref Nodes[nodeIndex];
        removalBoundary = removalBoundary.Union(in node.Boundary);
      }

      if (!RemoveNode(nodeIndex))
      {
        continue;
      }

      _itemToNodeIndex.Remove(item);

      ref var oldRoot = ref Nodes[RootIndex];
      if (oldRoot.ChildrenCount != 1)
      {
        continue;
      }

      ref readonly var newRootReference = ref ChildReferences[oldRoot.FirstChildReferenceIndex];
      RootIndex = newRootReference.NodeIndex;
      ref var newRoot = ref Nodes[RootIndex];
      newRoot.ParentIndex = RTreeNode<T>.NullIndex;

      RTreeNode<T>.Free(this, ref oldRoot);
    }

    if (!removalBoundary.IsEmpty)
    {
      // ReSharper disable once PossibleMultipleEnumeration : Intended, see comment above.
      _viewportItems.RemoveRange(items);
    }
  }

  public void Clear()
  {
    _viewportItems.Clear();

    _nodeCount = 0;
    _childReferencesCount = 0;
    FreeNodeHead = RTreeNode<T>.NullIndex;
    _freeChildBlockHead = RTreeNode<T>.NullIndex;

    RootIndex = RTreeNode<T>.AllocateNonLeaf(this).OwnIndex;
    _itemToNodeIndex.Clear();
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
  public int QueryTo(in RTreeBoundary searchBoundary, ICollection<T> targetCollection)
  {
    ArgumentNullException.ThrowIfNull(targetCollection);

    ref readonly var root = ref Nodes[RootIndex];
    if (!root.Boundary.Intersects(in searchBoundary))
    {
      return 0;
    }

    var count = 0;
    _queryStack.Push(RootIndex);

    // The stack will only hold node indices, which are within the search boundary.
    while (_queryStack.Count > 0)
    {
      var currentIndex = _queryStack.Pop();
      ref readonly var current = ref Nodes[currentIndex];

      if (current.IsLeaf)
      {
        targetCollection.Add(current.Data!);
        count++;
        continue;
      }

      var childReferencesOffset = current.FirstChildReferenceIndex;
      var childrenCount = current.ChildrenCount;

      for (var index = 0; index < childrenCount; index++)
      {
        ref readonly var childReference = ref ChildReferences[childReferencesOffset + index];

        if (searchBoundary.ContainsUnchecked(in childReference.Boundary))
        {
          // In case of containment, add children directly without bound checks.
          var offset = _queryStack.Count;
          _queryStack.Push(childReference.NodeIndex);

          while (_queryStack.Count > offset)
          {
            var containedIndex = _queryStack.Pop();
            ref readonly var contained = ref Nodes[containedIndex];

            if (contained.IsLeaf)
            {
              targetCollection.Add(contained.Data);
              count++;
              continue;
            }

            var containedChildReferencesOffset = contained.FirstChildReferenceIndex;
            var containedChildrenCount = contained.ChildrenCount;

            for (var childIndex = 0; childIndex < containedChildrenCount; childIndex++)
            {
              var childNodeIndex = ChildReferences[containedChildReferencesOffset + childIndex].NodeIndex;
              _queryStack.Push(childNodeIndex);
            }
          }

          continue;
        }

        if (searchBoundary.IntersectsUnchecked(in childReference.Boundary))
        {
          _queryStack.Push(childReference.NodeIndex);
        }
      }
    }

    return count;
  }

  public bool Contains(T item) => item != null! && _itemToNodeIndex.ContainsKey(item);

  public void CopyTo(T[] array, int arrayIndex)
  {
    ArgumentNullException.ThrowIfNull(array);
    ArgumentOutOfRangeException.ThrowIfNegative(arrayIndex);

    var available = array.Length - arrayIndex;
    if (available < Count)
    {
      throw new ArgumentException(
        "The target array is not large enough to hold all items starting at the given index.",
        nameof(array));
    }

    foreach (var (_, nodeIndex) in _itemToNodeIndex)
    {
      array[arrayIndex++] = Nodes[nodeIndex].Data!;
    }
  }

  public IEnumerator<T> GetEnumerator()
  {
    foreach (var (_, index) in _itemToNodeIndex)
    {
      yield return Nodes[index].Data!;
    }
  }

  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
  void ICollection<T>.Add(T item) => Add(item);
  bool ICollection<T>.IsReadOnly => false;

  internal int ChooseInsertParent(RTreeBoundary itemBoundary)
  {
    // Note: Method internal for tests
    var nodeIndex = RootIndex;
    ref var node = ref Nodes[nodeIndex];

    if (node.HasRemainingCapacity(MaxEntriesPerNode))
    {
      // If the root has capacity, always insert into it.
      return nodeIndex;
    }

    var (centerX, centerY) = (itemBoundary.CenterX, itemBoundary.CenterY);

    // Iterate over all children of the current node, find the best one, then repeat using the selected node again.
    // Do so, until the best candidate for insert is found.
    var bestCandidateIndex = nodeIndex;
    var totalMinDistance = double.MaxValue;

    while (true)
    {
      ref readonly var current = ref Nodes[nodeIndex];
      if (current.IsLeaf)
      {
        break;
      }

      var childrenCount = current.ChildrenCount;
      var closestChildIndex = RTreeNode<T>.NullIndex;
      var currentMinDistance = double.MaxValue;
      var childReferenceOffset = current.FirstChildReferenceIndex;

      for (var index = 0; index < childrenCount; index++)
      {
        // Read boundary from the cached child references, avoids chasing into _nodes for each
        // child and causing page misses.
        ref readonly var childReference = ref ChildReferences[childReferenceOffset + index];
        ref readonly var boundary = ref childReference.Boundary;

        var (childX, childY) = (boundary.CenterX, boundary.CenterY);
        var diffX = centerX - childX;
        var diffY = centerY - childY;

        var distanceSquared = diffX * diffX + diffY * diffY;

        ref readonly var child = ref Nodes[childReference.NodeIndex];
        var fullness = child.IsLeaf ? _leafNodeVirtualFullness : (float)child.ChildrenCount / MaxEntriesPerNode;
        // The non-linear penalty factor for fullness of nodes below causes better distribution of leafs across
        // branches. If we would not take this into account, inserting new nodes at a similar location would
        // form single but very deep branches, as always just the nearest is selected and a new parent would be
        // inserted.
        var penalty = 1 + fullness * fullness;
        var weightedDistance = (distanceSquared + double.Epsilon) * penalty; // Epsilon: Handle 0 distance edge case

        if (weightedDistance < currentMinDistance)
        {
          currentMinDistance = weightedDistance;
          closestChildIndex = childReference.NodeIndex;
        }
      }

      if (closestChildIndex == RTreeNode<T>.NullIndex)
      {
        Debug.Fail("It should always be possible to select a node for insertion. This could only happen if a node "
                   + "without any children was found, which should be impossible for non-leaf nodes"
                   + "(except for Root, which also should not reach this.");
        continue;
      }

      if (Nodes[closestChildIndex].HasRemainingCapacity(MaxEntriesPerNode))
      {
        // Insert early, don't iterate down the bottom.
        return closestChildIndex;
      }

      if (currentMinDistance < totalMinDistance)
      {
        totalMinDistance = currentMinDistance;
        bestCandidateIndex = closestChildIndex;
      }

      nodeIndex = closestChildIndex;
    }

    return bestCandidateIndex;
  }

  private void InsertNode(ref RTreeNode<T> node)
  {
    var nodeIndex = node.OwnIndex;
    var targetIndex = ChooseInsertParent(node.Boundary);
    ref var targetNode = ref Nodes[targetIndex];

    if (targetNode.IsLeaf)
    {
      if (targetIndex == RootIndex)
      {
        targetNode = ref RTreeNode<T>.InsertParentLayer(this, ref targetNode);
        targetIndex = targetNode.OwnIndex;
        RootIndex = targetIndex;
      }
      else
      {
        var targetParentIndex = targetNode.ParentIndex;
        Debug.Assert(targetParentIndex >= 0);

        ref var targetParentNode = ref Nodes[targetParentIndex];
        if (targetParentNode.HasRemainingCapacity(MaxEntriesPerNode))
        {
          // The parent of the selected insert-leaf has space available, so set that node as the target for insert.
          targetIndex = targetParentIndex;
          targetNode = ref Nodes[targetIndex];
        }
        else
        {
          RTreeNode<T>.RebalanceBranch(this, ref targetParentNode);

          // After rebalancing, check for a more suitable candidate (e.g. a node which has only a few items)
          targetIndex = ChooseInsertParent(node.Boundary);
          targetNode = ref Nodes[targetIndex];

          if (targetNode.IsLeaf)
          {
            // If a leaf is again returned, then most likely the same node had been returned and the parent of
            // it has still no capacity. In this case, fall back to inserting a direct parent layer.
            targetNode = ref RTreeNode<T>.InsertParentLayer(this, ref targetNode);
          }
        }
      }
    }
    else if (targetIndex != RootIndex)
    {
      Debug.Assert(
        targetNode.HasRemainingCapacity(MaxEntriesPerNode),
        "ChooseInsertParent should have returned a node with capacity.");
    }

    // Allocations of nodes may have resized the underlying array, hence the node may point to an outdated reference.
    // Hence, refetch the node to insert, which may no longer be valid.
    node = ref Nodes[nodeIndex];
    targetNode.AddChildDirect(this, ref node);

    // Note: Classical RTree implementations would now check if the node is overfull, split it if necessary and
    // start to rebalance the tree. This is not necessary in this implementation, as unlike a classical RTree,
    // it can work with leaf nodes on multiple depth layers and inserts parent layers on demand.
    // Here, nodes can never become overfull.
  }

  private bool RemoveNode(int toRemoveIndex)
  {
    ref var toRemove = ref Nodes[toRemoveIndex];
    var parentIndex = toRemove.ParentIndex;

    if (parentIndex < 0)
    {
      Debug.Fail("This could only occur if Remove is called on a detached node or the root.");
      return false;
    }

    ref var parent = ref Nodes[parentIndex];
    parent.RemoveChildDirect(this, toRemoveIndex);

    if (toRemove.IsLeaf)
    {
      parentIndex = MoveChildrenToParentIfCapacityAvailable(parentIndex);
      RemoveUnderfullFromAncestorNodes(parentIndex);

      RTreeNode<T>.Free(this, ref toRemove);
      return true;
    }

    // The array may have been resized during reinserting children, hence refetch the possibly outdated reference.
    parent = ref Nodes[parentIndex];
    var childCount = toRemove.ChildrenCount;

    // Removing a non-leaf: re-attach its children:
    if (parent.GetRemainingCapacity(MaxEntriesPerNode) >= childCount)
    {
      // Parent has enough space to adopt our children, populate it directly:
      for (var index = 0; index < childCount; index++)
      {
        var childReferenceIndex = toRemove.FirstChildReferenceIndex + index;
        var childNodeIndex = ChildReferences[childReferenceIndex].NodeIndex;

        ref var child = ref Nodes[childNodeIndex];
        parent.AddChildDirect(this, ref child);
      }

      toRemove.ChildrenCount = 0; // Reset before freeing to avoid double-detach
      RTreeNode<T>.Free(this, ref toRemove);
      return true;
    }

    // Not enough space in parent available, add what fits, re-insert the rest.
    // Store the child offset here, as the reference to toRemove could become invalid on Array resize for InsertNode.
    var childReferenceOffset = toRemove.FirstChildReferenceIndex;
    for (var index = 0; index < childCount; index++)
    {
      var childIndex = ChildReferences[childReferenceOffset + index].NodeIndex;
      ref var child = ref Nodes[childIndex];

      if (parent.HasRemainingCapacity(MaxEntriesPerNode))
      {
        parent.AddChildDirect(this, ref child);
      }
      else
      {
        InsertNode(ref child);
        // InsertNode could reallocate the array and invalidate existing node references, hence refetch here.
        toRemove = ref Nodes[toRemoveIndex];
        parent = ref Nodes[parentIndex];
      }
    }

    toRemove.ChildrenCount = 0; // Reset before freeing to avoid double-detach
    RTreeNode<T>.Free(this, ref toRemove);
    RemoveUnderfullFromAncestorNodes(parentIndex);

    return true;
  }

  private void RemoveUnderfullFromAncestorNodes(int nodeIndex)
  {
    var currentIndex = nodeIndex;
    while (currentIndex >= 0)
    {
      ref var current = ref Nodes[currentIndex];
      if (current.IsLeaf || current.ParentIndex < 0)
      {
        break;
      }

      if (current.ChildrenCount >= MinEntriesPerNode)
      {
        break;
      }

      var parentIndex = current.ParentIndex;

      ref var parent = ref Nodes[parentIndex];
      parent.RemoveChildDirect(this, currentIndex);

      var childReferenceOffset = current.FirstChildReferenceIndex;
      var childCount = current.ChildrenCount;

      if (Nodes[parentIndex].GetRemainingCapacity(MaxEntriesPerNode) >= childCount)
      {
        for (var index = 0; index < childCount; index++)
        {
          var childIndex = ChildReferences[childReferenceOffset + index].NodeIndex;
          ref var child = ref Nodes[childIndex];
          parent.AddChildDirect(this, ref child);
        }
      }
      else
      {
        for (var index = 0; index < childCount; index++)
        {
          var childIndex = ChildReferences[childReferenceOffset + index].NodeIndex;
          ref var child = ref Nodes[childIndex];

          if (Nodes[parentIndex].HasRemainingCapacity(MaxEntriesPerNode))
          {
            parent.AddChildDirect(this, ref child);
          }
          else
          {
            InsertNode(ref child);
            // InsertNode could reallocate the array and invalidate existing node references, hence refetch here.
            current = ref Nodes[currentIndex];
            parent = ref Nodes[parentIndex];
          }
        }
      }

      current.ChildrenCount = 0; // Reset before freeing to avoid double-detach
      RTreeNode<T>.Free(this, ref current);
      currentIndex = parentIndex;
    }
  }

  private unsafe int BulkInitialize(ReadOnlySpan<T> items, int capacity)
  {
    if (items.Length == 0)
    {
      return 0;
    }

    int[]? rented = null;
    Span<int> indices;

    if (items.Length > StackAllocLimit)
    {
      rented = ArrayPool<int>.Shared.Rent(items.Length);
      indices = rented.AsSpan(0, items.Length);
    }
    else
    {
#pragma warning disable CS9081
      // Disable "A result of a stackalloc expression of this type in this context may be exposed outside the containing method"
      // The stack allocated span is used for sorting only (Stack only grows, stack frame is not dropped).
      indices = stackalloc int[items.Length];
#pragma warning restore CS9081
    }

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

        ref var node = ref RTreeNode<T>.AllocateLeaf(this, item, _boundarySelector(item));

        if (!_itemToNodeIndex.TryAdd(item, node.OwnIndex))
        {
          RTreeNode<T>.Free(this, ref node);
          skipped++;
          continue;
        }

        indices[index - skipped] = node.OwnIndex;
      }

      ref readonly var root = ref RTreeNode<T>.AllocateNonLeaf(this);
      RootIndex = root.OwnIndex;

      var length = items.Length - skipped;
      BuildSortTileRecursiveTree(RootIndex, indices[..length], capacity);

      return length;
    }
    finally
    {
      if (rented != null)
      {
        ArrayPool<int>.Shared.Return(rented, true);
      }
    }
  }

  internal unsafe void BuildSortTileRecursiveTree(int rootIndex, Span<int> nodeIndices, int capacity)
  {
    Debug.Assert(!Nodes[rootIndex].IsLeaf, "Must not be called on leaf nodes.");
    Debug.Assert(Nodes[rootIndex].ChildrenCount == 0, "The passed root must be empty.");

    EnsureCapacity(Math.Max(nodeIndices.Length, capacity));

    // When we are building the tree, we will build it bottom up. This means we are creating parent nodes
    // for slices of the leaf-nodes, store their indices, and consume them during the next iteration to
    // create a further set of parent nodes for them, and so on.
    // This means two things:
    // - We need the create and collect nodes during one iteration, but consume them in the next iteration.
    // - The amount of nodes to consider will shrink with each iteration.
    // To efficiently store and handle this, we can allocate an array being big enough, to fit the nodes
    // of two iterations, then toggle the start offset during each round to use the lower/upper part.
    // The calculation here is the same as in the loop below.
    double leafCount = nodeIndices.Length + 1;

    var minParents = (int)Math.Ceiling(leafCount / MaxEntriesPerNode);
    var leafSlices = (int)Math.Ceiling(Math.Sqrt(minParents));
    var maxLeafsPerSlice = (int)Math.Ceiling(leafCount / leafSlices);
    var maxParentsPerLeafSlice = (int)Math.Ceiling((double)maxLeafsPerSlice / MaxEntriesPerNode);
    var maxLeafParents = leafSlices * maxParentsPerLeafSlice;

    var minGrandParents = (int)Math.Ceiling((double)maxLeafParents / MaxEntriesPerNode);
    var parentSlices = (int)Math.Ceiling(Math.Sqrt(minGrandParents));
    var maxParentsPerSlice = (int)Math.Ceiling((double)maxLeafParents / parentSlices);
    var maxGrandParentsPerParentSlice = (int)Math.Ceiling((double)maxParentsPerSlice / MaxEntriesPerNode);
    var maxLeafGrandparents = parentSlices * maxGrandParentsPerParentSlice;

    var bufferSize = maxLeafParents + maxLeafGrandparents;

    int[]? rented = null;
    Span<int> buffer;

    if (bufferSize > StackAllocLimit)
    {
      rented = ArrayPool<int>.Shared.Rent(bufferSize);
      buffer = rented.AsSpan(0, bufferSize);
    }
    else
    {
#pragma warning disable CS9081
      // Disable "A result of a stackalloc expression of this type in this context may be exposed outside the containing method"
      buffer = stackalloc int[bufferSize];
#pragma warning restore CS9081
    }

    var offsetFlag = 1;

    while (true)
    {
      // +1 to ensure at least one extra item can fit (for rebalance and less re-allocation).
      if (nodeIndices.Length + 1 <= MaxEntriesPerNode)
      {
        ref var root = ref Nodes[rootIndex];
        root.AddChildrenDirect(this, nodeIndices);
        break;
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

      double nodesLength = nodeIndices.Length + 1; // Just to ensure at least one extra item can already fit (for rebalance).
      var parentNodeCount = (int)Math.Ceiling(nodesLength / MaxEntriesPerNode);
      var sliceCount = (int)Math.Ceiling(Math.Sqrt(parentNodeCount));
      var sliceSize = (int)Math.Ceiling(nodesLength / sliceCount);

      SortIndicesByCenter(nodeIndices, true);

      var offsetMask = offsetFlag - 1;
      var offset = offsetMask & maxLeafParents; // Address 0 or second part of the buffer.
      var bufferIndex = offset;

      for (var nodeIndex = 0; nodeIndex < nodeIndices.Length; nodeIndex += sliceSize)
      {
        var slice = nodeIndices.Slice(nodeIndex, Math.Min(sliceSize, nodeIndices.Length - nodeIndex));
        SortIndicesByCenter(slice, false); // Sort slice by CenterY

        for (var sliceIndex = 0; sliceIndex < slice.Length; sliceIndex += MaxEntriesPerNode)
        {
          var sliceNodes = slice.Slice(sliceIndex, Math.Min(MaxEntriesPerNode, slice.Length - sliceIndex));
          ref var sliceParent = ref RTreeNode<T>.AllocateNonLeaf(this);

          sliceParent.AddChildrenDirect(this, sliceNodes);
          buffer[bufferIndex++] = sliceParent.OwnIndex;
        }
      }

      nodeIndices = buffer.Slice(offset, bufferIndex - offset);
      offsetFlag ^= 1;
    }

    if (rented != null)
    {
      ArrayPool<int>.Shared.Return(rented, true);
    }
  }

  private unsafe void SortIndicesByCenter(Span<int> indices, bool byX)
  {
    // Use a keys array for Span.Sort(keys, items) — sort keys, items follow along
    float[]? rented = null;
    Span<float> keys;

    if (indices.Length > StackAllocLimit)
    {
      rented = ArrayPool<float>.Shared.Rent(indices.Length);
      keys = rented.AsSpan(0, indices.Length);
    }
    else
    {
#pragma warning disable CS9081
      // Disable "A result of a stackalloc expression of this type in this context may be exposed outside the containing method"
      // The stack allocated span is used for sorting only (Stack only grows, stack frame is not dropped).
      keys = stackalloc float[indices.Length];
#pragma warning restore CS9081
    }

    if (byX)
    {
      for (var index = 0; index < indices.Length; index++)
      {
        keys[index] = Nodes[indices[index]].Boundary.CenterX;
      }
    }
    else
    {
      for (var index = 0; index < indices.Length; index++)
      {
        keys[index] = Nodes[indices[index]].Boundary.CenterY;
      }
    }

    keys.Sort(indices);

    if (rented != null)
    {
      ArrayPool<float>.Shared.Return(rented);
    }
  }

  private void UpdateViewportItems(in RTreeBoundary oldViewport, in RTreeBoundary newViewport)
  {
    if (newViewport.IsEmpty)
    {
      if (!oldViewport.IsEmpty)
      {
        _viewportItems.Clear();
      }

      return;
    }

    if (oldViewport.IsEmpty)
    {
      // Hot path if previously no viewport was set, just add all items intersecting the viewport.
      // Not directly querying to _viewPortItems to avoid unnecessary locks/collection changed events.
      Debug.Assert(_viewportUpdateCache.Count == 0);
      QueryTo(in newViewport, _viewportUpdateCache);
      _viewportItems.AddRange(_viewportUpdateCache);
      _viewportUpdateCache.Clear();
      return;
    }

    if (!oldViewport.IntersectsUnchecked(in newViewport))
    {
      _viewportItems.Clear();

      // Not directly querying to _viewPortItems to avoid unnecessary locks/collection changed events.
      Debug.Assert(_viewportUpdateCache.Count == 0);
      QueryTo(in newViewport, _viewportUpdateCache);
      _viewportItems.AddRange(_viewportUpdateCache);
      _viewportUpdateCache.Clear();
      return;
    }

    if (oldViewport.ContainsUnchecked(in newViewport))
    {
      // Hot path for zooming in/out and panning.
      Debug.Assert(_viewportUpdateCache.Count == 0);
      QueryTo(in newViewport, _viewportUpdateCache);
      _viewportItems.IntersectWith(_viewportUpdateCache);
      _viewportUpdateCache.Clear();
      return;
    }

    // At this point we deal with partially overlapping data.
    Debug.Assert(_viewportUpdateCache.Count == 0);

    QueryTo(in newViewport, _viewportUpdateCache);
    _viewportItems.IntersectThenUnionWith(_viewportUpdateCache);
    _viewportUpdateCache.Clear();
  }

  private unsafe int MoveChildrenToParentIfCapacityAvailable(int nodeIndex)
  {
    ref var node = ref Nodes[nodeIndex];
    Debug.Assert(!node.IsLeaf, "Must not be called on leaf nodes.");

    var parentIndex = node.ParentIndex;
    if (parentIndex < 0)
    {
      return nodeIndex;
    }

    if (Nodes[parentIndex].GetRemainingCapacity(MaxEntriesPerNode) < node.ChildrenCount - 1)
    {
      // Removing the node from its parent frees one slot, hence -1 in the condition above.
      return nodeIndex;
    }

    // Collect children before removing node from parent (which invalidates slots)
    var childReferenceIndex = node.FirstChildReferenceIndex;
    var childCount = node.ChildrenCount;

    int[]? rented = null;
    Span<int> childIndices;

    if (childCount > StackAllocLimit)
    {
      rented = ArrayPool<int>.Shared.Rent(childCount);
      childIndices = rented.AsSpan(0, childCount);
    }
    else
    {
#pragma warning disable CS9081
      // Disable "A result of a stackalloc expression of this type in this context may be exposed outside the containing method"
      // The stack allocated span is used for intermediate save of indices, does not leave the stack.
      childIndices = stackalloc int[childCount];
#pragma warning restore CS9081
    }

    for (var index = 0; index < childCount; index++)
    {
      childIndices[index] = ChildReferences[childReferenceIndex + index].NodeIndex;
    }

    ref var parent = ref Nodes[parentIndex];
    parent.RemoveChildDirect(this, nodeIndex);

    for (var index = 0; index < childCount; index++)
    {
      var childIndex = childIndices[index];
      ref var child = ref Nodes[childIndex];

      parent.AddChildDirect(this, ref child);
    }

    if (rented != null)
    {
      ArrayPool<int>.Shared.Return(rented, true);
    }

    Nodes[nodeIndex].ChildrenCount = 0; // Reset before freeing to avoid double-detach
    RTreeNode<T>.Free(this, ref node);

    return parentIndex;
  }

  internal int AllocateNodeSlot()
  {
    if (FreeNodeHead != RTreeNode<T>.NullIndex)
    {
      var index = FreeNodeHead;
      FreeNodeHead = Nodes[index].ParentIndex; // Next-free stored in ParentIndex
      return index;
    }

    if (_nodeCount >= Nodes.Length)
    {
      Array.Resize(ref Nodes, Nodes.Length * 2);
    }

    return _nodeCount++;
  }

  internal int AllocateChildBlock()
  {
    if (_freeChildBlockHead != RTreeNode<T>.NullIndex)
    {
      var blockIndex = _freeChildBlockHead;
      _freeChildBlockHead = ChildReferences[blockIndex * MaxEntriesPerNode].NodeIndex; // Next-free stored in first slot
      return blockIndex;
    }

    var requiredSlots = (_childReferencesCount + 1) * MaxEntriesPerNode;
    if (requiredSlots > ChildReferences.Length)
    {
      Array.Resize(ref ChildReferences, ChildReferences.Length * 2);
    }

    return _childReferencesCount++;
  }

  internal void FreeChildBlock(int firstChildIndex)
  {
    // Push onto free list (first slot's NodeIndex stores next-free block index)
    var blockIndex = firstChildIndex / MaxEntriesPerNode;
    ChildReferences[firstChildIndex].NodeIndex = _freeChildBlockHead;
    _freeChildBlockHead = blockIndex;
  }

  private static (int Nodes, int ChildBlocks) EstimateCapacities(int leafCapacity, int maxEntriesPerNode)
  {
    // Overshoot the specified target by about 25%, as the calculation below assumes a perfect distribution.
    const int overshoot = 25;
    // Every value above the following would overflow in the calculation below.
    const int upperLimit = (int)(int.MaxValue / (1 + overshoot / 100d));
    // Every value below the following can be safely multiplied by (100 + overshoot).
    // Every value above must first be divided by 100, then multiplied by (100 + overshoot).
    // Why: e.g. 10 * 125 / 100 = 12, but 10 / 100 * 125 = 0 -> Fraction loss of small numbers has high impact,
    // while it does not matter so much for large ones.
    const int saveMultiplyLimit = int.MaxValue / 125;

    if (leafCapacity > upperLimit)
    {
      leafCapacity = int.MaxValue;
    }
    else if (leafCapacity > saveMultiplyLimit)
    {
      leafCapacity = leafCapacity / 100 * (100 + overshoot);
    }
    else
    {
      leafCapacity = leafCapacity * (100 + overshoot) / 100;
    }

    var estimatedChildBlockCount = leafCapacity / Math.Max(maxEntriesPerNode - 1, 1) + 1;
    var estimatedNodeCount = Math.Max(1, leafCapacity + estimatedChildBlockCount); // 1: always required for root.
    return (estimatedNodeCount, estimatedChildBlockCount);
  }
}