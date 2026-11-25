#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using DryIoc;
using Krill;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Collections;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Numerics;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Data;
using WaywardBeyond.Meshing;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Voxels;
using Xunit;
using ClientGameState = WaywardBeyond.Client.GameState;
using ClientHost = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>Tests client reconciliation of fragmented server region edits and newly spawned dynamic structures.</summary>
[Collection("ClientGameState")]
public class ClientRegionReconcileTests
{
    [Fact]
    public void MultiPartRegionEditUpdatesVoxelsClearsDepthStateAndMarksDirtyAndLightChunks()
    {
        using var fixture = new Fixture();
        ushort rockId = fixture.Registry.Id("wb:rock");
        ushort panelId = fixture.Registry.Id("wb:panel");

        Uuid structureUuid = Uuid.FromValue(777);
        int entity = fixture.Store.Alloc(structureUuid);
        var targetVoxels = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        targetVoxels.Set(0, 0, 0, new Voxel(rockId, new ShapeLight(BrickShape.Block, 0), 0));
        targetVoxels.Set(32, 0, 0, new Voxel(rockId, new ShapeLight(BrickShape.Block, 0), 0));
        targetVoxels.Set(64, 64, 64, new Voxel(rockId, new ShapeLight(BrickShape.Block, 0), 0));
        var registry = new VoxelChunkRegistry();
        registry.KnownChunks.Add(new Short3(0, 0, 0));
        registry.KnownChunks.Add(new Short3(2, 0, 0));
        registry.KnownChunks.Add(new Short3(4, 4, 4));
        registry.DepthState.XY[new Int2(0, 0)] = new Int2(1, 5);
        registry.DepthStateInitialized = false;
        fixture.Store.AddOrUpdate(entity, new VoxelComponent(targetVoxels, registry));

        var sourceVoxels = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        for (var x = 0; x < 3; x++)
        {
            sourceVoxels.Set(x, 0, 0, new Voxel(panelId, new ShapeLight(BrickShape.Block, 0), 0));
        }

        VoxelRegionExtractResult extracted = VoxelRegionExtractor.Extract(sourceVoxels, new Int3(0, 0, 0), new Int3(2, 0, 0));
        VoxelRegionData encoded = VoxelCodec.Encode(extracted.Region, fixture.Registry);

        var wireMessage = new VoxelRegionEditMessage
        {
            EntityUuid = structureUuid.ToValue(),
            OriginX = 2,
            OriginY = 0,
            OriginZ = 0,
            OverwriteAir = false,
            SizeX = encoded.SizeX,
            SizeY = encoded.SizeY,
            SizeZ = encoded.SizeZ,
            Chunks = encoded.Chunks,
            BrickPalette = encoded.BrickPalette,
            PaintPalette = encoded.PaintPalette,
        };

        byte[] serialized = wireMessage.Serialize();
        int split = serialized.Length / 2;
        byte[] part0 = serialized[..split];
        byte[] part1 = serialized[split..];

        fixture.Inbound.VoxelRegionEdits.Enqueue(new VoxelRegionEditPart
        {
            BatchId = 10,
            PartIndex = 0,
            PartCount = 2,
            Payload = part0,
        });
        fixture.TickWhilePlaying();
        Assert.Equal(0, targetVoxels.Get(2, 0, 0).ID);

        fixture.Inbound.VoxelRegionEdits.Enqueue(new VoxelRegionEditPart
        {
            BatchId = 10,
            PartIndex = 1,
            PartCount = 2,
            Payload = part1,
        });
        fixture.TickWhilePlaying();

        Assert.Equal(panelId, targetVoxels.Get(2, 0, 0).ID);
        Assert.Equal(panelId, targetVoxels.Get(4, 0, 0).ID);
        Assert.True(registry.DepthStateInitialized);
        Assert.Contains(new Short3(0, 0, 0), registry.GeometryDirtyChunks);
        Assert.Contains(new Short3(2, 0, 0), registry.DirtyChunks);
        Assert.Contains(new Short3(2, 0, 0), registry.LightDirtyChunks);
        Assert.DoesNotContain(new Short3(0, 0, 0), registry.LightDirtyChunks);
        Assert.DoesNotContain(new Short3(4, 4, 4), registry.DirtyChunks);
        Assert.DoesNotContain(new Short3(4, 4, 4), registry.LightDirtyChunks);
    }

