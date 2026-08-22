using System.Security.Cryptography;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Swordfish.ECS;

public partial class DataStore
{
    private readonly int _chunkBitWidth;
    private readonly int _chunkSize;
    private readonly Dictionary<Type, ChunkedStore> _stores = [];
    private readonly List<Uuid[]> _uuids = [];
    private readonly Dictionary<Uuid, int> _slotByUuid = [];
    private readonly object _chunkAndStoreLock = new();
    private readonly Queue<int> _recycledEntities = new();
    private readonly HashSet<int> _recycledSet = [];
    private readonly object _recycleLock = new();
    private readonly uint _uuidSeed;

    private int _lastEntity;
    private uint _uuidCounter;

    public DataStore(byte chunkBitWidth = 16)
    {
        if (chunkBitWidth > 30)
        {
            throw new ArgumentOutOfRangeException(nameof(chunkBitWidth), "Chunk width can not exceed 30 bits.");
        }

        _chunkBitWidth = chunkBitWidth;
        _chunkSize = 1 << chunkBitWidth;

        Span<byte> seedBytes = stackalloc byte[sizeof(uint)];
        RandomNumberGenerator.Fill(seedBytes);
        _uuidSeed = BitConverter.ToUInt32(seedBytes);
    }

    public Uuid NewUuid()
    {
        return Uuid.FromValue(((ulong)_uuidSeed << 32) | Interlocked.Increment(ref _uuidCounter));
    }

    public int Alloc()
    {
        lock (_chunkAndStoreLock)
        {
            int entity = AllocNewEntity();
            return entity;
        }
    }

    public int Alloc(Uuid uuid)
    {
        lock (_chunkAndStoreLock)
        {
            if (uuid == Uuid.Null)
            {
                throw new InvalidOperationException("Uuid can not be Null.");
            }

            if (_slotByUuid.ContainsKey(uuid))
            {
                throw new InvalidOperationException($"Duplicate Uuid: {uuid}.");
            }

            int entity = AllocNewEntity(uuid);
            return entity;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Uuid GetUuid(int entity)
    {
        (int chunkIndex, int localEntity) = ToChunkSpace(entity);
        if (chunkIndex < _uuids.Count)
        {
            return _uuids[chunkIndex][localEntity];
        }

        return Uuid.Null;
    }

    public bool TryGet(Uuid uuid, out int entity)
    {
        lock (_chunkAndStoreLock)
        {
            return _slotByUuid.TryGetValue(uuid, out entity);
        }
    }

    public void Free(int entity)
    {
        lock (_chunkAndStoreLock)
        {
            lock (_recycleLock)
            {
                (int chunkIndex, int localEntity) = ToChunkSpace(entity);
                foreach (ChunkedStore store in _stores.Values)
                {
                    store.SetAt(chunkIndex, localEntity, false);
                }

                _slotByUuid.Remove(_uuids[chunkIndex][localEntity]);
                _uuids[chunkIndex][localEntity] = Uuid.Null;
                _recycledEntities.Enqueue(entity);
                _recycledSet.Add(entity);
            }
        }
    }

    public void Query(float delta, ForEach forEach)
    {
        lock (_chunkAndStoreLock)
        {
            lock (_recycleLock)
            {
                for (var i = 1; i <= _lastEntity; i++)
                {
                    if (_recycledSet.Contains(i))
                    {
                        continue;
                    }

                    forEach(delta, this, i);
                }
            }
        }
    }

    public bool Find<T1>(Predicate<T1> predicate, out int entity) where T1 : struct, IDataComponent
    {
        Span<Chunk<T1>?> chunks;
        lock (_chunkAndStoreLock)
        {
            if (!_stores.TryGetValue(typeof(T1), out ChunkedStore? store))
            {
                entity = -1;
                return false;
            }

            chunks = CollectionsMarshal.AsSpan(((ChunkedStore<T1>)store).Chunks);
        }

        for (var chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
        {
            Chunk<T1>? chunk = chunks[chunkIndex];
            if (chunk is null)
            {
                continue;
            }

            for (var componentIndex = 0; componentIndex < chunk.HighWater; componentIndex++)
            {
                if ((chunk.Flags[componentIndex] & DataFlags.EXISTS) == 0)
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

        entity = 0;
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
                if (!typeStore.Value.TryGetAt(chunkIndex, localEntity, out IDataComponent data))
                {
                    continue;
                }

                components.Add(data);
            }
        }

        return CollectionsMarshal.AsSpan(components);
    }

    public bool IsDirty(Type type, int entity)
    {
        lock (_chunkAndStoreLock)
        {
            if (!_stores.TryGetValue(type, out ChunkedStore? store))
            {
                return false;
            }

            (int chunkIndex, int localEntity) = ToChunkSpace(entity);
            return store.IsDirty(chunkIndex, localEntity);
        }
    }

    public void MarkDirty(Type type, int entity)
    {
        lock (_chunkAndStoreLock)
        {
            if (!_stores.TryGetValue(type, out ChunkedStore? store))
            {
                return;
            }

            (int chunkIndex, int localEntity) = ToChunkSpace(entity);
            store.MarkDirty(chunkIndex, localEntity);
        }
    }

    public void ClearDirty(Type type, int entity)
    {
        lock (_chunkAndStoreLock)
        {
            if (!_stores.TryGetValue(type, out ChunkedStore? store))
            {
                return;
            }

            (int chunkIndex, int localEntity) = ToChunkSpace(entity);
            store.ClearDirty(chunkIndex, localEntity);
        }
    }

    public bool IsDirty<T1>(int entity) where T1 : struct, IDataComponent
    {
        return IsDirty(typeof(T1), entity);
    }

    public void MarkDirty<T1>(int entity) where T1 : struct, IDataComponent
    {
        MarkDirty(typeof(T1), entity);
    }

    public void ClearDirty<T1>(int entity) where T1 : struct, IDataComponent
    {
        ClearDirty(typeof(T1), entity);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AllocNewEntity()
    {
        return AllocNewEntity(NewUuid());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int AllocNewEntity(Uuid uuid)
    {
        lock (_recycleLock)
        {
            int entity;
            if (_recycledEntities.Count > 0)
            {
                entity = _recycledEntities.Dequeue();
                _recycledSet.Remove(entity);
            }
            else
            {
                entity = Interlocked.Increment(ref _lastEntity);
            }

            AssignUuid(entity, uuid);
            return entity;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void AssignUuid(int entity, Uuid uuid)
    {
        (int chunkIndex, int localEntity) = ToChunkSpace(entity);
        EnsureUuidChunk(chunkIndex);
        _uuids[chunkIndex][localEntity] = uuid;
        _slotByUuid[uuid] = entity;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void EnsureUuidChunk(int chunkIndex)
    {
        while (_uuids.Count <= chunkIndex)
        {
            _uuids.Add(new Uuid[_chunkSize]);
        }
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