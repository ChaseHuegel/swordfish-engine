using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using Swordfish.Audio;
using Swordfish.ECS;
using Swordfish.IO;
using Swordfish.Library.IO;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Configuration;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Services;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Shared.Bricks;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Tests;

/// <summary>
/// 5.2 acceptance, headless and cross-platform over <see cref="LocalConnection"/>. The
/// <see cref="ClientVoxelReconcileSystem"/> applies authoritative <see cref="VoxelEditMessage"/>s onto the
/// shared client <c>VoxelObject</c> and reconciles the local player's predictions: a matching echo
/// confirms (no-op), a differing echo snaps to authority, and a prediction that ages past its bound with
/// no echo is reverted to the pre-prediction voxel. The apply is gated on <see cref="GameState.Playing"/>.
/// The mesh rebuild step is render-coupled and resolved lazily through a <c>Func</c>; headless tests pass
/// a stub.
/// </summary>
public class ClientVoxelReconcileSystemTests
{
    /// <summary>Reconcile compares by canonical name, so a small, fixture-local map suffices.</summary>
    private static readonly BrickIdRegistry _brickMap = BrickIdRegistry.FromNames(["wb:panel", "wb:rock"]);

    private const ulong STRUCTURE_UUID = 0xBEEF;
    private const ushort BRICK_ID = 7;

    private sealed class StubBrickDatabase : IBrickDatabase
    {
        private readonly Dictionary<ushort, BrickInfo> _bricks;

        public StubBrickDatabase(params BrickInfo[] bricks)
        {
            _bricks = bricks.ToDictionary(brick => brick.DataID);
        }

        public bool IsCuller(in Voxel voxel, BrickShape shape) => false;

        public Result<BrickInfo> Get(ushort id)
        {
            return _bricks.TryGetValue(id, out BrickInfo? info)
                ? Result<BrickInfo>.FromSuccess(info)
                : Result<BrickInfo>.FromFailure("Not registered.");
        }

        public List<BrickInfo> Get(Func<BrickInfo, bool> predicate) => _bricks.Values.Where(predicate).ToList();
    }

    /// <summary>
    /// Real audio plumbing with a temp VFS carrying one dummy sound per effect folder, so the sound
    /// service can actually emit plays that the channel system then allocates as entities.
    /// </summary>
    private sealed class SoundFixture : IDisposable
    {
        private readonly string _tempRoot;

        public AudioChannelSystem Channels { get; }
        public SoundEffectService Sounds { get; }
        public StubBrickDatabase Bricks { get; } = new(
            new BrickInfo("wb:rock", BRICK_ID, transparent: false, passable: true, meshID: null, BrickShape.Block, new BrickTextures(), ["environment"])
        );

        public SoundFixture()
        {
            Channels = new AudioChannelSystem(new VolumeSettings());
            _tempRoot = Path.Combine(Path.GetTempPath(), $"audio-fixture-{Guid.NewGuid():N}");
            string[] folders = ["sounds/place/metal", "sounds/remove/metal", "sounds/place/rock", "sounds/remove/rock"];
            foreach (string folder in folders)
            {
                Directory.CreateDirectory(Path.Combine(_tempRoot, "audio", folder));
                File.WriteAllText(Path.Combine(_tempRoot, "audio", folder, "dummy.wav"), "dummy");
            }

            var vfs = new VirtualFileSystem();
            Assert.That(vfs.Mount(new PathInfo(_tempRoot)).Success, Is.True);
            Sounds = new SoundEffectService(Channels, vfs);

            //  Create the channel entities so TryGetChannelEntity("effects") resolves.
            Channels.Tick(0f, new DataStore());
        }

        public int CountPlays(DataStore store)
        {
            Channels.Tick(0f, store);
            int plays = 0;
            store.Query<AudioSource>(0f, (float _, DataStore _, int _, in AudioSource _) => plays++);
            return plays;
        }

        public void Dispose()
        {
            try
            {
                Directory.Delete(_tempRoot, recursive: true);
            }
            catch (IOException)
            {
                //  Best-effort temp cleanup.
            }
        }
    }

    private static ClientVoxelReconcileSystem BuildSystem(in IClientConnection transport, SnapshotAckTracker snapshotAck, in SoundFixture sounds)
    {
        return new ClientVoxelReconcileSystem(transport, snapshotAck, _brickMap, sounds.Sounds, sounds.Bricks);
    }

    [TearDown]
    public void TearDown()
    {
        Core.WaywardBeyond.GameState.Set(Core.GameState.MainMenu);
    }

