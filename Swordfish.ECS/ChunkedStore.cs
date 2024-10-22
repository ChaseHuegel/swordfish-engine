namespace Swordfish.ECS;

internal abstract class ChunkedStore()
{
    public abstract void SetAt(int chunkIndex, int localEntity, bool exists);
}

internal class ChunkedStore<T>(int chunkSize) : ChunkedStore
{
    private readonly int _chunkSize = chunkSize;

    public readonly List<Chunk<T>> Chunks = [];

    public override void SetAt(int chunkIndex, int localEntity, bool exists)
    {
        SetAt(chunkIndex, localEntity, default!, exists);
    }

    public void SetAt(int chunkIndex, int localEntity, T component1, bool exists)
    {
        Chunk<T> chunk;
        if (Chunks.Count <= chunkIndex)
        {
            if (exists)
            {
                chunk = new Chunk<T>(_chunkSize);
                Chunks.Add(chunk);
            }
            else
            {
                //  Don't do anything if setting exists=false and a chunk doesn't exist here
                return;
            }
        }
        else
        {
            chunk = Chunks[chunkIndex];
        }

        chunk.Components[localEntity] = component1;
        chunk.Exists[localEntity] = exists;
        chunk.Count += exists ? 1 : -1;
        //  TODO should chunks get cleaned up when they are empty?
    }

    public bool TryGetAt(int chunkIndex, int localEntity, out T data)
    {
        Chunk<T> chunk;
        if (Chunks.Count <= chunkIndex)
        {
            data = default!;
            return false;
        }
        else
        {
            chunk = Chunks[chunkIndex];
        }

        data = chunk.Components[localEntity];
        return chunk.Exists[localEntity];
    }
}