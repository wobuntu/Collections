using Wobuntu.Collections.Spatial;

namespace Wobuntu.Collections.Tests.Spatial.Helpers;

internal static class RTreeExtensions
{
  internal static RTreeTestView<T> CreateTestView<T>(this RTree<T> self, int index) where T : notnull =>
    new RTreeTestView<T>(self, index);
}