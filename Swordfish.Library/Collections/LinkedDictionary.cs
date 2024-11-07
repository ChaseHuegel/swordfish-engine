using System.Collections.Generic;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Collections;

public class LinkedDictionary<TKey1, TKey2>
{
    private readonly Dictionary<TKey1, TKey2> _dictionary = new();

    private readonly Dictionary<TKey2, TKey1> _flippedDictionary = new();

    public TKey2 this[TKey1 key1]
    {
        get => _dictionary[key1];
        set => _dictionary[key1] = value;
    }

    public TKey1 this[TKey2 key2]
    {
        get => _flippedDictionary[key2];
        set => _flippedDictionary[key2] = value;
    }

    public int Count => _dictionary.Count;

    public void Add(TKey2 key, TKey1 value) => Add(value, key);

    public void Add(TKey1 key, TKey2 value)
    {
        _dictionary.Add(key, value);
        _flippedDictionary.Add(value, key);
    }

    public void Clear()
    {
        _dictionary.Clear();
        _flippedDictionary.Clear();
    }

    public bool Contains(TKey1 key) => _dictionary.ContainsKey(key);

    public bool Contains(TKey2 key) => _flippedDictionary.ContainsKey(key);

    public bool Remove(TKey1 key)
    {
        if (!_flippedDictionary.Remove(_dictionary[key]))
        {
            return false;
        }

        _dictionary.Remove(key);
        return true;
    }

    public bool Remove(TKey2 key)
    {
        if (!_dictionary.Remove(_flippedDictionary[key]))
        {
            return false;
        }

        _flippedDictionary.Remove(key);
        return true;
    }

    public bool TryGetValue(TKey1 key, out TKey2 value) => _dictionary.TryGetValue(key, out value);

    public bool TryGetValue(TKey2 key, out TKey1 value) => _flippedDictionary.TryGetValue(key, out value);

}