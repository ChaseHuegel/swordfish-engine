using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class LightPropagationPass(in LightingState lightingState, in IBrickDatabase brickDatabase)
    : VoxelObjectProcessor.IPass
{
    private readonly LightingState _lightingState = lightingState;
    private readonly IBrickDatabase _brickDatabase = brickDatabase;
    
    public void Process(VoxelObject voxelObject)
    {
        while (_lightingState.Lights.Count > 0)
        {
            LightingState.VoxelLight item = _lightingState.Lights.Dequeue();
            int lightLevel = item.Voxel.GetLightLevel();
            if (lightLevel <= 1)
            {
                continue;
            }
            
            int nextLightLevel = lightLevel - 1;
            VoxelSample sample = voxelObject.Sample(item.X, item.Y, item.Z);
            
            PropagateLight(ref sample.Left, item.X - 1, item.Y, item.Z);
            PropagateLight(ref sample.Right, item.X + 1, item.Y, item.Z);
            PropagateLight(ref sample.Above, item.X, item.Y + 1, item.Z);
            PropagateLight(ref sample.Below, item.X, item.Y - 1, item.Z);
            PropagateLight(ref sample.Ahead, item.X, item.Y, item.Z - 1);
            PropagateLight(ref sample.Behind, item.X, item.Y, item.Z + 1);
            
            void PropagateLight(ref Voxel voxel, int x, int y, int z)
            {
                ShapeLight shapeLight = voxel.GetShapeLight();
                if (shapeLight.LightLevel + 2 > nextLightLevel || _brickDatabase.IsCuller(voxel, shapeLight))
                {
                    return;
                }
                
                voxel.ShapeLight = new ShapeLight(shapeLight.Shape, nextLightLevel);
                _lightingState.Lights.Enqueue(new LightingState.VoxelLight(x, y, z, voxel));
            }
        }
    }
}