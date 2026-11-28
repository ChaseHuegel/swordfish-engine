#nullable enable
using System;
using System.Collections.Generic;
using System.Globalization;
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
using Swordfish.Graphics;
using Swordfish.Graphics.SilkNET.OpenGL;
using Swordfish.Input;
using Swordfish.Library.Collections;
using Swordfish.Library.Globalization;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Tests;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Items;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Components;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Gameplay.Stations;
using WaywardBeyond.Gameplay.Survival;
using WaywardBeyond.Items;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Interactions;
using WaywardBeyond.Server.Permissions;
using WaywardBeyond.Server.Stations;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Voxels;
using Xunit;
using GameState = WaywardBeyond.Client.GameState;
using WaywardBeyondClient = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>
/// Pins <see cref="InteractionKind.Activate"/> (<c>[F]</c>), <see cref="ActiveStationComponent"/>
/// (<c>OwnerOnly</c>) binding and auto-close lifecycle, and the sequence-deduped extensible
/// <see cref="StationActionOp"/> / <see cref="IStationActionHandler"/> command pipeline.
/// </summary>
[Collection("ClientGameState")]
public sealed class ActivateAndStationActionTests
{
    [Fact]
    public void NetworkingCompositionRegistersActiveStationOwnerOnlyAndStationActionClientOwned()
    {
        using var container = new Container();
        container.RegisterNetworking();
        NetworkRegistry registry = container.Resolve<NetworkRegistry>();

        Result<NetworkComponentInfo> activeStationInfo = registry.GetInfo<ActiveStationComponent>();
        Assert.True(activeStationInfo.Success);
        Assert.Equal(Uuid.FromValue(22), activeStationInfo.Value.Uuid);
        Assert.Equal(NetworkDirection.ServerOwned, activeStationInfo.Value.Direction);
        Assert.Equal(ReplicationAudience.OwnerOnly, activeStationInfo.Value.Audience);
        Assert.IsType<NsdComponentCodec<ActiveStationComponent>>(activeStationInfo.Value.Codec);

        Result<NetworkComponentInfo> stationActionInfo = registry.GetInfo<StationActionEvent>();
        Assert.True(stationActionInfo.Success);
        Assert.Equal(Uuid.FromValue(23), stationActionInfo.Value.Uuid);
        Assert.Equal(NetworkDirection.ClientOwned, stationActionInfo.Value.Direction);
        Assert.IsType<NsdComponentCodec<StationActionEvent>>(stationActionInfo.Value.Codec);

        //  Verify wire round-trip of ActiveStationComponent and StationActionEvent.
        var originalStation = new ActiveStationComponent(
            stationEntity: 12345UL,
            voxelEntity: 67890UL,
            voxelX: -2,
            voxelY: 4,
            voxelZ: 7,
            stationTag: StationTags.CONSOLE,
            uiKind: StationUiKind.Terminal);
        ActiveStationComponent decodedStation = ActiveStationComponent.Deserialize(originalStation.Serialize());
        Assert.True(originalStation.EqualsState(in decodedStation));
        Assert.Equal(StationUiKind.Terminal, decodedStation.Kind);
        Assert.True(decodedStation.IsOpen);

        var originalEvent = new StationActionEvent
        {
            Entity = 999UL,
            SequenceNumber = 42u,
            Action = new StationActionOp
            {
                TargetEntity = 12345UL,
                Action = StationActionKind.Custom,
                ActionId = "mod:calibrate",
                Argument = "sector-7",
                Count = 3u,
            },
        };
        StationActionEvent decodedEvent = StationActionEvent.Deserialize(originalEvent.Serialize());
        Assert.Equal(999UL, decodedEvent.Entity);
        Assert.Equal(42u, decodedEvent.SequenceNumber);
        Assert.NotNull(decodedEvent.Action);
        Assert.Equal(12345UL, decodedEvent.Action!.Value.TargetEntity);
        Assert.Equal(StationActionKind.Custom, decodedEvent.Action.Value.Action);
        Assert.Equal("mod:calibrate", decodedEvent.Action.Value.ActionId);
        Assert.Equal("sector-7", decodedEvent.Action.Value.Argument);
        Assert.Equal(3u, decodedEvent.Action.Value.Count);
    }

    [Fact]
    public async Task ActivateInteractionValidatesReachRoutesThroughPipelineAndOpensContextualOrTerminalUi()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using LoopbackHarness harness = LoopbackHarness.Create(bricks);
        LoopbackClient player = await harness.AddPlayerClientAsync(Uuid.FromValue(3001), new Vector3(1f, 0f, 2f));
        LoopbackClient bystander = await harness.AddPlayerClientAsync(Uuid.FromValue(3002), new Vector3(1f, 0f, 2f));

        Uuid structureUuid = Uuid.FromValue(93001);
        var storageCoord = new Int3(1, 0, 0);
        var consoleCoord = new Int3(2, 0, 0);
        var panelCoord = new Int3(3, 0, 0);
        var farStorageCoord = new Int3(25, 0, 0);
        harness.CreateStructure(
            structureUuid,
            Vector3.Zero,
            Quaternion.Identity,
            (storageCoord, bricks.Id("wb:storage")),
            (consoleCoord, bricks.Id("wb:display_console")),
            (panelCoord, bricks.Id("wb:panel")),
            (farStorageCoord, bricks.Id("wb:storage")));

        int storageEntity = harness.BrickEntities.OnBrickPlaced(harness.ServerStore, structureUuid, storageCoord, bricks.Id("wb:storage"));
        int consoleEntity = harness.BrickEntities.OnBrickPlaced(harness.ServerStore, structureUuid, consoleCoord, bricks.Id("wb:display_console"));
        int farStorageEntity = harness.BrickEntities.OnBrickPlaced(harness.ServerStore, structureUuid, farStorageCoord, bricks.Id("wb:storage"));
        Uuid storageUuid = harness.ServerStore.GetUuid(storageEntity);
        Uuid consoleUuid = harness.ServerStore.GetUuid(consoleEntity);

        harness.ClearAllDirty();

        //  Out-of-reach activation is rejected cleanly without binding ActiveStationComponent or subscribing viewer.
        harness.StageActivate(player, sequence: 1, structureUuid, farStorageCoord);
        harness.TickServer();
        Assert.False(harness.TryGetOpenStation(player.ServerEntity, out _));
        Assert.False(harness.Replication.IsViewerSubscribed(farStorageEntity, player.ServerPeer.GameId));

