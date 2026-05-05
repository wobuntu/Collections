using System;
using System.Buffers;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Disabling some resharper suggestions as they are hurting performance in hot paths of this file:
// ReSharper disable ForCanBeConvertedToForeach

namespace Wobuntu.Collections.Spatial;

[DebuggerDisplay($"{{{nameof(Boundary)}}} {{{nameof(IsLeaf)} ? {nameof(Data)}.ToString() : \"\"}}")]
[StructLayout(LayoutKind.Sequential)]
internal struct RTreeNode<T>
  where T : notnull
{
  internal const int NullIndex = -1;

  internal int ParentIndex;
  internal int OwnIndex;

  internal int FirstChildReferenceIndex;
  internal ushort ChildrenCount;
  private byte _childrenVisibleInViewport;
  private byte _childrenFullyVisibleInViewport;

  internal RTreeBoundary Boundary;
  internal T? Data;

  internal static ref RTreeNode<T> AllocateLeaf(RTree<T> owner, T data, RTreeBoundary boundary)
  {
    var index = owner.AllocateNodeSlot();

    ref var node = ref owner.Nodes[index];

    node.ParentIndex = NullIndex;
    node.OwnIndex = index;

    node.FirstChildReferenceIndex = NullIndex;
    node.ChildrenCount = 0;
    node._childrenVisibleInViewport = 0;
    node._childrenFullyVisibleInViewport = 0;

    node.Boundary = boundary;
    node.Data = data;

    return ref node;
  }

  internal static ref RTreeNode<T> AllocateNonLeaf(RTree<T> owner)
  {
    var index = owner.AllocateNodeSlot();
    var childBlockIndex = owner.AllocateChildBlock();

    ref var node = ref owner.Nodes[index];

    node.ParentIndex = NullIndex;
    node.OwnIndex = index;

    node.FirstChildReferenceIndex = childBlockIndex * owner.MaxEntriesPerNode;
    node.ChildrenCount = 0;
    node._childrenVisibleInViewport = 0;
    node._childrenFullyVisibleInViewport = 0;

    node.Boundary = new RTreeBoundary();
    node.Data = default;

    return ref node;
  }

  internal static void Free(RTree<T> owner, ref RTreeNode<T> node)
  {
    if (!node.IsLeaf)
    {
      owner.FreeChildBlock(node.FirstChildReferenceIndex);
    }

    node._childrenVisibleInViewport = 0;
    node._childrenFullyVisibleInViewport = 0;

    node.Data = default;
    node.FirstChildReferenceIndex = NullIndex;
    node.ChildrenCount = 0;
    node.Boundary = default;

    node.ParentIndex = owner.FreeNodeHead;
    owner.FreeNodeHead = node.OwnIndex;

    node.OwnIndex = NullIndex;
  }

  [MemberNotNullWhen(true, nameof(Data))]
  internal readonly bool IsLeaf
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => FirstChildReferenceIndex < 0;
  }

  internal readonly byte ChildrenVisibleInViewport
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get
    {
      Debug.Assert(FirstChildReferenceIndex >= 0, "For correct semantics, don't call this on data leafs.");
      return _childrenVisibleInViewport;
    }
  }

  internal bool IsFullyVisibleInViewport
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => _childrenFullyVisibleInViewport >= ChildrenCount
           && _childrenFullyVisibleInViewport > 0;
  }

  internal readonly bool IsVisibleInViewport
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get
    {
      Debug.Assert(IsLeaf, "For correct semantics, don't call this on non-leafs.");
      Debug.Assert(_childrenVisibleInViewport <= 1, "Must not become > 1 for leaf nodes.");
      return _childrenVisibleInViewport > 0;
    }
  }

  internal void SetLeafVisibleInViewport(RTree<T> owner, bool value)
  {
    Debug.Assert(IsLeaf, "Must only be called on leaf nodes.");

    var current = _childrenVisibleInViewport > 0;
    if (current == value)
    {
      return;
    }

    _childrenVisibleInViewport = value ? (byte)1 : (byte)0;
    _childrenFullyVisibleInViewport = _childrenVisibleInViewport;

    if (ParentIndex < 0)
    {
      return;
    }

    ref var parent = ref owner.Nodes[ParentIndex];
    var delta = value ? 1 : -1;
    parent.UpdateChildrenVisibleInViewport(owner, parent.ChildrenCount, delta, delta);
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal readonly bool HasRemainingCapacity(int maxEntriesPerNode) =>
    !IsLeaf && maxEntriesPerNode - ChildrenCount > 0;

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal readonly int GetRemainingCapacity(int maxEntriesPerNode) =>
    IsLeaf ? 0 : Math.Max(0, maxEntriesPerNode - ChildrenCount);

  internal bool IsOverFull(int maxEntriesPerNode)
  {
    Debug.Assert(!IsLeaf, "Must not be called on leafs.");
    return maxEntriesPerNode < ChildrenCount;
  }

  internal void UpdateChildrenVisibleInViewport(RTree<T> owner, int childrenCount, int visibleDelta, int fullyVisibleDelta)
  {
    Debug.Assert(!IsLeaf, "Must not be called on leaf nodes.");
    Debug.Assert(_childrenVisibleInViewport + visibleDelta >= 0, "Must not ever become negative.");
    Debug.Assert(_childrenFullyVisibleInViewport + fullyVisibleDelta >= 0, "Must not ever become negative.");

    if (visibleDelta == 0 && fullyVisibleDelta == 0)
    {
      return;
    }

    var oldVisible = _childrenVisibleInViewport;
    var oldFullyVisible = childrenCount == _childrenFullyVisibleInViewport;
    
    _childrenVisibleInViewport += (byte)visibleDelta;
    _childrenFullyVisibleInViewport += (byte)fullyVisibleDelta;

    Debug.Assert(_childrenFullyVisibleInViewport <= _childrenVisibleInViewport);

    if (ParentIndex < 0)
    {
      return;
    }

    ref var parent = ref owner.Nodes[ParentIndex];

    var parentVisibleDelta = 0;
    if (_childrenVisibleInViewport <= 0 && oldVisible > 0)
    {
      // If value becomes 0: No more children are in the viewport, hence notify
      // the parent as well that this node is now no longer part of the viewport nodes.
      parentVisibleDelta = -1;
    }
    else if (_childrenVisibleInViewport > 0 && oldVisible <= 0)
    {
      // If value becomes > 0: No children have been tracked in the viewport before, hence
      // notify the parent that this node is now part of the viewport nodes.
      parentVisibleDelta = 1;
    }

    var parentFullyVisibleDelta = 0;
    if (!oldFullyVisible && IsFullyVisibleInViewport)
    {
      parentFullyVisibleDelta = 1;
    }
    else if (oldFullyVisible && !IsFullyVisibleInViewport)
    {
      parentFullyVisibleDelta = -1;
    }

    if (parentVisibleDelta != 0 || parentFullyVisibleDelta != 0)
    {
      parent.UpdateChildrenVisibleInViewport(owner, parent.ChildrenCount, parentVisibleDelta, parentFullyVisibleDelta);
    }
  }

  internal void AddChildDirect(RTree<T> owner, ref RTreeNode<T> child)
  {
    Debug.Assert(!IsLeaf, "Must not be called on leaf nodes.");

    var childBoundary = child.Boundary;
    var childReferenceIndex = FirstChildReferenceIndex + ChildrenCount;

    owner.ChildReferences[childReferenceIndex] = new RTreeNodeReference
    {
      NodeIndex = child.OwnIndex,
      Boundary = childBoundary,
    };

    ChildrenCount++;

    child.ParentIndex = OwnIndex;
    UpdateBoundaryIncremental(owner, in childBoundary);

    if (child._childrenVisibleInViewport > 0)
    {
      var childFullyVisibleDelta = child.IsFullyVisibleInViewport ? 1 : 0;
      UpdateChildrenVisibleInViewport(owner, ChildrenCount - 1, 1, childFullyVisibleDelta);
    }

    Debug.Assert(!IsOverFull(owner.MaxEntriesPerNode), "Must not create overfull nodes.");
  }

  internal void AddChildrenDirect(RTree<T> owner, Span<int> childIndices)
  {
    Debug.Assert(!IsLeaf, "Must not be called on leaf nodes.");

    var childReferenceOffset = FirstChildReferenceIndex;

    for (var index = 0; index < childIndices.Length; index++)
    {
      var childIndex = childIndices[index];
      ref var child = ref owner.Nodes[childIndex];
      var childBoundary = child.Boundary;

      var childReferenceIndex = childReferenceOffset + ChildrenCount;
      owner.ChildReferences[childReferenceIndex] = new RTreeNodeReference
      {
        NodeIndex = childIndex,
        Boundary = childBoundary,
      };

      ChildrenCount++;

      child.ParentIndex = OwnIndex;
      UpdateBoundaryIncremental(owner, in childBoundary);

      if (child._childrenVisibleInViewport > 0)
      {
        var childFullyVisibleDelta = child.IsFullyVisibleInViewport ? 1 : 0;
        UpdateChildrenVisibleInViewport(owner, ChildrenCount - 1, 1, childFullyVisibleDelta);
      }
    }

    Debug.Assert(ChildrenCount <= owner.MaxEntriesPerNode, "Must not create overfull nodes.");
  }

  internal void RemoveChildDirect(RTree<T> owner, int childIndex)
  {
    // TODO: childIndex: replace with child
    Debug.Assert(!IsLeaf, "Must not be called on leaf nodes.");

    var childReferenceOffset = FirstChildReferenceIndex;

    var removeAt = -1;
    for (var index = 0; index < ChildrenCount; index++)
    {
      if (owner.ChildReferences[childReferenceOffset + index].NodeIndex == childIndex)
      {
        removeAt = index;
        break;
      }
    }

    if (removeAt < 0)
    {
      Debug.Fail("Child not found in parent's slot list.");
      return;
    }

    // Swap-remove with last
    var lastIndex = ChildrenCount - 1;
    if (removeAt != lastIndex)
    {
      owner.ChildReferences[childReferenceOffset + removeAt] = owner.ChildReferences[childReferenceOffset + lastIndex];
    }

    ChildrenCount--;
    ref var child = ref owner.Nodes[childIndex];
    child.ParentIndex = NullIndex;
    UpdateBoundary(owner);

    var visibleDelta = 0;
    var fullyVisibleDelta = 0;

    var isLeaf = child.IsLeaf;
    if (isLeaf && child.IsVisibleInViewport)
    {
      visibleDelta = fullyVisibleDelta = -1;
    }
    else if (!isLeaf && child.ChildrenVisibleInViewport > 0)
    {
      visibleDelta = -1;
      fullyVisibleDelta = child.IsFullyVisibleInViewport ? -1 : 0;
    }

    if (visibleDelta != 0)
    {
      UpdateChildrenVisibleInViewport(owner, ChildrenCount + 1, visibleDelta, fullyVisibleDelta);
    }
  }

  internal void UpdateBoundary(RTree<T> owner)
  {
    Debug.Assert(!IsLeaf, "Must not be called on leaf nodes.");

    if (ChildrenCount == 0)
    {
      Boundary = new RTreeBoundary();
      SyncBoundaryInParentSlot(owner);
      return;
    }

    var childReferenceOffset = FirstChildReferenceIndex;
    Boundary = owner.ChildReferences[childReferenceOffset].Boundary;

    for (var index = 1; index < ChildrenCount; index++)
    {
      var child = owner.ChildReferences[childReferenceOffset + index];
      var childBoundary = child.Boundary;

      if (childBoundary.IsEmpty)
      {
        continue;
      }

      Boundary = Boundary.IsEmpty ? childBoundary : Boundary.Union(childBoundary);
    }

    SyncBoundaryInParentSlot(owner);
  }

  private void UpdateBoundaryIncremental(RTree<T> owner, in RTreeBoundary addedBoundary)
  {
    if (addedBoundary.IsEmpty)
    {
      return;
    }

    if (Boundary.IsEmpty)
    {
      Boundary = addedBoundary;
    }
    else
    {
      Boundary = Boundary.Union(addedBoundary);
    }

    SyncBoundaryInParentSlot(owner);
  }

  internal void SyncBoundaryInParentSlot(RTree<T> owner)
  {
    if (ParentIndex < 0)
    {
      return;
    }

    ref readonly var parent = ref owner.Nodes[ParentIndex];
    var childReferenceOffset = parent.FirstChildReferenceIndex;

    for (var index = 0; index < parent.ChildrenCount; index++)
    {
      ref var childReference = ref owner.ChildReferences[childReferenceOffset + index];
      if (childReference.NodeIndex != OwnIndex)
      {
        continue;
      }

      childReference.Boundary = Boundary;
      return;
    }
  }

  internal static void RebalanceBranch(RTree<T> owner, ref RTreeNode<T> branchRoot)
  {
    Debug.Assert(!branchRoot.IsLeaf, "Must not be called on leaf nodes.");

    var nodeIndices = ArrayPool<int>.Shared.Rent(branchRoot.ChildrenCount);

    for (var index = 0; index < branchRoot.ChildrenCount; index++)
    {
      nodeIndices[index] = owner.ChildReferences[branchRoot.FirstChildReferenceIndex + index].NodeIndex;
    }

    // Reset viewport tracking before clearing children.
    // Setting field directly and propagating up manually.
    if (branchRoot is { ChildrenVisibleInViewport: > 0, ParentIndex: >= 0 })
    {
      ref var parent = ref owner.Nodes[branchRoot.ParentIndex];
      var fullyVisibleDelta = branchRoot.IsFullyVisibleInViewport ? -1 : 0;
      parent.UpdateChildrenVisibleInViewport(owner, parent.ChildrenCount, -1, fullyVisibleDelta);
    }

    var indicesSpan = nodeIndices.AsSpan(0, branchRoot.ChildrenCount);
    branchRoot.ChildrenCount = 0; // Reset before freeing to avoid double-detach
    branchRoot._childrenVisibleInViewport = 0;
    branchRoot._childrenFullyVisibleInViewport = 0; // TODO: Probably needs reset on each level
    branchRoot.Boundary = new RTreeBoundary();

    var branchRootIndex = branchRoot.OwnIndex;
    owner.BuildSortTileRecursiveTree(branchRootIndex, indicesSpan);

    // Reallocation may have occurred, which could cause references to be outdated.
    // Hence, refetch branchRoot.
    branchRoot = ref owner.Nodes[branchRootIndex];

    // Sync parent slot boundary after rebuild
    branchRoot.SyncBoundaryInParentSlot(owner);

    // Restore viewport propagation if any children are visible
    if (branchRoot.ChildrenVisibleInViewport > 0 && branchRoot.ParentIndex >= 0)
    {
      ref var parent = ref owner.Nodes[branchRoot.ParentIndex];
      var fullyVisibleDelta = branchRoot.IsFullyVisibleInViewport ? 1 : 0;
      parent.UpdateChildrenVisibleInViewport(owner, parent.ChildrenCount, 1, fullyVisibleDelta);
    }

    ArrayPool<int>.Shared.Return(nodeIndices, true);
  }

  internal static ref RTreeNode<T> InsertParentLayer(RTree<T> owner, ref RTreeNode<T> forNode)
  {
    var nodeIndex = forNode.OwnIndex;
    ref var newParent = ref AllocateNonLeaf(owner);

    // Allocation of a parent layer may invalidate forNode reference, hence refetch after allocation.
    forNode = ref owner.Nodes[nodeIndex];
    newParent.Boundary = forNode.Boundary;

    var oldParentIndex = forNode.ParentIndex;
    newParent.ParentIndex = oldParentIndex;

    // Replace nodeIndex with newParentIndex in old parent's child slots
    if (oldParentIndex >= 0)
    {
      ref var oldParent = ref owner.Nodes[oldParentIndex];
      var childReferenceOffset = oldParent.FirstChildReferenceIndex;
      var newParentIndex = newParent.OwnIndex;

      for (var index = 0; index < oldParent.ChildrenCount; index++)
      {
        if (owner.ChildReferences[childReferenceOffset + index].NodeIndex == forNode.OwnIndex)
        {
          owner.ChildReferences[childReferenceOffset + index].NodeIndex = newParentIndex;
          // Boundary stays the same - newParent inherits node's boundary
          break;
        }
      }
    }

    // Add node as the only child of newParent
    var slotIndex = newParent.FirstChildReferenceIndex;
    owner.ChildReferences[slotIndex] = new RTreeNodeReference
    {
      NodeIndex = forNode.OwnIndex,
      Boundary = forNode.Boundary,
    };
    newParent.ChildrenCount = 1;
    forNode.ParentIndex = newParent.OwnIndex;

    // Viewport tracking: directly set the field to avoid propagation to old parent
    if (forNode._childrenVisibleInViewport > 0)
    {
      newParent._childrenVisibleInViewport = 1;
      newParent._childrenFullyVisibleInViewport = forNode.IsFullyVisibleInViewport ? (byte)1 : (byte)0;
    }

    return ref newParent;
  }
}

// TODO: check full boundary necessary in both structs
// TODO: Mark readonly wherever possible
// TODO: Return and work with Nodes instead of indexes where possible
// TODO: Use readonly ref vars where possible
// TODO: Fix method order (public, public static, internal, internal static, private, private static)
// TODO: Use methods to get nodes at an index, to return readonly or just ref nodes?
// TODO: Update method with delta: just use an increment/decrement version instead
// TODO: check visibility
// TODO: Better dictionary for searching ChildNodeRefs instead of iterating?