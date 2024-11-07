using System;

namespace Swordfish.Library.Collections;

public class IndexLookup<TKey> where TKey : class
{
    public int Count { get; private set; }

    private TKey[] _keys;

    public IndexLookup()
    {
        _keys = new TKey[1];
    }

    public IndexLookup(int size)
    {
        _keys = new TKey[size];
    }

    public bool Add(TKey key)
    {
        if (_keys.Length == Count)
        {
            Array.Resize(ref _keys, _keys.Length * 2);
        }

        for (var i = 0; i < _keys.Length; i++)
        {
            if (_keys[i] is null)
            {
                _keys[i] = key;
                Count++;
                return true;
            }
        }

        return false;
    }

    public bool Remove(TKey key)
    {
        for (var i = 0; i < _keys.Length; i++)
        {
            if (_keys[i]?.Equals(key) ?? false)
            {
                _keys[i] = null;
                Count--;
                return true;
            }
        }

        return false;
    }

    public int IndexOf(TKey key)
    {
        for (var i = 0; i < _keys.Length; i++)
        {
            if (_keys[i]?.Equals(key) ?? false)
            {
                return i;
            }
        }

        return -1;
    }

    public bool Contains(TKey key)
    {
        for (var i = 0; i < _keys.Length; i++)
        {
            if (_keys[i]?.Equals(key) ?? false)
            {
                return true;
            }
        }

        return false;
    }
}