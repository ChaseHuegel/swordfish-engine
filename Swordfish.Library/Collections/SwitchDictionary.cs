using System.Collections.Generic;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Collections;

public class SwitchDictionary<TKey1, TKey2, TValue>
{
    private readonly LinkedDictionary<TKey1, TKey2> _link = new();

    private readonly Dictionary<TKey1, TValue> _dictionary = new();

    public TValue this[TKey1 key1]
    {
        get => _dictionary[key1];
        set => _dictionary[key1] = value;
    }

    public TValue this[TKey2 key2]
    {
        get => _dictionary[_link[key2]];
        set => _dictionary[_link[key2]] = value;
    }

    public int Count => _dictionary.Count;

    public void Add(TKey2 key2, TKey1 key1, TValue value) => Add(key1, key2, value);

    public void Add(TKey1 key1, TKey2 key2, TValue value)
    {
        _dictionary.Add(key1, value);
        _link.Add(key1, key2);
    }

    public void Clear()
    {
        _dictionary.Clear();
        _link.Clear();
    }

    public bool ContainsKey(TKey1 key1) => _dictionary.ContainsKey(key1);

    public bool ContainsKey(TKey2 key2) => _dictionary.ContainsKey(_link[key2]);

    public bool ContainsValue(TValue value) => _dictionary.ContainsValue(value);

    public bool Remove(TKey1 key1)
    {
        if (!_link.Remove(key1))
        {
            return false;
        }

        _dictionary.Remove(key1);
        return true;
    }

    public bool Remove(TKey2 key2)
    {
        TKey1 key1 = _link[key2];
        if (!_link.Remove(key2))
        {
            return false;
        }

        _dictionary.Remove(key1);
        return true;
    }

    public bool TryGetValue(TKey1 key1, TKey2 key2, out TValue value) => TryGetValue(key1, out value) || TryGetValue(key2, out value);

    public bool TryGetValue(TKey2 key2, TKey1 key1, out TValue value) => TryGetValue(key2, out value) || TryGetValue(key1, out value);

    public bool TryGetValue(TKey1 key1, out TValue value) => _dictionary.TryGetValue(key1, out value);

    public bool TryGetValue(TKey2 key2, out TValue value) => _dictionary.TryGetValue(_link[key2], out value);

}