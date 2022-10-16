using System;
using System.Collections;
using System.Collections.Generic;

namespace Swordfish.Library.Collections
{
    public class LockedList<T> : IList<T>
    {
        private readonly List<T> List = new List<T>();

        public T this[int index]
        {
            get
            {
                lock (List)
                {
                    return List[index];
                }
            }
            set
            {
                lock (List)
                {
                    List[index] = value;
                }
            }
        }

        public int Count
        {
            get
            {
                lock (List)
                {
                    return List.Count;
                }
            }
        }

        public bool IsReadOnly => false;

        public void Add(T item)
        {
            lock (List)
            {
                List.Add(item);
            }
        }

        public void Clear()
        {
            lock (List)
            {
                List.Clear();
            }
        }

        public bool Contains(T item)
        {
            lock (List)
            {
                return List.Contains(item);
            }
        }

        public void CopyTo(T[] array, int arrayIndex)
        {
            lock (List)
            {
                List.CopyTo(array, arrayIndex);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            lock (List)
            {
                return List.GetEnumerator();
            }
        }

        public int IndexOf(T item)
        {
            lock (List)
            {
                return List.IndexOf(item);
            }
        }

        public void Insert(int index, T item)
        {
            lock (List)
            {
                List.Insert(index, item);
            }
        }

        public bool Remove(T item)
        {
            lock (List)
            {
                return List.Remove(item);
            }
        }

        public void RemoveAt(int index)
        {
            lock (List)
            {
                List.RemoveAt(index);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            lock (List)
            {
                return List.GetEnumerator();
            }
        }
    }
}