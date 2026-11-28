#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using NsdRpc;
using Swordfish.ECS;
using Swordfish.Library.Collections;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Tests;
using WaywardBeyond.Bricks;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Buffs;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Gameplay.Statistics;
using WaywardBeyond.Gameplay.Survival;
using WaywardBeyond.Items;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Interactions;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Skills;
using Xunit;
using RpcServerConnection = NsdRpc.IServerConnection;

namespace WaywardBeyond.Tests;

/// <summary>Covers item Equip TOML parsing, slot-tag enforcement, S.P.A.C.E.R. stat aggregation, vital clamping, kinetic mitigation, movement scaling, and rejoin persistence.</summary>
public sealed class StatAggregationTests
{
    [Fact]
    public void ItemDatabaseParsesEquipBlockAndFiltersInvalidEntries()
    {
        using var assets = new TempItemAssets("""
            [[Items]]
            ID = "wb:eva_helmet"
            Name = "item.eva_helmet"
            MaxStack = 1
            [Items.Equip]
            SlotTag = "helmet"
            [[Items.Equip.StatModifiers]]
            StatId = "wb:oxygen_max"
            FlatDelta = 50.0
            [[Items.Equip.StatModifiers]]
            StatId = "wb:awareness"
            FlatDelta = 1.0

            [[Items]]
            ID = "wb:kinetic_chip"
            Name = "item.kinetic_chip"
            [Items.Equip]
            SlotTag = "augment"
            [[Items.Equip.StatModifiers]]
            StatId = "wb:kinetic_resist"
            FlatDelta = 0.2
            [[Items.Equip.StatModifiers]]
            StatId = ""
            FlatDelta = 5.0
            [[Items.Equip.StatModifiers]]
            StatId = "wb:health_max"
            FlatDelta = 0.0
            [[Items.Equip.StatModifiers]]
            StatId = "wb:move_speed_pct"
            FlatDelta = nan

            [[Items]]
            ID = "wb:broken_equip"
            Name = "item.broken"
            [Items.Equip]
            SlotTag = "invalid_slot"
            """);

        ItemDatabase db = assets.LoadItems();

        Item helmet = db.Get("wb:eva_helmet").Value;
        Assert.True(helmet.Equip.HasValue);
        Assert.Equal(ItemTags.WEARABLE_HELMET, helmet.Equip.Value.SlotTag);
        Assert.Equal(2, helmet.Equip.Value.StatModifiers!.Count);
        Assert.Equal(StatisticIds.OXYGEN_MAX, helmet.Equip.Value.StatModifiers[0].StatId);
        Assert.Equal(50f, helmet.Equip.Value.StatModifiers[0].FlatDelta);

        Item chip = db.Get("wb:kinetic_chip").Value;
        Assert.True(chip.Equip.HasValue);
        Assert.Equal(ItemTags.AUGMENT, chip.Equip.Value.SlotTag);
        EquipStatModifierDefinition validMod = Assert.Single(chip.Equip.Value.StatModifiers!);
        Assert.Equal(StatisticIds.KINETIC_RESIST, validMod.StatId);
        Assert.Equal(0.2f, validMod.FlatDelta);

        Item broken = db.Get("wb:broken_equip").Value;
        Assert.False(broken.Equip.HasValue);
    }

    [Fact]
    public void InventoryResolverEnforcesSlotTagsForAllWearablesAndAugments()
    {
        SlotGroupSchemaDatabase schemas = LoadShippedSchemas();
        var itemDb = new StubItemDatabase(
            CreateEquippable("wb:helmet", "helmet"),
            CreateEquippable("wb:torso", "torso"),
            CreateEquippable("wb:legs", "legs"),
            CreateEquippable("wb:head", "head"),
            CreateEquippable("wb:suit", "suit"),
            CreateEquippable("wb:o2", "o2"),
            CreateEquippable("wb:aug", "augment"),
            new Item("wb:rock", "item.rock", null, 64, null, null, null, null));

        var context = new InventoryValidationContext(schemas, invariantTags: null, itemDb);
        string[] slotTags = ["wb:helmet", "wb:torso", "wb:legs", "wb:head", "wb:suit", "wb:o2"];

        InventoryComponent inventory = InventoryComponent.CreatePlayer(schemas);
        var backpack0 = new SlotId(SlotGroups.BACKPACK, 0);

        //  Each wearable item fits only its matching wb:wearables[0..5] slot and rejects all others + wb:augments.
        for (var i = 0; i < slotTags.Length; i++)
        {
            string itemId = slotTags[i];
            inventory.Set(in backpack0, new ItemData(itemId, 1, 1));

            for (var targetIndex = 0; targetIndex < slotTags.Length; targetIndex++)
            {
                var targetSlot = new SlotId(SlotGroups.WEARABLES, targetIndex);
                bool moved = InventoryResolver.Apply(ref inventory, new TransferOp(0UL, backpack0, 0UL, targetSlot, TransferMode.Exact, null), in context);
                if (targetIndex == i)
                {
                    Assert.True(moved);
                    Assert.Equal(itemId, inventory.Get(in targetSlot).ID);
                    Assert.True(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, targetSlot, 0UL, backpack0, TransferMode.Exact, null), in context));
                }
                else
                {
                    Assert.False(moved);
                }
            }

