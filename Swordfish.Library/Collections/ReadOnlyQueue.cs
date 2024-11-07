using System.Collections.Generic;
// ReSharper disable UnusedMember.Global

namespace Swordfish.Library.Collections;

public class ReadOnlyQueue<T>(params T[] values)
{
    private readonly Queue<T> _queue = new(values);

    public T Take()
    {
        return _queue.Dequeue();
    }

    public T Peek()
    {
        return _queue.Peek();
    }

    public T[] TakeAll()
    {
        var values = new T[_queue.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = _queue.Dequeue();
        }

        return values;
    }
}