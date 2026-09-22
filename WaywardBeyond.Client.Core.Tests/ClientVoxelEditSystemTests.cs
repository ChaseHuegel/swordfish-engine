using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Client.Core.Components;
using WaywardBeyond.Client.Core.Systems;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Client.Core.Tests;

/// <summary>
/// 4.3 acceptance, headless and cross-platform: an authoritative <see cref="VoxelEditMessage"/> broadcast
/// by the server is received by the client's <see cref="ClientVoxelEditSystem"/> and applied onto the
/// same shared <c>VoxelObject</c> (the apply path the server authored with), so a remote witness and the
/// origin client both converge without client-side authority. The mesh rebuild step is render-coupled and
/// resolved lazily on the ECS thread through a <c>Func</c>; the headless test passes a stub. The apply is
/// gated on <see cref="GameState.Playing"/>, matching the reconcile gate.
/// </summary>
public class ClientVoxelEditSystemTests
{
    [TearDown]
    public void TearDown()
    {
        //  Reset the shared game-state binding so later tests don't observe Playing.
        Core.WaywardBeyond.GameState.Set(Core.GameState.MainMenu);
    }

    [Test]
    public void AppliesBroadcastEditToViewWorld()
    {
        Core.WaywardBeyond.GameState.Set(Core.GameState.Playing);

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<VoxelEditMessage>() });

        var store = new DataStore();
        var world = new VoxelObject(chunkSize: 16);
        world.Set(0, 0, 0, new Voxel(1, 0, 0));
        int entity = store.Alloc(Uuid.FromValue(0xBEEF));
        store.AddOrUpdate(entity, new VoxelComponent(world, transparencyPtr: Uuid.Null));

        var system = new ClientVoxelEditSystem(connection.Client, () => null!);

        connection.Server.Send(new VoxelEditMessage
        {
            EntityUuid = 0xBEEF,
            X = 0,
            Y = 0,
            Z = 0,
            Voxel = new Voxel(_ID: 0, _ShapeLight: 0, _Orientation: 0),
        });

        system.Tick(0f, store);

        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo((ushort)0), "The broadcast break should clear the voxel.");
        Assert.That(store.IsDirty<VoxelComponent>(entity), Is.True, "The applied edit should mark the voxel dirty.");
    }

    [Test]
    public void IgnoresEditsBeforePlaying()
    {
        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<VoxelEditMessage>() });

        var store = new DataStore();
        var world = new VoxelObject(chunkSize: 16);
        world.Set(0, 0, 0, new Voxel(1, 0, 0));
        int entity = store.Alloc(Uuid.FromValue(0xBEEF));
        store.AddOrUpdate(entity, new VoxelComponent(world, transparencyPtr: Uuid.Null));

        var system = new ClientVoxelEditSystem(connection.Client, () => null!);

        connection.Server.Send(new VoxelEditMessage { EntityUuid = 0xBEEF, X = 0, Y = 0, Z = 0, Voxel = new Voxel() });

        //  GameState is MainMenu here; the envelope stays queued and nothing is applied.
        system.Tick(0f, store);

        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo((ushort)1), "The edit should not apply before play begins.");

        //  Once Playing, the same queued edit applies (the gate is re-checked inside the drain).
        Core.WaywardBeyond.GameState.Set(Core.GameState.Playing);
        system.Tick(0f, store);
        Assert.That(world.Get(0, 0, 0).ID, Is.EqualTo((ushort)0), "The queued edit should apply once play begins.");
    }
}