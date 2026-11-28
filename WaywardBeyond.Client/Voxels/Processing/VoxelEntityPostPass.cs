using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

internal sealed class VoxelEntityPostPass(
    IBrickDatabase brickDatabase,
    EntityState entityState,
    IBrickEntityDecorator[]? decorators = null
) : VoxelObjectProcessor.ISamplePass
{
    private readonly IBrickEntityDecorator[] _decorators = decorators ?? [];

    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PostPass;

    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        return true;
    }

    public void Process(VoxelSample sample)
    {
        if (_decorators.Length == 0 || sample.Center.ID == 0)
        {
            return;
        }

        Result<Brick> brickInfoResult = brickDatabase.Get(sample.Center.ID);
        if (!brickInfoResult.Success || !HasMatchingDecorator(brickInfoResult.Value))
        {
            return;
        }

        int x = sample.Coords.X + sample.ChunkOffset.X;
        int y = sample.Coords.Y + sample.ChunkOffset.Y;
        int z = sample.Coords.Z + sample.ChunkOffset.Z;
        var voxelInfo = new VoxelInfo(x, y, z, sample.Center);
        entityState.Voxels.Add(voxelInfo);
    }

    private bool HasMatchingDecorator(Brick brick)
    {
        for (var i = 0; i < _decorators.Length; i++)
        {
            if (_decorators[i].AppliesTo(brick))
            {
                return true;
            }
        }

        return false;
    }
}
