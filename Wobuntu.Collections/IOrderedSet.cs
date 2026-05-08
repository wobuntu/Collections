using System.Collections;
using System.Collections.Generic;

namespace Wobuntu.Collections;

// ReSharper disable once PossibleInterfaceMemberAmbiguity
public interface IOrderedSet<T> : IReadOnlyOrderedSet<T>, IList<T>, IList, ISet<T>
{
  void IntersectThenUnionWith(IEnumerable<T> other);
}