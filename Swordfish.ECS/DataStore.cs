using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Swordfish.ECS;

public class DataStore
{
    private readonly int _chunkBitWidth;
    private readonly int _chunkSize;
    private readonly Dictionary<Type, ChunkedStore> _stores = [];   //  TODO not thread safe
    private readonly object _chunkAndStoreLock = new();
    private readonly Queue<int> _recycledEntities = new();
    private readonly object _recycleLock = new();

    private int _lastEntity = 0;

    public DataStore(byte chunkBitWidth = 16)
    {
        if (chunkBitWidth > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkBitWidth), "Chunk width can not exceed 30 bits.");
        }

        _chunkBitWidth = chunkBitWidth;
        _chunkSize = 1 << chunkBitWidth;
    }

    public int Create<T1>(T1 component1) where T1 : struct, IDataComponent
    {
        lock (_chunkAndStoreLock)
        {
            int entity = NewEntity();
            (int chunkIndex, int localEntity) = ToChunkSpace(entity);
            SetAt(chunkIndex, localEntity, component1, true);
            return entity;
        }
    }

    public int Create<T1, T2>(T1 component1, T2 component2)
        where T1 : struct, IDataComponent
        where T2 : struct, IDataComponent
    {
        lock (_chunkAndStoreLock)
        {
            int entity = NewEntity();
            (int chunkIndex, int localEntity) = ToChunkSpace(entity);
            SetAt(chunkIndex, localEntity, component1, true);
            SetAt(chunkIndex, localEntity, component2, true);
            return entity;
        }
    }

    public void Delete(int entity)
    {
        lock (_chunkAndStoreLock)
        {
            (int chunkIndex, int localEntity) = ToChunkSpace(entity);
            foreach (ChunkedStore store in _stores.Values)
            {
                store.SetAt(chunkIndex, localEntity, false);
            }

            lock (_recycleLock)
            {
                _recycledEntities.Enqueue(entity);
            }
        }
    }

    public void AddOrUpdate<T1>(int entity, T1 component1) where T1 : struct, IDataComponent
    {
        lock (_chunkAndStoreLock)
        {
            ChunkedStore<T1> store1;
            if (!_stores.TryGetValue(typeof(T1), out ChunkedStore? store))
            {
                store1 = new ChunkedStore<T1>(_chunkSize);
                _stores.Add(typeof(T1), store1);
            }
            else
            {
                store1 = (ChunkedStore<T1>)store;
            }

            (int chunkIndex, int localEntity) = ToChunkSpace(entity);
            store1.SetAt(chunkIndex, localEntity, component1, true);
        }
    }

    public void AddOrUpdate<T1, T2>(int entity, T1 component1, T2 component2)
        where T1 : struct, IDataComponent
        where T2 : struct, IDataComponent
    {
        lock (_chunkAndStoreLock)
        {
            ChunkedStore<T1> store1;
            if (!_stores.TryGetValue(typeof(T1), out ChunkedStore? store))
            {
                store1 = new ChunkedStore<T1>(_chunkSize);
                _stores.Add(typeof(T1), store1);
            }
            else
            {
                store1 = (ChunkedStore<T1>)store;
            }

            ChunkedStore<T2> store2;
            if (!_stores.TryGetValue(typeof(T2), out ChunkedStore? storeB))
            {
                store2 = new ChunkedStore<T2>(_chunkSize);
                _stores.Add(typeof(T2), store2);
            }
            else
            {
                store2 = (ChunkedStore<T2>)storeB;
            }

            (int chunkIndex, int localEntity) = ToChunkSpace(entity);
            store1.SetAt(chunkIndex, localEntity, component1, true);
            store2.SetAt(chunkIndex, localEntity, component2, true);
        }
    }

    public bool Remove<T1>(int entity) where T1 : struct, IDataComponent
    {
        lock (_chunkAndStoreLock)
        {
            if (!_stores.TryGetValue(typeof(T1), out ChunkedStore? store))
            {
                return false;
            }

            (int chunkIndex, int localEntity) = ToChunkSpace(entity);
            store.SetAt(chunkIndex, localEntity, false);
            return true;
        }
    }

    public bool Remove<T1, T2>(int entity)
        where T1 : struct, IDataComponent
        where T2 : struct, IDataComponent
    {
        lock (_chunkAndStoreLock)
        {
            if (!_stores.TryGetValue(typeof(T1), out ChunkedStore? store))
            {
                return false;
            }

            if (!_stores.TryGetValue(typeof(T1), out ChunkedStore? storeB))
            {
                return false;
            }

            (int chunkIndex, int localEntity) = ToChunkSpace(entity);
            store.SetAt(chunkIndex, localEntity, false);
            storeB.SetAt(chunkIndex, localEntity, false);
            return true;
        }
    }

    public unsafe void Query<T1>(float delta, ForEach<T1> forEach) where T1 : struct, IDataComponent
    {
        Span<Chunk<T1>> chunks;
        lock (_chunkAndStoreLock)
        {
            if (!_stores.TryGetValue(typeof(T1), out ChunkedStore? store))
            {
                return;
            }
            else
            {
                chunks = CollectionsMarshal.AsSpan(((ChunkedStore<T1>)store).Chunks);
            }
        }

        for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
        {
            Chunk<T1> chunk = chunks[chunkIndex];
            for (int componentIndex = 0; componentIndex < chunk.Components.Length; componentIndex++)
            {
                if (!chunk.Exists[componentIndex])
                {
                    continue;
                }

                int entity = ToGlobalSpace(componentIndex, chunkIndex);
                T1 c1 = chunk.Components[componentIndex];

                forEach(delta, this, entity, ref c1);

                chunk.Components[componentIndex] = c1;  //  TODO use a buffer to apply changes?
            }
        }
    }

    public void Query<T1, T2>(float delta, ForEach<T1, T2> forEach)
        where T1 : struct, IDataComponent
        where T2 : struct, IDataComponent
    {
        Span<Chunk<T1>> chunks1;
        Span<Chunk<T2>> chunks2;
        lock (_chunkAndStoreLock)
        {
            if (!_stores.TryGetValue(typeof(T1), out ChunkedStore? store1))
            {
                return;
            }
            else
            {
                chunks1 = CollectionsMarshal.AsSpan(((ChunkedStore<T1>)store1).Chunks);
            }

            if (!_stores.TryGetValue(typeof(T2), out ChunkedStore? store2))
            {
                return;
            }
            else
            {
                chunks2 = CollectionsMarshal.AsSpan(((ChunkedStore<T2>)store2).Chunks);
            }
        }

        //  TODO test speed of using Parallel over chunks with low a chunkBitWidth.
        //  TODO determine if lots of chunks with small loops or few chunks with big loops is faster.
        //  TODO based on findings above, perhaps introduce a dynamic switch between Parallel and Synchronous iterations over chunks?
        QueryDynamicInternal(delta, chunks1, chunks2, forEach);
    }

    public bool Query<T1>(int entity, out T1 component1) where T1 : struct, IDataComponent
    {
        lock (_chunkAndStoreLock)
        {
            if (!_stores.TryGetValue(typeof(T1), out ChunkedStore? store))
            {
                component1 = default!;
                return false;
            }

            (int chunkIndex, int localEntity) = ToChunkSpace(entity);
            return ((ChunkedStore<T1>)store).TryGetAt(chunkIndex, localEntity, out component1);
        }
    }

    public unsafe bool Find<T1>(Predicate<T1> predicate, out int entity) where T1 : struct, IDataComponent
    {
        Span<Chunk<T1>> chunks;
        lock (_chunkAndStoreLock)
        {
            if (!_stores.TryGetValue(typeof(T1), out ChunkedStore? store))
            {
                entity = default!;
                return false;
            }
            else
            {
                chunks = CollectionsMarshal.AsSpan(((ChunkedStore<T1>)store).Chunks);
            }
        }

        for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
        {
            Chunk<T1> chunk = chunks[chunkIndex];
            for (int componentIndex = 0; componentIndex < chunk.Components.Length; componentIndex++)
            {
                if (!chunk.Exists[componentIndex])
                {
                    continue;
                }

                entity = ToGlobalSpace(chunkIndex, componentIndex);
                T1 c1 = chunk.Components[componentIndex];

                if (predicate(c1))
                {
                    return true;
                }
            }
        }

        entity = default!;
        return false;
    }

    public Span<IDataComponent> Get(int entity)
    {
        (int chunkIndex, int localEntity) = ToChunkSpace(entity);
        List<IDataComponent> components = [];

        lock (_chunkAndStoreLock)
        {
            foreach (KeyValuePair<Type, ChunkedStore> typeStore in _stores)
            {
                ChunkedStore<IDataComponent> store = Unsafe.As<ChunkedStore<IDataComponent>>(typeStore.Value);
                if (!store.TryGetAt(chunkIndex, localEntity, out IDataComponent data))
                {
                    continue;
                }

                components.Add(data);
            }
        }

        return CollectionsMarshal.AsSpan(components);
    }

    private void QueryDynamicInternal<T1, T2>(float delta, Span<Chunk<T1>> chunks1, Span<Chunk<T2>> chunks2, ForEach<T1, T2> forEach)
        where T1 : struct, IDataComponent
        where T2 : struct, IDataComponent
    {
        int minChunks = Math.Min(chunks1.Length, chunks2.Length);
        for (int chunkIndex = 0; chunkIndex < minChunks; chunkIndex++)
        {
            Chunk<T1> chunk1 = chunks1[chunkIndex];
            Chunk<T2> chunk2 = chunks2[chunkIndex];
            T1[] components1 = chunk1.Components;
            bool[] exists1 = chunk1.Exists;
            T2[] components2 = chunk2.Components;
            bool[] exists2 = chunk2.Exists;
            int offset = _chunkSize * chunkIndex;

            //  ~30k entities with an empty forEach is when Parallel scheduling becomes cheaper and faster.
            //  TODO allow queries and systems to force parallelism so they caller can account for a heavy forEach.
            if (chunk1.Count > 30_000)
            {
                ForEachParallelInternal(delta, offset, components1, exists1, components2, exists2, forEach);
            }
            else
            {
                ForEachSynchronousInternal(delta, offset, components1, exists1, components2, exists2, forEach);
            }
        }
    }

    private void ForEachParallelInternal<T1, T2>(float delta, int offset, T1[] components1, bool[] exists1, T2[] components2, bool[] exists2, ForEach<T1, T2> forEach)
        where T1 : struct, IDataComponent
        where T2 : struct, IDataComponent
    {
        Parallel.For(0, components1.Length, ForEachLocalEntity);
        void ForEachLocalEntity(int componentIndex, ParallelLoopState state)
        {
            if (!exists1[componentIndex] || !exists2[componentIndex])
            {
                return;
            }

            int entity = componentIndex + offset;
            forEach(delta, this, entity, ref components1[componentIndex], ref components2[componentIndex]);
        }
    }

    private void ForEachSynchronousInternal<T1, T2>(float delta, int offset, T1[] components1, bool[] exists1, T2[] components2, bool[] exists2, ForEach<T1, T2> forEach)
        where T1 : struct, IDataComponent
        where T2 : struct, IDataComponent
    {
        for (int componentIndex = 0; componentIndex < components1.Length; componentIndex++)
        {
            if (!exists1[componentIndex] || !exists2[componentIndex])
            {
                continue;
            }

            int entity = componentIndex + offset;
            forEach(delta, this, entity, ref components1[componentIndex], ref components2[componentIndex]);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int NewEntity()
    {
        lock (_recycleLock)
        {
            if (_recycledEntities.Count > 0)
            {
                return _recycledEntities.Dequeue();
            }
        }

        return Interlocked.Increment(ref _lastEntity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (int chunkIndex, int localEntity) ToChunkSpace(int entity)
    {
        int chunkIndex = entity >> _chunkBitWidth;
        int localEntity = entity - _chunkSize * chunkIndex;
        return (chunkIndex, localEntity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ToGlobalSpace(int chunkIndex, int localEntity)
    {
        return localEntity + _chunkSize * chunkIndex;
    }

    private void SetAt<T1>(int chunkIndex, int localEntity, T1 component1, bool exists) where T1 : struct, IDataComponent
    {
        ChunkedStore<T1> store1;
        if (!_stores.TryGetValue(typeof(T1), out ChunkedStore? store))
        {
            store1 = new ChunkedStore<T1>(_chunkSize);
            _stores.Add(typeof(T1), store1);
        }
        else
        {
            store1 = (ChunkedStore<T1>)store;
        }

        store1.SetAt(chunkIndex, localEntity, component1, exists);
    }
}