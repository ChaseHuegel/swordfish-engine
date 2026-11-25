using System.Buffers;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace Krill;

internal abstract class ChunkedStore
{
    public abstract bool IsDisposable { get; }

    public abstract void SetAt(int chunkIndex, int localEntity, bool exists);

    public abstract bool TryGetAt(int chunkIndex, int localEntity, out IDataComponent data);

    public abstract bool IsDirty(int chunkIndex, int localEntity);

    public abstract void MarkDirty(int chunkIndex, int localEntity);

    public abstract void ClearDirty(int chunkIndex, int localEntity);

    public abstract void ClearRemoved(int entity);

    public abstract void DisposeRemoved(object syncRoot);

    protected delegate void RefAction<T>(ref T value);

    protected static RefAction<T>? BindDispose<T>() where T : struct, IDataComponent
    {
        if (!typeof(IDisposable).IsAssignableFrom(typeof(T)))
        {
            return null;
        }

        MethodInfo method = typeof(DisposableDispatch<>)
            .MakeGenericType(typeof(T))
            .GetMethod(nameof(DisposableDispatch<DummyDisposable>.Dispose), BindingFlags.Public | BindingFlags.Static)!;

        return method.CreateDelegate<RefAction<T>>();
    }

    private static class DisposableDispatch<T> where T : struct, IDisposable
    {
        public static void Dispose(ref T value)
        {
            value.Dispose();
        }
    }

    private struct DummyDisposable : IDisposable
    {
        public void Dispose()
        {
        }
    }
}

internal class ChunkedStore<T>(in int chunkSize) : ChunkedStore where T : struct, IDataComponent
{
    private static readonly RefAction<T>? _dispose = BindDispose<T>();

    public readonly List<Chunk<T>?> Chunks = [];

    /// <summary>Last values of unacknowledged removals, keyed by entity index. Allocated on first removal.</summary>
    public Dictionary<int, T>? Removed;

    private readonly int _chunkSize = chunkSize;

    public override bool IsDisposable => _dispose != null;

    public override void SetAt(int chunkIndex, int localEntity, bool exists)
    {
        if (Chunks.Count > chunkIndex && Chunks[chunkIndex] is Chunk<T> chunk)
        {
            SetAt(chunkIndex, localEntity, chunk.Components[localEntity], exists);
        }
    }

    public void SetAt(int chunkIndex, int localEntity, T data, bool exists)
    {
        Chunk<T> chunk;
        if (Chunks.Count <= chunkIndex)
        {
            if (exists)
            {
                //  Pad with nulls so the chunk lands at the correct index
                while (Chunks.Count <= chunkIndex)
                {
                    Chunks.Add(null);
                }

                chunk = new Chunk<T>(_chunkSize);
                Chunks[chunkIndex] = chunk;
            }
            else
            {
                //  Don't do anything if setting exists=false and a chunk doesn't exist here
                return;
            }
        }
        else
        {
            chunk = Chunks[chunkIndex] ??= new Chunk<T>(_chunkSize);
        }

        bool wasExists = (chunk.Flags[localEntity] & DataFlags.EXISTS) != 0;
        byte flags = exists
            ? (byte)(chunk.Flags[localEntity] | DataFlags.EXISTS | DataFlags.DIRTY)
            : (byte)((chunk.Flags[localEntity] & ~DataFlags.EXISTS) | DataFlags.DIRTY);

        //  Capture the removed value sparsely by entity index. A recycled entity index may take this
        //  component again before a cleanup pass runs, and the live value must not overwrite
        //  the pending removal. ClearRemoved acknowledges it and releases the capture.
        if (wasExists && !exists)
        {
            Removed ??= [];
            Removed[chunkIndex * _chunkSize + localEntity] = chunk.Components[localEntity];
        }

        chunk.Components[localEntity] = exists ? data : default;
        chunk.Flags[localEntity] = flags;
        
        //  Only adjust the count when existence actually transitions
        if (wasExists != exists)
        {
            chunk.Count += exists ? 1 : -1;
        }
        //  Track the exclusive upper bound of the used slot range so queries can
        //  iterate only over slots that have ever held a component.
        if (exists)
        {
            chunk.HighWater = Math.Max(chunk.HighWater, localEntity + 1);
        }
        //  TODO should chunks get cleaned up when they are empty?
    }

    public ref T GetAtRef(int chunkIndex, int localEntity)
    {
        return ref Chunks[chunkIndex]!.Components[localEntity];
    }

    public override bool TryGetAt(int chunkIndex, int localEntity, out IDataComponent data)
    {
        if (Chunks.Count <= chunkIndex)
        {
            data = default!;
            return false;
        }

        Chunk<T>? chunk = Chunks[chunkIndex];
        if (chunk is null)
        {
            data = default!;
            return false;
        }

        data = chunk.Components[localEntity];
        return (chunk.Flags[localEntity] & DataFlags.EXISTS) != 0;
    }

    public bool TryGetAt(int chunkIndex, int localEntity, out T data)
    {
        if (Chunks.Count <= chunkIndex)
        {
            data = default!;
            return false;
        }

        Chunk<T>? chunk = Chunks[chunkIndex];
        if (chunk is null)
        {
            data = default!;
            return false;
        }

        data = chunk.Components[localEntity];
        return (chunk.Flags[localEntity] & DataFlags.EXISTS) != 0;
    }

    public override bool IsDirty(int chunkIndex, int localEntity)
    {
        if (Chunks.Count <= chunkIndex)
        {
            return false;
        }

        Chunk<T>? chunk = Chunks[chunkIndex];
        if (chunk is null)
        {
            return false;
        }

        return (chunk.Flags[localEntity] & DataFlags.DIRTY) != 0;
    }

    public override void MarkDirty(int chunkIndex, int localEntity)
    {
        if (Chunks.Count <= chunkIndex)
        {
            return;
        }

        Chunk<T>? chunk = Chunks[chunkIndex];
        if (chunk is null)
        {
            return;
        }

        chunk.Flags[localEntity] |= DataFlags.DIRTY;
    }

    public override void ClearDirty(int chunkIndex, int localEntity)
    {
        if (Chunks.Count <= chunkIndex)
        {
            return;
        }

        Chunk<T>? chunk = Chunks[chunkIndex];
        if (chunk is null)
        {
            return;
        }

        chunk.Flags[localEntity] = (byte)(chunk.Flags[localEntity] & ~DataFlags.DIRTY);
    }

    public override void ClearRemoved(int entity)
    {
        Removed?.Remove(entity);
    }

    public override void DisposeRemoved(object syncRoot)
    {
        if (_dispose == null)
        {
            return;
        }

        T[] values;
        int count;
        lock (syncRoot)
        {
            if (Removed is not { Count: > 0 })
            {
                return;
            }

            count = Removed.Count;
            values = ArrayPool<T>.Shared.Rent(count);
            int writeIndex = 0;
            foreach (T value in Removed.Values)
            {
                values[writeIndex++] = value;
            }

            Removed.Clear();
        }

        try
        {
            for (var i = 0; i < count; i++)
            {
                _dispose(ref values[i]);
            }
        }
        finally
        {
            ArrayPool<T>.Shared.Return(values, clearArray: RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        }
    }
}