using System.Diagnostics;
using Wobuntu.Collections.Spatial;

namespace Wobuntu.Collections.Tests.Spatial.Helpers;

internal readonly struct RTreeTestView<T> where T : notnull
{
  private readonly RTree<T> _tree;
  private readonly int _index;

  internal RTreeTestView(RTree<T> tree, int index)
  {
    _tree = tree;
    _index = index;
  }

  internal int Index => _index;

  internal bool IsLeaf => _tree.Nodes[_index].IsLeaf;

  internal RTreeBoundary Boundary => _tree.Nodes[_index].Boundary;

  internal T? Data => _tree.Nodes[_index].Data;

  internal int ChildCount => IsLeaf ? 0 : _tree.Nodes[_index].ChildrenCount;

  internal int RemainingCapacity => IsLeaf
    ? 0
    : Math.Max(_tree.MaxEntriesPerNode - _tree.Nodes[_index].ChildrenCount, 0);

  internal RTreeTestView<T> Parent
  {
    get
    {
      var parentIndex = _tree.Nodes[_index].ParentIndex;
      Debug.Assert(parentIndex >= 0, "Node has no parent.");
      return new RTreeTestView<T>(_tree, parentIndex);
    }
  }

  internal bool HasParent => _tree.Nodes[_index].ParentIndex >= 0;

  internal RTreeTestView<T> Child(int childIndex)
  {
    Debug.Assert(!IsLeaf);
    ref readonly var node = ref _tree.Nodes[_index];
    Debug.Assert(childIndex >= 0 && childIndex < node.ChildrenCount);
    var slotIndex = node.FirstChildReferenceIndex + childIndex;
    return new RTreeTestView<T>(_tree, _tree.ChildReferences[slotIndex].NodeIndex);
  }

  /// <summary>
  ///   Gets the children as an array of views. Allocates — use only in tests.
  /// </summary>
  internal RTreeTestView<T>[] Children
  {
    get
    {
      if (IsLeaf)
      {
        return [];
      }

      ref readonly var node = ref _tree.Nodes[_index];
      var result = new RTreeTestView<T>[node.ChildrenCount];
      var blockStart = node.FirstChildReferenceIndex;
      for (var index = 0; index < node.ChildrenCount; index++)
      {
        result[index] = new RTreeTestView<T>(_tree, _tree.ChildReferences[blockStart + index].NodeIndex);
      }

      return result;
    }
  }
}