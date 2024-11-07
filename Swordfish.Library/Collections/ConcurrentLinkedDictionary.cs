using System.Collections.Concurrent;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Collections;

public class ConcurrentLinkedDictionary<TKey1, TKey2>
{
    private readonly ConcurrentDictionary<TKey1, TKey2> _dictionary = new();

    private readonly ConcurrentDictionary<TKey2, TKey1> _flippedDictionary = new();

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

    public bool TryAdd(TKey2 key, TKey1 value) => TryAdd(value, key);

    public bool TryAdd(TKey1 key, TKey2 value)
    {
        if (!_dictionary.TryAdd(key, value))
        {
            return false;
        }

        return _flippedDictionary.TryAdd(value, key) || _dictionary.TryRemove(key, out _);
    }

    public void Clear()
    {
        _dictionary.Clear();
        _flippedDictionary.Clear();
    }

    public bool Contains(TKey1 key) => _dictionary.ContainsKey(key);

    public bool Contains(TKey2 key) => _flippedDictionary.ContainsKey(key);

    public bool TryRemove(TKey1 key)
    {
        return _flippedDictionary.TryRemove(_dictionary[key], out _) && _dictionary.TryRemove(key, out _);
    }

    public bool TryRemove(TKey2 key)
    {
        return _dictionary.TryRemove(_flippedDictionary[key], out _) && _flippedDictionary.TryRemove(key, out _);
    }

    public bool TryGetValue(TKey1 key, out TKey2 value) => _dictionary.TryGetValue(key, out value);

    public bool TryGetValue(TKey2 key, out TKey1 value) => _flippedDictionary.TryGetValue(key, out value);

}