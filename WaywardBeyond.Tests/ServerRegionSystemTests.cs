#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using DryIoc;
using Krill;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NsdRpc;
using NsdRpc.Wire;
using Swordfish.ECS;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Voxels;
using Xunit;
using RpcServerConnection = NsdRpc.IServerConnection;

namespace WaywardBeyond.Tests;

/// <summary>Tests server-authoritative cut, paste, spawn, permission checks, sanitization, and fragmentation.</summary>
public class ServerRegionSystemTests
{
    [Fact]
    public void ShippedDefaultPermissionsGrantRegionWildcard()
    {
        string serverAssets = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "WaywardBeyond.Server", "assets"));
        string defaultPermPath = Path.Combine(serverAssets, "permissions", "default.toml");
        Assert.True(File.Exists(defaultPermPath), $"Expected permissions file at {defaultPermPath}");

        var vfs = new VirtualFileSystem();
        Assert.True(vfs.Mount(serverAssets).Success);
        var loader = new PermissionFileLoader(
            vfs,
            new InlineTomlParser(),
            AppPaths.ForDirectory(new PathInfo(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()))));
        IReadOnlyList<SourcedPermissionFile> files = loader.Load(new PermissionDiagnostics());
        PermissionPolicy policy = PermissionPolicy.Create(files);
        var service = new UserPermissionService(new PermissionResolver(policy));
        Uuid remoteClient = Uuid.FromValue(9001);
        service.Bind(remoteClient, new UserClaim("any_player"), isHost: false);

        Assert.True(service.HasPermission(remoteClient, GamePermissions.REGION_CUT));
        Assert.True(service.HasPermission(remoteClient, GamePermissions.REGION_PASTE));
        Assert.True(service.HasPermission(remoteClient, GamePermissions.REGION_SPAWN));
    }

    [Fact]
    public async Task CutRequiresCreativeMode()
    {
        using TestHarness harness = CreateHarness(GameMode.Adventure);

        Task<RpcResult<RegionOpResponse>> task = EnqueueCut(harness, harness.StructureUuid, new Int3(0, 0, 0), new Int3(0, 0, 0));
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> result = await task;
        Assert.True(result.IsSuccess);
        Assert.False(result.Value.Success);
        Assert.Equal("notification.region.creativeOnly", result.Value.ErrorKey);
        Assert.NotEqual(0, harness.StructureVoxels.Get(0, 0, 0).ID);
    }

    [Fact]
    public async Task CutAndPasteCheckPermissions()
    {
        const string denyToml = """
            [Groups.default]
            Permissions = []
            """;
        using TestHarness harness = CreateHarness(GameMode.Creative, isLocalHost: false, permissionsToml: denyToml);

        Task<RpcResult<RegionOpResponse>> cutTask = EnqueueCut(harness, harness.StructureUuid, new Int3(0, 0, 0), new Int3(0, 0, 0));
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> cutResult = await cutTask;
        Assert.False(cutResult.IsSuccess);
        Assert.Equal(GameRpcErrors.PERMISSION_DENIED, cutResult.ErrorCode);

        VoxelRegionData region = MakeSinglePanelRegion(harness.BrickRegistry, shape: BrickShape.Block);
        Task<RpcResult<RegionOpResponse>> pasteTask = EnqueueSinglePaste(
            harness,
            PasteTargetKind.IntoStructure,
            harness.StructureUuid,
            new Int3(2, 0, 0),
            overwriteAir: false,
            region);
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> pasteResult = await pasteTask;
        Assert.False(pasteResult.IsSuccess);
        Assert.Equal(GameRpcErrors.PERMISSION_DENIED, pasteResult.ErrorCode);

        Task<RpcResult<RegionOpResponse>> spawnTask = EnqueueSinglePaste(
            harness,
            PasteTargetKind.NewStructure,
            targetEntity: 0,
            new Int3(0, 0, 0),
            overwriteAir: false,
            region,
            spawnPos: new Vector3(10f, 0f, 0f));
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> spawnResult = await spawnTask;
        Assert.False(spawnResult.IsSuccess);
        Assert.Equal(GameRpcErrors.PERMISSION_DENIED, spawnResult.ErrorCode);
    }

    [Fact]
    public async Task CutClearsVoxelsRebuildsColliderAndBroadcastsEdit()
    {
        using TestHarness harness = CreateHarness(GameMode.Creative);
        // Place a second brick outside cut bounds so structure remains alive
        harness.StructureVoxels.Set(5, 0, 0, new Voxel(harness.BrickRegistry.Id("wb:panel"), new ShapeLight(BrickShape.Block, 0), 0));

        Task<RpcResult<RegionOpResponse>> task = EnqueueCut(harness, harness.StructureUuid, new Int3(0, 0, 0), new Int3(1, 0, 0));
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> result = await task;
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Success);
        Assert.True(result.Value.Completed);
        Assert.Equal(1, result.Value.AffectedBricks);
        Assert.Equal(0, harness.StructureVoxels.Get(0, 0, 0).ID);
        Assert.NotEqual(0, harness.StructureVoxels.Get(5, 0, 0).ID);
        Assert.Single(harness.ClientRegionEdits);
        Assert.True(harness.ClientRegionEdits[0].OverwriteAir);
        Assert.Equal(2, harness.ClientRegionEdits[0].SizeX);
    }

    [Fact]
    public async Task CutToEmptyDespawnsAndFreesStructure()
    {
        using TestHarness harness = CreateHarness(GameMode.Creative);

        Task<RpcResult<RegionOpResponse>> task = EnqueueCut(harness, harness.StructureUuid, new Int3(0, 0, 0), new Int3(0, 0, 0));
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> result = await task;
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Success);
        Assert.Equal(1, result.Value.AffectedBricks);
        Assert.False(harness.Store.TryGet(Uuid.FromValue(harness.StructureUuid), out _));
        Assert.Empty(harness.ClientRegionEdits);
    }

    [Fact]
    public async Task CutRejectsOversizedBoundsAndOutOfReach()
    {
        using TestHarness harness = CreateHarness(GameMode.Creative);

        Task<RpcResult<RegionOpResponse>> boundsTask = EnqueueCut(
            harness,
            harness.StructureUuid,
            new Int3(0, 0, 0),
            new Int3(RegionLimits.MAX_REGION_EDGE, 0, 0));
        Task<RpcResult<RegionOpResponse>> reachTask = EnqueueCut(
            harness,
            harness.StructureUuid,
            new Int3(200, 0, 0),
            new Int3(200, 0, 0));
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> boundsResult = await boundsTask;
        Assert.True(boundsResult.IsSuccess);
        Assert.False(boundsResult.Value.Success);
        Assert.Equal("notification.region.invalidBounds", boundsResult.Value.ErrorKey);

        RpcResult<RegionOpResponse> reachResult = await reachTask;
        Assert.True(reachResult.IsSuccess);
        Assert.False(reachResult.Value.Success);
        Assert.Equal("notification.region.outOfReach", reachResult.Value.ErrorKey);
    }

    [Fact]
    public async Task PasteRespectsOverwriteAirToggleAndDespawnsWhenClearedToEmpty()
    {
        using TestHarness harness = CreateHarness(GameMode.Creative);
        var panelId = harness.BrickRegistry.Id("wb:panel");
        harness.StructureVoxels.Set(2, 0, 0, new Voxel(panelId, new ShapeLight(BrickShape.Block, 0), 0));

        // 2x1x1 region with panel at (0,0,0) and air at (1,0,0), pasted at (1,0,0) -> (2,0,0) has air in region.
        VoxelRegionData sourceRegion = MakeRegionFromObject(new Vector3(2, 1, 1), obj =>
        {
            obj.Set(0, 0, 0, new Voxel(panelId, new ShapeLight(BrickShape.Block, 0), 0));
        }, harness.BrickRegistry, harness.PaintRegistry);

        // Skip-Air keeps existing (2,0,0).
        Task<RpcResult<RegionOpResponse>> skipTask = EnqueueSinglePaste(
            harness, PasteTargetKind.IntoStructure, harness.StructureUuid, new Int3(1, 0, 0), overwriteAir: false, sourceRegion);
        harness.System.Tick(1f / 60f, harness.Store);
        RpcResult<RegionOpResponse> skipResult = await skipTask;
        Assert.True(skipResult.Value.Success);
        Assert.Equal(panelId, harness.StructureVoxels.Get(2, 0, 0).ID);

        // Overwrite-Air clears existing (0,0,0), (1,0,0), (2,0,0) and despawns when entire structure becomes empty.
        var emptyRegion = new VoxelRegionData(3, 1, 1, [], null, null);
        Task<RpcResult<RegionOpResponse>> overwriteTask = EnqueueSinglePaste(
            harness, PasteTargetKind.IntoStructure, harness.StructureUuid, new Int3(0, 0, 0), overwriteAir: true, emptyRegion);
        harness.System.Tick(1f / 60f, harness.Store);
        RpcResult<RegionOpResponse> overwriteResult = await overwriteTask;
        Assert.True(overwriteResult.Value.Success);
        Assert.Equal(3, overwriteResult.Value.AffectedBricks);
        Assert.False(harness.Store.TryGet(Uuid.FromValue(harness.StructureUuid), out _));
    }

    [Fact]
    public async Task PasteSkipsUnknownBricksStripsBadPaintsAndSanitizesShapeOrientation()
    {
        using TestHarness harness = CreateHarness(GameMode.Creative);
        var localObj = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        // Palette index 1 = known paintable shapeable panel with valid color & incompatible texture + invalid shape
        localObj.Set(0, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Slope, 15), new Orientation(0, 1, 0).ToByte()));
        localObj.SetFaceOverride(0, 0, 0, BrickFaceMask.Top, new FaceOverride(texturePaintId: 1, colorPaintId: 2));
        // Palette index 2 = rock (not shapeable? Wait rock IS shapeable? Let's use thruster: non-shapeable Custom, non-paintable!)
        localObj.Set(1, 0, 0, new Voxel(2, new ShapeLight(BrickShape.Block, 12), new Orientation(0, 2, 0).ToByte()));
        localObj.SetFaceOverride(1, 0, 0, BrickFaceMask.Front, new FaceOverride(texturePaintId: 0, colorPaintId: 2));
        // Palette index 3 = unknown modded brick
        localObj.Set(2, 0, 0, new Voxel(3, new ShapeLight(BrickShape.Block, 0), 0));

        var wireRegion = new VoxelRegionData(
            3, 1, 1,
            localObj.GetChunkInfos(),
            _BrickPalette: [string.Empty, "wb:panel", "wb:thruster", "mod:unknown_brick"],
            _PaintPalette: [string.Empty, "wb:glass_blend", "wb:red", "mod:unknown_paint"]);

        Task<RpcResult<RegionOpResponse>> task = EnqueueSinglePaste(
            harness,
            PasteTargetKind.IntoStructure,
            harness.StructureUuid,
            new Int3(2, 0, 0),
            overwriteAir: false,
            wireRegion);
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> result = await task;
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Success);
        Assert.Equal(2, result.Value.AffectedBricks);
        Assert.Equal(1, result.Value.SkippedUnknownBricks);

        // Panel kept valid color paint (red) while incompatible blend texture paint was stripped, and light was zeroed.
        Voxel pastedPanel = harness.StructureVoxels.Get(2, 0, 0);
        Assert.Equal(harness.BrickRegistry.Id("wb:panel"), pastedPanel.ID);
        Assert.Equal(BrickShape.Slope, pastedPanel.GetShapeLight().Shape);
        Assert.Equal(0, pastedPanel.GetLightLevel());
        Assert.True(harness.StructureVoxels.TryGetFaceOverride(2, 0, 0, BrickFace.Top, out FaceOverride topOverride));
        Assert.Equal(0, topOverride.TexturePaintId);
        Assert.Equal(harness.PaintRegistry.Id("wb:red"), topOverride.ColorPaintId);

        // Thruster is fixed BrickShape.Custom even though wire payload claimed BrickShape.Block, and non-paintable strips face paint.
        Voxel pastedThruster = harness.StructureVoxels.Get(3, 0, 0);
        Assert.Equal(harness.BrickRegistry.Id("wb:thruster"), pastedThruster.ID);
        Assert.Equal(BrickShape.Custom, pastedThruster.GetShapeLight().Shape);
        Assert.False(pastedThruster.HasFaceOverrides());

        // Unknown brick was not written.
        Assert.Equal(0, harness.StructureVoxels.Get(4, 0, 0).ID);

        // If 100% of bricks are unknown on non-overwrite paste, return notification.region.empty.
        var onlyUnknownObj = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        onlyUnknownObj.Set(0, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Block, 0), 0));
        var onlyUnknownWire = new VoxelRegionData(1, 1, 1, onlyUnknownObj.GetChunkInfos(), [string.Empty, "mod:missing"], null);
        Task<RpcResult<RegionOpResponse>> emptyTask = EnqueueSinglePaste(
            harness, PasteTargetKind.IntoStructure, harness.StructureUuid, new Int3(6, 0, 0), overwriteAir: false, onlyUnknownWire);
        harness.System.Tick(1f / 60f, harness.Store);
        RpcResult<RegionOpResponse> emptyResult = await emptyTask;
        Assert.False(emptyResult.Value.Success);
        Assert.Equal("notification.region.empty", emptyResult.Value.ErrorKey);
        Assert.Equal(1, emptyResult.Value.SkippedUnknownBricks);
    }

    [Fact]
    public async Task PasteNewStructureOverMultipleFragmentsSpawnsEntityAndBroadcastsSpawn()
    {
        using TestHarness harness = CreateHarness(GameMode.Creative);
        var panelId = harness.BrickRegistry.Id("wb:panel");
        // Build a large enough region across multiple chunks (> 60 KB serialized) so FragmentAssembler spans 2+ parts.
        VoxelRegionData bigRegion = MakeRegionFromObject(new Vector3(32, 8, 8), obj =>
        {
            for (var z = 0; z < 8; z++)
            for (var y = 0; y < 8; y++)
            for (var x = 0; x < 32; x++)
            {
                obj.Set(x, y, z, new Voxel(panelId, new ShapeLight(BrickShape.Block, 0), 0));
            }
        }, harness.BrickRegistry, harness.PaintRegistry);

        var pasteRequest = new PasteRegionRequest
        {
            TargetKind = PasteTargetKind.NewStructure,
            TargetEntity = 0,
            SpawnX = 12f,
            SpawnY = 3f,
            SpawnZ = -4f,
            SpawnOrientationW = 1f,
            OverwriteAir = false,
            SizeX = bigRegion.SizeX,
            SizeY = bigRegion.SizeY,
            SizeZ = bigRegion.SizeZ,
            Chunks = bigRegion.Chunks,
            BrickPalette = bigRegion.BrickPalette,
            PaintPalette = bigRegion.PaintPalette,
        };
        byte[] payload = pasteRequest.Serialize();
        int partCount = PayloadFragmenter.PartCount(payload.Length);
        Assert.True(partCount >= 2, $"Expected multi-part upload, got {partCount} parts for {payload.Length} bytes.");

        for (var i = 0; i < partCount; i++)
        {
            Task<RpcResult<RegionOpResponse>> partTask = EnqueuePastePart(
                harness,
                new RegionUploadPart
                {
                    BatchId = 7,
                    PartIndex = (uint)i,
                    PartCount = (uint)partCount,
                    Payload = PayloadFragmenter.Slice(payload, i),
                });
            harness.System.Tick(1f / 60f, harness.Store);
            RpcResult<RegionOpResponse> response = await partTask;
            Assert.True(response.IsSuccess);
            Assert.True(response.Value.Success);
            if (i < partCount - 1)
            {
                Assert.False(response.Value.Completed);
                Assert.Empty(harness.ClientEntitySpawns);
            }
            else
            {
                Assert.True(response.Value.Completed);
                Assert.NotEqual(0UL, response.Value.SpawnedEntityUuid);
                Assert.Equal(32 * 8 * 8, response.Value.AffectedBricks);
                Assert.True(harness.Store.TryGet(Uuid.FromValue(response.Value.SpawnedEntityUuid), out int spawnedEntity));
                Assert.True(harness.Store.TryGet(spawnedEntity, out VoxelWorldComponent spawnedVoxels));
                Assert.Equal(panelId, spawnedVoxels.VoxelObject.Get(0, 0, 0).ID);
                Assert.Single(harness.ClientEntitySpawns);
                Assert.Equal(response.Value.SpawnedEntityUuid, harness.ClientEntitySpawns[0].Uuid);
            }
        }
    }

    [Fact]
    public async Task PasteRejectsPhysicsOverlap()
    {
        using TestHarness harness = CreateHarness(GameMode.Creative);
        harness.Physics.OverlapResult = true;
        VoxelRegionData region = MakeSinglePanelRegion(harness.BrickRegistry, shape: BrickShape.Block);

        Task<RpcResult<RegionOpResponse>> intoTask = EnqueueSinglePaste(
            harness, PasteTargetKind.IntoStructure, harness.StructureUuid, new Int3(2, 0, 0), overwriteAir: false, region);
        Task<RpcResult<RegionOpResponse>> newTask = EnqueueSinglePaste(
            harness, PasteTargetKind.NewStructure, targetEntity: 0, default, overwriteAir: false, region, spawnPos: new Vector3(5f, 0f, 0f));
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> intoResult = await intoTask;
        RpcResult<RegionOpResponse> newResult = await newTask;
        Assert.Equal("notification.placement.occupied", intoResult.Value.ErrorKey);
        Assert.Equal("notification.placement.occupied", newResult.Value.ErrorKey);
    }

    [Fact]
    public async Task PastePartRejectsAdventureAndUnpermittedBeforeBuffering()
    {
        using TestHarness advHarness = CreateHarness(GameMode.Adventure);
        Task<RpcResult<RegionOpResponse>> advTask = EnqueuePastePart(advHarness, new RegionUploadPart
        {
            BatchId = 1,
            PartIndex = 0,
            PartCount = 2,
            Payload = [1, 2, 3, 4],
        });
        advHarness.System.Tick(1f / 60f, advHarness.Store);

        RpcResult<RegionOpResponse> advResult = await advTask;
        Assert.True(advResult.IsSuccess);
        Assert.False(advResult.Value.Success);
        Assert.Equal("notification.region.creativeOnly", advResult.Value.ErrorKey);
        Assert.False(advHarness.Peer.RegionUploadAssembler.IsActive);

        const string denyToml = """
            [Groups.default]
            Permissions = []
            """;
        using TestHarness permHarness = CreateHarness(GameMode.Creative, isLocalHost: false, permissionsToml: denyToml);
        Task<RpcResult<RegionOpResponse>> permTask = EnqueuePastePart(permHarness, new RegionUploadPart
        {
            BatchId = 2,
            PartIndex = 0,
            PartCount = 2,
            Payload = [1, 2, 3, 4],
        });
        permHarness.System.Tick(1f / 60f, permHarness.Store);

        RpcResult<RegionOpResponse> permResult = await permTask;
        Assert.False(permResult.IsSuccess);
        Assert.Equal(GameRpcErrors.PERMISSION_DENIED, permResult.ErrorCode);
        Assert.False(permHarness.Peer.RegionUploadAssembler.IsActive);
    }

    [Fact]
    public async Task PastePartRejectsOver4MiBPayloadDuringAssembly()
    {
        using TestHarness harness = CreateHarness(GameMode.Creative);
        int maxAllowedParts = PayloadFragmenter.PartCount(RegionLimits.MAX_PAYLOAD_BYTES);

        Task<RpcResult<RegionOpResponse>> tooManyPartsTask = EnqueuePastePart(harness, new RegionUploadPart
        {
            BatchId = 10,
            PartIndex = 0,
            PartCount = (uint)(maxAllowedParts + 1),
            Payload = [1],
        });
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> tooManyResult = await tooManyPartsTask;
        Assert.True(tooManyResult.IsSuccess);
        Assert.False(tooManyResult.Value.Success);
        Assert.Equal("notification.region.invalidBounds", tooManyResult.Value.ErrorKey);
        Assert.False(harness.Peer.RegionUploadAssembler.IsActive);

        var fullChunk = new byte[PayloadFragmenter.FRAGMENT_BUDGET];
        for (var i = 0; i < maxAllowedParts - 1; i++)
        {
            Task<RpcResult<RegionOpResponse>> midTask = EnqueuePastePart(harness, new RegionUploadPart
            {
                BatchId = 11,
                PartIndex = (uint)i,
                PartCount = (uint)maxAllowedParts,
                Payload = fullChunk,
            });
            harness.System.Tick(1f / 60f, harness.Store);
            RpcResult<RegionOpResponse> midResult = await midTask;
            Assert.True(midResult.IsSuccess);
            Assert.True(midResult.Value.Success);
            Assert.False(midResult.Value.Completed);
            Assert.True(harness.Peer.RegionUploadAssembler.IsActive);
        }

        Task<RpcResult<RegionOpResponse>> finalOverCapTask = EnqueuePastePart(harness, new RegionUploadPart
        {
            BatchId = 11,
            PartIndex = (uint)(maxAllowedParts - 1),
            PartCount = (uint)maxAllowedParts,
            Payload = fullChunk,
        });
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> overCapResult = await finalOverCapTask;
        Assert.True(overCapResult.IsSuccess);
        Assert.False(overCapResult.Value.Success);
        Assert.Equal("notification.region.invalidBounds", overCapResult.Value.ErrorKey);
        Assert.False(harness.Peer.RegionUploadAssembler.IsActive);
    }

    [Fact]
    public async Task MissingGameModeComponentFailsClosed()
    {
        using TestHarness harness = CreateHarness(GameMode.Creative);
        int playerEntity = -1;
        Assert.True(harness.Store.TryGet(harness.Peer.GameId, out _) || harness.Peer.GameId != Uuid.Null);
        harness.Store.Query<GameModeComponent>(0f, (float _, DataStore _, int e, in GameModeComponent _) => playerEntity = e);
        Assert.True(playerEntity >= 0);
        harness.Store.Remove(typeof(GameModeComponent), playerEntity);

        Task<RpcResult<RegionOpResponse>> cutTask = EnqueueCut(harness, harness.StructureUuid, new Int3(0, 0, 0), new Int3(0, 0, 0));
        VoxelRegionData region = MakeSinglePanelRegion(harness.BrickRegistry, BrickShape.Block);
        Task<RpcResult<RegionOpResponse>> pasteTask = EnqueueSinglePaste(
            harness, PasteTargetKind.IntoStructure, harness.StructureUuid, new Int3(2, 0, 0), overwriteAir: false, region);
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> cutResult = await cutTask;
        RpcResult<RegionOpResponse> pasteResult = await pasteTask;
        Assert.False(cutResult.Value.Success);
        Assert.Equal("notification.region.creativeOnly", cutResult.Value.ErrorKey);
        Assert.False(pasteResult.Value.Success);
        Assert.Equal("notification.region.creativeOnly", pasteResult.Value.ErrorKey);
    }

    [Fact]
    public async Task CutAndPasteRejectOverflowAndOutOfChunkRangeCoordinates()
    {
        using TestHarness harness = CreateHarness(GameMode.Creative);
        var panelId = harness.BrickRegistry.Id("wb:panel");
        VoxelRegionData twoWide = MakeRegionFromObject(new Vector3(2, 1, 1), obj =>
        {
            obj.Set(0, 0, 0, new Voxel(panelId, new ShapeLight(BrickShape.Block, 0), 0));
        }, harness.BrickRegistry, null);

        Task<RpcResult<RegionOpResponse>> overflowPaste = EnqueueSinglePaste(
            harness, PasteTargetKind.IntoStructure, harness.StructureUuid, new Int3(int.MaxValue, 0, 0), overwriteAir: false, twoWide);
        Task<RpcResult<RegionOpResponse>> outOfChunkPaste = EnqueueSinglePaste(
            harness, PasteTargetKind.IntoStructure, harness.StructureUuid, new Int3(1_000_000, 0, 0), overwriteAir: false, twoWide);
        Task<RpcResult<RegionOpResponse>> outOfChunkCut = EnqueueCut(
            harness, harness.StructureUuid, new Int3(1_000_000, 0, 0), new Int3(1_000_000, 0, 0));
        harness.System.Tick(1f / 60f, harness.Store);

        Assert.Equal("notification.region.invalidBounds", (await overflowPaste).Value.ErrorKey);
        Assert.Equal("notification.region.invalidBounds", (await outOfChunkPaste).Value.ErrorKey);
        Assert.Equal("notification.region.invalidBounds", (await outOfChunkCut).Value.ErrorKey);
    }

    [Fact]
    public async Task PasteWithoutBrickPaletteSkipsRawIdsInsteadOfAcceptingThem()
    {
        using TestHarness harness = CreateHarness(GameMode.Creative);
        var obj = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        obj.Set(0, 0, 0, new Voxel(1, new ShapeLight(BrickShape.Block, 0), 0));
        var noPaletteRegion = new VoxelRegionData(1, 1, 1, obj.GetChunkInfos(), _BrickPalette: null, _PaintPalette: null);

        Task<RpcResult<RegionOpResponse>> task = EnqueueSinglePaste(
            harness, PasteTargetKind.IntoStructure, harness.StructureUuid, new Int3(2, 0, 0), overwriteAir: true, noPaletteRegion);
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> result = await task;
        Assert.True(result.IsSuccess);
        Assert.True(result.Value.Success);
        Assert.Equal(1, result.Value.SkippedUnknownBricks);
        Assert.Equal(0, harness.StructureVoxels.Get(2, 0, 0).ID);
    }

    [Fact]
    public void IsBoxWithinReachAccountsForNonUnitStructureScale()
    {
        var scaledTransform = new TransformComponent(Vector3.Zero, Quaternion.Identity, new Vector3(2f, 2f, 2f));
        var cell = new Int3(10, 0, 0);

        // At scale 2, voxel cell 10 spans [19..21] in world X.
        Assert.True(RegionPlacementResolver.IsBoxWithinReach(cell, cell, new Vector3(25f, 0f, 0f), in scaledTransform, 5f));
        Assert.False(RegionPlacementResolver.IsBoxWithinReach(cell, cell, new Vector3(10f, 0f, 0f), in scaledTransform, 5f));
    }

    [Fact]
    public async Task ConfigurableRegionCapsEnforceServerLimitsAndSupportZeroUnlimited()
    {
        var strictConfig = new GameplayConfig();
        strictConfig.Regions.MaxEdge.Set(4);
        strictConfig.Regions.MaxVolume.Set(16);

        using TestHarness strictHarness = CreateHarness(GameMode.Creative, gameplayConfig: strictConfig);
        Task<RpcResult<RegionOpResponse>> rejectedCut = EnqueueCut(strictHarness, strictHarness.StructureUuid, new Int3(0, 0, 0), new Int3(4, 0, 0));
        strictHarness.System.Tick(1f / 60f, strictHarness.Store);
        RpcResult<RegionOpResponse> strictResult = await rejectedCut;
        Assert.True(strictResult.IsSuccess);
        Assert.False(strictResult.Value.Success);
        Assert.Equal("notification.region.invalidBounds", strictResult.Value.ErrorKey);

        var unlimitedConfig = new GameplayConfig();
        unlimitedConfig.Regions.MaxEdge.Set(0);
        unlimitedConfig.Regions.MaxVolume.Set(0);
        unlimitedConfig.Regions.MaxPayloadBytes.Set(0);

        using TestHarness unlimitedHarness = CreateHarness(GameMode.Creative, gameplayConfig: unlimitedConfig);
        Task<RpcResult<RegionOpResponse>> allowedCut = EnqueueCut(unlimitedHarness, unlimitedHarness.StructureUuid, new Int3(0, 0, 0), new Int3(80, 0, 0));
        unlimitedHarness.System.Tick(1f / 60f, unlimitedHarness.Store);
        RpcResult<RegionOpResponse> unlimitedResult = await allowedCut;
        Assert.True(unlimitedResult.IsSuccess);
        Assert.True(unlimitedResult.Value.Success);
    }

    private static Task<RpcResult<RegionOpResponse>> EnqueueCut(
        TestHarness harness,
        ulong targetEntity,
        Int3 min,
        Int3 max
    )
    {
        var tcs = new TaskCompletionSource<RpcResult<RegionOpResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Peer.CutRegionCalls.Enqueue(new ServerPeer.CutRegionCall(
            new CutRegionRequest
            {
                TargetEntity = targetEntity,
                MinX = min.X,
                MinY = min.Y,
                MinZ = min.Z,
                MaxX = max.X,
                MaxY = max.Y,
                MaxZ = max.Z,
            },
            tcs,
            CancellationToken.None));
        return tcs.Task;
    }

    private static Task<RpcResult<RegionOpResponse>> EnqueueSinglePaste(
        TestHarness harness,
        PasteTargetKind kind,
        ulong targetEntity,
        Int3 origin,
        bool overwriteAir,
        VoxelRegionData region,
        Vector3? spawnPos = null
    )
    {
        Vector3 spawn = spawnPos ?? Vector3.Zero;
        var request = new PasteRegionRequest
        {
            TargetKind = kind,
            TargetEntity = targetEntity,
            OriginX = origin.X,
            OriginY = origin.Y,
            OriginZ = origin.Z,
            SpawnX = spawn.X,
            SpawnY = spawn.Y,
            SpawnZ = spawn.Z,
            SpawnOrientationW = 1f,
            OverwriteAir = overwriteAir,
            SizeX = region.SizeX,
            SizeY = region.SizeY,
            SizeZ = region.SizeZ,
            Chunks = region.Chunks,
            BrickPalette = region.BrickPalette,
            PaintPalette = region.PaintPalette,
        };
        byte[] payload = request.Serialize();
        return EnqueuePastePart(harness, new RegionUploadPart
        {
            BatchId = 1,
            PartIndex = 0,
            PartCount = 1,
            Payload = payload,
        });
    }

    [Fact]
    public async Task BlueprintItemPlacementWorksInAdventureAndCreativeModesAndConsumesOnlyInAdventure()
    {
        //  Adventure mode: grid-attached placement succeeds, broadcasts edit, and consumes 1 item.
        using (TestHarness adventure = CreateHarness(GameMode.Adventure))
        {
            adventure.SetHeldItem("wb:core", count: 3);

            Task<RpcResult<RegionOpResponse>> attachTask = EnqueuePlaceBlueprintItem(
                adventure,
                PasteTargetKind.IntoStructure,
                adventure.StructureUuid,
                origin: new Int3(1, 0, 0),
                spawnPos: Vector3.Zero);
            adventure.System.Tick(1f / 60f, adventure.Store);

            RpcResult<RegionOpResponse> attachResult = await attachTask;
            Assert.True(attachResult.IsSuccess);
            Assert.True(attachResult.Value.Success, attachResult.Value.ErrorKey);
            Assert.Equal(1, attachResult.Value.AffectedBricks);
            Assert.Equal(adventure.BrickRegistry.Id("wb:core"), adventure.StructureVoxels.Get(1, 0, 0).ID);
            Assert.Single(adventure.ClientRegionEdits);
            Assert.False(adventure.ClientRegionEdits[0].OverwriteAir);
            Assert.Equal(2, adventure.GetHeldItemCount());

            //  Adventure mode: free-floating placement spawns new structure and consumes 1 more item.
            Task<RpcResult<RegionOpResponse>> spawnTask = EnqueuePlaceBlueprintItem(
                adventure,
                PasteTargetKind.NewStructure,
                targetEntity: 0,
                origin: default,
                spawnPos: new Vector3(5f, 0f, 0f));
            adventure.System.Tick(1f / 60f, adventure.Store);

            RpcResult<RegionOpResponse> spawnResult = await spawnTask;
            Assert.True(spawnResult.IsSuccess);
            Assert.True(spawnResult.Value.Success, spawnResult.Value.ErrorKey);
            Assert.NotEqual(0UL, spawnResult.Value.SpawnedEntityUuid);
            Assert.Equal(1, spawnResult.Value.AffectedBricks);
            Assert.Single(adventure.ClientEntitySpawns);
            Assert.Equal(1, adventure.GetHeldItemCount());

            //  Overlapping an existing brick on target structure is rejected without consuming item.
            Task<RpcResult<RegionOpResponse>> overlapTask = EnqueuePlaceBlueprintItem(
                adventure,
                PasteTargetKind.IntoStructure,
                adventure.StructureUuid,
                origin: new Int3(0, 0, 0),
                spawnPos: Vector3.Zero);
            adventure.System.Tick(1f / 60f, adventure.Store);

            RpcResult<RegionOpResponse> overlapResult = await overlapTask;
            Assert.True(overlapResult.IsSuccess);
            Assert.False(overlapResult.Value.Success);
            Assert.Equal("notification.placement.occupied", overlapResult.Value.ErrorKey);
            Assert.Equal(1, adventure.GetHeldItemCount());

            //  World collider overlap is rejected without consuming item.
            adventure.Physics.OverlapResult = true;
            Task<RpcResult<RegionOpResponse>> colliderTask = EnqueuePlaceBlueprintItem(
                adventure,
                PasteTargetKind.NewStructure,
                targetEntity: 0,
                origin: default,
                spawnPos: new Vector3(8f, 0f, 0f));
            adventure.System.Tick(1f / 60f, adventure.Store);

            RpcResult<RegionOpResponse> colliderResult = await colliderTask;
            Assert.True(colliderResult.IsSuccess);
            Assert.False(colliderResult.Value.Success);
            Assert.Equal("notification.placement.occupied", colliderResult.Value.ErrorKey);
            Assert.Equal(1, adventure.GetHeldItemCount());
            adventure.Physics.OverlapResult = false;

            //  Out of reach is rejected without consuming item.
            Task<RpcResult<RegionOpResponse>> farTask = EnqueuePlaceBlueprintItem(
                adventure,
                PasteTargetKind.NewStructure,
                targetEntity: 0,
                origin: default,
                spawnPos: new Vector3(500f, 0f, 0f));
            adventure.System.Tick(1f / 60f, adventure.Store);

            RpcResult<RegionOpResponse> farResult = await farTask;
            Assert.True(farResult.IsSuccess);
            Assert.False(farResult.Value.Success);
            Assert.Equal("notification.region.outOfReach", farResult.Value.ErrorKey);
            Assert.Equal(1, adventure.GetHeldItemCount());
        }

        //  Creative mode: placement succeeds and does NOT consume the item stack.
        using (TestHarness creative = CreateHarness(GameMode.Creative))
        {
            creative.SetHeldItem("wb:core", count: 5);

            Task<RpcResult<RegionOpResponse>> task = EnqueuePlaceBlueprintItem(
                creative,
                PasteTargetKind.NewStructure,
                targetEntity: 0,
                origin: default,
                spawnPos: new Vector3(6f, 0f, 0f));
            creative.System.Tick(1f / 60f, creative.Store);

            RpcResult<RegionOpResponse> result = await task;
            Assert.True(result.IsSuccess);
            Assert.True(result.Value.Success, result.Value.ErrorKey);
            Assert.Equal(5, creative.GetHeldItemCount());
        }
    }

    [Fact]
    public async Task BlueprintItemPlacementRejectsNonBlueprintHeldItemAndAppliesTransforms()
    {
        using TestHarness harness = CreateHarness(GameMode.Adventure);
        harness.SetHeldItem("wb:panel", count: 5);

        Task<RpcResult<RegionOpResponse>> badItemTask = EnqueuePlaceBlueprintItem(
            harness,
            PasteTargetKind.NewStructure,
            targetEntity: 0,
            origin: default,
            spawnPos: new Vector3(4f, 0f, 0f));
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> badItemResult = await badItemTask;
        Assert.True(badItemResult.IsSuccess);
        Assert.False(badItemResult.Value.Success);
        Assert.Equal(5, harness.GetHeldItemCount());
        Assert.Empty(harness.ClientEntitySpawns);

        //  Multi-brick blueprint with yaw rotation rotates dimensions from 2x1x1 to 1x1x2.
        harness.SetHeldItem("wb:two_panel", count: 2);
        var yawRotation = new Orientation(0, 1, 0);
        Task<RpcResult<RegionOpResponse>> rotTask = EnqueuePlaceBlueprintItem(
            harness,
            PasteTargetKind.IntoStructure,
            harness.StructureUuid,
            origin: new Int3(3, 0, 0),
            spawnPos: Vector3.Zero,
            rotation: yawRotation.ToByte());
        harness.System.Tick(1f / 60f, harness.Store);

        RpcResult<RegionOpResponse> rotResult = await rotTask;
        Assert.True(rotResult.IsSuccess);
        Assert.True(rotResult.Value.Success, rotResult.Value.ErrorKey);
        Assert.Equal(2, rotResult.Value.AffectedBricks);
        Assert.Equal(harness.BrickRegistry.Id("wb:panel"), harness.StructureVoxels.Get(3, 0, 0).ID);
        Assert.Equal(harness.BrickRegistry.Id("wb:panel"), harness.StructureVoxels.Get(3, 0, 1).ID);
        Assert.Equal(1, harness.GetHeldItemCount());
    }

    private static Task<RpcResult<RegionOpResponse>> EnqueuePlaceBlueprintItem(
        TestHarness harness,
        PasteTargetKind kind,
        ulong targetEntity,
        Int3 origin,
        Vector3 spawnPos,
        byte rotation = 0,
        byte mirror = 0
    )
    {
        var tcs = new TaskCompletionSource<RpcResult<RegionOpResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Peer.PlaceBlueprintItemCalls.Enqueue(new ServerPeer.PlaceBlueprintItemCall(
            new PlaceBlueprintItemRequest
            {
                TargetKind = kind,
                TargetEntity = targetEntity,
                OriginX = origin.X,
                OriginY = origin.Y,
                OriginZ = origin.Z,
                SpawnX = spawnPos.X,
                SpawnY = spawnPos.Y,
                SpawnZ = spawnPos.Z,
                SpawnOrientationW = 1f,
                Rotation = rotation,
                Mirror = mirror,
            },
            tcs,
            CancellationToken.None));
        return tcs.Task;
    }

    private static Task<RpcResult<RegionOpResponse>> EnqueuePastePart(TestHarness harness, RegionUploadPart part)
    {
        var tcs = new TaskCompletionSource<RpcResult<RegionOpResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
        harness.Peer.PasteRegionParts.Enqueue(new ServerPeer.PasteRegionPartCall(part, tcs, CancellationToken.None));
        return tcs.Task;
    }

    private static VoxelRegionData MakeSinglePanelRegion(StubBrickRegistry brickRegistry, BrickShape shape)
    {
        return MakeRegionFromObject(new Vector3(1, 1, 1), obj =>
        {
            obj.Set(0, 0, 0, new Voxel(brickRegistry.Id("wb:panel"), new ShapeLight(shape, 0), 0));
        }, brickRegistry, null);
    }

    private static VoxelRegionData MakeRegionFromObject(
        Vector3 size,
        Action<VoxelObject> populate,
        IBrickRegistry brickRegistry,
        IPaintRegistry? paintRegistry
    )
    {
        var obj = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        populate(obj);
        var raw = new VoxelRegionData((int)size.X, (int)size.Y, (int)size.Z, obj.GetChunkInfos(), null, null);
        return VoxelCodec.Encode(in raw, brickRegistry, paintRegistry);
    }

    private static TestHarness CreateHarness(
        GameMode mode,
        bool isLocalHost = true,
        string? permissionsToml = null,
        GameplayConfig? gameplayConfig = null
    )
    {
        var peers = new ServerPeers();
        var sessions = new SessionManager();
        var registry = new NetworkRegistry();
        var physics = new StubServerPhysics();
        var replication = new NetworkReplicationSystem(
            peers, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingConfig(), registry);

        var brickDb = new StubBrickDatabase();
        var brickRegistry = new StubBrickRegistry();
        var panel = new Brick("wb:panel", BrickAlpha.Opaque, false, null, BrickShape.Any, default, paintable: true);
        var thruster = new Brick("wb:thruster", BrickAlpha.Opaque, false, "thruster.obj", BrickShape.Custom, default, orientable: true, paintable: false);
        var core = new Brick("wb:core", BrickAlpha.Opaque, false, null, BrickShape.Block, default, paintable: false);
        brickRegistry.Register(panel, 1);
        brickDb.Add(1, panel);
        brickRegistry.Register(thruster, 2);
        brickDb.Add(2, thruster);
        brickRegistry.Register(core, 3);
        brickDb.Add(3, core);

        var paintDb = new StubPaintDatabase();
        paintDb.Add(1, Paint.CreateTexture("wb:glass_blend", "glass", BrickAlpha.Blend, false, ["glass"]));
        paintDb.Add(2, Paint.CreateColor("wb:red", "red", new Vector3(1f, 0f, 0f)));

        string permText = permissionsToml ?? """
            [Groups.default]
            Permissions = ["waywardbeyond.region.*"]
            """;
        var policy = PermissionPolicy.Create([new SourcedPermissionFile("test.toml", Toml.To<PermissionFile>(permText))]);
        var permService = new UserPermissionService(new PermissionResolver(policy));

        var connection = new CapturingClientConnection(clientId: 1);
        var ecsWorld = new World();
        var simStep = new SimulationStep(ecsWorld.DataStore, physics);
        var handlerRegistry = new InteractionHandlerRegistry([]);
        var saveService = new LevelSaveService(
            NullLogger<LevelSaveService>.Instance,
            new StubLevelCatalog(),
            brickRegistry,
            brickDb,
            new GameplayConfig(),
            paintDb);
        var joinQueue = new ServerJoinQueue();
        Func<DataStore, IVoxelInteractionWorld> worldFactory = s => new ServerVoxelInteractionWorld(s, physics);
        var interactionSystem = new ServerInteractionSystem(
            sessions, NullLogger<ServerInteractionSystem>.Instance, handlerRegistry, worldFactory, simStep);

        var worldContainer = new Container();
        worldContainer.RegisterInstance(ecsWorld);
        worldContainer.RegisterInstance(sessions);
        worldContainer.RegisterInstance(saveService);
        worldContainer.RegisterInstance(joinQueue);
        worldContainer.RegisterInstance(replication);
        worldContainer.RegisterInstance<IInteractionHandlerRegistry>(handlerRegistry);
        worldContainer.RegisterInstance<IUserPermissionService>(permService);
        worldContainer.RegisterInstance<IServerWorldPhysics>(physics);
        worldContainer.RegisterInstance(simStep);
        worldContainer.RegisterInstance(worldFactory);
        worldContainer.RegisterInstance(interactionSystem);
        worldContainer.RegisterInstance(new WaywardBeyond.Server.Particles.ServerParticleService(
            peers,
            sessions,
            replication,
            NullLogger<WaywardBeyond.Server.Particles.ServerParticleService>.Instance));
        var world = new ServerWorld(worldContainer);

        ServerPeer peer = peers.GetOrAdd(new StubHost(), new RpcPeer(connection), isLocal: isLocalHost);
        peer.World = world;
        permService.Bind(peer.GameId, new UserClaim("player"), isHost: isLocalHost);

        int playerEntity = ecsWorld.DataStore.Alloc();
        ecsWorld.DataStore.AddOrUpdate(playerEntity, new NetworkComponent());
        ecsWorld.DataStore.AddOrUpdate(playerEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        ecsWorld.DataStore.AddOrUpdate(playerEntity, new GameModeComponent(mode));
        ecsWorld.DataStore.AddOrUpdate(playerEntity, new InventoryComponent(36));
        ecsWorld.DataStore.AddOrUpdate(playerEntity, new EquipmentComponent { ActiveInventorySlot = 0 });
        sessions.Register(ecsWorld.DataStore, playerEntity, peer.GameId, new Session(1));

        var itemDb = new StubItemDatabase();
        itemDb.Add(new Item("wb:panel", "Panel", null, 100, new PlaceableDefinition { Type = PlaceableType.Brick, ID = "wb:panel" }, null, null, null));
        itemDb.Add(new Item("wb:core", "Core", null, 100, new PlaceableDefinition { Type = PlaceableType.Blueprint, ID = "builtin/core" }, null, null, null));
        itemDb.Add(new Item("wb:two_panel", "Two Panel", null, 100, new PlaceableDefinition { Type = PlaceableType.Blueprint, ID = "test/two_panel" }, null, null, null));

        var bundledBlueprints = new StubBundledBlueprintDatabase();
        VoxelRegionData coreRegion = MakeRegionFromObject(new Vector3(1, 1, 1), obj =>
        {
            obj.Set(0, 0, 0, new Voxel(core.DataID, new ShapeLight(BrickShape.Block, 0), 0));
        }, brickRegistry, null);
        bundledBlueprints.Add("builtin/core", new BlueprintData(
            new WaywardBeyond.Data.Version(DataVersion.CURRENT, "Wayward Beyond", "Blueprint"),
            "Core", 0, 0, 1, coreRegion.SizeX, coreRegion.SizeY, coreRegion.SizeZ,
            coreRegion.Chunks, coreRegion.BrickPalette, coreRegion.PaintPalette));

        VoxelRegionData twoPanelRegion = MakeRegionFromObject(new Vector3(2, 1, 1), obj =>
        {
            obj.Set(0, 0, 0, new Voxel(panel.DataID, new ShapeLight(BrickShape.Block, 0), 0));
            obj.Set(1, 0, 0, new Voxel(panel.DataID, new ShapeLight(BrickShape.Block, 0), 0));
        }, brickRegistry, null);
        bundledBlueprints.Add("test/two_panel", new BlueprintData(
            new WaywardBeyond.Data.Version(DataVersion.CURRENT, "Wayward Beyond", "Blueprint"),
            "Two Panel", 0, 0, 2, twoPanelRegion.SizeX, twoPanelRegion.SizeY, twoPanelRegion.SizeZ,
            twoPanelRegion.Chunks, twoPanelRegion.BrickPalette, twoPanelRegion.PaintPalette));

        var structureVoxels = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        structureVoxels.Set(0, 0, 0, new Voxel(panel.DataID, new ShapeLight(BrickShape.Block, 0), 0));
        int structureEntity = ecsWorld.DataStore.Alloc();
        ulong structureUuid = ecsWorld.DataStore.GetUuid(structureEntity).ToValue();
        ecsWorld.DataStore.AddOrUpdate(structureEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        var voxelWorld = new VoxelWorldComponent(structureVoxels);
        ecsWorld.DataStore.AddOrUpdate(structureEntity, new ColliderComponent(VoxelColliderBuilder.BuildCollision(in voxelWorld, brickDb)));
        ecsWorld.DataStore.AddOrUpdate(structureEntity, voxelWorld);

        var system = new ServerRegionSystem(
            peers,
            sessions,
            permService,
            physics,
            replication,
            brickRegistry,
            brickDb,
            NullLogger<ServerRegionSystem>.Instance,
            paintDb,
            paintDb,
            gameplayConfig,
            itemDb,
            bundledBlueprints);

        return new TestHarness(ecsWorld.DataStore, peer, world, system, connection, structureUuid, structureVoxels, brickRegistry, paintDb, physics, playerEntity);
    }

    private sealed class TestHarness(
        DataStore store,
        ServerPeer peer,
        ServerWorld world,
        ServerRegionSystem system,
        CapturingClientConnection connection,
        ulong structureUuid,
        VoxelObject structureVoxels,
        StubBrickRegistry brickRegistry,
        StubPaintDatabase paintRegistry,
        StubServerPhysics physics,
        int playerEntity
    ) : IDisposable
    {
        private readonly FragmentAssembler _editAssembler = new();
        private readonly FragmentAssembler _spawnAssembler = new();
        public DataStore Store { get; } = store;
        public ServerPeer Peer { get; } = peer;
        public ServerWorld World { get; } = world;
        public ServerRegionSystem System { get; } = system;
        public ulong StructureUuid { get; } = structureUuid;
        public VoxelObject StructureVoxels { get; } = structureVoxels;
        public StubBrickRegistry BrickRegistry { get; } = brickRegistry;
        public StubPaintDatabase PaintRegistry { get; } = paintRegistry;
        public StubServerPhysics Physics { get; } = physics;
        public List<VoxelRegionEditMessage> ClientRegionEdits => DecodeEdits();
        public List<VoxelEntityData> ClientEntitySpawns => DecodeSpawns();

        public void SetHeldItem(string itemId, int count, int slot = 0)
        {
            var inv = new InventoryComponent(36);
            inv.Contents[slot] = new ItemData(itemId, count, 100);
            Store.AddOrUpdate(playerEntity, inv);
            Store.AddOrUpdate(playerEntity, new EquipmentComponent { ActiveInventorySlot = slot });
        }

        public int GetHeldItemCount(int slot = 0)
        {
            return Store.TryGet(playerEntity, out InventoryComponent inv) && inv.Contents != null && slot < inv.Contents.Length
                ? inv.Contents[slot].Count
                : 0;
        }

        private readonly List<VoxelRegionEditMessage> _decodedEdits = [];
        private readonly List<VoxelEntityData> _decodedSpawns = [];

        private List<VoxelRegionEditMessage> DecodeEdits()
        {
            while (connection.EditParts.TryDequeue(out VoxelRegionEditPart part))
            {
                if (_editAssembler.Append(part.BatchId, (int)part.PartIndex, (int)part.PartCount, part.Payload, out byte[] payload) == FragmentState.Complete)
                {
                    _decodedEdits.Add(VoxelRegionEditMessage.Deserialize(payload));
                }
            }

            return _decodedEdits;
        }

        private List<VoxelEntityData> DecodeSpawns()
        {
            while (connection.SpawnParts.TryDequeue(out VoxelEntitySpawnPart part))
            {
                if (_spawnAssembler.Append(part.BatchId, (int)part.PartIndex, (int)part.PartCount, part.Payload, out byte[] payload) == FragmentState.Complete)
                {
                    _decodedSpawns.Add(VoxelEntityData.Deserialize(payload));
                }
            }

            return _decodedSpawns;
        }

        public void Dispose()
        {
            World.Dispose();
        }
    }

    private sealed class CapturingClientConnection(ulong clientId) : RpcServerConnection
    {
        public readonly Queue<VoxelRegionEditPart> EditParts = new();
        public readonly Queue<VoxelEntitySpawnPart> SpawnParts = new();
        public ulong ClientId { get; } = clientId;
        public EndPoint? RemoteEndPoint => null;
        public bool IsConnected => true;
        public void Close(RpcDisconnectReason reason = RpcDisconnectReason.LocalClose) { }
        public event Action<RpcDisconnectReason>? Disconnected { add { } remove { } }
        public event RpcFrameHandler? FrameReceived { add { } remove { } }

        public bool TrySend(ReadOnlySpan<byte> frame)
        {
            Envelope env = Envelope.Deserialize(frame.Slice(4));
            if (env.Method == GameRpc.Methods.VOXEL_REGION_EDIT && env.Payload != null)
            {
                EditParts.Enqueue(VoxelRegionEditPart.Deserialize(env.Payload));
            }
            else if (env.Method == GameRpc.Methods.VOXEL_ENTITY_SPAWN && env.Payload != null)
            {
                SpawnParts.Enqueue(VoxelEntitySpawnPart.Deserialize(env.Payload));
            }

            return true;
        }
    }

    private sealed class StubHost : IRpcHost
    {
        public PeerSet Peers => default;
    }

    private sealed class StubServerPhysics : IServerWorldPhysics
    {
        public bool OverlapResult;
        public float FixedTimestamp => 1f / 64f;
        public event EventHandler<EventArgs>? FixedUpdate { add { } remove { } }
        public RaycastResult Raycast(in Ray ray) => default;
        public bool Overlaps(in ShapeOverlapQuery query) => OverlapResult;
        public void SetGravity(Vector3 gravity) { }
        public void Tick(float delta, DataStore store) { }
    }

    private sealed class StubBrickRegistry : IBrickRegistry
    {
        private readonly Dictionary<string, ushort> _toId = new(StringComparer.Ordinal);
        private readonly Dictionary<ushort, string> _toName = [];

        public void Register(Brick brick, ushort id)
        {
            brick.BindDataID(id);
            _toId[brick.ID] = id;
            _toName[id] = brick.ID;
        }

        public ushort Id(string name) => _toId.TryGetValue(name, out ushort id) ? id : (ushort)0;
        public string? Name(ushort id) => _toName.TryGetValue(id, out string? name) ? name : null;
    }

    private sealed class StubBrickDatabase : IBrickDatabase
    {
        private readonly Dictionary<ushort, Brick> _byId = [];
        public void Add(ushort id, Brick brick) => _byId[id] = brick;
        public Result<Brick> Get(string id) => Result<Brick>.FromFailure((string?)null);
        public Result<Brick> Get(ushort id) => _byId.TryGetValue(id, out Brick? b) ? Result<Brick>.FromSuccess(b) : Result<Brick>.FromFailure((string?)null);
        public List<Brick> Get(Func<Brick, bool> predicate) => [.. _byId.Values.Where(predicate)];
        public bool IsCuller(in Voxel voxel, BrickShape shape) => false;
    }

    private sealed class StubPaintDatabase : IPaintDatabase, IPaintRegistry
    {
        private readonly Dictionary<ushort, Paint> _byId = [];
        private readonly Dictionary<string, ushort> _toId = new(StringComparer.Ordinal);

        public void Add(ushort id, Paint paint)
        {
            paint.BindDataID(id);
            _byId[id] = paint;
            _toId[paint.ID] = id;
        }

        public IReadOnlyList<Paint> TexturePaints => [.. _byId.Values.Where(p => p.Kind == PaintKind.Texture)];
        public IReadOnlyList<Paint> ColorPaints => [.. _byId.Values.Where(p => p.Kind == PaintKind.Color)];
        public ushort Id(string name) => _toId.TryGetValue(name, out ushort id) ? id : (ushort)0;
        public string? Name(ushort id) => _byId.TryGetValue(id, out Paint? p) ? p.ID : null;
        public Result<Paint> Get(string id) => _toId.TryGetValue(id, out ushort idNum) ? Result<Paint>.FromSuccess(_byId[idNum]) : Result<Paint>.FromFailure((string?)null);
        public Result<Paint> Get(ushort id) => _byId.TryGetValue(id, out Paint? p) ? Result<Paint>.FromSuccess(p) : Result<Paint>.FromFailure((string?)null);
        public List<Paint> Get(Func<Paint, bool> predicate) => [.. _byId.Values.Where(predicate)];
    }

    private sealed class InlineTomlParser : IFileParseService
    {
        public TResult Parse<TResult>(PathInfo path) => (TResult)(object)Toml.To<PermissionFile>(path.ReadString());
        public TResult Parse<TResult>(Stream stream) => throw new NotSupportedException();
        public bool TryParse<TResult>(PathInfo path, out TResult result) => throw new NotSupportedException();
        public bool TryParse<TResult>(Stream stream, out TResult result) => throw new NotSupportedException();
    }

    private sealed class StubLevelCatalog : WaywardBeyond.Server.Saves.ILevelCatalog
    {
        public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
        {
            levelGuid = Guid.NewGuid().ToString();
            return true;
        }

        public WaywardBeyond.Data.Level[] ListLevels() => [];
        public bool Delete(string levelGuid) => false;
        public bool Exists(string levelGuid) => false;
        public WaywardBeyond.Data.Levels.ILevelStorage? Open(string levelGuid) => null;
    }

    private sealed class StubItemDatabase : IItemDatabase
    {
        private readonly Dictionary<string, Item> _items = new(StringComparer.Ordinal);
        public void Add(Item item) => _items[item.ID] = item;
        public Result<Item> Get(string id) =>
            _items.TryGetValue(id, out Item? item)
                ? Result<Item>.FromSuccess(item)
                : Result<Item>.FromFailure((string?)null);
    }

    private sealed class StubBundledBlueprintDatabase : IBundledBlueprintDatabase
    {
        private readonly Dictionary<string, BlueprintData> _blueprints = new(StringComparer.Ordinal);
        public void Add(string id, BlueprintData data) => _blueprints[id] = data;
        public IReadOnlyList<BlueprintSummary> ListSummaries() => [];
        public Result<BlueprintData> GetBlueprint(string id) =>
            _blueprints.TryGetValue(id, out BlueprintData data)
                ? Result<BlueprintData>.FromSuccess(data)
                : Result<BlueprintData>.FromFailure((string?)null);
    }
}
