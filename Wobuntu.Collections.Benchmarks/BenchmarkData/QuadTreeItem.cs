using System.Drawing;
using QuadTrees.QTreeRectF;

namespace Wobuntu.Collections.Benchmarks.BenchmarkData;

public sealed class QuadTreeItem(float x, float y) : IRectFQuadStorable
{
  public RectangleF Rect { get; } = new(x, y, 1.0f, 1.0f);
}