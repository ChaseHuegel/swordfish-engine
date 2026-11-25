#nullable enable
using System;
using System.Collections.Generic;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Audio;
using Swordfish.Library.Events;
using Swordfish.Library.Util;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Saves;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.Statistics;
using WaywardBeyond.Client.Statistics.Listeners;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Characters;
using WaywardBeyond.Data.Localization;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Voxels;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Covers unified brick sound playback across local prediction, authoritative edits, and statistic isolation.</summary>
[Collection("ClientGameState")]
public class BrickSoundListenerTests
{
    private const ushort METAL_BRICK_ID = 1;
    private const ushort ROCK_BRICK_ID = 2;
    private const ushort SILENT_BRICK_ID = 3;
    private const string METAL_PLACE_CLIP = "sounds/place/metal/metal_place.1.wav";
    private const string METAL_BREAK_CLIP = "sounds/break/metal/metal_break.1.wav";
    private const string ROCK_PLACE_CLIP = "sounds/place/rock/rock_place.1.wav";
    private const string ROCK_BREAK_CLIP = "sounds/break/rock/rock_break.1.wav";

    [Fact]
    public void LocalPredictionPlacePlaysBrickSoundAndIncrementsStatistic()
    {
        Fixture fixture = CreateFixture();
        var resolution = new InteractionResolution(
            InteractionAction.Place,
            entity: 1,
            coordinate: new Int3(1, 1, 1),
            voxel: new Voxel(METAL_BRICK_ID, new ShapeLight(BrickShape.Block, 0), 0)
        );

        bool succeeded = PlayerInteractionService.FirePresentationHook(
            in resolution,
            fixture.Bricks,
            fixture.BreakEvent,
            fixture.PlaceEvent,
            fixture.BrickEditEvent
        );

        Assert.True(succeeded);
        Assert.Equal([METAL_PLACE_CLIP], fixture.DrainPlayedClips());
        Assert.Equal(1, fixture.GetStatistic("bricks.placed"));
        Assert.Equal(1, fixture.GetStatistic("bricks.placed:wb:panel"));
        Assert.Equal(0, fixture.GetStatistic("bricks.broken"));
    }

    [Fact]
    public void LocalPredictionBreakPlaysBrickSoundAndIncrementsStatistic()
    {
        Fixture fixture = CreateFixture();
        var resolution = new InteractionResolution(
            InteractionAction.Break,
            entity: 1,
            coordinate: new Int3(1, 1, 1),
            voxel: new Voxel(ROCK_BRICK_ID, new ShapeLight(BrickShape.Block, 0), 0)
        );

        bool succeeded = PlayerInteractionService.FirePresentationHook(
            in resolution,
            fixture.Bricks,
            fixture.BreakEvent,
            fixture.PlaceEvent,
            fixture.BrickEditEvent
        );

        Assert.True(succeeded);
        Assert.Equal([ROCK_BREAK_CLIP], fixture.DrainPlayedClips());
        Assert.Equal(1, fixture.GetStatistic("bricks.broken"));
        Assert.Equal(1, fixture.GetStatistic("bricks.broken:wb:rock"));
        Assert.Equal(0, fixture.GetStatistic("bricks.placed"));
    }

    [Fact]
    public void CancelledLocalPlaceEventSkipsBrickSound()
    {
        Fixture fixture = CreateFixture(cancelPlace: true);
        var resolution = new InteractionResolution(
            InteractionAction.Place,
            entity: 1,
            coordinate: new Int3(1, 1, 1),
            voxel: new Voxel(METAL_BRICK_ID, new ShapeLight(BrickShape.Block, 0), 0)
        );

        bool succeeded = PlayerInteractionService.FirePresentationHook(
            in resolution,
            fixture.Bricks,
            fixture.BreakEvent,
            fixture.PlaceEvent,
            fixture.BrickEditEvent
        );

        Assert.False(succeeded);
        Assert.Empty(fixture.DrainPlayedClips());
    }

    [Fact]
    public void CancelledLocalBreakEventSkipsBrickSound()
    {
        Fixture fixture = CreateFixture(cancelBreak: true);
        var resolution = new InteractionResolution(
            InteractionAction.Break,
            entity: 1,
            coordinate: new Int3(1, 1, 1),
            voxel: new Voxel(ROCK_BRICK_ID, new ShapeLight(BrickShape.Block, 0), 0)
        );

        bool succeeded = PlayerInteractionService.FirePresentationHook(
            in resolution,
            fixture.Bricks,
            fixture.BreakEvent,
            fixture.PlaceEvent,
            fixture.BrickEditEvent
        );

        Assert.False(succeeded);
        Assert.Empty(fixture.DrainPlayedClips());
    }

