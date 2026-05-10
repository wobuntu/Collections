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

  internal RTreeBoundary Boundary;
  internal T? Data;

  [MemberNotNullWhen(true, nameof(Data))]
  internal readonly bool IsLeaf
  {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    get => FirstChildReferenceIndex < 0;
  }

  internal static ref RTreeNode<T> AllocateLeaf(RTree<T> owner, T data, in RTreeBoundary boundary)
  {
    var index = owner.AllocateNodeSlot();

    ref var node = ref owner.Nodes[index];

    node.ParentIndex = NullIndex;
    node.OwnIndex = index;

    node.FirstChildReferenceIndex = NullIndex;
    node.ChildrenCount = 0;

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

    node.Data = default;
    node.FirstChildReferenceIndex = NullIndex;
    node.ChildrenCount = 0;
    node.Boundary = default;

    node.ParentIndex = owner.FreeNodeHead;
    owner.FreeNodeHead = node.OwnIndex;

    node.OwnIndex = NullIndex;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal readonly bool HasRemainingCapacity(int maxEntriesPerNode) =>
    !IsLeaf && maxEntriesPerNode - ChildrenCount > 0;

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  internal readonly int GetRemainingCapacity(int maxEntriesPerNode) =>
    IsLeaf ? 0 : Math.Max(0, maxEntriesPerNode - ChildrenCount);

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

    Debug.Assert(!IsOverFull(owner.MaxEntriesPerNode), "Must not create overfull nodes.");
  }

  internal void AddChildrenDirect(RTree<T> owner, ReadOnlySpan<int> childIndices)
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
    }

    Debug.Assert(ChildrenCount <= owner.MaxEntriesPerNode, "Must not create overfull nodes.");
  }

  internal void RemoveChildDirect(RTree<T> owner, int childIndex)
  {
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
  }

  internal static void RebalanceBranch(RTree<T> owner, ref RTreeNode<T> branchRoot)
  {
    Debug.Assert(!branchRoot.IsLeaf, "Must not be called on leaf nodes.");

    var nodeIndices = ArrayPool<int>.Shared.Rent(branchRoot.ChildrenCount);

    for (var index = 0; index < branchRoot.ChildrenCount; index++)
    {
      nodeIndices[index] = owner.ChildReferences[branchRoot.FirstChildReferenceIndex + index].NodeIndex;
    }

    var indicesSpan = nodeIndices.AsSpan(0, branchRoot.ChildrenCount);
    branchRoot.ChildrenCount = 0; // Reset before freeing to avoid double-detach
    branchRoot.Boundary = new RTreeBoundary();

    var branchRootIndex = branchRoot.OwnIndex;
    owner.BuildSortTileRecursiveTree(branchRootIndex, indicesSpan, 0);

    // Reallocation may have occurred, which could cause references to be outdated.
    // Hence, refetch branchRoot.
    branchRoot = ref owner.Nodes[branchRootIndex];

    // Sync parent slot boundary after rebuild
    branchRoot.SyncBoundaryInParentSlot(owner);
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

    return ref newParent;
  }

  private bool IsOverFull(int maxEntriesPerNode)
  {
    Debug.Assert(!IsLeaf, "Must not be called on leafs.");
    return maxEntriesPerNode < ChildrenCount;
  }

  private void UpdateBoundary(RTree<T> owner)
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

      Boundary = Boundary.IsEmpty ? childBoundary : Boundary.Union(in childBoundary);
    }

    SyncBoundaryInParentSlot(owner);
  }

  private void SyncBoundaryInParentSlot(RTree<T> owner)
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

  private void UpdateBoundaryIncremental(RTree<T> owner, in RTreeBoundary addedBoundary)
  {
    if (addedBoundary.IsEmpty)
    {
      return;
    }

    Boundary = Boundary.IsEmpty ? addedBoundary : Boundary.Union(in addedBoundary);
    SyncBoundaryInParentSlot(owner);
  }
}