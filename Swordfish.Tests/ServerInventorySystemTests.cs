using System;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Components;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Serialization;
using WaywardBeyond.Networking.Sessions;
using WaywardBeyond.Networking.Transport;
using Xunit;

using WaywardBeyond.Config;

namespace Swordfish.Tests;

/// <summary>
/// Server-side inventory op consumption: staged ops are validated and applied to the authoritative
/// inventory, the inventory is marked dirty for the downstream echo, duplicates are deduped by sequence,
/// and a move lands before the same tick's interaction processing (the glass-brick regression: the
/// server places against the moved inventory, not its stale copy).
/// </summary>
public class ServerInventorySystemTests
{
    [Fact]
    public void StagedMoveAppliesAndMarksInventoryDirty()
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);

        var store = new DataStore();
        int entity = store.Alloc();
        var inventory = new InventoryComponent(10);
        inventory.Contents[7] = InventoryComponent.Stack("glass", 1, 10);
        store.AddOrUpdate(entity, inventory);
        store.AddOrUpdate(entity, new NetworkComponent());

        var opBuffer = new InventoryOpStageBuffer();
        opBuffer.Stage(1, new SlotMoveOp { Mode = SlotMoveOp.MODE_EXACT, FromSlot = 7, ToSlot = 0, Count = null });
        store.AddOrUpdate(entity, new NetworkComponent { StagedInventoryOps = opBuffer });

        var system = new ServerInventorySystem(NullLogger<ServerInventorySystem>.Instance);
        system.Tick(0f, store);

        Assert.True(store.TryGet(entity, out InventoryComponent updated));
        Assert.Equal("glass", updated.Contents[0].ID);
        Assert.Equal(1, updated.Contents[0].Count);
        Assert.True(updated.Contents[7].Count <= 0, "The source slot must empty on the server copy.");
        Assert.True(store.IsDirty<InventoryComponent>(entity), "The inventory must be marked dirty for the echo.");

        Assert.True(store.TryGet(entity, out NetworkComponent net));
        Assert.Null(net.StagedInventoryOps);
    }

    [Fact]
    public void ClientMoveAppliesOnServerAndFeedsSameTickConsume()
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);

        var store = new DataStore();
        int entity = store.Alloc();
        var inventory = new InventoryComponent(10);
        inventory.Contents[7] = InventoryComponent.Stack("glass", 1, 10);
        store.AddOrUpdate(entity, inventory);
        store.AddOrUpdate(entity, new NetworkComponent());
        store.AddOrUpdate(entity, new WaywardBeyond.Networking.Components.InputComponent());

        //  One client session with a mirror; the client predicts a whole-stack move 7 -> 0 and sends it
        //  as an InventoryEvent.
        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<WorldSnapshot>() });
        var hub = new ServerConnectionHub();
        Uuid clientId = hub.Add(connection.Server);
        var sessions = new SessionManager();
        sessions.Register(store, entity, clientId, new Session(1u));

        var replication = new NetworkReplicationSystem(hub, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingSettings());
        var inventorySystem = new ServerInventorySystem(NullLogger<ServerInventorySystem>.Instance);

        IPayloadCodec<InventoryEvent> inventoryCodec = new NsdComponentCodec<InventoryEvent>();
        connection.Client.Send(new WorldSnapshot
        {
            Components =
            [
                new ComponentSnapshot(
                    store.GetUuid(entity).ToValue(),
                    16,
                    inventoryCodec.Serialize(new InventoryEvent
                    {
                        Entity = store.GetUuid(entity).ToValue(),
                        SequenceNumber = 1,
                        SlotMove = new SlotMoveOp { Mode = SlotMoveOp.MODE_EXACT, FromSlot = 7, ToSlot = 0, Count = null },
                    })),
            ],
            RemovedEntities = [],
        });

        //  Server tick order: apply inbound, then consume inventory ops, then interactions.
        replication.ApplyStage(0f, store);
        inventorySystem.Tick(0f, store);

        Assert.True(store.TryGet(entity, out InventoryComponent authoritative));
        Assert.Equal("glass", authoritative.Contents[0].ID);
        Assert.True(authoritative.Contents[7].Count <= 0);
    }

    [Fact]
    public void RetransmittedOpAppliesExactlyOnce()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        var inventory = new InventoryComponent(10);
        inventory.Contents[0] = InventoryComponent.Stack("glass", 5, 10);
        store.AddOrUpdate(entity, inventory);
        store.AddOrUpdate(entity, new NetworkComponent());

        //  The same sequence staged twice (a retransmitted packet) must collapse to one op.
        var opBuffer = new InventoryOpStageBuffer();
        var op = new SlotMoveOp { Mode = SlotMoveOp.MODE_EXACT, FromSlot = 0, ToSlot = 2, Count = 2 };
        opBuffer.Stage(1, op);
        opBuffer.Stage(1, op);
        store.AddOrUpdate(entity, new NetworkComponent { StagedInventoryOps = opBuffer });

        var system = new ServerInventorySystem(NullLogger<ServerInventorySystem>.Instance);
        system.Tick(0f, store);

        Assert.True(store.TryGet(entity, out InventoryComponent updated));
        Assert.Equal(2, updated.Contents[2].Count);
        Assert.Equal(3, updated.Contents[0].Count);
    }
}