            Assert.False(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, backpack0, 0UL, new SlotId(SlotGroups.AUGMENTS, 0), TransferMode.Exact, null), in context));

            //  AutoStack targeting wb:wearables routes directly into slot i.
            Assert.True(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, backpack0, 0UL, new SlotId(SlotGroups.WEARABLES, -1), TransferMode.AutoStack, null), in context));
            Assert.Equal(itemId, inventory.Get(new SlotId(SlotGroups.WEARABLES, i)).ID);
            Assert.True(inventory.Get(in backpack0).IsEmpty());
        }

        //  Verify all 5 augment sockets accept wb:aug and reject wb:rock and wearables.
        for (var socket = 0; socket < 5; socket++)
        {
            inventory.Set(in backpack0, new ItemData("wb:aug", 1, 1));
            var augSlot = new SlotId(SlotGroups.AUGMENTS, socket);
            Assert.True(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, backpack0, 0UL, augSlot, TransferMode.Exact, null), in context));
            Assert.Equal("wb:aug", inventory.Get(in augSlot).ID);

            //  Bidirectional swap with non-equippable rock must fail.
            inventory.Set(in backpack0, new ItemData("wb:rock", 5, 64));
            Assert.False(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, backpack0, 0UL, augSlot, TransferMode.Exact, null), in context));
            Assert.False(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, augSlot, 0UL, backpack0, TransferMode.Exact, null), in context));
        }

        Assert.False(InventoryResolver.Apply(ref inventory, new TransferOp(0UL, backpack0, 0UL, new SlotId(SlotGroups.WEARABLES, 0), TransferMode.Exact, null), in context));
    }

    [Fact]
    public void StatAggregationLayersBaseSpacerWearablesAugmentsAndBuffs()
    {
        var items = new StubItemDatabase(
            CreateEquippable("wb:eva_helmet", "helmet",
                (StatisticIds.OXYGEN_MAX, 50f, null),
                (StatisticIds.AWARENESS, 2f, null),
                ("mod:shield", 15f, null)),
            CreateEquippable("wb:eva_suit", "suit",
                (StatisticIds.HEALTH_MAX, 40f, null),
                (StatisticIds.KINETIC_RESIST, 0.25f, null),
                (StatisticIds.RESOLVE, 2f, null)),
            CreateEquippable("wb:thruster_pack", "o2",
                (StatisticIds.MOVE_SPEED_PCT, 0.2f, null),
                (StatisticIds.PRECISION, 1f, null)),
            CreateEquippable("wb:kinetic_aug", "augment",
                (StatisticIds.KINETIC_RESIST, 0.1f, null),
                (StatisticIds.HEALTH_MAX, null, 0.1f)));

        var buffs = new StubBuffDatabase();
        buffs.Add(new Buff
        {
            ID = "wb:combat_stim",
            Name = "buff.stim",
            DurationSeconds = 30f,
            StackPolicy = BuffStackPolicy.Refresh,
            StatModifiers =
            [
                new BuffStatModifier(StatisticIds.RESOLVE, FlatDelta: 1f),
                new BuffStatModifier(StatisticIds.HEALTH_MAX, FlatDelta: 20f),
                new BuffStatModifier(StatisticIds.MOVE_SPEED_PCT, FlatDelta: 0.1f),
            ],
        }.Validate().Value);

        var store = new DataStore();
        using var simStep = new SimulationStep(store, new FakePhysics());
        simStep.AlignTo(100);
        var aggregation = new ServerStatAggregationSystem(simStep, items, buffs);

        int player = store.Alloc(Uuid.FromValue(101));
        store.AddOrUpdate(player, new OwnedCharacterComponent());
        CharacterStatisticsSeeder.SeedAuthoritativeStatistics(
            store,
            player,
            new CharacterState
            {
                Statistics =
                [
                    new Statistic(StatisticIds.STRENGTH, 3L),
                    new Statistic(StatisticIds.PRECISION, 2L),
                    new Statistic(StatisticIds.AWARENESS, 2L),
                    new Statistic(StatisticIds.CHARISMA, 1L),
                    new Statistic(StatisticIds.EDUCATION, 4L),
                    new Statistic(StatisticIds.RESOLVE, 2L),
                ],
            },
            GameMode.Adventure);

        InventoryComponent inv = InventoryComponent.CreatePlayer();
        inv.Set(new SlotId(SlotGroups.WEARABLES, 0), new ItemData("wb:eva_helmet", 1, 1));
        inv.Set(new SlotId(SlotGroups.WEARABLES, 4), new ItemData("wb:eva_suit", 1, 1));
        inv.Set(new SlotId(SlotGroups.WEARABLES, 5), new ItemData("wb:thruster_pack", 1, 1));
        //  Stack two identical kinetic augments in sockets 0 and 1.
        inv.Set(new SlotId(SlotGroups.AUGMENTS, 0), new ItemData("wb:kinetic_aug", 1, 1));
        inv.Set(new SlotId(SlotGroups.AUGMENTS, 1), new ItemData("wb:kinetic_aug", 1, 1));
        store.AddOrUpdate(player, inv);

        store.ApplyBuff(player, buffs.Get("wb:combat_stim").Value, currentSimTick: 100L, fixedDt: 1f / 60f);

        aggregation.Tick(1f / 60f, store);

        Assert.True(store.TryGet(player, out StatisticsComponent stats));
        //  Resolve: base 2 + suit 2 + stim 1 = 5.
        Assert.Equal(5L, stats.GetValue(StatisticIds.RESOLVE));
        //  Awareness: base 2 + helmet 2 = 4.
        Assert.Equal(4L, stats.GetValue(StatisticIds.AWARENESS));
        //  Precision: base 2 + pack 1 = 3.
        Assert.Equal(3L, stats.GetValue(StatisticIds.PRECISION));
        //  Strength: 3, Education: 4.
        Assert.Equal(3L, stats.GetValue(StatisticIds.STRENGTH));
        Assert.Equal(4L, stats.GetValue(StatisticIds.EDUCATION));

        //  HealthMax: (Base 100 + Strength(3->+10) + Resolve(5->+20) + Suit(+40) + Stim(+20)) * (1 + 0.1 + 0.1) = 190 * 1.2 = 228.
        Assert.Equal(228f, stats.GetFixed(StatisticIds.HEALTH_MAX), precision: 3);
        //  OxygenMax: Base 100 + Awareness(4->+15) + Helmet(+50) = 165.
        Assert.Equal(165f, stats.GetFixed(StatisticIds.OXYGEN_MAX), precision: 3);
        //  KineticResist: Resolve(5->+0.08) + Suit(+0.25) + 2x Aug(+0.20) = 0.53.
        Assert.Equal(0.53f, stats.GetFixed(StatisticIds.KINETIC_RESIST), precision: 3);
        //  MoveSpeedPct: Precision(3->+0.04) + Pack(+0.20) + Stim(+0.10) = 0.34.
        Assert.Equal(0.34f, stats.GetFixed(StatisticIds.MOVE_SPEED_PCT), precision: 3);
        //  MiningSpeed entity bonus: Education(4->+0.06).
        Assert.Equal(0.06f, stats.GetFixed(StatisticIds.MINING_SPEED), precision: 3);
        //  Custom gear stat present while equipped.
        Assert.Equal(15f, stats.GetFixed("mod:shield"), precision: 3);

        //  Expire the buff and unequip the helmet -> buff bonus and mod:shield cleanly drop off.
        Assert.True(stats.TryGetBuffExpiresTick("wb:combat_stim", out long expiryTick));
        simStep.AlignTo((uint)expiryTick);
        inv = inv.Clone();
        inv.Set(new SlotId(SlotGroups.WEARABLES, 0), default);
        store.AddOrUpdate(player, inv);

        aggregation.Tick(1f / 60f, store);

        Assert.True(store.TryGet(player, out stats));
        Assert.Equal(4L, stats.GetValue(StatisticIds.RESOLVE));
        Assert.Equal(2L, stats.GetValue(StatisticIds.AWARENESS));
        //  HealthMax: (100 + Strength(3->+10) + Resolve(4->+15) + Suit(+40)) * 1.2 = 165 * 1.2 = 198.
        Assert.Equal(198f, stats.GetFixed(StatisticIds.HEALTH_MAX), precision: 3);
        //  OxygenMax: 100 + Awareness(2->+5) = 105.
        Assert.Equal(105f, stats.GetFixed(StatisticIds.OXYGEN_MAX), precision: 3);
        Assert.False(stats.TryGetFixed("mod:shield", out _));
    }

    [Fact]
    public void UnequippingMaxVitalGearClampsCurrentVitalsWithoutHealExploit()
    {
        var items = new StubItemDatabase(
            CreateEquippable("wb:heavy_suit", "suit", (StatisticIds.HEALTH_MAX, 50f, null)),
            CreateEquippable("wb:o2_pack", "o2", (StatisticIds.OXYGEN_MAX, 80f, null)));

        var store = new DataStore();
        using var simStep = new SimulationStep(store, new FakePhysics());
        var aggregation = new ServerStatAggregationSystem(simStep, items);

        int player = store.Alloc(Uuid.FromValue(102));
        store.AddOrUpdate(player, new OwnedCharacterComponent());
        CharacterStatisticsSeeder.SeedAuthoritativeStatistics(store, player, new CharacterState(), GameMode.Adventure);
        InventoryComponent inv = InventoryComponent.CreatePlayer();
        store.AddOrUpdate(player, inv);

        aggregation.Tick(1f / 60f, store);

        //  Equip max-HP and max-O2 gear: current vitals stay at 100 (never grant free heal!).
        inv = inv.Clone();
        inv.Set(new SlotId(SlotGroups.WEARABLES, 4), new ItemData("wb:heavy_suit", 1, 1));
        inv.Set(new SlotId(SlotGroups.WEARABLES, 5), new ItemData("wb:o2_pack", 1, 1));
        store.AddOrUpdate(player, inv);
        aggregation.Tick(1f / 60f, store);

        Assert.True(store.TryGet(player, out StatisticsComponent stats));
        Assert.Equal(150f, stats.GetFixed(StatisticIds.HEALTH_MAX), precision: 3);
        Assert.Equal(100f, stats.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.Equal(180f, stats.GetFixed(StatisticIds.OXYGEN_MAX), precision: 3);
        Assert.Equal(100f, stats.GetFixed(StatisticIds.OXYGEN), precision: 3);

        //  Heal/refill up to 140 HP and 160 O2, then unequip both items -> vitals clamp cleanly to 100.
        store.SetStatisticFixed(player, StatisticIds.HEALTH, 140f);
        store.SetStatisticFixed(player, StatisticIds.OXYGEN, 160f);
        inv = inv.Clone();
        inv.Set(new SlotId(SlotGroups.WEARABLES, 4), default);
        inv.Set(new SlotId(SlotGroups.WEARABLES, 5), default);
        store.AddOrUpdate(player, inv);
        aggregation.Tick(1f / 60f, store);

        Assert.True(store.TryGet(player, out stats));
        Assert.Equal(100f, stats.GetFixed(StatisticIds.HEALTH_MAX), precision: 3);
        Assert.Equal(100f, stats.GetFixed(StatisticIds.HEALTH), precision: 3);
        Assert.Equal(100f, stats.GetFixed(StatisticIds.OXYGEN_MAX), precision: 3);
        Assert.Equal(100f, stats.GetFixed(StatisticIds.OXYGEN), precision: 3);

        //  Re-equipping gear leaves current HP/O2 at 100 (no heal exploit).
        inv = inv.Clone();
        inv.Set(new SlotId(SlotGroups.WEARABLES, 4), new ItemData("wb:heavy_suit", 1, 1));
        store.AddOrUpdate(player, inv);
        aggregation.Tick(1f / 60f, store);

        Assert.True(store.TryGet(player, out stats));
        Assert.Equal(150f, stats.GetFixed(StatisticIds.HEALTH_MAX), precision: 3);
        Assert.Equal(100f, stats.GetFixed(StatisticIds.HEALTH), precision: 3);
    }

    [Fact]
    public void TransferOpAndDamageServiceIntegrateWithDirtyGatedAggregation()
    {
        SlotGroupSchemaDatabase schemas = LoadShippedSchemas();
        var items = new StubItemDatabase(
            CreateEquippable("wb:plated_suit", "suit",
                (StatisticIds.KINETIC_RESIST, 0.3f, null),
                (StatisticIds.RESOLVE, 3f, null)),
            CreateEquippable("wb:impact_chip", "augment",
                (StatisticIds.KINETIC_RESIST, 0.1f, null)),
            new Item("wb:rock", "item.rock", null, 64, null, null, null, null));

        var store = new DataStore();
        using var simStep = new SimulationStep(store, new FakePhysics());
        var inventorySystem = new ServerInventorySystem(schemas, tagDatabase: null, items);
        var aggregation = new ServerStatAggregationSystem(simStep, items);

        int player = store.Alloc(Uuid.FromValue(103));
        store.AddOrUpdate(player, new OwnedCharacterComponent());
        store.AddOrUpdate(player, new GameModeComponent(GameMode.Adventure));
        store.AddOrUpdate(player, NetworkComponent.Create());
        CharacterStatisticsSeeder.SeedAuthoritativeStatistics(store, player, new CharacterState(), GameMode.Adventure);

        InventoryComponent inv = InventoryComponent.CreatePlayer(schemas);
        inv.Set(new SlotId(SlotGroups.BACKPACK, 0), new ItemData("wb:plated_suit", 1, 1));
        inv.Set(new SlotId(SlotGroups.BACKPACK, 1), new ItemData("wb:impact_chip", 1, 1));
        inv.Set(new SlotId(SlotGroups.BACKPACK, 2), new ItemData("wb:rock", 5, 64));
        store.AddOrUpdate(player, inv);

        aggregation.Tick(1f / 60f, store);
        store.ClearDirty<StatisticsComponent>(player);
        store.ClearDirty<InventoryComponent>(player);

        //  Backpack-only TransferOp must NOT re-dirty StatisticsComponent during aggregation.
        Assert.True(store.TryGet(player, out NetworkComponent net));
        net.StagedInventoryOps!.Stage(1, new TransferOp(0UL, new SlotId(SlotGroups.BACKPACK, 2), 0UL, new SlotId(SlotGroups.HOTBAR, 0), TransferMode.Exact, 2));
        inventorySystem.Tick(1f / 60f, store);
        Assert.True(store.IsDirty<InventoryComponent>(player));
        aggregation.Tick(1f / 60f, store);
        Assert.False(store.IsDirty<StatisticsComponent>(player));
        store.ClearDirty<InventoryComponent>(player);

        //  Equip suit and augment via TransferOp -> triggers aggregation and mitigates kinetic damage.
        net.StagedInventoryOps!.Stage(2, new TransferOp(0UL, new SlotId(SlotGroups.BACKPACK, 0), 0UL, new SlotId(SlotGroups.WEARABLES, -1), TransferMode.AutoStack, null));
        net.StagedInventoryOps!.Stage(3, new TransferOp(0UL, new SlotId(SlotGroups.BACKPACK, 1), 0UL, new SlotId(SlotGroups.AUGMENTS, -1), TransferMode.AutoStack, null));
        inventorySystem.Tick(1f / 60f, store);
        aggregation.Tick(1f / 60f, store);

        Assert.True(store.IsDirty<StatisticsComponent>(player));
        Assert.True(store.TryGet(player, out StatisticsComponent stats));
        //  Resolve: 1 + 3 = 4 (3 points above min -> +0.06 kinetic resist) + Suit 0.30 + Aug 0.10 = 0.46.
        Assert.Equal(0.46f, stats.GetFixed(StatisticIds.KINETIC_RESIST), precision: 3);

        DamageResult dmg = DamageService.ApplyDamage(store, player, 100f, DamageSource.Kinetic, simTick: 1L);
        Assert.Equal(54f, dmg.Applied, precision: 3);
    }

    [Fact]
    public void SimulationStepAppliesReplicatedMoveSpeedModifierWithinBounds()
    {
        var physics = new SteppingFakePhysics();
        var serverStore = new DataStore();
        var clientStore = new DataStore();
        using var serverStep = new SimulationStep(serverStore, physics);
        using var clientStep = new SimulationStep(clientStore, physics);

        int serverPlayer = AllocateMotionPlayer(serverStore, moveSpeedPct: 0.25f);
        int clientPlayer = AllocateMotionPlayer(clientStore, moveSpeedPct: 0.25f);

        physics.Step();

        Assert.True(serverStore.TryGet(serverPlayer, out PhysicsComponent serverPhys));
        Assert.True(clientStore.TryGet(clientPlayer, out PhysicsComponent clientPhys));
        float expectedZ = CharacterDefaults.BASE_SPEED * 1.25f * physics.FixedTimestamp;
        Assert.Equal(expectedZ, serverPhys.Velocity.Z, precision: 4);
        Assert.Equal(serverPhys.Velocity.Z, clientPhys.Velocity.Z, precision: 5);

        //  Verify upper and lower safety clamps.
        int fastPlayer = AllocateMotionPlayer(serverStore, moveSpeedPct: 10.0f);
        int slowPlayer = AllocateMotionPlayer(serverStore, moveSpeedPct: -0.95f);
        physics.Step();

        Assert.True(serverStore.TryGet(fastPlayer, out PhysicsComponent fastPhys));
        Assert.True(serverStore.TryGet(slowPlayer, out PhysicsComponent slowPhys));
        Assert.Equal(CharacterDefaults.BASE_SPEED * CharacterDefaults.MAX_MOVE_SPEED_MULTIPLIER * physics.FixedTimestamp, fastPhys.Velocity.Z, precision: 4);
        Assert.Equal(CharacterDefaults.BASE_SPEED * CharacterDefaults.MIN_MOVE_SPEED_MULTIPLIER * physics.FixedTimestamp, slowPhys.Velocity.Z, precision: 4);
    }

    [Fact]
    public async Task RejoinWithEquippedGearDoesNotDoubleCountAndPreservesBoostedHealth()
    {
        var items = new StubItemDatabase(
            CreateEquippable("wb:eva_suit", "suit", (StatisticIds.HEALTH_MAX, 40f, null)),
            CreateEquippable("wb:eva_helmet", "helmet", (StatisticIds.STRENGTH, 2f, null)));

        var storage = new InMemoryLevelStorage("agg-rejoin-level");
        using TestHarness harness = CreateJoinHarness(storage, items);
        ServerPeer peer = harness.AddPeer(clientId: 1);

        var joinWithGear = new JoinRequest
        {
            LevelGuid = "agg-rejoin-level",
            CharacterUuid = 501UL,
            View = new CharacterView { CharacterUuid = 501UL, Name = "Nova", Body = "wb:human_f" },
            State = new CharacterState
            {
                CharacterUuid = 501UL,
                Name = "Nova",
                Body = "wb:human_f",
                GameMode = GameMode.Adventure,
                Statistics = [new Statistic(StatisticIds.STRENGTH, 3L)],
                Inventory =
                [
                    new KeyedSlotEntry(SlotGroups.WEARABLES, 0, new ItemData("wb:eva_helmet", 1, 1)),
                    new KeyedSlotEntry(SlotGroups.WEARABLES, 4, new ItemData("wb:eva_suit", 1, 1)),
                ],
            },
        };

        RpcResult<JoinAccept> firstJoin = await harness.JoinAsync(peer, joinWithGear);
        Assert.True(firstJoin.IsSuccess, firstJoin.ErrorMessage);
        int entity = harness.GetPlayerEntity(peer.GameId);
        harness.RunAggregation();

        Assert.True(harness.Store.TryGet(entity, out StatisticsComponent stats));
        //  Strength: 3 + 2 (helmet) = 5 -> +20 health_max from Strength + 40 from suit = 160 health_max.
        Assert.Equal(5L, stats.GetValue(StatisticIds.STRENGTH));
        Assert.Equal(160f, stats.GetFixed(StatisticIds.HEALTH_MAX), precision: 3);

        //  Heal up to 145 HP (above base max 100) and leave.
        harness.Store.SetStatisticFixed(entity, StatisticIds.HEALTH, 145f);
        harness.JoinQueue.EnqueueLeave(peer.GameId);
        harness.TickJoin();

        //  Rejoin with the same gear: strength must stay 5 (not 7), health_max 160 (not 200), health preserved at 145.
        RpcResult<JoinAccept> rejoinWithGear = await harness.JoinAsync(peer, joinWithGear);
        Assert.True(rejoinWithGear.IsSuccess, rejoinWithGear.ErrorMessage);
        int rejoinedEntity = harness.GetPlayerEntity(peer.GameId);
        harness.RunAggregation();

        Assert.True(harness.Store.TryGet(rejoinedEntity, out stats));
        Assert.Equal(5L, stats.GetValue(StatisticIds.STRENGTH));
        Assert.Equal(160f, stats.GetFixed(StatisticIds.HEALTH_MAX), precision: 3);
        Assert.Equal(145f, stats.GetFixed(StatisticIds.HEALTH), precision: 3);

        //  Leave and rejoin without gear: base stats restore cleanly and 145 HP clamps down to 110 (base 100 + Strength(3->+10)).
        harness.JoinQueue.EnqueueLeave(peer.GameId);
        harness.TickJoin();

        var joinWithoutGear = new JoinRequest
        {
            LevelGuid = joinWithGear.LevelGuid,
            CharacterUuid = joinWithGear.CharacterUuid,
            View = joinWithGear.View,
            State = joinWithGear.State with { Inventory = [] },
        };

        RpcResult<JoinAccept> rejoinBare = await harness.JoinAsync(peer, joinWithoutGear);
        Assert.True(rejoinBare.IsSuccess, rejoinBare.ErrorMessage);
        int bareEntity = harness.GetPlayerEntity(peer.GameId);
        harness.RunAggregation();

        Assert.True(harness.Store.TryGet(bareEntity, out stats));
        Assert.Equal(3L, stats.GetValue(StatisticIds.STRENGTH));
        Assert.Equal(110f, stats.GetFixed(StatisticIds.HEALTH_MAX), precision: 3);
        Assert.Equal(110f, stats.GetFixed(StatisticIds.HEALTH), precision: 3);
    }

    [Fact]
    public void ConsumableBuffGrantAndSurvivalExpiryRecomputeDerivedStats()
    {
        var items = new StubItemDatabase(
            new Item(
                "wb:stim",
                "item.stim",
                null,
                5,
                null,
                null,
                null,
                null,
                consumable: new ConsumableDefinition
                {
                    BuffID = "wb:stim_buff",
                    CooldownSeconds = 0f,
                }));

        var buffs = new StubBuffDatabase();
        buffs.Add(new Buff
        {
            ID = "wb:stim_buff",
            Name = "buff.stim",
            DurationSeconds = 1f,
            StackPolicy = BuffStackPolicy.Refresh,
            StatModifiers =
            [
                new BuffStatModifier(StatisticIds.HEALTH_MAX, FlatDelta: 30f),
                new BuffStatModifier(StatisticIds.RESOLVE, FlatDelta: 2f),
            ],
        }.Validate().Value);

        var store = new DataStore();
        var physics = new SteppingFakePhysics();
        using var simStep = new SimulationStep(store, physics);
        simStep.AlignTo(100);
        var config = new GameplayConfig();
        var survival = new ServerSurvivalSystem(simStep, config, buffs);
        var aggregation = new ServerStatAggregationSystem(simStep, items, buffs, config);
        var consumeHandler = new ConsumeItemInteractionHandler(items, buffs);

        int player = store.Alloc(Uuid.FromValue(701));
        store.AddOrUpdate(player, new OwnedCharacterComponent());
        store.AddOrUpdate(player, new GameModeComponent(GameMode.Adventure));
        store.AddOrUpdate(player, new EquipmentComponent(0));
        InventoryComponent inv = InventoryComponent.CreatePlayer();
        inv.Set(new SlotId(SlotGroups.HOTBAR, 0), new ItemData("wb:stim", 2, 5));
        store.AddOrUpdate(player, inv);
        CharacterStatisticsSeeder.SeedAuthoritativeStatistics(store, player, new CharacterState(), GameMode.Adventure);
        store.SetStatisticFlag(player, StatisticIds.PRESSURIZED, true);

        aggregation.Tick(1f / 60f, store);
        Assert.True(store.TryGet(player, out StatisticsComponent stats));
        Assert.Equal(100f, stats.GetFixed(StatisticIds.HEALTH_MAX), precision: 3);

        var request = new InteractionRequest(Vector3.Zero, null, InteractionKind.SecondaryPressed, null, GameMode.Adventure, CharacterDefaults.DEFAULT_REACH);
        var context = new InteractionContext(store, player, Uuid.FromValue(701), 1, null!, in request, "wb:stim", simStep.CurrentSimTick, physics.FixedTimestamp);
        consumeHandler.Handle(ref context);

        aggregation.Tick(1f / 60f, store);
        Assert.True(store.TryGet(player, out stats));
        Assert.Equal(3L, stats.GetValue(StatisticIds.RESOLVE));
        //  HealthMax: Base 100 + Resolve(3->+10) + Buff(+30) = 140.
        Assert.Equal(140f, stats.GetFixed(StatisticIds.HEALTH_MAX), precision: 3);

        //  Heal up to 135 while buffed, then advance past buff expiry -> Survival prunes, Aggregation drops +30 & clamps to 100.
        store.SetStatisticFixed(player, StatisticIds.HEALTH, 135f);
        Assert.True(stats.TryGetBuffExpiresTick("wb:stim_buff", out long expiresTick));
        simStep.AlignTo((uint)expiresTick);
        survival.Tick(1f / 60f, store);
        aggregation.Tick(1f / 60f, store);

        Assert.True(store.TryGet(player, out stats));
        Assert.Equal(1L, stats.GetValue(StatisticIds.RESOLVE));
        Assert.Equal(100f, stats.GetFixed(StatisticIds.HEALTH_MAX), precision: 3);
        Assert.Equal(100f, stats.GetFixed(StatisticIds.HEALTH), precision: 3);
    }

    private static int AllocateMotionPlayer(DataStore store, float moveSpeedPct)
    {
        int entity = store.Alloc(Uuid.NewUuid());
        store.AddOrUpdate(entity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(entity, new PhysicsComponent());
        store.AddOrUpdate(entity, new InputComponent());

        var net = NetworkComponent.Create();
        var input = new InputComponent { MovementZ = 1f, DampenersDisabled = true };
        net.StagedInputs!.Stage(in input, stagedAtSimTick: 1u);
        store.AddOrUpdate(entity, net);

        var stats = new StatisticsComponent();
        stats.SetFixed(StatisticIds.MOVE_SPEED_PCT, moveSpeedPct);
        store.AddOrUpdate(entity, stats);
        return entity;
    }

    private static Item CreateEquippable(string id, string slotTag, params (string StatId, float? Flat, float? Pct)[] modifiers)
    {
        ItemTags.TryResolveEquipTag(slotTag, out string resolvedTag);
        var list = new List<EquipStatModifierDefinition>(modifiers.Length);
        for (var i = 0; i < modifiers.Length; i++)
        {
            list.Add(new EquipStatModifierDefinition
            {
                StatId = modifiers[i].StatId,
                FlatDelta = modifiers[i].Flat,
                PercentDelta = modifiers[i].Pct,
            });
        }

        return new Item(
            id,
            $"item.{id}",
            null,
            1,
            null,
            null,
            null,
            null,
            equip: new EquipDefinition
            {
                SlotTag = resolvedTag,
                StatModifiers = list,
            });
    }

    private static SlotGroupSchemaDatabase LoadShippedSchemas()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(Path.Combine(AppContext.BaseDirectory, "TestFiles", "Items", "assets")));
        var parser = new VirtualFileParseService([new TomlParser<SlotGroupDefinitions>()], vfs);
        return new SlotGroupSchemaDatabase(NullLogger<SlotGroupSchemaDatabase>.Instance, parser, vfs);
    }

    private static TestHarness CreateJoinHarness(InMemoryLevelStorage storage, IItemDatabase items)
    {
        var store = new DataStore();
        var sessions = new SessionManager();
        var physics = new SteppingFakePhysics();
        var simulationStep = new SimulationStep(store, physics);
        var saveService = new LevelSaveService(
            NullLogger.Instance,
            new SingleLevelCatalog(storage),
            BrickRegistry.FromNames(["test"]),
            new FakeBrickDatabase(),
            new GameplayConfig());
        var peers = new ServerPeers();
        var registry = new NetworkRegistry();
        var replication = new NetworkReplicationSystem(peers, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingConfig(), registry);
        var joinQueue = new ServerJoinQueue();
        var permissions = new UserPermissionService(new PermissionResolver(PermissionPolicy.Create([])));
        var joinSystem = new ServerJoinSystem(
            sessions,
            saveService,
            replication,
            simulationStep,
            NullLogger<ServerJoinSystem>.Instance,
            new GameplayConfig(),
            new StubSkillDatabase(),
            joinQueue,
            peers,
            permissions,
            SlotGroupSchemaDatabase.Default,
            items);
        var aggregation = new ServerStatAggregationSystem(simulationStep, items);

        return new TestHarness(store, sessions, saveService, peers, joinQueue, joinSystem, aggregation);
    }

    private sealed class TestHarness(
        DataStore store,
        SessionManager sessions,
        LevelSaveService saveService,
        ServerPeers peers,
        ServerJoinQueue joinQueue,
        ServerJoinSystem joinSystem,
        ServerStatAggregationSystem aggregation
    ) : IDisposable
    {
        private readonly FakeHost _host = new();

        public DataStore Store { get; } = store;
        public ServerJoinQueue JoinQueue { get; } = joinQueue;

        public ServerPeer AddPeer(ulong clientId)
        {
            return peers.GetOrAdd(_host, new RpcPeer(new FakeServerConnection(clientId)), isLocal: false);
        }

        public async Task<RpcResult<JoinAccept>> JoinAsync(ServerPeer peer, JoinRequest request)
        {
            var completion = new TaskCompletionSource<RpcResult<JoinAccept>>(TaskCreationOptions.RunContinuationsAsynchronously);
            JoinQueue.Enqueue(peer, request, completion, CancellationToken.None);
            joinSystem.Tick(1f / 60f, Store);
            return await completion.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }

        public int GetPlayerEntity(Uuid clientId)
        {
            Assert.True(sessions.TryGetEntity(clientId, out int entity));
            return entity;
        }

        public void RunAggregation() => aggregation.Tick(1f / 60f, Store);

        public void TickJoin() => joinSystem.Tick(1f / 60f, Store);

        public void Dispose() => saveService.Dispose();
    }

    private sealed class StubItemDatabase(params Item[] items) : IItemDatabase
    {
        private readonly Dictionary<string, Item> _items = BuildMap(items);

        public Result<Item> Get(string id)
        {
            return _items.TryGetValue(id, out Item? item)
                ? Result<Item>.FromSuccess(item)
                : Result<Item>.FromFailure($"Unknown item \"{id}\".");
        }

        private static Dictionary<string, Item> BuildMap(Item[] items)
        {
            var map = new Dictionary<string, Item>(items.Length, StringComparer.Ordinal);
            for (var i = 0; i < items.Length; i++)
            {
                map[items[i].ID] = items[i];
            }

            return map;
        }
    }

    private sealed class StubBuffDatabase : IBuffDatabase
    {
        private readonly List<Buff> _buffs = [];
        private readonly Dictionary<string, Buff> _byId = new(StringComparer.Ordinal);

        public IReadOnlyList<Buff> All => _buffs;

        public void Add(Buff buff)
        {
            _buffs.Add(buff);
            _byId[buff.ID] = buff;
        }

        public Result<Buff> Get(string id)
        {
            return _byId.TryGetValue(id, out Buff buff)
                ? Result<Buff>.FromSuccess(buff)
                : Result<Buff>.FromFailure($"Unknown buff \"{id}\".");
        }
    }

    private sealed class TempItemAssets : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"wb-equip-{Path.GetRandomFileName()}");

        public TempItemAssets(string toml)
        {
            Directory.CreateDirectory(Path.Combine(Root, "items"));
            File.WriteAllText(Path.Combine(Root, "items", "items.toml"), toml);
        }

        public ItemDatabase LoadItems()
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(Root));
            IFileParser[] parsers = [new TomlParser<ItemDefinitions>()];
            var parser = new VirtualFileParseService(parsers, vfs);
            return new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Root))
                {
                    Directory.Delete(Root, recursive: true);
                }
            }
            catch
            {
                //  Best-effort temp cleanup.
            }
        }
    }

    private sealed class SteppingFakePhysics : IServerWorldPhysics
    {
        public float FixedTimestamp => 1f / 64f;

        public event EventHandler<EventArgs>? FixedUpdate;

        public RaycastResult Raycast(in Ray ray) => default;

        public bool Overlaps(in ShapeOverlapQuery query) => false;

        public void SetGravity(Vector3 gravity) { }

        public void Tick(float delta, DataStore store) { }

        public void Step() => FixedUpdate?.Invoke(this, EventArgs.Empty);
    }

    private sealed class SingleLevelCatalog(InMemoryLevelStorage storage) : ILevelCatalog
    {
        public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
        {
            levelGuid = storage.LevelGuid;
            return true;
        }

        public Level[] ListLevels() => [];

        public bool Delete(string levelGuid) => true;

        public bool Exists(string levelGuid) => levelGuid == storage.LevelGuid;

        public ILevelStorage? Open(string levelGuid) => levelGuid == storage.LevelGuid ? storage : null;
    }

    private sealed class InMemoryLevelStorage(string levelGuid) : ILevelStorage
    {
        private readonly Lock _lock = new();
        private readonly Dictionary<ulong, CharacterEntityData> _characters = [];
        private Level _level = new()
        {
            Guid = levelGuid,
            Name = "Stat Aggregation Test Level",
            SpawnX = 0,
            SpawnY = 10,
            SpawnZ = 0,
            DefaultGameMode = GameMode.Adventure,
        };

        public string LevelGuid { get; } = levelGuid;

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
                    : Result<CharacterEntityData>.FromFailure("Location not found.");
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

    private sealed class FakeBrickDatabase : IBrickDatabase
    {
        public Result<Brick> Get(string id) => Result<Brick>.FromFailure((string?)null);

        public Result<Brick> Get(ushort id) => Result<Brick>.FromFailure((string?)null);

        public List<Brick> Get(Func<Brick, bool> predicate) => [];

        public bool IsCuller(in Voxel voxel, BrickShape shape) => false;
    }

    private sealed class FakeHost : IRpcHost
    {
        public PeerSet Peers => default;
    }

    private sealed class FakeServerConnection(ulong clientId) : RpcServerConnection
    {
        public ulong ClientId { get; } = clientId;
        public EndPoint? RemoteEndPoint => null;
        public bool IsConnected => true;

        public void Close(RpcDisconnectReason reason = RpcDisconnectReason.LocalClose) { }

        public bool TrySend(ReadOnlySpan<byte> frame) => true;

        public event Action<RpcDisconnectReason>? Disconnected { add { } remove { } }

        public event RpcFrameHandler? FrameReceived { add { } remove { } }
    }
}
