using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Swordfish.Library.Collections;

public class LockedObservableCollection<T> : Collection<T>, INotifyCollectionChanged, IEnumerable
{
    private readonly ObservableCollection<T> _observableCollection;

    public new int Count
    {
        get
        {
            lock (_observableCollection)
            {
                return _observableCollection.Count;
            }
        }
    }

    public bool IsReadOnly => false;

    public event NotifyCollectionChangedEventHandler CollectionChanged;

    public LockedObservableCollection()
    {
        _observableCollection = new ObservableCollection<T>();
        _observableCollection.CollectionChanged += OnCollectinChanged;
    }

    private void OnCollectinChanged(object sender, NotifyCollectionChangedEventArgs e)
    {
        CollectionChanged?.Invoke(sender, e);
    }

    public LockedObservableCollection(IList<T> list)
    {
        _observableCollection = new ObservableCollection<T>(list);
    }

    public new T this[int index]
    {
        get
        {
            lock (_observableCollection)
            {
                return _observableCollection[index];
            }
        }
        set
        {
            lock (_observableCollection)
            {
                _observableCollection[index] = value;
            }
        }
    }

    public new void Add(T item)
    {
        lock (_observableCollection)
        {
            _observableCollection.Add(item);
        }
    }

    public new void Clear()
    {
        lock (_observableCollection)
        {
            _observableCollection.Clear();
        }
    }

    public new bool Contains(T item)
    {
        lock (_observableCollection)
        {
            return _observableCollection.Contains(item);
        }
    }

    public new void CopyTo(T[] array, int arrayIndex)
    {
        lock (_observableCollection)
        {
            _observableCollection.CopyTo(array, arrayIndex);
        }
    }

    public void ForEach(Action<T> action)
    {
        lock (_observableCollection)
        {
            foreach (T item in _observableCollection)
            {
                action.Invoke(item);
            }
        }
    }

    public new IEnumerator<T> GetEnumerator()
    {
        lock (_observableCollection)
        {
            return _observableCollection.GetEnumerator();
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public new int IndexOf(T item)
    {
        lock (_observableCollection)
        {
            return _observableCollection.IndexOf(item);
        }
    }

    public new void Insert(int index, T item)
    {
        lock (_observableCollection)
        {
            _observableCollection.Insert(index, item);
        }
    }

    public new bool Remove(T item)
    {
        lock (_observableCollection)
        {
            return _observableCollection.Remove(item);
        }
    }

    public new void RemoveAt(int index)
    {
        lock (_observableCollection)
        {
            _observableCollection.RemoveAt(index);
        }
    }
}