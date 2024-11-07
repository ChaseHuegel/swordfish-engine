using System;
using System.Collections;
using System.Runtime.CompilerServices;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Collections;

/// <summary>
/// Represents a non-shrinking typed list
/// </summary>
/// <typeparam name="T"></typeparam>
// ReSharper disable once UnusedType.Global
public class ExpandingList<T> : IEnumerable
{
    private T[] _array = new T[1];

    public int Count;

    /// <summary>
    /// Try adding an element to the list, ignoring duplicates
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(T value)
    {
        if (Contains(value))
        {
            return false;
        }

        Add(value);
        return true;

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
    private ExpandingListEnumerator GetEnumerator() => new(_array);

    private class ExpandingListEnumerator(in T[] array) : IEnumerator
    {
        private readonly T[] _array = array;
        private int _position = -1;

        public bool MoveNext()
        {
            _position++;
            return _position < _array.Length;
        }

        public void Reset()
        {
            _position = -1;
        }

        object IEnumerator.Current => Current;

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