    [Fact]
    public void EntitySpawnStreamCreatesNewEntityAndPreservesExistingSnapshotTransformOnAttach()
    {
        using var fixture = new Fixture();
        ushort panelId = fixture.Registry.Id("wb:panel");

        // 1. Fresh spawn (no prior entity in DataStore)
        Uuid freshUuid = Uuid.FromValue(9001);
        EnqueueSpawn(fixture, freshUuid, panelId, new Vector3(10f, 4f, -3f));
        fixture.TickWhilePlaying();

        Assert.True(fixture.Store.TryGet(freshUuid, out int freshEntity));
        Assert.True(fixture.Store.TryGet(freshEntity, out VoxelComponent freshVoxel));
        Assert.True(fixture.Store.TryGet(freshEntity, out TransformComponent freshTransform));
        Assert.Equal(panelId, freshVoxel.VoxelObject.Get(0, 0, 0).ID);
        Assert.Equal(new Vector3(10f, 4f, -3f), freshTransform.Position);

        // 2. Snapshot-preallocated entity preserves its existing TransformComponent
        Uuid existingUuid = Uuid.FromValue(9002);
        int existingEntity = fixture.Store.Alloc(existingUuid);
        var snapshotPos = new Vector3(42f, 12f, 8f);
        Quaternion snapshotRot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.75f);
        fixture.Store.AddOrUpdate(existingEntity, new TransformComponent(snapshotPos, snapshotRot, Vector3.One));

        EnqueueSpawn(fixture, existingUuid, panelId, new Vector3(1f, 2f, 3f));
        fixture.TickWhilePlaying();

