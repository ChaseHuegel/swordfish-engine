using System.Collections.Generic;

namespace Swordfish.Library.Collections;

public sealed class DoubleList<T>
{
    private readonly object _swapLock = new();
    private readonly List<T>[] _buffers = [[], []];
    private int _bufferIndex;

    public void Write(T item)
    {
        lock (_swapLock)
        {
            _buffers[_bufferIndex].Add(item);
        }
    }

    public void Swap()
    {
        lock (_swapLock)
        {
            _bufferIndex = (_bufferIndex + 1) % 2;
        }
    }
    
    public void Clear()
    {
        lock (_swapLock)
        {
            _buffers[_bufferIndex].Clear();
        }
    }

    public T[] Read()
    {
        lock (_swapLock)
        {
            return _buffers[(_bufferIndex + 1) % 2].ToArray();
        }
    }
}