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
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Blueprints;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Data.Migrations;
using WaywardBeyond.Data.Networking;
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
/// Pins brick functional block-entity TOML metadata, server ECS block-entity lifecycle, blueprint spawn +
/// cargo seeding, viewer-scoped replication isolation, and <c>level.db</c> save/load round-trips.
/// </summary>
[Collection("ClientGameState")]
public sealed class BlockEntityTests
{
    [Fact]
    public void BrickDatabaseLoadsBlockEntityMetadataAndSkipsInvalidEntries()
    {
        BrickDatabase shipped = LoadShippedBricks();

        Brick storage = shipped.Get("wb:storage").Value;
        Assert.True(storage.HasBlockEntity);
        Assert.Equal("station.storage", storage.BlockEntity!.StationTag);
        Assert.True(storage.BlockEntity.Airtight);
        Assert.False(storage.BlockEntity.Toggleable);
        Assert.True(storage.BlockEntity.HasInventory);
        Assert.Single(storage.BlockEntity.SlotGroups);
        Assert.Equal(SlotGroups.CARGO, storage.BlockEntity.SlotGroups[0].GroupId);
        Assert.Null(storage.BlockEntity.SlotGroups[0].Capacity);

        Brick core = shipped.Get("wb:core").Value;
        Assert.True(core.HasBlockEntity);
        Assert.Equal("station.core", core.BlockEntity!.StationTag);
        Assert.Equal(50f, core.BlockEntity.PowerKw);
        Assert.True(core.BlockEntity.Airtight);
        Assert.True(core.BlockEntity.Toggleable);
        Assert.True(core.BlockEntity.DefaultPowerEnabled);
        Assert.Single(core.BlockEntity.SlotGroups);
        Assert.Equal(SlotGroups.FUEL, core.BlockEntity.SlotGroups[0].GroupId);

        Brick console = shipped.Get("wb:display_console").Value;
        Assert.True(console.HasBlockEntity);
        Assert.Equal("station.console", console.BlockEntity!.StationTag);
        Assert.Equal(-1f, console.BlockEntity.PowerKw);
        Assert.True(console.BlockEntity.Toggleable);
        Assert.False(console.BlockEntity.HasInventory);

        Brick panel = shipped.Get("wb:panel").Value;
        Assert.False(panel.HasBlockEntity);
        Assert.Null(panel.BlockEntity);

        //  Custom TOML parsing with explicit slot capacity overrides, default flags, and invalid entries skipped.
        string tempDir = Path.Combine(Path.GetTempPath(), $"wb-be-bricks-{Path.GetRandomFileName()}");
        string bricksDir = Path.Combine(tempDir, "bricks");
        Directory.CreateDirectory(bricksDir);
        try
        {
            File.WriteAllText(
                Path.Combine(bricksDir, "machines.toml"),
                """
                [[Bricks]]
                ID = "wb:test_refinery"
                [Bricks.BlockEntity]
                StationTag = "station.refinery"
                PowerKw = -12.5
                Airtight = true
                Toggleable = true
                DefaultPowerEnabled = false
                DefaultOpen = true
                [[Bricks.BlockEntity.SlotGroups]]
                ID = "wb:input"
                Capacity = 3
                [[Bricks.BlockEntity.SlotGroups]]
                ID = "wb:output"
                Capacity = 2
                [[Bricks.BlockEntity.SlotGroups]]
                ID = ""
                Capacity = 1
                [[Bricks.BlockEntity.SlotGroups]]
                ID = "wb:bad_cap"
                Capacity = -4
                [[Bricks.BlockEntity.SlotGroups]]
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
            Assert.True(refinery.HasBlockEntity);
            Assert.Equal("station.refinery", refinery.BlockEntity!.StationTag);
            Assert.Equal(-12.5f, refinery.BlockEntity.PowerKw);
            Assert.True(refinery.BlockEntity.Airtight);
            Assert.True(refinery.BlockEntity.Toggleable);
            Assert.False(refinery.BlockEntity.DefaultPowerEnabled);
            Assert.True(refinery.BlockEntity.DefaultOpen);
            Assert.Equal(2, refinery.BlockEntity.SlotGroups.Count);
            Assert.Equal(new BrickSlotGroup(SlotGroups.INPUT, 3), refinery.BlockEntity.SlotGroups[0]);
            Assert.Equal(new BrickSlotGroup(SlotGroups.OUTPUT, 2), refinery.BlockEntity.SlotGroups[1]);
        }
        finally
        {
            Directory.Delete(tempDir, recursive: true);
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

        //  Non-functional brick placement allocates no block-entity.
        int panelResult = harness.BlockEntities.OnBrickPlaced(harness.ServerStore, structureUuid, new Int3(0, 0, 0), bricks.Id("wb:panel"));
        Assert.Equal(Entity.Null, panelResult);
        Assert.Equal(0, harness.BlockEntities.Count);

        //  Pre-allocate a colliding entity UUID to verify DeriveUuid avoids collisions safely.
        var storageCoord = new Int3(1, 0, 0);
        Uuid firstCandidate = ServerBlockEntityService.DeriveUuid(structureUuid, storageCoord, harness.ServerStore);
        Assert.NotEqual(Uuid.Null, firstCandidate);
        Assert.NotEqual(structureUuid, firstCandidate);
        int blockerEntity = harness.ServerStore.Alloc(firstCandidate);

        //  Placing a functional wb:storage brick allocates a server ECS block-entity with expected components.
        int storageEntity = harness.BlockEntities.OnBrickPlaced(harness.ServerStore, structureUuid, storageCoord, bricks.Id("wb:storage"));
        Assert.NotEqual(Entity.Null, storageEntity);
        Assert.Equal(1, harness.BlockEntities.Count);

        Uuid storageUuid = harness.ServerStore.GetUuid(storageEntity);
        Assert.NotEqual(Uuid.Null, storageUuid);
        Assert.NotEqual(firstCandidate, storageUuid);
        Assert.NotEqual(structureUuid, storageUuid);
        Assert.NotEqual(breaker.EntityUuid, storageUuid);

        Assert.True(harness.BlockEntities.TryGetEntity(structureUuid, storageCoord, out int lookedUp));
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
        Result<int> seeded = harness.BlockEntities.SeedItems(
            harness.ServerStore,
            storageEntity,
            SlotGroups.CARGO,
            [new ItemData("wb:panel", 12, 100)]);
        Assert.True(seeded.Success);
        Assert.Equal(1, seeded.Value);

        harness.ClearAllDirty();
        Assert.True(harness.BlockEntities.AttachViewer(harness.ServerStore, breaker.ServerPeer.GameId, storageEntity).Success);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot attachSnap = await breaker.WaitForSnapshotAsync();
        Assert.Contains(attachSnap.Components, c => c.Entity == storageUuid.ToValue() && c.TypeUuid == harness.InventoryTypeUuid);
        Assert.Contains(attachSnap.Components, c => c.Entity == storageUuid.ToValue() && c.TypeUuid == harness.MachineStateTypeUuid);
        Assert.Empty(bystander.Inbound.Snapshots);

        //  Breaking the brick in Adventure transfers stored contents to breaker, tears down viewer, and frees block-entity.
        bool broken = harness.BlockEntities.OnBrickBroken(
            harness.ServerStore,
            structureUuid,
            storageCoord,
            breaker.ServerEntity,
            GameMode.Adventure);
        Assert.True(broken);
        Assert.Equal(0, harness.BlockEntities.Count);
        Assert.False(harness.BlockEntities.TryGetEntity(structureUuid, storageCoord, out _));
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
    public void TransferOpWorksOnAnchoredBlockEntitiesEvenWhenStructureMovesAndRotates()
    {
        BrickDatabase bricks = LoadShippedBricks();
        var store = new DataStore();
        var blockEntities = new ServerBlockEntityService(bricks);
        var inventorySystem = new ServerInventorySystem();

        //  Structure translated to (10, 0, 0) and rotated +90 deg around Y:
        //  local voxel (2, 0, 0) transforms to world (10, 0, -2).
        Uuid structureUuid = Uuid.FromValue(77001);
        Quaternion rot90Y = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);
        int structure = store.Alloc(structureUuid);
        store.AddOrUpdate(structure, new TransformComponent(new Vector3(10f, 0f, 0f), rot90Y, Vector3.One));

        int blockEntity = blockEntities.OnBrickPlaced(store, structureUuid, new Int3(2, 0, 0), bricks.Id("wb:storage"));
        Assert.NotEqual(Entity.Null, blockEntity);
        Uuid blockEntityUuid = store.GetUuid(blockEntity);

        //  Player stands at (10, 0, -1) — 1m from rotated block-entity at (10, 0, -2), well within reach.
        Uuid playerUuid = Uuid.FromValue(77002);
        int player = store.Alloc(playerUuid);
        NetworkComponent playerNet = NetworkComponent.Create();
        InventoryComponent playerInv = InventoryComponent.CreatePlayer();
        playerInv.Set(new SlotId(SlotGroups.HOTBAR, 0), new ItemData("wb:rock", 20, 100));
        store.AddOrUpdate(player, playerNet);
        store.AddOrUpdate(player, new TransformComponent(new Vector3(10f, 0f, -1f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(player, playerInv);

        //  Transfer 5 rocks from player hotbar[0] -> block-entity wb:cargo[0].
        playerNet.StagedInventoryOps!.Stage(
            sequenceNumber: 1,
            new TransferOp(
                _FromContainer: 0UL,
                _FromSlot: new SlotId(SlotGroups.HOTBAR, 0),
                _ToContainer: blockEntityUuid.ToValue(),
                _ToSlot: new SlotId(SlotGroups.CARGO, 0),
                _Mode: TransferMode.Exact,
                _Count: 5u));
        inventorySystem.Tick(1f / 60f, store);

        Assert.True(store.TryGet(player, out InventoryComponent playerAfterPush));
        Assert.True(store.TryGet(blockEntity, out InventoryComponent blockAfterPush));
        Assert.Equal(15, playerAfterPush.Get(new SlotId(SlotGroups.HOTBAR, 0)).Count);
        Assert.Equal(5, blockAfterPush.Get(new SlotId(SlotGroups.CARGO, 0)).Count);

        //  Move structure far away; subsequent TransferOp must be rejected as out of reach.
        store.AddOrUpdate(structure, new TransformComponent(new Vector3(100f, 0f, 0f), rot90Y, Vector3.One));
        playerNet.StagedInventoryOps!.Stage(
            sequenceNumber: 2,
            new TransferOp(
                _FromContainer: blockEntityUuid.ToValue(),
                _FromSlot: new SlotId(SlotGroups.CARGO, 0),
                _ToContainer: 0UL,
                _ToSlot: new SlotId(SlotGroups.HOTBAR, 1),
                _Mode: TransferMode.Exact,
                _Count: 5u));
        inventorySystem.Tick(1f / 60f, store);

        Assert.True(store.TryGet(blockEntity, out InventoryComponent blockAfterDenied));
        Assert.Equal(5, blockAfterDenied.Get(new SlotId(SlotGroups.CARGO, 0)).Count);
    }

    [Fact]
    public void ServerInteractionSystemAllocatesAndCleansUpBlockEntitiesOnPlaceAndBreak()
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
        var blockEntities = new ServerBlockEntityService(bricks, sessions: sessions);

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
            blockEntities);

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

        Assert.True(blockEntities.TryGetEntity(structureUuid, new Int3(1, 0, 0), out int spawnedBlockEntity));
        Assert.True(store.TryGet(spawnedBlockEntity, out MachineStateComponent machine));
        Assert.Equal("station.storage", machine.StationTag);

        //  Seed an item into the storage block-entity, switch player to empty slot, and break it.
        Assert.True(blockEntities.SeedItems(store, spawnedBlockEntity, SlotGroups.CARGO, [new ItemData("wb:ice", 6, 100)]).Success);
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

        Assert.Equal(0, blockEntities.Count);
        Assert.Equal(Uuid.Null, store.GetUuid(spawnedBlockEntity));
        Assert.True(store.TryGet(player, out InventoryComponent playerAfterBreak));
        Assert.Equal(6, CountItem(in playerAfterBreak, "wb:ice"));
    }

    [Fact]
    public async Task BlueprintSpawnAndStarterWreckAllocateBlockEntitiesAndAcceptCargoSeeding()
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

            Assert.True(catalog.Create("Adventure Wreck", "seed-block-entity-929", GameMode.Adventure, out string levelGuid));

            var store = new DataStore();
            var blockEntities = new ServerBlockEntityService(bricks);
            using var saveService = new LevelSaveService(
                NullLogger<LevelSaveService>.Instance,
                catalog,
                bricks,
                bricks,
                gameplayConfig,
                paintRegistry: null,
                blockEntities: blockEntities);

            Assert.True(saveService.LoadLevel(levelGuid, store));

            var expectedGen = new LevelGenerator(saveService.CurrentLevel!.Value.Seed, bricks, levelGenConfig, null, bundled);
            Uuid wreckUuid = expectedGen.GenerateStarterWreckUuid();
            int[] wreckBlockEntities = blockEntities.GetEntitiesForStructure(wreckUuid);
            Assert.NotEmpty(wreckBlockEntities);

            //  Verify starter wreck storage container accepts programmatic cargo seeding and persists across reload.
            int cargoEntity = Entity.Null;
            for (var i = 0; i < wreckBlockEntities.Length; i++)
            {
                if (store.TryGet(wreckBlockEntities[i], out InventoryComponent inv) && inv.HasGroup(SlotGroups.CARGO))
                {
                    cargoEntity = wreckBlockEntities[i];
                    break;
                }
            }

            if (cargoEntity == Entity.Null)
            {
                //  Fall back to first inventoried block-entity if the starter wreck uses wb:core fuel slot instead.
                cargoEntity = wreckBlockEntities.First(e => store.TryGet(e, out InventoryComponent _));
            }

            Assert.True(store.TryGet(cargoEntity, out InventoryComponent targetInv));
            string seedGroup = targetInv.Groups[0].GroupId;
            Result<int> seeded = blockEntities.SeedItems(store, cargoEntity, seedGroup, [new ItemData("wb:panel", 7, 100)]);
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
                blockEntities: harness.BlockEntities);

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
            Assert.Equal(2, harness.BlockEntities.GetEntitiesForStructure(pastedUuid).Length);
            Assert.True(harness.BlockEntities.TryFindFirstByStationTag(harness.ServerStore, pastedUuid, "station.storage", out int pastedStorage));
            Assert.True(harness.BlockEntities.TryFindFirstByStationTag(harness.ServerStore, pastedUuid, "station.core", out _));

            //  Seed cargo into the freshly pasted blueprint's storage block-entity.
            Result<int> pasteSeeded = harness.BlockEntities.SeedItems(
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
            Assert.Equal(0, harness.BlockEntities.Count);
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
        int machineEntity = harness.BlockEntities.OnBrickPlaced(harness.ServerStore, structureUuid, new Int3(0, 0, 0), bricks.Id("wb:core"));
        Uuid machineUuid = harness.ServerStore.GetUuid(machineEntity);

        harness.BlockEntities.SeedItems(harness.ServerStore, machineEntity, SlotGroups.FUEL, [new ItemData("wb:ice", 4, 100)]);
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

        //  2. Unsubscribed block-entity mutation produces zero wire snapshots for all clients and clears dirty flags.
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
        Assert.True(harness.BlockEntities.AttachViewer(harness.ServerStore, viewerA.ServerPeer.GameId, machineEntity).Success);
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
        Assert.True(harness.BlockEntities.AttachViewer(harness.ServerStore, viewerB.ServerPeer.GameId, machineEntity).Success);
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

        //  5. Explicit detach of viewer A sends despawn ONLY to A and frees client A's mirrored block-entity.
        Assert.True(harness.BlockEntities.DetachViewer(harness.ServerStore, viewerA.ServerPeer.GameId, machineEntity));
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot detachA = await viewerA.WaitForSnapshotAsync();
        Assert.Contains(detachA.RemovedEntities, id => id == machineUuid.ToValue());
        Assert.Empty(viewerB.Inbound.Snapshots);
        Assert.Empty(nonViewer.Inbound.Snapshots);

        viewerA.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(detachA));
        TickReconcileWhilePlaying(reconcileA, clientAStore);
        Assert.False(clientAStore.TryGet(machineUuid, out _));

        //  6. Moving viewer B out of reach causes ServerBlockEntitySystem to detach B and despawn on B's client.
        var blockEntitySystem = new ServerBlockEntitySystem(harness.BlockEntities);
        harness.ServerStore.AddOrUpdate(viewerB.ServerEntity, new TransformComponent(new Vector3(0f, 0f, 50f), Quaternion.Identity, Vector3.One));
        harness.ClearAllDirty();

        blockEntitySystem.Tick(1f / 60f, harness.ServerStore);
        Assert.False(harness.Replication.IsViewerSubscribed(machineEntity, viewerB.ServerPeer.GameId));
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot outOfReachB = await viewerB.WaitForSnapshotAsync();
        Assert.Contains(outOfReachB.RemovedEntities, id => id == machineUuid.ToValue());
        Assert.Empty(viewerA.Inbound.Snapshots);
        Assert.Empty(nonViewer.Inbound.Snapshots);

        //  7. Client disconnect teardown clears active viewer subscriptions without leaks.
        Assert.True(harness.BlockEntities.AttachViewer(harness.ServerStore, viewerA.ServerPeer.GameId, machineEntity).Success);
        Assert.Equal(1, harness.Replication.ActiveViewerSubscriptionCount);
        harness.Replication.CancelStream(viewerA.ServerPeer.GameId);
        Assert.Equal(0, harness.Replication.ActiveViewerSubscriptionCount);

        //  8. Recycled entity index safety: if an entity slot is freed and immediately reused by a different
        //     entity, DetachOutOfReachViewers despawns the originally delivered UUID and never the recycled entity.
        Assert.True(harness.BlockEntities.AttachViewer(harness.ServerStore, viewerA.ServerPeer.GameId, machineEntity).Success);
        harness.Replication.PublishStage(1f, harness.ServerStore);
        _ = await viewerA.WaitForSnapshotAsync();

        harness.ServerStore.Free(machineEntity);
        Uuid recycledUuid = Uuid.FromValue(95999);
        int recycledEntity = harness.ServerStore.Alloc(recycledUuid);
        Assert.Equal(machineEntity, recycledEntity);

        blockEntitySystem.Tick(1f / 60f, harness.ServerStore);
        harness.Replication.PublishStage(1f, harness.ServerStore);
        WorldSnapshot recycledDetach = await viewerA.WaitForSnapshotAsync();
        Assert.Contains(recycledDetach.RemovedEntities, id => id == machineUuid.ToValue());
        Assert.DoesNotContain(recycledDetach.RemovedEntities, id => id == recycledUuid.ToValue());
    }

    [Fact]
    public void LevelDbSaveLoadRoundTripsBlockEntitiesAndPrunesOrphans()
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
            const string levelGuid = "block-entity-roundtrip-level";

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
            var blockEntities1 = new ServerBlockEntityService(bricks);
            using (var saveService1 = new LevelSaveService(
                NullLogger<LevelSaveService>.Instance,
                catalog,
                bricks,
                bricks,
                gameplayConfig,
                paintRegistry: null,
                blockEntities: blockEntities1))
            {
                Assert.True(saveService1.LoadLevel(levelGuid, store1));
                Assert.Equal(2, blockEntities1.Count);

                Assert.True(blockEntities1.TryGetEntity(structureUuid, new Int3(0, 0, 0), out int storage1));
                Assert.True(blockEntities1.TryGetEntity(structureUuid, new Int3(1, 0, 0), out int core1));

                Assert.True(blockEntities1.SeedSlots(
                    store1,
                    storage1,
                    [
                        new KeyedSlotEntry(SlotGroups.CARGO, 0, new ItemData("wb:panel", 18, 100)),
                        new KeyedSlotEntry(SlotGroups.CARGO, 5, new ItemData("wb:rock", 33, 100)),
                    ]).Success);

                Assert.True(blockEntities1.SeedSlots(
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
                Assert.Equal(0, blockEntities1.Count);
            }

            //  Session 2: reload into a brand-new DataStore and verify full round-trip fidelity.
            var store2 = new DataStore();
            var blockEntities2 = new ServerBlockEntityService(bricks);
            using (var saveService2 = new LevelSaveService(
                NullLogger<LevelSaveService>.Instance,
                catalog,
                bricks,
                bricks,
                gameplayConfig,
                paintRegistry: null,
                blockEntities: blockEntities2))
            {
                Assert.True(saveService2.LoadLevel(levelGuid, store2));
                Assert.Equal(2, blockEntities2.Count);

                Assert.True(blockEntities2.TryGetEntity(structureUuid, new Int3(0, 0, 0), out int storage2));
                Assert.True(blockEntities2.TryGetEntity(structureUuid, new Int3(1, 0, 0), out int core2));

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

            //  Session 3: inject orphaned block-entity records (missing structure + non-matching brick coord)
            //  and verify LoadLevel ignores them AND deletes them from level.db.
            using (var orphanInject = new SqliteLevelStorage(NullLogger<SqliteLevelStorage>.Instance, storagePaths, levelGuid, migrator))
            {
                IReadOnlyList<BlockEntityData> existing = orphanInject.GetBlockEntities();
                Assert.Equal(2, existing.Count);

                var withOrphans = new List<BlockEntityData>(existing)
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
                Assert.Equal(4, orphanInject.GetBlockEntities().Count);
            }

            var store3 = new DataStore();
            var blockEntities3 = new ServerBlockEntityService(bricks);
            using (var saveService3 = new LevelSaveService(
                NullLogger<LevelSaveService>.Instance,
                catalog,
                bricks,
                bricks,
                gameplayConfig,
                paintRegistry: null,
                blockEntities: blockEntities3))
            {
                Assert.True(saveService3.LoadLevel(levelGuid, store3));
                Assert.Equal(2, blockEntities3.Count);
                Assert.False(blockEntities3.TryGetEntity(Uuid.FromValue(999999UL), new Int3(0, 0, 0), out _));
                Assert.False(blockEntities3.TryGetEntity(structureUuid, new Int3(2, 0, 0), out _));
                saveService3.Unload(store3);
            }

            using (var verifyPruned = new SqliteLevelStorage(NullLogger<SqliteLevelStorage>.Instance, storagePaths, levelGuid, migrator))
            {
                IReadOnlyList<BlockEntityData> remaining = verifyPruned.GetBlockEntities();
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
        public ServerBlockEntityService BlockEntities { get; }
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
            BlockEntities = new ServerBlockEntityService(
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
