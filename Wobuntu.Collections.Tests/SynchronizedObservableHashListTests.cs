using System.Collections;
using System.Collections.Specialized;

namespace Wobuntu.Collections.Tests;

// Justification for suppressed warning xUnit2017: "Do not use Contains() to check if a value exists in a collection"
// We need to ensure, that the correct Contains() method is called, as we otherwise we cannot know which method
// (the public one or the explicit interface implementations) are called. Would cause ambiguity anyway.
#pragma warning disable xUnit2017

public class SynchronizedObservableOrderedSetTests
{
  [Fact]
  public void Constructor_WithCollectionParameter_CopiesItemsInOrder()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    Assert.Equal(3, set.Count);
    Assert.Equal(1, set[0]);
    Assert.Equal(2, set[1]);
    Assert.Equal(3, set[2]);
  }

  [Fact]
  public void Constructor_WithCollectionParameter_SkipsDuplicates()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 2, 3, 1]);
    Assert.Equal(3, set.Count);
    Assert.Equal(1, set[0]);
    Assert.Equal(2, set[1]);
    Assert.Equal(3, set[2]);
  }

  [Fact]
  public void Constructor_WithCollectionParameter_SkipsNullReferences()
  {
    var set = new SynchronizedObservableOrderedSet<string>(["a", null!, "b"]);
    Assert.Equal(2, set.Count);
    Assert.Equal("a", set[0]);
    Assert.Equal("b", set[1]);
  }

  [Fact]
  public void Add_NewItem_ReturnsTrue()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    Assert.True(set.Add(1));
    Assert.Single(set);
  }

  [Fact]
  public void Add_DuplicateItem_ReturnsFalse()
  {
    var set = new SynchronizedObservableOrderedSet<int> { 1 };
    Assert.False(set.Add(1));
    Assert.Single(set);
  }

  [Fact]
  public void Add_NullItem_ReturnsFalse()
  {
    var set = new SynchronizedObservableOrderedSet<string>();
    Assert.False(set.Add(null!));
    Assert.Empty(set);
  }

  [Fact]
  public void Add_ConsecutiveCalls_PreservesInsertionOrder()
  {
    var set = new SynchronizedObservableOrderedSet<int> { 3, 1, 2 };

    Assert.Equal(3, set[0]);
    Assert.Equal(1, set[1]);
    Assert.Equal(2, set[2]);
  }

  [Fact]
  public void AddRange_MultipleItems_AddsItemsPreservesOrder()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    var added = set.AddRange([3, 1, 2]);
    Assert.Equal(3, added);
    Assert.Equal(3, set.Count);
    Assert.Equal(3, set[0]);
    Assert.Equal(1, set[1]);
    Assert.Equal(2, set[2]);
  }

  [Fact]
  public void AddRange_WithDuplicatesAndNullReferences_SkipsBoth()
  {
    var set = new SynchronizedObservableOrderedSet<string> { "a" };
    var added = set.AddRange(["a", null!, "b", "b", "c"]);
    Assert.Equal(2, added);
    Assert.Equal(3, set.Count);
  }

  [Fact]
  public void AddRange_EmptyEnumerable_ReturnsZero()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    Assert.Equal(0, set.AddRange([]));
  }

  [Fact]
  public void AddRange_ThrowsOnNull()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    Assert.Throws<ArgumentNullException>(() => set.AddRange(null!));
  }

  [Fact]
  public void Insert_AtBeginning_ShiftsExisting()
  {
    var set = new SynchronizedObservableOrderedSet<int> { 2, 3 };
    Assert.True(set.Insert(0, 1));

    Assert.Equal(1, set[0]);
    Assert.Equal(2, set[1]);
    Assert.Equal(3, set[2]);
  }

  [Fact]
  public void Insert_InMiddle_ShiftsSubsequent()
  {
    var set = new SynchronizedObservableOrderedSet<int> { 1, 3 };
    set.Insert(1, 2);

    Assert.Equal(1, set[0]);
    Assert.Equal(2, set[1]);
    Assert.Equal(3, set[2]);
  }

  [Fact]
  public void Insert_Duplicate_ReturnsFalse()
  {
    var set = new SynchronizedObservableOrderedSet<int> { 1 };
    Assert.False(set.Insert(0, 1));
    Assert.Single(set);
  }

  [Fact]
  public void Insert_UpdatesIndexOf()
  {
    var set = new SynchronizedObservableOrderedSet<int> { 10, 20 };
    set.Insert(0, 5);

    Assert.Equal(0, set.IndexOf(5));
    Assert.Equal(1, set.IndexOf(10));
    Assert.Equal(2, set.IndexOf(20));
  }

  [Fact]
  public void InsertRange_InsertsAtPosition()
  {
    var set = new SynchronizedObservableOrderedSet<int> { 1, 4 };
    var inserted = set.InsertRange(1, [2, 3]);

    Assert.Equal(2, inserted);
    Assert.Equal(1, set[0]);
    Assert.Equal(2, set[1]);
    Assert.Equal(3, set[2]);
    Assert.Equal(4, set[3]);
  }

  [Fact]
  public void InsertRange_ThrowsOnNull()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    Assert.Throws<ArgumentNullException>(() => set.InsertRange(0, null!));
  }

  [Fact]
  public void IndexerSet_ReplacesItem()
  {
    var set = new SynchronizedObservableOrderedSet<int> { 1, 2 };
    set[0] = 10;

    Assert.Equal(10, set[0]);
    Assert.Equal(2, set[1]);
    Assert.False(set.Contains(1));
    Assert.True(set.Contains(10));
  }

  [Fact]
  public void IndexerSet_SetSameValueAtSamePosition_NoChangeNoCollectionChanged()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int> { 1 };

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set[0] = 1;

    // Assert
    Assert.Empty(events);
  }

  [Fact]
  public void IndexerSet_DuplicateAtDifferentIndex_Throws()
  {
    // ReSharper disable once CollectionNeverQueried.Local
    var set = new SynchronizedObservableOrderedSet<int> { 1, 2 };
    Assert.Throws<ArgumentException>(() => set[0] = 2);
  }

  [Fact]
  public void IndexerSet_NullValue_Throws()
  {
    // ReSharper disable once CollectionNeverQueried.Local
    var set = new SynchronizedObservableOrderedSet<string> { "a" };
    Assert.Throws<ArgumentNullException>(() => set[0] = null!);
  }

  [Fact]
  public void Remove_ExistingItem_ReturnsTrue()
  {
    var set = new SynchronizedObservableOrderedSet<int> { 1, 2, 3 };

    Assert.True(set.Remove(2));
    Assert.Equal(2, set.Count);
    Assert.Equal(1, set[0]);
    Assert.Equal(3, set[1]);
  }

  [Fact]
  public void Remove_NonExistingItem_ReturnsFalse()
  {
    var set = new SynchronizedObservableOrderedSet<int> { 1 };
    Assert.False(set.Remove(99));
  }

  [Fact]
  public void Remove_UpdatesIndices()
  {
    var set = new SynchronizedObservableOrderedSet<int> { 10, 20, 30 };
    set.Remove(10);

    Assert.Equal(0, set.IndexOf(20));
    Assert.Equal(1, set.IndexOf(30));
  }

  [Fact]
  public void RemoveAt_ValidIndex_RemovesItem()
  {
    var set = new SynchronizedObservableOrderedSet<int> { 10, 20, 30 };
    set.RemoveAt(1);

    Assert.Equal(2, set.Count);
    Assert.Equal(10, set[0]);
    Assert.Equal(30, set[1]);
    Assert.False(set.Contains(20));
  }

  [Fact]
  public void RemoveAt_InvalidIndex_Throws()
  {
    // ReSharper disable once CollectionNeverQueried.Local
    var set = new SynchronizedObservableOrderedSet<int> { 1 };
    Assert.Throws<ArgumentOutOfRangeException>(() => set.RemoveAt(5));
  }

  [Fact]
  public void RemoveRange_ByItems_RemovesSpecifiedItems()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3, 4, 5]);
    var removed = set.RemoveRange([2, 4]);

    Assert.Equal(2, removed);
    Assert.Equal(3, set.Count);
    Assert.True(set.Contains(1));
    Assert.False(set.Contains(2));
    Assert.True(set.Contains(3));
    Assert.False(set.Contains(4));
    Assert.True(set.Contains(5));
  }

  [Fact]
  public void RemoveRange_ContiguousItems_OptimizedRemoval()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3, 4, 5]);

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    var removed = set.RemoveRange([2, 3, 4]);

    // Assert
    Assert.Equal(3, removed);
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Remove, events[0].Action);
  }

  [Fact]
  public void RemoveRange_NonContiguousItems_FallsBackToReset()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3, 4, 5]);

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    var removed = set.RemoveRange([1, 3, 5]);

    // Assert
    Assert.Equal(3, removed);
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Reset, events[0].Action);
  }

  [Fact]
  public void RemoveRange_ByItems_NonExistingItemsIgnored()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3]);

    // Act
    var removed = set.RemoveRange([99, 100]);

    // Assert
    Assert.Equal(0, removed);
    Assert.Equal(3, set.Count);
  }

  [Fact]
  public void RemoveRange_ByItems_ThrowsOnNull()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    Assert.Throws<ArgumentNullException>(() => set.RemoveRange(null!));
  }

  [Fact]
  public void RemoveRange_ByIndex_RemovesCorrectRange()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3, 4, 5]);

    // Act
    var removed = set.RemoveRange(1, 3);

    // Assert
    Assert.Equal(3, removed);
    Assert.Equal(2, set.Count);
    Assert.Equal(1, set[0]);
    Assert.Equal(5, set[1]);
  }

  [Fact]
  public void RemoveRange_ByIndexZeroCount_ReturnsZero()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3]);

    // Act
    var removed = set.RemoveRange(0, 0);

    // Assert
    Assert.Equal(0, removed);
    Assert.Equal(3, set.Count);
  }

  [Fact]
  public void RemoveRange_ByIndex_InvalidRange_Throws()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3]);
    Assert.Throws<ArgumentOutOfRangeException>(() => set.RemoveRange(-1, 1));
    Assert.Throws<ArgumentOutOfRangeException>(() => set.RemoveRange(0, 10));
  }

  [Fact]
  public void RemoveRange_ByIndex_AllItems_RaisesReset()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3]);

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.RemoveRange(0, 3);

    // Assert
    Assert.Empty(set);
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Reset, events[0].Action);
  }

  [Fact]
  public void Clear_RemovesAllItems()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3]);
    set.Clear();

    Assert.Empty(set);
    Assert.False(set.Contains(1));
  }

  [Fact]
  public void Clear_WithoutItems_RaisesNoEvent()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.Clear();

    // Assert
    Assert.Empty(events);
  }

  [Fact]
  public void Clear_WithItems_RaisesResetEvent()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int> { 1, 2, 3 };
    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.Clear();

    // Assert
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Reset, events[0].Action);
  }

  [Fact]
  public void Contains_ExistingItem_ReturnsTrue()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    Assert.True(set.Contains(2));
  }

  [Fact]
  public void Contains_NonExistingItem_ReturnsFalse()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    Assert.False(set.Contains(99));
  }

  [Fact]
  public void IndexOf_ExistingItem_ReturnsCorrectIndex()
  {
    var set = new SynchronizedObservableOrderedSet<int> { 10, 20, 30 };

    Assert.Equal(0, set.IndexOf(10));
    Assert.Equal(1, set.IndexOf(20));
    Assert.Equal(2, set.IndexOf(30));
  }

  [Fact]
  public void IndexOf_NonExistingItem_ReturnsNegativeOne()
  {
    // ReSharper disable once CollectionNeverUpdated.Local
    var set = new SynchronizedObservableOrderedSet<int>();
    Assert.Equal(-1, set.IndexOf(99));
  }

  [Fact]
  public void IndexOf_Null_ReturnsNegativeOne()
  {
    // ReSharper disable once CollectionNeverUpdated.Local
    var set = new SynchronizedObservableOrderedSet<string>();
    Assert.Equal(-1, set.IndexOf(null!));
  }

  [Fact]
  public void CopyTo_CopiesItemsToArray()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    var array = new int[5];
    set.CopyTo(array, 1);

    Assert.Equal(0, array[0]);
    Assert.Equal(1, array[1]);
    Assert.Equal(2, array[2]);
    Assert.Equal(3, array[3]);
    Assert.Equal(0, array[4]);
  }

  [Fact]
  public void Move_ForwardMovesItem()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3, 4, 5]);

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.Move(0, 3);

    // Assert
    Assert.Equal(2, set[0]);
    Assert.Equal(3, set[1]);
    Assert.Equal(4, set[2]);
    Assert.Equal(1, set[3]);
    Assert.Equal(5, set[4]);
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Move, events[0].Action);
    Assert.Equal(0, events[0].OldStartingIndex);
    Assert.Equal(3, events[0].NewStartingIndex);
    Assert.Equal(1, events[0].OldItems![0]);
    Assert.Equal(1, events[0].NewItems![0]);
  }

  [Fact]
  public void Move_BackwardMovesItem()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3, 4, 5]);

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.Move(3, 1);

    // Assert
    Assert.Equal(1, set[0]);
    Assert.Equal(4, set[1]);
    Assert.Equal(2, set[2]);
    Assert.Equal(3, set[3]);
    Assert.Equal(5, set[4]);
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Move, events[0].Action);
    Assert.Equal(3, events[0].OldStartingIndex);
    Assert.Equal(1, events[0].NewStartingIndex);
    Assert.Equal(4, events[0].OldItems![0]);
    Assert.Equal(4, events[0].NewItems![0]);
  }

  [Fact]
  public void Move_SameIndex_DoesNothingNoEventFired()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3]);

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.Move(1, 1);

    // Assert
    Assert.Equal(1, set[0]);
    Assert.Equal(2, set[1]);
    Assert.Equal(3, set[2]);
    Assert.Empty(events);
  }

  [Fact]
  public void Move_UpdatesIndices()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([10, 20, 30]);

    // Act
    set.Move(0, 2);

    // Assert
    Assert.Equal(0, set.IndexOf(20));
    Assert.Equal(1, set.IndexOf(30));
    Assert.Equal(2, set.IndexOf(10));
  }

  [Fact]
  public void GetEnumerator_ReturnsSnapshot()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3]);

    var items = new List<int>();
    using var enumerator = set.GetEnumerator();

    // Act
    set.Add(4); // Modify while enumerating — should not throw

    while (enumerator.MoveNext())
    {
      items.Add(enumerator.Current);
    }

    // Assert
    Assert.Equal(new[] { 1, 2, 3 }, items); // Snapshot has original 3 items
    Assert.Equal(4, set.Count); // Collection itself has now 4
  }

  [Fact]
  public void GetEnumerator_EmptySet_ReturnsNoItems()
  {
    // ReSharper disable once CollectionNeverUpdated.Local
    var set = new SynchronizedObservableOrderedSet<int>();
    Assert.Empty(set.ToList());
  }

  [Fact]
  public void Foreach_Works()
  {
    var set = new SynchronizedObservableOrderedSet<int>([10, 20, 30]);
    var sum = 0;
    foreach (var item in set)
    {
      sum += item;
    }

    Assert.Equal(60, sum);
  }

  [Fact]
  public void UnionWith_AddsNewItems()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    set.UnionWith([3, 4, 5]);

    Assert.Equal(5, set.Count);
    Assert.Equal(new[] { 1, 2, 3, 4, 5 }, set.ToList());
  }

  [Fact]
  public void UnionWith_WithSelf_NoChanges()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    set.UnionWith(set);
    Assert.Equal(3, set.Count);
  }

  [Fact]
  public void UnionWith_WithOtherSynchronizedSet_MergesCorrectly()
  {
    var set1 = new SynchronizedObservableOrderedSet<int>([1, 2]);
    var set2 = new SynchronizedObservableOrderedSet<int>([2, 3]);
    set1.UnionWith(set2);

    Assert.Equal(3, set1.Count);
    Assert.Equal(new[] { 1, 2, 3 }, set1.ToList());
  }

  [Fact]
  public void UnionWith_WithNull_Throws()
  {
    // ReSharper disable once CollectionNeverQueried.Local
    var set = new SynchronizedObservableOrderedSet<int>();
    Assert.Throws<ArgumentNullException>(() => set.UnionWith(null!));
  }

  [Fact]
  public void ExceptWith_RemovesCommonItems()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3, 4, 5]);
    set.ExceptWith([2, 4]);

    Assert.Equal(3, set.Count);
    Assert.Equal(new[] { 1, 3, 5 }, set);
  }

  [Fact]
  public void ExceptWith_Self_ClearsSet()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    set.ExceptWith(set);
    Assert.Empty(set);
  }

  [Fact]
  public void ExceptWith_WithOtherSynchronizedSet_RemovesCorrectly()
  {
    var set1 = new SynchronizedObservableOrderedSet<int>([1, 2, 3, 4]);
    var set2 = new SynchronizedObservableOrderedSet<int>([2, 4]);
    set1.ExceptWith(set2);

    Assert.Equal(new[] { 1, 3 }, set1);
  }

  [Fact]
  public void ExceptWith_ThrowsOnNull()
  {
    // ReSharper disable once CollectionNeverUpdated.Local
    var set = new SynchronizedObservableOrderedSet<int>();
    Assert.Throws<ArgumentNullException>(() => set.ExceptWith(null!));
  }

  [Fact]
  public void IntersectWith_KeepsOnlyCommonItems()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3, 4, 5]);
    set.IntersectWith([2, 4, 6]);

    Assert.Equal(2, set.Count);
    Assert.Equal(new[] { 2, 4 }, set.ToList());
  }

  [Fact]
  public void IntersectWith_Self_NoChanges()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    set.IntersectWith(set);
    Assert.Equal(3, set.Count);
  }

  [Fact]
  public void IntersectWith_EmptySet_ClearsCollection()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    set.IntersectWith([]);
    Assert.Empty(set);
  }

  [Fact]
  public void IntersectWith_EmptyTarget_DoesNothing()
  {
    // ReSharper disable once CollectionNeverUpdated.Local
    var set = new SynchronizedObservableOrderedSet<int>();
    set.IntersectWith([1, 2, 3]);
    Assert.Empty(set);
  }

  [Fact]
  public void IntersectWith_WithOtherSynchronizedSet_IntersectsCorrectly()
  {
    var set1 = new SynchronizedObservableOrderedSet<int>([1, 2, 3, 4]);
    var set2 = new SynchronizedObservableOrderedSet<int>([2, 3, 5]);
    set1.IntersectWith(set2);

    Assert.Equal(new[] { 2, 3 }, set1.ToList());
  }

  [Fact]
  public void IntersectWith_ThrowsOnNull()
  {
    // ReSharper disable once CollectionNeverUpdated.Local
    var set = new SynchronizedObservableOrderedSet<int>();
    Assert.Throws<ArgumentNullException>(() => set.IntersectWith(null!));
  }

  [Fact]
  public void SymmetricExceptWith_ProducesDifference()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    set.SymmetricExceptWith([2, 3, 4]);

    Assert.Equal(2, set.Count);
    Assert.True(set.Contains(1));
    Assert.True(set.Contains(4));
    Assert.False(set.Contains(2));
    Assert.False(set.Contains(3));
  }

  [Fact]
  public void SymmetricExceptWith_Self_ClearsSet()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    set.SymmetricExceptWith(set);
    Assert.Empty(set);
  }

  [Fact]
  public void SymmetricExceptWith_WithDuplicateInput_DeduplicatesCorrectly()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    // 2 appears twice: should still only count as one removal
    set.SymmetricExceptWith([2, 2, 4]);

    Assert.Equal(3, set.Count);
    Assert.True(set.Contains(1));
    Assert.False(set.Contains(2));
    Assert.True(set.Contains(3));
    Assert.True(set.Contains(4));
  }

  [Fact]
  public void SymmetricExceptWith_WithOtherSynchronizedSet_WorksAsExpected()
  {
    var set1 = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    var set2 = new SynchronizedObservableOrderedSet<int>([2, 3, 4]);
    set1.SymmetricExceptWith(set2);

    Assert.True(set1.Contains(1));
    Assert.True(set1.Contains(4));
    Assert.False(set1.Contains(2));
    Assert.False(set1.Contains(3));
  }

  [Fact]
  public void SymmetricExceptWith_ThrowsOnNull()
  {
    // ReSharper disable once CollectionNeverQueried.Local
    var set = new SynchronizedObservableOrderedSet<int>();
    Assert.Throws<ArgumentNullException>(() => set.SymmetricExceptWith(null!));
  }

  [Fact]
  public void IsSubsetOf_ReturnsCorrectResult()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2]);
    Assert.True(set.IsSubsetOf([1, 2, 3]));
    Assert.True(set.IsSubsetOf([1, 2]));
    Assert.False(set.IsSubsetOf([1, 3]));
  }

  [Fact]
  public void IsSubsetOf_OtherSynchronizedSet()
  {
    var set1 = new SynchronizedObservableOrderedSet<int>([1, 2]);
    var set2 = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    Assert.True(set1.IsSubsetOf(set2));
  }

  [Fact]
  public void IsProperSubsetOf_ReturnsCorrectResult()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2]);
    Assert.True(set.IsProperSubsetOf([1, 2, 3]));
    Assert.False(set.IsProperSubsetOf([1, 2]));
  }

  [Fact]
  public void IsProperSubsetOf_OtherSynchronizedSet()
  {
    var set1 = new SynchronizedObservableOrderedSet<int>([1, 2]);
    var set2 = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    Assert.True(set1.IsProperSubsetOf(set2));

    var set3 = new SynchronizedObservableOrderedSet<int>([1, 2]);
    Assert.False(set1.IsProperSubsetOf(set3));
  }

  [Fact]
  public void IsSupersetOf_ReturnsCorrectResult()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    Assert.True(set.IsSupersetOf([1, 2]));
    Assert.True(set.IsSupersetOf([1, 2, 3]));
    Assert.False(set.IsSupersetOf([1, 4]));
  }

  [Fact]
  public void IsSupersetOf_OtherSynchronizedSet()
  {
    var set1 = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    var set2 = new SynchronizedObservableOrderedSet<int>([1, 2]);
    Assert.True(set1.IsSupersetOf(set2));
  }

  [Fact]
  public void IsProperSupersetOf_ReturnsCorrectResult()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    Assert.True(set.IsProperSupersetOf([1, 2]));
    Assert.False(set.IsProperSupersetOf([1, 2, 3]));
  }

  [Fact]
  public void IsProperSupersetOf_WithOtherSynchronizedSet_ReturnsCorrectResult()
  {
    var set1 = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    var set3 = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    var set2 = new SynchronizedObservableOrderedSet<int>([1, 2]);

    Assert.True(set1.IsProperSupersetOf(set2));
    Assert.False(set1.IsProperSupersetOf(set3));
  }

  [Fact]
  public void Overlaps_ReturnsCorrectResult()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    Assert.True(set.Overlaps([3, 4]));
    Assert.False(set.Overlaps([4, 5]));
  }

  [Fact]
  public void Overlaps_OtherSynchronizedSet()
  {
    var set1 = new SynchronizedObservableOrderedSet<int>([1, 2]);
    var set2 = new SynchronizedObservableOrderedSet<int>([2, 3]);
    Assert.True(set1.Overlaps(set2));

    var set3 = new SynchronizedObservableOrderedSet<int>([4, 5]);
    Assert.False(set1.Overlaps(set3));
  }

  [Fact]
  public void SetEquals_ReturnsCorrectResult()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    Assert.True(set.SetEquals([3, 2, 1]));
    Assert.False(set.SetEquals([1, 2]));
    Assert.False(set.SetEquals([1, 2, 3, 4]));
  }

  [Fact]
  public void SetEquals_OtherSynchronizedSet()
  {
    var set1 = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    var set2 = new SynchronizedObservableOrderedSet<int>([3, 1, 2]);
    var set3 = new SynchronizedObservableOrderedSet<int>([1, 2]);

    Assert.True(set1.SetEquals(set2));
    Assert.False(set1.SetEquals(set3));
  }

  [Fact]
  public void Add_RaisesCollectionChangedAdd()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.Add(42);

    // Assert
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Add, events[0].Action);
    Assert.Equal(42, events[0].NewItems![0]);
    Assert.Equal(0, events[0].NewStartingIndex);
  }

  [Fact]
  public void Add_Duplicate_NoEvent()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int> { 1 };

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.Add(1);

    // Assert
    Assert.Empty(events);
  }

  [Fact]
  public void AddRange_RaisesSingleAddEvent()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.AddRange([1, 2, 3]);

    // Assert
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Add, events[0].Action);
    Assert.Equal(3, events[0].NewItems!.Count);
    Assert.Equal(0, events[0].NewStartingIndex);
  }

  [Fact]
  public void Insert_RaisesCollectionChangedAddAtIndex()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int> { 1, 3 };

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.Insert(1, 2);

    // Assert
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Add, events[0].Action);
    Assert.Equal(2, events[0].NewItems![0]);
    Assert.Equal(1, events[0].NewStartingIndex);
  }

  [Fact]
  public void Remove_RaisesCollectionChangedRemove()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3]);

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.Remove(2);

    // Assert
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Remove, events[0].Action);
    Assert.Equal(2, events[0].OldItems![0]);
    Assert.Equal(1, events[0].OldStartingIndex);
  }

  [Fact]
  public void RemoveAt_RaisesCollectionChangedRemove()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([10, 20, 30]);

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.RemoveAt(0);

    // Assert
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Remove, events[0].Action);
    Assert.Equal(10, events[0].OldItems![0]);
    Assert.Equal(0, events[0].OldStartingIndex);
  }

  [Fact]
  public void Clear_RaisesCollectionChangedReset()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.Clear();

    // Assert
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Reset, events[0].Action);
  }

  [Fact]
  public void IndexerSet_RaisesCollectionChangedReplace()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int> { 1 };

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set[0] = 99;

    // Assert
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Replace, events[0].Action);
    Assert.Equal(99, events[0].NewItems![0]);
    Assert.Equal(1, events[0].OldItems![0]);
    Assert.Equal(0, events[0].NewStartingIndex);
  }

  [Fact]
  public void Move_RaisesCollectionChangedMove()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange([1, 2, 3]);

    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    // Act
    set.Move(0, 2);

    // Assert
    Assert.Single(events);
    Assert.Equal(NotifyCollectionChangedAction.Move, events[0].Action);
    Assert.Equal(1, events[0].OldItems![0]);
    Assert.Equal(0, events[0].OldStartingIndex);
    Assert.Equal(2, events[0].NewStartingIndex);
  }

  [Fact]
  public void Add_RaisesCountAndIndexerPropertyChanged()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    var propertyNames = new List<string>();
    set.PropertyChanged += (_, e) => propertyNames.Add(e.PropertyName!);

    // Act
    set.Add(1);

    // Assert
    Assert.Contains("Count", propertyNames);
    Assert.Contains("Item[]", propertyNames);
  }

  [Fact]
  public void Remove_RaisesCountAndIndexerPropertyChanged()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>([1]);
    var propertyNames = new List<string>();
    set.PropertyChanged += (_, e) => propertyNames.Add(e.PropertyName!);

    // Act
    set.Remove(1);

    // Assert
    Assert.Contains("Count", propertyNames);
    Assert.Contains("Item[]", propertyNames);
  }

  [Fact]
  public void IndexerSet_RaisesIndexerPropertyChanged_NotCount()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>([1]);
    var propertyNames = new List<string>();
    set.PropertyChanged += (_, e) => propertyNames.Add(e.PropertyName!);

    // Act
    set[0] = 99;

    // Assert
    Assert.Contains("Item[]", propertyNames);
    Assert.DoesNotContain("Count", propertyNames);
  }

  [Fact]
  public void Move_RaisesIndexerPropertyChanged_NotCount()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>([1, 2]);
    var propertyNames = new List<string>();
    set.PropertyChanged += (_, e) => propertyNames.Add(e.PropertyName!);

    // Act
    set.Move(0, 1);

    // Assert
    Assert.Contains("Item[]", propertyNames);
    Assert.DoesNotContain("Count", propertyNames);
  }

  [Fact]
  public void Reentrancy_ModifyingDuringCollectionChanged_Throws()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    set.CollectionChanged += (_, _) =>
    {
      // Attempt to modify during notification → should throw
      Assert.Throws<InvalidOperationException>(() => set.Add(999));
    };

    set.Add(1);
  }

  [Fact]
  public void Reentrancy_ModifyingDuringPropertyChanged_Throws()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    set.PropertyChanged += (_, _) =>
    {
      Assert.Throws<InvalidOperationException>(() => set.Add(999));
    };

    set.Add(1);
  }

  [Fact]
  public void Reentrancy_ReadDuringCollectionChanged_Succeeds()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    var readCount = -1;

    set.CollectionChanged += (_, _) =>
    {
      // Reading should work during notification (bypass flag)
      readCount = set.Count;
    };

    set.Add(1);
    Assert.Equal(1, readCount);
  }

  [Fact]
  public void Reentrancy_ContainsDuringCollectionChanged_Succeeds()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    var found = false;

    set.CollectionChanged += (_, _) =>
    {
      found = set.Contains(1);
    };

    set.Add(1);
    Assert.True(found);
  }

  [Fact]
  public void Reentrancy_IndexOfDuringCollectionChanged_Succeeds()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    var index = -1;

    set.CollectionChanged += (_, _) =>
    {
      index = set.IndexOf(42);
    };

    set.Add(42);
    Assert.Equal(0, index);
  }

  [Fact]
  public async Task ConcurrentAdds_NoDuplicates_NoCorruption()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    const int itemsPerTask = 500;
    const int taskCount = 4;

    var barrier = new Barrier(taskCount);
    var tasks = new Task[taskCount];

    // Act
    for (var index = 0; index < taskCount; index++)
    {
      var offset = index * itemsPerTask;
      tasks[index] = Task.Run(() =>
      {
        barrier.SignalAndWait();
        for (var itemIndex = 0; itemIndex < itemsPerTask; itemIndex++)
        {
          set.Add(offset + itemIndex);
        }
      });
    }

    await Task.WhenAll(tasks);

    // Assert (verify all items are present)
    Assert.Equal(itemsPerTask * taskCount, set.Count);

    for (var i = 0; i < itemsPerTask * taskCount; i++)
    {
      Assert.True(set.Contains(i), $"Missing item {i}");
    }
  }

  [Fact]
  public async Task ConcurrentAdds_AllSameItems_OnlyOneWins()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    const int taskCount = 8;

    var barrier = new Barrier(taskCount);
    var tasks = new Task<bool>[taskCount];

    for (var index = 0; index < taskCount; index++)
    {
      tasks[index] = Task.Run(() =>
      {
        barrier.SignalAndWait();
        return set.Add(42);
      });
    }

    await Task.WhenAll(tasks);

    Assert.Single(set);
    Assert.Equal(1, tasks.Count(x => x.Result));
  }

  [Fact]
  public async Task ConcurrentReadsAndWrites_NoExceptions()
  {
    var set = new SynchronizedObservableOrderedSet<int>();
    var exceptions = new List<Exception>();
    var stop = false;

    var writerTask = Task.Run(() =>
    {
      for (var i = 0; i < 1000; i++)
      {
        try
        {
          set.Add(i);
        }
        catch (Exception ex)
        {
          lock (exceptions)
          {
            exceptions.Add(ex);
          }
        }
      }

      stop = true;
    });

    var readerTasks = Enumerable.Range(0, 3).Select(x => Task.Run(() =>
    {
      while (!stop)
      {
        try
        {
          // Use methods and properties requiring a read lock:
          _ = set.Count;
          _ = set.Contains(42);
          if (set.Count > 0)
          {
            _ = set[0];
          }
        }
        catch (Exception ex)
        {
          lock (exceptions)
          {
            exceptions.Add(ex);
          }
        }
      }
    })).ToArray();

    await Task.WhenAll([writerTask, .. readerTasks]);

    Assert.Empty(exceptions);
    Assert.Equal(1000, set.Count);
  }

  [Fact]
  public async Task ConcurrentAddAndRemove_MaintainsConsistency()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    const int iterations = 500;

    // Act
    var adder = Task.Run(() =>
    {
      for (var index = 0; index < iterations; index++)
      {
        set.Add(index);
      }
    });

    var remover = Task.Run(() =>
    {
      for (var index = 0; index < iterations; index++)
      {
        set.Remove(index);
      }
    });

    await Task.WhenAll(adder, remover);

    // Assert
    // Check the Count property against the result produced by the enumerator:
    var count = set.Count;
    var listCount = set.ToList().Count;
    Assert.Equal(count, listCount);

    // Check containment of all expected items and if indexing them works as expected:
    for (var i = 0; i < iterations; i++)
    {
      if (!set.Contains(i))
      {
        continue;
      }

      var index = set.IndexOf(i);
      Assert.True(index >= 0);
      Assert.Equal(i, set[index]);
    }
  }

  [Fact]
  public async Task ConcurrentEnumeration_DuringModification_UsesSnapshot()
  {
    // Arrange
    var set = new SynchronizedObservableOrderedSet<int>();
    set.AddRange(Enumerable.Range(0, 100));
    var barrier = new Barrier(2);

    // Act/Arrange
    var enumerationTask = Task.Run(() =>
    {
      barrier.SignalAndWait(); // Wait until modifyTask also started
      var snapshot = set.ToList();
      Assert.True(snapshot.Count > 0);
    });

    var modifyTask = Task.Run(() =>
    {
      barrier.SignalAndWait(); // Wait until enumerationTask also started
      for (var i = 100; i < 200; i++)
      {
        set.Add(i);
      }
    });

    await Task.WhenAll(enumerationTask, modifyTask);
  }

  [Fact]
  public void Add_ExplicitAsIList_ReturnsIndexOrNegativeOne()
  {
    // Arrange
    IList list = new SynchronizedObservableOrderedSet<int>();

    // Act
    var index1 = list.Add(1);
    var index2 = list.Add(1); // Duplicate
    var index3 = list.Add(null); // Null ignored

    // Assert
    Assert.Equal(0, index1);
    Assert.Equal(-1, index2);
    Assert.Equal(-1, index3);
  }

  [Fact]
  public void Contains_ExplicitAsIList_WorksWithBoxedValues()
  {
    IList list = new SynchronizedObservableOrderedSet<int>();
    list.Add(42);
    Assert.True(list.Contains(42));
    Assert.False(list.Contains(99));
    Assert.False(list.Contains(null));
  }

  [Fact]
  public void IndexOf_ExplicitAsIList_WorksWithBoxedValues()
  {
    IList list = new SynchronizedObservableOrderedSet<int>();
    list.Add(10);
    list.Add(20);

    Assert.Equal(0, list.IndexOf(10));
    Assert.Equal(1, list.IndexOf(20));
    Assert.Equal(-1, list.IndexOf(null));
  }

  [Fact]
  public void Insert_ExplicitAsIList_Works()
  {
    IList list = new SynchronizedObservableOrderedSet<int>();
    list.Add(1);
    list.Add(3);
    list.Insert(1, 2);

    Assert.Equal(1, list[0]);
    Assert.Equal(2, list[1]);
    Assert.Equal(3, list[2]);
  }

  [Fact]
  public void Insert_ExplicitAsIListIList_NullThrows()
  {
    // ReSharper disable once CollectionNeverQueried.Local
    IList list = new SynchronizedObservableOrderedSet<int>();
    Assert.Throws<ArgumentNullException>(() => list.Insert(0, null));
  }

  [Fact]
  public void Remove_ExplicitAsIList_Works()
  {
    IList list = new SynchronizedObservableOrderedSet<int>();
    list.Add(1);
    list.Add(2);
    list.Remove(1);
    list.Remove(null); // Always ignored

    Assert.Single(list);
    Assert.Equal(2, list[0]);
  }

  [Fact]
  public void IndexerSet_ExplicitAsIList_Works()
  {
    IList list = new SynchronizedObservableOrderedSet<int>();
    list.Add(1);
    list[0] = 99;

    Assert.Equal(99, list[0]);
  }

  [Fact]
  public void IndexerSet_ExplicitAsIList_SetNullThrows()
  {
    // ReSharper disable once CollectionNeverQueried.Local
    IList list = new SynchronizedObservableOrderedSet<string>();
    list.Add("a");
    Assert.Throws<ArgumentNullException>(() => list[0] = null);
  }

  [Fact]
  public void IsReadOnly_ExplicitAsIList_ReturnsFalse()
  {
    // ReSharper disable once CollectionNeverUpdated.Local
    IList list = new SynchronizedObservableOrderedSet<int>();
    Assert.False(list.IsReadOnly);
  }

  [Fact]
  public void IsFixedSize_ExplicitAsIList_ReturnsFalse()
  {
    // ReSharper disable once CollectionNeverUpdated.Local
    IList list = new SynchronizedObservableOrderedSet<int>();
    Assert.False(list.IsFixedSize);
  }

  [Fact]
  public void IsSynchronized_ExplicitAsICollection_ReturnsTrue()
  {
    // ReSharper disable once CollectionNeverUpdated.Local
    ICollection collection = new SynchronizedObservableOrderedSet<int>();
    Assert.True(collection.IsSynchronized);
  }

  [Fact]
  public void SyncRoot_ExplicitAsICollection_NotNull()
  {
    // ReSharper disable once CollectionNeverUpdated.Local
    ICollection collection = new SynchronizedObservableOrderedSet<int>();
    Assert.NotNull(collection.SyncRoot);
  }

  [Fact]
  public void ICollection_CopyTo_CopiesItems()
  {
    ICollection col = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    var array = new int[3];
    col.CopyTo(array, 0);
    Assert.Equal(new[] { 1, 2, 3 }, array);
  }

  [Fact]
  public void IsReadOnly_ExplicitAsICollectionT_ReturnsFalse()
  {
    // ReSharper disable once CollectionNeverUpdated.Local
    ICollection<int> collection = new SynchronizedObservableOrderedSet<int>();
    Assert.False(collection.IsReadOnly);
  }

  [Fact]
  public void Add_ExplicitAsICollectionT_AddsItem()
  {
    ICollection<int> col = new SynchronizedObservableOrderedSet<int>();
    col.Add(1);
    Assert.Single(col);
  }

  [Fact]
  public void IReadOnlySetMethods_ExplicitAsIReadOnlySet_MethodsDelegateCorrectly()
  {
    IReadOnlySet<int> readOnlySet = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);

    Assert.True(readOnlySet.IsSubsetOf([1, 2, 3, 4]));
    Assert.True(readOnlySet.IsSupersetOf([1, 2]));
    Assert.True(readOnlySet.IsProperSubsetOf([1, 2, 3, 4]));
    Assert.True(readOnlySet.IsProperSupersetOf([1, 2]));
    Assert.True(readOnlySet.Overlaps([3, 4]));
    Assert.True(readOnlySet.SetEquals([3, 2, 1]));
    Assert.True(readOnlySet.Contains(2));
  }

  [Fact]
  public void IndexerGet_OutOfRange_Throws()
  {
    // ReSharper disable once CollectionNeverUpdated.Local
    var set = new SynchronizedObservableOrderedSet<int>();
    Assert.Throws<ArgumentOutOfRangeException>(() => _ = set[0]);
  }

  [Fact]
  public void Remove_FromEmpty_ReturnsFalse()
  {
    // ReSharper disable once CollectionNeverUpdated.Local
    var set = new SynchronizedObservableOrderedSet<int>();
    Assert.False(set.Remove(1));
  }

  [Fact]
  public void AddRange_AllDuplicates_ReturnsZeroNoEventFired()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    var added = set.AddRange([1, 2, 3]);

    Assert.Equal(0, added);
    Assert.Empty(events);
  }

  [Fact]
  public void RemoveRange_ByItems_AllNullsReturnsZero()
  {
    var set = new SynchronizedObservableOrderedSet<string>(["a", "b"]);
    var removed = set.RemoveRange([null!, null!]);
    Assert.Equal(0, removed);
  }

  [Fact]
  public void LargeCollection_MaintainsOrderAndUniqueness()
  {
    const int count = 10000;
    var set = new SynchronizedObservableOrderedSet<int>();

    for (var index = 0; index < count; index++)
    {
      set.Add(index);
    }

    Assert.Equal(count, set.Count);

    for (var index = 0; index < count; index++)
    {
      Assert.Equal(index, set[index]);
      Assert.Equal(index, set.IndexOf(index));
    }
  }

  [Fact]
  public void InsertRange_WithAllDuplicates_NoModificationNoEvent()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    var events = new List<NotifyCollectionChangedEventArgs>();
    set.CollectionChanged += (_, args) => events.Add(args);

    var inserted = set.InsertRange(0, [1, 2, 3]);

    Assert.Equal(0, inserted);
    Assert.Empty(events);
  }

  [Fact]
  public void ClearAndReAdd_WorksCorrectly()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    set.Clear();
    set.Add(1);

    Assert.Single(set);
    Assert.Equal(0, set.IndexOf(1));
    Assert.True(set.Contains(1));
  }

  [Fact]
  public async Task CrossSetOperations_ConcurrentOppositeDirections_NoDeadlock()
  {
    // Tests the WriteReadLockScope ordered locking: a.ExceptWith(b) + b.ExceptWith(a)
    var setA = new SynchronizedObservableOrderedSet<int>([1, 2, 3, 4, 5]);
    var setB = new SynchronizedObservableOrderedSet<int>([3, 4, 5, 6, 7]);

    var barrier = new Barrier(2);
    var task1 = Task.Run(() =>
    {
      barrier.SignalAndWait();
      setA.ExceptWith(setB);
    });

    var task2 = Task.Run(() =>
    {
      barrier.SignalAndWait();
      setB.ExceptWith(setA);
    });

    await Task
      .WhenAll(task1, task2)
      .WaitAsync(TimeSpan.FromSeconds(5));

    // WhenAll re-throws any task exception; TimeoutException fails the test on deadlock
  }

  [Fact]
  public async Task CrossSetOperations_UnionWithBothDirections_NoDeadlock()
  {
    var setA = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    var setB = new SynchronizedObservableOrderedSet<int>([4, 5, 6]);

    var barrier = new Barrier(2);
    var task1 = Task.Run(() =>
    {
      barrier.SignalAndWait();
      setA.UnionWith(setB);
    });

    var task2 = Task.Run(() =>
    {
      barrier.SignalAndWait();
      setB.UnionWith(setA);
    });

    await Task
      .WhenAll(task1, task2)
      .WaitAsync(TimeSpan.FromSeconds(5));

    // WhenAll re-throws any task exception; TimeoutException fails the test on deadlock
  }

  [Fact]
  public void CollectionChanged_EventArgs_ReflectActualStateOnAdd()
  {
    var set = new SynchronizedObservableOrderedSet<int>();

    set.CollectionChanged += (sender, args) =>
    {
      if (args.Action != NotifyCollectionChangedAction.Add)
      {
        Assert.Fail("Unexpected event action.");
      }

      // The item should already be in the set at event time
      var self = (SynchronizedObservableOrderedSet<int>)sender!;
      Assert.Single(self);
      Assert.True(self.Contains(42));
      Assert.Equal(42, self[0]);
    };

    set.Add(42);
  }

  [Fact]
  public void CollectionChanged_EventArgs_ReflectActualStateOnRemove()
  {
    var set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);

    set.CollectionChanged += (sender, args) =>
    {
      if (args.Action != NotifyCollectionChangedAction.Remove)
      {
        Assert.Fail("Unexpected event action.");
        return;
      }

      var self = (SynchronizedObservableOrderedSet<int>)sender!;
      Assert.Equal(2, self.Count);
      Assert.False(self.Contains(2));
    };

    set.Remove(2);
  }

  [Fact]
  public void NonGenericEnumerator_Works()
  {
    IEnumerable set = new SynchronizedObservableOrderedSet<int>([1, 2, 3]);
    var items = new List<int>();

    foreach (int item in set)
    {
      items.Add(item);
    }

    Assert.Equal(new[] { 1, 2, 3 }, items);
  }
}

#pragma warning restore xUnit2017 // Do not use Contains() to check if a value exists in a collection