using System;

namespace WaywardBeyond.Shared.Data;

public static class FNV1a
{
    public static uint ComputeHash32(string str)
    {
        const uint fnvOffset = 0x811C9DC5;
        const uint fnvPrime = 0x01000193;

        uint hash = fnvOffset;
        for (var i = 0; i < str.Length; i++)
        {
            char c = str[i];
            hash ^= c;
            hash *= fnvPrime;
        }

        return hash;
    }

    /// <summary>
    ///     Derives the stable 16-bit brick id for a brick name. Matches the id the client's brick
    ///     database assigns; the game-shared voxel generation (server, worldgen, tests) and the client
    ///     must derive identical voxel ids for the same named material, which is what lets server-authored
    ///     world data resolve to correct bricks once streamed to the client. Contrast with the client's
    ///     full <see cref="WaywardBeyond.Client.Core.Bricks.BrickDatabase"/> which additionally shifts a
    ///     DataID when a rare FNV collision occurs within the baked brick set; the worldgen materials are
    ///     never affected by that pass.
    /// </summary>
    public static ushort ComputeDataID(string str)
    {
        return (ushort)(ComputeHash32(str) % ushort.MaxValue);
    }
}