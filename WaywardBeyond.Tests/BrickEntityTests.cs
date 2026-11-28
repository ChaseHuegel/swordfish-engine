#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using DryIoc;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using NsdRpc;
using Swordfish.ECS;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Tests;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.Voxels.Building;
using WaywardBeyond.Client.Voxels.Models;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Data.Migrations;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Generation;
using WaywardBeyond.Server.Interactions;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Voxels;
using Xunit;
using GameState = WaywardBeyond.Client.GameState;
using WaywardBeyondClient = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>
/// Pins brick functional brick-entity TOML metadata, server ECS brick-entity lifecycle, blueprint spawn +
/// cargo seeding, viewer-scoped replication isolation, and <c>level.db</c> save/load round-trips.
/// </summary>
[Collection("ClientGameState")]
public sealed class BrickEntityTests
{
    [Fact]
    public void BrickDatabaseLoadsBrickEntityMetadataTagsAndStatisticsAndSkipsInvalidEntries()
    {
        BrickDatabase shipped = LoadShippedBricks();

        Brick storage = shipped.Get("wb:storage").Value;
        Assert.True(storage.HasEntity);
        Assert.Equal("station.storage", storage.StationTag);
        Assert.True(storage.Airtight);
        Assert.False(storage.Toggleable);
        Assert.True(storage.Entity!.HasInventory);
        Assert.Single(storage.Entity.SlotGroups);
        Assert.Equal(SlotGroups.CARGO, storage.Entity.SlotGroups[0].GroupId);
        Assert.Null(storage.Entity.SlotGroups[0].Capacity);

        Brick core = shipped.Get("wb:core").Value;
        Assert.True(core.HasEntity);
        Assert.Equal("station.core", core.StationTag);
        Assert.Equal(50f, core.GetStatistic(StatisticIds.POWER_KW));
        Assert.True(core.Airtight);
        Assert.True(core.Toggleable);
        Assert.True(core.DefaultPowerEnabled);
        Assert.Single(core.Entity!.SlotGroups);
        Assert.Equal(SlotGroups.FUEL, core.Entity.SlotGroups[0].GroupId);

        Brick console = shipped.Get("wb:display_console").Value;
        Assert.Equal("station.console", console.StationTag);
        Assert.Equal(-1f, console.GetStatistic(StatisticIds.POWER_KW));
        Assert.True(console.Toggleable);
        Assert.False(console.Entity?.HasInventory ?? false);

        Brick panel = shipped.Get("wb:panel").Value;
        Assert.False(panel.HasEntity);
        Assert.Null(panel.Entity);

        //  Custom TOML parsing with tags, statistics, [Bricks.Entity.Light], slot overrides, and invalid entries skipped.
        string tempDir = Path.Combine(Path.GetTempPath(), $"wb-be-bricks-{Path.GetRandomFileName()}");
        string bricksDir = Path.Combine(tempDir, "bricks");
        string tagsDir = Path.Combine(tempDir, "lang", "tags");
        Directory.CreateDirectory(bricksDir);
        Directory.CreateDirectory(tagsDir);
        try
        {
            File.WriteAllText(
                Path.Combine(tagsDir, "bricks.toml"),
                """
                "Language" = ""
                [Tags]
                "station.refinery" = ["wb:test_refinery"]
                "brick.airtight" = ["wb:test_refinery"]
                "brick.toggleable" = ["wb:test_refinery"]
                "brick.default_power_off" = ["wb:test_refinery"]
                "brick.default_open" = ["wb:test_refinery"]
                """
            );
            File.WriteAllText(
                Path.Combine(bricksDir, "machines.toml"),
                """
                [[Bricks]]
                ID = "wb:test_refinery"
                [Bricks.Statistics]
                "wb:power_kw" = -12.5
                [Bricks.Entity.Light]
                Radius = 4.5
                Size = 0.5
                Color = "#FF8000"
                Intensity = 3.0
                CastShadows = true
                Offset = { X = 0.0, Y = 0.5, Z = 0.25 }
                [[Bricks.Entity.SlotGroups]]
                ID = "wb:input"
                Capacity = 3
                [[Bricks.Entity.SlotGroups]]
                ID = "wb:output"
                Capacity = 2
                [[Bricks.Entity.SlotGroups]]
                ID = ""
                Capacity = 1
                [[Bricks.Entity.SlotGroups]]
                ID = "wb:bad_cap"
                Capacity = -4
                [[Bricks.Entity.SlotGroups]]
                ID = "wb:input"
                Capacity = 9
                """
            );

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(tempDir));
            IFileParser[] parsers = [new TomlParser<BrickDefinitions>(), new LocalizedTagDefinitionParser()];
            var parseService = new VirtualFileParseService(parsers, vfs);
            var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parseService, vfs);
            var bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parseService, vfs, tags);

            Brick refinery = bricks.Get("wb:test_refinery").Value;
            Assert.True(refinery.HasEntity);
            Assert.Equal("station.refinery", refinery.StationTag);
            Assert.Equal(-12.5f, refinery.GetStatistic(StatisticIds.POWER_KW));
            Assert.True(refinery.Airtight);
            Assert.True(refinery.Toggleable);
            Assert.False(refinery.DefaultPowerEnabled);
            Assert.True(refinery.DefaultOpen);
            Assert.Equal(2, refinery.Entity!.SlotGroups.Count);
            Assert.Equal(new BrickSlotGroup(SlotGroups.INPUT, 3), refinery.Entity.SlotGroups[0]);
            Assert.Equal(new BrickSlotGroup(SlotGroups.OUTPUT, 2), refinery.Entity.SlotGroups[1]);

            Assert.NotNull(refinery.Entity.Light);
            BrickLight light = refinery.Entity.Light!.Value;
            Assert.Equal(4.5f, light.Radius);
            Assert.Equal(0.5f, light.Size);
            Assert.Equal(3.0f, light.Intensity);
            Assert.Equal(true, light.CastShadows);
            Assert.Equal(new Vector3(0f, 0.5f, 0.25f), light.Offset);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
        }
    }

    [Fact]
    public void LightEntityDecoratorRotatesConfiguredOffsetByVoxelOrientation()
    {
        var lightConfig = new BrickLight(
            Radius: 2.0f,
            Size: 0.4f,
            Color: new Vector3(0.2f, 0.8f, 1.0f),
            Intensity: 4.0f,
            CastShadows: true,
            Offset: new Vector3(0f, 0f, 0.5f),
            TintFromPaint: false);
        var brick = new Brick("wb:beacon", BrickAlpha.Opaque, false, null, BrickShape.Block, default, entity: new BrickEntity(lightConfig));

        var store = new DataStore();
        int parent = store.Alloc(Uuid.FromValue(77001));
        int entity = store.Alloc(BrickEntityId.Derive(Uuid.FromValue(77001), new Int3(2, 3, 4)));
        store.AddOrUpdate(entity, new ChildComponent(Uuid.FromValue(77001)) { LocalPosition = new Vector3(2f, 3f, 4f) });

        //  Rotate 90 degrees around Y (yaw = 1).
        var orientation = new Orientation(pitch: 0, yaw: 1, roll: 0);
        var voxel = new Voxel(_ID: 1, _ShapeLight: new ShapeLight(BrickShape.Block, 0), _OrientationFlags: new OrientationFlags(orientation));
        var voxelComponent = new VoxelComponent(new VoxelObject(16));

        var decorator = new LightEntityDecorator(LoadShippedBricks());
        decorator.Process(store, parent, entity, voxelComponent, new VoxelInfo(2, 3, 4, voxel), brick);

        Assert.True(store.TryGet(entity, out LightComponent light));
        Assert.Equal(2.0f, light.Radius);
        Assert.Equal(0.4f, light.Size);
        Assert.Equal(4.0f, light.Intensity);
        Assert.True(light.CastShadows);

        Assert.True(store.TryGet(entity, out ChildComponent child));
        Vector3 expectedOffset = Vector3.Transform(new Vector3(0f, 0f, 0.5f), orientation.ToQuaternion());
        Vector3 expectedPos = new Vector3(2f, 3f, 4f) + expectedOffset;
        Assert.True(Vector3.Distance(expectedPos, child.LocalPosition) < 0.001f);
    }

    [Fact]
    public void CustomServerDecoratorHooksBrickEntityLifecycleWithoutCoreEdits()
    {
        BrickDatabase bricks = LoadShippedBricks();
        var customDecorator = new RecordingServerDecorator("wb:panel");
        var service = new ServerBrickEntityService(bricks, decorators: [customDecorator]);

        var store = new DataStore();
        Uuid structureUuid = Uuid.FromValue(88001);
        var coord = new Int3(3, 1, 2);

        int entity = service.OnBrickPlaced(store, structureUuid, coord, bricks.Id("wb:panel"));
        Assert.NotEqual(Entity.Null, entity);
        Assert.Equal(1, customDecorator.SpawnCount);
        Assert.Equal(BrickEntityId.Derive(structureUuid, coord), store.GetUuid(entity));

        Assert.True(service.OnBrickBroken(store, structureUuid, coord));
        Assert.Equal(1, customDecorator.DespawnCount);
    }

    [Fact]
    public async Task ClientReconcilePreservesLocalVisualBrickEntityOnViewerDetach()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using LoopbackHarness harness = LoopbackHarness.Create(bricks);
        LoopbackClient viewer = await harness.AddPlayerClientAsync(Uuid.FromValue(2601), new Vector3(1f, 0f, 1.5f));
        harness.ClearAllDirty();

        Uuid structureUuid = Uuid.FromValue(91001);
        var coreCoord = new Int3(1, 0, 0);
        harness.CreateStructure(structureUuid, Vector3.Zero, Quaternion.Identity, (coreCoord, bricks.Id("wb:core")));
        int serverCore = harness.BrickEntities.OnBrickPlaced(harness.ServerStore, structureUuid, coreCoord, bricks.Id("wb:core"));
        Uuid coreUuid = harness.ServerStore.GetUuid(serverCore);
        Assert.Equal(BrickEntityId.Derive(structureUuid, coreCoord), coreUuid);

        var clientStore = new DataStore();
        int localPlayer = clientStore.Alloc(viewer.EntityUuid);
        clientStore.AddOrUpdate(localPlayer, new PlayerComponent());

        //  Pre-seed a client-side visual brick entity at the exact same deterministic UUID (as VoxelEntityBuilder does).
        int clientVisualEntity = clientStore.Alloc(coreUuid);
        clientStore.AddOrUpdate(clientVisualEntity, new BrickAnchorComponent(structureUuid, coreCoord));
        clientStore.AddOrUpdate(clientVisualEntity, new ChildComponent(structureUuid) { LocalPosition = new Vector3(1f, 0f, 0f) });
        clientStore.AddOrUpdate(clientVisualEntity, new LightComponent(radius: 3f, color: Vector3.One, size: 0.5f));

        ClientReconcileSystem reconcile = CreateClientReconcile(viewer.Inbound, harness.Registry);

        //  Attaching viewer replicates server components onto the SAME client entity (no duplicate).
        Assert.True(harness.BrickEntities.AttachViewer(harness.ServerStore, viewer.ServerPeer.GameId, serverCore).Success);
        harness.Replication.PublishStage(1f, harness.ServerStore);
        WorldSnapshot attachSnap = await viewer.WaitForSnapshotAsync();
        viewer.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(attachSnap));
        TickReconcileWhilePlaying(reconcile, clientStore);

        Assert.True(clientStore.TryGet(coreUuid, out int resolvedClientEntity));
        Assert.Equal(clientVisualEntity, resolvedClientEntity);
        Assert.True(clientStore.TryGet(clientVisualEntity, out MachineStateComponent clientMachine));
        Assert.Equal("station.core", clientMachine.StationTag);
        Assert.True(clientStore.TryGet(clientVisualEntity, out InventoryComponent _));
        Assert.True(clientStore.TryGet(clientVisualEntity, out LightComponent _));

        //  Detaching viewer strips server-replicated components while preserving the local visual LightComponent and ChildComponent.
        Assert.True(harness.BrickEntities.DetachViewer(harness.ServerStore, viewer.ServerPeer.GameId, serverCore));
        harness.Replication.PublishStage(1f, harness.ServerStore);
        WorldSnapshot detachSnap = await viewer.WaitForSnapshotAsync();
        viewer.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(detachSnap));
        TickReconcileWhilePlaying(reconcile, clientStore);

        Assert.True(clientStore.TryGet(coreUuid, out int preservedEntity));
        Assert.Equal(clientVisualEntity, preservedEntity);
        Assert.True(clientStore.TryGet(preservedEntity, out LightComponent _));
        Assert.True(clientStore.TryGet(preservedEntity, out ChildComponent _));
        Assert.False(clientStore.TryGet(preservedEntity, out MachineStateComponent _));
        Assert.False(clientStore.TryGet(preservedEntity, out InventoryComponent _));
    }

    [Fact]
    public async Task ClientReconcileFreesChildBrickEntitiesWhenParentStructureDespawns()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using LoopbackHarness harness = LoopbackHarness.Create(bricks);
        LoopbackClient viewer = await harness.AddPlayerClientAsync(Uuid.FromValue(2602), Vector3.Zero);
        harness.ClearAllDirty();

        Uuid structureUuid = Uuid.FromValue(91002);
        var lightCoord = new Int3(0, 1, 0);
        Uuid lightUuid = BrickEntityId.Derive(structureUuid, lightCoord);

        var clientStore = new DataStore();
        int localPlayer = clientStore.Alloc(viewer.EntityUuid);
        clientStore.AddOrUpdate(localPlayer, new PlayerComponent());

        int clientStructure = clientStore.Alloc(structureUuid);
        clientStore.AddOrUpdate(clientStructure, new VoxelComponent(new VoxelObject(16)));

        int clientLight = clientStore.Alloc(lightUuid);
        clientStore.AddOrUpdate(clientLight, new BrickAnchorComponent(structureUuid, lightCoord));
        clientStore.AddOrUpdate(clientLight, new ChildComponent(structureUuid) { LocalPosition = new Vector3(0f, 1f, 0f) });
        clientStore.AddOrUpdate(clientLight, new LightComponent(radius: 15f, color: Vector3.One, size: 2.4f));

        ClientReconcileSystem reconcile = CreateClientReconcile(viewer.Inbound, harness.Registry);
        harness.Replication.RequestDespawn(structureUuid.ToValue());
        harness.Replication.PublishStage(1f, harness.ServerStore);
        WorldSnapshot despawnSnap = await viewer.WaitForSnapshotAsync();
        viewer.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(despawnSnap));
        TickReconcileWhilePlaying(reconcile, clientStore);

        Assert.False(clientStore.TryGet(structureUuid, out _));
        Assert.False(clientStore.TryGet(lightUuid, out _));
    }

    private sealed class RecordingServerDecorator(string targetBrickId) : IServerBrickEntityDecorator
    {
        public int SpawnCount { get; private set; }
        public int DespawnCount { get; private set; }

        public bool AppliesTo(Brick brick) => string.Equals(brick.ID, targetBrickId, StringComparison.Ordinal);

        public void OnSpawn(DataStore store, int entity, Brick brick, in BrickEntityData? persisted)
        {
            SpawnCount++;
        }

        public void OnDespawn(DataStore store, int entity, Brick brick)
        {
            DespawnCount++;
        }
    }

    [Fact]
    public void NetworkingCompositionRegistersMachineStateAndInventoryAsViewersOnly()
    {
        using var container = new Container();
        container.RegisterNetworking();
        NetworkRegistry registry = container.Resolve<NetworkRegistry>();

        Result<NetworkComponentInfo> invInfo = registry.GetInfo<InventoryComponent>();
        Assert.True(invInfo.Success);
        Assert.Equal(Uuid.FromValue(13), invInfo.Value.Uuid);
        Assert.Equal(NetworkDirection.ServerOwned, invInfo.Value.Direction);
        Assert.Equal(ReplicationAudience.ViewersOnly, invInfo.Value.Audience);
        Assert.IsType<InventoryCodec>(invInfo.Value.Codec);

        Result<NetworkComponentInfo> machineInfo = registry.GetInfo<MachineStateComponent>();
        Assert.True(machineInfo.Success);
        Assert.Equal(Uuid.FromValue(21), machineInfo.Value.Uuid);
        Assert.Equal(NetworkDirection.ServerOwned, machineInfo.Value.Direction);
        Assert.Equal(ReplicationAudience.ViewersOnly, machineInfo.Value.Audience);
        Assert.IsType<NsdComponentCodec<MachineStateComponent>>(machineInfo.Value.Codec);

        var custom = new NetworkRegistry();
        Result clientOwnedViewersOnly = custom.Register<InputComponent>(
            Uuid.FromValue(1),
            NetworkDirection.ClientOwned,
            new NsdComponentCodec<InputComponent>(),
            ReplicationAudience.ViewersOnly);
        Assert.False(clientOwnedViewersOnly.Success);
    }

    [Fact]
    public async Task PlacementAndBreakLifecycleSynchronizesIndexTransfersContentsAndCleansUpWithoutLeaks()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using LoopbackHarness harness = LoopbackHarness.Create(bricks);
        LoopbackClient breaker = await harness.AddPlayerClientAsync(Uuid.FromValue(2001), new Vector3(0f, 0f, 2f));
        LoopbackClient bystander = await harness.AddPlayerClientAsync(Uuid.FromValue(2002), new Vector3(20f, 0f, 0f));

        Uuid structureUuid = Uuid.FromValue(90001);
        int structureEntity = harness.CreateStructure(structureUuid, Vector3.Zero, Quaternion.Identity, (new Int3(0, 0, 0), bricks.Id("wb:panel")));

        //  Non-functional brick placement allocates no brick-entity.
        int panelResult = harness.BrickEntities.OnBrickPlaced(harness.ServerStore, structureUuid, new Int3(0, 0, 0), bricks.Id("wb:panel"));
        Assert.Equal(Entity.Null, panelResult);
        Assert.Equal(0, harness.BrickEntities.Count);

        //  Pre-allocate a colliding entity UUID to verify DeriveUuid avoids collisions safely.
        var storageCoord = new Int3(1, 0, 0);
        Uuid firstCandidate = BrickEntityId.DeriveUnique(structureUuid, storageCoord, harness.ServerStore);
        Assert.NotEqual(Uuid.Null, firstCandidate);
        Assert.NotEqual(structureUuid, firstCandidate);
        int blockerEntity = harness.ServerStore.Alloc(firstCandidate);

        //  Placing a functional wb:storage brick allocates a server ECS brick-entity with expected components.
        int storageEntity = harness.BrickEntities.OnBrickPlaced(harness.ServerStore, structureUuid, storageCoord, bricks.Id("wb:storage"));
        Assert.NotEqual(Entity.Null, storageEntity);
        Assert.Equal(1, harness.BrickEntities.Count);

        Uuid storageUuid = harness.ServerStore.GetUuid(storageEntity);
        Assert.NotEqual(Uuid.Null, storageUuid);
        Assert.NotEqual(firstCandidate, storageUuid);
        Assert.NotEqual(structureUuid, storageUuid);
        Assert.NotEqual(breaker.EntityUuid, storageUuid);

        Assert.True(harness.BrickEntities.TryGetEntity(structureUuid, storageCoord, out int lookedUp));
        Assert.Equal(storageEntity, lookedUp);

        Assert.True(harness.ServerStore.TryGet(storageEntity, out BrickAnchorComponent anchor));
        Assert.Equal(new BrickAnchorComponent(structureUuid, storageCoord), anchor);

        Assert.True(harness.ServerStore.TryGet(storageEntity, out InventoryComponent storageInv));
        Assert.True(storageInv.HasGroup(SlotGroups.CARGO));
        Assert.Equal(SlotGroups.DEFAULT_CARGO_CAPACITY, storageInv.GetGroupCapacity(SlotGroups.CARGO));

        Assert.True(harness.ServerStore.TryGet(storageEntity, out MachineStateComponent machineState));
        Assert.Equal("station.storage", machineState.StationTag);
        Assert.True(machineState.PowerEnabled);
        Assert.False(machineState.HasPower);
        Assert.Equal((byte)MachineStatusFlags.None, machineState.StatusFlags);

        //  Seed stored items and attach breaker as viewer.
        Result<int> seeded = harness.BrickEntities.SeedItems(
            harness.ServerStore,
            storageEntity,
            SlotGroups.CARGO,
            [new ItemData("wb:panel", 12, 100)]);
        Assert.True(seeded.Success);
        Assert.Equal(1, seeded.Value);

        harness.ClearAllDirty();
        Assert.True(harness.BrickEntities.AttachViewer(harness.ServerStore, breaker.ServerPeer.GameId, storageEntity).Success);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot attachSnap = await breaker.WaitForSnapshotAsync();
        Assert.Contains(attachSnap.Components, c => c.Entity == storageUuid.ToValue() && c.TypeUuid == harness.InventoryTypeUuid);
        Assert.Contains(attachSnap.Components, c => c.Entity == storageUuid.ToValue() && c.TypeUuid == harness.MachineStateTypeUuid);
        Assert.Empty(bystander.Inbound.Snapshots);

        //  Breaking the brick in Adventure transfers stored contents to breaker, tears down viewer, and frees brick-entity.
        bool broken = harness.BrickEntities.OnBrickBroken(
            harness.ServerStore,
            structureUuid,
            storageCoord,
            breaker.ServerEntity,
            GameMode.Adventure);
        Assert.True(broken);
        Assert.Equal(0, harness.BrickEntities.Count);
        Assert.False(harness.BrickEntities.TryGetEntity(structureUuid, storageCoord, out _));
        Assert.Equal(Uuid.Null, harness.ServerStore.GetUuid(storageEntity));
        Assert.Equal(0, harness.Replication.ActiveViewerSubscriptionCount);

        //  Player inventory received the stored cargo stack.
        Assert.True(harness.ServerStore.TryGet(breaker.ServerEntity, out InventoryComponent breakerInv));
        Assert.Equal(12, CountItem(in breakerInv, "wb:panel"));

        //  Publish delivers targeted despawn to former viewer; bystander receives nothing.
        harness.Replication.PublishStage(1f, harness.ServerStore);
        WorldSnapshot breakSnap = await breaker.WaitForSnapshotAsync();
        Assert.Contains(breakSnap.RemovedEntities, id => id == storageUuid.ToValue());
        Assert.Empty(bystander.Inbound.Snapshots);

        //  Cleanup blocker and structure cleanly.
        harness.ServerStore.Free(blockerEntity);
        harness.ServerStore.Free(structureEntity);
    }

    [Fact]
    public void TransferOpWorksOnAnchoredBrickEntitiesEvenWhenStructureMovesAndRotates()
    {
        BrickDatabase bricks = LoadShippedBricks();
        var store = new DataStore();
        var brickEntities = new ServerBrickEntityService(bricks);
        var inventorySystem = new ServerInventorySystem();

        //  Structure translated to (10, 0, 0) and rotated +90 deg around Y:
        //  local voxel (2, 0, 0) transforms to world (10, 0, -2).
        Uuid structureUuid = Uuid.FromValue(77001);
        Quaternion rot90Y = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);
        int structure = store.Alloc(structureUuid);
        store.AddOrUpdate(structure, new TransformComponent(new Vector3(10f, 0f, 0f), rot90Y, Vector3.One));

        int brickEntity = brickEntities.OnBrickPlaced(store, structureUuid, new Int3(2, 0, 0), bricks.Id("wb:storage"));
        Assert.NotEqual(Entity.Null, brickEntity);
        Uuid brickEntityUuid = store.GetUuid(brickEntity);

        //  Player stands at (10, 0, -1) — 1m from rotated brick-entity at (10, 0, -2), well within reach.
        Uuid playerUuid = Uuid.FromValue(77002);
        int player = store.Alloc(playerUuid);
        NetworkComponent playerNet = NetworkComponent.Create();
        InventoryComponent playerInv = InventoryComponent.CreatePlayer();
        playerInv.Set(new SlotId(SlotGroups.HOTBAR, 0), new ItemData("wb:rock", 20, 100));
        store.AddOrUpdate(player, playerNet);
        store.AddOrUpdate(player, new TransformComponent(new Vector3(10f, 0f, -1f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(player, playerInv);

        //  Transfer 5 rocks from player hotbar[0] -> brick-entity wb:cargo[0].
        playerNet.StagedInventoryOps!.Stage(
            sequenceNumber: 1,
            new TransferOp(
                _FromContainer: 0UL,
                _FromSlot: new SlotId(SlotGroups.HOTBAR, 0),
                _ToContainer: brickEntityUuid.ToValue(),
                _ToSlot: new SlotId(SlotGroups.CARGO, 0),
                _Mode: TransferMode.Exact,
                _Count: 5u));
        inventorySystem.Tick(1f / 60f, store);

        Assert.True(store.TryGet(player, out InventoryComponent playerAfterPush));
        Assert.True(store.TryGet(brickEntity, out InventoryComponent blockAfterPush));
        Assert.Equal(15, playerAfterPush.Get(new SlotId(SlotGroups.HOTBAR, 0)).Count);
        Assert.Equal(5, blockAfterPush.Get(new SlotId(SlotGroups.CARGO, 0)).Count);

        //  Move structure far away; subsequent TransferOp must be rejected as out of reach.
        store.AddOrUpdate(structure, new TransformComponent(new Vector3(100f, 0f, 0f), rot90Y, Vector3.One));
        playerNet.StagedInventoryOps!.Stage(
            sequenceNumber: 2,
            new TransferOp(
                _FromContainer: brickEntityUuid.ToValue(),
                _FromSlot: new SlotId(SlotGroups.CARGO, 0),
                _ToContainer: 0UL,
                _ToSlot: new SlotId(SlotGroups.HOTBAR, 1),
                _Mode: TransferMode.Exact,
                _Count: 5u));
        inventorySystem.Tick(1f / 60f, store);

        Assert.True(store.TryGet(brickEntity, out InventoryComponent blockAfterDenied));
        Assert.Equal(5, blockAfterDenied.Get(new SlotId(SlotGroups.CARGO, 0)).Count);
    }

    [Fact]
    public void ServerInteractionSystemAllocatesAndCleansUpBrickEntitiesOnPlaceAndBreak()
    {
        BrickDatabase bricks = LoadShippedBricks();
        var items = new StubItemDatabase(new Item(
            id: "wb:storage",
            name: "item.storage",
            iconID: null,
            maxStack: 100,
            placeable: new PlaceableDefinition { Type = PlaceableType.Brick, ID = "wb:storage" },
            tool: null,
            viewModel: null,
            worldModel: null));

        var peers = new ServerPeers();
        var sessions = new SessionManager();
        var store = new DataStore();
        var physics = new StubServerWorldPhysics();
        var brickEntities = new ServerBrickEntityService(bricks, sessions: sessions);

        var handlers = new InteractionHandlerRegistry(
        [
            new ItemPlaceableSelector(items, bricks),
            new HintResolver(bricks),
            new VoxelWriteCommitter(peers, bricks, bricks, NullLogger<VoxelWriteCommitter>.Instance),
        ]);

        using var simStep = new SimulationStep(store, physics);
        var interactionSystem = new ServerInteractionSystem(
            sessions,
            NullLogger<ServerInteractionSystem>.Instance,
            handlers,
            s => new ServerVoxelInteractionWorld(s, physics),
            simStep,
            brickEntities);

        Uuid structureUuid = Uuid.FromValue(66001);
        int structure = store.Alloc(structureUuid);
        var voxels = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
        voxels.Set(0, 0, 0, new Voxel { ID = bricks.Id("wb:panel") });
        store.AddOrUpdate(structure, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(structure, new VoxelWorldComponent(voxels));
        store.AddOrUpdate(structure, new NetworkComponent());

        Uuid playerUuid = Uuid.FromValue(66002);
        int player = store.Alloc(playerUuid);
        NetworkComponent playerNet = NetworkComponent.Create();
        InventoryComponent playerInv = InventoryComponent.CreatePlayer();
        playerInv.Set(new SlotId(SlotGroups.HOTBAR, 0), new ItemData("wb:storage", 2, 100));
        store.AddOrUpdate(player, playerNet);
        store.AddOrUpdate(player, new OwnedCharacterComponent());
        store.AddOrUpdate(player, new TransformComponent(new Vector3(0f, 0f, 2f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(player, new EquipmentComponent(0));
        store.AddOrUpdate(player, new GameModeComponent((int)GameMode.Adventure));
        store.AddOrUpdate(player, playerInv);
        sessions.Register(store, player, Uuid.FromValue(66003), new Session(1));

        //  Place wb:storage at (1, 0, 0) via ServerInteractionSystem.
        playerNet.StagedInteractions!.Stage(new InteractionEvent
        {
            Entity = playerUuid.ToValue(),
            SequenceNumber = 1,
            ServerTickAtSample = 1,
            Kind = InteractionKind.PrimaryPressed,
            Brick = new BrickInteraction
            {
                TargetEntity = structureUuid.ToValue(),
                TargetX = 1,
                TargetY = 0,
                TargetZ = 0,
                HintShape = (byte)BrickShape.Block,
                HintOrientation = 0,
            },
        });
        interactionSystem.Tick(1f / 60f, store, simTick: 1);

        Assert.True(brickEntities.TryGetEntity(structureUuid, new Int3(1, 0, 0), out int spawnedBrickEntity));
        Assert.True(store.TryGet(spawnedBrickEntity, out MachineStateComponent machine));
        Assert.Equal("station.storage", machine.StationTag);

        //  Seed an item into the storage brick-entity, switch player to empty slot, and break it.
        Assert.True(brickEntities.SeedItems(store, spawnedBrickEntity, SlotGroups.CARGO, [new ItemData("wb:ice", 6, 100)]).Success);
        store.AddOrUpdate(player, new EquipmentComponent(1));

        playerNet.StagedInteractions!.Stage(new InteractionEvent
        {
            Entity = playerUuid.ToValue(),
            SequenceNumber = 2,
            ServerTickAtSample = 2,
            Kind = InteractionKind.PrimaryPressed,
            Brick = new BrickInteraction
            {
                TargetEntity = structureUuid.ToValue(),
                TargetX = 1,
                TargetY = 0,
                TargetZ = 0,
                HintShape = (byte)BrickShape.Block,
                HintOrientation = 0,
            },
        });
        interactionSystem.Tick(1f / 60f, store, simTick: 2);

        Assert.Equal(0, brickEntities.Count);
        Assert.Equal(Uuid.Null, store.GetUuid(spawnedBrickEntity));
        Assert.True(store.TryGet(player, out InventoryComponent playerAfterBreak));
        Assert.Equal(6, CountItem(in playerAfterBreak, "wb:ice"));
    }

    [Fact]
    public async Task BlueprintSpawnAndStarterWreckAllocateBrickEntitiesAndAcceptCargoSeeding()
    {
        BrickDatabase bricks = LoadShippedBricks();
        string tempRoot = Path.Combine(Path.GetTempPath(), $"wb-be-wreck-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            //  Part 1: LevelGenerator Adventure starter wreck loads via LevelSaveService and allocates functional block-entities.
            AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(tempRoot));
            var storagePaths = new StoragePaths(new StorageConfig(), appPaths);
            var gameplayConfig = new GameplayConfig();
            var levelGenConfig = new LevelGeneratorConfig();
            levelGenConfig.AsteroidCount.Set(2);
            levelGenConfig.AsteroidMinRadius.Set(6);
            levelGenConfig.AsteroidMaxRadius.Set(8);
            levelGenConfig.WorldSpan.Set(100);
            levelGenConfig.WorldHeight.Set(40);

            IBundledBlueprintDatabase bundled = CreateServerBundledBlueprintDatabase();
            var migrator = new Migrator([]);
            var seedPhrases = CreateSeedPhraseService();

            var catalog = new SqliteLevelCatalog(
                NullLogger<SqliteLevelCatalog>.Instance,
                storagePaths,
                bricks,
                NullLoggerFactory.Instance,
                migrator,
                gameplayConfig,
                levelGenConfig,
                seedPhrases,
                paintRegistry: null,
                bundledBlueprints: bundled);

            Assert.True(catalog.Create("Adventure Wreck", "seed-brick-entity-929", GameMode.Adventure, out string levelGuid));

            var store = new DataStore();
            var brickEntities = new ServerBrickEntityService(bricks);
            using var saveService = new LevelSaveService(
                NullLogger<LevelSaveService>.Instance,
                catalog,
                bricks,
                bricks,
                gameplayConfig,
                paintRegistry: null,
                brickEntities: brickEntities);

            Assert.True(saveService.LoadLevel(levelGuid, store));

            var expectedGen = new LevelGenerator(saveService.CurrentLevel!.Value.Seed, bricks, levelGenConfig, null, bundled);
            Uuid wreckUuid = expectedGen.GenerateStarterWreckUuid();
            int[] wreckBrickEntities = brickEntities.GetEntitiesForStructure(wreckUuid);
            Assert.NotEmpty(wreckBrickEntities);

            //  Verify starter wreck storage container accepts programmatic cargo seeding and persists across reload.
            int cargoEntity = Entity.Null;
            for (var i = 0; i < wreckBrickEntities.Length; i++)
            {
                if (store.TryGet(wreckBrickEntities[i], out InventoryComponent inv) && inv.HasGroup(SlotGroups.CARGO))
                {
                    cargoEntity = wreckBrickEntities[i];
                    break;
                }
            }

            if (cargoEntity == Entity.Null)
            {
                //  Fall back to first inventoried brick-entity if the starter wreck uses wb:core fuel slot instead.
                cargoEntity = wreckBrickEntities.First(e => store.TryGet(e, out InventoryComponent _));
            }

            Assert.True(store.TryGet(cargoEntity, out InventoryComponent targetInv));
            string seedGroup = targetInv.Groups[0].GroupId;
            Result<int> seeded = brickEntities.SeedItems(store, cargoEntity, seedGroup, [new ItemData("wb:panel", 7, 100)]);
            Assert.True(seeded.Success);
            Assert.Equal(1, seeded.Value);
            Assert.True(store.TryGet(cargoEntity, out InventoryComponent seededInv));
            Assert.Equal(7, CountItem(in seededInv, "wb:panel"));

            //  Part 2: ServerRegionSystem PasteRegionPart spawns a new structure with functional bricks and cleans up on Cut.
            using LoopbackHarness harness = LoopbackHarness.Create(bricks);
            LoopbackClient builder = await harness.AddPlayerClientAsync(Uuid.FromValue(3001), Vector3.Zero);
            harness.ServerStore.AddOrUpdate(builder.ServerEntity, new GameModeComponent((int)GameMode.Creative));

            var permPolicy = PermissionPolicy.Create([]);
            var perms = new UserPermissionService(new PermissionResolver(permPolicy));
            perms.Bind(builder.ServerPeer.GameId, new UserClaim("builder"), isHost: true);

            var regionSystem = new ServerRegionSystem(
                harness.Peers,
                harness.Sessions,
                perms,
                new StubServerWorldPhysics(),
                harness.Replication,
                bricks,
                bricks,
                NullLogger<ServerRegionSystem>.Instance,
                brickEntities: harness.BrickEntities);

            var blueprintVoxels = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
            blueprintVoxels.Set(0, 0, 0, new Voxel { ID = bricks.Id("wb:storage") });
            blueprintVoxels.Set(1, 0, 0, new Voxel { ID = bricks.Id("wb:core") });
            blueprintVoxels.Set(2, 0, 0, new Voxel { ID = bricks.Id("wb:panel") });
            VoxelRegionData encodedRegion = VoxelCodec.Encode(
                new VoxelRegionData(3, 1, 1, blueprintVoxels.GetChunkInfos(), null, null),
                bricks);

            var pasteRequest = new PasteRegionRequest
            {
                TargetKind = PasteTargetKind.NewStructure,
                SpawnX = 0f,
                SpawnY = 0f,
                SpawnZ = -3f,
                SpawnOrientationW = 1f,
                SizeX = encodedRegion.SizeX,
                SizeY = encodedRegion.SizeY,
                SizeZ = encodedRegion.SizeZ,
                Chunks = encodedRegion.Chunks,
                BrickPalette = encodedRegion.BrickPalette,
                PaintPalette = encodedRegion.PaintPalette,
            };

            var pasteTcs = new TaskCompletionSource<RpcResult<RegionOpResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
            builder.ServerPeer.PasteRegionParts.Enqueue(new ServerPeer.PasteRegionPartCall(
                new RegionUploadPart
                {
                    BatchId = 1,
                    PartIndex = 0,
                    PartCount = 1,
                    Payload = pasteRequest.Serialize(),
                },
                pasteTcs,
                CancellationToken.None));

            regionSystem.Tick(1f / 60f, harness.ServerStore);
            RpcResult<RegionOpResponse> pasteResult = await pasteTcs.Task;
            Assert.True(pasteResult.IsSuccess && pasteResult.Value.Success, pasteResult.ErrorMessage ?? pasteResult.Value.ErrorKey);

            Uuid pastedUuid = Uuid.FromValue(pasteResult.Value.SpawnedEntityUuid);
            Assert.Equal(2, harness.BrickEntities.GetEntitiesForStructure(pastedUuid).Length);
            Assert.True(harness.BrickEntities.TryFindFirstByStationTag(harness.ServerStore, pastedUuid, "station.storage", out int pastedStorage));
            Assert.True(harness.BrickEntities.TryFindFirstByStationTag(harness.ServerStore, pastedUuid, "station.core", out _));

            //  Seed cargo into the freshly pasted blueprint's storage brick-entity.
            Result<int> pasteSeeded = harness.BrickEntities.SeedItems(
                harness.ServerStore,
                pastedStorage,
                SlotGroups.CARGO,
                [new ItemData("wb:ice", 25, 100)]);
            Assert.True(pasteSeeded.Success);
            Assert.Equal(1, pasteSeeded.Value);

            //  Cut entire pasted structure -> structure despawns and both block-entities are freed without leaks.
            var cutTcs = new TaskCompletionSource<RpcResult<RegionOpResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
            builder.ServerPeer.CutRegionCalls.Enqueue(new ServerPeer.CutRegionCall(
                new CutRegionRequest
                {
                    TargetEntity = pastedUuid.ToValue(),
                    MinX = -4,
                    MinY = -4,
                    MinZ = -4,
                    MaxX = 4,
                    MaxY = 4,
                    MaxZ = 4,
                },
                cutTcs,
                CancellationToken.None));

            regionSystem.Tick(1f / 60f, harness.ServerStore);
            RpcResult<RegionOpResponse> cutResult = await cutTcs.Task;
            Assert.True(cutResult.IsSuccess && cutResult.Value.Success);
            Assert.Equal(0, harness.BrickEntities.Count);
            Assert.Equal(Uuid.Null, harness.ServerStore.GetUuid(pastedStorage));
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    [Fact]
    public async Task ViewerScopedReplicationStreamsExclusivelyToSubscribedViewersAndCleansUpOnDetachAndOutOfReach()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using LoopbackHarness harness = LoopbackHarness.Create(bricks);
        LoopbackClient viewerA = await harness.AddPlayerClientAsync(Uuid.FromValue(4001), new Vector3(0f, 0f, 2f));
        LoopbackClient viewerB = await harness.AddPlayerClientAsync(Uuid.FromValue(4002), new Vector3(0f, 0f, -2f));
        LoopbackClient nonViewer = await harness.AddPlayerClientAsync(Uuid.FromValue(4003), new Vector3(0f, 0f, 3f));

        Uuid structureUuid = Uuid.FromValue(95001);
        harness.CreateStructure(structureUuid, Vector3.Zero, Quaternion.Identity, (new Int3(0, 0, 0), bricks.Id("wb:core")));
        int machineEntity = harness.BrickEntities.OnBrickPlaced(harness.ServerStore, structureUuid, new Int3(0, 0, 0), bricks.Id("wb:core"));
        Uuid machineUuid = harness.ServerStore.GetUuid(machineEntity);

        harness.BrickEntities.SeedItems(harness.ServerStore, machineEntity, SlotGroups.FUEL, [new ItemData("wb:ice", 4, 100)]);
        harness.ClearAllDirty();

        //  1. Join full sync delivers each player's own inventory, omits other players' inventories, and omits block-entities entirely.
        harness.Replication.RequestFullSync(viewerA.ServerPeer.GameId);
        harness.Replication.RequestFullSync(nonViewer.ServerPeer.GameId);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot fullA = await viewerA.WaitForSnapshotAsync();
        WorldSnapshot fullNonViewer = await nonViewer.WaitForSnapshotAsync();

        Assert.Contains(fullA.Components, c => c.Entity == viewerA.EntityUuid.ToValue() && c.TypeUuid == harness.InventoryTypeUuid);
        Assert.DoesNotContain(fullA.Components, c => c.Entity == viewerB.EntityUuid.ToValue() && c.TypeUuid == harness.InventoryTypeUuid);
        Assert.DoesNotContain(fullA.Components, c => c.Entity == machineUuid.ToValue());
        Assert.DoesNotContain(fullNonViewer.Components, c => c.Entity == machineUuid.ToValue());
        Assert.Empty(viewerB.Inbound.Snapshots);

        //  2. Unsubscribed brick-entity mutation produces zero wire snapshots for all clients and clears dirty flags.
        Assert.True(harness.ServerStore.TryGet(machineEntity, out MachineStateComponent state));
        state.Progress = 1.5f;
        state.Duration = 10f;
        state.ActiveRecipeId = "wb:core_burn";
        state.SetStatusFlag(MachineStatusFlags.Running, true);
        harness.ServerStore.AddOrUpdate(machineEntity, state);

        harness.Replication.PublishStage(1f, harness.ServerStore);
        Assert.Empty(viewerA.Inbound.Snapshots);
        Assert.Empty(viewerB.Inbound.Snapshots);
        Assert.Empty(nonViewer.Inbound.Snapshots);
        Assert.False(harness.ServerStore.IsDirty<MachineStateComponent>(machineEntity));

        //  3. Attaching viewer A streams initial full snapshot of InventoryComponent + MachineStateComponent exclusively to A.
        Assert.True(harness.BrickEntities.AttachViewer(harness.ServerStore, viewerA.ServerPeer.GameId, machineEntity).Success);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot attachA = await viewerA.WaitForSnapshotAsync();
        Assert.Contains(attachA.Components, c => c.Entity == machineUuid.ToValue() && c.TypeUuid == harness.InventoryTypeUuid);
        Assert.Contains(attachA.Components, c => c.Entity == machineUuid.ToValue() && c.TypeUuid == harness.MachineStateTypeUuid);
        Assert.Empty(viewerB.Inbound.Snapshots);
        Assert.Empty(nonViewer.Inbound.Snapshots);

        //  Reconcile onto client A's DataStore and verify both components hydrate cleanly.
        var clientAStore = new DataStore();
        int localPlayerA = clientAStore.Alloc(viewerA.EntityUuid);
        clientAStore.AddOrUpdate(localPlayerA, new PlayerComponent());
        ClientReconcileSystem reconcileA = CreateClientReconcile(viewerA.Inbound, harness.Registry);
        viewerA.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(attachA));
        TickReconcileWhilePlaying(reconcileA, clientAStore);

        Assert.True(clientAStore.TryGet(machineUuid, out int clientMachineA));
        Assert.True(clientAStore.TryGet(clientMachineA, out MachineStateComponent clientStateA));
        Assert.Equal("station.core", clientStateA.StationTag);
        Assert.Equal("wb:core_burn", clientStateA.ActiveRecipeId);
        Assert.Equal(1.5f, clientStateA.Progress);
        Assert.True(clientAStore.TryGet(clientMachineA, out InventoryComponent clientInvA));
        Assert.Equal(4, CountItem(in clientInvA, "wb:ice"));

        //  4. Attach viewer B as well, then mutate MachineStateComponent: both A and B receive delta, nonViewer receives nothing.
        Assert.True(harness.BrickEntities.AttachViewer(harness.ServerStore, viewerB.ServerPeer.GameId, machineEntity).Success);
        harness.Replication.PublishStage(1f, harness.ServerStore);
        WorldSnapshot attachB = await viewerB.WaitForSnapshotAsync();
        Assert.Contains(attachB.Components, c => c.Entity == machineUuid.ToValue() && c.TypeUuid == harness.MachineStateTypeUuid);
        Assert.Empty(viewerA.Inbound.Snapshots);
        Assert.Empty(nonViewer.Inbound.Snapshots);

        state.Progress = 5.0f;
        harness.ServerStore.AddOrUpdate(machineEntity, state);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot deltaA = await viewerA.WaitForSnapshotAsync();
        WorldSnapshot deltaB = await viewerB.WaitForSnapshotAsync();
        Assert.Single(deltaA.Components);
        Assert.Equal(machineUuid.ToValue(), deltaA.Components[0].Entity);
        Assert.Equal(harness.MachineStateTypeUuid, deltaA.Components[0].TypeUuid);
        Assert.Single(deltaB.Components);
        Assert.Equal(machineUuid.ToValue(), deltaB.Components[0].Entity);
        Assert.Empty(nonViewer.Inbound.Snapshots);

        //  5. Explicit detach of viewer A sends despawn ONLY to A and frees client A's mirrored brick-entity.
        Assert.True(harness.BrickEntities.DetachViewer(harness.ServerStore, viewerA.ServerPeer.GameId, machineEntity));
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot detachA = await viewerA.WaitForSnapshotAsync();
        Assert.Contains(detachA.RemovedEntities, id => id == machineUuid.ToValue());
        Assert.Empty(viewerB.Inbound.Snapshots);
        Assert.Empty(nonViewer.Inbound.Snapshots);

        viewerA.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(detachA));
        TickReconcileWhilePlaying(reconcileA, clientAStore);
        Assert.False(clientAStore.TryGet(machineUuid, out _));

        //  6. Moving viewer B out of reach causes ServerBrickEntitySystem to detach B and despawn on B's client.
        var brickEntitySystem = new ServerBrickEntitySystem(harness.BrickEntities);
        harness.ServerStore.AddOrUpdate(viewerB.ServerEntity, new TransformComponent(new Vector3(0f, 0f, 50f), Quaternion.Identity, Vector3.One));
        harness.ClearAllDirty();

        brickEntitySystem.Tick(1f / 60f, harness.ServerStore);
        Assert.False(harness.Replication.IsViewerSubscribed(machineEntity, viewerB.ServerPeer.GameId));
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot outOfReachB = await viewerB.WaitForSnapshotAsync();
        Assert.Contains(outOfReachB.RemovedEntities, id => id == machineUuid.ToValue());
        Assert.Empty(viewerA.Inbound.Snapshots);
        Assert.Empty(nonViewer.Inbound.Snapshots);

        //  7. Client disconnect teardown clears active viewer subscriptions without leaks.
        Assert.True(harness.BrickEntities.AttachViewer(harness.ServerStore, viewerA.ServerPeer.GameId, machineEntity).Success);
        Assert.Equal(1, harness.Replication.ActiveViewerSubscriptionCount);
        harness.Replication.CancelStream(viewerA.ServerPeer.GameId);
        Assert.Equal(0, harness.Replication.ActiveViewerSubscriptionCount);

        //  8. Recycled entity index safety: if an entity slot is freed and immediately reused by a different
        //     entity, DetachOutOfReachViewers despawns the originally delivered UUID and never the recycled entity.
        Assert.True(harness.BrickEntities.AttachViewer(harness.ServerStore, viewerA.ServerPeer.GameId, machineEntity).Success);
        harness.Replication.PublishStage(1f, harness.ServerStore);
        _ = await viewerA.WaitForSnapshotAsync();

        harness.ServerStore.Free(machineEntity);
        Uuid recycledUuid = Uuid.FromValue(95999);
        int recycledEntity = harness.ServerStore.Alloc(recycledUuid);
        Assert.Equal(machineEntity, recycledEntity);

        brickEntitySystem.Tick(1f / 60f, harness.ServerStore);
        harness.Replication.PublishStage(1f, harness.ServerStore);
        WorldSnapshot recycledDetach = await viewerA.WaitForSnapshotAsync();
        Assert.Contains(recycledDetach.RemovedEntities, id => id == machineUuid.ToValue());
        Assert.DoesNotContain(recycledDetach.RemovedEntities, id => id == recycledUuid.ToValue());
    }

    [Fact]
    public void LevelDbSaveLoadRoundTripsBrickEntitiesAndPrunesOrphans()
    {
        BrickDatabase bricks = LoadShippedBricks();
        string tempRoot = Path.Combine(Path.GetTempPath(), $"wb-be-save-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempRoot);
        try
        {
            AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(tempRoot));
            var storagePaths = new StoragePaths(new StorageConfig(), appPaths);
            var gameplayConfig = new GameplayConfig();
            var migrator = new Migrator([]);
            const string levelGuid = "brick-entity-roundtrip-level";

            //  Seed initial level.db with one structure containing wb:storage, wb:core, and wb:panel.
            Uuid structureUuid = Uuid.FromValue(88001);
            var initialVoxels = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
            initialVoxels.Set(0, 0, 0, new Voxel { ID = bricks.Id("wb:storage") });
            initialVoxels.Set(1, 0, 0, new Voxel { ID = bricks.Id("wb:core") });
            initialVoxels.Set(2, 0, 0, new Voxel { ID = bricks.Id("wb:panel") });

            var rawStructure = new VoxelEntityData(
                structureUuid.ToValue(),
                10, 20, 30,
                0, 0, 0, 1,
                1, 1, 1,
                initialVoxels.GetChunkInfos(),
                _BrickPalette: null,
                _PaintPalette: null);
            VoxelEntityData encodedStructure = VoxelCodec.Encode(in rawStructure, bricks).Value;
            var level = new Level(
                new Data.Version(DataVersion.CURRENT, "Wayward Beyond", "Test"),
                _Seed: 42L,
                _Playtime: new Playtime(),
                _SpawnX: 0,
                _SpawnY: 0,
                _SpawnZ: 0,
                GameMode.Adventure,
                levelGuid,
                "Test Level");

            Directory.CreateDirectory(storagePaths.GetLevelDirectory(levelGuid));
            using (var initStorage = new SqliteLevelStorage(NullLogger<SqliteLevelStorage>.Instance, storagePaths, levelGuid, migrator))
            {
                Assert.True(initStorage.Save(level, [encodedStructure], [], []).Success);
            }

            var catalog = new SqliteLevelCatalog(
                NullLogger<SqliteLevelCatalog>.Instance,
                storagePaths,
                bricks,
                NullLoggerFactory.Instance,
                migrator,
                gameplayConfig,
                new LevelGeneratorConfig(),
                CreateSeedPhraseService());

            //  Session 1: load level, mutate storage inventory and core machine state, then Flush.
            var store1 = new DataStore();
            var brickEntities1 = new ServerBrickEntityService(bricks);
            using (var saveService1 = new LevelSaveService(
                NullLogger<LevelSaveService>.Instance,
                catalog,
                bricks,
                bricks,
                gameplayConfig,
                paintRegistry: null,
                brickEntities: brickEntities1))
            {
                Assert.True(saveService1.LoadLevel(levelGuid, store1));
                Assert.Equal(2, brickEntities1.Count);

                Assert.True(brickEntities1.TryGetEntity(structureUuid, new Int3(0, 0, 0), out int storage1));
                Assert.True(brickEntities1.TryGetEntity(structureUuid, new Int3(1, 0, 0), out int core1));

                Assert.True(brickEntities1.SeedSlots(
                    store1,
                    storage1,
                    [
                        new KeyedSlotEntry(SlotGroups.CARGO, 0, new ItemData("wb:panel", 18, 100)),
                        new KeyedSlotEntry(SlotGroups.CARGO, 5, new ItemData("wb:rock", 33, 100)),
                    ]).Success);

                Assert.True(brickEntities1.SeedSlots(
                    store1,
                    core1,
                    [new KeyedSlotEntry(SlotGroups.FUEL, 0, new ItemData("wb:ice", 9, 100))]).Success);

                store1.AddOrUpdate(core1, new MachineStateComponent(
                    _StationTag: "station.core",
                    _ActiveRecipeId: "wb:core_fusion",
                    _Progress: 3.75f,
                    _Duration: 12.5f,
                    _PowerEnabled: false,
                    _HasPower: true,
                    _StatusFlags: (byte)(MachineStatusFlags.Open | MachineStatusFlags.Running)));

                saveService1.Flush(store1);
                saveService1.Unload(store1);
                Assert.Equal(0, brickEntities1.Count);
            }

            //  Session 2: reload into a brand-new DataStore and verify full round-trip fidelity.
            var store2 = new DataStore();
            var brickEntities2 = new ServerBrickEntityService(bricks);
            using (var saveService2 = new LevelSaveService(
                NullLogger<LevelSaveService>.Instance,
                catalog,
                bricks,
                bricks,
                gameplayConfig,
                paintRegistry: null,
                brickEntities: brickEntities2))
            {
                Assert.True(saveService2.LoadLevel(levelGuid, store2));
                Assert.Equal(2, brickEntities2.Count);

                Assert.True(brickEntities2.TryGetEntity(structureUuid, new Int3(0, 0, 0), out int storage2));
                Assert.True(brickEntities2.TryGetEntity(structureUuid, new Int3(1, 0, 0), out int core2));

                Assert.True(store2.TryGet(storage2, out InventoryComponent restoredStorageInv));
                Assert.Equal("wb:panel", restoredStorageInv.Get(new SlotId(SlotGroups.CARGO, 0)).ID);
                Assert.Equal(18, restoredStorageInv.Get(new SlotId(SlotGroups.CARGO, 0)).Count);
                Assert.Equal("wb:rock", restoredStorageInv.Get(new SlotId(SlotGroups.CARGO, 5)).ID);
                Assert.Equal(33, restoredStorageInv.Get(new SlotId(SlotGroups.CARGO, 5)).Count);

                Assert.True(store2.TryGet(core2, out InventoryComponent restoredCoreInv));
                Assert.Equal("wb:ice", restoredCoreInv.Get(new SlotId(SlotGroups.FUEL, 0)).ID);
                Assert.Equal(9, restoredCoreInv.Get(new SlotId(SlotGroups.FUEL, 0)).Count);

                Assert.True(store2.TryGet(core2, out MachineStateComponent restoredCoreState));
                Assert.Equal("station.core", restoredCoreState.StationTag);
                Assert.Equal("wb:core_fusion", restoredCoreState.ActiveRecipeId);
                Assert.Equal(3.75f, restoredCoreState.Progress);
                Assert.Equal(12.5f, restoredCoreState.Duration);
                Assert.False(restoredCoreState.PowerEnabled);
                Assert.False(restoredCoreState.HasPower);
                Assert.True(restoredCoreState.HasStatusFlag(MachineStatusFlags.Open));
                Assert.False(restoredCoreState.HasStatusFlag(MachineStatusFlags.Running));

                saveService2.Unload(store2);
            }

            //  Session 3: inject orphaned brick-entity records (missing structure + non-matching brick coord)
            //  and verify LoadLevel ignores them AND deletes them from level.db.
            using (var orphanInject = new SqliteLevelStorage(NullLogger<SqliteLevelStorage>.Instance, storagePaths, levelGuid, migrator))
            {
                IReadOnlyList<BrickEntityData> existing = orphanInject.GetBrickEntities();
                Assert.Equal(2, existing.Count);

                var withOrphans = new List<BrickEntityData>(existing)
                {
                    new(
                        _VoxelEntityUuid: 999999UL,
                        _VoxelX: 0,
                        _VoxelY: 0,
                        _VoxelZ: 0,
                        _BrickId: "wb:storage",
                        _StationTag: "station.storage",
                        _ActiveRecipeId: null,
                        _Progress: 0f,
                        _Duration: 0f,
                        _PowerEnabled: true,
                        _StatusFlags: 0,
                        _Inventory: null),
                    new(
                        _VoxelEntityUuid: structureUuid.ToValue(),
                        _VoxelX: 2,
                        _VoxelY: 0,
                        _VoxelZ: 0,
                        _BrickId: "wb:storage",
                        _StationTag: "station.storage",
                        _ActiveRecipeId: null,
                        _Progress: 0f,
                        _Duration: 0f,
                        _PowerEnabled: true,
                        _StatusFlags: 0,
                        _Inventory: null),
                };

                Assert.True(orphanInject.Save(level, [encodedStructure], [], withOrphans).Success);
                Assert.Equal(4, orphanInject.GetBrickEntities().Count);
            }

            var store3 = new DataStore();
            var brickEntities3 = new ServerBrickEntityService(bricks);
            using (var saveService3 = new LevelSaveService(
                NullLogger<LevelSaveService>.Instance,
                catalog,
                bricks,
                bricks,
                gameplayConfig,
                paintRegistry: null,
                brickEntities: brickEntities3))
            {
                Assert.True(saveService3.LoadLevel(levelGuid, store3));
                Assert.Equal(2, brickEntities3.Count);
                Assert.False(brickEntities3.TryGetEntity(Uuid.FromValue(999999UL), new Int3(0, 0, 0), out _));
                Assert.False(brickEntities3.TryGetEntity(structureUuid, new Int3(2, 0, 0), out _));
                saveService3.Unload(store3);
            }

            using (var verifyPruned = new SqliteLevelStorage(NullLogger<SqliteLevelStorage>.Instance, storagePaths, levelGuid, migrator))
            {
                IReadOnlyList<BrickEntityData> remaining = verifyPruned.GetBrickEntities();
                Assert.Equal(2, remaining.Count);
                Assert.DoesNotContain(remaining, b => b.VoxelEntityUuid == 999999UL);
                Assert.DoesNotContain(remaining, b => b.VoxelEntityUuid == structureUuid.ToValue() && b.VoxelX == 2);
            }
        }
        finally
        {
            if (Directory.Exists(tempRoot))
            {
                Directory.Delete(tempRoot, recursive: true);
            }
        }
    }

    private static int CountItem(in InventoryComponent inventory, string itemId)
    {
        KeyedSlotEntry[] entries = inventory.ToSparseEntries();
        var total = 0;
        for (var i = 0; i < entries.Length; i++)
        {
            if (string.Equals(entries[i].Item.ID, itemId, StringComparison.Ordinal))
            {
                total += entries[i].Item.Count;
            }
        }

        return total;
    }

    private static BrickDatabase LoadShippedBricks()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Bricks", "assets")));
        IFileParser[] parsers = [new TomlParser<BrickDefinitions>(), new LocalizedTagDefinitionParser()];
        var parseService = new VirtualFileParseService(parsers, vfs);
        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parseService, vfs);
        return new BrickDatabase(NullLogger<BrickDatabase>.Instance, parseService, vfs, tags);
    }

    private static IBundledBlueprintDatabase CreateServerBundledBlueprintDatabase()
    {
        string repoRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
        string serverAssets = Path.Combine(repoRoot, "WaywardBeyond.Server", "assets");
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(serverAssets));
        var parseService = new VirtualFileParseService([new BlueprintDataParser()], vfs);
        return new BundledBlueprintDatabase(NullLogger<BundledBlueprintDatabase>.Instance, parseService, vfs);
    }

    private static SeedPhraseService CreateSeedPhraseService()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo("assets"));
        var parseService = new VirtualFileParseService([new LocalizedTagDefinitionParser()], vfs);
        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parseService, vfs);
        return new SeedPhraseService(tags);
    }

    private static ClientReconcileSystem CreateClientReconcile(ClientInbound inbound, NetworkRegistry registry)
    {
        var motionProcessor = new ClientPlayerMotionProcessor(
            new FakeInputService(),
            new FakeWindowContext(),
            new FakePhysics(),
            new EventInvoker<PlayerMovedEvent>([])
        );

        return new ClientReconcileSystem(
            inbound,
            new SnapshotAckTracker(),
            motionProcessor,
            new LocalPlayerReconciler(NullLogger<LocalPlayerReconciler>.Instance),
            registry,
            NullLogger<ClientReconcileSystem>.Instance
        );
    }

    private static void TickReconcileWhilePlaying(ClientReconcileSystem reconcile, DataStore store)
    {
        GameState previous = WaywardBeyondClient.GameState.Get();
        WaywardBeyondClient.GameState.Set(GameState.Playing);
        try
        {
            reconcile.Tick(1f / 128f, store);
        }
        finally
        {
            WaywardBeyondClient.GameState.Set(previous);
        }
    }

    private sealed class LoopbackHarness : IDisposable
    {
        private readonly LoopbackRpcServerTransport _transport;
        private readonly RpcServer _rpcServer;
        private readonly StubRpcHost _host = new();
        private readonly List<LoopbackClient> _clients = [];
        private readonly Lock _connectLock = new();
        private TaskCompletionSource<RpcPeer>? _pendingConnect;
        private uint _nextSessionId = 1;

        public DataStore ServerStore { get; } = new();
        public ServerPeers Peers { get; } = new();
        public SessionManager Sessions { get; } = new();
        public NetworkRegistry Registry { get; }
        public NetworkReplicationSystem Replication { get; }
        public ServerBrickEntityService BrickEntities { get; }
        public ulong InventoryTypeUuid { get; }
        public ulong MachineStateTypeUuid { get; }

        private LoopbackHarness(NetworkRegistry registry, IBrickDatabase bricks)
        {
            Registry = registry;
            Replication = new NetworkReplicationSystem(
                Peers,
                Sessions,
                NullLogger<NetworkReplicationSystem>.Instance,
                new NetworkingConfig(),
                registry);
            BrickEntities = new ServerBrickEntityService(
                bricks,
                Replication,
                Sessions);

            InventoryTypeUuid = registry.GetInfo<InventoryComponent>().Value.Uuid.ToValue();
            MachineStateTypeUuid = registry.GetInfo<MachineStateComponent>().Value.Uuid.ToValue();

            _transport = new LoopbackRpcServerTransport();
            var serverRpcRegistry = new RpcRegistry();
            serverRpcRegistry.Freeze();
            _rpcServer = new RpcServer(_transport, serverRpcRegistry);
            _rpcServer.ClientConnected += OnClientConnected;
            _rpcServer.Start();
        }

        public static LoopbackHarness Create(IBrickDatabase bricks)
        {
            using var container = new Container();
            container.RegisterNetworking();
            return new LoopbackHarness(container.Resolve<NetworkRegistry>(), bricks);
        }

        public int CreateStructure(Uuid structureUuid, Vector3 position, Quaternion orientation, params (Int3 Coord, ushort BrickId)[] voxels)
        {
            int entity = ServerStore.Alloc(structureUuid);
            var voxelObject = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
            for (var i = 0; i < voxels.Length; i++)
            {
                voxelObject.Set(voxels[i].Coord.X, voxels[i].Coord.Y, voxels[i].Coord.Z, new Voxel { ID = voxels[i].BrickId });
            }

            ServerStore.AddOrUpdate(entity, new TransformComponent(position, orientation, Vector3.One));
            ServerStore.AddOrUpdate(entity, new VoxelWorldComponent(voxelObject));
            ServerStore.AddOrUpdate(entity, new NetworkComponent());
            return entity;
        }

        public async Task<LoopbackClient> AddPlayerClientAsync(Uuid entityUuid, Vector3 position)
        {
            var connected = new TaskCompletionSource<RpcPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_connectLock)
            {
                _pendingConnect = connected;
            }

            var connection = new LoopbackRpcClientConnection(_transport);
            await connection.ConnectAsync();

            var inbound = new ClientInbound();
            var clientRpcRegistry = new RpcRegistry();
            clientRpcRegistry.AddService(new ClientHandlers(inbound));
            clientRpcRegistry.Freeze();
            _ = new RpcClient(connection, clientRpcRegistry);

            RpcPeer rpcPeer = await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
            ServerPeer serverPeer = Peers.GetOrAdd(_host, rpcPeer, isLocal: true);

            int entity = ServerStore.Alloc(entityUuid);
            ServerStore.AddOrUpdate(entity, NetworkComponent.Create());
            ServerStore.AddOrUpdate(entity, new TransformComponent(position, Quaternion.Identity, CharacterDefaults.PlayerScale));
            ServerStore.AddOrUpdate(entity, InventoryComponent.CreatePlayer());
            Sessions.Register(ServerStore, entity, serverPeer.GameId, new Session(_nextSessionId++));

            var client = new LoopbackClient(connection, inbound, serverPeer, entity, entityUuid);
            _clients.Add(client);
            return client;
        }

        public void ClearAllDirty()
        {
            NetworkComponentInfo[] serverOwned = Registry.GetComponents(NetworkDirection.ServerOwned);
            var action = new ClearDirtyAction(serverOwned);
            ServerStore.Query<NetworkComponent, ClearDirtyAction>(0f, ref action);
        }

        public void Dispose()
        {
            for (var i = 0; i < _clients.Count; i++)
            {
                _clients[i].Connection.Close();
            }

            _rpcServer.Close();
            _transport.Close();
        }

        private void OnClientConnected(RpcPeer peer)
        {
            lock (_connectLock)
            {
                _pendingConnect?.TrySetResult(peer);
            }
        }

        private readonly struct ClearDirtyAction(NetworkComponentInfo[] components) : IForEach<NetworkComponent>
        {
            public void Execute(float delta, DataStore store, int entity, in NetworkComponent net)
            {
                for (var i = 0; i < components.Length; i++)
                {
                    store.ClearDirty(components[i].Type, entity);
                }
            }
        }
    }

    private sealed class LoopbackClient(
        LoopbackRpcClientConnection connection,
        ClientInbound inbound,
        ServerPeer serverPeer,
        int serverEntity,
        Uuid entityUuid
    )
    {
        public LoopbackRpcClientConnection Connection { get; } = connection;
        public ClientInbound Inbound { get; } = inbound;
        public ServerPeer ServerPeer { get; } = serverPeer;
        public int ServerEntity { get; } = serverEntity;
        public Uuid EntityUuid { get; } = entityUuid;

        public async Task<WorldSnapshot> WaitForSnapshotAsync()
        {
            long deadline = Environment.TickCount64 + 5_000;
            while (Inbound.Snapshots.IsEmpty && Environment.TickCount64 < deadline)
            {
                await Task.Delay(5);
            }

            Assert.False(Inbound.Snapshots.IsEmpty, "Timed out waiting for a replicated snapshot.");
            Assert.True(Inbound.Snapshots.TryDequeue(out ClientInbound.InboundSnapshot message));
            Assert.False(message.IsBatch);
            return message.Full;
        }
    }

    private sealed class StubRpcHost : IRpcHost
    {
        public PeerSet Peers => default;
    }

    private sealed class StubServerWorldPhysics : IServerWorldPhysics
    {
        private readonly FakePhysics _inner = new();

        public event EventHandler<EventArgs>? FixedUpdate
        {
            add => _inner.FixedUpdate += value;
            remove => _inner.FixedUpdate -= value;
        }

        public float FixedTimestamp => _inner.FixedTimestamp;

        public RaycastResult Raycast(in Ray ray) => _inner.Raycast(in ray);

        public bool Overlaps(in ShapeOverlapQuery query) => _inner.Overlaps(in query);

        public void SetGravity(Vector3 gravity) => _inner.SetGravity(gravity);

        public void Tick(float delta, DataStore store) { }
    }

    private sealed class StubItemDatabase(params Item[] items) : IItemDatabase
    {
        private readonly Dictionary<string, Item> _items = items.ToDictionary(static i => i.ID, StringComparer.Ordinal);

        public Result<Item> Get(string id)
        {
            return _items.TryGetValue(id, out Item? item)
                ? Result<Item>.FromSuccess(item)
                : Result<Item>.FromFailure($"Unknown item \"{id}\".");
        }

        public List<Item> Get(Func<Item, bool> predicate)
        {
            return [.. _items.Values.Where(predicate)];
        }
    }
}
