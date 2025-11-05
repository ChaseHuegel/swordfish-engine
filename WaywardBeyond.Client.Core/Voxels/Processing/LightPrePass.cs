using System.Linq;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

/// <summary>
///     An abstract base implementation for pre-passes which are only valid for chunks containing lights.
/// </summary>
internal abstract class LightPrePass
{
    protected readonly IBrickDatabase BrickDatabase;
    private readonly ushort[] _lightBrickIDs;

    protected LightPrePass(in IBrickDatabase brickDatabase)
    {
        BrickDatabase = brickDatabase;
        _lightBrickIDs = brickDatabase.Get(info => info.LightSource).Select(info => info.DataID).ToArray();
    }
    
    public bool ShouldProcessChunk(ChunkData chunkData)
    {
        for (var i = 0; i < _lightBrickIDs.Length; i++)
        {
            if (chunkData.Palette.Any(_lightBrickIDs[i]))
            {
                return true;
            }
        }

        return false;
    }
}