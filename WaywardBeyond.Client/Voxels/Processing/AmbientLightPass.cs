using WaywardBeyond.Bricks;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Voxels.Processing;

/// <summary>
/// Seeds ambient light across every resident air cell that is exterior.
/// </summary>
internal sealed class AmbientLightPass(
    LightingState lightingState,
    DepthState depthState,
    IBrickDatabase brickDatabase
) : VoxelObjectProcessor.IPass
{
    /// <inheritdoc/>
    public void Process(VoxelObject voxelObject)
    {
        foreach (ChunkData chunk in voxelObject)
        {
            if (chunk.Palette.Only(0))
            {
                continue;
            }

            foreach (VoxelSample sample in chunk.GetSampler())
            {
                if (sample.Center.ID != 0 && brickDatabase.IsCuller(sample.Center, sample.Center.GetShapeLight().Shape))
                {
                    continue;
                }

                int x = sample.Coords.X + sample.ChunkOffset.X;
                int y = sample.Coords.Y + sample.ChunkOffset.Y;
                int z = sample.Coords.Z + sample.ChunkOffset.Z;

                if (!VoxelAmbient.IsAmbient(voxelObject, depthState, x, y, z))
                {
                    continue;
                }

                if (sample.Center.GetLightLevel() >= 15)
                {
                    continue;
                }

                ShapeLight shapeLight = sample.Center.GetShapeLight();
                sample.Center.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
                lightingState.ToPropagate.Enqueue(new LightingState.VoxelLight(x, y, z, sample.Center));
            }
        }
    }
}
