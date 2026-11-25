using System;
using WaywardBeyond.Bricks;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

internal sealed class CollisionPostPass(CollisionState collisionState, IBrickDatabase brickDatabase) : VoxelObjectProcessor.ISamplePass
{
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PostPass;

    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        int totalVoxels = chunkData.Palette.Count();
        int emptyVoxels = chunkData.Palette.Count(id: 0);
        if (emptyVoxels == totalVoxels)
        {
            return false;
        }

        //  Attempt to expand collections pre-emptively to
        //  reduce allocations that may occur during processing.
        int nonEmptyVoxels = Math.Max(totalVoxels, emptyVoxels) - Math.Min(totalVoxels, emptyVoxels);
        collisionState.Shapes.EnsureCapacity(collisionState.Shapes.Count + nonEmptyVoxels);
        collisionState.Positions.EnsureCapacity(collisionState.Positions.Count + nonEmptyVoxels);
        collisionState.Orientations.EnsureCapacity(collisionState.Orientations.Count + nonEmptyVoxels);

        return true;
    }

    /// <summary>Appends merged collision primitives for every exposed solid voxel in <paramref name="chunkData"/>.</summary>
    public void ProcessChunk(ChunkData chunkData)
    {
        if (!ShouldProcessChunk(chunkData))
        {
            return;
        }

        VoxelCollision.AppendChunk(
            chunkData,
            brickDatabase,
            collisionState.Shapes,
            collisionState.Positions,
            collisionState.Orientations
        );
    }

    public void Process(VoxelSample sample)
    {
        VoxelCollision.AppendVoxel(
            in sample,
            brickDatabase,
            collisionState.Shapes,
            collisionState.Positions,
            collisionState.Orientations
        );
    }
}
