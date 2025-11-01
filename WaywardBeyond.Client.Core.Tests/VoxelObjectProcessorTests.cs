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
    public void Test1()
    {
        var voxelObject = new VoxelObject(chunkSize: 16);
        
        voxelObject.Set(-1, 0, 0, new Voxel(SOLID_VOXEL, 0, 0));
        voxelObject.Set(0, 0, 0, new Voxel(SOLID_VOXEL, 0, 0));
        voxelObject.Set(8, 8, 8, new Voxel(LIGHT_VOXEL, 0, 0));
        voxelObject.Set(15, 15, 15, new Voxel(SOLID_VOXEL, 0, 0));
        voxelObject.Set(16, 16, 16, new Voxel(SOLID_VOXEL, 0, 0));
        voxelObject.Set(24, 24, 24, new Voxel(SOLID_VOXEL, 0, 0));
        voxelObject.Set(31, 31, 31, new Voxel(SOLID_VOXEL, 0, 0));

        var lightingState = new LightingState();
        IBrickDatabase brickDatabase = new TestBrickDatabase();
        
        var passes = new VoxelObjectProcessor.IPass[]
        {
            new AmbientLightPass(),
            new LightPropagationPass(lightingState, brickDatabase),
        };

        var voxelPasses = new VoxelObjectProcessor.IVoxelPass[]
        {
            new LightSeedPrePass(brickDatabase),
        };

        var samplePasses = new VoxelObjectProcessor.ISamplePass[]
        {
            new LightPropagationPrePass(lightingState),
            new MeshPostPass(),
        };
        
        var processor = new VoxelObjectProcessor(passes, voxelPasses, samplePasses);
        int passCount = processor.Process(voxelObject);
        Console.WriteLine($"Completed {passCount} passes.");
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
    }
}