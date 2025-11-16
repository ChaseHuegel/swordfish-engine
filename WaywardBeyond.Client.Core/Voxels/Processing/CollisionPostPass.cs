using System.Numerics;
using Swordfish.Library.Types.Shapes;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class CollisionPostPass(in CollisionState collisionState) : VoxelObjectProcessor.ISamplePass
{
    private readonly CollisionState _collisionState = collisionState;
    
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PostPass;
    
    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        return !chunkData.Palette.Only(id: 0);
    }
    
    public void Process(VoxelSample sample)
    {
        if (sample.Center.ID == 0 || !sample.HasAny())
        {
            return;
        }
        
        var origin = new Vector3(sample.Coords.X + sample.ChunkOffset.X, sample.Coords.Y + sample.ChunkOffset.Y, sample.Coords.Z + sample.ChunkOffset.Z);

        _collisionState.Locations.Add(origin);
        _collisionState.Shapes.Add(new Box3(Vector3.One));
        _collisionState.Orientations.Add(Quaternion.Identity);
    }
}