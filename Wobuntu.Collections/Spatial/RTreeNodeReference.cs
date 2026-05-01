#nullable enable
using System.Runtime.InteropServices;

namespace Wobuntu.Collections.Spatial;

[StructLayout(LayoutKind.Sequential)]
internal struct RTreeNodeReference
{
  internal int NodeIndex;
  internal RTreeBoundary Boundary;
}