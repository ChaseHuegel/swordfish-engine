using System;
using System.Collections.Generic;

namespace Swordfish.Library.Extensions
{
    public static class DictionaryExtensions
    {
        public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> valueFactory)
        {
            if (dictionary.TryGetValue(key, out TValue value))
                return value;

            value = valueFactory.Invoke();
            dictionary.Add(key, value);
            return value;
        }
    }
}
