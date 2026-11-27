#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using DryIoc;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using NsdRpc;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.Particles;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Library.Collections;
using Swordfish.Library.Events;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Tests;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Interactions;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Statistics;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.Voxels;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Gameplay.Statistics;
using WaywardBeyond.Items;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Interactions;
using WaywardBeyond.Server.Particles;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Skills;
using WaywardBeyond.Voxels;
using Xunit;
using WaywardBeyondClient = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>Tests Adventure Mode timed brick breaking, duration math, server validation, client progress, and laser visuals.</summary>
[Collection("ClientGameState")]
public class TimedBrickBreakingTests
{
    private const string ROCK_BRICK_ID = "wb:rock";
    private const string PANEL_BRICK_ID = "wb:panel";
    private const string LASER_ITEM_ID = "wb:laser";
    private const string FAST_LASER_ITEM_ID = "wb:fast_laser";

    [Fact]
    public void BrickDatabase_LoadsStatistics_CustomKeysAndFiltersNonFinite()
    {
        using TempAssetDir temp = new();
        temp.WriteBricks("""
            [[Bricks]]
            ID = "wb:default_brick"

            [[Bricks]]
            ID = "wb:hard_brick"
            [Bricks.Statistics]
            "wb:hardness" = 2.75
            "mod:blast_resistance" = 12.5

            [[Bricks]]
            ID = "wb:fragile_brick"
            [Bricks.Statistics]
            "wb:hardness" = 0.0

            [[Bricks]]
            ID = "wb:bad_stats_brick"
            [Bricks.Statistics]
            "wb:hardness" = 1.5
            "mod:bad_nan" = nan
            "mod:bad_inf" = inf
            """);

        BrickDatabase db = temp.LoadBricks();

        Brick defaultBrick = db.Get("wb:default_brick").Value;
        Assert.Empty(defaultBrick.Statistics);
        Assert.Equal(StatisticIds.DEFAULT_HARDNESS, defaultBrick.GetStatistic(StatisticIds.HARDNESS, StatisticIds.DEFAULT_HARDNESS));

        Brick hardBrick = db.Get("wb:hard_brick").Value;
        Assert.Equal(2.75f, hardBrick.GetStatistic(StatisticIds.HARDNESS));
        Assert.Equal(12.5f, hardBrick.GetStatistic("mod:blast_resistance"));

        Brick fragileBrick = db.Get("wb:fragile_brick").Value;
        Assert.Equal(0f, fragileBrick.GetStatistic(StatisticIds.HARDNESS));

        Brick badStatsBrick = db.Get("wb:bad_stats_brick").Value;
        Assert.Equal(1.5f, badStatsBrick.GetStatistic(StatisticIds.HARDNESS));
        Assert.False(badStatsBrick.TryGetStatistic("mod:bad_nan", out _));
        Assert.False(badStatsBrick.TryGetStatistic("mod:bad_inf", out _));
    }

    [Fact]
    public void ItemDatabase_LoadsStatistics_CustomKeysAndFiltersNonFinite()
    {
        using TempAssetDir temp = new();
        temp.WriteItems("""
            [[Items]]
            ID = "wb:laser_default"
            Name = "item.default"
            [Items.Tool]
            Type = "Mining"

            [[Items]]
            ID = "wb:laser_fast"
            Name = "item.fast"
            [Items.Tool]
            Type = "Mining"
            [Items.Statistics]
            "wb:mining_speed" = 2.5
            "mod:beam_heat" = -1.25

            [[Items]]
            ID = "wb:custom_stat_item"
            Name = "item.custom"
            [Items.Statistics]
            "mod:power" = 42.0
            "mod:bad_nan" = nan
            "mod:bad_inf" = inf
            """);

        ItemDatabase db = temp.LoadItems();

        Item defaultLaser = db.Get("wb:laser_default").Value;
        Assert.Empty(defaultLaser.Statistics);
        Assert.Equal(StatisticIds.DEFAULT_MINING_SPEED, defaultLaser.GetStatistic(StatisticIds.MINING_SPEED, StatisticIds.DEFAULT_MINING_SPEED));

        Item fastLaser = db.Get("wb:laser_fast").Value;
        Assert.Equal(2.5f, fastLaser.GetStatistic(StatisticIds.MINING_SPEED));
        Assert.Equal(-1.25f, fastLaser.GetStatistic("mod:beam_heat"));

        Item customItem = db.Get("wb:custom_stat_item").Value;
        Assert.Equal(42.0f, customItem.GetStatistic("mod:power"));
        Assert.False(customItem.TryGetStatistic("mod:bad_nan", out _));
        Assert.False(customItem.TryGetStatistic("mod:bad_inf", out _));
    }

    [Fact]
    public void StatResolver_ComposesItemEntitySkillAndModModifierSources()
    {
        using ServerHarness harness = new(GameMode.Adventure, rockHardness: 2.0f, panelHardness: 2.0f, laserSpeed: 1.0f, fastLaserSpeed: 2.0f);
        var miningXp = new Dictionary<string, long> { ["mining"] = 10_000 };
        int miningLevel = harness.Skills.Get("mining").Value.CalculateLevel(10_000).Level;
        Assert.True(miningLevel > 0);

        //  Add a custom mod modifier source applying +50% to mining speed, *1.5 to brick hardness, and *2 to a custom mod stat.
        var hasteMod = new DelegateStatModifierSource(
            StatModifierPriority.Normal,
            StatisticIds.MINING_SPEED,
            (in StatQuery _, ref StatAccumulator acc) => acc.Apply(StatModifier.AddPercent(0.50f)));
        var reinforcedMod = new DelegateStatModifierSource(
            StatModifierPriority.Normal,
            StatisticIds.HARDNESS,
            (in StatQuery _, ref StatAccumulator acc) => acc.Apply(StatModifier.Multiply(1.5f)));
        var customStatMod = new DelegateStatModifierSource(
            StatModifierPriority.Normal,
            "mod:jump_boost",
            (in StatQuery _, ref StatAccumulator acc) => acc.Apply(StatModifier.Multiply(2.0f)));

        var resolver = new StatResolver(
        [
            new BrickStatisticModifierSource(harness.Bricks),
            new ItemStatisticModifierSource(harness.Items),
            new EntityStatisticModifierSource(),
            new SkillMiningSpeedModifierSource(harness.Skills),
            hasteMod,
            reinforcedMod,
            customStatMod,
        ]);

        //  Brick hardness flows through the same stat modifier pipeline (base 2.0 * 1.5 reinforced mod = 3.0).
        float rockHardness = resolver.Evaluate(new StatQuery(
            StatisticIds.HARDNESS,
            StatisticIds.DEFAULT_HARDNESS,
            subject: StatSubject.Brick,
            targetBrickDataId: harness.RockId));
        Assert.Equal(3.0f, rockHardness, precision: 5);

        //  Fast laser (SetBase 2.0) + entity stat (+0.5) + mining skill (+2%/lvl) + mod haste (+50%).
        var entityStats = new StatisticsComponent();
        entityStats.SetFixed(StatisticIds.MINING_SPEED, 0.5f);
        entityStats.SetFixed("mod:jump_boost", 3.0f);
        harness.Store.AddOrUpdate(harness.PlayerEntity, entityStats);
        harness.Store.AddOrUpdate(harness.PlayerEntity, new SkillStateComponent(miningXp));

        float rockSpeed = resolver.Evaluate(new StatQuery(
            StatisticIds.MINING_SPEED,
            StatisticIds.DEFAULT_MINING_SPEED,
            subject: StatSubject.Item,
            store: harness.Store,
            entity: harness.PlayerEntity,
            heldItemId: FAST_LASER_ITEM_ID,
            targetBrickDataId: harness.RockId));
        float expectedRockSpeed = (2.0f + 0.5f) * (1.0f + (miningLevel * SkillMiningSpeedModifierSource.SKILL_SPEED_BONUS_PER_LEVEL) + 0.50f);
        Assert.Equal(expectedRockSpeed, rockSpeed, precision: 5);

        //  Manufactured panel gets no mining skill bonus, only base + entity + haste.
        float panelSpeed = resolver.Evaluate(new StatQuery(
            StatisticIds.MINING_SPEED,
            StatisticIds.DEFAULT_MINING_SPEED,
            subject: StatSubject.Item,
            store: harness.Store,
            entity: harness.PlayerEntity,
            heldItemId: FAST_LASER_ITEM_ID,
            targetBrickDataId: harness.PanelId));
        Assert.Equal((2.0f + 0.5f) * 1.50f, panelSpeed, precision: 5);

        //  Arbitrary custom mod statistic flows through the exact same pipeline.
        float jumpBoost = resolver.Evaluate(new StatQuery(
            "mod:jump_boost",
            defaultBase: 1.0f,
            subject: StatSubject.Entity,
            store: harness.Store,
            entity: harness.PlayerEntity));
        Assert.Equal((1.0f + 3.0f) * 2.0f, jumpBoost, precision: 5);

        //  Float accumulation drift tolerance completes at target without triggering early.
        var accumulated = 0f;
        for (var i = 0; i < 49; i++)
        {
            accumulated += 0.02f;
        }
        Assert.False(TimedInteractionProgress.IsComplete(accumulated, 1.0f));
        accumulated += 0.02f;
        Assert.True(TimedInteractionProgress.IsComplete(accumulated, 1.0f));
    }

