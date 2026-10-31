using WaywardBeyond.Bricks;
using WaywardBeyond.Voxels;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Voxels.Processing;

/// <summary>
/// Propagates light levels across resident voxels using a breadth-first search.
/// </summary>
internal sealed class LightPropagationPass(
    LightingState lightingState,
    DepthState depthState,
    IBrickDatabase brickDatabase
) : VoxelObjectProcessor.IPass
{
    /// <inheritdoc/>
    public void Process(VoxelObject voxelObject)
    {
        while (lightingState.ToPropagate.Count > 0)
        {
            LightingState.VoxelLight item = lightingState.ToPropagate.Dequeue();
            int lightLevel = item.Voxel.GetLightLevel();
            if (lightLevel <= 1)
            {
                continue;
            }

            int nextLightLevel = lightLevel - 1;
            VoxelSample sample = voxelObject.Sample(item.X, item.Y, item.Z);

            PropagateLight(voxelObject, ref sample.Left, item.X - 1, item.Y, item.Z, nextLightLevel);
            PropagateLight(voxelObject, ref sample.Right, item.X + 1, item.Y, item.Z, nextLightLevel);
            PropagateLight(voxelObject, ref sample.Above, item.X, item.Y + 1, item.Z, nextLightLevel);
            PropagateLight(voxelObject, ref sample.Below, item.X, item.Y - 1, item.Z, nextLightLevel);
            PropagateLight(voxelObject, ref sample.Ahead, item.X, item.Y, item.Z + 1, nextLightLevel);
            PropagateLight(voxelObject, ref sample.Behind, item.X, item.Y, item.Z - 1, nextLightLevel);
        }
    }

    private void PropagateLight(VoxelObject voxelObject, ref Voxel voxel, int x, int y, int z, int nextLightLevel)
    {
        if (!voxelObject.TryGetChunk(voxelObject.ToChunkOffset(x, y, z), out _))
        {
            return;
        }

        int targetLight = VoxelAmbient.IsAmbient(voxelObject, depthState, x, y, z) ? 15 : nextLightLevel;

        ShapeLight shapeLight = voxel.GetShapeLight();
        if (shapeLight.LightLevel >= targetLight)
        {
            return;
        }

        if (brickDatabase.IsCuller(voxel, shapeLight.Shape))
        {
            return;
        }

        voxel.ShapeLight = new ShapeLight(shapeLight.Shape, targetLight);
        lightingState.ToPropagate.Enqueue(new LightingState.VoxelLight(x, y, z, voxel));
    }
}
