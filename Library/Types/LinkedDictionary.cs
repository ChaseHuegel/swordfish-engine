using System.Collections.Generic;

namespace Swordfish.Library.Types
{
    public class LinkedDictionary<TKey1, TKey2>
    {
        private Dictionary<TKey1, TKey2> dictionary = new Dictionary<TKey1, TKey2>();

        private Dictionary<TKey2, TKey1> flippedDictionary = new Dictionary<TKey2, TKey1>();

        public TKey2 this[TKey1 key1] {
            get => dictionary[key1];
            set => dictionary[key1] = value;
        }

        public TKey1 this[TKey2 key2] {
            get => flippedDictionary[key2];
            set => flippedDictionary[key2] = value;
        }

        public int Count => dictionary.Count;

        public void Add(TKey2 key, TKey1 value) => Add(value, key);

        public void Add(TKey1 key, TKey2 value)
        {
            dictionary.Add(key, value);
            flippedDictionary.Add(value, key);
        }

        public void Clear()
        {
            dictionary.Clear();
            flippedDictionary.Clear();
        }
        
        public bool Contains(TKey1 key) => dictionary.ContainsKey(key);

        public bool Contains(TKey2 key) => flippedDictionary.ContainsKey(key);

        public bool Remove(TKey1 key)
        {
            if (flippedDictionary.Remove(dictionary[key]))
            {
                dictionary.Remove(key);
                return true;
            }

            return false;
        }

        public bool Remove(TKey2 key)
        {
            if (dictionary.Remove(flippedDictionary[key]))
            {
                flippedDictionary.Remove(key);
                return true;
            }

            return false;
        }

        public bool TryGetValue(TKey1 key, out TKey2 value) => dictionary.TryGetValue(key, out value);

        public bool TryGetValue(TKey2 key, out TKey1 value) => flippedDictionary.TryGetValue(key, out value);

    }
}
