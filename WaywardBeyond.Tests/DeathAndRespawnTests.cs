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
using WaywardBeyond.Data.Buffs;
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
using WaywardBeyond.Gameplay.Statistics;
using WaywardBeyond.Gameplay.Survival;
using WaywardBeyond.Items;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Particles;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Skills;
using WaywardBeyond.Voxels;
using Xunit;
using GameState = WaywardBeyond.Client.GameState;
using WaywardBeyondClient = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>
/// Pins Adventure Mode death transitions, movement/interaction/survival freeze, buff clearing,
/// Death-Container blueprint drops (inventory dropped, wearables/augments kept), safe geometry nudging,
/// auto-despawn across server/level.db/clients on last item looted, Respawn RPC authority, and
/// disconnect-while-dead anti-duplication safety.
/// </summary>
[Collection("ClientGameState")]
public sealed class DeathAndRespawnTests
{
    private const string DEATH_CONTAINER_ID = "builtin/death_container";
    private const string LEVEL_GUID = "death-respawn-test-level";

    [Fact]
    public void BundledDeathContainerBlueprintLoadsHeadlesslyFromServerAssets()
    {
        string serverAssets = GetServerAssetsRoot();
        string blueprintFile = Path.Combine(serverAssets, "blueprints", "builtin", "death_container.wbp");
        Assert.True(File.Exists(blueprintFile), $"Expected bundled death container blueprint at {blueprintFile}");

        IBundledBlueprintDatabase bundled = CreateServerBundledBlueprintDatabase();
        Assert.Contains(bundled.ListSummaries(), s => s.Id == DEATH_CONTAINER_ID && s.BrickCount == 1);

        Result<BlueprintData> loaded = bundled.Get(DEATH_CONTAINER_ID);
        Assert.True(loaded.Success, loaded.Message);

        BlueprintData raw = loaded.Value;
        Assert.Equal(1, raw.SizeX);
        Assert.Equal(1, raw.SizeY);
        Assert.Equal(1, raw.SizeZ);
        Assert.Equal(1, raw.BrickCount);
        Assert.NotNull(raw.BrickPalette);
        Assert.Contains("wb:storage", raw.BrickPalette!);

        BrickDatabase bricks = LoadShippedBricks();
        VoxelRegionData encoded = raw.ToRegion();
        VoxelRegionData decoded = VoxelCodec.Decode(in encoded, bricks);
        VoxelObject centered = VoxelRegionApplier.ToCenteredVoxelObject(in decoded, out _);

        Voxel cell = centered.Get(0, 0, 0);
        Assert.Equal(bricks.Id("wb:storage"), cell.ID);
    }

    [Fact]
    public async Task AdventureDeathFreezesMotionHaltsSurvivalClearsBuffsAndBlocksActions()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using var harness = new DeathWorldHarness(bricks);
        LoopbackClient player = await harness.AddPlayerClientAsync(
            Uuid.FromValue(7101),
            new Vector3(12.5f, -4.25f, 33.75f),
            GameMode.Adventure,
            inventory: []);

        //  Attach viewer to a nearby container so we verify death detaches viewers.
        Uuid chestStructure = Uuid.FromValue(81001);
        harness.CreateStructure(chestStructure, new Vector3(13f, -4.25f, 33.75f), Quaternion.Identity, (new Int3(0, 0, 0), bricks.Id("wb:storage")));
        int chestEntity = harness.BrickEntities.OnBrickPlaced(harness.Store, chestStructure, new Int3(0, 0, 0), bricks.Id("wb:storage"));
        Assert.True(harness.BrickEntities.AttachViewer(harness.Store, player.ServerPeer.GameId, chestEntity).Success);
        Assert.True(harness.Replication.IsViewerSubscribed(chestEntity, player.ServerPeer.GameId));

