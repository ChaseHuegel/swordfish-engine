using System.Collections;
using System.Collections.Generic;

namespace Swordfish.Library.Collections
{
    public class LockedList<T> : IList<T>
    {
        private readonly object Lock = new object();
        private readonly List<T> List = new List<T>();

        public T this[int index]
        {
            get
            {
                lock (Lock)
                {
                    return List[index];
                }
            }
            set
            {
                lock (Lock)
                {
                    List[index] = value;
                }
            }
        }

        public int Count
        {
            get
            {
                lock (Lock)
                {
                    return List.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            lock (Lock)
            {
                List.Add(item);
            }
        }

        public void Clear()
        {
            lock (Lock)
            {
                List.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (Lock)
            {
                return List.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (Lock)
            {
                //  ! This can throw, there is a race here
                List.CopyTo(array, arrayIndex);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (Lock)
            {
                return List.GetEnumerator();
            }
        }

        public int IndexOf(T item)
        {
            lock (Lock)
            {
                return List.IndexOf(item);
            }
        }

        public void Insert(int index, T item)
        {
            lock (Lock)
            {
                List.Insert(index, item);
            }
        }

        public bool Remove(T item)
        {
            lock (Lock)
            {
                return List.Remove(item);
            }
        }

        public void RemoveAt(int index)
        {
            lock (Lock)
            {
                List.RemoveAt(index);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (Lock)
            {
                return List.GetEnumerator();
            }
        }
    }
}