        //  Non-station solid brick (wb:panel) and empty cell do not open a station.
        harness.StageActivate(player, sequence: 2, structureUuid, panelCoord);
        harness.StageActivate(player, sequence: 3, structureUuid, new Int3(1, 1, 0));
        harness.TickServer();
        Assert.False(harness.TryGetOpenStation(player.ServerEntity, out _));

        //  In-reach storage activation binds ActiveStationComponent (ContextualTab) and subscribes viewer.
        harness.StageActivate(player, sequence: 4, structureUuid, storageCoord);
        harness.TickServer();
        Assert.True(harness.TryGetOpenStation(player.ServerEntity, out ActiveStationComponent activeStorage));
        Assert.Equal(storageUuid.ToValue(), activeStorage.StationEntity);
        Assert.Equal(structureUuid.ToValue(), activeStorage.VoxelEntity);
        Assert.Equal(storageCoord.X, activeStorage.VoxelX);
        Assert.Equal(storageCoord.Y, activeStorage.VoxelY);
        Assert.Equal(storageCoord.Z, activeStorage.VoxelZ);
        Assert.Equal(StationTags.STORAGE, activeStorage.StationTag);
        Assert.Equal(StationUiKind.ContextualTab, activeStorage.Kind);
        Assert.True(harness.Replication.IsViewerSubscribed(storageEntity, player.ServerPeer.GameId));

        //  Verify OwnerOnly ActiveStationComponent and ViewersOnly storage components replicate ONLY to the activating player.
        harness.Replication.PublishStage(1f, harness.ServerStore);
        WorldSnapshot playerSnap = await player.WaitForSnapshotAsync();
        Assert.Contains(playerSnap.Components, c => c.Entity == player.EntityUuid.ToValue() && c.TypeUuid == harness.ActiveStationTypeUuid);
        Assert.Contains(playerSnap.Components, c => c.Entity == storageUuid.ToValue() && c.TypeUuid == harness.InventoryTypeUuid);
        Assert.Contains(playerSnap.Components, c => c.Entity == storageUuid.ToValue() && c.TypeUuid == harness.MachineStateTypeUuid);
        await Task.Delay(30);
        Assert.True(bystander.Inbound.Snapshots.IsEmpty, "Bystander must receive neither OwnerOnly ActiveStationComponent nor ViewersOnly station state.");