        Assert.True(fixture.Store.TryGet(existingEntity, out VoxelComponent attachedVoxel));
        Assert.True(fixture.Store.TryGet(existingEntity, out TransformComponent preservedTransform));
        Assert.Equal(panelId, attachedVoxel.VoxelObject.Get(0, 0, 0).ID);
        Assert.Equal(snapshotPos, preservedTransform.Position);
        Assert.True(Quaternion.Dot(snapshotRot, preservedTransform.Orientation) > 0.9999f);
    }

    [Fact]
    public void AirOverwriteRegionEditEmptyingChunkRemovesItFromKnownChunks()
    {
        using var fixture = new Fixture();
        ushort rockId = fixture.Registry.Id("wb:rock");

        Uuid structureUuid = Uuid.FromValue(778);
        int entity = fixture.Store.Alloc(structureUuid);
        var targetVoxels = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        targetVoxels.Set(0, 0, 0, new Voxel(rockId, new ShapeLight(BrickShape.Block, 0), 0));
        targetVoxels.Set(32, 0, 0, new Voxel(rockId, new ShapeLight(BrickShape.Block, 0), 0));
        targetVoxels.Set(64, 64, 64, new Voxel(rockId, new ShapeLight(BrickShape.Block, 0), 0));
        var registry = new VoxelChunkRegistry();
        registry.KnownChunks.Add(new Short3(0, 0, 0));
        registry.KnownChunks.Add(new Short3(2, 0, 0));
        registry.KnownChunks.Add(new Short3(4, 4, 4));
        fixture.Store.AddOrUpdate(entity, new VoxelComponent(targetVoxels, registry));

        var wireMessage = new VoxelRegionEditMessage
        {
            EntityUuid = structureUuid.ToValue(),
            OriginX = 0,
            OriginY = 0,
            OriginZ = 0,
            OverwriteAir = true,
            SizeX = 1,
            SizeY = 1,
            SizeZ = 1,
            Chunks = [],
            BrickPalette = null,
            PaintPalette = null,
        };

        byte[] payload = wireMessage.Serialize();
        fixture.Inbound.VoxelRegionEdits.Enqueue(new VoxelRegionEditPart
        {
            BatchId = 11,
            PartIndex = 0,
            PartCount = 1,
            Payload = payload,
        });
        fixture.TickWhilePlaying();

        Assert.Equal(0, targetVoxels.Get(0, 0, 0).ID);
        Assert.DoesNotContain(new Short3(0, 0, 0), registry.KnownChunks);
        Assert.Contains(new Short3(0, 0, 0), registry.GeometryDirtyChunks);
        Assert.Contains(new Short3(2, 0, 0), registry.KnownChunks);
        Assert.Contains(new Short3(2, 0, 0), registry.LightDirtyChunks);
    }

    private static void EnqueueSpawn(Fixture fixture, Uuid uuid, ushort brickId, Vector3 pos)
    {
        var obj = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        obj.Set(0, 0, 0, new Voxel(brickId, new ShapeLight(BrickShape.Block, 0), 0));
        var rawEntityData = new VoxelEntityData(
            uuid.ToValue(),
            pos.X, pos.Y, pos.Z,
            0f, 0f, 0f, 1f,
            1f, 1f, 1f,
            obj.GetChunkInfos(),
            _BrickPalette: null,
            _PaintPalette: null);
        VoxelEntityData wire = VoxelCodec.Encode(in rawEntityData, fixture.Registry);

        byte[] payload = wire.Serialize();
        int partCount = PayloadFragmenter.PartCount(payload.Length);
        for (var i = 0; i < partCount; i++)
        {
            fixture.Inbound.VoxelEntitySpawns.Enqueue(new VoxelEntitySpawnPart
            {
                BatchId = (uint)uuid.ToValue(),
                PartIndex = (uint)i,
                PartCount = (uint)partCount,
                Payload = PayloadFragmenter.Slice(payload, i),
            });
        }
    }

    private sealed class Fixture : IDisposable
    {
        private readonly Container _container = new();
        public readonly TestBrickRegistry Registry = new();
        public readonly TestBrickDatabase Bricks = new();
        public readonly ClientInbound Inbound = new();
        public readonly DataStore Store = new();
        public readonly ClientRegionReconcileSystem System;

        public Fixture()
        {
            ushort rockId = Registry.Bind("wb:rock");
            ushort panelId = Registry.Bind("wb:panel");
            Bricks.Add(rockId, new Brick("wb:rock", BrickAlpha.Opaque, passable: false, null, BrickShape.Block, default));
            Bricks.Add(panelId, new Brick("wb:panel", BrickAlpha.Opaque, passable: false, null, BrickShape.Block, default));

            var albedo = new TextureArray(
                "albedo",
                [new Texture("missing", [255, 0, 255, 255], 1, 1, mipmaps: false)],
                mipmaps: false);
            var other = new TextureArray(
                "other",
                [new Texture("other", [0, 0, 0, 255], 1, 1, mipmaps: false)],
                mipmaps: false);
            var textures = new PBRTextureArrays(albedo, other, other, other, other, other);

            _container.RegisterInstance<IContainer>(_container);
            _container.RegisterInstance<IBrickDatabase>(Bricks);
            _container.RegisterInstance(textures);
            _container.RegisterInstance<ITextureLayerLookup>(new TextureArrayLayerLookup(albedo));
            _container.RegisterInstance<IAssetDatabase<Mesh>>(new EmptyMeshDatabase());
            _container.RegisterInstance<IAssetDatabase<VoxelMesh>>(new EngineMeshPreviewDatabase(new EmptyMeshDatabase()));
            _container.Register<MeshState>(Reuse.Scoped);
            _container.Register<EntityState>(Reuse.Scoped);
            _container.Register<CollisionState>(Reuse.Scoped);
            _container.Register<DepthState>(Reuse.Scoped);
            _container.Register<LightingState>(Reuse.Scoped);
            _container.Register<MeshPostPass>(Reuse.Scoped);
            _container.Register<VoxelEntityPostPass>(Reuse.Scoped);
            _container.Register<CollisionPostPass>(Reuse.Scoped);
            _container.Register<VoxelObjectProcessor>(Reuse.Scoped);
            _container.Register<DepthPrePass>(Reuse.Scoped);
            _container.RegisterMapping<VoxelObjectProcessor.ISamplePass, DepthPrePass>();
            _container.Register<LightSeedPrePass>(Reuse.Scoped);
            _container.RegisterMapping<VoxelObjectProcessor.IVoxelPass, LightSeedPrePass>();
            _container.Register<LightPropagationPrePass>(Reuse.Scoped);
            _container.RegisterMapping<VoxelObjectProcessor.ISamplePass, LightPropagationPrePass>();
            _container.Register<LightPropagationPass>(Reuse.Scoped);
            _container.RegisterMapping<VoxelObjectProcessor.IPass, LightPropagationPass>();
            _container.Register<AmbientLightPass>(Reuse.Scoped);
            _container.RegisterMapping<VoxelObjectProcessor.IPass, AmbientLightPass>();

            var objectBuilder = new VoxelObjectBuilder(_container);
            var materials = new VoxelMaterials(new Shader("test"), textures);
            var entityBuilder = new VoxelEntityBuilder(
                NullLogger<VoxelEntityBuilder>.Instance,
                materials,
                objectBuilder,
                [],
                new VoxelSettings());

            System = new ClientRegionReconcileSystem(
                Inbound,
                Registry,
                objectBuilder,
                entityBuilder,
                Logger);
        }

        public readonly RecordingLogger Logger = new();

        public void TickWhilePlaying()
        {
            ClientHost.GameState.Set(ClientGameState.Playing);
            try
            {
                System.Tick(1f / 60f, Store);
                Assert.Empty(Logger.Warnings);
            }
            finally
            {
                ClientHost.GameState.Set(ClientGameState.MainMenu);
            }
        }

        public void Dispose()
        {
            _container.Dispose();
        }
    }

    private sealed class TestBrickRegistry : IBrickRegistry
    {
        private readonly Dictionary<string, ushort> _ids = new(StringComparer.Ordinal);

        public ushort Bind(string id)
        {
            ushort val = (ushort)(_ids.Count + 1);
            _ids[id] = val;
            return val;
        }

        public ushort Id(string name) => _ids.TryGetValue(name, out ushort id) ? id : (ushort)0;

        public string? Name(ushort id)
        {
            foreach ((string name, ushort value) in _ids)
            {
                if (value == id)
                {
                    return name;
                }
            }

            return null;
        }
    }

    private sealed class TestBrickDatabase : IBrickDatabase
    {
        private readonly Dictionary<ushort, Brick> _bricks = [];

        public void Add(ushort id, Brick brick) => _bricks[id] = brick;

        public Result<Brick> Get(string id)
        {
            Brick? found = _bricks.Values.FirstOrDefault(b => b.ID == id);
            return found != null ? Result<Brick>.FromSuccess(found) : Result<Brick>.FromFailure("Unknown brick");
        }

        public Result<Brick> Get(ushort id) =>
            _bricks.TryGetValue(id, out Brick? brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure("Unknown brick");

        public List<Brick> Get(Func<Brick, bool> predicate) => [.. _bricks.Values.Where(predicate)];

        public bool IsCuller(in Voxel voxel, BrickShape shape) =>
            voxel.ID != 0 && shape == BrickShape.Block && _bricks.TryGetValue(voxel.ID, out Brick? b) && b.Alpha == BrickAlpha.Opaque;
    }

    private sealed class EmptyMeshDatabase : IAssetDatabase<Mesh>
    {
        public Result<Mesh> Get(string id) => Result<Mesh>.FromFailure("No mesh");
    }

    private sealed class RecordingLogger : ILogger<ClientRegionReconcileSystem>
    {
        public readonly List<string> Warnings = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel >= LogLevel.Warning)
            {
                Warnings.Add($"{formatter(state, exception)}: {exception}");
            }
        }
    }
}