    [Fact]
    public void StatSubject_PreventsBrickAndItemBaseCollisionsWhenBothArePresent()
    {
        var brick = new Brick(
            "mod:test_brick",
            BrickAlpha.Opaque,
            passable: false,
            meshID: null,
            BrickShape.Block,
            default,
            statistics: new Dictionary<string, float> { ["mod:shared"] = 10f });
        var item = new Item(
            "mod:test_item",
            "item.test",
            iconID: null,
            maxStack: 1,
            placeable: null,
            tool: null,
            viewModel: null,
            worldModel: null,
            statistics: new Dictionary<string, float> { ["mod:shared"] = 3f });

        var store = new DataStore();
        int entity = store.Alloc();
        var entityStats = new StatisticsComponent();
        entityStats.SetFixed("mod:shared", 2f);
        store.AddOrUpdate(entity, entityStats);

        var resolver = new StatResolver(
        [
            new BrickStatisticModifierSource(),
            new ItemStatisticModifierSource(),
            new EntityStatisticModifierSource(),
        ]);

        float brickValue = resolver.Evaluate(new StatQuery("mod:shared", defaultBase: 1f, subject: StatSubject.Brick, store: store, entity: entity, heldItem: item, targetBrick: brick));
        float itemValue = resolver.Evaluate(new StatQuery("mod:shared", defaultBase: 1f, subject: StatSubject.Item, store: store, entity: entity, heldItem: item, targetBrick: brick));
        float entityValue = resolver.Evaluate(new StatQuery("mod:shared", defaultBase: 1f, subject: StatSubject.Entity, store: store, entity: entity, heldItem: item, targetBrick: brick));

        Assert.Equal(12f, brickValue, precision: 5);
        Assert.Equal(5f, itemValue, precision: 5);
        Assert.Equal(3f, entityValue, precision: 5);
    }

    [Fact]
    public void ServerInteractionSystem_CachesInteractionWorldAcrossTimedHoldTicks()
    {
        using ServerHarness harness = new(GameMode.Adventure, rockHardness: 2.0f, panelHardness: 2.0f, laserSpeed: 1.0f, fastLaserSpeed: 2.0f);
        harness.SetHeldSlot(0, LASER_ITEM_ID);
        harness.SetPrimaryHeld(true);
        harness.StageInteraction(InteractionKind.PrimaryPressed, harness.StructureUuid, new Int3(0, 0, -4));

        for (var i = 0; i < 10; i++)
        {
            harness.TickServer(0.2f);
        }

        Assert.Equal((ushort)0, harness.GetVoxel(0, 0, -4).ID);
        Assert.Equal(1, harness.WorldFactoryCalls);
    }

    [Fact]
    public void ServerInteractionSystem_CreativeMode_PreservesInstantBreak()
    {
        using ServerHarness harness = new(GameMode.Creative, rockHardness: 5.0f, panelHardness: 1.0f, laserSpeed: 1.0f, fastLaserSpeed: 2.0f);
        harness.SetHeldSlot(0, LASER_ITEM_ID);
        harness.SetPrimaryHeld(false);

        harness.StageInteraction(InteractionKind.PrimaryPressed, harness.StructureUuid, new Int3(0, 0, -4));
        harness.TickServer(0.016f);

        Assert.Equal((ushort)0, harness.GetVoxel(0, 0, -4).ID);
        Assert.False(harness.Interactions.GetTimedSession(harness.PlayerUuid).Success);
        Assert.Equal(0, harness.GetInventoryCount(ROCK_BRICK_ID));
    }

    [Fact]
    public void ServerInteractionSystem_AdventureMode_CompletesTimedHoldAndGrantsLootAndXp()
    {
        using ServerHarness harness = new(GameMode.Adventure, rockHardness: 2.0f, panelHardness: 2.0f, laserSpeed: 1.0f, fastLaserSpeed: 2.0f);
        harness.SetHeldSlot(0, LASER_ITEM_ID);
        harness.SetPrimaryHeld(true);

        //  Single PrimaryPressed starts the timed hold; zero additional interaction events are sent mid-hold.
        harness.StageInteraction(InteractionKind.PrimaryPressed, harness.StructureUuid, new Int3(0, 0, -4));
        for (var i = 0; i < 10; i++)
        {
            harness.TickServer(0.1f);
        }

        Assert.Equal(harness.RockId, harness.GetVoxel(0, 0, -4).ID);
        Result<ServerInteractionSystem.TimedSessionState> midSession = harness.Interactions.GetTimedSession(harness.PlayerUuid);
        Assert.True(midSession.Success);
        Assert.Equal(1.0f, midSession.Value.Elapsed, precision: 3);
        Assert.Equal(2.0f, midSession.Value.RequiredDuration, precision: 3);
        Assert.Equal(0.5f, midSession.Value.Progress, precision: 3);
        Assert.Equal(0, harness.GetInventoryCount(ROCK_BRICK_ID));
        Assert.Equal(0, harness.GetSkillXp("mining"));

        for (var i = 0; i < 10; i++)
        {
            harness.TickServer(0.1f);
        }

        Assert.Equal((ushort)0, harness.GetVoxel(0, 0, -4).ID);
        Assert.False(harness.Interactions.GetTimedSession(harness.PlayerUuid).Success);
        Assert.Equal(1, harness.GetInventoryCount(ROCK_BRICK_ID));
        Assert.True(harness.GetSkillXp("mining") > 0);
    }

    [Fact]
    public void ServerInteractionSystem_AdventureMode_AppliesToolSpeedAndSkillScaling()
    {
        using ServerHarness harness = new(GameMode.Adventure, rockHardness: 3.0f, panelHardness: 2.0f, laserSpeed: 1.0f, fastLaserSpeed: 2.0f);
        harness.SetHeldSlot(0, FAST_LASER_ITEM_ID);
        harness.SetSkillXp("mining", 200_000);
        int level = harness.Skills.Get("mining").Value.CalculateLevel(200_000).Level;
        float expectedSpeed = 2.0f * (1.0f + (level * SkillMiningSpeedModifierSource.SKILL_SPEED_BONUS_PER_LEVEL));
        float expectedDuration = 3.0f / expectedSpeed;
        Assert.True(expectedDuration < 1.5f);

        harness.SetPrimaryHeld(true);
        harness.StageInteraction(InteractionKind.PrimaryPressed, harness.StructureUuid, new Int3(0, 0, -4));

        harness.TickServer(expectedDuration * 0.5f);
        Assert.Equal(harness.RockId, harness.GetVoxel(0, 0, -4).ID);

        harness.TickServer(expectedDuration * 0.6f);
        Assert.Equal((ushort)0, harness.GetVoxel(0, 0, -4).ID);
    }

    [Fact]
    public void ServerInteractionSystem_CustomModTimedInteraction_ExecutesCommitAfterHold()
    {
        var committed = 0;
        var customValidator = new CustomTimedValidateHandler("mod:charge_action", duration: 0.5f);
        var customCommitter = new CustomTimedCommitHandler(() => committed++);

        using ServerHarness harness = new(
            GameMode.Creative,
            rockHardness: 2.0f,
            panelHardness: 2.0f,
            laserSpeed: 1.0f,
            fastLaserSpeed: 2.0f,
            extraHandlers: [customValidator, customCommitter]);

        harness.SetHeldSlot(0, LASER_ITEM_ID);
        harness.SetSecondaryHeld(true);
        harness.StageInteraction(InteractionKind.SecondaryPressed, harness.StructureUuid, new Int3(0, 0, -4));

        harness.TickServer(0.25f);
        Assert.Equal(0, committed);
        Result<ServerInteractionSystem.TimedSessionState> session = harness.Interactions.GetTimedSession(harness.PlayerUuid);
        Assert.True(session.Success);
        Assert.Equal("mod:charge_action", session.Value.Key.HandlerId);
        Assert.Equal(0.5f, session.Value.Progress, precision: 3);

        harness.TickServer(0.25f);
        Assert.Equal(1, committed);
        Assert.False(harness.Interactions.GetTimedSession(harness.PlayerUuid).Success);
    }

