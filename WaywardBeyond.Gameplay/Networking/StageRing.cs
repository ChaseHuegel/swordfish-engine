using System;

namespace WaywardBeyond.Gameplay.Networking;

/// <summary>A fixed-capacity ring of staged values, oldest first. Not thread-safe.</summary>
public sealed class StageRing<T>(int capacity)
{
    private readonly T[] _entries = capacity > 0 ? new T[capacity] : throw new ArgumentOutOfRangeException(nameof(capacity));
    private ulong _head;
    private ulong _tail;

    /// <summary>The number of staged values.</summary>
    public int Count => (int)(_head - _tail);

    /// <summary>Returns the value at <paramref name="index"/>, where zero is the oldest.</summary>
    public ref T this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
            {
                throw new ArgumentOutOfRangeException(nameof(index));
            }

            return ref _entries[(int)((_tail + (ulong)index) % (ulong)_entries.Length)];
        }
    }

    /// <summary>Stages one value, dropping the oldest when the ring is full.</summary>
    public void Add(in T value)
    {
        _entries[(int)(_head % (ulong)_entries.Length)] = value;
        _head++;

        if (_head - _tail > (ulong)_entries.Length)
        {
            _tail = _head - (ulong)_entries.Length;
        }
    }

    /// <summary>Drops the oldest <paramref name="count"/> values.</summary>
    public void Advance(int count)
    {
        _tail = Math.Min(_tail + (ulong)Math.Max(0, count), _head);
    }

    /// <summary>Drops every staged value.</summary>
    public void Clear()
    {
        _head = 0;
        _tail = 0;
    }
}