    [Fact]
    public void UnpredictedAuthoritativePlacePlaysSoundWithoutChangingLocalStatistics()
    {
        Fixture fixture = CreateFixture();
        var placedVoxel = new Voxel(METAL_BRICK_ID, new ShapeLight(BrickShape.Block, 0), 0);

        fixture.Inbound.VoxelEdits.Enqueue(new VoxelEditMessage
        {
            EntityUuid = fixture.StructureUuid.ToValue(),
            X = 1,
            Y = 1,
            Z = 1,
            Voxel = placedVoxel,
            Sequence = 0,
            BrickId = "wb:panel",
        });

        fixture.TickReconcileWhilePlaying();

        Assert.Equal([METAL_PLACE_CLIP], fixture.DrainPlayedClips());
        Assert.Equal(0, fixture.GetStatistic("bricks.placed"));
        Assert.Equal(0, fixture.GetStatistic("bricks.broken"));
    }

    [Fact]
    public void UnpredictedAuthoritativeBreakResolvesPreEditBrickAndLeavesStatisticsUnchanged()
    {
        Fixture fixture = CreateFixture();
        fixture.VoxelObject.Set(1, 1, 1, new Voxel(ROCK_BRICK_ID, new ShapeLight(BrickShape.Block, 0), 0));

        fixture.Inbound.VoxelEdits.Enqueue(new VoxelEditMessage
        {
            EntityUuid = fixture.StructureUuid.ToValue(),
            X = 1,
            Y = 1,
            Z = 1,
            Voxel = new Voxel(),
            Sequence = 0,
            BrickId = null,
        });

        fixture.TickReconcileWhilePlaying();

        Assert.Equal([ROCK_BREAK_CLIP], fixture.DrainPlayedClips());
        Assert.Equal(0, fixture.GetStatistic("bricks.placed"));
        Assert.Equal(0, fixture.GetStatistic("bricks.broken"));
    }

    [Fact]
    public void OwningPlayerPredictionEchoStaysSilent()
    {
        Fixture fixture = CreateFixture();
        var coordinate = new Int3(1, 1, 1);
        var predicted = new Voxel(METAL_BRICK_ID, new ShapeLight(BrickShape.Block, 0), 0);
        fixture.VoxelObject.Set(coordinate.X, coordinate.Y, coordinate.Z, predicted);
        fixture.PendingQueue.Register(fixture.StructureEntity, coordinate, new Voxel(), predicted, sequence: 7, serverTickAtSample: 10);

        fixture.Inbound.VoxelEdits.Enqueue(new VoxelEditMessage
        {
            EntityUuid = fixture.StructureUuid.ToValue(),
            X = coordinate.X,
            Y = coordinate.Y,
            Z = coordinate.Z,
            Voxel = predicted,
            Sequence = 7,
            BrickId = "wb:panel",
        });

        fixture.TickReconcileWhilePlaying();

        Assert.Empty(fixture.DrainPlayedClips());
    }

    [Fact]
    public void RemoteEditOnLocallyPredictedCellStaysSilentForPredictingPlayer()
    {
        Fixture fixture = CreateFixture();
        var coordinate = new Int3(1, 1, 1);
        var predicted = new Voxel(METAL_BRICK_ID, new ShapeLight(BrickShape.Block, 0), 0);
        var remoteAuthoritative = new Voxel(ROCK_BRICK_ID, new ShapeLight(BrickShape.Block, 0), 0);
        fixture.VoxelObject.Set(coordinate.X, coordinate.Y, coordinate.Z, predicted);
        fixture.PendingQueue.Register(fixture.StructureEntity, coordinate, new Voxel(), predicted, sequence: 3, serverTickAtSample: 10);

        fixture.Inbound.VoxelEdits.Enqueue(new VoxelEditMessage
        {
            EntityUuid = fixture.StructureUuid.ToValue(),
            X = coordinate.X,
            Y = coordinate.Y,
            Z = coordinate.Z,
            Voxel = remoteAuthoritative,
            Sequence = 0,
            BrickId = "wb:rock",
        });

        fixture.TickReconcileWhilePlaying();

        Assert.Empty(fixture.DrainPlayedClips());
        Assert.Equal(ROCK_BRICK_ID, fixture.VoxelObject.Get(coordinate.X, coordinate.Y, coordinate.Z).ID);
    }