    [Fact]
    public void ServerInteractionSystem_AdventureMode_ResetsOnReleaseRetargetSlotSwitchAndOutOfReach()
    {
        using ServerHarness harness = new(GameMode.Adventure, rockHardness: 2.0f, panelHardness: 2.0f, laserSpeed: 1.0f, fastLaserSpeed: 2.0f);
        harness.SetHeldSlot(0, LASER_ITEM_ID);
        harness.SetHeldSlot(1, FAST_LASER_ITEM_ID);
        harness.SetActiveSlot(0);

        //  1. Tap without PrimaryHeld does not start a session or break.
        harness.SetPrimaryHeld(false);
        harness.StageInteraction(InteractionKind.PrimaryPressed, harness.StructureUuid, new Int3(0, 0, -4));
        harness.TickServer(0.5f);
        Assert.False(harness.Interactions.GetTimedSession(harness.PlayerUuid).Success);
        Assert.Equal(harness.RockId, harness.GetVoxel(0, 0, -4).ID);

        //  2. Release resets accumulated progress so re-holding requires full duration again.
        harness.SetPrimaryHeld(true);
        harness.StageInteraction(InteractionKind.PrimaryPressed, harness.StructureUuid, new Int3(0, 0, -4));
        harness.TickServer(1.2f);
        Assert.True(harness.Interactions.GetTimedSession(harness.PlayerUuid).Success);

        harness.SetPrimaryHeld(false);
        harness.TickServer(0.1f);
        Assert.False(harness.Interactions.GetTimedSession(harness.PlayerUuid).Success);

        harness.SetPrimaryHeld(true);
        harness.StageInteraction(InteractionKind.PrimaryPressed, harness.StructureUuid, new Int3(0, 0, -4));
        harness.TickServer(1.2f);
        Assert.Equal(harness.RockId, harness.GetVoxel(0, 0, -4).ID);

        //  3. Retargeting to another voxel resets progress to 0 for the new target.
        harness.StageInteraction(InteractionKind.PrimaryPressed, harness.StructureUuid, new Int3(1, 0, -4));
        harness.TickServer(0.25f);
        Result<ServerInteractionSystem.TimedSessionState> retargeted = harness.Interactions.GetTimedSession(harness.PlayerUuid);
        Assert.True(retargeted.Success);
        Assert.Equal(new Int3(1, 0, -4), retargeted.Value.Key.TargetCoord);
        Assert.Equal(0.25f, retargeted.Value.Elapsed, precision: 3);

        //  4. Switching hotbar slot (even to another mining tool) resets progress immediately.
        harness.SetActiveSlot(1);
        harness.TickServer(0.1f);
        Assert.False(harness.Interactions.GetTimedSession(harness.PlayerUuid).Success);

        //  5. Moving out of DEFAULT_REACH resets progress immediately.
        harness.SetActiveSlot(0);
        harness.StageInteraction(InteractionKind.PrimaryPressed, harness.StructureUuid, new Int3(0, 0, -4));
        harness.TickServer(0.5f);
        Assert.True(harness.Interactions.GetTimedSession(harness.PlayerUuid).Success);

        harness.SetPlayerPosition(new Vector3(0f, 0f, 15f));
        harness.TickServer(0.1f);
        Assert.False(harness.Interactions.GetTimedSession(harness.PlayerUuid).Success);
        Assert.Equal(harness.RockId, harness.GetVoxel(0, 0, -4).ID);
    }

    [Fact]
    public void PlayerInteractionService_AdventureMode_TracksProgressWithoutChatterAndResets()
    {
        GameState previous = WaywardBeyondClient.GameState.Get();
        WaywardBeyondClient.GameState.Set(GameState.Playing);
        try
        {
            using ClientHarness harness = new(GameMode.Adventure, rockHardness: 2.0f, laserSpeed: 1.0f);
            harness.SetSkillXp("mining", 0);

            //  Press and hold LMB on brick at (0, 0, -4).
            harness.SetPrimaryHeld(true, firePressTrigger: true);
            for (var i = 0; i < 4; i++)
            {
                harness.TickClient(0.25f);
            }

            Assert.Equal(0.5f, harness.InteractionState.HoldProgress.Get(), precision: 3);
            Assert.Equal(harness.RockId, harness.Voxels.Get(0, 0, -4).ID);
            Assert.Empty(harness.BreakEvents);
            Assert.Single(harness.StagedEvents, e => e.Kind == InteractionKind.PrimaryPressed);

            //  Retargeting to (1, 0, -4) resets progress to the new brick's elapsed duration and sends one PrimaryPressed.
            harness.SetRaycastTarget(new Int3(1, 0, -4));
            harness.TickClient(0.25f);
            Assert.Equal(0.125f, harness.InteractionState.HoldProgress.Get(), precision: 3);
            Assert.Equal(2, harness.CountStaged(InteractionKind.PrimaryPressed));

            //  Moving out of DEFAULT_REACH cancels hold, stages PrimaryReleased, and resets progress to 0.
            harness.SetPlayerPosition(new Vector3(0f, 0f, 16f));
            harness.TickClient(0.25f);
            Assert.Equal(0f, harness.InteractionState.HoldProgress.Get());
            Assert.Equal(1, harness.CountStaged(InteractionKind.PrimaryReleased));

            //  Move back in reach, hold halfway, then switch hotbar slot -> resets progress to 0 and emits PrimaryReleased.
            harness.SetPlayerPosition(Vector3.Zero);
            harness.SetRaycastTarget(new Int3(0, 0, -4));
            harness.TickClient(0.5f);
            Assert.Equal(0.25f, harness.InteractionState.HoldProgress.Get(), precision: 3);

            harness.SetActiveSlot(1);
            harness.TickClient(0.1f);
            Assert.Equal(0f, harness.InteractionState.HoldProgress.Get());
            Assert.Equal(2, harness.CountStaged(InteractionKind.PrimaryReleased));

            //  Switch back to mining tool, advance halfway, then replace voxel underneath -> resets progress for new brick.
            harness.SetActiveSlot(0);
            harness.TickClient(0.5f);
            Assert.Equal(0.25f, harness.InteractionState.HoldProgress.Get(), precision: 3);

            ushort panelId = harness.Bricks.Id(PANEL_BRICK_ID);
            harness.Voxels.Set(0, 0, -4, new Voxel(panelId, new ShapeLight(BrickShape.Block, 0), 0));
            harness.TickClient(0.2f);
            Assert.Equal(0.1f, harness.InteractionState.HoldProgress.Get(), precision: 3);

            //  Releasing LMB resets progress to 0 and emits PrimaryReleased.
            harness.SetPrimaryHeld(false);
            harness.TickClient(0.1f);
            Assert.Equal(0f, harness.InteractionState.HoldProgress.Get());
            Assert.Equal(3, harness.CountStaged(InteractionKind.PrimaryReleased));

            //  Switching to Creative preserves instant predictive break and keeps HoldProgress at 0.
            harness.Voxels.Set(0, 0, -4, new Voxel(harness.RockId, new ShapeLight(BrickShape.Block, 0), 0));
            harness.SetGameMode(GameMode.Creative);
            harness.SetPrimaryHeld(true, firePressTrigger: true);
            harness.TickClient(0.016f);
            Assert.Equal((ushort)0, harness.Voxels.Get(0, 0, -4).ID);
            Assert.Single(harness.BreakEvents);
            Assert.Equal(0f, harness.InteractionState.HoldProgress.Get());
        }
        finally
        {
            WaywardBeyondClient.GameState.Set(previous);
        }
    }

    [Fact]
    public void ClientTimedInteractionController_SupportsCustomModTimedInteraction()
    {
        var custom = new CustomClientTimedInteraction("mod:scan_hold", duration: 1.0f);
        var controller = new ClientTimedInteractionController([custom]);
        var store = new DataStore();
        var context = new ClientTimedInteractionContext(
            store,
            playerEntity: 0,
            GameMode.Creative,
            activeSlot: 0,
            heldItemId: LASER_ITEM_ID,
            heldItem: null,
            heldPlaceable: null,
            tryRaycastBrick: (bool _, bool _, out Entity e, out Voxel v, out Int3 c, out BrickInteraction h) =>
            {
                e = default;
                v = default;
                c = default;
                h = default;
                return false;
            });

        IClientTimedInteraction? found = controller.FindActive(InteractionInput.Secondary, in context);
        Assert.Same(custom, found);

        float p1 = controller.Update(custom, isButtonHeld: true, delta: 0.4f, in context, out ClientTimedStageEvent e1);
        Assert.True(e1.HasEvent);
        Assert.Equal(InteractionKind.SecondaryPressed, e1.Kind);
        Assert.Equal(0.4f, p1, precision: 3);

        float p2 = controller.Update(custom, isButtonHeld: true, delta: 0.6f, in context, out ClientTimedStageEvent e2);
        Assert.False(e2.HasEvent);
        Assert.Equal(1.0f, p2, precision: 3);

        float p3 = controller.Update(custom, isButtonHeld: false, delta: 0.1f, in context, out ClientTimedStageEvent e3);
        Assert.True(e3.HasEvent);
        Assert.Equal(InteractionKind.SecondaryReleased, e3.Kind);
        Assert.Equal(0f, p3);
    }

    [Fact]
    public void StatisticsComposition_RegisterStatisticsAndModModifierSource_IsIdempotentAndComposes()
    {
        using var container = new Container();
        container.RegisterStatistics();
        container.RegisterStatistics();
        container.RegisterStatModifierSource<ModBonusStatSource>();
        container.RegisterStatModifierSource<ModBonusStatSource>();

        IStatResolver resolver = container.Resolve<IStatResolver>();
        Assert.Equal(5, resolver.GetSources().Count);

        float value = resolver.Evaluate(new StatQuery("mod:custom_stat", defaultBase: 10f));
        Assert.Equal(15f, value, precision: 5);
    }

