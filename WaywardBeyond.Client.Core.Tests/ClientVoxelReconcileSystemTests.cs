using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Networking;
using WaywardBeyond.Client.Core.Numerics;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Client.Core.Voxels;
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
    private const ulong STRUCTURE_UUID = 0xBEEF;
    private const ushort BRICK_ID = 7;

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
        var system = new ClientVoxelReconcileSystem(connection.Client, snapshotAck);

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

        var system = new ClientVoxelReconcileSystem(connection.Client, new SnapshotAckTracker());

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

        var system = new ClientVoxelReconcileSystem(connection.Client, new SnapshotAckTracker { LastAppliedSnapshotTick = 12 });

        //  The server broadcast agrees with the prediction (break -> empty).
        connection.Server.Send(new VoxelEditMessage { EntityUuid = STRUCTURE_UUID, X = 0, Y = 0, Z = 0, Voxel = new Voxel(0, 0, 0) });

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

        var system = new ClientVoxelReconcileSystem(connection.Client, new SnapshotAckTracker { LastAppliedSnapshotTick = 12 });

        connection.Server.Send(new VoxelEditMessage { EntityUuid = STRUCTURE_UUID, X = 0, Y = 0, Z = 0, Voxel = new Voxel(0, 0, 0) });

        system.Tick(0f, store);

        Assert.That(queue.TryFind(structure, new Int3(0, 0, 0), out _), Is.False, "The snapped prediction should be resolved.");
        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo((ushort)0), "The view should snap to the authoritative empty voxel.");
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
        var system = new ClientVoxelReconcileSystem(connection.Client, new SnapshotAckTracker { LastAppliedSnapshotTick = 200 });

        system.Tick(0f, store);

        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo((ushort)0), "The rejected prediction should revert to the pre-prediction (empty) voxel.");
        Assert.That(queue.TryFind(structure, new Int3(0, 0, 0), out _), Is.False, "The reverted prediction should be resolved.");
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