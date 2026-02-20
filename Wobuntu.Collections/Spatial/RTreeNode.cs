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
    _children = new List<RTreeNode<T>>(_maxEntries);
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

    Debug.Assert(!IsOverFull, "This implementation must not create overfull nodes.");
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

    Debug.Assert(!IsOverFull, "This implementation must not create overfull nodes.");
  }

  internal void RemoveChildDirect(RTreeNode<T> child)
  {
    Debug.Assert(!IsLeaf, "Must not be called on data leafs.");

    var index = _children.IndexOf(child);
    if (child.Parent != this || index < 0)
    {
      Debug.Fail("Should not be possible.");
      return;
    }

    // Swap with the last element before removing to avoid O(n) shifting.
    var lastIndex = _children.Count - 1;
    if (index != lastIndex)
    {
      _children[index] = _children[lastIndex];
    }

    _children.RemoveAt(lastIndex);
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