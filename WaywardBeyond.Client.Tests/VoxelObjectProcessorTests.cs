using System.Diagnostics;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Data;

namespace WaywardBeyond.Client.Tests;

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
            // new AmbientLightPass(lightingState),
            new LightPropagationPass(lightingState, brickDatabase),
        };

        var voxelPasses = new VoxelObjectProcessor.IVoxelPass[]
        {
            new LightSeedPrePass(brickDatabase),
        };

        var samplePasses = new VoxelObjectProcessor.ISamplePass[]
        {
            new LightPropagationPrePass(lightingState, brickDatabase),
        };
        
        var processor = new VoxelObjectProcessor(passes, voxelPasses, samplePasses);
        var sw = Stopwatch.StartNew();
        int passCount = processor.Process(voxelObject);
        sw.Stop();
        Console.WriteLine($"Completed {passCount} passes in {sw.ElapsedMilliseconds} ms.");

        var lightData = new int[32, 32];
        foreach (ChunkData chunk in voxelObject)
        foreach (VoxelSample sample in chunk.GetSampler())
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
                Console.Write(lightStr + " ");
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
        var depthState = new DepthState();
        IBrickDatabase brickDatabase = new TestBrickDatabase();
        
        var passes = new VoxelObjectProcessor.IPass[]
        {
            new AmbientLightPass(lightingState, depthState),
            new LightPropagationPass(lightingState, brickDatabase),
        };

        var voxelPasses = new VoxelObjectProcessor.IVoxelPass[]
        {
            new LightSeedPrePass(brickDatabase),
        };

        var samplePasses = new VoxelObjectProcessor.ISamplePass[]
        {
            new DepthPrePass(depthState),
            new LightPropagationPrePass(lightingState, brickDatabase),
        };
        
        var processor = new VoxelObjectProcessor(passes, voxelPasses, samplePasses);
        var sw = Stopwatch.StartNew();
        int passCount = processor.Process(voxelObject);
        sw.Stop();
        Console.WriteLine($"Completed {passCount} passes in {sw.ElapsedMilliseconds} ms.");

        var lightData = new int[32, 32];
        foreach (ChunkData chunk in voxelObject)
        foreach (VoxelSample sample in chunk.GetSampler())
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
                Console.Write(lightStr + " ");
            }
            Console.WriteLine();
        }
    }
    
    private class TestBrickDatabase : IBrickDatabase
    {
        private readonly Brick _emptyBrick = new(
            id: string.Empty,
            transparent: false,
            passable: false,
            meshID: null,
            BrickShape.Block,
            new BrickTextures(),
            tags: null
        ) { DataID = 0 };

        private readonly Brick _solidBrick = new(
            id: string.Empty,
            transparent: false,
            passable: false,
            meshID: null,
            BrickShape.Block,
            new BrickTextures(),
            tags: null
        ) { DataID = SOLID_VOXEL };

        private readonly Brick _lightBrick = new(
            id: string.Empty,
            transparent: false,
            passable: false,
            meshID: null,
            BrickShape.Block,
            new BrickTextures(),
            tags: ["wb:light"]
        ) { DataID = LIGHT_VOXEL };
        
        public bool IsCuller(in Voxel voxel, BrickShape shape)
        {
            return voxel.ID != 0;
        }

        public Result<Brick> Get(ushort id)
        {
            Brick info = id switch
            {
                SOLID_VOXEL => _solidBrick,
                LIGHT_VOXEL => _lightBrick,
                _ => _emptyBrick,
            };

            return Result<Brick>.FromSuccess(info);
        }

        public List<Brick> Get(Func<Brick, bool> predicate)
        {
            return [_lightBrick];
        }

        public Result<Brick> Get(string id)
        {
            return Result<Brick>.FromFailure($"Unknown brick \"{id}\"");
        }
    }
}