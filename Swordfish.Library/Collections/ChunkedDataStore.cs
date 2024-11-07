using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Swordfish.Library.Collections;

public class ChunkedDataStore
{
    public const int NULL_PTR = -1;

    public int Size { get; }
    public int ChunkSize { get; }
    public int Count { get; private set; }

    private readonly object[] _data;
    private volatile int _highestPtr;
    private volatile int _lowestPtr;
    private volatile int _secondLowestPtr;
    private readonly Queue<int> _recycledPtrs;
    private readonly int _chunkOffset;

    public ChunkedDataStore(int size, int chunkSize)
    {
        if (chunkSize < 1)
        {
            throw new ArgumentException($"{nameof(chunkSize)} must be greater than 0.");
        }

        Size = size;
        ChunkSize = chunkSize;
        _chunkOffset = chunkSize + 1;

        _data = new object[Size * _chunkOffset];

        Count = 0;
        _highestPtr = 0;
        _lowestPtr = 0;
        _secondLowestPtr = 0;
        _recycledPtrs = new Queue<int>();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Add(int ptr = NULL_PTR)
    {
        if (ptr == NULL_PTR)
        {
            ptr = AllocatePtr();
        }

        _data[ptr * _chunkOffset] = true;
        for (var i = 1; i <= ChunkSize; i++)
        {
            _data[ptr * _chunkOffset + i] = null;
        }

        return ptr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Add(object[] data, int ptr = NULL_PTR)
    {
        if (data.Length != ChunkSize)
        {
            throw new ArgumentException("Data length must be equal to chunk count.");
        }

        if (ptr == NULL_PTR)
        {
            ptr = AllocatePtr();
        }

        _data[ptr * _chunkOffset] = true;
        for (var i = 0; i < ChunkSize; i++)
        {
            _data[ptr * _chunkOffset + i + 1] = data[i];
        }

        return ptr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int Add(Dictionary<int, object> chunks, int ptr = NULL_PTR)
    {
        if (ptr == NULL_PTR)
        {
            ptr = AllocatePtr();
        }

        _data[ptr * _chunkOffset] = true;
        for (var i = 1; i <= ChunkSize; i++)
        {
            _data[ptr * _chunkOffset + i] = chunks.TryGetValue(i, out object chunk) ? chunk : null;
        }

        return ptr;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Set(int ptr, int chunkIndex, object chunk)
    {
        _data[ptr * _chunkOffset + chunkIndex + 1] = chunk;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Remove(int ptr)
    {
        if (ptr == NULL_PTR)
        {
            throw new NullReferenceException();
        }

        _data[ptr * _chunkOffset] = null;
        for (var i = 1; i <= ChunkSize; i++)
        {
            _data[ptr * _chunkOffset + i] = null;
        }

        FreePtr(ptr);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object[] Get(int ptr)
    {
        if (ptr == NULL_PTR)
        {
            throw new NullReferenceException();
        }

        var chunks = new object[ChunkSize];
        Array.Copy(_data, ptr * _chunkOffset + 1, chunks, 0, ChunkSize);
        return chunks;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public object GetAt(int ptr, int chunkIndex)
    {
        if (ptr == NULL_PTR)
        {
            throw new NullReferenceException();
        }

        return _data[ptr * _chunkOffset + chunkIndex + 1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public T GetAt<T>(int ptr, int chunkIndex)
    {
        if (ptr == NULL_PTR)
        {
            throw new NullReferenceException();
        }

        return (T)_data[ptr * _chunkOffset + chunkIndex + 1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ref object GetRefAt(int ptr, int chunkIndex)
    {
        if (ptr == NULL_PTR)
        {
            throw new NullReferenceException();
        }

        return ref _data[ptr * _chunkOffset + chunkIndex + 1];
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has(int ptr)
    {
        if (ptr == NULL_PTR)
        {
            throw new NullReferenceException();
        }

        return _data[ptr * _chunkOffset] is true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool HasAt(int ptr, int chunkIndex)
    {
        if (ptr == NULL_PTR)
        {
            throw new NullReferenceException();
        }

        int index = ptr * _chunkOffset + chunkIndex + 1;
        return _data[index] != null;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int[] All()
    {
        var ptrs = new int[Count];
        var ptrIndex = 0;
        for (int i = _lowestPtr; i < _highestPtr; i++)
        {
            //  TODO this can throw index out of range without ptrIndex < length.
            //  ! This is a bandaid hiding a race issue that results in not all entities being rendered when hit
            if (_data[i * _chunkOffset] != null && ptrIndex < ptrs.Length)
            {
                ptrs[ptrIndex++] = i;
            }
        }

        return ptrs;
    }

    public void ForEach(int ptr, Action<object> action)
    {
        if (ptr == NULL_PTR)
        {
            throw new NullReferenceException();
        }

        for (var i = 1; i <= ChunkSize; i++)
        {
            action.Invoke(_data[ptr * _chunkOffset + i]);
        }
    }

    public void ForEachOf<T>(int chunkIndex, Action<T> action)
    {
        for (int i = _lowestPtr; i < _highestPtr; i++)
        {
            int ptr = i * _chunkOffset + chunkIndex + 1;
            if (_data[ptr] != null)
            {
                action.Invoke((T)_data[ptr]);
            }
        }
    }

    public IEnumerable<object> EnumerateAt(int ptr)
    {
        if (ptr == NULL_PTR)
        {
            throw new NullReferenceException();
        }

        ptr = ptr * _chunkOffset;
        for (var i = 1; i <= ChunkSize; i++)
        {
            yield return _data[ptr + i];
        }
    }

    public IEnumerable<DataPtr<T>> EnumerateEachOf<T>(int chunkIndex)
    {
        for (int ptr = _lowestPtr; ptr < _highestPtr; ptr++)
        {
            int dataIndex = ptr * _chunkOffset + chunkIndex + 1;
            object data = _data[dataIndex];
            if (data != null)
            {
                yield return new DataPtr<T>(ptr, (T)data);
            }
        }
    }

    private int AllocatePtr()
    {
        bool anyRecycledPtrs = _recycledPtrs.Count != 0;

        if (_highestPtr > Size && !anyRecycledPtrs)
        {
            throw new OutOfMemoryException($"Exceeded maximum chunk allocations ({Size}).");
        }

        int ptr = anyRecycledPtrs ? _recycledPtrs.Dequeue() : _highestPtr++;

        if (ptr < _lowestPtr)
        {
            _lowestPtr = ptr;
        }

        Count++;
        return ptr;
    }

    private void FreePtr(int ptr)
    {
        _recycledPtrs.Enqueue(ptr);
        Count--;

        if (_secondLowestPtr < _lowestPtr)
        {
            _secondLowestPtr = _lowestPtr;
        }

        if (ptr == _lowestPtr)
        {
            _lowestPtr = _secondLowestPtr == ptr ? _lowestPtr + 1 : _secondLowestPtr;
        }

        if (_secondLowestPtr < ptr)
        {
            _secondLowestPtr = ptr;
        }

        if (ptr == _secondLowestPtr)
        {
            _secondLowestPtr++;
        }
    }
}