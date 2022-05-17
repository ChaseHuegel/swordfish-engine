using System.Collections.Generic;

namespace Swordfish.Library.Types
{
    public class SwitchDictionary<TKey1, TKey2, TValue>
    {
        private LinkedDictionary<TKey1, TKey2> link = new LinkedDictionary<TKey1, TKey2>();

        private Dictionary<TKey1, TValue> dictonary = new Dictionary<TKey1, TValue>();

        public TValue this[TKey1 key1] {
            get => dictonary[key1];
            set => dictonary[key1] = value;
        }

        public TValue this[TKey2 key2] {
            get => dictonary[link[key2]];
            set => dictonary[link[key2]] = value;
        }

        public int Count => dictonary.Count;

        public void Add(TKey2 key2, TKey1 key1, TValue value) => Add(key1, key2, value);

        public void Add(TKey1 key1, TKey2 key2, TValue value)
        {
            dictonary.Add(key1, value);
            link.Add(key1, key2);
        }

        public void Clear()
        {
            dictonary.Clear();
            link.Clear();
        }
        
        public bool ContainsKey(TKey1 key1) => dictonary.ContainsKey(key1);

        public bool ContainsKey(TKey2 key2) => dictonary.ContainsKey(link[key2]);

        public bool ContainsValue(TValue value) => dictonary.ContainsValue(value);

        public bool Remove(TKey1 key1)
        {
            if (link.Remove(key1))
            {
                dictonary.Remove(key1);
                return true;
            }

            return false;
        }

        public bool Remove(TKey2 key2)
        {
            TKey1 key1 = link[key2];
            if (link.Remove(key2))
            {
                dictonary.Remove(key1);
                return true;
            }

            return false;
        }

        public bool TryGetValue(TKey1 key1, out TValue value) => dictonary.TryGetValue(key1, out value);

        public bool TryGetValue(TKey2 key2, out TValue value) => dictonary.TryGetValue(link[key2], out value);

    }
}
