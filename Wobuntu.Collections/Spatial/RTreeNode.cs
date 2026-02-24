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

  internal readonly List<RTreeNode<T>>? Children;
  internal RTreeNode<T>? Parent;
  internal RTreeBoundary Boundary;
  internal T? Data;

  private int _childrenVisibleInViewport;

  private RTreeNode(T data, RTreeBoundary boundary)
  {
    ArgumentNullException.ThrowIfNull(data);
    Data = data;
    Boundary = boundary;
  }

  private RTreeNode(int maxEntries)
  {
    Debug.Assert(maxEntries >= RTreeOptions.MinEntriesPerNodeMinimum);

    _maxEntries = maxEntries;
    Children = new List<RTreeNode<T>>(_maxEntries);
  }

  internal static RTreeNode<T> CreateLeaf(T data, RTreeBoundary boundary) => new(data, boundary);

  internal static RTreeNode<T> CreateNonLeaf(int maxEntries) => new(maxEntries);

  [MemberNotNullWhen(true, nameof(Data))]
  [MemberNotNullWhen(false, nameof(Children))]
  internal bool IsLeaf => Children == null;

  internal int RemainingCapacity => IsLeaf ? 0 : Math.Max(_maxEntries - Children.Count, 0);

  internal bool IsOverFull
  {
    get
    {
      Debug.Assert(!IsLeaf, "Must not be called on leafs.");
      return _maxEntries < Children.Count;
    }
  }

  internal int ChildrenVisibleInViewport
  {
    get => _childrenVisibleInViewport;
    set
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
    get => _childrenVisibleInViewport > 0;
    set
    {
      Debug.Assert(IsLeaf, "Must not be called on non-leafs.");
      if (_childrenVisibleInViewport > 0 == value)
      {
        return;
      }

      _childrenVisibleInViewport = value ? 1 : 0;
      Parent?.ChildrenVisibleInViewport += value ? 1 : -1;
    }
  }

  internal void AddChildDirect(RTreeNode<T> child)
  {
    Debug.Assert(!IsLeaf, "Must not be called on data leafs.");
    Children.Add(child);
    child.Parent = this;
    UpdateBoundaryIncremental(child);

    if (child._childrenVisibleInViewport > 0)
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
      Children.Add(child);
      child.Parent = this;
      UpdateBoundaryIncremental(child);

      if (child._childrenVisibleInViewport > 0)
      {
        ChildrenVisibleInViewport++;
      }
    }

    Debug.Assert(!IsOverFull, "This implementation must not create overfull nodes.");
  }

  internal void RemoveChildDirect(RTreeNode<T> child)
  {
    Debug.Assert(!IsLeaf, "Must not be called on data leafs.");

    var index = Children.IndexOf(child);
    if (child.Parent != this || index < 0)
    {
      Debug.Fail("Should not be possible.");
      return;
    }

    // Swap with the last element before removing to avoid O(n) shifting.
    var lastIndex = Children.Count - 1;
    if (index != lastIndex)
    {
      Children[index] = Children[lastIndex];
    }

    Children.RemoveAt(lastIndex);
    child.Parent = null;
    UpdateBoundary();

    if (child._childrenVisibleInViewport > 0)
    {
      ChildrenVisibleInViewport--;
    }
  }

  internal RTreeNode<T> InsertParentLayer(int maxEntriesPerNode)
  {
    var newParent = CreateNonLeaf(maxEntriesPerNode);
    newParent.Boundary = Boundary;
    newParent.Parent = Parent;

    if (Parent != null && Parent.Children!.IndexOf(this) is var ownIndex)
    {
      Debug.Assert(ownIndex >= 0);
      Parent.Children![ownIndex] = newParent;
    }

    newParent.Children!.Add(this);
    Parent = newParent;

    if (_childrenVisibleInViewport > 0)
    {
      // Setting the field directly, because nothing changed for the
      // parent of newParent, which would be checked by the property setter.
      newParent._childrenVisibleInViewport = 1;
    }

    return newParent;
  }

  internal void Reset()
  {
    Parent = null;
    Boundary = new RTreeBoundary();
    _childrenVisibleInViewport = 0;

    Children?.Clear();
    Data = default;
  }

  private void UpdateBoundary()
  {
    Debug.Assert(!IsLeaf, "Must not be called on data leafs.");
    if (Children.Count == 0)
    {
      Boundary = new RTreeBoundary();
      return;
    }

    Boundary = Children[0].Boundary;

    for (var index = 1; index < Children.Count; index++)
    {
      var childBoundary = Children[index].Boundary;
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