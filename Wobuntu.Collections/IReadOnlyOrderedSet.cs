using System.Collections.Generic;

namespace Wobuntu.Collections;

public interface IReadOnlyOrderedSet<T> : IReadOnlyList<T>, IReadOnlySet<T>;