    [Fact]
    public void InteractionInputExtensions_MapsEdgesAndFailsClosedForUnknownInput()
    {
        Assert.Equal(InteractionKind.PrimaryPressed, InteractionInput.Primary.ToPressedKind());
        Assert.Equal(InteractionKind.PrimaryReleased, InteractionInput.Primary.ToReleasedKind());
        Assert.Equal(InteractionKind.SecondaryPressed, InteractionInput.Secondary.ToPressedKind());
        Assert.Equal(InteractionKind.SecondaryReleased, InteractionInput.Secondary.ToReleasedKind());
        Assert.Equal(InteractionKind.None, InteractionInput.None.ToPressedKind());
        Assert.Equal(InteractionKind.None, InteractionInput.None.ToReleasedKind());

        Assert.Equal(InteractionInput.Primary, InteractionKind.PrimaryPressed.ToInput());
        Assert.Equal(InteractionInput.Primary, InteractionKind.PrimaryReleased.ToInput());
        Assert.Equal(InteractionInput.Secondary, InteractionKind.SecondaryPressed.ToInput());
        Assert.Equal(InteractionInput.Secondary, InteractionKind.SecondaryReleased.ToInput());
        Assert.Equal(InteractionInput.None, InteractionKind.None.ToInput());

        Assert.True(InteractionKind.PrimaryReleased.IsRelease());
        Assert.True(InteractionKind.SecondaryReleased.IsRelease());
        Assert.False(InteractionKind.PrimaryPressed.IsRelease());
        Assert.False(InteractionKind.SecondaryPressed.IsRelease());

        var bothHeld = new InputComponent { PrimaryHeld = true, SecondaryHeld = true };
        var noneHeld = new InputComponent { PrimaryHeld = false, SecondaryHeld = false };
        Assert.True(InteractionInput.None.IsHeld(in noneHeld));
        Assert.True(InteractionInput.Primary.IsHeld(in bothHeld));
        Assert.True(InteractionInput.Secondary.IsHeld(in bothHeld));
        Assert.False(InteractionInput.Primary.IsHeld(in noneHeld));
        Assert.False(InteractionInput.Secondary.IsHeld(in noneHeld));
        Assert.False(((InteractionInput)99).IsHeld(in bothHeld));
    }

    [Fact]
    public void PlayerInteractionService_SecondaryTimedInteractionRunsWhenPrimaryAlsoApplies()
    {
        GameState previous = WaywardBeyondClient.GameState.Get();
        WaywardBeyondClient.GameState.Set(GameState.Playing);
        try
        {
            var secondaryTimed = new CustomClientTimedInteraction("mod:scan_hold", duration: 1.0f);
            using ClientHarness harness = new(GameMode.Adventure, rockHardness: 2.0f, laserSpeed: 1.0f, extraTimedInteractions: [secondaryTimed]);

            //  Holding Secondary must activate the secondary timed interaction even though AdventureBreakTimedInteraction also applies to Primary.
            harness.SetSecondaryHeld(true, firePressTrigger: true);
            harness.TickClient(0.5f);
            Assert.Equal(0.5f, harness.InteractionState.HoldProgress.Get(), precision: 3);
            Assert.Equal(1, harness.CountStaged(InteractionKind.SecondaryPressed));
            Assert.Equal(0, harness.CountStaged(InteractionKind.PrimaryPressed));

            //  Releasing Secondary cancels cleanly.
            harness.SetSecondaryHeld(false);
            harness.TickClient(0.05f);
            Assert.Equal(0f, harness.InteractionState.HoldProgress.Get());
            Assert.Equal(1, harness.CountStaged(InteractionKind.SecondaryReleased));
        }
        finally
        {
            WaywardBeyondClient.GameState.Set(previous);
        }
    }

    [Fact]
    public void AdventureTimedBreaking_MaintainsBeamAndImpactSparksDuringHold_AndEmitsBreakBurstOnCompletion()
    {
        GameState previous = WaywardBeyondClient.GameState.Get();
        WaywardBeyondClient.GameState.Set(GameState.Playing);
        try
        {
            using EndToEndBreakHarness harness = new(rockHardness: 1.0f, laserSpeed: 1.0f);
            harness.StartHold();

            //  Mid-hold: server beam component and client beam/impact emitter remain active continuously.
            for (var i = 0; i < 4; i++)
            {
                harness.TickBoth(0.1f);
                Assert.True(harness.ServerHasActiveBeam());
                Assert.True(harness.ClientHasActiveBeamAndImpact());
                Assert.Empty(harness.Particles.Bursts);
            }

            //  Finish hold: server commits removal, client reconciles edit, receives skill update, and fires break spark burst.
            for (var i = 0; i < 6; i++)
            {
                harness.TickBoth(0.1f);
            }

            Assert.Equal((ushort)0, harness.ServerVoxels.Get(0, 0, -4).ID);
            Assert.Equal((ushort)0, harness.ClientVoxels.Get(0, 0, -4).ID);
            Assert.Contains(harness.Particles.Bursts, burst => burst.EffectId == "mining_laser_break");
            Assert.Contains(harness.SkillUpdates, update => update.SkillId == "mining" && update.GainedXP > 0);
        }
        finally
        {
            WaywardBeyondClient.GameState.Set(previous);
        }
    }

    private sealed class TempAssetDir : IDisposable
    {
        public string Root { get; } = Path.Combine(Path.GetTempPath(), $"wb-timed-break-{Path.GetRandomFileName()}");

        public TempAssetDir()
        {
            Directory.CreateDirectory(Path.Combine(Root, "bricks"));
            Directory.CreateDirectory(Path.Combine(Root, "items"));
            Directory.CreateDirectory(Path.Combine(Root, "lang", "tags"));
            Directory.CreateDirectory(Path.Combine(Root, "skills"));

            string shippedTagsPath = Path.Combine("TestFiles", "Bricks", "assets", "lang", "tags", "bricks.toml");
            if (File.Exists(shippedTagsPath))
            {
                File.Copy(shippedTagsPath, Path.Combine(Root, "lang", "tags", "bricks.toml"), overwrite: true);
            }
        }

        public void WriteBricks(string toml) => File.WriteAllText(Path.Combine(Root, "bricks", "bricks.toml"), toml);

        public void WriteItems(string toml) => File.WriteAllText(Path.Combine(Root, "items", "items.toml"), toml);

        public BrickDatabase LoadBricks()
        {
            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(Root));
            IFileParser[] parsers = [new TomlParser<BrickDefinitions>(), new LocalizedTagDefinitionParser()];
            var parser = new VirtualFileParseService(parsers, vfs);
            var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
            return new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs, tags);
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

    private sealed class ServerHarness : IDisposable
    {
        private readonly TempAssetDir _assets = new();
        private readonly SimulationStep _simStep;
        private readonly StubServerConnection _connection = new();
        private readonly SessionManager _sessions = new();
        private readonly ServerPeers _peers = new();
        private readonly VoxelObject _voxels = new(16);
        private uint _sequence;
        private uint _simTick;

        public DataStore Store { get; } = new();
        public BrickDatabase Bricks { get; }
        public ItemDatabase Items { get; }
        public SkillDatabase Skills { get; }
        public ServerInteractionSystem Interactions { get; }
        public int PlayerEntity { get; }
        public Uuid PlayerUuid { get; }
        public int StructureEntity { get; }
        public Uuid StructureUuid { get; }
        public ushort RockId { get; }
        public ushort PanelId { get; }

        public ServerHarness(
            GameMode mode,
            float rockHardness,
            float panelHardness,
            float laserSpeed,
            float fastLaserSpeed,
            IReadOnlyList<IInteractionHandler>? extraHandlers = null
        ) {
            _assets.WriteBricks($"""
                [[Bricks]]
                ID = "{ROCK_BRICK_ID}"
                [Bricks.Statistics]
                "{StatisticIds.HARDNESS}" = {rockHardness.ToString(System.Globalization.CultureInfo.InvariantCulture)}

                [[Bricks]]
                ID = "{PANEL_BRICK_ID}"
                [Bricks.Statistics]
                "{StatisticIds.HARDNESS}" = {panelHardness.ToString(System.Globalization.CultureInfo.InvariantCulture)}
                """);
            _assets.WriteItems($"""
                [[Items]]
                ID = "{LASER_ITEM_ID}"
                Name = "item.laser"
                [Items.Tool]
                Type = "Mining"
                [Items.Statistics]
                "{StatisticIds.MINING_SPEED}" = {laserSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}

                [[Items]]
                ID = "{FAST_LASER_ITEM_ID}"
                Name = "item.fast_laser"
                [Items.Tool]
                Type = "Mining"
                [Items.Statistics]
                "{StatisticIds.MINING_SPEED}" = {fastLaserSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}
                """);

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(_assets.Root));
            vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Skills", "assets")));

