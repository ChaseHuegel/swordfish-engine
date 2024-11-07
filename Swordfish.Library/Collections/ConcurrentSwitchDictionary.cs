using System.Collections.Concurrent;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Collections;

public class ConcurrentSwitchDictionary<TKey1, TKey2, TValue>
{
    private readonly ConcurrentLinkedDictionary<TKey1, TKey2> _link = new();

    private readonly ConcurrentDictionary<TKey1, TValue> _dictionary = new();

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

    public bool TryAdd(TKey2 key2, TKey1 key1, TValue value) => TryAdd(key1, key2, value);

    public bool TryAdd(TKey1 key1, TKey2 key2, TValue value)
    {
        return _dictionary.TryAdd(key1, value) && _link.TryAdd(key1, key2);
    }

    public void Clear()
    {
        _dictionary.Clear();
        _link.Clear();
    }

    public bool ContainsKey(TKey1 key1) => _dictionary.ContainsKey(key1);

    public bool ContainsKey(TKey2 key2) => _dictionary.ContainsKey(_link[key2]);

    public bool TryRemove(TKey1 key1)
    {
        if (_link.TryRemove(key1))
        {
            return _dictionary.TryRemove(key1, out _);
        }

        return false;
    }

    public bool TryRemove(TKey2 key2)
    {
        TKey1 key1 = _link[key2];
        if (_link.TryRemove(key2))
        {
            return _dictionary.TryRemove(key1, out _);
        }

        return false;
    }

    public bool TryGetValue(TKey1 key1, TKey2 key2, out TValue value) => TryGetValue(key1, out value) || TryGetValue(key2, out value);

    public bool TryGetValue(TKey2 key2, TKey1 key1, out TValue value) => TryGetValue(key2, out value) || TryGetValue(key1, out value);

    public bool TryGetValue(TKey1 key1, out TValue value) => _dictionary.TryGetValue(key1, out value);

    public bool TryGetValue(TKey2 key2, out TValue value) => _dictionary.TryGetValue(_link[key2], out value);

}