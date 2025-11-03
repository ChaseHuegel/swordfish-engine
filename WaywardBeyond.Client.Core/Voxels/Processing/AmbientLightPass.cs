using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Voxels.Processing;

internal sealed class AmbientLightPass(in LightingState lightingState) : VoxelObjectProcessor.IPass
{
    private readonly LightingState _lightingState = lightingState;
    
    public void Process(VoxelObject voxelObject)
    {
        //  TODO dont use hardcoded ray step range
        //  TODO each ambient cast should have a unique distance based on the chunks in that dir
        const int ambientRange = 32;
        
        for (int x = -ambientRange; x <= ambientRange; x++)
        for (int z = -ambientRange; z <= ambientRange; z++)
        {
            CastAmbientLight(x, ambientRange, z, 0, -1, 0);
            CastAmbientLight(x, -ambientRange, z, 0, 1, 0);
        }
        
        for (int y = -ambientRange; y <= ambientRange; y++)
        for (int z = -ambientRange; z <= ambientRange; z++)
        {
            CastAmbientLight(ambientRange, y, z, -1, 0, 0);
            CastAmbientLight(-ambientRange, y, z, 1, 0, 0);
        }
        
        for (int x = -ambientRange; x <= ambientRange; x++)
        for (int y = -ambientRange; y <= ambientRange; y++)
        {
            CastAmbientLight(x, y, ambientRange, 0, 0, -1);
            CastAmbientLight(x, y, -ambientRange, 0, 0, 1);
        }
        
        void CastAmbientLight(int x, int y, int z, int vectorX, int vectorY, int vectorZ)
        {
            for (var step = 1; step <= 32; step++)
            {
                int bx = x + vectorX * step;
                int by = y + vectorY * step;
                int bz = z + vectorZ * step;
                VoxelSample sample = voxelObject.Sample(bx, by, bz);

                if (sample.Center.ID != 0)
                {
                    return;
                }

                if (!sample.HasAny())
                {
                    continue;
                }
                
                ShapeLight shapeLight = sample.Center.ShapeLight;
                sample.Center.ShapeLight = new ShapeLight(shapeLight.Shape, lightLevel: 15);
                
                var light = new LightingState.VoxelLight(bx, by, bz, sample.Center);
                _lightingState.Lights.Enqueue(light);
            }
        }
    }
}