using System.Collections.Generic;
using WaywardBeyond.Client.Core.Numerics;

namespace WaywardBeyond.Client.Core.Voxels.Models;

public readonly struct ChunkData(in Short3 coords, in Chunk chunk)
{
    public readonly Short3 Coords = coords;
    public readonly Chunk Chunk = chunk;
    public readonly VoxelPalette Palette = new(chunk.Voxels.Length);
}