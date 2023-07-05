using System.Collections.Concurrent;
using System.Collections.Generic;

namespace Swordfish.Library.Collections
{
    public class ConcurrentLinkedDictionary<TKey1, TKey2>
    {
        private ConcurrentDictionary<TKey1, TKey2> dictionary = new ConcurrentDictionary<TKey1, TKey2>();

        private ConcurrentDictionary<TKey2, TKey1> flippedDictionary = new ConcurrentDictionary<TKey2, TKey1>();

        public TKey2 this[TKey1 key1]
        {
            get => dictionary[key1];
            set => dictionary[key1] = value;
        }

        public TKey1 this[TKey2 key2]
        {
            get => flippedDictionary[key2];
            set => flippedDictionary[key2] = value;
        }

        public int Count => dictionary.Count;

        public bool TryAdd(TKey2 key, TKey1 value) => TryAdd(value, key);

        public bool TryAdd(TKey1 key, TKey2 value)
        {
            if (dictionary.TryAdd(key, value))
            {
                if (!flippedDictionary.TryAdd(value, key)) {
                    return dictionary.TryRemove(key, out _);
                }
                
                return true;
            }

            return false;
        }

        public void Clear()
        {
            dictionary.Clear();
            flippedDictionary.Clear();
        }

        public bool Contains(TKey1 key) => dictionary.ContainsKey(key);

        public bool Contains(TKey2 key) => flippedDictionary.ContainsKey(key);

        public bool TryRemove(TKey1 key)
        {
            if (flippedDictionary.TryRemove(dictionary[key], out _))
            {
                return dictionary.TryRemove(key, out _);
            }

            return false;
        }

        public bool TryRemove(TKey2 key)
        {
            if (dictionary.TryRemove(flippedDictionary[key], out _))
            {
                return flippedDictionary.TryRemove(key, out _);
            }

            return false;
        }

        public bool TryGetValue(TKey1 key, out TKey2 value) => dictionary.TryGetValue(key, out value);

        public bool TryGetValue(TKey2 key, out TKey1 value) => flippedDictionary.TryGetValue(key, out value);

    }
}
