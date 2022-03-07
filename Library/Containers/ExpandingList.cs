using System;
using System.Collections;
using System.Runtime.CompilerServices;

namespace Swordfish.Library.Containers
{
    /// <summary>
    /// Represents a non-shrinking typed list
    /// </summary>
    /// <typeparam name="T"></typeparam>
    public class ExpandingList<T> : IEnumerable
    {
        private T[] array = new T[1];

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
            if (array.Length == Count)
                Array.Resize(ref array, array.Length << 1);

            array[Count++] = value;
        }

        /// <summary>
        /// Check if the list contains an element
        /// </summary>
        /// <param name="value"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Contains(T value)
        {
            foreach (T entry in array)
                if (entry != null && entry.Equals(value))
                    return true;

            return false;
        }

        //  Indexer
        public T this[int index]
        {
            get => array[index];
            set => array[index] = value;
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
