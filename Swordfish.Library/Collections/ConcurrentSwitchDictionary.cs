using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Swordfish.Library.Collections
{
    public class ConcurrentSwitchDictionary<TKey1, TKey2, TValue>
    {
        private ConcurrentLinkedDictionary<TKey1, TKey2> link = new ConcurrentLinkedDictionary<TKey1, TKey2>();

        private ConcurrentDictionary<TKey1, TValue> dictonary = new ConcurrentDictionary<TKey1, TValue>();

        public TValue this[TKey1 key1]
        {
            get => dictonary[key1];
            set => dictonary[key1] = value;
        }

        public TValue this[TKey2 key2]
        {
            get => dictonary[link[key2]];
            set => dictonary[link[key2]] = value;
        }

        public int Count => dictonary.Count;

        public bool TryAdd(TKey2 key2, TKey1 key1, TValue value) => TryAdd(key1, key2, value);

        public bool TryAdd(TKey1 key1, TKey2 key2, TValue value)
        {
            return dictonary.TryAdd(key1, value) && link.TryAdd(key1, key2);
        }

        public void Clear()
        {
            dictonary.Clear();
            link.Clear();
        }

        public bool ContainsKey(TKey1 key1) => dictonary.ContainsKey(key1);

        public bool ContainsKey(TKey2 key2) => dictonary.ContainsKey(link[key2]);

        public bool TryRemove(TKey1 key1)
        {
            if (link.TryRemove(key1))
            {
                return dictonary.TryRemove(key1, out _);
            }

            return false;
        }

        public bool TryRemove(TKey2 key2)
        {
            TKey1 key1 = link[key2];
            if (link.TryRemove(key2))
            {
                return dictonary.TryRemove(key1, out _);
            }

            return false;
        }

        public bool TryGetValue(TKey1 key1, TKey2 key2, out TValue value) => TryGetValue(key1, out value) || TryGetValue(key2, out value);

        public bool TryGetValue(TKey2 key2, TKey1 key1, out TValue value) => TryGetValue(key2, out value) || TryGetValue(key1, out value);

        public bool TryGetValue(TKey1 key1, out TValue value) => dictonary.TryGetValue(key1, out value);

        public bool TryGetValue(TKey2 key2, out TValue value) => dictonary.TryGetValue(link[key2], out value);

    }
}