            IFileParser[] parsers =
            [
                new TomlParser<BrickDefinitions>(),
                new TomlParser<ItemDefinitions>(),
                new TomlParser<SkillDefinitions>(),
                new LocalizedTagDefinitionParser(),
            ];
            var parser = new VirtualFileParseService(parsers, vfs);
            var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
            Bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs, tags);
            Items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);
            Skills = new SkillDatabase(NullLogger<SkillDatabase>.Instance, parser, vfs, Bricks, tags);

            RockId = Bricks.Id(ROCK_BRICK_ID);
            PanelId = Bricks.Id(PANEL_BRICK_ID);
            _voxels.Set(0, 0, -4, new Voxel(RockId, new ShapeLight(BrickShape.Block, 0), 0));
            _voxels.Set(1, 0, -4, new Voxel(PanelId, new ShapeLight(BrickShape.Block, 0), 0));

            var physics = new RaycastServerPhysics();
            _simStep = new SimulationStep(Store, physics);
            ServerPeer peer = _peers.GetOrAdd(new StubRpcHost(), new RpcPeer(_connection), isLocal: true);

            StructureEntity = Store.Alloc();
            StructureUuid = Store.GetUuid(StructureEntity);
            Store.AddOrUpdate(StructureEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            Store.AddOrUpdate(StructureEntity, new VoxelWorldComponent(_voxels));

            PlayerUuid = Uuid.FromValue(5001);
            PlayerEntity = Store.Alloc(PlayerUuid);
            Store.AddOrUpdate(PlayerEntity, NetworkComponent.Create());
            Store.AddOrUpdate(PlayerEntity, new OwnedCharacterComponent());
            Store.AddOrUpdate(PlayerEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            Store.AddOrUpdate(PlayerEntity, new GameModeComponent(mode));
            Store.AddOrUpdate(PlayerEntity, new EquipmentComponent(0));
            Store.AddOrUpdate(PlayerEntity, InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.HOTBAR, 8)));
            Store.AddOrUpdate(PlayerEntity, new SkillStateComponent(new Dictionary<string, long>()));
            Store.AddOrUpdate(PlayerEntity, new InputComponent());
            _sessions.Register(Store, PlayerEntity, peer.GameId, new Session(1));

            var statResolver = new StatResolver(
            [
                new BrickStatisticModifierSource(Bricks),
                new ItemStatisticModifierSource(Items),
                new EntityStatisticModifierSource(),
                new SkillMiningSpeedModifierSource(Skills),
            ]);

            var handlers = new List<IInteractionHandler>
            {
                new ItemPlaceableSelector(Items, Bricks),
                new HintResolver(Bricks),
                new BreakToolValidator(Items),
                new AdventureBreakHoldValidator(statResolver),
                new VoxelWriteCommitter(_peers, Bricks, Bricks, NullLogger<VoxelWriteCommitter>.Instance),
                new BreakLootReactor(Bricks),
                new SkillXpReactor(Skills, _peers, NullLogger<SkillXpReactor>.Instance),
            };
            if (extraHandlers != null)
            {
                handlers.AddRange(extraHandlers);
            }

            var handlerRegistry = new InteractionHandlerRegistry(handlers);
            Interactions = new ServerInteractionSystem(
                _sessions,
                NullLogger<ServerInteractionSystem>.Instance,
                handlerRegistry,
                s =>
                {
                    WorldFactoryCalls++;
                    return new ServerVoxelInteractionWorld(s, physics);
                },
                _simStep);
        }

        public int WorldFactoryCalls { get; private set; }

        public void SetSecondaryHeld(bool held)
        {
            Store.AddOrUpdate(PlayerEntity, new InputComponent { SecondaryHeld = held });
        }

        public void SetHeldSlot(int slot, string itemId)
        {
            if (Store.TryGet(PlayerEntity, out InventoryComponent inv))
            {
                InventoryComponent updated = inv.Clone();
                updated.Set(new SlotId(SlotGroups.HOTBAR, slot), new ItemData(itemId, 1, 1));
                Store.AddOrUpdate(PlayerEntity, updated);
            }
        }

        public void SetActiveSlot(int slot)
        {
            Store.AddOrUpdate(PlayerEntity, new EquipmentComponent(slot));
        }

        public void SetPrimaryHeld(bool held)
        {
            Store.AddOrUpdate(PlayerEntity, new InputComponent { PrimaryHeld = held });
        }

        public void SetPlayerPosition(Vector3 position)
        {
            Store.AddOrUpdate(PlayerEntity, new TransformComponent(position, Quaternion.Identity, Vector3.One));
        }

        public void SetSkillXp(string skillId, long xp)
        {
            if (Store.TryGet(PlayerEntity, out SkillStateComponent state))
            {
                state.SetXP(skillId, xp);
                Store.AddOrUpdate(PlayerEntity, state);
            }
        }

        public long GetSkillXp(string skillId)
        {
            return Store.TryGet(PlayerEntity, out SkillStateComponent state) ? state.GetXP(skillId) : 0;
        }

        public int GetInventoryCount(string itemId)
        {
            if (!Store.TryGet(PlayerEntity, out InventoryComponent inv))
            {
                return 0;
            }

            ReadOnlySpan<ItemData> items = inv.AsSpan();
            var total = 0;
            for (var i = 0; i < items.Length; i++)
            {
                if (items[i].ID == itemId)
                {
                    total += items[i].Count;
                }
            }

            return total;
        }

        public Voxel GetVoxel(int x, int y, int z) => _voxels.Get(x, y, z);

        public void StageInteraction(InteractionKind kind, Uuid targetUuid, Int3 coord)
        {
            if (!Store.TryGet(PlayerEntity, out NetworkComponent net) || net.StagedInteractions == null)
            {
                return;
            }

            uint seq = ++_sequence;
            net.StagedInteractions.Stage(new InteractionEvent
            {
                Entity = PlayerUuid.ToValue(),
                SequenceNumber = seq,
                ServerTickAtSample = _simTick,
                Kind = kind,
                Brick = new BrickInteraction
                {
                    TargetEntity = targetUuid.ToValue(),
                    TargetX = coord.X,
                    TargetY = coord.Y,
                    TargetZ = coord.Z,
                },
            });
        }

        public void TickServer(float delta)
        {
            _simTick++;
            Interactions.Tick(delta, Store, _simTick);
        }

        public void Dispose()
        {
            _simStep.Dispose();
            _assets.Dispose();
        }
    }

    private sealed class ClientHarness : IDisposable
    {
        private readonly TempAssetDir _assets = new();
        private readonly RaycastClientPhysics _physics = new();
        private readonly HoldingShortcutService _shortcuts = new();
        private readonly NotificationService _notifications;
        private readonly PlayerInteractionService _service;

        public DataStore Store { get; } = new();
        public BrickDatabase Bricks { get; }
        public ItemDatabase Items { get; }
        public SkillDatabase Skills { get; }
        public InteractionState InteractionState { get; }
        public VoxelObject Voxels { get; } = new(16);
        public int StructureEntity { get; }
        public int PlayerEntity { get; }
        public ushort RockId { get; }
        public List<BreakEvent> BreakEvents { get; } = [];
        public List<InteractionEvent> StagedEvents { get; } = [];

        public ClientHarness(
            GameMode mode,
            float rockHardness,
            float laserSpeed,
            IClientTimedInteraction[]? extraTimedInteractions = null
        ) {
            _assets.WriteBricks($"""
                [[Bricks]]
                ID = "{ROCK_BRICK_ID}"
                [Bricks.Statistics]
                "{StatisticIds.HARDNESS}" = {rockHardness.ToString(System.Globalization.CultureInfo.InvariantCulture)}

                [[Bricks]]
                ID = "{PANEL_BRICK_ID}"
                [Bricks.Statistics]
                "{StatisticIds.HARDNESS}" = {rockHardness.ToString(System.Globalization.CultureInfo.InvariantCulture)}
                """);
            _assets.WriteItems($"""
                [[Items]]
                ID = "{LASER_ITEM_ID}"
                Name = "item.laser"
                [Items.Tool]
                Type = "Mining"
                [Items.Statistics]
                "{StatisticIds.MINING_SPEED}" = {laserSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}
                """);

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(_assets.Root));
            vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Skills", "assets")));

            IFileParser[] parsers =
            [
                new TomlParser<BrickDefinitions>(),
                new TomlParser<ItemDefinitions>(),
                new TomlParser<SkillDefinitions>(),
                new LocalizedTagDefinitionParser(),
            ];
            var parser = new VirtualFileParseService(parsers, vfs);
            var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
            Bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs, tags);
            Items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);
            Skills = new SkillDatabase(NullLogger<SkillDatabase>.Instance, parser, vfs, Bricks, tags);

            RockId = Bricks.Id(ROCK_BRICK_ID);
            Voxels.Set(0, 0, -4, new Voxel(RockId, new ShapeLight(BrickShape.Block, 0), 0));
            Voxels.Set(1, 0, -4, new Voxel(Bricks.Id(PANEL_BRICK_ID), new ShapeLight(BrickShape.Block, 0), 0));

            StructureEntity = Store.Alloc();
            Store.AddOrUpdate(StructureEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            Store.AddOrUpdate(StructureEntity, new VoxelComponent(Voxels));

            PlayerEntity = Store.Alloc(Uuid.FromValue(7001));
            Store.AddOrUpdate(PlayerEntity, new PlayerComponent());
            Store.AddOrUpdate(PlayerEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            Store.AddOrUpdate(PlayerEntity, new GameModeComponent(mode));
            Store.AddOrUpdate(PlayerEntity, new EquipmentComponent(0));
            InventoryComponent inv = InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.HOTBAR, 4));
            inv.Set(new SlotId(SlotGroups.HOTBAR, 0), new ItemData(LASER_ITEM_ID, 1, 1));
            Store.AddOrUpdate(PlayerEntity, inv);
            Store.AddOrUpdate(PlayerEntity, new PendingInteractionComponent(new PendingInteractionQueue()));
            Store.AddOrUpdate(PlayerEntity, new SkillStateComponent(new Dictionary<string, long>(StringComparer.Ordinal)));
            Store.AddOrUpdate(PlayerEntity, new StatisticsComponent());

            var input = new FakeInputService();
            var window = new FakeWindowContext { Resolution = new Vector2(1280f, 720f) };
            var motionProcessor = new ClientPlayerMotionProcessor(input, window, _physics, new EventInvoker<PlayerMovedEvent>([]));
            InteractionState = new InteractionState(in motionProcessor);
            _notifications = new NotificationService(NullLogger<NotificationService>.Instance, window);

            var meshDb = new StubMeshDatabase();
            var emptyMesh = new Mesh([], [], [], [], []);
            meshDb.Assets["slope.obj"] = emptyMesh;
            meshDb.Assets["stair.obj"] = emptyMesh;
            meshDb.Assets["slab.obj"] = emptyMesh;
            meshDb.Assets["column.obj"] = emptyMesh;
            meshDb.Assets["plate.obj"] = emptyMesh;

            var statResolver = new StatResolver(
            [
                new BrickStatisticModifierSource(Bricks),
                new ItemStatisticModifierSource(Items),
                new EntityStatisticModifierSource(),
                new SkillMiningSpeedModifierSource(Skills),
            ]);
            var timedInteractions = new List<IClientTimedInteraction>
            {
                new AdventureBreakTimedInteraction(_physics, Bricks, statResolver),
            };
            if (extraTimedInteractions != null)
            {
                timedInteractions.AddRange(extraTimedInteractions);
            }

            var timedController = new ClientTimedInteractionController(timedInteractions);

            var breakProcessor = new RecordingBreakEventProcessor(BreakEvents);
            _service = new PlayerInteractionService(
                InteractionState,
                new BrickPreviewState(),
                _physics,
                new NullLineRenderer(),
                new FakeRenderContext(Store),
                window,
                new PlayerData(Items),
                Bricks,
                Bricks,
                Items,
                new DebugSettings(),
                meshDb,
                _shortcuts,
                new VoxelLightingService(Bricks),
                new EventInvoker<PlaceEvent>([]),
                new EventInvoker<BreakEvent>([breakProcessor]),
                new EventInvoker<BrickEvent>([]),
                new SnapshotAckTracker(),
                _notifications,
                new TestLocalization(),
                paintDatabase: null,
                paintRegistry: null,
                paintFaceEvent: null,
                timedInteractionController: timedController);

            SetRaycastTarget(new Int3(0, 0, -4));
        }

        public void SetPrimaryHeld(bool held, bool firePressTrigger = false)
        {
            _shortcuts.PrimaryHeld = held;
            if (firePressTrigger)
            {
                _shortcuts.InvokePrimaryAction();
            }
        }

        public void SetSecondaryHeld(bool held, bool firePressTrigger = false)
        {
            _shortcuts.SecondaryHeld = held;
            if (firePressTrigger)
            {
                _shortcuts.InvokeSecondaryAction();
            }
        }

        public void SetRaycastTarget(Int3 coord)
        {
            var point = new Vector3(coord.X, coord.Y, coord.Z + 0.5f);
            _physics.NextHit = new RaycastResult(hit: true, new Entity(StructureEntity, Store), point, new Vector3(0f, 0f, 1f));
        }

        public void SetPlayerPosition(Vector3 position)
        {
            Store.AddOrUpdate(PlayerEntity, new TransformComponent(position, Quaternion.Identity, Vector3.One));
        }

        public void SetActiveSlot(int slot)
        {
            Store.AddOrUpdate(PlayerEntity, new EquipmentComponent(slot));
        }

        public void SetGameMode(GameMode mode)
        {
            Store.AddOrUpdate(PlayerEntity, new GameModeComponent(mode));
        }

        public void SetSkillXp(string skillId, long xp)
        {
            if (Store.TryGet(PlayerEntity, out SkillStateComponent state))
            {
                state.SetXP(skillId, xp);
            }
        }

        public int CountStaged(InteractionKind kind)
        {
            var count = 0;
            for (var i = 0; i < StagedEvents.Count; i++)
            {
                if (StagedEvents[i].Kind == kind)
                {
                    count++;
                }
            }

            return count;
        }

        public void TickClient(float delta)
        {
            _service.Tick(delta, Store);
            if (Store.TryGet(PlayerEntity, out PendingInteractionComponent pending))
            {
                for (var i = 0; i < pending.Outbound.Count; i++)
                {
                    StagedEvents.Add(pending.Outbound[i]);
                }

                pending.Outbound.Clear();
            }
        }

        public void Dispose()
        {
            _notifications.Dispose();
            _assets.Dispose();
        }
    }

    private sealed class EndToEndBreakHarness : IDisposable
    {
        private readonly TempAssetDir _assets = new();
        private readonly SimulationStep _simStep;
        private readonly ServerWorld _serverWorld;
        private readonly ServerPeers _peers = new();
        private readonly SessionManager _sessions = new();
        private readonly LoopbackRpcServerTransport _transport = new();
        private readonly RpcServer _rpcServer;
        private readonly LoopbackRpcClientConnection _clientConnection;
        private readonly RpcClient _rpcClient;
        private readonly ClientInbound _clientInbound = new();
        private readonly FakeInputService _clientInput = new();
        private readonly RaycastServerPhysics _serverPhysics = new();
        private readonly RaycastClientPhysics _clientPhysics = new();
        private readonly BrickDatabase _bricks;
        private readonly ServerInteractionSystem _serverInteraction;
        private readonly ServerLaserBeamSystem _serverBeam;
        private readonly MiningLaserEffectSystem _clientEffects;
        private readonly ClientVoxelReconcileSystem _clientReconcile;
        private readonly int _serverPlayer;
        private readonly Uuid _serverPlayerUuid;
        private readonly Uuid _serverStructureUuid;
        private readonly int _clientPlayer;
        private uint _simTick;
        private uint _seq;

        public DataStore ServerStore => _serverWorld.Store;
        public DataStore ClientStore { get; } = new();
        public VoxelObject ServerVoxels { get; } = new(16);
        public VoxelObject ClientVoxels { get; } = new(16);
        public RecordingParticleService Particles { get; } = new();
        public List<SkillStateUpdateMessage> SkillUpdates { get; } = [];

        public EndToEndBreakHarness(float rockHardness, float laserSpeed)
        {
            _assets.WriteBricks($"""
                [[Bricks]]
                ID = "{ROCK_BRICK_ID}"
                [Bricks.Statistics]
                "{StatisticIds.HARDNESS}" = {rockHardness.ToString(System.Globalization.CultureInfo.InvariantCulture)}
                """);
            _assets.WriteItems($"""
                [[Items]]
                ID = "{LASER_ITEM_ID}"
                Name = "item.laser"
                [Items.Tool]
                Type = "Mining"
                [Items.Statistics]
                "{StatisticIds.MINING_SPEED}" = {laserSpeed.ToString(System.Globalization.CultureInfo.InvariantCulture)}
                """);

            var vfs = new VirtualFileSystem();
            vfs.Mount(new PathInfo(_assets.Root));
            vfs.Mount(new PathInfo(Path.Combine("TestFiles", "Skills", "assets")));

            IFileParser[] parsers =
            [
                new TomlParser<BrickDefinitions>(),
                new TomlParser<ItemDefinitions>(),
                new TomlParser<SkillDefinitions>(),
                new LocalizedTagDefinitionParser(),
            ];
            var parser = new VirtualFileParseService(parsers, vfs);
            var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parser, vfs);
            _bricks = new BrickDatabase(NullLogger<BrickDatabase>.Instance, parser, vfs, tags);
            var items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, parser, vfs);
            var skills = new SkillDatabase(NullLogger<SkillDatabase>.Instance, parser, vfs, _bricks, tags);

            var ecsWorld = new World();
            _simStep = new SimulationStep(ecsWorld.DataStore, _serverPhysics);
            var replication = new NetworkReplicationSystem(_peers, _sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingConfig(), new NetworkRegistry());
            var permPolicy = PermissionPolicy.Create([new SourcedPermissionFile("test.toml", new PermissionFile())]);
            var permService = new UserPermissionService(new PermissionResolver(permPolicy));
            var saveService = new LevelSaveService(
                NullLogger<LevelSaveService>.Instance,
                new StubLevelCatalog(),
                _bricks,
                _bricks,
                new GameplayConfig());

            var statResolver = new StatResolver(
            [
                new BrickStatisticModifierSource(_bricks),
                new ItemStatisticModifierSource(items),
                new EntityStatisticModifierSource(),
                new SkillMiningSpeedModifierSource(skills),
            ]);
            IInteractionHandler[] handlers =
            [
                new ItemPlaceableSelector(items, _bricks),
                new HintResolver(_bricks),
                new BreakToolValidator(items),
                new AdventureBreakHoldValidator(statResolver),
                new VoxelWriteCommitter(_peers, _bricks, _bricks, NullLogger<VoxelWriteCommitter>.Instance),
                new BreakLootReactor(_bricks),
                new SkillXpReactor(skills, _peers, NullLogger<SkillXpReactor>.Instance),
            ];
            var handlerRegistry = new InteractionHandlerRegistry(handlers);
            Func<DataStore, IVoxelInteractionWorld> worldFactory = s => new ServerVoxelInteractionWorld(s, _serverPhysics);
            _serverInteraction = new ServerInteractionSystem(
                _sessions,
                NullLogger<ServerInteractionSystem>.Instance,
                handlerRegistry,
                worldFactory,
                _simStep);

            var worldContainer = new Container();
            worldContainer.RegisterInstance(ecsWorld);
            worldContainer.RegisterInstance(_sessions);
            worldContainer.RegisterInstance(saveService);
            worldContainer.RegisterInstance(new ServerJoinQueue());
            worldContainer.RegisterInstance(replication);
            worldContainer.RegisterInstance<IInteractionHandlerRegistry>(handlerRegistry);
            worldContainer.RegisterInstance<IUserPermissionService>(permService);
            worldContainer.RegisterInstance<IServerWorldPhysics>(_serverPhysics);
            worldContainer.RegisterInstance(_simStep);
            worldContainer.RegisterInstance(worldFactory);
            worldContainer.RegisterInstance(_serverInteraction);
            worldContainer.RegisterInstance(new ServerParticleService(_peers, _sessions, replication, NullLogger<ServerParticleService>.Instance));
            _serverWorld = new ServerWorld(worldContainer);

            ushort rockId = _bricks.Id(ROCK_BRICK_ID);
            ServerVoxels.Set(0, 0, -4, new Voxel(rockId, new ShapeLight(BrickShape.Block, 0), 0));
            ClientVoxels.Set(0, 0, -4, new Voxel(rockId, new ShapeLight(BrickShape.Block, 0), 0));

            int serverStructure = ServerStore.Alloc();
            _serverStructureUuid = ServerStore.GetUuid(serverStructure);
            ServerStore.AddOrUpdate(serverStructure, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            ServerStore.AddOrUpdate(serverStructure, new VoxelWorldComponent(ServerVoxels));

            int clientStructure = ClientStore.Alloc(_serverStructureUuid);
            ClientStore.AddOrUpdate(clientStructure, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            ClientStore.AddOrUpdate(clientStructure, new VoxelComponent(ClientVoxels));

            var serverRpcRegistry = new RpcRegistry();
            serverRpcRegistry.Freeze();
            _rpcServer = new RpcServer(_transport, serverRpcRegistry);
            TaskCompletionSource<RpcPeer> connectedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            _rpcServer.ClientConnected += p => connectedTcs.TrySetResult(p);
            _rpcServer.Start();

            _clientConnection = new LoopbackRpcClientConnection(_transport);
            _clientConnection.ConnectAsync().GetAwaiter().GetResult();
            var clientRpcRegistry = new RpcRegistry();
            clientRpcRegistry.AddService(new ClientHandlers(_clientInbound));
            clientRpcRegistry.Freeze();
            _rpcClient = new RpcClient(_clientConnection, clientRpcRegistry);
            RpcPeer serverRpcPeer = connectedTcs.Task.GetAwaiter().GetResult();
            ServerPeer serverPeer = _peers.GetOrAdd(new StubRpcHost(), serverRpcPeer, isLocal: true);
            serverPeer.World = _serverWorld;

            _serverPlayerUuid = Uuid.FromValue(8001);
            _serverPlayer = ServerStore.Alloc(_serverPlayerUuid);
            ServerStore.AddOrUpdate(_serverPlayer, NetworkComponent.Create());
            ServerStore.AddOrUpdate(_serverPlayer, new OwnedCharacterComponent());
            ServerStore.AddOrUpdate(_serverPlayer, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            ServerStore.AddOrUpdate(_serverPlayer, new GameModeComponent(GameMode.Adventure));
            ServerStore.AddOrUpdate(_serverPlayer, new EquipmentComponent(0));
            InventoryComponent serverInv = InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.HOTBAR, 2));
            serverInv.Set(new SlotId(SlotGroups.HOTBAR, 0), new ItemData(LASER_ITEM_ID, 1, 1));
            ServerStore.AddOrUpdate(_serverPlayer, serverInv);
            ServerStore.AddOrUpdate(_serverPlayer, new SkillStateComponent(new Dictionary<string, long>()));
            ServerStore.AddOrUpdate(_serverPlayer, new InputComponent());
            _sessions.Register(ServerStore, _serverPlayer, serverPeer.GameId, new Session(1));

            _serverBeam = new ServerLaserBeamSystem(_serverPhysics, items, _simStep);

            _serverPhysics.NextHit = new RaycastResult(true, default, new Vector3(0f, 0f, -3.5f), new Vector3(0f, 0f, 1f));
            _clientPhysics.NextHit = new RaycastResult(true, new Entity(clientStructure, ClientStore), new Vector3(0f, 0f, -3.5f), new Vector3(0f, 0f, 1f));

            _clientPlayer = ClientStore.Alloc(_serverPlayerUuid);
            ClientStore.AddOrUpdate(_clientPlayer, new PlayerComponent());
            ClientStore.AddOrUpdate(_clientPlayer, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            InventoryComponent clientInv = InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.HOTBAR, 2));
            clientInv.Set(new SlotId(SlotGroups.HOTBAR, 0), new ItemData(LASER_ITEM_ID, 1, 1));
            ClientStore.AddOrUpdate(_clientPlayer, new EquipmentComponent(0));
            ClientStore.AddOrUpdate(_clientPlayer, clientInv);

            var motionProcessor = new ClientPlayerMotionProcessor(_clientInput, new FakeWindowContext(), _clientPhysics, new EventInvoker<PlayerMovedEvent>([]));
            var interactionState = new InteractionState(in motionProcessor);
            var materialDb = new StubAssetDatabase<Material>
            {
                Assets = { ["effects/laser_beam"] = new Material(new Shader("laser_beam"), []) { AlphaMode = AlphaMode.Blend } },
            };
            var effectDb = new StubAssetDatabase<ParticleEffect>
            {
                Assets = { ["mining_laser_impact"] = CreateParticleEffect("mining_laser_impact") },
            };
            _clientEffects = new MiningLaserEffectSystem(new PlayerData(items), interactionState, _clientInput, _clientPhysics, materialDb, effectDb);
            var breakParticleListener = new BrickBreakParticleListener(Particles);
            _clientReconcile = new ClientVoxelReconcileSystem(
                _clientInbound,
                new SnapshotAckTracker(),
                _bricks,
                new VoxelLightingService(_bricks),
                new EventInvoker<BrickEvent>([breakParticleListener]));
        }

        public void StartHold()
        {
            _clientInput.HeldButtons.Add(MouseButton.Left);
            ServerStore.AddOrUpdate(_serverPlayer, new InputComponent { PrimaryHeld = true });
            if (ServerStore.TryGet(_serverPlayer, out NetworkComponent net) && net.StagedInteractions != null)
            {
                net.StagedInteractions.Stage(new InteractionEvent
                {
                    Entity = _serverPlayerUuid.ToValue(),
                    SequenceNumber = ++_seq,
                    ServerTickAtSample = _simTick,
                    Kind = InteractionKind.PrimaryPressed,
                    Brick = new BrickInteraction
                    {
                        TargetEntity = _serverStructureUuid.ToValue(),
                        TargetX = 0,
                        TargetY = 0,
                        TargetZ = -4,
                    },
                });
            }
        }

        public void TickBoth(float delta)
        {
            _simTick++;
            _serverInteraction.Tick(delta, ServerStore, _simTick);
            _serverBeam.Tick(ServerStore, _simTick);
            if (ServerVoxels.Get(0, 0, -4).ID == 0)
            {
                WaitForAsync(() => !_clientInbound.VoxelEdits.IsEmpty && !_clientInbound.SkillUpdates.IsEmpty);
            }

            while (_clientInbound.SkillUpdates.TryDequeue(out SkillStateUpdateMessage update))
            {
                SkillUpdates.Add(update);
            }

            _clientReconcile.Tick(delta, ClientStore);
            _clientEffects.Tick(delta, ClientStore);
        }

        public bool ServerHasActiveBeam()
        {
            return ServerStore.TryGet(_serverPlayer, out LaserBeamComponent beam) && beam.Hit;
        }

        public bool ClientHasActiveBeamAndImpact()
        {
            var foundEmitter = false;
            ClientStore.Query<ParticleEmitterComponent>(0f, (float _, DataStore _, int _, in ParticleEmitterComponent emitter) =>
            {
                if (emitter.Playing)
                {
                    foundEmitter = true;
                }
            });
            return foundEmitter;
        }

        private static void WaitForAsync(Func<bool> condition)
        {
            long deadline = Environment.TickCount64 + 500;
            while (!condition() && Environment.TickCount64 < deadline)
            {
                Task.Delay(5).GetAwaiter().GetResult();
            }
        }

        private static ParticleEffect CreateParticleEffect(string id)
        {
            return new ParticleEffect(
                id,
                texture: null,
                ParticleBlendMode.Additive,
                rate: 55f,
                burstCount: 8,
                maxParticles: 128,
                new ParticleRange(0.12f, 0.28f),
                new ParticleRange(2.5f, 6.0f),
                spreadDegrees: 62f,
                spawnRadius: 0.05f,
                new ParticleRange(0.04f, 0.075f),
                new ParticleRange(0.01f, 0.02f),
                Vector4.One,
                secondaryStartColor: null,
                endColor: Vector4.One,
                emissiveIntensity: 3.2f,
                gravity: Vector3.Zero,
                drag: 3.2f,
                new ParticleRange(0f, 0f),
                new ParticleRange(0f, 0f));
        }

        public void Dispose()
        {
            _ = _rpcClient;
            _clientConnection.Close();
            _rpcServer.Close();
            _transport.Close();
            _serverWorld.Dispose();
            _simStep.Dispose();
            _assets.Dispose();
        }
    }

    private sealed class RaycastClientPhysics : IPhysics
    {
        public RaycastResult NextHit { get; set; }
        public float FixedTimestamp => 1f / 64f;

        public event EventHandler<EventArgs>? FixedUpdate
        {
            add { }
            remove { }
        }

        public RaycastResult Raycast(in Ray ray) => NextHit;

        public RaycastResult Raycast(in Ray ray, int ignoreEntity) => NextHit;

        public bool Overlaps(in ShapeOverlapQuery query) => false;

        public void SetGravity(Vector3 gravity) { }
    }

    private sealed class RaycastServerPhysics : IServerWorldPhysics
    {
        public RaycastResult NextHit { get; set; }
        public float FixedTimestamp => 1f / 64f;

        public event EventHandler<EventArgs>? FixedUpdate
        {
            add { }
            remove { }
        }

        public RaycastResult Raycast(in Ray ray) => NextHit;

        public RaycastResult Raycast(in Ray ray, int ignoreEntity) => NextHit;

        public bool Overlaps(in ShapeOverlapQuery query) => false;

        public void SetGravity(Vector3 gravity) { }

        public void Tick(float delta, DataStore store) { }
    }

    private sealed class HoldingShortcutService : IShortcutService
    {
        private readonly List<Shortcut> _shortcuts = [];

        public bool PrimaryHeld { get; set; }

        public bool SecondaryHeld { get; set; }

        public event Action? BindingsChanged
        {
            add { }
            remove { }
        }

        public bool IsCapturing => false;

        public bool RegisterShortcut(Shortcut shortcut)
        {
            _shortcuts.Add(shortcut);
            return true;
        }

        public bool AddHandler(string id, Func<bool>? isEnabled, Action action, Action? released = null) => true;

        public IReadOnlyList<ShortcutInfo> GetShortcuts() => [];

        public Result<ShortcutBinding> GetBinding(string id) => Result<ShortcutBinding>.FromFailure("Not registered.");

        public bool IsHeld(string id) =>
            (id == GameShortcuts.PRIMARY_ACTION && PrimaryHeld) ||
            (id == GameShortcuts.SECONDARY_ACTION && SecondaryHeld);

        public Result Rebind(string id, ShortcutBinding binding) => Result.FromSuccess();

        public Result ResetBinding(string id) => Result.FromSuccess();

        public void ResetAllBindings() { }

        public void ApplyOverrides(IReadOnlyDictionary<string, ShortcutBinding> overrides) { }

        public IReadOnlyDictionary<string, ShortcutBinding> GetOverrides() => new Dictionary<string, ShortcutBinding>();

        public void BeginCapture(Action<ShortcutCaptureResult> onCompleted) { }

        public void CancelCapture() { }

        public void InvokePrimaryAction()
        {
            for (var i = 0; i < _shortcuts.Count; i++)
            {
                if (_shortcuts[i].GetId() == GameShortcuts.PRIMARY_ACTION && _shortcuts[i].IsEnabled())
                {
                    _shortcuts[i].Action?.Invoke();
                }
            }
        }

        public void InvokeSecondaryAction()
        {
            for (var i = 0; i < _shortcuts.Count; i++)
            {
                if (_shortcuts[i].GetId() == GameShortcuts.SECONDARY_ACTION && _shortcuts[i].IsEnabled())
                {
                    _shortcuts[i].Action?.Invoke();
                }
            }
        }
    }

    private sealed class NullLineRenderer : ILineRenderer
    {
        public Line CreateLine(bool alwaysOnTop = false) => new(this, Vector3.Zero, Vector3.Zero, Vector4.Zero);

        public Line CreateLine(Vector3 start, Vector3 end, bool alwaysOnTop = false) => new(this, start, end, Vector4.Zero);

        public Line CreateLine(Vector3 start, Vector3 end, Vector4 color, bool alwaysOnTop = false) => new(this, start, end, color);

        public void DeleteLine(Line line) { }
    }

    private sealed class StubMeshDatabase : IAssetDatabase<Mesh>
    {
        public Dictionary<string, Mesh> Assets { get; } = new(StringComparer.Ordinal);

        public Result<Mesh> Get(string id)
        {
            return Assets.TryGetValue(id, out Mesh? mesh)
                ? Result<Mesh>.FromSuccess(mesh)
                : Result<Mesh>.FromFailure((string?)null);
        }
    }

    private sealed class StubAssetDatabase<T> : IAssetDatabase<T>
    {
        public Dictionary<string, T> Assets { get; } = new(StringComparer.Ordinal);

        public Result<T> Get(string id)
        {
            return Assets.TryGetValue(id, out T? asset)
                ? Result<T>.FromSuccess(asset)
                : Result<T>.FromFailure((string?)null);
        }
    }

    private sealed class RecordingBreakEventProcessor(List<BreakEvent> recorded) : IEventProcessor<BreakEvent>
    {
        public Result<EventBehavior> ProcessEvent(object sender, BreakEvent e)
        {
            recorded.Add(e);
            return Result<EventBehavior>.FromSuccess(EventBehavior.Continue);
        }
    }

    private sealed class RecordingParticleService : IParticleService
    {
        public readonly List<(string EffectId, Vector3 Position, Vector3 Direction, int? Count)> Bursts = [];

        public int ActiveParticleCount => 0;

        public void EmitBurst(in ParticleBurstRequest request) { }

        public Result EmitBurst(string effectId, Vector3 position, Vector3 direction, int? count = null)
        {
            Bursts.Add((effectId, position, direction, count));
            return Result.FromSuccess();
        }

        public void Clear() { }
    }

    private sealed class StubServerConnection : NsdRpc.IServerConnection, IRpcConnectionStats
    {
        public ulong ClientId => 1;
        public System.Net.EndPoint? RemoteEndPoint => null;
        public bool IsConnected => true;
        public RpcConnectionStats Stats => default;

        public void Close(RpcDisconnectReason reason = RpcDisconnectReason.LocalClose) { }

        public bool TrySend(ReadOnlySpan<byte> frame) => true;

        public event Action<RpcDisconnectReason>? Disconnected { add { } remove { } }

        public event RpcFrameHandler? FrameReceived { add { } remove { } }
    }

    private sealed class StubRpcHost : IRpcHost
    {
        public PeerSet Peers => default;
    }

    private sealed class StubLevelCatalog : ILevelCatalog
    {
        public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
        {
            levelGuid = Guid.NewGuid().ToString();
            return true;
        }

        public Level[] ListLevels() => [];

        public bool Delete(string levelGuid) => false;

        public bool Exists(string levelGuid) => false;

        public ILevelStorage? Open(string levelGuid) => null;
    }

    private delegate void StatApplyCallback(in StatQuery query, ref StatAccumulator accumulator);

    private sealed class DelegateStatModifierSource(
        StatModifierPriority priority,
        string? statId,
        StatApplyCallback callback
    ) : IStatModifierSource
    {
        public StatModifierPriority Priority => priority;
        public string? StatId => statId;

        public void Apply(in StatQuery query, ref StatAccumulator accumulator)
        {
            callback(in query, ref accumulator);
        }
    }

    private sealed class ModBonusStatSource : IStatModifierSource
    {
        public StatModifierPriority Priority => StatModifierPriority.Normal;
        public string? StatId => "mod:custom_stat";

        public void Apply(in StatQuery query, ref StatAccumulator accumulator)
        {
            accumulator.Apply(StatModifier.AddPercent(0.5f));
        }
    }

    private sealed class CustomTimedValidateHandler(string handlerId, float duration) : IInteractionHandler
    {
        public InteractionStage Stage => InteractionStage.Validate;
        public InteractionPriority Priority => InteractionPriority.High;
        public InteractionHandlerFilter Filter => new(Kind: InteractionKind.SecondaryPressed);

        public InteractionDecision Handle(ref InteractionContext context)
        {
            var key = new TimedInteractionKey(handlerId, Uuid.Null, default, 0);
            context.Hold = InteractionHold.Require(key, duration, InteractionInput.Secondary);
            return InteractionDecision.Handled;
        }
    }

    private sealed class CustomTimedCommitHandler(Action onCommit) : IInteractionHandler
    {
        public InteractionStage Stage => InteractionStage.Commit;
        public InteractionPriority Priority => InteractionPriority.High;
        public InteractionHandlerFilter Filter => new(Kind: InteractionKind.SecondaryPressed);

        public InteractionDecision Handle(ref InteractionContext context)
        {
            onCommit();
            return InteractionDecision.Handled;
        }
    }

    private sealed class CustomClientTimedInteraction(string handlerId, float duration) : IClientTimedInteraction
    {
        public InteractionInput Input => InteractionInput.Secondary;

        public bool AppliesTo(in ClientTimedInteractionContext context) => true;

        public bool TryResolveTarget(in ClientTimedInteractionContext context, out ClientTimedTarget target)
        {
            target = new ClientTimedTarget(
                new TimedInteractionKey(handlerId, Uuid.Null, default, 0),
                duration,
                InteractionInput.Secondary);
            return true;
        }
    }
}