    [Fact]
    public void BrickWithoutDeclaredSoundsStaysSilent()
    {
        Fixture fixture = CreateFixture();
        fixture.BrickEditEvent.Invoke(new BrickEditEvent(SILENT_BRICK_ID, BrickSoundEvent.Place));

        Assert.Empty(fixture.DrainPlayedClips());
    }

    private static Fixture CreateFixture(bool cancelPlace = false, bool cancelBreak = false)
    {
        var bricks = new StubBrickDatabase();
        bricks.Add(METAL_BRICK_ID, "wb:panel", new Dictionary<BrickSoundEvent, string[]>
        {
            [BrickSoundEvent.Place] = ["metal_place"],
            [BrickSoundEvent.Break] = ["metal_break"],
        });
        bricks.Add(ROCK_BRICK_ID, "wb:rock", new Dictionary<BrickSoundEvent, string[]>
        {
            [BrickSoundEvent.Place] = ["rock_place"],
            [BrickSoundEvent.Break] = ["rock_break"],
        });
        bricks.Add(SILENT_BRICK_ID, "wb:silent", sounds: null);

        var tags = new StubTagDatabase(new Dictionary<string, List<string>>
        {
            ["metal_place"] = [METAL_PLACE_CLIP],
            ["metal_break"] = [METAL_BREAK_CLIP],
            ["rock_place"] = [ROCK_PLACE_CLIP],
            ["rock_break"] = [ROCK_BREAK_CLIP],
        });

        var store = new DataStore();
        var audioChannels = new AudioChannelSystem(new VolumeSettings());
        audioChannels.Tick(0f, store);

        var soundEffects = new SoundEffectService(audioChannels, tags);
        var listener = new BrickSoundListener(bricks, soundEffects);
        var brickEditEvent = new EventInvoker<BrickEditEvent>([listener]);

        var activeSave = new ActiveCharacterSave
        {
            ActiveSave = new Character
            {
                Uuid = Uuid.NewUuid().ToValue(),
                Name = "Test",
            },
        };
        var saveManager = new CharacterSaveManager(
            NullLogger<CharacterSaveManager>.Instance,
            new StubCharacterStorage(),
            activeSave,
            new ProfileSettings()
        );

        IEventProcessor<PlaceEvent>[] placeProcessors = cancelPlace
            ? [new CancellingProcessor<PlaceEvent>(), new PlaceEventStatisticListener(saveManager)]
            : [new PlaceEventStatisticListener(saveManager)];
        IEventProcessor<BreakEvent>[] breakProcessors = cancelBreak
            ? [new CancellingProcessor<BreakEvent>(), new BreakEventStatisticListener(saveManager)]
            : [new BreakEventStatisticListener(saveManager)];

        var placeEvent = new EventInvoker<PlaceEvent>(placeProcessors);
        var breakEvent = new EventInvoker<BreakEvent>(breakProcessors);

        var voxelObject = new VoxelObject(chunkSize: 16);
        int structureEntity = store.Alloc(new VoxelComponent(voxelObject));
        Uuid structureUuid = store.GetUuid(structureEntity);

        var pendingQueue = new PendingInteractionQueue();
        store.Alloc(new PlayerComponent(), new PendingInteractionComponent(pendingQueue));

        var inbound = new ClientInbound();
        var snapshotAck = new SnapshotAckTracker();
        var lighting = new VoxelLightingService(bricks);
        var reconcile = new ClientVoxelReconcileSystem(inbound, snapshotAck, bricks, lighting, brickEditEvent);

        return new Fixture(
            store,
            audioChannels,
            bricks,
            saveManager,
            placeEvent,
            breakEvent,
            brickEditEvent,
            inbound,
            reconcile,
            voxelObject,
            structureEntity,
            structureUuid,
            pendingQueue
        );
    }

