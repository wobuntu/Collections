using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;


namespace Wobuntu.Collections
{
  public class ObservableWrappingCollection<TWrapped> : INotifyCollectionChanged, INotifyPropertyChanged, IList
  {
    #region Constructors
    public ObservableWrappingCollection(Func<object, TWrapped> transformer, IEqualityComparer<TWrapped> equalityComparer = null)
    {
      _transform = transformer ?? throw new ArgumentNullException(nameof(transformer));
      _equalityComparer = equalityComparer ?? EqualityComparer<TWrapped>.Default;
      _items = new ObservableCollection<TWrapped>();
    }
    #endregion

    // TODO LATER: Add other constructor overloads / convenience methods for lists

    #region Privates
    private readonly ObservableCollection<TWrapped> _items;

    private readonly Func<object, TWrapped> _transform;

    private readonly IEqualityComparer<TWrapped> _equalityComparer;
    #endregion


    #region IEnumerable implementation
    public IEnumerator<TWrapped> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    #endregion


    #region ICollection, IList implementation (preferred one)
    public void CopyTo(Array array, int index) => _items.CopyTo((TWrapped[])array, index);

    public int Add(object toWrap)
    {
      int pos = _items.Count;
      if (!(toWrap is TWrapped wrapped))
      {
        wrapped = _transform(toWrap);
      }

      _items.Add(wrapped);
      return pos;
    }

    public bool IsSynchronized => ((ICollection)_items).IsSynchronized;

    public object SyncRoot => ((ICollection)_items).SyncRoot;

    public void RemoveAt(int index) => _items.RemoveAt(index);

    public bool IsFixedSize => ((IList)_items).IsFixedSize;

    bool IList.IsReadOnly => ((IList)_items).IsReadOnly;

    object IList.this[int index]
    {
      get => _items[index];
      set
      {
        if (!(value is TWrapped wrapped))
        {
          wrapped = _transform(value);
        }

        _items[index] = wrapped;
      }
    }

    public int Count => _items.Count;

    public void Clear() => _items.Clear();

    public bool Contains(object value)
    {
      if (!(value is TWrapped wrapped))
      {
        wrapped = _transform(value);
      }

      for (int i = 0; i < _items.Count; i++)
      {
        if (_equalityComparer.Equals(wrapped, _items[i]))
        {
          return true;
        }
      }

      return false;
    }

    public int IndexOf(object value)
    {
      if (!(value is TWrapped wrapped))
      {
        wrapped = _transform(value);
      }

      for (int i = 0; i < _items.Count; i++)
      {
        if (_equalityComparer.Equals(wrapped, _items[i]))
        {
          return i;
        }
      }

      return -1;
    }

    public void Insert(int index, object value)
    {
      if (!(value is TWrapped wrapped))
      {
        wrapped = _transform(value);
      }

      _items.Insert(index, wrapped);
    }

    public void Remove(object value)
    {
      if (!(value is TWrapped wrapped))
      {
        wrapped = _transform(value);
      }

      var index = IndexOf(wrapped);
      if (index >= 0)
      {
        _items.RemoveAt(index);
      }
    }
    #endregion


    #region Additional ICollection, IList methods for convenience
    public void Move(int oldIndex, int newIndex) => _items.Move(oldIndex, newIndex);
    #endregion


    #region ICollection<TWrapped>, IList<TWrapped> implementation
    //bool ICollection<TWrapped>.Remove(TWrapped item)
    //{
    //  var index = ((IList<TWrapped>)this).IndexOf(item);
    //  if (index < 0)
    //  {
    //    return false;
    //  }

    //  _items.RemoveAt(index);
    //  return true;
    //}

    //bool ICollection<TWrapped>.IsReadOnly => false;

    //void ICollection<TWrapped>.Add(TWrapped item) => _items.Add(item);

    //void ICollection<TWrapped>.Clear() => _items.Clear();

    //bool ICollection<TWrapped>.Contains(TWrapped item)
    //{
    //  for (int i = 0; i < _items.Count; i++)
    //  {
    //    if (_equalityComparer.Equals(item, _items[i]))
    //    {
    //      return true;
    //    }
    //  }

    //  return false;
    //}

    public int IndexOf(TWrapped item)
    {
      for (int i = 0; i < _items.Count; i++)
      {
        if (_equalityComparer.Equals(_items[i], item))
        {
          return i;
        }
      }

      return -1;
    }

    //void IList<TWrapped>.Insert(int index, TWrapped item) => _items.Insert(index, item);

    public TWrapped this[int index] { get => _items[index]; set => _items[index] = value; }

    public void CopyTo(TWrapped[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    #endregion


    #region INotifyCollectionChanged, INotifyPropertyChanged implementation
    private int _collectionChangedSubscriberCount;

    private event NotifyCollectionChangedEventHandler RedirectedCollectionChanged;

    public event NotifyCollectionChangedEventHandler CollectionChanged
    {
      add
      {
        lock (_items)
        {
          // This collection is not thread safe, but anyways, this part is crucial and an damage to much to just not care.
          if (_collectionChangedSubscriberCount == 0)
          {
            _items.CollectionChanged += OnCollectionChangedRedirect;
          }

          _collectionChangedSubscriberCount++;
          RedirectedCollectionChanged += value;
        }
      }
      remove
      {
        lock (_items)
        {
          _collectionChangedSubscriberCount--;
          if (_collectionChangedSubscriberCount <= 0)
          {
            // Be sure our object can be properly cleared by the garbage collector
            _items.CollectionChanged -= OnCollectionChangedRedirect;
          }

          RedirectedCollectionChanged -= value;
        }
      }
    }

    public bool CollectionChangedEventsDisabled { get; set; }

    private void OnCollectionChangedRedirect(object sender, NotifyCollectionChangedEventArgs args)
    {
      if (CollectionChangedEventsDisabled)
      {
        return;
      }

      // This redirect simply changes the sender from being the internal collection to the current class.
      RedirectedCollectionChanged?.Invoke(this, args);
    }


    protected void FireCollectionChanged(NotifyCollectionChangedEventArgs args)
      => RedirectedCollectionChanged?.Invoke(this, args);

    private bool _innerSubscriptionAssigned;

    private event PropertyChangedEventHandler RedirectedPropertyChanged;

    public event PropertyChangedEventHandler PropertyChanged
    {
      add
      {
        lock (_items)
        {
          // This collection is not thread safe, but anyways, this part is crucial and an damage to much to just not care.
          if (!_innerSubscriptionAssigned)
          {
            ((INotifyPropertyChanged)_items).PropertyChanged += OnPropertyChangedRedirect;
            _innerSubscriptionAssigned = true;
          }

          RedirectedPropertyChanged += value;
        }
      }
      remove
      {
        lock (_items)
        {
          if (_innerSubscriptionAssigned)
          {
            // Be sure our object can be properly cleared by the garbage collector
            ((INotifyPropertyChanged)_items).PropertyChanged -= OnPropertyChangedRedirect;
            _innerSubscriptionAssigned = false;
          }

          RedirectedPropertyChanged -= value;
        }
      }
    }

    public bool PropertyChangedUpdatesDisabled { get; set; }

    private void OnPropertyChangedRedirect(object sender, PropertyChangedEventArgs args)
    {
      if (PropertyChangedUpdatesDisabled)
      {
        return;
      }

      // This redirect simply changes the sender from being the internal collection to the current class.
      RedirectedPropertyChanged?.Invoke(this, args);
    }

    protected void FirePropertyChanged(PropertyChangedEventArgs args)
      => RedirectedPropertyChanged?.Invoke(this, args);
    #endregion
  }
}