    [Test]
    public void AppliesBroadcastEditToViewWorld()
    {
        Core.WaywardBeyond.GameState.Set(Core.GameState.Playing);

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<VoxelEditMessage>() });
        DataStore store = BuildWorld(out int structure, out VoxelObject world);

        var snapshotAck = new SnapshotAckTracker();
        using SoundFixture sounds = new();
        var system = BuildSystem(connection.Client, snapshotAck, sounds);

        connection.Server.Send(new VoxelEditMessage { EntityUuid = STRUCTURE_UUID, X = 0, Y = 0, Z = 0, Voxel = new Voxel(0, 0, 0) });

        system.Tick(0f, store);

        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo((ushort)0), "The broadcast break should clear the voxel.");
        Assert.That(store.IsDirty<VoxelComponent>(structure), Is.True, "The applied edit should mark the voxel dirty.");
    }

    [Test]
    public void IgnoresEditsBeforePlaying()
    {
        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<VoxelEditMessage>() });
        DataStore store = BuildWorld(out _, out VoxelObject world);

        using SoundFixture sounds = new();
        var system = BuildSystem(connection.Client, new SnapshotAckTracker(), sounds);

        connection.Server.Send(new VoxelEditMessage { EntityUuid = STRUCTURE_UUID, X = 0, Y = 0, Z = 0, Voxel = new Voxel(0, 0, 0) });

        system.Tick(0f, store);

        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo(BRICK_ID), "The edit should not apply before play begins.");

        Core.WaywardBeyond.GameState.Set(Core.GameState.Playing);
        system.Tick(0f, store);
        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo((ushort)0), "The queued edit should apply once play begins.");
    }

    [Test]
    public void ConfirmedPredictionIsNoOpAndResolvesPending()
    {
        Core.WaywardBeyond.GameState.Set(Core.GameState.Playing);

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<VoxelEditMessage>() });
        DataStore store = BuildWorld(out int structure, out VoxelObject world);

        //  The client predicted a break: empties the cell and records the prediction.
        world.Set(0, 0, 0, new Voxel(0, 0, 0));
        var queue = new PendingInteractionQueue();
        queue.Register(structure, new Int3(0, 0, 0), new Voxel(BRICK_ID, 0, 0), new Voxel(0, 0, 0), sequence: 1, serverTickAtSample: 10);
        int player = AddPlayer(store, queue);

        using SoundFixture sounds = new();
        var system = BuildSystem(connection.Client, new SnapshotAckTracker { LastAppliedSnapshotTick = 12 }, sounds);

        //  The server broadcast agrees with the prediction (break -> empty).
        connection.Server.Send(new VoxelEditMessage { EntityUuid = STRUCTURE_UUID, X = 0, Y = 0, Z = 0, Voxel = new Voxel(0, 0, 0), Sequence = 1 });

        system.Tick(0f, store);

        Assert.That(queue.TryFind(structure, new Int3(0, 0, 0), out _), Is.False, "The confirmed prediction should be resolved.");
        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo((ushort)0), "The confirmed prediction stays as-is.");
    }

    [Test]
    public void DifferingAuthoritativeEditSnapsToAuthority()
    {
        Core.WaywardBeyond.GameState.Set(Core.GameState.Playing);

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<VoxelEditMessage>() });
        DataStore store = BuildWorld(out int structure, out VoxelObject world);

        //  The client predicted a place (brick at (0,0,0)) but the server authoritatively says empty.
        world.Set(0, 0, 0, new Voxel(BRICK_ID, 0, 0));
        var queue = new PendingInteractionQueue();
        queue.Register(structure, new Int3(0, 0, 0), new Voxel(0, 0, 0), new Voxel(BRICK_ID, 0, 0), sequence: 1, serverTickAtSample: 10);
        int player = AddPlayer(store, queue);

        using SoundFixture sounds = new();
        var system = BuildSystem(connection.Client, new SnapshotAckTracker { LastAppliedSnapshotTick = 12 }, sounds);

        connection.Server.Send(new VoxelEditMessage { EntityUuid = STRUCTURE_UUID, X = 0, Y = 0, Z = 0, Voxel = new Voxel(0, 0, 0), Sequence = 1 });

        system.Tick(0f, store);

        Assert.That(queue.TryFind(structure, new Int3(0, 0, 0), out _), Is.False, "The snapped prediction should be resolved.");
        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo((ushort)0), "The view should snap to the authoritative empty voxel.");
    }

    [Test]
    public void ConfirmsPlacementAcrossDifferingRegistryIds()
    {
        Core.WaywardBeyond.GameState.Set(Core.GameState.Playing);

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<VoxelEditMessage>() });
        DataStore store = BuildWorld(out int structure, out VoxelObject world);

        //  The client predicted a place with its own registry id for the brick.
        ushort localPanel = _brickMap.Id("wb:panel");
        var queue = new PendingInteractionQueue();
        queue.Register(structure, new Int3(0, 0, 0), new Voxel(0, 0, 0), new Voxel(localPanel, 0, 0), sequence: 1, serverTickAtSample: 10);
        int player = AddPlayer(store, queue);

        using SoundFixture sounds = new();
        var system = BuildSystem(connection.Client, new SnapshotAckTracker { LastAppliedSnapshotTick = 12 }, sounds);

        //  The server echoes the same brick but with a different numeric id; the canonical name confirms.
        connection.Server.Send(new VoxelEditMessage { EntityUuid = STRUCTURE_UUID, X = 0, Y = 0, Z = 0, Voxel = new Voxel(999, 0, 0), Sequence = 1, BrickId = "wb:panel" });

        system.Tick(0f, store);

        Assert.That(queue.TryFindBySequence(1, out _), Is.False, "The name-confirmed prediction should be resolved.");
    }

    [Test]
    public void SnapsToAuthoritativeBrickResolvedLocally()
    {
        Core.WaywardBeyond.GameState.Set(Core.GameState.Playing);

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<VoxelEditMessage>() });
        DataStore store = BuildWorld(out int structure, out VoxelObject world);

        //  The client predicted panel, but the server authoritatively says rock with a server-only id.
        ushort localPanel = _brickMap.Id("wb:panel");
        ushort localRock = _brickMap.Id("wb:rock");
        var queue = new PendingInteractionQueue();
        queue.Register(structure, new Int3(0, 0, 0), new Voxel(0, 0, 0), new Voxel(localPanel, 0, 0), sequence: 1, serverTickAtSample: 10);
        int player = AddPlayer(store, queue);

        using SoundFixture sounds = new();
        var system = BuildSystem(connection.Client, new SnapshotAckTracker { LastAppliedSnapshotTick = 12 }, sounds);

        connection.Server.Send(new VoxelEditMessage { EntityUuid = STRUCTURE_UUID, X = 0, Y = 0, Z = 0, Voxel = new Voxel(999, 0, 0), Sequence = 1, BrickId = "wb:rock" });

        system.Tick(0f, store);

        Assert.That(queue.TryFindBySequence(1, out _), Is.False, "The snapped prediction should be resolved.");
        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo(localRock), "The view should write the local id for the authoritative brick name.");
    }

    [Test]
    public void EchoResolvesPendingBySequenceAcrossCells()
    {
        Core.WaywardBeyond.GameState.Set(Core.GameState.Playing);

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<VoxelEditMessage>() });
        DataStore store = BuildWorld(out int structure, out VoxelObject world);

        //  Two distinct placements on different cells: seq 1 (break (0,0,0) -> empty) and seq 2 (break
        //  (1,0,0) -> empty). Correlating by sequence resolves each echo to the exact prediction.
        world.Set(0, 0, 0, new Voxel(0, 0, 0));
        world.Set(1, 0, 0, new Voxel(0, 0, 0));
        var queue = new PendingInteractionQueue();
        queue.Register(structure, new Int3(0, 0, 0), new Voxel(BRICK_ID, 0, 0), new Voxel(0, 0, 0), sequence: 1, serverTickAtSample: 10);
        queue.Register(structure, new Int3(1, 0, 0), new Voxel(BRICK_ID, 0, 0), new Voxel(0, 0, 0), sequence: 2, serverTickAtSample: 10);
        AddPlayer(store, queue);

        using SoundFixture sounds = new();
        var system = BuildSystem(connection.Client, new SnapshotAckTracker { LastAppliedSnapshotTick = 12 }, sounds);

        //  The echo for seq 2 arrives first, then seq 1; each confirms its own pending prediction.
        connection.Server.Send(new VoxelEditMessage { EntityUuid = STRUCTURE_UUID, X = 1, Y = 0, Z = 0, Voxel = new Voxel(0, 0, 0), Sequence = 2 });
        connection.Server.Send(new VoxelEditMessage { EntityUuid = STRUCTURE_UUID, X = 0, Y = 0, Z = 0, Voxel = new Voxel(0, 0, 0), Sequence = 1 });

        system.Tick(0f, store);

        Assert.That(queue.TryFindBySequence(1, out _), Is.False, "The sequence-1 prediction should be resolved by its echo.");
        Assert.That(queue.TryFindBySequence(2, out _), Is.False, "The sequence-2 prediction should be resolved by its echo.");
        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo((ushort)0), "The (0,0,0) break should hold.");
        Assert.That(world.Get(1, 0, 0).ID, Is.EqualTo((ushort)0), "The (1,0,0) break should hold.");
    }

    [Test]
    public void RejectedPredictionIsRevertedAfterBound()
    {
        Core.WaywardBeyond.GameState.Set(Core.GameState.Playing);

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<VoxelEditMessage>() });
        DataStore store = BuildWorld(out int structure, out VoxelObject world);

        //  The client predicted a place, but the server never echoes it (rejected). The presentation voxel
        //  already shows the brick; the untouched other cell stays empty.
        world.Set(0, 0, 0, new Voxel(BRICK_ID, 0, 0));
        var queue = new PendingInteractionQueue();
        queue.Register(structure, new Int3(0, 0, 0), new Voxel(0, 0, 0), new Voxel(BRICK_ID, 0, 0), sequence: 1, serverTickAtSample: 0);
        AddPlayer(store, queue);

        //  No authoritative edit is sent; the ack advances far past the sample tick.
        using SoundFixture sounds = new();
        var system = BuildSystem(connection.Client, new SnapshotAckTracker { LastAppliedSnapshotTick = 200 }, sounds);

        system.Tick(0f, store);

        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo((ushort)0), "The rejected prediction should revert to the pre-prediction (empty) voxel.");
        Assert.That(queue.TryFind(structure, new Int3(0, 0, 0), out _), Is.False, "The reverted prediction should be resolved.");
        Assert.That(sounds.CountPlays(store), Is.Zero, "A reverted own prediction plays no sound - the prediction already sounded.");
    }

    [Test]
    public void UnpredictedEditPlaysASound()
    {
        Core.WaywardBeyond.GameState.Set(Core.GameState.Playing);

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<VoxelEditMessage>() });
        DataStore store = BuildWorld(out int structure, out VoxelObject world);

        using SoundFixture sounds = new();
        var system = BuildSystem(connection.Client, new SnapshotAckTracker(), sounds);

        //  A remote player breaks the (0,0,0) brick: no local prediction exists.
        connection.Server.Send(new VoxelEditMessage { EntityUuid = STRUCTURE_UUID, X = 0, Y = 0, Z = 0, Voxel = new Voxel(0, 0, 0), Sequence = 1 });

        system.Tick(0f, store);

        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo((ushort)0));
        Assert.That(sounds.CountPlays(store), Is.EqualTo(1), "A remote break must be audible.");

        //  A remote player places a brick at (1,0,0): also audible.
        connection.Server.Send(new VoxelEditMessage { EntityUuid = STRUCTURE_UUID, X = 1, Y = 0, Z = 0, Voxel = new Voxel(BRICK_ID, 0, 0), Sequence = 2 });
        system.Tick(0f, store);

        Assert.That(world.Get(1, 0, 0).ID, Is.EqualTo(BRICK_ID));
        Assert.That(sounds.CountPlays(store), Is.EqualTo(2), "A remote place must be audible.");
    }

    [Test]
    public void ConfirmedOwnPredictionPlaysNoSound()
    {
        Core.WaywardBeyond.GameState.Set(Core.GameState.Playing);

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<VoxelEditMessage>() });
        DataStore store = BuildWorld(out int structure, out VoxelObject world);

        //  The local player predicted a break of (0,0,0): the presentation already shows it cleared.
        world.Set(0, 0, 0, new Voxel(0, 0, 0));
        var queue = new PendingInteractionQueue();
        queue.Register(structure, new Int3(0, 0, 0), new Voxel(BRICK_ID, 0, 0), new Voxel(0, 0, 0), sequence: 1, serverTickAtSample: 0);
        AddPlayer(store, queue);

        using SoundFixture sounds = new();
        var system = BuildSystem(connection.Client, new SnapshotAckTracker { LastAppliedSnapshotTick = 12 }, sounds);

        //  The server echoes the exact prediction.
        connection.Server.Send(new VoxelEditMessage { EntityUuid = STRUCTURE_UUID, X = 0, Y = 0, Z = 0, Voxel = new Voxel(0, 0, 0), Sequence = 1 });

        system.Tick(0f, store);

        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo((ushort)0));
        Assert.That(sounds.CountPlays(store), Is.Zero, "Confirming an own prediction echo must not double-play.");
        Assert.That(queue.TryFindBySequence(1, out _), Is.False, "The confirmed prediction should be resolved.");
    }

    private DataStore BuildWorld(out int structure, out VoxelObject world)
    {
        var store = new DataStore();
        world = new VoxelObject(chunkSize: 16);
        world.Set(0, 0, 0, new Voxel(BRICK_ID, 0, 0));
        structure = store.Alloc(Uuid.FromValue(STRUCTURE_UUID));
        store.AddOrUpdate(structure, new VoxelComponent(world, transparencyPtr: Uuid.Null));
        store.AddOrUpdate(structure, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        return store;
    }

    private int AddPlayer(DataStore store, PendingInteractionQueue? queue)
    {
        int player = store.Alloc();
        store.AddOrUpdate(player, new PlayerComponent());
        if (queue != null)
        {
            store.AddOrUpdate(player, new PendingInteractionComponent(queue));
        }
        return player;
    }
}