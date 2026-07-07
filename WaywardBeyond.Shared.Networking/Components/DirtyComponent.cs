using System;
using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Shared.Networking.Components;

public struct DirtyComponent : IDataComponent
{
    private const int BIT_COUNT = 256;
    private const int ULONG_COUNT = BIT_COUNT / 64;

    private unsafe fixed ulong Bits[ULONG_COUNT];

    public void SetDirty<T>() where T : struct, IDataComponent
    {
        int bit = NetworkRegistry.GetBit<T>();
        unsafe
        {
            fixed (ulong* bits = Bits)
            {
                int index = bit / 64;
                int offset = bit % 64;
                bits[index] |= 1UL << offset;
            }
        }
    }

    public readonly bool IsDirty<T>() where T : struct, IDataComponent
    {
        int bit = NetworkRegistry.GetBit<T>();
        unsafe
        {
            fixed (ulong* bits = Bits)
            {
                int index = bit / 64;
                int offset = bit % 64;
                return (bits[index] & (1UL << offset)) != 0;
            }
        }
    }

    public void Clear()
    {
        unsafe
        {
            fixed (ulong* bits = Bits)
            {
                for (var i = 0; i < ULONG_COUNT; i++)
                {
                    bits[i] = 0UL;
                }
            }
        }
    }

    public readonly bool Any()
    {
        unsafe
        {
            fixed (ulong* bits = Bits)
            {
                for (var i = 0; i < ULONG_COUNT; i++)
                {
                    if (bits[i] != 0UL)
                    {
                        return true;
                    }
                }
            }
        }

        return false;
    }

    public readonly void ForEachDirty(Action<int> onDirty)
    {
        unsafe
        {
            fixed (ulong* bits = Bits)
            {
                for (var i = 0; i < ULONG_COUNT; i++)
                {
                    ulong word = bits[i];
                    if (word == 0UL)
                    {
                        continue;
                    }

                    int baseBit = i * 64;
                    while (word != 0UL)
                    {
                        int offset = System.Numerics.BitOperations.TrailingZeroCount(word);
                        onDirty(baseBit + offset);
                        word &= word - 1;
                    }
                }
            }
        }
    }
}
