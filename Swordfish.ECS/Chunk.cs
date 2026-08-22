using System.Runtime.CompilerServices;

namespace Swordfish.ECS;

internal class Chunk<T>(int size)
{
    // ReSharper disable once UnusedMember.Global
    public readonly int Size = size;
    public readonly T[] Components = new T[size];
    public readonly byte[] Flags = new byte[size];

    public int Count = 0;
    public int HighWater = 0;
}