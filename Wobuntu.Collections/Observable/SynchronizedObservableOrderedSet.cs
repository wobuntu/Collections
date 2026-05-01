using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Diagnostics;

namespace Wobuntu.Collections.Observable;

/// <summary>
///   Represents a thread-safe, observable collection that maintains unique items in insertion order.
///   Provides O(1) lookups, additions, and index-based access with collection change notifications.
/// </summary>
/// <remarks>
///   <para>
///     <see cref="INotifyCollectionChanged.CollectionChanged"/> and
///     <see cref="INotifyPropertyChanged.PropertyChanged"/> events are raised while the write lock is held.
///     This guarantees that event args always reflect the actual state of the collection at the time the event fires,
///     avoiding stale or inconsistent data in handlers.
///   </para>
///   <para>
///     As a consequence, event handlers must not perform synchronous cross-thread marshaling (e.g.
///     <c>Dispatcher.Invoke</c>) that accesses this collection, as this will deadlock. Use asynchronous dispatch
///     (e.g. <c>Dispatcher.BeginInvoke</c>) instead.
///   </para>
/// </remarks>
/// <typeparam name="T">The type of elements in the set.</typeparam>
[DebuggerDisplay($"Count = {{{nameof(Count)}}}")]
public class SynchronizedObservableOrderedSet<T>
  : IList<T>,
    IList,
    IReadOnlyList<T>,
    ISet<T>,
    IReadOnlySet<T>,
    INotifyCollectionChanged,
    INotifyPropertyChanged
    where T : notnull
{
  private const string ErrorSetItemDuplicate = "The item already exists at a different index.";

  // ReSharper disable once StaticMemberInGenericType : Intended.
  private static int _nextInstanceId;

  private readonly int _instanceId = Interlocked.Increment(ref _nextInstanceId);
  private readonly List<T> _ordered;
  private readonly HashSet<T> _hashed;
  private readonly Dictionary<T, int> _indices;

  private int _blockReentryCount;

  /// <summary>
  ///   Indicates that a write lock is currently still obtained, but finished modifications, so that read access is
  ///   safe. This flag exists to prevent deadlocks from scenarios like the following:<br />
  ///   - Background Thread B modifies the collection -> write lock obtained<br />
  ///   - Collection raises property changed / collection changed<br />
  ///   - Both event handlers may be marshalled to the UI thread A, and it is legitimate that handlers attempt
  ///     to access the collection for read<br />
  ///   - Without the flag, B holds the write lock, A tries to read, but is blocked -> deadlock
  /// </summary>
  private volatile bool _isBypassingReadLockSafe;

  protected readonly ReaderWriterLockSlim Lock = new(LockRecursionPolicy.SupportsRecursion);

  public SynchronizedObservableOrderedSet()
  {
    _ordered = [];
    _hashed = [];
    _indices = [];
  }

  public SynchronizedObservableOrderedSet(IReadOnlyList<T> collection)
  {
    ArgumentNullException.ThrowIfNull(collection);

    var count = collection.Count;
    var ordered = new List<T>(count);
    var hashed = new HashSet<T>(count);
    var indices = new Dictionary<T, int>(count);

    var index = 0;
    foreach (var item in collection)
    {
      if (item == null!)
      {
        continue;
      }

      if (!hashed.Add(item))
      {
        continue;
      }

      ordered.Add(item);
      indices[item] = index++;
    }

    _ordered = ordered;
    _hashed = hashed;
    _indices = indices;
  }

  public SynchronizedObservableOrderedSet(int capacity)
  {
    _ordered = new List<T>(capacity);
    _hashed = new HashSet<T>(capacity);
    _indices = new Dictionary<T, int>(capacity);
  }

  public T this[int index]
  {
    get
    {
      using var _ = new ReadLockScope(this);
      return GetItem(index);
    }
    set
    {
      using var _ = new WriteLockScope(this);
      SetItem(index, value);
    }
  }

  /// <inheritdoc cref="ISet{T}" />
  public int Count
  {
    get
    {
      using var _ = new ReadLockScope(this);
      return _ordered.Count;
    }
  }

  /// <inheritdoc cref="ISet{T}" />
  public int Capacity
  {
    get
    {
      using var _ = new ReadLockScope(this);
      return _ordered.Capacity;
    }
  }

  /// <inheritdoc cref="ISet{T}" />
  public bool Add(T item)
  {
    if (item == null!)
    {
      return false;
    }

    using var _ = new WriteLockScope(this);
    var index = _ordered.Count;
    return InsertItem(index, item);
  }

  /// <summary>
  ///   Adds the elements of the specified collection to the end of the set, skipping duplicates and null references.
  ///   Raises a single <see cref="INotifyCollectionChanged.CollectionChanged"/> event for all added items.
  /// </summary>
  /// <returns>The number of items actually added.</returns>
  public int AddRange(IEnumerable<T> items)
  {
    ArgumentNullException.ThrowIfNull(items);
    using var _ = new WriteLockScope(this);
    return InsertItems(_ordered.Count, items);
  }

  /// <inheritdoc cref="IList{T}" />
  public void EnsureCapacity(int capacity)
  {
    using var _ = new WriteLockScope(this);
    _ordered.EnsureCapacity(capacity);
    _hashed.EnsureCapacity(capacity);
    _indices.EnsureCapacity(capacity);
  }

  /// <inheritdoc cref="IList{T}" />
  public bool Insert(int index, T item)
  {
    if (item == null!)
    {
      return false;
    }

    using var _ = new WriteLockScope(this);
    return InsertItem(index, item);
  }

  /// <summary>
  ///   Inserts elements of the specified collection at the specified <paramref name="startIndex"/>,
  ///   skipping duplicates and null references.
  ///   Raises a single <see cref="INotifyCollectionChanged.CollectionChanged"/> event for all added items.
  /// </summary>
  /// <returns>The number of items actually inserted.</returns>
  public int InsertRange(int startIndex, IEnumerable<T> items)
  {
    ArgumentNullException.ThrowIfNull(items);
    using var _ = new WriteLockScope(this);
    return InsertItems(startIndex, items);
  }

  /// <inheritdoc cref="ISet{T}.Remove" />
  public void Clear()
  {
    using var _ = new WriteLockScope(this);
    ClearItems();
  }

  /// <summary>
  ///   Copies elements to an array. If the collection size changes during the copy,
  ///   copies as many elements as fit (truncating if grown, partial if shrunk).
  /// </summary>
  /// <remarks>
  ///   <para>
  ///     Note that this is different to how CopyTo works for most of the .NET base class library, which would
  ///     throw if the target array is not large enough to fit the entire data.<br />
  ///   </para>
  ///   <para>
  ///     E.g. the LINQ <c>ToList()</c> method does not call the <see cref="GetEnumerator"/> on lists which would
  ///     provide a thread-safe snapshot for our collection, instead it allocates an array using <see cref="Count"/>
  ///     together with <see cref="CopyTo"/>.<br />
  ///     Between reading <see cref="Count"/> and invoking <see cref="CopyTo"/>, the size might have changed already,
  ///     and an exception would be thrown.
  ///   </para>
  /// </remarks>
  public void CopyTo(T[] array, int index)
  {
    T[] snapshot;
    using (var _ = new ReadLockScope(this))
    {
      snapshot = _ordered.ToArray();
    }
    var count = Math.Min(snapshot.Length, array.Length - index);
    Array.Copy(snapshot, 0, array, index, count);
  }

  /// <inheritdoc cref="ISet{T}.Contains" />
  public bool Contains(T item)
  {
    if (item == null!)
    {
      return false;
    }

    using var _ = new ReadLockScope(this);
    return _hashed.Contains(item);
  }

  public void Move(int oldIndex, int newIndex)
  {
    if (oldIndex == newIndex)
    {
      return;
    }

    using var _ = new WriteLockScope(this);

    ArgumentOutOfRangeException.ThrowIfNegative(oldIndex);
    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(oldIndex, _ordered.Count);
    ArgumentOutOfRangeException.ThrowIfNegative(newIndex);
    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(newIndex, _ordered.Count);

    MoveItem(oldIndex, newIndex);
  }

  /// <inheritdoc cref="ISet{T}.Remove" />
  public bool Remove(T item)
  {
    if (item == null!)
    {
      return false;
    }

    using var _ = new WriteLockScope(this);
    return RemoveItem(item);
  }

  /// <inheritdoc cref="IList{T}.RemoveAt" />
  public void RemoveAt(int index)
  {
    using var _ = new WriteLockScope(this);
    RemoveItem(index);
  }

  /// <summary>
  ///   Removes the specified items from the set.
  ///   Raises a single <see cref="INotifyCollectionChanged.CollectionChanged"/> reset event for all removed items.
  /// </summary>
  /// <returns>The number of items actually removed.</returns>
  public int RemoveRange(IEnumerable<T> items)
  {
    ArgumentNullException.ThrowIfNull(items);
    using var _ = new WriteLockScope(this);
    return RemoveItems(items);
  }

  public int RemoveRange(int startIndex, int count)
  {
    using var _ = new WriteLockScope(this);

    ArgumentOutOfRangeException.ThrowIfNegative(startIndex);
    ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(startIndex, _ordered.Count);
    ArgumentOutOfRangeException.ThrowIfNegative(count);
    ArgumentOutOfRangeException.ThrowIfGreaterThan(count, _ordered.Count - startIndex);

    return RemoveItems(startIndex, count);
  }

  /// <inheritdoc />
  public int IndexOf(T item)
  {
    if (item == null!)
    {
      // Null values are not allowed.
      return -1;
    }

    using var _ = new ReadLockScope(this);
    return _indices.GetValueOrDefault(item, -1);
  }

  /// <summary>
  ///   Returns a snapshot enumerator. The collection can be modified during enumeration without throwing exceptions,
  ///   but modifications won't be reflected in the enumeration.<br />
  ///   To avoid allocation in memory critical scenarios, use a for loop with classical indexing instead.
  /// </summary>
  public IEnumerator<T> GetEnumerator()
  {
    T[] snapshot;
    using (var _ = new ReadLockScope(this))
    {
      snapshot = _ordered.ToArray();
    }
    return ((IEnumerable<T>)snapshot).GetEnumerator();
  }

  /// <inheritdoc />
  object? IList.this[int index]
  {
    get => this[index];
    set
    {
      ArgumentNullException.ThrowIfNull(value);
      this[index] = (T)value;
    }
  }

  /// <inheritdoc />
  bool IList.IsReadOnly => false;

  /// <inheritdoc />
  bool IList.IsFixedSize => false;

  /// <inheritdoc />
  bool ICollection<T>.IsReadOnly => false;

  /// <inheritdoc />
  bool ICollection.IsSynchronized => true;

  /// <inheritdoc />
  int IList.Add(object? value)
  {
    if (value == null)
    {
      return -1;
    }

    using var _ = new WriteLockScope(this);
    var index = _ordered.Count;
    var typed = (T)value;
    return !InsertItem(index, typed) ? -1 : index;
  }

  /// <inheritdoc />
  void IList<T>.Insert(int index, T item)
  {
    using var _ = new WriteLockScope(this);
    InsertItem(index, item);
  }

  /// <inheritdoc />
  object ICollection.SyncRoot => Lock;

  /// <inheritdoc />
  void ICollection.CopyTo(Array array, int index)
  {
    T[] snapshot;
    using (var _ = new ReadLockScope(this))
    {
      snapshot = _ordered.ToArray();
    }
    var count = Math.Min(snapshot.Length, array.Length - index);
    Array.Copy(snapshot, 0, array, index, count);
  }

  /// <inheritdoc />
  void ICollection<T>.Add(T item) => Add(item);

  /// <inheritdoc />
  IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

  /// <inheritdoc />
  bool IList.Contains(object? value)
  {
    if (value == null)
    {
      return false;
    }

    using var _ = new ReadLockScope(this);
    var typed = (T)value;
    return _hashed.Contains(typed);
  }

  /// <inheritdoc />
  int IList.IndexOf(object? value) => value == null ? -1 : IndexOf((T)value);

  /// <inheritdoc />
  void IList.Insert(int index, object? value)
  {
    ArgumentNullException.ThrowIfNull(value);

    using var _ = new WriteLockScope(this);
    var typed = (T)value;
    InsertItem(index, typed);
  }

  /// <inheritdoc />
  void IList.Remove(object? value)
  {
    if (value == null)
    {
      return;
    }

    using var _ = new WriteLockScope(this);
    RemoveItem((T)value);
  }

  /// <inheritdoc />
  public void ExceptWith(IEnumerable<T> other)
  {
    ArgumentNullException.ThrowIfNull(other);

    if (ReferenceEquals(other, this))
    {
      using var _ = new WriteLockScope(this);
      ClearItems();
      return;
    }

    if (other is SynchronizedObservableOrderedSet<T> synchronizedSet)
    {
      using var _ = new WriteReadLockScope(this, synchronizedSet);
      RemoveItems(synchronizedSet._ordered);
      return;
    }

    using var __ = new WriteLockScope(this);
    RemoveItems(other);
  }

  /// <inheritdoc />
  public void IntersectWith(IEnumerable<T> other)
  {
    ArgumentNullException.ThrowIfNull(other);

    if (ReferenceEquals(other, this))
    {
      return;
    }

    if (other is SynchronizedObservableOrderedSet<T> synchronizedSet)
    {
      using var _ = new WriteReadLockScope(this, synchronizedSet);
      var toRemove = new List<T>();
      for (var index = 0; index < _ordered.Count; index++)
      {
        if (!synchronizedSet._hashed.Contains(_ordered[index]))
        {
          toRemove.Add(_ordered[index]);
        }
      }

      RemoveItems(toRemove);
      return;
    }

    using var __ = new WriteLockScope(this);

    if (_ordered.Count == 0)
    {
      return;
    }

    var otherSet = other as IReadOnlySet<T> ?? other.ToHashSet();
    var itemsToRemove = new List<T>();
    for (var index = 0; index < _ordered.Count; index++)
    {
      if (!otherSet.Contains(_ordered[index]))
      {
        itemsToRemove.Add(_ordered[index]);
      }
    }

    RemoveItems(itemsToRemove);
  }

  /// <inheritdoc />
  public bool IsProperSubsetOf(IEnumerable<T> other)
  {
    ArgumentNullException.ThrowIfNull(other);
    using var _ = new ReadLockScope(this);

    if (other is not SynchronizedObservableOrderedSet<T> synchronizedSet)
    {
      return _hashed.IsProperSubsetOf(other);
    }

    using var __ = new ReadLockScope(synchronizedSet);
    return _hashed.IsProperSubsetOf(synchronizedSet._hashed);
  }

  /// <inheritdoc />
  public bool IsProperSupersetOf(IEnumerable<T> other)
  {
    ArgumentNullException.ThrowIfNull(other);
    using var _ = new ReadLockScope(this);

    if (other is not SynchronizedObservableOrderedSet<T> synchronizedSet)
    {
      return _hashed.IsProperSupersetOf(other);
    }

    using var __ = new ReadLockScope(synchronizedSet);
    return _hashed.IsProperSupersetOf(synchronizedSet._hashed);
  }

  /// <inheritdoc />
  public bool IsSubsetOf(IEnumerable<T> other)
  {
    ArgumentNullException.ThrowIfNull(other);
    using var _ = new ReadLockScope(this);

    if (other is not SynchronizedObservableOrderedSet<T> synchronizedSet)
    {
      return _hashed.IsSubsetOf(other);
    }

    using var __ = new ReadLockScope(synchronizedSet);
    return _hashed.IsSubsetOf(synchronizedSet._hashed);
  }

  /// <inheritdoc />
  public bool IsSupersetOf(IEnumerable<T> other)
  {
    ArgumentNullException.ThrowIfNull(other);
    using var _ = new ReadLockScope(this);

    if (other is not SynchronizedObservableOrderedSet<T> synchronizedSet)
    {
      return _hashed.IsSupersetOf(other);
    }

    using var __ = new ReadLockScope(synchronizedSet);
    return _hashed.IsSupersetOf(synchronizedSet._hashed);
  }

  /// <inheritdoc />
  public bool Overlaps(IEnumerable<T> other)
  {
    ArgumentNullException.ThrowIfNull(other);
    using var _ = new ReadLockScope(this);

    if (other is not SynchronizedObservableOrderedSet<T> synchronizedSet)
    {
      return _hashed.Overlaps(other);
    }

    using var __ = new ReadLockScope(synchronizedSet);
    return _hashed.Overlaps(synchronizedSet._hashed);
  }

  /// <inheritdoc />
  public bool SetEquals(IEnumerable<T> other)
  {
    ArgumentNullException.ThrowIfNull(other);
    using var _ = new ReadLockScope(this);

    if (other is not SynchronizedObservableOrderedSet<T> synchronizedSet)
    {
      return _hashed.SetEquals(other);
    }

    using var __ = new ReadLockScope(synchronizedSet);
    return _hashed.SetEquals(synchronizedSet._hashed);
  }

  /// <inheritdoc />
  bool IReadOnlySet<T>.IsProperSubsetOf(IEnumerable<T> other) => IsProperSubsetOf(other);

  /// <inheritdoc />
  bool IReadOnlySet<T>.IsProperSupersetOf(IEnumerable<T> other) => IsProperSupersetOf(other);

  /// <inheritdoc />
  bool IReadOnlySet<T>.IsSubsetOf(IEnumerable<T> other) => IsSubsetOf(other);

  /// <inheritdoc />
  bool IReadOnlySet<T>.IsSupersetOf(IEnumerable<T> other) => IsSupersetOf(other);

  /// <inheritdoc />
  bool IReadOnlySet<T>.Overlaps(IEnumerable<T> other) => Overlaps(other);

  bool IReadOnlySet<T>.SetEquals(IEnumerable<T> other) => SetEquals(other);

  public void SymmetricExceptWith(IEnumerable<T> other)
  {
    ArgumentNullException.ThrowIfNull(other);

    if (ReferenceEquals(other, this))
    {
      using var _ = new WriteLockScope(this);
      ClearItems();
      return;
    }

    if (other is SynchronizedObservableOrderedSet<T> synchronizedSet)
    {
      using var _ = new WriteReadLockScope(this, synchronizedSet);
      var toRemove = new List<T>();
      var toAdd = new List<T>();

      for (var index = 0; index < synchronizedSet._ordered.Count; index++)
      {
        var otherItem = synchronizedSet._ordered[index];
        if (_hashed.Contains(otherItem))
        {
          toRemove.Add(otherItem);
        }
        else
        {
          toAdd.Add(otherItem);
        }
      }

      RemoveItems(toRemove);
      InsertItems(_ordered.Count, toAdd);
      return;
    }

    using var __ = new WriteLockScope(this);

    // Deduplication is required because non-set inputs may contain duplicates.
    // E.g. other=[A, A] with A in this: without dedup both would be classified as "to remove",
    // but duplicates in toAdd are harmless since InsertItems filters them via _hashed.Add.
    var processed = new HashSet<T>();
    var itemsToRemove = new List<T>();
    var itemsToAdd = new List<T>();

    foreach (var otherItem in other)
    {
      if (!processed.Add(otherItem))
      {
        continue;
      }

      if (_hashed.Contains(otherItem))
      {
        itemsToRemove.Add(otherItem);
      }
      else
      {
        itemsToAdd.Add(otherItem);
      }
    }

    RemoveItems(itemsToRemove);
    InsertItems(_ordered.Count, itemsToAdd);
  }

  public void UnionWith(IEnumerable<T> other)
  {
    ArgumentNullException.ThrowIfNull(other);

    // Union with self is a no-op since all items already exist.
    if (ReferenceEquals(other, this))
    {
      return;
    }

    if (other is SynchronizedObservableOrderedSet<T> synchronizedSet)
    {
      using var _ = new WriteReadLockScope(this, synchronizedSet);
      InsertItems(_ordered.Count, synchronizedSet._ordered);
      return;
    }

    using var __ = new WriteLockScope(this);
    InsertItems(_ordered.Count, other);
  }

  protected virtual void ClearItems()
  {
    // Note: Locks are handled by calling public methods.
    if (_ordered.Count == 0)
    {
      return;
    }

    CheckReentrancy();

    _ordered.Clear();
    _hashed.Clear();
    _indices.Clear();

    OnCountPropertyChanged();
    OnIndexerPropertyChanged();
    OnCollectionChanged(EventArgsCache.ResetCollectionChanged);
  }

  protected virtual bool RemoveItem(T item)
  {
    // Note: Locks are handled by calling public methods.
    CheckReentrancy();

    if (!_hashed.Remove(item))
    {
      return false;
    }

    _indices.Remove(item, out var removeAt);
    _ordered.RemoveAt(removeAt);

    // Update indices for all items after the removed one
    for (var index = removeAt; index < _ordered.Count; index++)
    {
      _indices[_ordered[index]] = index;
    }

    OnCountPropertyChanged();
    OnIndexerPropertyChanged();
    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, removeAt));

    return true;
  }

  protected virtual void RemoveItem(int index)
  {
    // Note: Locks are handled by calling public methods.
    CheckReentrancy();

    var removedItem = _ordered[index];
    _indices.Remove(removedItem);
    _ordered.RemoveAt(index);
    _hashed.Remove(removedItem);

    // Update indices for all items after the removed one
    for (var current = index; current < _ordered.Count; current++)
    {
      _indices[_ordered[current]] = current;
    }

    OnCountPropertyChanged();
    OnIndexerPropertyChanged();
    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItem, index));
  }

  protected virtual int RemoveItems(IEnumerable<T> items)
  {
    // Note: Locks are handled by calling public methods.
    CheckReentrancy();

    var removedCount = 0;

    var lowestIndex = int.MaxValue;
    var highestIndex = -1;

    foreach (var item in items)
    {
      if (item == null! || !_hashed.Remove(item))
      {
        continue;
      }

      _indices.Remove(item, out var previousIndex);
      if (lowestIndex > previousIndex)
      {
        lowestIndex = previousIndex;
      }

      if (highestIndex < previousIndex)
      {
        highestIndex = previousIndex;
      }

      removedCount++;
    }

    if (removedCount == 0)
    {
      return 0;
    }

    if (highestIndex - lowestIndex + 1 == removedCount)
    {
      // All removed items are in a contiguous range -> optimize removal.
      var removedItems = _ordered.GetRange(lowestIndex, removedCount);
      _ordered.RemoveRange(lowestIndex, removedCount);

      // Update indices for all items after the removed range
      for (var index = lowestIndex; index < _ordered.Count; index++)
      {
        _indices[_ordered[index]] = index;
      }

      OnCountPropertyChanged();
      OnIndexerPropertyChanged();
      OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
        removedItems, lowestIndex));

      return removedCount;
    }

    // Non-contiguous removals -> fall back to filtering.
    _ordered.RemoveAll(item => !_hashed.Contains(item));

    // Rebuild index lookup.
    _indices.Clear();
    for (var i = 0; i < _ordered.Count; i++)
    {
      _indices[_ordered[i]] = i;
    }

    OnCountPropertyChanged();
    OnIndexerPropertyChanged();
    OnCollectionChanged(EventArgsCache.ResetCollectionChanged);

    return removedCount;
  }

  protected virtual int RemoveItems(int startIndex, int count)
  {
    // Note: Locks are handled by calling public methods.
    CheckReentrancy();

    if (count == 0)
    {
      return 0;
    }

    var removedItems = new List<T>(count);
    for (var i = startIndex; i < startIndex + count; i++)
    {
      var item = _ordered[i];
      removedItems.Add(item);
      _hashed.Remove(item);
      _indices.Remove(item);
    }

    _ordered.RemoveRange(startIndex, count);

    // Update indices for all items after the removed range
    for (var index = startIndex; index < _ordered.Count; index++)
    {
      _indices[_ordered[index]] = index;
    }

    OnCountPropertyChanged();
    OnIndexerPropertyChanged();

    OnCollectionChanged(
      _ordered.Count == 0
        ? EventArgsCache.ResetCollectionChanged
        : new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItems, startIndex));

    return removedItems.Count;
  }

  protected virtual bool InsertItem(int index, T item)
  {
    // Note: Locks are handled by calling public methods.
    CheckReentrancy();

    if (item == null! || !_hashed.Add(item))
    {
      return false;
    }

    _ordered.Insert(index, item);

    // Update indices for all items at or after the insertion point
    for (var current = index + 1; current < _ordered.Count; current++)
    {
      _indices[_ordered[current]] = current;
    }

    _indices[item] = index;

    OnCountPropertyChanged();
    OnIndexerPropertyChanged();
    OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));

    return true;
  }

  protected virtual int InsertItems(int startIndex, IEnumerable<T> items)
  {
    // Note: Locks are handled by calling public methods.
    CheckReentrancy();

    var addedItems = new List<T>();

    foreach (var item in items)
    {
      if (item == null! || !_hashed.Add(item))
      {
        continue;
      }

      addedItems.Add(item);
    }

    if (addedItems.Count == 0)
    {
      return 0;
    }

    _ordered.InsertRange(startIndex, addedItems);

    // Update indices from the insertion point onwards (new + shifted items).
    for (var i = startIndex; i < _ordered.Count; i++)
    {
      _indices[_ordered[i]] = i;
    }

    OnCountPropertyChanged();
    OnIndexerPropertyChanged();
    OnCollectionChanged(
      new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, addedItems, startIndex));

    return addedItems.Count;
  }

  protected virtual T GetItem(int index)
  {
    // Note: Locks are handled by calling public methods.
    return _ordered[index];
  }

  protected virtual void SetItem(int index, T item)
  {
    // Note: Locks are handled by calling public methods.
    CheckReentrancy();

    ArgumentNullException.ThrowIfNull(item);
    var originalItem = _ordered[index];

    // If setting the same value to the same item, then nothing to do.
    if (EqualityComparer<T>.Default.Equals(originalItem, item))
    {
      return;
    }

    // Can't set to an item that already exists elsewhere
    if (!_hashed.Add(item))
    {
      throw new ArgumentException(ErrorSetItemDuplicate, nameof(item));
    }

    _ordered[index] = item;
    _hashed.Remove(originalItem);
    _indices.Remove(originalItem);
    _indices[item] = index;

    OnIndexerPropertyChanged();
    OnCollectionChanged(
      new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, item, originalItem, index));
  }

  protected virtual void MoveItem(int oldIndex, int newIndex)
  {
    // Note: Locks are handled by calling public methods.
    if (oldIndex == newIndex)
    {
      return;
    }

    CheckReentrancy();

    var removedItem = _ordered[oldIndex];
    _ordered.RemoveAt(oldIndex);
    _ordered.Insert(newIndex, removedItem);

    // Update the indices for all affected items.
    var minIndex = Math.Min(oldIndex, newIndex);
    var maxIndex = Math.Max(oldIndex, newIndex);

    for (var index = minIndex; index <= maxIndex; index++)
    {
      _indices[_ordered[index]] = index;
    }

    OnIndexerPropertyChanged();
    OnCollectionChanged(
      new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Move, removedItem, newIndex, oldIndex));
  }

  protected virtual void OnPropertyChanged(PropertyChangedEventArgs args)
  {
    var handler = PropertyChanged;
    if (handler == null)
    {
      return;
    }

    _blockReentryCount++;
    var previous = _isBypassingReadLockSafe;

    try
    {
      _isBypassingReadLockSafe = true;
      handler(this, args);
    }
    finally
    {
      _blockReentryCount--;
      _isBypassingReadLockSafe = previous;
    }
  }

  protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
  {
    var handler = CollectionChanged;
    if (handler == null)
    {
      return;
    }

    _blockReentryCount++;
    var previous = _isBypassingReadLockSafe;

    try
    {
      _isBypassingReadLockSafe = true;
      handler(this, args);
    }
    finally
    {
      _blockReentryCount--;
      _isBypassingReadLockSafe = previous;
    }
  }

  protected void CheckReentrancy()
  {
    if (_blockReentryCount > 0)
    {
      throw new InvalidOperationException(
        $"Cannot change {nameof(SynchronizedObservableOrderedSet<>)} during a change notification event.");
    }
  }

  private void OnCountPropertyChanged() => OnPropertyChanged(EventArgsCache.CountPropertyChanged);

  private void OnIndexerPropertyChanged() => OnPropertyChanged(EventArgsCache.IndexerPropertyChanged);

  public event PropertyChangedEventHandler? PropertyChanged;

  public event NotifyCollectionChangedEventHandler? CollectionChanged;

  protected readonly struct ReadLockScope : IDisposable
  {
    private readonly ReaderWriterLockSlim? _lock;

    public ReadLockScope(SynchronizedObservableOrderedSet<T> toReadLock)
    {
      if (toReadLock._isBypassingReadLockSafe)
      {
        return;
      }

      _lock = toReadLock.Lock;
      _lock.EnterReadLock();
    }

    public void Dispose() => _lock?.ExitReadLock();
  }

  protected readonly struct WriteLockScope : IDisposable
  {
    private readonly ReaderWriterLockSlim _lock;

    public WriteLockScope(SynchronizedObservableOrderedSet<T> toWriteLock)
    {
      _lock = toWriteLock.Lock;
      _lock.EnterWriteLock();
    }

    public void Dispose() => _lock.ExitWriteLock();
  }

  private readonly struct WriteReadLockScope : IDisposable
  {
    private readonly ReaderWriterLockSlim _writeLock;
    private readonly ReaderWriterLockSlim _readLock;

    /// <summary>
    ///   Acquires a write lock on <paramref name="toWriteLock"/> and a read lock on <paramref name="toReadLock"/>
    ///   in a consistent order based on instance identity, preventing deadlocks when two instances perform
    ///   cross-set operations concurrently (e.g. a.ExceptWith(b) and b.ExceptWith(a) on different threads).
    /// </summary>
    public WriteReadLockScope(
      SynchronizedObservableOrderedSet<T> toWriteLock,
      SynchronizedObservableOrderedSet<T> toReadLock)
    {
      _writeLock = toWriteLock.Lock;
      _readLock = toReadLock.Lock;

      if (toWriteLock._instanceId < toReadLock._instanceId)
      {
        _writeLock.EnterWriteLock();
        _readLock.EnterReadLock();
      }
      else
      {
        _readLock.EnterReadLock();
        _writeLock.EnterWriteLock();
      }
    }

    public void Dispose()
    {
      _readLock.ExitReadLock();
      _writeLock.ExitWriteLock();
    }
  }
}