        //  Activating display_console switches ActiveStationComponent to Terminal and detaches the previous storage viewer.
        harness.StageActivate(player, sequence: 5, structureUuid, consoleCoord);
        harness.TickServer();
        Assert.True(harness.TryGetOpenStation(player.ServerEntity, out ActiveStationComponent activeConsole));
        Assert.Equal(consoleUuid.ToValue(), activeConsole.StationEntity);
        Assert.Equal(StationTags.CONSOLE, activeConsole.StationTag);
        Assert.Equal(StationUiKind.Terminal, activeConsole.Kind);
        Assert.False(harness.Replication.IsViewerSubscribed(storageEntity, player.ServerPeer.GameId));
        Assert.True(harness.Replication.IsViewerSubscribed(consoleEntity, player.ServerPeer.GameId));
    }

    [Fact]
    public async Task PluggableActivateHandlerPreEmptsStationCommitterAndAllowsReactStage()
    {
        BrickDatabase bricks = LoadShippedBricks();
        var doorHandler = new RecordingActivateHandler(InteractionStage.Commit, InteractionPriority.Normal, InteractionDecision.Handled);
        var reactHandler = new RecordingActivateHandler(InteractionStage.React, InteractionPriority.Normal, InteractionDecision.Continue);

        using LoopbackHarness harness = LoopbackHarness.Create(
            bricks,
            extraInteractionHandlers: [doorHandler, reactHandler]);
        LoopbackClient player = await harness.AddPlayerClientAsync(Uuid.FromValue(3010), new Vector3(1f, 0f, 2f));

        Uuid structureUuid = Uuid.FromValue(93010);
        var storageCoord = new Int3(1, 0, 0);
        harness.CreateStructure(structureUuid, Vector3.Zero, Quaternion.Identity, (storageCoord, bricks.Id("wb:storage")));
        int storageEntity = harness.BrickEntities.OnBrickPlaced(harness.ServerStore, structureUuid, storageCoord, bricks.Id("wb:storage"));

        harness.StageActivate(player, sequence: 1, structureUuid, storageCoord);
        harness.TickServer();

        Assert.Equal(1, doorHandler.CallCount);
        Assert.Equal(InteractionAction.Activate, doorHandler.LastAction);
        Assert.Equal(1, reactHandler.CallCount);
        Assert.False(harness.TryGetOpenStation(player.ServerEntity, out _));
        Assert.False(harness.Replication.IsViewerSubscribed(storageEntity, player.ServerPeer.GameId));
    }

    [Fact]
    public async Task ActiveStationAutoClosesOnMoveOutOfReachBrickDestroyDeathDisconnectAndCloseAction()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using LoopbackHarness harness = LoopbackHarness.Create(bricks);
        LoopbackClient player = await harness.AddPlayerClientAsync(Uuid.FromValue(3020), new Vector3(1f, 0f, 2f));

        Uuid structureUuid = Uuid.FromValue(93020);
        var coord = new Int3(1, 0, 0);
        harness.CreateStructure(structureUuid, Vector3.Zero, Quaternion.Identity, (coord, bricks.Id("wb:core")));
        int coreEntity = harness.BrickEntities.OnBrickPlaced(harness.ServerStore, structureUuid, coord, bricks.Id("wb:core"));

        //  1. Out-of-reach auto-close.
        harness.StageActivate(player, sequence: 1, structureUuid, coord);
        harness.TickServer();
        Assert.True(harness.TryGetOpenStation(player.ServerEntity, out _));
        Assert.True(harness.Replication.IsViewerSubscribed(coreEntity, player.ServerPeer.GameId));

        harness.ServerStore.AddOrUpdate(player.ServerEntity, new TransformComponent(new Vector3(50f, 0f, 0f), Quaternion.Identity, CharacterDefaults.PlayerScale));
        harness.TickServer();
        Assert.False(harness.TryGetOpenStation(player.ServerEntity, out _));
        Assert.False(harness.Replication.IsViewerSubscribed(coreEntity, player.ServerPeer.GameId));

        //  Move back in reach and reopen for case 2: CloseStation action.
        harness.ServerStore.AddOrUpdate(player.ServerEntity, new TransformComponent(new Vector3(1f, 0f, 2f), Quaternion.Identity, CharacterDefaults.PlayerScale));
        harness.StageActivate(player, sequence: 2, structureUuid, coord);
        harness.TickServer();
        Assert.True(harness.TryGetOpenStation(player.ServerEntity, out _));

        harness.StageStationAction(player, sequence: 1, new StationActionOp
        {
            TargetEntity = harness.ServerStore.GetUuid(coreEntity).ToValue(),
            Action = StationActionKind.CloseStation,
        });
        harness.TickServer();
        Assert.False(harness.TryGetOpenStation(player.ServerEntity, out _));
        Assert.False(harness.Replication.IsViewerSubscribed(coreEntity, player.ServerPeer.GameId));

        //  3. Player death auto-close and rejection while dead.
        harness.ServerStore.AddOrUpdate(player.ServerEntity, new GameModeComponent(GameMode.Adventure));
        harness.ServerStore.AddOrUpdate(player.ServerEntity, new StatisticsComponent(
        [
            new Statistic(StatisticIds.HEALTH_MAX, StatisticIds.DEFAULT_HEALTH_MAX_FIXED),
            new Statistic(StatisticIds.HEALTH, StatisticIds.DEFAULT_HEALTH_MAX_FIXED),
        ]));
        harness.StageActivate(player, sequence: 3, structureUuid, coord);
        harness.TickServer();
        Assert.True(harness.TryGetOpenStation(player.ServerEntity, out _));

        DamageResult fatal = DamageService.ApplyDamage(harness.ServerStore, player.ServerEntity, 200f, DamageSource.Suffocation, simTick: 10L);
        Assert.True(fatal.Fatal);
        harness.TickServer();
        Assert.False(harness.TryGetOpenStation(player.ServerEntity, out _));
        Assert.False(harness.Replication.IsViewerSubscribed(coreEntity, player.ServerPeer.GameId));

        //  Dead player cannot reopen the station.
        harness.StageActivate(player, sequence: 4, structureUuid, coord);
        harness.TickServer();
        Assert.False(harness.TryGetOpenStation(player.ServerEntity, out _));

        //  Revive player for case 4: brick broken while open.
        harness.ServerStore.SetStatisticFixed(player.ServerEntity, StatisticIds.HEALTH, StatisticIds.DEFAULT_HEALTH_MAX);
        harness.StageActivate(player, sequence: 5, structureUuid, coord);
        harness.TickServer();
        Assert.True(harness.TryGetOpenStation(player.ServerEntity, out _));

        Assert.True(harness.BrickEntities.OnBrickBroken(harness.ServerStore, structureUuid, coord));
        Assert.False(harness.TryGetOpenStation(player.ServerEntity, out _));
        Assert.Equal(0, harness.Replication.ActiveViewerSubscriptionCount);

        //  5. Disconnect cleanup: reopen a newly placed brick-entity, then disconnect.
        int replacedEntity = harness.BrickEntities.OnBrickPlaced(harness.ServerStore, structureUuid, coord, bricks.Id("wb:core"));
        harness.StageActivate(player, sequence: 6, structureUuid, coord);
        harness.TickServer();
        Assert.True(harness.TryGetOpenStation(player.ServerEntity, out _));
        Assert.True(harness.Replication.IsViewerSubscribed(replacedEntity, player.ServerPeer.GameId));

        harness.Stations.OnClientDisconnected(harness.ServerStore, player.ServerPeer.GameId, player.ServerEntity);
        harness.Replication.CancelStream(player.ServerPeer.GameId);
        Assert.False(harness.TryGetOpenStation(player.ServerEntity, out _));
        Assert.Equal(0, harness.Replication.ActiveViewerSubscriptionCount);
    }

    [Fact]
    public async Task StationActionOpDeduplicatesSequencesOrdersOutOfOrderOpsAndRejectsReplays()
    {
        BrickDatabase bricks = LoadShippedBricks();
        var customHandler = new RecordingStationActionHandler(StationActionKind.Custom, "mod:pulse");
        using LoopbackHarness harness = LoopbackHarness.Create(bricks, extraStationActionHandlers: [customHandler]);
        LoopbackClient player = await harness.AddPlayerClientAsync(Uuid.FromValue(3030), Vector3.Zero);

        //  Stage out-of-order sequences (3, 1, 2) and a duplicate of sequence 2.
        harness.StageStationAction(player, sequence: 3, CustomOp("mod:pulse", "third", 3u));
        harness.StageStationAction(player, sequence: 1, CustomOp("mod:pulse", "first", 1u));
        harness.StageStationAction(player, sequence: 2, CustomOp("mod:pulse", "second-stale", 99u));
        harness.StageStationAction(player, sequence: 2, CustomOp("mod:pulse", "second", 2u));
        harness.TickServer();

        Assert.Equal(["first", "second", "third"], customHandler.Arguments);
        Assert.True(harness.ServerStore.TryGet(player.ServerEntity, out NetworkComponent net));
        Assert.Equal(3u, net.LastConsumedStationActionSequence);

        //  Replaying sequence <= 3 is ignored completely.
        harness.StageStationAction(player, sequence: 2, CustomOp("mod:pulse", "replay-2", 1u));
        harness.StageStationAction(player, sequence: 3, CustomOp("mod:pulse", "replay-3", 1u));
        harness.TickServer();
        Assert.Equal(3, customHandler.Arguments.Count);

        //  An unhandled/rejected op still advances the sequence watermark so it cannot replay.
        harness.StageStationAction(player, sequence: 4, CustomOp("mod:unknown", "bad", 1u));
        harness.TickServer();
        Assert.True(harness.ServerStore.TryGet(player.ServerEntity, out net));
        Assert.Equal(4u, net.LastConsumedStationActionSequence);

        //  A fresh session resets the watermark so low sequence numbers are accepted again.
        harness.ServerStore.AddOrUpdate(player.ServerEntity, NetworkComponent.Create());
        harness.StageStationAction(player, sequence: 1, CustomOp("mod:pulse", "after-rejoin", 1u));
        harness.TickServer();
        Assert.Equal("after-rejoin", customHandler.Arguments[^1]);
    }

    [Fact]
    public async Task TogglePowerAndCustomStationActionsEnforceReachOwnershipLockPermissionAndWireBounds()
    {
        BrickDatabase bricks = LoadShippedBricks();
        var permissions = new StubPermissionService();
        using LoopbackHarness harness = LoopbackHarness.Create(bricks, permissions: permissions);
        LoopbackClient owner = await harness.AddPlayerClientAsync(Uuid.FromValue(3040), new Vector3(1f, 0f, 2f));
        LoopbackClient intruder = await harness.AddPlayerClientAsync(Uuid.FromValue(3041), new Vector3(1f, 0f, 2f));

        Uuid structureUuid = Uuid.FromValue(93040);
        var coreCoord = new Int3(1, 0, 0);
        var storageCoord = new Int3(2, 0, 0);
        harness.CreateStructure(
            structureUuid,
            Vector3.Zero,
            Quaternion.Identity,
            (coreCoord, bricks.Id("wb:core")),
            (storageCoord, bricks.Id("wb:storage")));

        int coreEntity = harness.BrickEntities.OnBrickPlaced(harness.ServerStore, structureUuid, coreCoord, bricks.Id("wb:core"));
        int storageEntity = harness.BrickEntities.OnBrickPlaced(harness.ServerStore, structureUuid, storageCoord, bricks.Id("wb:storage"));
        ulong coreUuid = harness.ServerStore.GetUuid(coreEntity).ToValue();
        ulong storageUuid = harness.ServerStore.GetUuid(storageEntity).ToValue();

        //  Default wb:core starts powered on; TogglePower in reach toggles it off.
        Assert.True(harness.ServerStore.TryGet(coreEntity, out MachineStateComponent machine));
        Assert.True(machine.PowerEnabled);

        harness.StageStationAction(owner, sequence: 1, new StationActionOp { TargetEntity = coreUuid, Action = StationActionKind.TogglePower });
        harness.TickServer();
        Assert.True(harness.ServerStore.TryGet(coreEntity, out machine));
        Assert.False(machine.PowerEnabled);

        //  Non-toggleable storage brick is rejected.
        Assert.True(harness.ServerStore.TryGet(storageEntity, out MachineStateComponent storageMachine));
        bool initialStoragePower = storageMachine.PowerEnabled;
        harness.StageStationAction(owner, sequence: 2, new StationActionOp { TargetEntity = storageUuid, Action = StationActionKind.TogglePower });
        harness.TickServer();
        Assert.True(harness.ServerStore.TryGet(storageEntity, out storageMachine));
        Assert.Equal(initialStoragePower, storageMachine.PowerEnabled);

        //  Lock core to owner + require permission: intruder is rejected, owner without permission is rejected, owner with permission succeeds.
        harness.ServerStore.AddOrUpdate(coreEntity, new ContainerAccessComponent
        {
            Owner = owner.EntityUuid,
            IsLocked = true,
            MaxReach = CharacterDefaults.DEFAULT_REACH,
            RequiredPermission = "wb.station.power",
        });

        permissions.Grant(intruder.ServerPeer.GameId, "wb.station.power");
        harness.StageStationAction(intruder, sequence: 1, new StationActionOp { TargetEntity = coreUuid, Action = StationActionKind.TogglePower });
        harness.TickServer();
        Assert.True(harness.ServerStore.TryGet(coreEntity, out machine));
        Assert.False(machine.PowerEnabled);

        harness.StageStationAction(owner, sequence: 3, new StationActionOp { TargetEntity = coreUuid, Action = StationActionKind.TogglePower });
        harness.TickServer();
        Assert.True(harness.ServerStore.TryGet(coreEntity, out machine));
        Assert.False(machine.PowerEnabled);

        permissions.Grant(owner.ServerPeer.GameId, "wb.station.power");
        harness.StageStationAction(owner, sequence: 4, new StationActionOp { TargetEntity = coreUuid, Action = StationActionKind.TogglePower });
        harness.TickServer();
        Assert.True(harness.ServerStore.TryGet(coreEntity, out machine));
        Assert.True(machine.PowerEnabled);

        //  Out-of-reach TogglePower is rejected.
        harness.ServerStore.AddOrUpdate(owner.ServerEntity, new TransformComponent(new Vector3(40f, 0f, 0f), Quaternion.Identity, CharacterDefaults.PlayerScale));
        harness.StageStationAction(owner, sequence: 5, new StationActionOp { TargetEntity = coreUuid, Action = StationActionKind.TogglePower });
        harness.TickServer();
        Assert.True(harness.ServerStore.TryGet(coreEntity, out machine));
        Assert.True(machine.PowerEnabled);

        //  Targeting another player entity or TargetEntity == 0 is rejected.
        harness.ServerStore.AddOrUpdate(owner.ServerEntity, new TransformComponent(new Vector3(1f, 0f, 2f), Quaternion.Identity, CharacterDefaults.PlayerScale));
        harness.StageStationAction(owner, sequence: 6, new StationActionOp { TargetEntity = intruder.EntityUuid.ToValue(), Action = StationActionKind.TogglePower });
        harness.StageStationAction(owner, sequence: 7, new StationActionOp { TargetEntity = 0UL, Action = StationActionKind.TogglePower });
        harness.TickServer();
        Assert.True(harness.ServerStore.TryGet(coreEntity, out machine));
        Assert.True(machine.PowerEnabled);

        //  Oversized wire strings and undefined enum values fail bounds validation and never stage through replication.
        var overlongAction = new StationActionOp
        {
            TargetEntity = 0UL,
            Action = StationActionKind.Custom,
            ActionId = new string('x', StationActionOp.MAX_ACTION_ID_LENGTH + 1),
        };
        var overlongArgument = new StationActionOp
        {
            TargetEntity = 0UL,
            Action = StationActionKind.Custom,
            ActionId = "mod:ok",
            Argument = new string('a', StationActionOp.MAX_ARGUMENT_LENGTH + 1),
        };
        var undefinedKind = new StationActionOp
        {
            TargetEntity = 0UL,
            Action = (StationActionKind)99,
        };
        Assert.False(overlongAction.IsWithinWireBounds());
        Assert.False(overlongArgument.IsWithinWireBounds());
        Assert.False(undefinedKind.IsWithinWireBounds());

        harness.DeliverClientSnapshot(owner, new StationActionEvent
        {
            Entity = owner.EntityUuid.ToValue(),
            SequenceNumber = 8u,
            Action = overlongAction,
        });
        harness.Replication.ApplyStage(0f, harness.ServerStore);
        Assert.True(harness.ServerStore.TryGet(owner.ServerEntity, out NetworkComponent ownerNet));
        Assert.Equal(0, ownerNet.StagedStationActions!.Count);
    }

    [Fact]
    public void ClientActivateShortcutHonorsBlockerBlueprintToolRebindAndZeroPrediction()
    {
        using var harness = new ClientActivateHarness();
        GameState previous = WaywardBeyondClient.GameState.Get();
        WaywardBeyondClient.GameState.Set(GameState.Playing);
        try
        {
            //  First tick syncs held-item state; pressing [F] stages InteractionKind.Activate targeting the raycasted brick with zero local prediction.
            harness.TickClient();
            harness.PressKey(Key.F);
            harness.TickClient();

            InteractionEvent staged = Assert.Single(harness.StagedEvents);
            Assert.Equal(InteractionKind.Activate, staged.Kind);
            Assert.NotNull(staged.Brick);
            Assert.Equal(harness.StructureUuid.ToValue(), staged.Brick!.Value.TargetEntity);
            Assert.Equal(harness.TargetCoord.X, staged.Brick.Value.TargetX);
            Assert.Equal(harness.TargetCoord.Y, staged.Brick.Value.TargetY);
            Assert.Equal(harness.TargetCoord.Z, staged.Brick.Value.TargetZ);
            Assert.False(harness.Store.TryGet(harness.PlayerEntity, out ActiveStationComponent _));

            //  Blocked while InteractionState.IsInteractionBlocked() is true (e.g. chat/menus).
            harness.StagedEvents.Clear();
            using (harness.InteractionState.BlockInteraction())
            {
                harness.PressKey(Key.F);
                harness.TickClient();
            }

            Assert.Empty(harness.StagedEvents);

            //  Suppressed while holding the Blueprint Projector so [F] belongs to blueprint mirror.
            harness.EquipProjector();
            harness.TickClient();
            harness.PressKey(Key.F);
            harness.TickClient();
            Assert.Empty(harness.StagedEvents);

            //  Rebindable via ShortcutService.
            harness.EquipEmpty();
            harness.TickClient();
            Assert.True(harness.Shortcuts.Rebind(GameShortcuts.ACTIVATE, new ShortcutBinding(Key.H)).Success);
            harness.PressKey(Key.F);
            harness.TickClient();
            Assert.Empty(harness.StagedEvents);

            harness.PressKey(Key.H);
            harness.TickClient();
            Assert.Single(harness.StagedEvents);
            Assert.Equal(InteractionKind.Activate, harness.StagedEvents[0].Kind);
        }
        finally
        {
            WaywardBeyondClient.GameState.Set(previous);
        }
    }

    [Fact]
    public void ClientStationSystemSynchronizesContextualTabTerminalAndCloseStationWithoutPrediction()
    {
        GameState previous = WaywardBeyondClient.GameState.Get();
        WaywardBeyondClient.GameState.Set(GameState.Playing);
        try
        {
            var world = new World();
            DataStore store = world.DataStore;
            var input = new FakeInputService();
            var shortcuts = new ShortcutService(input, NullLogger.Instance);
            var motionProcessor = new ClientPlayerMotionProcessor(input, new FakeWindowContext(), new FakePhysics(), new EventInvoker<PlayerMovedEvent>([]));
            var interactionState = new InteractionState(motionProcessor);
            var items = new StubItemAssetDatabase();
            var bricks = LoadShippedBricks();
            var playerData = new PlayerData(items);
            var stationState = new ClientStationState();
            var stationSystem = new ClientStationSystem(stationState, playerData);
            var cleanup = new ClientCleanupSystem(NullLogger<ClientCleanupSystem>.Instance, stationState: stationState);

            int player = store.Alloc(Uuid.FromValue(7001));
            store.AddOrUpdate(player, new PlayerComponent());
            store.AddOrUpdate(player, InventoryComponent.CreatePlayer());
            store.AddOrUpdate(player, new EquipmentComponent(0));
            store.AddOrUpdate(player, new PendingInventoryComponent());
            store.AddOrUpdate(player, new PendingStationActionComponent());

            var icons = new ItemIconDatabase(
                NullLogger<ItemIconDatabase>.Instance,
                new StubFileParseService(),
                new StubTextureDatabase(),
                items);
            var inventory = new Inventory(
                shortcuts,
                items,
                bricks,
                new EmptyTagDatabase(),
                icons,
                playerData,
                new StubEcsContext(world),
                interactionState,
                input,
                new TestLocalization(),
                new DebugSettings(),
                stationState);

            Assert.False(inventory.IsVisible());
            Assert.False(stationState.HasOpenStation);

            //  Server replicates ContextualTab ActiveStationComponent -> ClientStationSystem updates state and opens [Tab].
            var storageStation = new ActiveStationComponent(501UL, 901UL, 1, 2, 3, StationTags.STORAGE, StationUiKind.ContextualTab);
            store.AddOrUpdate(player, storageStation);
            stationSystem.Tick(1f / 60f, store);

            Assert.True(stationState.IsContextualTabOpen);
            Assert.True(inventory.IsVisible());
            Assert.True(interactionState.IsInteractionBlocked());

            //  Pressing Esc closes [Tab] and enqueues CloseStation without mutating ActiveStationComponent locally.
            input.KeyPressed?.Invoke(input, new KeyEventArgs(Key.Esc));
            Assert.False(inventory.IsVisible());
            Assert.False(interactionState.IsInteractionBlocked());
            Assert.True(store.TryGet(player, out ActiveStationComponent stillServerOwned) && stillServerOwned.IsOpen);

            stationSystem.Tick(1f / 60f, store);
            Assert.True(store.TryGet(player, out PendingStationActionComponent pending));
            Assert.Equal(1, pending.Outbound.Count);
            Assert.Equal(1u, pending.Outbound[0].SequenceNumber);
            Assert.Equal(StationActionKind.CloseStation, pending.Outbound[0].Op.Action);
            Assert.Equal(501UL, pending.Outbound[0].Op.TargetEntity);
            pending.Outbound.Clear();

            //  Server confirms close -> then opens another ContextualTab -> then server auto-closes it -> [Tab] auto-closes without queuing CloseStation.
            store.AddOrUpdate(player, ActiveStationComponent.None);
            stationSystem.Tick(1f / 60f, store);
            Assert.False(stationState.HasOpenStation);

            var coreStation = new ActiveStationComponent(502UL, 901UL, 2, 0, 0, StationTags.CORE, StationUiKind.ContextualTab);
            store.AddOrUpdate(player, coreStation);
            stationSystem.Tick(1f / 60f, store);
            Assert.True(inventory.IsVisible());

            store.AddOrUpdate(player, ActiveStationComponent.None);
            stationSystem.Tick(1f / 60f, store);
            Assert.False(inventory.IsVisible());
            Assert.False(interactionState.IsInteractionBlocked());
            Assert.True(store.TryGet(player, out pending));
            Assert.Equal(0, pending.Outbound.Count);

            //  Server replicates Terminal ActiveStationComponent -> IsTerminalOpen becomes true, [Tab] does NOT open.
            var terminalStation = new ActiveStationComponent(503UL, 901UL, 3, 0, 0, StationTags.CONSOLE, StationUiKind.Terminal);
            store.AddOrUpdate(player, terminalStation);
            stationSystem.Tick(1f / 60f, store);
            Assert.True(stationState.IsTerminalOpen);
            Assert.False(stationState.IsContextualTabOpen);
            Assert.False(inventory.IsVisible());

            //  ClientCleanupSystem resets station state on disconnect.
            cleanup.RequestCleanup();
            cleanup.Tick(1f / 60f, store);
            Assert.False(stationState.HasOpenStation);
            Assert.False(stationState.IsTerminalOpen);
        }
        finally
        {
            WaywardBeyondClient.GameState.Set(previous);
        }
    }

    [Fact]
    public async Task ClientReplicationDrainsStationActionsOverWireToServerWithoutLocalPrediction()
    {
        BrickDatabase bricks = LoadShippedBricks();
        using LoopbackHarness harness = LoopbackHarness.Create(bricks);
        LoopbackClient player = await harness.AddPlayerClientAsync(Uuid.FromValue(3050), new Vector3(1f, 0f, 2f));

        Uuid structureUuid = Uuid.FromValue(93050);
        var coreCoord = new Int3(1, 0, 0);
        harness.CreateStructure(structureUuid, Vector3.Zero, Quaternion.Identity, (coreCoord, bricks.Id("wb:core")));
        int coreEntity = harness.BrickEntities.OnBrickPlaced(harness.ServerStore, structureUuid, coreCoord, bricks.Id("wb:core"));
        ulong coreUuid = harness.ServerStore.GetUuid(coreEntity).ToValue();

        //  Open the station server-side first so we can verify both TogglePower and CloseStation over the wire.
        harness.StageActivate(player, sequence: 1, structureUuid, coreCoord);
        harness.TickServer();
        Assert.True(harness.TryGetOpenStation(player.ServerEntity, out _));
        Assert.True(harness.ServerStore.TryGet(coreEntity, out MachineStateComponent beforeMachine));
        Assert.True(beforeMachine.PowerEnabled);

        //  Stage TogglePower and CloseStation on the client store via PlayerData and drain through ClientReplicationSystem.
        var clientStore = new DataStore();
        int localPlayer = clientStore.Alloc(player.EntityUuid);
        clientStore.AddOrUpdate(localPlayer, new PlayerComponent());
        clientStore.AddOrUpdate(localPlayer, new PendingStationActionComponent());
        clientStore.AddOrUpdate(localPlayer, new ActiveStationComponent(coreUuid, structureUuid.ToValue(), coreCoord.X, coreCoord.Y, coreCoord.Z, StationTags.CORE, StationUiKind.ContextualTab));

        var playerData = new PlayerData(new StubItemAssetDatabase());
        playerData.StageStationAction(clientStore, new StationActionOp
        {
            TargetEntity = coreUuid,
            Action = StationActionKind.TogglePower,
        });
        playerData.StageStationAction(clientStore, new StationActionOp
        {
            TargetEntity = coreUuid,
            Action = StationActionKind.CloseStation,
        });

        //  Verify zero client prediction: local ActiveStationComponent stays unchanged until server confirms.
        Assert.True(clientStore.TryGet(localPlayer, out ActiveStationComponent localStation) && localStation.IsOpen);
        Assert.True(clientStore.TryGet(localPlayer, out PendingStationActionComponent pending));
        Assert.Equal(2, pending.Outbound.Count);

        //  Deliver through the real wire serializer and server ApplyStage -> ServerStationActionSystem.
        for (var i = 0; i < pending.Outbound.Count; i++)
        {
            StagedStationAction staged = pending.Outbound[i];
            harness.DeliverClientSnapshot(player, new StationActionEvent
            {
                Entity = player.EntityUuid.ToValue(),
                SequenceNumber = staged.SequenceNumber,
                Action = staged.Op,
            });
        }

        harness.Replication.ApplyStage(1f / 60f, harness.ServerStore);
        harness.TickServer();

        Assert.True(harness.ServerStore.TryGet(coreEntity, out MachineStateComponent afterMachine));
        Assert.False(afterMachine.PowerEnabled);
        Assert.False(harness.TryGetOpenStation(player.ServerEntity, out _));
        Assert.False(harness.Replication.IsViewerSubscribed(coreEntity, player.ServerPeer.GameId));
    }

    private static StationActionOp CustomOp(string actionId, string argument, uint count)
    {
        return new StationActionOp
        {
            TargetEntity = 0UL,
            Action = StationActionKind.Custom,
            ActionId = actionId,
            Argument = argument,
            Count = count,
        };
    }

    private static BrickDatabase LoadShippedBricks()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo("assets"));
        IFileParser[] parsers = [new TomlParser<BrickDefinitions>(), new LocalizedTagDefinitionParser()];
        var parseService = new VirtualFileParseService(parsers, vfs);
        var tags = new LocalizedTagsDatabase(NullLogger<LocalizedTagsDatabase>.Instance, parseService, vfs);
        return new BrickDatabase(NullLogger<BrickDatabase>.Instance, parseService, vfs, tags);
    }

    private sealed class RecordingActivateHandler(
        InteractionStage stage,
        InteractionPriority priority,
        InteractionDecision decision
    ) : IInteractionHandler
    {
        public int CallCount { get; private set; }
        public InteractionAction LastAction { get; private set; }

        public InteractionStage Stage => stage;
        public InteractionPriority Priority => priority;
        public InteractionHandlerFilter Filter => new(Kind: InteractionKind.Activate);

        public InteractionDecision Handle(ref InteractionContext context)
        {
            CallCount++;
            LastAction = context.Resolution.Action;
            return decision;
        }
    }

    private sealed class RecordingStationActionHandler(
        StationActionKind action,
        string? actionId = null,
        InteractionPriority priority = InteractionPriority.Normal
    ) : IStationActionHandler
    {
        public List<string?> Arguments { get; } = [];

        public InteractionPriority Priority => priority;
        public StationActionFilter Filter => new(Action: action, ActionId: actionId);

        public StationActionResult Handle(in StationActionContext context)
        {
            Arguments.Add(context.Op.Argument);
            return StationActionResult.Handled;
        }
    }

    private sealed class StubPermissionService : IUserPermissionService
    {
        private readonly HashSet<(Uuid ClientId, string Permission)> _granted = [];

        public void Grant(Uuid clientId, string permission) => _granted.Add((clientId, permission));

        public void Bind(Uuid clientId, in UserClaim claim, bool isHost) { }

        public void Unbind(Uuid clientId) { }

        public bool HasPermission(Uuid clientId, string permission) => _granted.Contains((clientId, permission));
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
        public ServerStationService Stations { get; }
        public ServerInteractionSystem InteractionSystem { get; }
        public ServerStationActionSystem StationActionSystem { get; }
        public ServerBrickEntitySystem BrickEntitySystem { get; }
        public ulong InventoryTypeUuid { get; }
        public ulong MachineStateTypeUuid { get; }
        public ulong ActiveStationTypeUuid { get; }
        public ulong StationActionTypeUuid { get; }

        private LoopbackHarness(
            NetworkRegistry registry,
            BrickDatabase bricks,
            IUserPermissionService? permissions,
            IInteractionHandler[]? extraInteractionHandlers,
            IStationActionHandler[]? extraStationActionHandlers
        )
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
                Sessions,
                permissions: permissions);
            Stations = new ServerStationService(
                BrickEntities,
                Replication,
                Sessions,
                permissions);

            var interactionHandlers = new List<IInteractionHandler>
            {
                new HintResolver(bricks),
                new StationActivateCommitter(),
            };
            if (extraInteractionHandlers != null)
            {
                interactionHandlers.AddRange(extraInteractionHandlers);
            }

            var interactionRegistry = new InteractionHandlerRegistry(interactionHandlers);

            var stationHandlers = new List<IStationActionHandler>
            {
                new CloseStationActionHandler(),
                new TogglePowerStationActionHandler(bricks),
            };
            if (extraStationActionHandlers != null)
            {
                stationHandlers.AddRange(extraStationActionHandlers);
            }

            var stationActionRegistry = new StationActionHandlerRegistry(stationHandlers);
            var physics = new StubServerWorldPhysics();
            var simStep = new SimulationStep(ServerStore, physics);

            InteractionSystem = new ServerInteractionSystem(
                Sessions,
                NullLogger<ServerInteractionSystem>.Instance,
                interactionRegistry,
                store => new ServerVoxelInteractionWorld(store, physics),
                simStep,
                BrickEntities,
                Stations);
            StationActionSystem = new ServerStationActionSystem(
                Sessions,
                stationActionRegistry,
                Stations,
                simStep,
                NullLogger<ServerStationActionSystem>.Instance);
            BrickEntitySystem = new ServerBrickEntitySystem(BrickEntities, Stations);

            InventoryTypeUuid = registry.GetInfo<InventoryComponent>().Value.Uuid.ToValue();
            MachineStateTypeUuid = registry.GetInfo<MachineStateComponent>().Value.Uuid.ToValue();
            ActiveStationTypeUuid = registry.GetInfo<ActiveStationComponent>().Value.Uuid.ToValue();
            StationActionTypeUuid = registry.GetInfo<StationActionEvent>().Value.Uuid.ToValue();

            _transport = new LoopbackRpcServerTransport();
            var serverRpcRegistry = new RpcRegistry();
            serverRpcRegistry.Freeze();
            _rpcServer = new RpcServer(_transport, serverRpcRegistry);
            _rpcServer.ClientConnected += OnClientConnected;
            _rpcServer.Start();
        }

        public static LoopbackHarness Create(
            BrickDatabase bricks,
            IUserPermissionService? permissions = null,
            IInteractionHandler[]? extraInteractionHandlers = null,
            IStationActionHandler[]? extraStationActionHandlers = null
        )
        {
            using var container = new Container();
            container.RegisterNetworking();
            return new LoopbackHarness(
                container.Resolve<NetworkRegistry>(),
                bricks,
                permissions,
                extraInteractionHandlers,
                extraStationActionHandlers);
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
            ServerStore.AddOrUpdate(entity, new OwnedCharacterComponent());
            ServerStore.AddOrUpdate(entity, new GameModeComponent(GameMode.Adventure));
            ServerStore.AddOrUpdate(entity, new TransformComponent(position, Quaternion.Identity, CharacterDefaults.PlayerScale));
            ServerStore.AddOrUpdate(entity, InventoryComponent.CreatePlayer());
            Sessions.Register(ServerStore, entity, serverPeer.GameId, new Session(_nextSessionId++));

            var client = new LoopbackClient(connection, inbound, serverPeer, entity, entityUuid);
            _clients.Add(client);
            return client;
        }

        public void StageActivate(LoopbackClient client, uint sequence, Uuid structureUuid, Int3 coord)
        {
            Assert.True(ServerStore.TryGet(client.ServerEntity, out NetworkComponent net));
            net.StagedInteractions!.Stage(new InteractionEvent
            {
                Entity = client.EntityUuid.ToValue(),
                SequenceNumber = sequence,
                ServerTickAtSample = 0u,
                Kind = InteractionKind.Activate,
                Brick = new BrickInteraction
                {
                    TargetEntity = structureUuid.ToValue(),
                    TargetX = coord.X,
                    TargetY = coord.Y,
                    TargetZ = coord.Z,
                },
            });
        }

        public void StageStationAction(LoopbackClient client, uint sequence, in StationActionOp op)
        {
            Assert.True(ServerStore.TryGet(client.ServerEntity, out NetworkComponent net));
            net.StagedStationActions!.Stage(sequence, in op);
        }

        public void DeliverClientSnapshot(LoopbackClient client, in StationActionEvent actionEvent)
        {
            byte[] payload = actionEvent.Serialize();
            client.ServerPeer.Snapshots.Enqueue(new WorldSnapshot
            {
                TickNumber = 0u,
                LastProcessedInput = 0u,
                Components =
                [
                    new ComponentSnapshot(client.EntityUuid.ToValue(), StationActionTypeUuid, payload),
                ],
                RemovedEntities = [],
                RemovedComponents = [],
            });
        }

        public void TickServer(float delta = 1f / 60f)
        {
            StationActionSystem.Tick(delta, ServerStore);
            InteractionSystem.Tick(delta, ServerStore);
            BrickEntitySystem.Tick(delta, ServerStore);
        }

        public bool TryGetOpenStation(int playerEntity, out ActiveStationComponent active)
        {
            return ServerStore.TryGet(playerEntity, out active) && active.IsOpen;
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

    private sealed class ClientActivateHarness : IDisposable
    {
        private readonly FakeInputService _input = new();
        private readonly FakePhysics _physics = new();
        private readonly NotificationService _notifications;
        private readonly PlayerInteractionService _service;
        private readonly string _tempAssetRoot;

        public DataStore Store { get; } = new();
        public ShortcutService Shortcuts { get; }
        public InteractionState InteractionState { get; }
        public Uuid StructureUuid { get; } = Uuid.FromValue(95001);
        public Int3 TargetCoord { get; } = new(0, 0, -2);
        public int PlayerEntity { get; }
        public int StructureEntity { get; }
        public List<InteractionEvent> StagedEvents { get; } = [];

        public ClientActivateHarness()
        {
            Shortcuts = new ShortcutService(_input, NullLogger.Instance);
            BrickDatabase bricks = LoadShippedBricks();

            _tempAssetRoot = Path.Combine(Path.GetTempPath(), $"wb-activate-test-{Path.GetRandomFileName()}");
            string itemsDir = Path.Combine(_tempAssetRoot, "items");
            Directory.CreateDirectory(itemsDir);
            File.WriteAllText(
                Path.Combine(itemsDir, "tools.toml"),
                """
                [[Items]]
                ID = "wb:blueprint_projector"
                Name = "Blueprint Projector"
                MaxStack = 1
                [Items.Tool]
                Type = "Blueprint"
                """);
            var itemVfs = new VirtualFileSystem();
            itemVfs.Mount(new PathInfo(_tempAssetRoot));
            var itemParser = new VirtualFileParseService([new TomlParser<ItemDefinitions>()], itemVfs);
            var items = new ItemDatabase(NullLogger<ItemDatabase>.Instance, itemParser, itemVfs);

            StructureEntity = Store.Alloc(StructureUuid);
            var voxelObject = new VoxelObject(VoxelObject.DEFAULT_CHUNK_EDGE);
            voxelObject.Set(TargetCoord.X, TargetCoord.Y, TargetCoord.Z, new Voxel { ID = bricks.Id("wb:storage") });
            Store.AddOrUpdate(StructureEntity, new VoxelComponent(voxelObject));
            Store.AddOrUpdate(StructureEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));

            PlayerEntity = Store.Alloc(Uuid.FromValue(8001));
            Store.AddOrUpdate(PlayerEntity, new PlayerComponent());
            Store.AddOrUpdate(PlayerEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            Store.AddOrUpdate(PlayerEntity, new GameModeComponent(GameMode.Adventure));
            Store.AddOrUpdate(PlayerEntity, new EquipmentComponent(0));
            InventoryComponent inv = InventoryComponent.Create(new SlotGroupDescriptor(SlotGroups.HOTBAR, 4));
            inv.Set(new SlotId(SlotGroups.HOTBAR, 1), new ItemData("wb:blueprint_projector", 1, 1));
            Store.AddOrUpdate(PlayerEntity, inv);
            Store.AddOrUpdate(PlayerEntity, new PendingInteractionComponent(new PendingInteractionQueue()));

            var window = new FakeWindowContext { Resolution = new Vector2(1280f, 720f) };
            var motionProcessor = new ClientPlayerMotionProcessor(_input, window, _physics, new EventInvoker<PlayerMovedEvent>([]));
            InteractionState = new InteractionState(in motionProcessor);
            _notifications = new NotificationService(NullLogger<NotificationService>.Instance, window);

            var meshDb = new StubMeshDatabase();
            var emptyMesh = new Mesh([], [], [], [], []);
            meshDb.Assets["slope.obj"] = emptyMesh;
            meshDb.Assets["stair.obj"] = emptyMesh;
            meshDb.Assets["slab.obj"] = emptyMesh;
            meshDb.Assets["column.obj"] = emptyMesh;
            meshDb.Assets["plate.obj"] = emptyMesh;

            _physics.NextHit = new RaycastResult(
                hit: true,
                new Entity(StructureEntity, Store),
                new Vector3(TargetCoord.X, TargetCoord.Y, TargetCoord.Z + 0.5f),
                new Vector3(0f, 0f, 1f));

            _service = new PlayerInteractionService(
                InteractionState,
                new BrickPreviewState(),
                _physics,
                new NullLineRenderer(),
                new FakeRenderContext(Store),
                window,
                new PlayerData(items),
                bricks,
                bricks,
                items,
                new DebugSettings(),
                meshDb,
                Shortcuts,
                new VoxelLightingService(bricks),
                new EventInvoker<PlaceEvent>([]),
                new EventInvoker<BreakEvent>([]),
                new EventInvoker<BrickEvent>([]),
                new SnapshotAckTracker(),
                _notifications,
                new TestLocalization());
        }

        public void PressKey(Key key) => _input.KeyPressed?.Invoke(_input, new KeyEventArgs(key));

        public void EquipProjector() => Store.AddOrUpdate(PlayerEntity, new EquipmentComponent(1));

        public void EquipEmpty() => Store.AddOrUpdate(PlayerEntity, new EquipmentComponent(0));

        public void TickClient(float delta = 1f / 60f)
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
            if (Directory.Exists(_tempAssetRoot))
            {
                Directory.Delete(_tempAssetRoot, recursive: true);
            }
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

    private sealed class FakePhysics : IPhysics
    {
        public RaycastResult NextHit { get; set; }

        public event EventHandler<EventArgs>? FixedUpdate
        {
            add { }
            remove { }
        }

        public float FixedTimestamp => 1f / 60f;
        public RaycastResult Raycast(in Ray ray) => NextHit;
        public bool Overlaps(in ShapeOverlapQuery query) => false;
        public void SetGravity(Vector3 gravity) { }
    }

    private sealed class StubMeshDatabase : IAssetDatabase<Mesh>
    {
        public Dictionary<string, Mesh> Assets { get; } = new(StringComparer.Ordinal);
        public Result<Mesh> Get(string id) => Assets.TryGetValue(id, out Mesh? m) ? Result<Mesh>.FromSuccess(m) : Result<Mesh>.FromFailure(id);
    }

    private sealed class NullLineRenderer : ILineRenderer
    {
        public Line CreateLine(bool alwaysOnTop = false) => new(this, Vector3.Zero, Vector3.Zero, Vector4.Zero);
        public Line CreateLine(Vector3 start, Vector3 end, bool alwaysOnTop = false) => new(this, start, end, Vector4.Zero);
        public Line CreateLine(Vector3 start, Vector3 end, Vector4 color, bool alwaysOnTop = false) => new(this, start, end, color);
        public void DeleteLine(Line line) { }
    }

    private sealed class StubItemAssetDatabase : IAssetDatabase<Item>
    {
        public Result<Item> Get(string id) => Result<Item>.FromFailure($"Unknown item \"{id}\".");
    }

    private sealed class EmptyTagDatabase : ILocalizedTagsDatabase
    {
        private readonly LocalizedTags _tags = new(new Dictionary<string, List<string>>(StringComparer.Ordinal));
        public LocalizedTags? Invariant => _tags;
        public Result<LocalizedTags> Get(string id) => Result<LocalizedTags>.FromSuccess(_tags);
    }

    private sealed class StubEcsContext(World world) : IECSContext
    {
        public DataBinding<double> Delta { get; } = new();
        public World World { get; } = world;
    }

    private sealed class TestLocalization : ILocalization
    {
        public string? GetString(string value) => value;
        public string? GetString(string value, string cultureName) => value;
        public string? GetString(string value, CultureInfo cultureInfo) => value;
    }
}
