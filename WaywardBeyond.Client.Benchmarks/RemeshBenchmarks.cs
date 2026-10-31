using System.Collections.Generic;
using BenchmarkDotNet.Attributes;
using DryIoc;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Data;
using WaywardBeyond.Voxels;

namespace WaywardBeyond.Client.Benchmarks;

[MemoryDiagnoser]
public class RemeshBenchmarks
{
    private readonly Voxel _stone = new(_ID: 1, _ShapeLight: new ShapeLight(default, 0), _Orientation: 0);

    private VoxelObjectBuilder _builder = null!;
    private VoxelObjectBuilder.ChunkBuildContext _context = null!;
    private ChunkData _denseChunk;
    private ChunkData _sparseChunk;
    private VoxelObject _dense = null!;
    private VoxelObject _sparse = null!;
    private VoxelObjectProcessor _lightProcessor = null!;

    [GlobalSetup]
    public void Setup()
    {
        var brickDatabase = new FakeBrickDatabase();
        brickDatabase.Bricks[1] = new Brick(
            "stone",
            alpha: BrickAlpha.Opaque,
            passable: false,
            meshID: null,
            BrickShape.Block,
            textures: default,
            tags: []
        );

        var stoneTexture = new Texture("stone", [255, 255, 255, 255], 1, 1, mipmaps: false);
        var albedo = new TextureArray("albedo", [stoneTexture], mipmaps: false);
        var textureArrays = new PBRTextureArrays(albedo, albedo, albedo, albedo, albedo);

        var container = new Container();
        container.RegisterInstance<IBrickDatabase>(brickDatabase);
        container.RegisterInstance<PBRTextureArrays>(textureArrays);
        container.RegisterInstance<IAssetDatabase<Mesh>>(new FakeMeshDatabase());
        container.Register<MeshState>(Reuse.Scoped);
        container.Register<EntityState>(Reuse.Scoped);
        container.Register<CollisionState>(Reuse.Scoped);
        container.Register<MeshPostPass>(Reuse.Scoped);
        container.Register<VoxelEntityPostPass>(Reuse.Scoped);
        container.Register<CollisionPostPass>(Reuse.Scoped);

        _builder = new VoxelObjectBuilder(container);
        _context = _builder.OpenChunkBuildContext();

        _dense = new VoxelObject(16);
        for (int x = 0; x < 16; x++)
        for (int y = 0; y < 16; y++)
        for (int z = 0; z < 16; z++)
        {
            _dense.Set(x, y, z, _stone);
        }

        _dense.TryGetChunk(new Short3(0, 0, 0), out _denseChunk);

        _sparse = new VoxelObject(16);
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        {
            _sparse.Set(x, 0, z, _stone);
        }

        _sparse.TryGetChunk(new Short3(0, 0, 0), out _sparseChunk);

        var lightingState = new LightingState();
        var depthState = new DepthState();
        VoxelObjectProcessor.IPass[] passes = [new AmbientLightPass(lightingState, depthState, brickDatabase)];
        VoxelObjectProcessor.IVoxelPass[] voxelPasses = [new LightSeedPrePass(brickDatabase)];
        VoxelObjectProcessor.ISamplePass[] samplePasses = [new DepthPrePass(depthState, brickDatabase)];
        _lightProcessor = new VoxelObjectProcessor(passes, voxelPasses, samplePasses);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _context.Dispose();
    }

    [Benchmark]
    public VoxelObjectBuilder.ChunkMeshData BuildChunk_Dense()
    {
        return _builder.BuildChunk(_context, _denseChunk);
    }

    [Benchmark]
    public VoxelObjectBuilder.ChunkMeshData BuildChunk_Sparse()
    {
        return _builder.BuildChunk(_context, _sparseChunk);
    }

    [Benchmark]
    public ChunkCollisionData BuildChunkCollision_Dense()
    {
        return _builder.BuildChunkCollision(_context, _denseChunk);
    }

    [Benchmark]
    public ChunkCollisionData BuildChunkCollision_Sparse()
    {
        return _builder.BuildChunkCollision(_context, _sparseChunk);
    }

    [Benchmark]
    public VoxelObject BuildLighting_SparseChunk()
    {
        var voxelObject = new VoxelObject(16);
        for (int x = 0; x < 16; x++)
        for (int z = 0; z < 16; z++)
        {
            voxelObject.Set(x, 0, z, _stone);
        }

        _lightProcessor.ProcessLighting(voxelObject);
        return voxelObject;
    }

    private sealed class FakeBrickDatabase : IBrickDatabase
    {
        public readonly Dictionary<ushort, Brick> Bricks = [];

        public Result<Brick> Get(string id) => Result<Brick>.FromFailure((string?)null);

        public Result<Brick> Get(ushort id)
        {
            return Bricks.TryGetValue(id, out Brick brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure((string?)null);
        }

        public List<Brick> Get(Func<Brick, bool> predicate) => [];

        public bool IsCuller(in Voxel voxel, BrickShape shape)
        {
            if (voxel.ID == 0 || shape != BrickShape.Block)
            {
                return false;
            }

            if (Bricks.TryGetValue(voxel.ID, out Brick brick))
            {
                return brick.Alpha == BrickAlpha.Opaque && !brick.Passable;
            }

            return true;
        }
    }

    private sealed class FakeMeshDatabase : IAssetDatabase<Mesh>
    {
        private readonly Mesh _mesh = new([], [], [], [], []);

        public Result<Mesh> Get(string id) => Result<Mesh>.FromSuccess(_mesh);
    }
}
