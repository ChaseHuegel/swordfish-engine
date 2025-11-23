using System.Linq;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class LightPropagationPrePass(LightingState lightingState, IBrickDatabase brickDatabase) : VoxelObjectProcessor.ISamplePass
{
    private readonly LightingState _lightingState = lightingState;
    private readonly ushort[] _lightBrickIDs = brickDatabase.Get(info => info.LightSource).Select(info => info.DataID).ToArray();
    
    public VoxelObjectProcessor.Stage Stage => VoxelObjectProcessor.Stage.PrePass;
    
    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        //  If this chunk is made up of a single type, there isn't any reason to propagate lights.
        //  ! TODO There is an edge case here where the chunk could still need seeding if it were all lights
        if (chunkData.Palette.Count() == 1)
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
        _lightingState.ToPropagate.Enqueue(light);
    }
}