        //  Seed active buff, non-zero velocity/torque, laser beam, and low HP at 0 oxygen.
        harness.Store.SetStatisticFixed(player.ServerEntity, StatisticIds.HEALTH, 2f);
        harness.Store.SetStatisticFixed(player.ServerEntity, StatisticIds.OXYGEN, 0f);
        harness.Store.SetBuffExpiresTick(player.ServerEntity, "wb:well_fed", 5000L);
        harness.Store.AddOrUpdate(player.ServerEntity, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous)
        {
            Velocity = new Vector3(5f, -2f, 3f),
            Torque = new Vector3(1f, 2f, -1f),
        });
        harness.Store.AddOrUpdate(player.ServerEntity, new LaserBeamComponent { Hit = true, TargetZ = -2f });

        //  Suffocation drops HP to 0 and ServerDeathSystem transitions the player on the same tick.
        harness.TickSurvivalAndDeath(1f);

        Assert.True(harness.Store.TryGet(player.ServerEntity, out StatisticsComponent stats));
        Assert.True(stats.IsDead);
        Assert.Equal(0f, stats.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.Equal((long)DamageSource.Suffocation, stats.GetValue(StatisticIds.DEATH_CAUSE));
        Assert.Equal(12.5f, stats.GetFixed(StatisticIds.DEATH_X), precision: 3);
        Assert.Equal(-4.25f, stats.GetFixed(StatisticIds.DEATH_Y), precision: 3);
        Assert.Equal(33.75f, stats.GetFixed(StatisticIds.DEATH_Z), precision: 3);
        Assert.False(stats.GetFlag(StatisticIds.DEATH_HAS_CONTAINER));
        Assert.False(stats.TryGetBuffExpiresTick("wb:well_fed", out _));

        //  Motion frozen and active viewer detached.
        Assert.True(harness.Store.TryGet(player.ServerEntity, out PhysicsComponent physicsAfterDeath));
        Assert.Equal(Vector3.Zero, physicsAfterDeath.Velocity);
        Assert.Equal(Vector3.Zero, physicsAfterDeath.Torque);
        Assert.False(harness.Replication.IsViewerSubscribed(chestEntity, player.ServerPeer.GameId));
        Assert.False(harness.BrickEntities.AttachViewer(harness.Store, player.ServerPeer.GameId, chestEntity).Success);

        //  Movement input while dead is ignored by SimulationStep.
        Assert.True(harness.Store.TryGet(player.ServerEntity, out NetworkComponent net));
        var moveInput = new InputComponent
        {
            MovementX = 1f,
            MovementY = 1f,
            MovementZ = -1f,
            LookPitch = 1.5f,
            LookYaw = -2.0f,
            PrimaryHeld = true,
            SequenceNumber = 1,
            ServerTickAtSample = 1,
        };
        net.StagedInputs!.Stage(in moveInput, harness.SimulationStep.CurrentSimTick + 1);
        harness.Physics.Step();
        Assert.True(harness.Store.TryGet(player.ServerEntity, out PhysicsComponent physicsAfterInput));
        Assert.Equal(Vector3.Zero, physicsAfterInput.Velocity);
        Assert.Equal(Vector3.Zero, physicsAfterInput.Torque);

        //  Survival drain/refill is halted while dead (even in a pressurized room).
        harness.Store.SetStatisticFlag(player.ServerEntity, StatisticIds.PRESSURIZED, true);
        harness.TickSurvivalAndDeath(2f);
        Assert.True(harness.Store.TryGet(player.ServerEntity, out StatisticsComponent statsAfterSurvival));
        Assert.Equal(0f, statsAfterSurvival.GetFixed(StatisticIds.OXYGEN), precision: 3);

        //  Laser beam is removed while dead.
        harness.LaserBeamSystem.Tick(harness.Store, harness.SimulationStep.CurrentSimTick);
        Assert.False(harness.Store.Has(typeof(LaserBeamComponent), player.ServerEntity));

        //  Staged interaction and inventory ops are discarded while dead and their sequences advance.
        net.StagedInteractions!.Stage(new InteractionEvent
        {
            Entity = player.EntityUuid.ToValue(),
            SequenceNumber = 7,
            ServerTickAtSample = harness.SimulationStep.CurrentSimTick,
            Kind = InteractionKind.PrimaryPressed,
        });
        net.StagedInventoryOps!.Stage(9, new TransferOp(
            0UL,
            new SlotId(SlotGroups.HOTBAR, 0),
            0UL,
            new SlotId(SlotGroups.BACKPACK, 0),
            TransferMode.Exact,
            null));

        harness.InventorySystem.Tick(1f / 60f, harness.Store);
        harness.InteractionSystem.Tick(1f / 60f, harness.Store, harness.SimulationStep.CurrentSimTick);

        Assert.True(harness.Store.TryGet(player.ServerEntity, out NetworkComponent netAfter));
        Assert.Equal(9u, netAfter.LastConsumedInventorySequence);
        Assert.Equal(7u, netAfter.LastConsumedInteractionSequence);
        Assert.Equal(0, harness.CountDeathContainers());
    }

    [Fact]
    public async Task AdventureDeathMovesHotbarAndBackpackIntoDeathContainerAndKeepsWearablesAndAugments()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using var harness = new DeathWorldHarness(bricks);

        //  Build an inventory with 34 stacks across hotbar (9) + backpack (25) — exceeding default 27 cargo slots —
        //  plus equipped wearables and augments that must stay on the colonist across death.
        var entries = new List<KeyedSlotEntry>();
        for (var i = 0; i < 9; i++)
        {
            entries.Add(new KeyedSlotEntry(SlotGroups.HOTBAR, i, new ItemData("wb:rock", i + 1, 100)));
        }

        for (var i = 0; i < 25; i++)
        {
            entries.Add(new KeyedSlotEntry(SlotGroups.BACKPACK, i, new ItemData("wb:panel", i + 1, 100)));
        }

        entries.Add(new KeyedSlotEntry(SlotGroups.WEARABLES, 0, new ItemData("wb:eva_helmet", 1, 1)));
        entries.Add(new KeyedSlotEntry(SlotGroups.WEARABLES, 4, new ItemData("wb:eva_suit", 1, 1)));
        entries.Add(new KeyedSlotEntry(SlotGroups.AUGMENTS, 0, new ItemData("wb:o2_augment", 1, 1)));

        var deathPos = new Vector3(40f, 15f, -22f);
        LoopbackClient player = await harness.AddPlayerClientAsync(
            Uuid.FromValue(7102),
            deathPos,
            GameMode.Adventure,
            inventory: [.. entries]);

        DamageResult fatal = DamageService.ApplyDamage(harness.Store, player.ServerEntity, 200f, DamageSource.Kinetic, simTick: 42L);
        Assert.True(fatal.Fatal);

        harness.DeathSystem.Tick(1f / 60f, harness.Store);

        //  Player stats mark dead with kinetic cause and container flag.
        Assert.True(harness.Store.TryGet(player.ServerEntity, out StatisticsComponent stats));
        Assert.True(stats.IsDead);
        Assert.Equal((long)DamageSource.Kinetic, stats.GetValue(StatisticIds.DEATH_CAUSE));
        Assert.True(stats.GetFlag(StatisticIds.DEATH_HAS_CONTAINER));
        Assert.Equal(deathPos.X, stats.GetFixed(StatisticIds.DEATH_X), precision: 3);
        Assert.Equal(deathPos.Y, stats.GetFixed(StatisticIds.DEATH_Y), precision: 3);
        Assert.Equal(deathPos.Z, stats.GetFixed(StatisticIds.DEATH_Z), precision: 3);

        //  Player hotbar and backpack are completely empty; wearables and augments are intact.
        Assert.True(harness.Store.TryGet(player.ServerEntity, out InventoryComponent postDeathInv));
        KeyedSlotEntry[] remaining = postDeathInv.ToSparseEntries();
        Assert.DoesNotContain(remaining, e => e.GroupId == SlotGroups.HOTBAR);
        Assert.DoesNotContain(remaining, e => e.GroupId == SlotGroups.BACKPACK);
        Assert.Equal("wb:eva_helmet", postDeathInv.Get(new SlotId(SlotGroups.WEARABLES, 0)).ID);
        Assert.Equal("wb:eva_suit", postDeathInv.Get(new SlotId(SlotGroups.WEARABLES, 4)).ID);
        Assert.Equal("wb:o2_augment", postDeathInv.Get(new SlotId(SlotGroups.AUGMENTS, 0)).ID);

        //  Exactly one Death-Container spawned with expanded cargo capacity holding all 34 stacks.
        (Uuid structureUuid, int brickEntity) = harness.GetSingleDeathContainer();
        Assert.True(harness.Store.TryGet(structureUuid, out int structureEntity));
        Assert.True(harness.Store.TryGet(structureEntity, out TransformComponent containerTransform));
        Assert.Equal(deathPos, containerTransform.Position);

        Assert.True(harness.Store.TryGet(brickEntity, out MachineStateComponent machine));
        Assert.Equal(MachineStateComponent.DEATH_CONTAINER_STATION_TAG, machine.StationTag);
        Assert.True(machine.HasStatusFlag(MachineStatusFlags.AutoDespawnWhenEmpty));

        Assert.True(harness.Store.TryGet(brickEntity, out InventoryComponent containerInv));
        Assert.True(containerInv.GetGroupCapacity(SlotGroups.CARGO) >= 34);
        Assert.Equal(34, containerInv.ToSparseEntries().Length);
        Assert.Equal(45, CountItem(in containerInv, "wb:rock"));
        Assert.Equal(325, CountItem(in containerInv, "wb:panel"));

        //  Connected client received VoxelEntitySpawn notification for the Death-Container.
        VoxelEntitySpawnPart spawnPart = await player.WaitForEntitySpawnPartAsync();
        Assert.Equal(1u, spawnPart.PartCount);
        VoxelEntityData spawnedWire = VoxelEntityData.Deserialize(spawnPart.Payload);
        Assert.Equal(structureUuid.ToValue(), spawnedWire.Uuid);
    }

    [Fact]
    public async Task DeathContainerNudgesSafelyAwayFromOverlappingSolidGeometry()
    {
        BrickDatabase bricks = LoadShippedBricks();
        var deathPos = new Vector3(10f, 5f, -8f);
        var physics = new OverlapControlledPhysics(pos => Vector3.DistanceSquared(pos, deathPos) < 0.25f);
        using var harness = new DeathWorldHarness(bricks, physicsOverride: physics);

        LoopbackClient player = await harness.AddPlayerClientAsync(
            Uuid.FromValue(7103),
            deathPos,
            GameMode.Adventure,
            inventory: [new KeyedSlotEntry(SlotGroups.HOTBAR, 0, new ItemData("wb:rock", 10, 100))]);

        DamageService.ApplyDamage(harness.Store, player.ServerEntity, 100f, DamageSource.Kinetic, simTick: 10L);
        harness.DeathSystem.Tick(1f / 60f, harness.Store);

        (Uuid structureUuid, _) = harness.GetSingleDeathContainer();
        Assert.True(harness.Store.TryGet(structureUuid, out int structureEntity));
        Assert.True(harness.Store.TryGet(structureEntity, out TransformComponent containerTransform));
        Assert.NotEqual(deathPos, containerTransform.Position);
        Assert.Equal(deathPos + Vector3.UnitY, containerTransform.Position);

        Assert.True(harness.Store.TryGet(player.ServerEntity, out StatisticsComponent stats));
        Assert.Equal(containerTransform.Position.X, stats.GetFixed(StatisticIds.DEATH_X), precision: 3);
        Assert.Equal(containerTransform.Position.Y, stats.GetFixed(StatisticIds.DEATH_Y), precision: 3);
        Assert.Equal(containerTransform.Position.Z, stats.GetFixed(StatisticIds.DEATH_Z), precision: 3);
    }

    [Fact]
    public async Task LootingLastItemAutoDespawnsDeathContainerAcrossServerLevelDbAndClients()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using var harness = new DeathWorldHarness(bricks, useSqliteStorage: true);

        var deathPos = new Vector3(2f, 1f, 5f);
        LoopbackClient looter = await harness.AddPlayerClientAsync(
            Uuid.FromValue(7104),
            deathPos,
            GameMode.Adventure,
            inventory:
            [
                new KeyedSlotEntry(SlotGroups.HOTBAR, 0, new ItemData("wb:rock", 12, 100)),
                new KeyedSlotEntry(SlotGroups.BACKPACK, 0, new ItemData("wb:panel", 4, 100)),
            ]);
        LoopbackClient witness = await harness.AddPlayerClientAsync(
            Uuid.FromValue(7105),
            new Vector3(20f, 1f, 5f),
            GameMode.Adventure,
            inventory: []);

        DamageService.ApplyDamage(harness.Store, looter.ServerEntity, 100f, DamageSource.Suffocation, simTick: 5L);
        harness.DeathSystem.Tick(1f / 60f, harness.Store);

        (Uuid structureUuid, int brickEntity) = harness.GetSingleDeathContainer();
        Uuid brickUuid = harness.Store.GetUuid(brickEntity);

        //  Flush pending saves and verify Death-Container exists in level.db before looting.
        harness.SaveService.Flush(harness.Store);
        using (SqliteLevelStorage dbBefore = harness.OpenSqliteStorage())
        {
            Assert.Contains(dbBefore.GetVoxelEntities(DataVersion.CURRENT), e => e.Uuid == structureUuid.ToValue());
            Assert.Contains(dbBefore.GetBrickEntities(), b => b.VoxelEntityUuid == structureUuid.ToValue());
        }

        //  Respawn looter, move within reach of the Death-Container, and attach viewer.
        Assert.True(harness.DeathService.RespawnPlayer(harness.Store, looter.ServerEntity).Success);
        harness.Store.AddOrUpdate(looter.ServerEntity, new TransformComponent(deathPos, Quaternion.Identity, CharacterDefaults.PlayerScale));
        Assert.True(harness.BrickEntities.AttachViewer(harness.Store, looter.ServerPeer.GameId, brickEntity).Success);

        //  Publish viewer attach so looter has delivered brick-entity state.
        harness.Replication.PublishStage(1f, harness.Store);
        _ = await looter.WaitForSnapshotAsync();
        witness.DrainSnapshots();

        //  Loot first item -> container stays alive because 1 stack remains.
        Assert.True(harness.Store.TryGet(looter.ServerEntity, out NetworkComponent net));
        Assert.NotNull(net.StagedInventoryOps);
        net.StagedInventoryOps.Stage(1, new TransferOp(
            brickUuid.ToValue(),
            new SlotId(SlotGroups.CARGO, 0),
            0UL,
            new SlotId(SlotGroups.HOTBAR, 0),
            TransferMode.Exact,
            null));
        harness.InventorySystem.Tick(1f / 60f, harness.Store);

        Assert.True(harness.Store.TryGet(structureUuid, out _));
        Assert.Equal(1, harness.BrickEntities.Count);

        //  Loot final item -> Death-Container auto-despawns across server, level.db, and connected clients!
        net.StagedInventoryOps.Stage(2, new TransferOp(
            brickUuid.ToValue(),
            new SlotId(SlotGroups.CARGO, 1),
            0UL,
            new SlotId(SlotGroups.BACKPACK, 0),
            TransferMode.Exact,
            null));
        harness.InventorySystem.Tick(1f / 60f, harness.Store);

        Assert.False(harness.Store.TryGet(structureUuid, out _));
        Assert.False(harness.Store.TryGet(brickUuid, out _));
        Assert.Equal(0, harness.BrickEntities.Count);

        //  Player received both looted items.
        Assert.True(harness.Store.TryGet(looter.ServerEntity, out InventoryComponent looterInv));
        Assert.Equal(12, CountItem(in looterInv, "wb:rock"));
        Assert.Equal(4, CountItem(in looterInv, "wb:panel"));

        //  Connected clients receive despawn snapshots (viewer gets structure + brick-entity; witness gets structure).
        harness.Replication.PublishStage(1f, harness.Store);
        WorldSnapshot looterDespawn = await looter.WaitForSnapshotAsync();
        WorldSnapshot witnessDespawn = await witness.WaitForSnapshotAsync();

        Assert.Contains(structureUuid.ToValue(), looterDespawn.RemovedEntities);
        Assert.Contains(brickUuid.ToValue(), looterDespawn.RemovedEntities);
        Assert.Contains(structureUuid.ToValue(), witnessDespawn.RemovedEntities);
        Assert.DoesNotContain(brickUuid.ToValue(), witnessDespawn.RemovedEntities);

        //  Verify level.db no longer contains the despawned structure or brick-entity (even before full save).
        harness.SaveService.Dispose();
        using SqliteLevelStorage dbAfter = harness.OpenSqliteStorage();
        Assert.DoesNotContain(dbAfter.GetVoxelEntities(DataVersion.CURRENT), e => e.Uuid == structureUuid.ToValue());
        Assert.DoesNotContain(dbAfter.GetBrickEntities(), b => b.VoxelEntityUuid == structureUuid.ToValue());
    }

    [Fact]
    public async Task DeathContainerSurvivesLevelSaveAndReloadWithExpandedCapacityAndAutoDespawn()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using var harness = new DeathWorldHarness(bricks, useSqliteStorage: true);

        var entries = new List<KeyedSlotEntry>();
        for (var i = 0; i < 32; i++)
        {
            string group = i < 9 ? SlotGroups.HOTBAR : SlotGroups.BACKPACK;
            int localIndex = i < 9 ? i : i - 9;
            entries.Add(new KeyedSlotEntry(group, localIndex, new ItemData("wb:rock", 2, 100)));
        }

        LoopbackClient player = await harness.AddPlayerClientAsync(
            Uuid.FromValue(7106),
            new Vector3(5f, 2f, -3f),
            GameMode.Adventure,
            inventory: [.. entries]);

        DamageService.ApplyDamage(harness.Store, player.ServerEntity, 100f, DamageSource.Kinetic, simTick: 20L);
        harness.DeathSystem.Tick(1f / 60f, harness.Store);

        (Uuid structureUuid, _) = harness.GetSingleDeathContainer();
        harness.SaveService.Flush(harness.Store);
        harness.SaveService.Unload(harness.Store);

        //  Reload into a brand-new DataStore and ServerBrickEntityService.
        var reloadedStore = new DataStore();
        var reloadedBrickEntities = new ServerBrickEntityService(bricks);
        using var reloadedSaveService = harness.CreateSeparateSaveService(reloadedBrickEntities);
        Assert.True(reloadedSaveService.LoadLevel(LEVEL_GUID, reloadedStore));

        Assert.True(reloadedBrickEntities.TryGetEntity(structureUuid, new Int3(0, 0, 0), out int restoredBrickEntity));
        Assert.True(reloadedStore.TryGet(restoredBrickEntity, out MachineStateComponent restoredMachine));
        Assert.Equal(MachineStateComponent.DEATH_CONTAINER_STATION_TAG, restoredMachine.StationTag);
        Assert.True(restoredMachine.HasStatusFlag(MachineStatusFlags.AutoDespawnWhenEmpty));

        Assert.True(reloadedStore.TryGet(restoredBrickEntity, out InventoryComponent restoredInv));
        Assert.True(restoredInv.GetGroupCapacity(SlotGroups.CARGO) >= 32);
        Assert.Empty(restoredInv.Orphans);
        Assert.Equal(32, restoredInv.ToSparseEntries().Length);

        //  Emptying the reloaded Death-Container still triggers auto-despawn sweep.
        InventoryComponent emptied = restoredInv.Clone();
        emptied.LoadSparse([]);
        reloadedStore.AddOrUpdate(restoredBrickEntity, emptied);

        var despawned = new List<Uuid>();
        reloadedBrickEntities.SweepEmptyAutoDespawnContainers(reloadedStore, despawned);
        Assert.Single(despawned, structureUuid);
        Assert.False(reloadedStore.TryGet(structureUuid, out _));
        Assert.Equal(0, reloadedBrickEntities.Count);

        reloadedSaveService.Unload(reloadedStore);
    }

    [Fact]
    public async Task RespawnCommandTeleportsDeadPlayerResetsVitalsReconcilesClientAndRejectsAlivePlayer()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using var harness = new DeathWorldHarness(bricks, levelSpawn: new Vector3(100f, 25f, -50f));

        var startPos = new Vector3(100.5f, 25f, -50f);
        Quaternion tilted = Quaternion.Normalize(Quaternion.CreateFromYawPitchRoll(1.2f, 0.5f, -0.4f));
        LoopbackClient player = await harness.AddPlayerClientAsync(
            Uuid.FromValue(7107),
            startPos,
            GameMode.Adventure,
            inventory: []);
        harness.Store.AddOrUpdate(player.ServerEntity, new TransformComponent(startPos, tilted, CharacterDefaults.PlayerScale));
        harness.Store.SetStatisticFixed(player.ServerEntity, StatisticIds.OXYGEN, 15f);

        //  Sending Respawn while alive is rejected without changing state.
        RpcResult<RespawnResponse> aliveRespawn = await harness.SendRespawnAsync(player);
        Assert.True(aliveRespawn.IsSuccess, aliveRespawn.ErrorMessage);
        Assert.False(aliveRespawn.Value.Success);
        Assert.True(harness.Store.TryGet(player.ServerEntity, out TransformComponent aliveTransform));
        Assert.Equal(startPos, aliveTransform.Position);
        Assert.Equal(15f, harness.Store.TryGet(player.ServerEntity, out StatisticsComponent aliveStats) ? aliveStats.GetFixed(StatisticIds.OXYGEN) : -1f, precision: 3);

        //  Kill the player and sync dead snapshot to a client reconcile store.
        DamageService.ApplyDamage(harness.Store, player.ServerEntity, 100f, DamageSource.Kinetic, simTick: 88L);
        harness.DeathSystem.Tick(1f / 60f, harness.Store);

        //  Apply non-zero residual physics to confirm respawn explicitly zeroes it.
        harness.Store.AddOrUpdate(player.ServerEntity, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous)
        {
            Velocity = new Vector3(7f, 0f, -3f),
            Torque = new Vector3(0.5f, -0.5f, 0.25f),
        });

        var clientStore = new DataStore();
        int clientPlayer = clientStore.Alloc(player.EntityUuid);
        clientStore.AddOrUpdate(clientPlayer, new PlayerComponent());
        clientStore.AddOrUpdate(clientPlayer, new TransformComponent(startPos, tilted, CharacterDefaults.PlayerScale));
        clientStore.AddOrUpdate(clientPlayer, CharacterDefaults.CreateCharacterPhysicsComponent());
        clientStore.AddOrUpdate(clientPlayer, new InputComponent());
        ClientReconcileSystem clientReconcile = CreateClientReconcile(player.Inbound, harness.Registry);

        harness.Replication.RequestFullSync(player.ServerPeer.GameId);
        harness.Replication.PublishStage(1f, harness.Store);
        WorldSnapshot deadSnapshot = await player.WaitForSnapshotAsync();
        player.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(deadSnapshot));
        TickReconcileWhilePlaying(clientReconcile, clientStore);

        Assert.True(clientStore.TryGet(clientPlayer, out StatisticsComponent clientDeadStats));
        Assert.True(clientDeadStats.IsDead);

        //  Send Respawn while dead -> succeeds, teleports to LevelSpawn with upright orientation and zero velocity, restores HP/O2.
        RpcResult<RespawnResponse> respawn = await harness.SendRespawnAsync(player);
        Assert.True(respawn.IsSuccess, respawn.ErrorMessage);
        Assert.True(respawn.Value.Success);

        Assert.True(harness.Store.TryGet(player.ServerEntity, out TransformComponent respawnedTransform));
        Assert.Equal(new Vector3(100f, 25f, -50f), respawnedTransform.Position);
        Assert.Equal(Quaternion.Identity, respawnedTransform.Orientation);

        Assert.True(harness.Store.TryGet(player.ServerEntity, out PhysicsComponent respawnedPhysics));
        Assert.Equal(Vector3.Zero, respawnedPhysics.Velocity);
        Assert.Equal(Vector3.Zero, respawnedPhysics.Torque);

        Assert.True(harness.Store.TryGet(player.ServerEntity, out StatisticsComponent respawnedStats));
        Assert.False(respawnedStats.IsDead);
        Assert.Equal(StatisticIds.DEFAULT_HEALTH_MAX, respawnedStats.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.Equal(StatisticIds.DEFAULT_OXYGEN_MAX, respawnedStats.GetFixed(StatisticIds.OXYGEN), precision: 3);
        Assert.False(respawnedStats.TryGetValue(StatisticIds.DEATH_CAUSE, out _));
        Assert.False(respawnedStats.TryGetValue(StatisticIds.DEATH_X, out _));
        Assert.False(respawnedStats.TryGetValue(StatisticIds.DEATH_HAS_CONTAINER, out _));

        Assert.True(harness.Store.TryGet(player.ServerEntity, out DamageStateComponent damageAfterRespawn));
        Assert.Equal(DamageSource.None, damageAfterRespawn.DeathCause);
        Assert.Equal(0L, damageAfterRespawn.DeathTick);

        //  Client reconciliation snaps directly to LevelSpawn + upright orientation and clears dead state.
        harness.Replication.PublishStage(1f, harness.Store);
        WorldSnapshot revivedSnapshot = await player.WaitForSnapshotAsync();
        player.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(revivedSnapshot));
        TickReconcileWhilePlaying(clientReconcile, clientStore);

        Assert.True(clientStore.TryGet(clientPlayer, out StatisticsComponent clientRevivedStats));
        Assert.False(clientRevivedStats.IsDead);
        Assert.True(clientStore.TryGet(clientPlayer, out TransformComponent clientRevivedTransform));
        Assert.Equal(new Vector3(100f, 25f, -50f), clientRevivedTransform.Position);
        Assert.Equal(Quaternion.Identity, clientRevivedTransform.Orientation);

        //  Immediate duplicate Respawn while now alive is rejected.
        RpcResult<RespawnResponse> secondRespawn = await harness.SendRespawnAsync(player);
        Assert.True(secondRespawn.IsSuccess);
        Assert.False(secondRespawn.Value.Success);
    }

    [Fact]
    public async Task DisconnectWhileDeadPreservesDeadStateAndPreventsItemDuplicationOnReconnect()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using var harness = new DeathWorldHarness(bricks, useSqliteStorage: true);

        const ulong characterUuid = 9901UL;
        KeyedSlotEntry[] preDeathInventory =
        [
            new(SlotGroups.HOTBAR, 0, new ItemData("laser", 1, 1)),
            new(SlotGroups.HOTBAR, 1, new ItemData("wb:rock", 50, 100)),
            new(SlotGroups.BACKPACK, 0, new ItemData("wb:panel", 20, 100)),
            new(SlotGroups.WEARABLES, 0, new ItemData("wb:eva_helmet", 1, 1)),
            new(SlotGroups.AUGMENTS, 0, new ItemData("wb:o2_augment", 1, 1)),
        ];

        JoinRequest staleJoinRequest = new()
        {
            LevelGuid = LEVEL_GUID,
            CharacterUuid = characterUuid,
            View = new CharacterView { CharacterUuid = characterUuid, Name = "Vega", Body = "wb:human_f" },
            State = new CharacterState
            {
                CharacterUuid = characterUuid,
                Name = "Vega",
                Body = "wb:human_f",
                GameMode = GameMode.Adventure,
                Inventory = preDeathInventory,
                Statistics =
                [
                    new Statistic(StatisticIds.HEALTH, StatisticIds.DEFAULT_HEALTH_MAX_FIXED),
                    new Statistic(StatisticIds.OXYGEN, StatisticIds.DEFAULT_OXYGEN_MAX_FIXED),
                ],
            },
        };

        LoopbackClient firstClient = await harness.ConnectClientPeerAsync();
        RpcResult<JoinAccept> firstJoin = await harness.JoinViaJoinSystemAsync(firstClient.ServerPeer, staleJoinRequest);
        Assert.True(firstJoin.IsSuccess, firstJoin.ErrorMessage);
        Assert.True(harness.Sessions.TryGetEntity(firstClient.ServerPeer.GameId, out int firstEntity));

        //  Kill the player -> hotbar + backpack drop into ONE Death-Container, wearables + augments remain.
        DamageService.ApplyDamage(harness.Store, firstEntity, 100f, DamageSource.Suffocation, simTick: 33L);
        harness.DeathSystem.Tick(1f / 60f, harness.Store);
        Assert.Equal(1, harness.CountDeathContainers());

        //  Abrupt disconnect while dead (client never saved post-death empty inventory to profile.db).
        harness.JoinQueue.EnqueueDisconnect(firstClient.ServerPeer.GameId);
        harness.JoinSystem.Tick(1f / 60f, harness.Store);

        //  Reconnect with a new peer sending the STALE pre-death CharacterState.Inventory (which still has hotbar + backpack items!).
        LoopbackClient reconnectedClient = await harness.ConnectClientPeerAsync();
        RpcResult<JoinAccept> rejoin = await harness.JoinViaJoinSystemAsync(reconnectedClient.ServerPeer, staleJoinRequest);
        Assert.True(rejoin.IsSuccess, rejoin.ErrorMessage);
        Assert.True(harness.Sessions.TryGetEntity(reconnectedClient.ServerPeer.GameId, out int rejoinedEntity));

        //  Player remains dead on reconnect, keeps wearables/augments, and has NO duplicated hotbar/backpack items.
        Assert.True(harness.Store.TryGet(rejoinedEntity, out StatisticsComponent rejoinedStats));
        Assert.True(rejoinedStats.IsDead);
        Assert.Equal(0f, rejoinedStats.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.Equal((long)DamageSource.Suffocation, rejoinedStats.GetValue(StatisticIds.DEATH_CAUSE));
        Assert.True(rejoinedStats.GetFlag(StatisticIds.DEATH_HAS_CONTAINER));

        Assert.True(harness.Store.TryGet(rejoinedEntity, out InventoryComponent rejoinedInv));
        Assert.DoesNotContain(rejoinedInv.ToSparseEntries(), e => e.GroupId == SlotGroups.HOTBAR);
        Assert.DoesNotContain(rejoinedInv.ToSparseEntries(), e => e.GroupId == SlotGroups.BACKPACK);
        Assert.Equal("wb:eva_helmet", rejoinedInv.Get(new SlotId(SlotGroups.WEARABLES, 0)).ID);
        Assert.Equal("wb:o2_augment", rejoinedInv.Get(new SlotId(SlotGroups.AUGMENTS, 0)).ID);

        //  Ticking ServerDeathSystem again does NOT spawn a duplicate Death-Container.
        harness.DeathSystem.Tick(1f / 60f, harness.Store);
        Assert.Equal(1, harness.CountDeathContainers());

        //  Client profile stat merge strips per-level death state so other levels never inherit it.
        Statistic[]? clientMerged = StatisticsPersistence.MergeClientStatistics(null, rejoinedStats.Entries);
        Assert.NotNull(clientMerged);
        Assert.DoesNotContain(clientMerged!, s => StatisticIds.IsDeathStat(s.ID.AsSpan()));

        //  Joining a brand-new level with a client seed carrying 0 health starts alive at full health.
        var freshStore = new DataStore();
        int freshPlayer = freshStore.Alloc();
        CharacterStatisticsSeeder.SeedAuthoritativeStatistics(
            freshStore,
            freshPlayer,
            new CharacterState { Statistics = [new Statistic(StatisticIds.HEALTH, 0L)] },
            GameMode.Adventure);
        Assert.True(freshStore.TryGet(freshPlayer, out StatisticsComponent freshStats));
        Assert.False(freshStats.IsDead);
        Assert.Equal(StatisticIds.DEFAULT_HEALTH_MAX, freshStats.GetFixed(StatisticIds.HEALTH), precision: 3);
    }

    [Fact]
    public async Task CreativeModePlayerIsImmuneToDeathAndNeverDropsContainer()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using var harness = new DeathWorldHarness(bricks);
        LoopbackClient creativePlayer = await harness.AddPlayerClientAsync(
            Uuid.FromValue(7108),
            new Vector3(1f, 2f, 3f),
            GameMode.Creative,
            inventory: [new KeyedSlotEntry(SlotGroups.HOTBAR, 0, new ItemData("wb:rock", 50, 100))]);

        harness.Store.SetStatisticFixed(creativePlayer.ServerEntity, StatisticIds.HEALTH, 0f);
        harness.DeathSystem.Tick(1f / 60f, harness.Store);

        Assert.True(harness.Store.TryGet(creativePlayer.ServerEntity, out StatisticsComponent stats));
        Assert.False(stats.IsDead);
        Assert.Equal(0, harness.CountDeathContainers());

        harness.SurvivalSystem.Tick(1f, harness.Store);
        Assert.True(harness.Store.TryGet(creativePlayer.ServerEntity, out StatisticsComponent restored));
        Assert.Equal(StatisticIds.DEFAULT_HEALTH_MAX, restored.GetFixed(StatisticIds.HEALTH), precision: 3);
    }

    [Fact]
    public async Task BreakingDeathContainerTransfersItemsAndDespawnsEmptyStructure()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using var harness = new DeathWorldHarness(bricks, useSqliteStorage: true);

        var deathPos = new Vector3(3f, 0f, 3f);
        LoopbackClient victim = await harness.AddPlayerClientAsync(
            Uuid.FromValue(7109),
            deathPos,
            GameMode.Adventure,
            inventory:
            [
                new KeyedSlotEntry(SlotGroups.HOTBAR, 0, new ItemData("wb:rock", 9, 100)),
                new KeyedSlotEntry(SlotGroups.BACKPACK, 0, new ItemData("wb:panel", 3, 100)),
            ]);
        LoopbackClient breaker = await harness.AddPlayerClientAsync(
            Uuid.FromValue(7110),
            deathPos + new Vector3(1.5f, 0f, 0f),
            GameMode.Adventure,
            inventory: []);

        DamageService.ApplyDamage(harness.Store, victim.ServerEntity, 100f, DamageSource.Kinetic, simTick: 5L);
        harness.DeathSystem.Tick(1f / 60f, harness.Store);
        harness.SaveService.Flush(harness.Store);

        (Uuid structureUuid, _) = harness.GetSingleDeathContainer();
        Assert.True(harness.Store.TryGet(structureUuid, out int structureEntity));
        Assert.True(harness.Store.TryGet(structureEntity, out VoxelWorldComponent structureWorld));
        harness.Replication.PublishStage(1f, harness.Store);
        _ = await breaker.WaitForSnapshotAsync();

        //  Simulate clearing the single storage voxel and invoking ServerBrickEntityService.OnBrickBroken.
        structureWorld.VoxelObject.Set(0, 0, 0, default);
        Assert.True(harness.BrickEntities.OnBrickBroken(
            harness.Store,
            structureUuid,
            new Int3(0, 0, 0),
            out Uuid despawnedStructure,
            breaker.ServerEntity,
            GameMode.Adventure));
        Assert.Equal(structureUuid, despawnedStructure);

        harness.SaveService.QueueDeleteStructure(despawnedStructure);

        Assert.False(harness.Store.TryGet(structureUuid, out _));
        Assert.Equal(0, harness.BrickEntities.Count);

        Assert.True(harness.Store.TryGet(breaker.ServerEntity, out InventoryComponent breakerInventory));
        Assert.Equal(9, CountItem(in breakerInventory, "wb:rock"));
        Assert.Equal(3, CountItem(in breakerInventory, "wb:panel"));

        harness.Replication.PublishStage(1f, harness.Store);
        WorldSnapshot breakerDespawn = await breaker.WaitForSnapshotAsync();
        Assert.Contains(structureUuid.ToValue(), breakerDespawn.RemovedEntities);

        harness.SaveService.Dispose();
        using SqliteLevelStorage db = harness.OpenSqliteStorage();
        Assert.Empty(db.GetVoxelEntities(DataVersion.CURRENT));
        Assert.Empty(db.GetBrickEntities());
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

    private static string GetRepoRoot()
    {
        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", ".."));
    }

    private static string GetServerAssetsRoot()
    {
        return Path.Combine(GetRepoRoot(), "WaywardBeyond.Server", "assets");
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
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(GetServerAssetsRoot()));
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

    private sealed class DeathWorldHarness : IDisposable
    {
        private readonly string? _tempRoot;
        private readonly StoragePaths? _storagePaths;
        private readonly Migrator _migrator = new([]);
        private readonly BrickDatabase _bricks;
        private readonly ILevelCatalog _catalog;
        private readonly GameplayConfig _gameplayConfig = new();
        private readonly LoopbackRpcServerTransport _transport;
        private readonly RpcServer _rpcServer;
        private readonly StubRpcHost _host = new();
        private readonly List<LoopbackClient> _clients = [];
        private readonly Lock _connectLock = new();
        private TaskCompletionSource<RpcPeer>? _pendingConnect;
        private uint _nextSessionId = 1;

        public World EcsWorld { get; } = new();
        public DataStore Store => EcsWorld.DataStore;
        public ServerPeers Peers { get; } = new();
        public SessionManager Sessions { get; } = new();
        public NetworkRegistry Registry { get; }
        public NetworkReplicationSystem Replication { get; }
        public ServerBrickEntityService BrickEntities { get; }
        public LevelSaveService SaveService { get; }
        public OverlapControlledPhysics Physics { get; }
        public SimulationStep SimulationStep { get; }
        public ServerSurvivalSystem SurvivalSystem { get; }
        public ServerDeathService DeathService { get; }
        public ServerDeathSystem DeathSystem { get; }
        public ServerInventorySystem InventorySystem { get; }
        public ServerInteractionSystem InteractionSystem { get; }
        public ServerLaserBeamSystem LaserBeamSystem { get; }
        public ServerJoinQueue JoinQueue { get; } = new();
        public ServerJoinSystem JoinSystem { get; }
        public ServerWorld ServerWorld { get; }

        public DeathWorldHarness(
            BrickDatabase bricks,
            bool useSqliteStorage = false,
            Vector3? levelSpawn = null,
            OverlapControlledPhysics? physicsOverride = null
        )
        {
            _bricks = bricks;
            Vector3 spawn = levelSpawn ?? new Vector3(0f, 1f, 5f);
            _gameplayConfig.DefaultSpawnPosition.Set(spawn);

            using (var netContainer = new Container())
            {
                netContainer.RegisterNetworking();
                Registry = netContainer.Resolve<NetworkRegistry>();
            }

            Physics = physicsOverride ?? new OverlapControlledPhysics();
            SimulationStep = new SimulationStep(Store, Physics);
            Replication = new NetworkReplicationSystem(
                Peers,
                Sessions,
                NullLogger<NetworkReplicationSystem>.Instance,
                new NetworkingConfig(),
                Registry);
            var permissions = new UserPermissionService(new PermissionResolver(PermissionPolicy.Create([])));
            BrickEntities = new ServerBrickEntityService(bricks, Replication, Sessions, permissions: permissions);

            if (useSqliteStorage)
            {
                _tempRoot = Path.Combine(Path.GetTempPath(), $"wb-death-{Guid.NewGuid():N}");
                Directory.CreateDirectory(_tempRoot);
                AppPaths appPaths = AppPaths.ForDirectory(new PathInfo(_tempRoot));
                _storagePaths = new StoragePaths(new StorageConfig(), appPaths);

                var level = new Level(
                    new Data.Version(DataVersion.CURRENT, "Wayward Beyond", "Test"),
                    _Seed: 42L,
                    _Playtime: new Playtime(),
                    _SpawnX: (int)spawn.X,
                    _SpawnY: (int)spawn.Y,
                    _SpawnZ: (int)spawn.Z,
                    GameMode.Adventure,
                    LEVEL_GUID,
                    "Death Test Level");
                Directory.CreateDirectory(_storagePaths.GetLevelDirectory(LEVEL_GUID));
                using (var initDb = new SqliteLevelStorage(NullLogger<SqliteLevelStorage>.Instance, _storagePaths, LEVEL_GUID, _migrator))
                {
                    Assert.True(initDb.Save(level, [], [], []).Success);
                }

                _catalog = new SqliteLevelCatalog(
                    NullLogger<SqliteLevelCatalog>.Instance,
                    _storagePaths,
                    bricks,
                    NullLoggerFactory.Instance,
                    _migrator,
                    _gameplayConfig,
                    new LevelGeneratorConfig(),
                    CreateSeedPhraseService());
            }
            else
            {
                _catalog = new SingleLevelCatalog(new InMemoryLevelStorage(LEVEL_GUID, spawn));
            }

            SaveService = new LevelSaveService(
                NullLogger<LevelSaveService>.Instance,
                _catalog,
                bricks,
                bricks,
                _gameplayConfig,
                paintRegistry: null,
                brickEntities: BrickEntities);
            Assert.True(SaveService.LoadLevel(LEVEL_GUID, Store));

            IBundledBlueprintDatabase bundled = CreateServerBundledBlueprintDatabase();
            SurvivalSystem = new ServerSurvivalSystem(SimulationStep, _gameplayConfig);
            DeathService = new ServerDeathService(
                SimulationStep,
                _gameplayConfig,
                Replication,
                Sessions,
                Peers,
                SaveService,
                BrickEntities,
                Physics,
                bricks,
                bricks,
                paintRegistry: null,
                bundledBlueprints: bundled,
                itemDatabase: null,
                logger: NullLogger<ServerDeathService>.Instance);
            DeathSystem = new ServerDeathSystem(DeathService, Peers, Sessions, SimulationStep);
            InventorySystem = new ServerInventorySystem(
                schemas: null,
                tagDatabase: null,
                itemDatabase: null,
                sessions: Sessions,
                permissions: permissions,
                brickEntities: BrickEntities,
                saveService: SaveService);

            var handlerRegistry = new InteractionHandlerRegistry([]);
            Func<DataStore, IVoxelInteractionWorld> worldFactory = s => new ServerVoxelInteractionWorld(s, Physics);
            InteractionSystem = new ServerInteractionSystem(
                Sessions,
                NullLogger<ServerInteractionSystem>.Instance,
                handlerRegistry,
                worldFactory,
                SimulationStep,
                BrickEntities,
                SaveService);
            LaserBeamSystem = new ServerLaserBeamSystem(Physics, new StubItemDatabase(), SimulationStep);
            JoinSystem = new ServerJoinSystem(
                Sessions,
                SaveService,
                Replication,
                SimulationStep,
                NullLogger<ServerJoinSystem>.Instance,
                _gameplayConfig,
                new StubSkillDatabase(),
                JoinQueue,
                Peers,
                permissions);

            var worldContainer = new Container();
            worldContainer.RegisterInstance(EcsWorld);
            worldContainer.RegisterInstance(Sessions);
            worldContainer.RegisterInstance(JoinQueue);
            worldContainer.RegisterInstance(Replication);
            worldContainer.RegisterInstance<IUserPermissionService>(permissions);
            worldContainer.RegisterInstance(BrickEntities);
            worldContainer.RegisterInstance(SaveService);
            worldContainer.RegisterInstance(new ServerParticleService(Peers, Sessions, Replication, NullLogger<ServerParticleService>.Instance));
            worldContainer.RegisterInstance<IInteractionHandlerRegistry>(handlerRegistry);
            worldContainer.RegisterInstance<IServerWorldPhysics>(Physics);
            worldContainer.RegisterInstance(SimulationStep);
            worldContainer.RegisterInstance(DeathService);
            worldContainer.RegisterInstance(worldFactory);
            worldContainer.RegisterInstance(InteractionSystem);
            ServerWorld = new ServerWorld(worldContainer);

            _transport = new LoopbackRpcServerTransport();
            var serverRpcRegistry = new RpcRegistry();
            serverRpcRegistry.Freeze();
            _rpcServer = new RpcServer(_transport, serverRpcRegistry);
            _rpcServer.ClientConnected += OnClientConnected;
            _rpcServer.Start();
        }

        public SqliteLevelStorage OpenSqliteStorage()
        {
            Assert.NotNull(_storagePaths);
            return new SqliteLevelStorage(NullLogger<SqliteLevelStorage>.Instance, _storagePaths!, LEVEL_GUID, _migrator);
        }

        public LevelSaveService CreateSeparateSaveService(ServerBrickEntityService brickEntities)
        {
            return new LevelSaveService(
                NullLogger<LevelSaveService>.Instance,
                _catalog,
                _bricks,
                _bricks,
                _gameplayConfig,
                paintRegistry: null,
                brickEntities: brickEntities);
        }

        public void CreateStructure(Uuid structureUuid, Vector3 position, Quaternion orientation, params (Int3 Coord, ushort BrickId)[] voxels)
        {
            int entity = Store.Alloc(structureUuid);
            var voxelObject = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
            for (var i = 0; i < voxels.Length; i++)
            {
                voxelObject.Set(voxels[i].Coord.X, voxels[i].Coord.Y, voxels[i].Coord.Z, new Voxel { ID = voxels[i].BrickId });
            }

            Store.AddOrUpdate(entity, new TransformComponent(position, orientation, Vector3.One));
            Store.AddOrUpdate(entity, new VoxelWorldComponent(voxelObject));
            Store.AddOrUpdate(entity, new NetworkComponent());
        }

        public async Task<LoopbackClient> ConnectClientPeerAsync()
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
            serverPeer.World = ServerWorld;

            var client = new LoopbackClient(connection, inbound, serverPeer, Entity.Null, Uuid.Null);
            _clients.Add(client);
            return client;
        }

        public async Task<LoopbackClient> AddPlayerClientAsync(
            Uuid entityUuid,
            Vector3 position,
            GameMode gameMode,
            KeyedSlotEntry[] inventory
        )
        {
            LoopbackClient peerClient = await ConnectClientPeerAsync();
            int entity = Store.Alloc(entityUuid);
            Store.AddOrUpdate(entity, NetworkComponent.Create());
            Store.AddOrUpdate(entity, new InputComponent());
            Store.AddOrUpdate(entity, new TransformComponent(position, Quaternion.Identity, CharacterDefaults.PlayerScale));
            Store.AddOrUpdate(entity, CharacterDefaults.CreateCharacterPhysicsComponent());
            Store.AddOrUpdate(entity, new OwnedCharacterComponent());
            Store.AddOrUpdate(entity, new GameModeComponent(gameMode));
            Store.AddOrUpdate(entity, new EquipmentComponent(0));

            var inv = InventoryComponent.CreatePlayer();
            inv.LoadSparse(inventory);
            Store.AddOrUpdate(entity, inv);

            CharacterStatisticsSeeder.SeedAuthoritativeStatistics(Store, entity, new CharacterState(), gameMode);
            Sessions.Register(Store, entity, peerClient.ServerPeer.GameId, new Session(_nextSessionId++));

            var bound = new LoopbackClient(peerClient.Connection, peerClient.Inbound, peerClient.ServerPeer, entity, entityUuid);
            _clients[^1] = bound;
            return bound;
        }

        public async Task<RpcResult<JoinAccept>> JoinViaJoinSystemAsync(ServerPeer peer, JoinRequest request)
        {
            var completion = new TaskCompletionSource<RpcResult<JoinAccept>>(TaskCreationOptions.RunContinuationsAsynchronously);
            JoinQueue.Enqueue(peer, request, completion, CancellationToken.None);
            JoinSystem.Tick(1f / 60f, Store);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public async Task<RpcResult<RespawnResponse>> SendRespawnAsync(LoopbackClient client)
        {
            var completion = new TaskCompletionSource<RpcResult<RespawnResponse>>(TaskCreationOptions.RunContinuationsAsynchronously);
            client.ServerPeer.RespawnCalls.Enqueue(new ServerPeer.RespawnCall(completion, CancellationToken.None));
            DeathSystem.Tick(1f / 60f, Store);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public void TickSurvivalAndDeath(float delta)
        {
            Physics.Step();
            SurvivalSystem.Tick(delta, Store);
            DeathSystem.Tick(delta, Store);
        }

        public int CountDeathContainers()
        {
            var count = 0;
            CountAction action = new();
            Store.Query<MachineStateComponent, CountAction>(0f, ref action);
            count = action.Count;
            return count;
        }

        public (Uuid StructureUuid, int BrickEntity) GetSingleDeathContainer()
        {
            FindAction action = new();
            Store.Query<BrickAnchorComponent, MachineStateComponent, FindAction>(0f, ref action);
            Assert.Equal(1, action.FoundCount);
            return (action.StructureUuid, action.BrickEntity);
        }

        public void Dispose()
        {
            for (var i = 0; i < _clients.Count; i++)
            {
                _clients[i].Connection.Close();
            }

            _rpcServer.Close();
            _transport.Close();
            SaveService.Dispose();
            SimulationStep.Dispose();

            if (_tempRoot != null && Directory.Exists(_tempRoot))
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
        }

        private void OnClientConnected(RpcPeer peer)
        {
            lock (_connectLock)
            {
                _pendingConnect?.TrySetResult(peer);
            }
        }

        private struct CountAction : IForEach<MachineStateComponent>
        {
            public int Count;

            public void Execute(float delta, DataStore store, int entity, in MachineStateComponent machine)
            {
                if (machine.HasStatusFlag(MachineStatusFlags.AutoDespawnWhenEmpty)
                    && string.Equals(machine.StationTag, MachineStateComponent.DEATH_CONTAINER_STATION_TAG, StringComparison.Ordinal))
                {
                    Count++;
                }
            }
        }

        private struct FindAction : IForEach<BrickAnchorComponent, MachineStateComponent>
        {
            public int FoundCount;
            public Uuid StructureUuid;
            public int BrickEntity;

            public void Execute(float delta, DataStore store, int entity, in BrickAnchorComponent anchor, in MachineStateComponent machine)
            {
                if (machine.HasStatusFlag(MachineStatusFlags.AutoDespawnWhenEmpty)
                    && string.Equals(machine.StationTag, MachineStateComponent.DEATH_CONTAINER_STATION_TAG, StringComparison.Ordinal))
                {
                    FoundCount++;
                    StructureUuid = anchor.VoxelEntityUuid;
                    BrickEntity = entity;
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

        public void DrainSnapshots()
        {
            while (Inbound.Snapshots.TryDequeue(out _)) { }
        }

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

        public async Task<VoxelEntitySpawnPart> WaitForEntitySpawnPartAsync()
        {
            long deadline = Environment.TickCount64 + 5_000;
            while (Inbound.VoxelEntitySpawns.IsEmpty && Environment.TickCount64 < deadline)
            {
                await Task.Delay(5);
            }

            Assert.False(Inbound.VoxelEntitySpawns.IsEmpty, "Timed out waiting for a VoxelEntitySpawnPart.");
            Assert.True(Inbound.VoxelEntitySpawns.TryDequeue(out VoxelEntitySpawnPart part));
            return part;
        }
    }

    private sealed class OverlapControlledPhysics(Func<Vector3, bool>? overlapPredicate = null) : IServerWorldPhysics
    {
        public float FixedTimestamp => 1f / 64f;

        public event EventHandler<EventArgs>? FixedUpdate;

        public RaycastResult Raycast(in Ray ray) => default;

        public bool Overlaps(in ShapeOverlapQuery query) => overlapPredicate != null && overlapPredicate(query.Position);

        public void SetGravity(Vector3 gravity) { }

        public void Tick(float delta, DataStore store) { }

        public void Step()
        {
            FixedUpdate?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class StubRpcHost : IRpcHost
    {
        public PeerSet Peers => default;
    }

    private sealed class StubSkillDatabase : ISkillDatabase
    {
        public IReadOnlyList<Skill> All => [];

        public Result<Skill> Get(string id) => Result<Skill>.FromFailure((string?)null);

        public IReadOnlyList<Skill> Get(XPSource xpSource) => [];
    }

    private sealed class StubItemDatabase : IItemDatabase
    {
        public Result<Item> Get(string id) => Result<Item>.FromFailure((string?)null);

        public List<Item> Get(Func<Item, bool> predicate) => [];
    }

    private sealed class SingleLevelCatalog(ILevelStorage storage) : ILevelCatalog
    {
        public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
        {
            levelGuid = LEVEL_GUID;
            return true;
        }

        public Level[] ListLevels() => [];

        public bool Delete(string levelGuid) => true;

        public bool Exists(string levelGuid) => levelGuid == LEVEL_GUID;

        public ILevelStorage? Open(string levelGuid) => levelGuid == LEVEL_GUID ? storage : null;
    }

    private sealed class InMemoryLevelStorage(string levelGuid, Vector3 spawn) : ILevelStorage
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<ulong, CharacterEntityData> _characters = [];
        private Level _level = new()
        {
            Guid = levelGuid,
            Name = "In-Memory Death Test Level",
            SpawnX = (int)spawn.X,
            SpawnY = (int)spawn.Y,
            SpawnZ = (int)spawn.Z,
            DefaultGameMode = GameMode.Adventure,
        };

        public Result<Level> GetLevel()
        {
            lock (_lock)
            {
                return Result<Level>.FromSuccess(_level);
            }
        }

        public IReadOnlyList<VoxelEntityData> GetVoxelEntities(uint dataVersion) => [];

        public Result<CharacterEntityData> GetCharacterEntity(ulong uuid)
        {
            lock (_lock)
            {
                return _characters.TryGetValue(uuid, out CharacterEntityData data)
                    ? Result<CharacterEntityData>.FromSuccess(data)
                    : Result<CharacterEntityData>.FromFailure("Not found.");
            }
        }

        public Result Save(Level level, IReadOnlyList<VoxelEntityData> voxelEntities, IReadOnlyList<CharacterEntityData> characterEntities)
        {
            lock (_lock)
            {
                _level = level;
                foreach (CharacterEntityData characterEntity in characterEntities)
                {
                    _characters[characterEntity.Uuid] = characterEntity;
                }
            }

            return Result.FromSuccess();
        }

        public Result Save(Level level)
        {
            lock (_lock)
            {
                _level = level;
            }

            return Result.FromSuccess();
        }

        public Result Save(CharacterEntityData characterEntity)
        {
            lock (_lock)
            {
                _characters[characterEntity.Uuid] = characterEntity;
            }

            return Result.FromSuccess();
        }

        public void Dispose() { }
    }
}
