using System;
using System.Collections;
using System.Runtime.CompilerServices;

namespace Swordfish.Library.Collections;

/// <summary>
/// Represents a non-shrinking typed list
/// </summary>
/// <typeparam name="T"></typeparam>
public class ExpandingList<T> : IEnumerable
{
    private T[] _array = new T[1];

    /// <summary>
    /// Number of indices in the list
    /// </summary>
    public int Count = 0;

    /// <summary>
    /// Try adding an element to the list, ignoring duplicates
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(T value)
    {
        if (!Contains(value))
        {
            Add(value);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Add an element to the list
    /// </summary>
    /// <param name="value"></param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(T value)
    {
        if (_array.Length == Count)
        {
            Array.Resize(ref _array, _array.Length * 2);
        }

        _array[Count++] = value;
    }

    /// <summary>
    /// Check if the list contains an element
    /// </summary>
    /// <param name="value"></param>
    /// <returns></returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Contains(T value)
    {
        foreach (T entry in _array)
        {
            if (entry != null && entry.Equals(value))
            {
                return true;
            }
        }

        return false;
    }

    //  Indexer
    public T this[int index]
    {
        get => _array[index];
        set => _array[index] = value;
    }

    //  Enumerator
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    public ExpandingListEnum GetEnumerator() => new(_array);

    public class ExpandingListEnum : IEnumerator
    {
        private T[] _array = new T[1];
        private int _position = -1;

        public ExpandingListEnum(T[] array)
        {
            _array = array;
        }

        public bool MoveNext()
        {
            _position++;
            return _position < _array.Length;
        }

        public void Reset()
        {
            _position = -1;
        }

        object IEnumerator.Current
        {
            get => Current;
        }

        public T Current
        {
            get
            {
                try { return _array[_position]; }
                catch (IndexOutOfRangeException)
                {
                    throw new InvalidOperationException();
                }
            }
        }
    }
}