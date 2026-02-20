#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

// Disabling some resharper suggestions as they are hurting performance in hot paths of this file:
// ReSharper disable ForCanBeConvertedToForeach

namespace Wobuntu.Collections.Spatial;

[DebuggerDisplay($"{{{nameof(Boundary)}}} {{{nameof(IsLeaf)} ? {nameof(Data)}.ToString() : \"\"}}")]
internal class RTreeNode<T>
  where T : notnull
{
  private readonly int _maxEntries;
  private readonly int _minEntries;
  private readonly List<RTreeNode<T>>? _children;

  private int _childrenVisibleInViewport;
  private bool _isVisibleInViewport;

  internal RTreeNode<T>? Parent;
  internal RTreeBoundary Boundary;
  internal T? Data;

  private RTreeNode(T data, RTreeBoundary boundary)
  {
    ArgumentNullException.ThrowIfNull(data);
    Data = data;
    Boundary = boundary;
    IsLeaf = true;
  }

  private RTreeNode(int maxEntries)
  {
    Debug.Assert(maxEntries >= RTreeOptions.MinEntriesPerNodeMinimum);

    _maxEntries = maxEntries;
    _minEntries = RTreeOptions.DeriveMinEntriesFromMaxEntriesPerNode(maxEntries);
    _children = new List<RTreeNode<T>>(_maxEntries + 1); // +1 to avoid early reallocation when capacity is met.
  }

  internal static RTreeNode<T> CreateLeaf(T data, RTreeBoundary boundary) => new(data, boundary);

  internal static RTreeNode<T> CreateNonLeaf(int maxEntries) => new(maxEntries);

  [MemberNotNullWhen(true, nameof(Data))]
  [MemberNotNullWhen(false, nameof(_children), nameof(Children))]
  internal bool IsLeaf { get; }

  internal IReadOnlyList<RTreeNode<T>>? Children => _children;

  internal int RemainingCapacity => IsLeaf ? 0 : Math.Max(_maxEntries - _children.Count, 0);

  internal bool IsUnderFull => !IsLeaf && _children.Count < _minEntries;

  internal bool IsOverFull
  {
    get
    {
      Debug.Assert(!IsLeaf, "Must not be called on leafs.");
      return _maxEntries < _children.Count;
    }
  }

  internal int ChildrenVisibleInViewport
  {
    get => _childrenVisibleInViewport;
    private set
    {
      Debug.Assert(!IsLeaf, "Must not be called on leafs.");
      if (_childrenVisibleInViewport == value)
      {
        return;
      }

      var oldValue = _childrenVisibleInViewport;
      _childrenVisibleInViewport = value;

      Debug.Assert(_childrenVisibleInViewport >= 0, "Must not ever become negative.");
      Debug.Assert(_childrenVisibleInViewport <= Children.Count);

      if (Parent == null)
      {
        return;
      }

      if (value <= 0 && oldValue > 0)
      {
        // If value becomes 0: No more children of the parent are in the viewport, hence notify
        // its parent as well that it is now no longer part of the viewport nodes.
        Parent.ChildrenVisibleInViewport--;
      }
      else if (value > 0 && oldValue <= 0)
      {
        // If value becomes > 0: No children of the parent have been tracked before, hence
        // notify its parent as well that it is now part of the viewport nodes.
        Parent.ChildrenVisibleInViewport++;
      }
    }
  }

  internal bool IsVisibleInViewport
  {
    get => _isVisibleInViewport;
    set
    {
      Debug.Assert(IsLeaf, "Must not be called on non-leafs.");
      if (_isVisibleInViewport == value)
      {
        return;
      }

      _isVisibleInViewport = value;
      Parent?.ChildrenVisibleInViewport += value ? 1 : -1;
    }
  }

  internal void AddChildDirect(RTreeNode<T> child)
  {
    Debug.Assert(!IsLeaf, "Must not be called on data leafs.");
    _children.Add(child);
    child.Parent = this;
    UpdateBoundaryIncremental(child);

    if (child._isVisibleInViewport || child._childrenVisibleInViewport > 0)
    {
      ChildrenVisibleInViewport++;
    }

    Debug.Assert(!IsOverFull); // It should not be possible in this implementation to exceed _maxEntries.
  }

  internal void AddChildrenDirect(Span<RTreeNode<T>> children)
  {
    Debug.Assert(!IsLeaf, "Must not be called on data leafs.");
    for (var index = 0; index < children.Length; index++)
    {
      var child = children[index];
      _children.Add(child);
      child.Parent = this;
      UpdateBoundaryIncremental(child);

      if (child._isVisibleInViewport || child._childrenVisibleInViewport > 0)
      {
        ChildrenVisibleInViewport++;
      }
    }
  }

  internal void RemoveChildDirect(RTreeNode<T> child)
  {
    Debug.Assert(!IsLeaf, "Must not be called on data leafs.");
    if (child.Parent != this || !_children.Remove(child))
    {
      Debug.Fail("Should not be possible.");
      return;
    }

    child.Parent = null;
    UpdateBoundary();

    if (child._isVisibleInViewport || child._childrenVisibleInViewport > 0)
    {
      ChildrenVisibleInViewport--;
    }
  }

  internal RTreeNode<T> InsertParentLayer(int maxEntriesPerNode)
  {
    var newParent = CreateNonLeaf(maxEntriesPerNode);
    newParent.Boundary = Boundary;
    newParent.Parent = Parent;

    if (Parent != null && Parent._children!.IndexOf(this) is var ownIndex)
    {
      Debug.Assert(ownIndex >= 0);
      Parent._children![ownIndex] = newParent;
    }

    newParent._children!.Add(this);
    Parent = newParent;

    if (_isVisibleInViewport || _childrenVisibleInViewport > 0)
    {
      // Setting the field directly, because nothing changed for the
      // parent of newParent, which would be checked by the property setter.
      newParent._childrenVisibleInViewport = 1;
    }

    return newParent;
  }

  internal RTreeNode<T>? SplitAndGetCutoffBranch()
  {
    Debug.Assert(!IsLeaf, "Must not be called on data leafs.");
    Debug.Assert(IsOverFull, "Should not be called on nodes which are not yet full.");
    Debug.Assert(_children.Count >= 2, "Must not be called on nodes with less than two children.");

    var keptSeedIndex = -1;
    var cutoffSeedIndex = -1;
    RTreeNode<T> cutoffSeed = null!;
    var maxDistanceSquared = -1d;
    var keptSeedX = 0d;
    var keptSeedY = 0d;
    var cutoffSeedX = 0d;
    var cutoffSeedY = 0d;

    // First search for the 2 most distant nodes, which will be used as the base for arranging the split.
    for (var outerIndex = 0; outerIndex < _children.Count - 1; outerIndex++)
    {
      var outerChild = _children[outerIndex];
      var outerCenterX = outerChild.Boundary.CenterX;
      var outerCenterY = outerChild.Boundary.CenterY;

      for (var innerIndex = outerIndex + 1; innerIndex < _children.Count; innerIndex++)
      {
        var innerChild = _children[innerIndex];
        var innerCenterX = innerChild.Boundary.CenterX;
        var innerCenterY = innerChild.Boundary.CenterY;

        var diffX = innerCenterX - outerCenterX;
        var diffY = innerCenterY - outerCenterY;

        var distanceSquared = diffX * diffX + diffY * diffY;

        if (distanceSquared <= maxDistanceSquared)
        {
          continue;
        }

        maxDistanceSquared = distanceSquared;

        keptSeedIndex = outerIndex;
        keptSeedX = outerCenterX;
        keptSeedY = outerCenterY;

        cutoffSeedIndex = innerIndex;
        cutoffSeed = innerChild;
        cutoffSeedX = innerCenterX;
        cutoffSeedY = innerCenterY;
      }
    }

    // Remove only the second seed node, add it to a new detached parent:
    _children.RemoveAt(cutoffSeedIndex);

    if (cutoffSeed._isVisibleInViewport || cutoffSeed._childrenVisibleInViewport > 0)
    {
      ChildrenVisibleInViewport--;
    }

    var cutoffBranch = CreateNonLeaf(_maxEntries);
    cutoffBranch.AddChildDirect(cutoffSeed);

    // Foreach of the remaining nodes, add them either to the first or second group based again on the
    // distance to the seed nodes. We are not using the distance from the center of the currently populated
    // parent nodes, as this could lead to unwanted clustering due to a moved center point on every add.
    for (var index = _children.Count - 1; index >= 0; index--)
    {
      if (index == keptSeedIndex)
      {
        continue;
      }

      var child = _children[index];
      var centerX = child.Boundary.CenterX;
      var centerY = child.Boundary.CenterY;

      var firstDiffX = keptSeedX - centerX;
      var firstDiffY = keptSeedY - centerY;
      var keptSeedDistanceSquared = firstDiffX * firstDiffX + firstDiffY * firstDiffY;

      var secondDiffX = cutoffSeedX - centerX;
      var secondDiffY = cutoffSeedY - centerY;
      var cutoffSeedDistanceSquared = secondDiffX * secondDiffX + secondDiffY * secondDiffY;

      if (cutoffSeedDistanceSquared >= keptSeedDistanceSquared)
      {
        continue;
      }

      cutoffBranch.AddChildDirect(child);
      _children.RemoveAt(index);

      if (child._isVisibleInViewport || child._childrenVisibleInViewport > 0)
      {
        ChildrenVisibleInViewport--;
      }
    }

    // As we removed child nodes, our own boundary changed and needs an update.
    UpdateBoundary();

    if (Parent is null)
    {
      return cutoffBranch;
    }

    if (Parent.RemainingCapacity <= 0)
    {
      Parent.UpdateBoundary();
      return cutoffBranch;
    }

    // The parent still has capacity left, so add the cutoffBranch directly there.
    // The boundary of the parent hasn't changed either, so directly linking is fine.
    Parent._children!.Add(cutoffBranch);
    cutoffBranch.Parent = Parent;

    if (cutoffBranch._isVisibleInViewport || cutoffBranch._childrenVisibleInViewport > 0)
    {
      Parent.ChildrenVisibleInViewport++;
    }

    return null;
  }

  private void UpdateBoundary()
  {
    Debug.Assert(!IsLeaf, "Must not be called on data leafs.");
    if (_children.Count == 0)
    {
      Boundary = new RTreeBoundary();
      return;
    }

    Boundary = _children[0].Boundary;

    for (var index = 1; index < _children.Count; index++)
    {
      var childBoundary = _children[index].Boundary;
      if (childBoundary.IsEmpty)
      {
        continue;
      }

      Boundary = Boundary.IsEmpty ? childBoundary : Boundary.Union(childBoundary);
    }
  }

  private void UpdateBoundaryIncremental(RTreeNode<T> added)
  {
    var addedBoundary = added.Boundary;
    if (addedBoundary.IsEmpty)
    {
      return;
    }

    if (Boundary.IsEmpty)
    {
      Boundary = addedBoundary;
      return;
    }

    Boundary = Boundary.Union(addedBoundary);
  }
}