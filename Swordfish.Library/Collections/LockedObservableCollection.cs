using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;

namespace Swordfish.Library.Collections
{
    public class LockedObservableCollection<T> : Collection<T>, INotifyCollectionChanged, IEnumerable
    {
        private readonly ObservableCollection<T> ObservableCollection;

        public new int Count
        {
            get
            {
                lock (ObservableCollection)
                {
                    return ObservableCollection.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        public event NotifyCollectionChangedEventHandler CollectionChanged;

        public LockedObservableCollection()
        {
            ObservableCollection = new ObservableCollection<T>();
            ObservableCollection.CollectionChanged += OnCollectinChanged;
        }

        private void OnCollectinChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            CollectionChanged?.Invoke(sender, e);
        }

        public LockedObservableCollection(IList<T> list)
        {
            ObservableCollection = new ObservableCollection<T>(list);
        }

        public new T this[int index]
        {
            get
            {
                lock (ObservableCollection)
                {
                    return ObservableCollection[index];
                }
            }
            set
            {
                lock (ObservableCollection)
                {
                    ObservableCollection[index] = value;
                }
            }
        }

        public new void Add(T item)
        {
            lock (ObservableCollection)
            {
                ObservableCollection.Add(item);
            }
        }

        public new void Clear()
        {
            lock (ObservableCollection)
            {
                ObservableCollection.Clear();
            }
        }

        public new bool Contains(T item)
        {
            lock (ObservableCollection)
            {
                return ObservableCollection.Contains(item);
            }
        }

        public new void CopyTo(T[] array, int arrayIndex)
        {
            lock (ObservableCollection)
            {
                ObservableCollection.CopyTo(array, arrayIndex);
            }
        }

        public void ForEach(Action<T> action)
        {
            lock (ObservableCollection)
            {
                foreach (T item in ObservableCollection)
                {
                    action.Invoke(item);
                }
            }
        }

        public new IEnumerator<T> GetEnumerator()
        {
            lock (ObservableCollection)
            {
                return ObservableCollection.GetEnumerator();
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public new int IndexOf(T item)
        {
            lock (ObservableCollection)
            {
                return ObservableCollection.IndexOf(item);
            }
        }

        public new void Insert(int index, T item)
        {
            lock (ObservableCollection)
            {
                ObservableCollection.Insert(index, item);
            }
        }

        public new bool Remove(T item)
        {
            lock (ObservableCollection)
            {
                return ObservableCollection.Remove(item);
            }
        }

        public new void RemoveAt(int index)
        {
            lock (ObservableCollection)
            {
                ObservableCollection.RemoveAt(index);
            }
        }
    }
}
