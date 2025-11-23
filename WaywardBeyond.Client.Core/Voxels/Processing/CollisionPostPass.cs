using System;
using System.Numerics;
using Swordfish.Library.Types.Shapes;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class CollisionPostPass(CollisionState collisionState) : VoxelObjectProcessor.ISamplePass
{
    private readonly CollisionState _collisionState = collisionState;
    
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
        _collisionState.Shapes.EnsureCapacity(_collisionState.Shapes.Count + nonEmptyVoxels);
        _collisionState.Positions.EnsureCapacity(_collisionState.Positions.Count + nonEmptyVoxels);
        _collisionState.Orientations.EnsureCapacity(_collisionState.Orientations.Count + nonEmptyVoxels);
        
        return true;
    }
    
    public void Process(VoxelSample sample)
    {
        if (sample.Center.ID == 0 || !sample.HasAny())
        {
            return;
        }
        
        var origin = new Vector3(sample.Coords.X + sample.ChunkOffset.X, sample.Coords.Y + sample.ChunkOffset.Y, sample.Coords.Z + sample.ChunkOffset.Z);
        
        _collisionState.Shapes.Add(new Box3(Vector3.One));
        _collisionState.Positions.Add(origin);
        _collisionState.Orientations.Add(Quaternion.Identity);
    }
}