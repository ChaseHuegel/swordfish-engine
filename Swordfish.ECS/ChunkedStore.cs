namespace Swordfish.ECS;

internal abstract class ChunkedStore
{
    public abstract void SetAt(int chunkIndex, int localEntity, bool exists);

    public abstract bool TryGetAt(int chunkIndex, int localEntity, out IDataComponent data);

    public abstract bool IsDirty(int chunkIndex, int localEntity);

    public abstract void MarkDirty(int chunkIndex, int localEntity);

    public abstract void ClearDirty(int chunkIndex, int localEntity);
}

internal class ChunkedStore<T>(in int chunkSize) : ChunkedStore where T : struct, IDataComponent
{
    public readonly List<Chunk<T>?> Chunks = [];

    private readonly int _chunkSize = chunkSize;

    public override void SetAt(int chunkIndex, int localEntity, bool exists)
    {
        //  Preserve the component value on removal so QueryRemoved can read the last known data
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
        chunk.Components[localEntity] = data;
        chunk.Flags[localEntity] = exists
            ? (byte)(chunk.Flags[localEntity] | DataFlags.EXISTS | DataFlags.DIRTY)
            : (byte)((chunk.Flags[localEntity] & ~DataFlags.EXISTS) | DataFlags.DIRTY);
        
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
}