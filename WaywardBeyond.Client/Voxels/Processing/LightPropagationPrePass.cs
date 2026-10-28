using System.Linq;
using WaywardBeyond.Bricks;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

/// <summary>
/// Enqueues light-emitting voxels for full-build light propagation.
/// </summary>
internal sealed class LightPropagationPrePass(LightingState lightingState, IBrickDatabase brickDatabase) : VoxelObjectProcessor.ISamplePass
{
    private readonly ushort[] _lightBrickIDs = brickDatabase.Get(info => info.LightSource).Select(info => info.DataID).ToArray();
    
    /// <inheritdoc/>
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PrePass;
    
    /// <inheritdoc/>
    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        //  Skip empty chunks
        if (chunkData.Palette.Only(0))
        {
            return false;
        }
        
        for (var i = 0; i < _lightBrickIDs.Length; i++)
        {
            if (chunkData.Palette.Any(_lightBrickIDs[i]))
            {
                return true;
            }
        }

        return false;
    }
    
    /// <inheritdoc/>
    public void Process(VoxelSample sample)
    {
        if (sample.Center.GetLightLevel() == 0)
        {
            return;
        }
        
        int x = sample.Coords.X + sample.ChunkOffset.X;
        int y = sample.Coords.Y + sample.ChunkOffset.Y;
        int z = sample.Coords.Z + sample.ChunkOffset.Z;
        var light = new LightingState.VoxelLight(x, y, z, sample.Center);
        lightingState.ToPropagate.Enqueue(light);
    }
}