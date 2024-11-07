using System;
using System.Collections.Generic;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Extensions;

// ReSharper disable once UnusedType.Global
public static class DictionaryExtensions
{
    public static TValue GetOrAdd<TKey, TValue>(this IDictionary<TKey, TValue> dictionary, TKey key, Func<TValue> valueFactory)
    {
        if (dictionary.TryGetValue(key, out TValue value))
        {
            return value;
        }

        value = valueFactory.Invoke();
        dictionary.Add(key, value);
        return value;
    }
}