    private sealed class Fixture(
        DataStore store,
        AudioChannelSystem audioChannels,
        StubBrickDatabase bricks,
        CharacterSaveManager saveManager,
        EventInvoker<PlaceEvent> placeEvent,
        EventInvoker<BreakEvent> breakEvent,
        EventInvoker<BrickEditEvent> brickEditEvent,
        ClientInbound inbound,
        ClientVoxelReconcileSystem reconcile,
        VoxelObject voxelObject,
        int structureEntity,
        Uuid structureUuid,
        PendingInteractionQueue pendingQueue
    )
    {
        public readonly DataStore Store = store;
        public readonly StubBrickDatabase Bricks = bricks;
        public readonly EventInvoker<PlaceEvent> PlaceEvent = placeEvent;
        public readonly EventInvoker<BreakEvent> BreakEvent = breakEvent;
        public readonly EventInvoker<BrickEditEvent> BrickEditEvent = brickEditEvent;
        public readonly ClientInbound Inbound = inbound;
        public readonly VoxelObject VoxelObject = voxelObject;
        public readonly int StructureEntity = structureEntity;
        public readonly Uuid StructureUuid = structureUuid;
        public readonly PendingInteractionQueue PendingQueue = pendingQueue;

        public void TickReconcileWhilePlaying()
        {
            GameState previous = WaywardBeyond.Client.WaywardBeyond.GameState.Get();
            WaywardBeyond.Client.WaywardBeyond.GameState.Set(GameState.Playing);
            try
            {
                reconcile.Tick(0.016f, Store);
            }
            finally
            {
                WaywardBeyond.Client.WaywardBeyond.GameState.Set(previous);
            }
        }

        public List<string> DrainPlayedClips()
        {
            audioChannels.Tick(0f, Store);
            var clips = new List<string>();
            Store.Query<AudioSource>(0f, (float _, DataStore _, int _, in AudioSource source) => clips.Add(source.ID));
            return clips;
        }

        public long GetStatistic(string key)
        {
            Character? save = saveManager.ActiveSave;
            return save != null ? save.Value.GetStatistic(key) : 0;
        }
    }

    private sealed class CancellingProcessor<TEvent> : IEventProcessor<TEvent>
    {
        /// <inheritdoc/>
        public Result<EventBehavior> ProcessEvent(object sender, TEvent e) => Result<EventBehavior>.FromFailure("Cancelled");
    }

    private sealed class StubBrickDatabase : IBrickDatabase, IBrickRegistry
    {
        private readonly Dictionary<ushort, Brick> _byDataID = [];
        private readonly Dictionary<string, Brick> _byID = new(StringComparer.Ordinal);

        public void Add(ushort dataID, string id, Dictionary<BrickSoundEvent, string[]>? sounds)
        {
            var brick = new Brick(
                id,
                alpha: BrickAlpha.Opaque,
                passable: false,
                meshID: null,
                BrickShape.Block,
                textures: default,
                tags: [],
                sounds: sounds
            );
            brick.BindDataID(dataID);
            _byDataID[dataID] = brick;
            _byID[id] = brick;
        }

        /// <inheritdoc/>
        public Result<Brick> Get(string id)
        {
            return _byID.TryGetValue(id, out Brick? brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure((string?)null);
        }

        /// <inheritdoc/>
        public Result<Brick> Get(ushort id)
        {
            return _byDataID.TryGetValue(id, out Brick? brick)
                ? Result<Brick>.FromSuccess(brick)
                : Result<Brick>.FromFailure((string?)null);
        }

        /// <inheritdoc/>
        public List<Brick> Get(Func<Brick, bool> predicate) => [];

        /// <inheritdoc/>
        public bool IsCuller(in Voxel voxel, BrickShape shape) => voxel.ID != 0 && shape == BrickShape.Block;

        /// <inheritdoc/>
        public ushort Id(string name) => _byID.TryGetValue(name, out Brick? brick) ? brick.DataID : (ushort)0;

        /// <inheritdoc/>
        public ushort Resolve(string name) => Id(name);

        /// <inheritdoc/>
        public string? Name(ushort id) => _byDataID.TryGetValue(id, out Brick? brick) ? brick.ID : null;
    }

    private sealed class StubTagDatabase(Dictionary<string, List<string>> tags) : ILocalizedTagsDatabase
    {
        private readonly LocalizedTags _tags = new(tags);

        /// <inheritdoc/>
        public LocalizedTags? Invariant => _tags;

        /// <inheritdoc/>
        public Result<LocalizedTags> Get(string id) => Result<LocalizedTags>.FromSuccess(_tags);
    }

    private sealed class StubCharacterStorage : ICharacterStorage
    {
        /// <inheritdoc/>
        public Result<Character> GetCharacter(ulong id) => Result<Character>.FromFailure("Not implemented");

        /// <inheritdoc/>
        public IReadOnlyList<Character> GetAllCharacters() => [];

        /// <inheritdoc/>
        public Result SaveCharacter(Character character) => Result.FromSuccess();

        /// <inheritdoc/>
        public Result DeleteCharacter(ulong id) => Result.FromSuccess();
    }
}
