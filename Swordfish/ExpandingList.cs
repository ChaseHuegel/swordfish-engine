using System;
using System.Runtime.CompilerServices;

namespace Swordfish
{
    public class ExpandingList<T>
    {
        private T[] array = new T[1];
        public int Count = 0;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Add(T value)
        {
            if (array.Length == Count)
            {
                Array.Resize(ref array, array.Length << 1);
            }

            array[Count++] = value;
        }

        public T this[int index]
        {
            get => array[index];
            set => array[index] = value;
        }
    }
}
