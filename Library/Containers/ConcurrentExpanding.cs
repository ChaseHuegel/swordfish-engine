using System;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Swordfish.Library.Containers
{
    /// <summary>
    /// Represents a thread-safe non-shrinking typed list
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ConcurrentExpanding<T> : IEnumerable
    {
        private ReaderWriterLockSlim listLock = new ReaderWriterLockSlim();
        private T[] array = new T[1];

        //  Deconstructor
        ~ConcurrentExpanding()
        {
            if (listLock != null) listLock.Dispose();
        }

        /// <summary>
        /// Number of indices in the list
        /// </summary>
        public int Count = 0;

        /// <summary>
        /// Try adding an element to the list, ignoring duplicates
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryAdd(T value)
        {
            if (!Contains(value))
            {
                Add(value);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Add an element to the list
        /// </summary>
        /// <param name="value"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T value)
        {
            listLock.EnterWriteLock();
            try
            {
                if (array.Length == Count)
                    Array.Resize(ref array, array.Length << 1);

                array[Count++] = value;
            }
            finally
            {
                listLock.ExitWriteLock();
            }
        }

        /// <summary>
        /// Check if the list contains an element
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T value)
        {
            listLock.EnterReadLock();

            try
            {
                foreach (T entry in array)
                    if (entry != null && entry.Equals(value))
                        return true;
            }
            finally
            {
                listLock.ExitReadLock();
            }

            return false;
        }

        //  Indexer
        public T this[int index]
        {
            get
            {
                listLock.EnterReadLock();
                try { return array[index];  }
                finally { listLock.ExitReadLock(); }
            }

            set
            {
                listLock.EnterWriteLock();
                try { array[index] = value; }
                finally { listLock.ExitWriteLock();  }
            }
        }

        //  Enumerator
        IEnumerator IEnumerable.GetEnumerator() => (IEnumerator)GetEnumerator();
        public ExpandingListEnum GetEnumerator() => new ExpandingListEnum(array);

        public class ExpandingListEnum : IEnumerator
        {
            private T[] array = new T[1];
            private int position = -1;

            public ExpandingListEnum(T[] array)
            {
                this.array = array;
            }

            public bool MoveNext()
            {
                position++;
                return position < array.Length;
            }

            public void Reset()
            {
                position = -1;
            }

            object IEnumerator.Current
            {
                get => Current;
            }

            public T Current
            {
                get
                {
                    try { return array[position]; }
                    catch (IndexOutOfRangeException)
                    {
                        throw new InvalidOperationException();
                    }
                }
            }
        }
    }
}
