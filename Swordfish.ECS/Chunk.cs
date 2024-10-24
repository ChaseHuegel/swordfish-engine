namespace Swordfish.ECS;

internal class Chunk() { }

internal class Chunk<T>(int size) : Chunk
{
    public readonly int Size = size;
    public readonly T[] Components = new T[size];
    public readonly bool[] Exists = new bool[size];

    public int Count = 0;
}