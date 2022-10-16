using System;

namespace Swordfish.Library.Collections
{
    public class IndexLookup<TKey> where TKey : class
    {
        public int Count { get; private set; }

        private TKey[] Keys;

        public IndexLookup()
        {
            Keys = new TKey[1];
        }

        public IndexLookup(int size)
        {
            Keys = new TKey[size];
        }

        public bool Add(TKey key)
        {
            if (Keys.Length == Count)
                Array.Resize(ref Keys, Keys.Length * 2);

            for (int i = 0; i < Keys.Length; i++)
            {
                if (Keys[i] is null)
                {
                    Keys[i] = key;
                    Count++;
                    return true;
                }
            }

            return false;
        }

        public bool Remove(TKey key)
        {
            for (int i = 0; i < Keys.Length; i++)
            {
                if (Keys[i]?.Equals(key) ?? false)
                {
                    Keys[i] = null;
                    Count--;
                    return true;
                }
            }

            return false;
        }

        public int IndexOf(TKey key)
        {
            for (int i = 0; i < Keys.Length; i++)
                if (Keys[i]?.Equals(key) ?? false)
                    return i;

            return -1;
        }

        public bool Contains(TKey key)
        {
            for (int i = 0; i < Keys.Length; i++)
                if (Keys[i]?.Equals(key) ?? false)
                    return true;

            return false;
        }
    }
}
