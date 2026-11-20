using Swordfish.Library.Types.Shapes;
using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Server.Systems;

/// <summary>
/// Server authority collision-shape derivation for voxel structures. Delegates to <see cref="VoxelCollision"/>
/// so authority physics and client prediction share one exact shape, orientation, and interior-culling implementation.
/// </summary>
public static class VoxelColliderBuilder
{
    /// <summary>Builds the collision shape for a live voxel structure in world-local coordinates.</summary>
    public static CompoundShape BuildCollision(VoxelObject voxelObject, IBrickDatabase? brickDatabase = null)
    {
        return VoxelCollision.BuildCompound(voxelObject, brickDatabase);
    }

    /// <summary>Builds the collision shape for a voxel structure from serialized chunks.</summary>
    public static CompoundShape BuildCollision(ChunkInfo[] chunks, IBrickDatabase? brickDatabase = null)
    {
        if (chunks.Length == 0)
        {
            return new CompoundShape([], [], []);
        }

        byte chunkSize = chunks[0].Chunk.Size;
        if (chunkSize <= 0 || (chunkSize & (chunkSize - 1)) != 0)
        {
            chunkSize = VoxelObject.DEFAULT_CHUNK_EDGE;
        }

        var voxelObject = new VoxelObject(chunkSize, chunks);
        return VoxelCollision.BuildCompound(voxelObject, brickDatabase);
    }
}
