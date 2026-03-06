#nullable enable

using System;
using System.Runtime.CompilerServices;

namespace Wobuntu.Collections.Spatial;

internal readonly struct RTreeNodeReference<T>
  : IEquatable<RTreeNodeReference<T>> where T : notnull
{
  public readonly RTreeBoundary Boundary;

  public readonly RTreeNode<T> Node;

  public readonly bool IsLeaf;

  internal static RTreeNodeReference<T> CreateNullReference(RTreeBoundary boundary) => new(boundary);

  public RTreeNodeReference(RTreeNode<T> node)
  {
    Node = node;
    IsLeaf = node.IsLeaf;
  }

  public RTreeNodeReference(RTreeNode<T> node, RTreeBoundary boundary)
  {
    Boundary = boundary;
    Node = node;
    IsLeaf = node.IsLeaf;
  }

  private RTreeNodeReference(RTreeBoundary boundary)
  {
    Boundary = boundary;
    Node = null!;
  }

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public bool Equals(RTreeNodeReference<T> other) => other.Node == Node;

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool operator ==(RTreeNodeReference<T> self, RTreeNodeReference<T> other) => self.Equals(other);

  [MethodImpl(MethodImplOptions.AggressiveInlining)]
  public static bool operator !=(RTreeNodeReference<T> self, RTreeNodeReference<T> other) => !(self == other);

  public override bool Equals(object? other) => other is RTreeNodeReference<T> otherReference && Equals(otherReference);

  public override int GetHashCode() => Node.GetHashCode();
}