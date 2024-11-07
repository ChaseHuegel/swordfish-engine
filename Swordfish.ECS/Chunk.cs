namespace Swordfish.ECS;

internal class Chunk;

internal class Chunk<T>(int size) : Chunk
{
    // ReSharper disable once UnusedMember.Global
    public readonly int Size = size;
    public readonly T[] Components = new T[size];
    public readonly bool[] Exists = new bool[size];

    public int Count = 0;
}