using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Client.Core.Voxels.Processing;

namespace WaywardBeyond.Client.Core.Tests;

public class VoxelObjectProcessorTests
{
    private const int SOLID_VOXEL = 1;
    private const int LIGHT_VOXEL = 2;
    
    [Test]
    public void LightPropagationTest()
    {
        var voxelObject = new VoxelObject(chunkSize: 16);
        
        voxelObject.Set(-1, 0, 0, new Voxel(LIGHT_VOXEL, 0, 0));
        voxelObject.Set(0, 0, -1, new Voxel(LIGHT_VOXEL, 0, 0));
        voxelObject.Set(-1, 0, -1, new Voxel(LIGHT_VOXEL, 0, 0));
        voxelObject.Set(0, 0, 0, new Voxel(LIGHT_VOXEL, 0, 0));

        var lightingState = new LightingState();
        IBrickDatabase brickDatabase = new TestBrickDatabase();
        
        var passes = new VoxelObjectProcessor.IPass[]
        {
            new AmbientLightPass(lightingState),
            new LightPropagationPass(lightingState, brickDatabase),
        };

        var voxelPasses = new VoxelObjectProcessor.IVoxelPass[]
        {
            new LightSeedPrePass(brickDatabase),
        };

        var samplePasses = new VoxelObjectProcessor.ISamplePass[]
        {
            new LightPropagationPrePass(lightingState, brickDatabase),
            new MeshPostPass(),
        };
        
        var processor = new VoxelObjectProcessor(passes, voxelPasses, samplePasses);
        int passCount = processor.Process(voxelObject);
        Console.WriteLine($"Completed {passCount} passes.");

        var lightData = new int[32, 32];
        foreach (VoxelSample sample in voxelObject.GetSampler())
        {
            int y = sample.Coords.Y + sample.ChunkOffset.Y;
            if (y != 0)
            {
                continue;
            }
            
            int x = sample.Coords.X + sample.ChunkOffset.X + 16;
            int z = sample.Coords.Z + sample.ChunkOffset.Z + 16;
            int lightLevel = sample.Center.GetLightLevel();
            lightData[x, z] = lightLevel;
        }

        for (var y = 0; y < lightData.GetLength(1); y++)
        {
            for (var x = 0; x < lightData.GetLength(0); x++)
            {
                int light = lightData[x, y];
                string lightStr = light != 0 ? light.ToString("00") : "--";
                Console.Write(lightStr + "-");
            }
            Console.WriteLine();
        }
    }
    
    [Test]
    public void AmbientLightTest()
    {
        var voxelObject = new VoxelObject(chunkSize: 16);
        
        voxelObject.Set(-1, 0, 0, new Voxel(SOLID_VOXEL, 0, 0));
        voxelObject.Set(0, 0, -1, new Voxel(SOLID_VOXEL, 0, 0));
        voxelObject.Set(-1, 0, -1, new Voxel(SOLID_VOXEL, 0, 0));
        voxelObject.Set(0, 0, 0, new Voxel(SOLID_VOXEL, 0, 0));

        var lightingState = new LightingState();
        IBrickDatabase brickDatabase = new TestBrickDatabase();
        
        var passes = new VoxelObjectProcessor.IPass[]
        {
            new AmbientLightPass(lightingState),
            new LightPropagationPass(lightingState, brickDatabase),
        };

        var voxelPasses = new VoxelObjectProcessor.IVoxelPass[]
        {
            new LightSeedPrePass(brickDatabase),
        };

        var samplePasses = new VoxelObjectProcessor.ISamplePass[]
        {
            new LightPropagationPrePass(lightingState, brickDatabase),
            new MeshPostPass(),
        };
        
        var processor = new VoxelObjectProcessor(passes, voxelPasses, samplePasses);
        int passCount = processor.Process(voxelObject);
        Console.WriteLine($"Completed {passCount} passes.");

        var lightData = new int[32, 32];
        foreach (VoxelSample sample in voxelObject.GetSampler())
        {
            int y = sample.Coords.Y + sample.ChunkOffset.Y;
            if (y != 0)
            {
                continue;
            }
            
            int x = sample.Coords.X + sample.ChunkOffset.X + 16;
            int z = sample.Coords.Z + sample.ChunkOffset.Z + 16;
            int lightLevel = sample.Center.GetLightLevel();
            lightData[x, z] = lightLevel;
        }

        for (var y = 0; y < lightData.GetLength(1); y++)
        {
            for (var x = 0; x < lightData.GetLength(0); x++)
            {
                int light = lightData[x, y];
                string lightStr = light != 0 ? light.ToString("00") : "--";
                Console.Write(lightStr + "-");
            }
            Console.WriteLine();
        }
    }
    
    private class TestBrickDatabase : IBrickDatabase
    {
        private readonly BrickInfo _emptyBrickInfo =  new(
            id: string.Empty,
            dataID: 0,
            transparent: false,
            passable: false,
            mesh: null,
            BrickShape.Block,
            new BrickTextures(),
            tags: null
        );
        
        private readonly BrickInfo _solidBrickInfo =  new(
            id: string.Empty,
            dataID: SOLID_VOXEL,
            transparent: false,
            passable: false,
            mesh: null,
            BrickShape.Block,
            new BrickTextures(),
            tags: null
        );
        
        private readonly BrickInfo _lightBrickInfo =  new(
            id: string.Empty,
            dataID: LIGHT_VOXEL,
            transparent: false,
            passable: false,
            mesh: null,
            BrickShape.Block,
            new BrickTextures(),
            tags: ["light"]
        );
        
        public bool IsCuller(Voxel voxel)
        {
            return voxel.ID != 0;
        }

        public bool IsCuller(Voxel voxel, ShapeLight shapeLight)
        {
            return voxel.ID != 0;
        }

        public bool IsCuller(Voxel voxel, BrickShape shape)
        {
            return voxel.ID != 0;
        }

        public Result<BrickInfo> Get(ushort id)
        {
            BrickInfo info = id switch
            {
                SOLID_VOXEL => _solidBrickInfo,
                LIGHT_VOXEL => _lightBrickInfo,
                _ => _emptyBrickInfo,
            };

            return Result<BrickInfo>.FromSuccess(info);
        }

        public List<BrickInfo> Get(Func<BrickInfo, bool> predicate)
        {
            return [_lightBrickInfo];
        }
    }
}