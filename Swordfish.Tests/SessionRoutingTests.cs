using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Sessions;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Phase 3: multi-client sessions over the <see cref="ServerConnectionHub"/>. N synthetic clients
/// (each a <see cref="LocalConnection"/> pair) connect to one server hub; join requests are routed back
/// to the requesting client, snapshots carry a per-client ack, and a disconnect frees the session's
/// entity and replicates its despawn to the remaining clients.
/// </summary>
public class SessionRoutingTests
{
    private struct MarkerComponent : IDataComponent
    {
        public int Value;
    }

    private sealed class MarkerCodec : IPayloadCodec
    {
        public Type ComponentType => typeof(MarkerComponent);

        public byte[] Serialize(DataStore store, int entity)
        {
            store.TryGet(entity, out MarkerComponent value);
            return BitConverter.GetBytes(value.Value);
        }

        public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
        {
            store.AddOrUpdate(entity, new MarkerComponent { Value = BitConverter.ToInt32(payload) });
        }
    }

    //  ServerOwned marker so PublishStage has something to send. Unique uuid avoids colliding with other
    //  tests' ad hoc registrations in the shared NetworkRegistry.
    private const ulong MarkerUuid = 0xE010;

    //  ClientOwned component whose codec rejects the payload, standing in for a malformed inbound frame.
    private const ulong ThrowingUuid = 0xEF11;

    private struct RejectingComponent : IDataComponent
    {
        public int Value;
    }

    private sealed class RejectingCodec : IPayloadCodec
    {
        public Type ComponentType => typeof(RejectingComponent);

        public byte[] Serialize(DataStore store, int entity) => [];

        public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
        {
            throw new InvalidOperationException("Malformed payload.");
        }
    }

    public SessionRoutingTests()
    {
        NetworkRegistry.Register<MarkerComponent>(Uuid.FromValue(MarkerUuid), NetworkDirection.ServerOwned, new MarkerCodec());
        NetworkRegistry.Register<RejectingComponent>(Uuid.FromValue(ThrowingUuid), NetworkDirection.ClientOwned, new RejectingCodec());
    }

    private static INetworkSerializer[] Serializers => new INetworkSerializer[]
    {
        new NsdMessageSerializer<JoinRequest>(),
        new NsdMessageSerializer<JoinAccept>(),
        new NsdMessageSerializer<WorldStreamComplete>(),
        new NsdMessageSerializer<WorldSnapshot>(),
        new NsdMessageSerializer<LeaveGameRequest>(),
    };

    private sealed class Fixture
    {
        public ServerConnectionHub Hub { get; } = new();
        public SessionManager Sessions { get; } = new();
        public DataStore Store { get; } = new();
        public List<LocalConnection> Connections { get; } = [];
        public List<Uuid> ClientIds { get; } = [];

        public Fixture(int clientCount)
        {
            for (var i = 0; i < clientCount; i++)
            {
                var connection = new LocalConnection(Serializers);
                Connections.Add(connection);
                ClientIds.Add(Hub.Add(connection.Server));
            }
        }

        public IClientConnection Client(int i) => Connections[i].Client;
    }

    [Fact]
    public void ConcurrentJoinsAreRoutedToTheirOwnClient()
    {
        const int count = 3;
        Fixture fixture = new(count);
        var system = new ServerJoinSystem(
            fixture.Hub,
            fixture.Sessions,
            new WorldSaveService(NullLogger<WorldSaveService>.Instance, () => throw new NotImplementedException(), TestBricks.Map),
            new NetworkReplicationSystem(fixture.Hub, fixture.Sessions, NullLogger<NetworkReplicationSystem>.Instance),
            TestInteractionSystem.Create(fixture.Hub),
            NullLogger<ServerJoinSystem>.Instance,
            TestBricks.Map
        );

        for (var i = 0; i < count; i++)
        {
            ulong characterId = (ulong)(100 + i);
            fixture.Client(i).Send(new JoinRequest
            {
                CharacterId = characterId,
                PublicView = new PublicView { CharacterId = characterId, Name = "P", Body = "wb:m_human" },
            });
        }

        system.Tick(0f, fixture.Store);

        //  Each client receives exactly one join accept (its own player entity) and one stream complete.
        var assigned = new List<(Uuid clientId, ulong entity)>();
        for (var i = 0; i < count; i++)
        {
            Result<JoinAccept> accept = fixture.Client(i).Receive<JoinAccept>();
            Assert.True(accept.Success, $"Client {i} should receive a join accept.");
            Assert.NotEqual((ulong)0, accept.Value.PlayerEntity);
            assigned.Add((fixture.ClientIds[i], accept.Value.PlayerEntity));

            Assert.True(fixture.Client(i).Receive<WorldStreamComplete>().Success, $"Client {i} should receive a world stream complete.");
        }

        //  No client received another client's join accept.
        for (var i = 0; i < count; i++)
        {
            Assert.False(fixture.Client(i).Receive<JoinAccept>().Success, $"Client {i} received a stray join accept.");
        }

        //  Distinct entities, each bound back to the client that requested it and carrying a session.
        Assert.Equal(count, assigned.Select(x => x.entity).Distinct().Count());
        foreach ((Uuid clientId, ulong entity) in assigned)
        {
            Assert.True(fixture.Sessions.TryGetEntity(clientId, out int playerEntity));
            Assert.Equal(entity, fixture.Store.GetUuid(playerEntity).ToValue());
            Assert.True(fixture.Store.TryGet(playerEntity, out WaywardBeyond.Shared.Networking.Components.NetworkComponent net));
            Assert.True(fixture.Sessions.TryGetSession(clientId, out Session session));
            Assert.Equal(session, net.Session);
        }
    }

    [Fact]
    public void EachClientReceivesItsOwnProcessedInputAck()
    {
        const int count = 3;
        Fixture fixture = new(count);

        //  Spawn one player entity per client, each with a distinct acked input, and dirty a server-owned
        //  component so the publish stage has content to broadcast.
        var acks = new uint[count];
        for (var i = 0; i < count; i++)
        {
            acks[i] = (uint)(10 + i * 100);
            int entity = fixture.Store.Alloc();
            fixture.Store.AddOrUpdate(entity, new WaywardBeyond.Shared.Networking.Components.NetworkComponent { LastAckedInput = acks[i] });
            fixture.Store.AddOrUpdate(entity, new MarkerComponent { Value = i });
            fixture.Sessions.Register(fixture.Store, entity, fixture.ClientIds[i], new Session((uint)i));
        }

        var replication = new NetworkReplicationSystem(
            fixture.Hub,
            fixture.Sessions,
            NullLogger<NetworkReplicationSystem>.Instance
        );
        replication.SimTick = 42;
        replication.PublishStage(0f, fixture.Store);

        //  Each client's snapshot reports its own ack, not a shared server-wide value.
        for (var i = 0; i < count; i++)
        {
            Result<WorldSnapshot> received = fixture.Client(i).Receive<WorldSnapshot>();
            Assert.True(received.Success, $"Client {i} should receive a snapshot.");
            Assert.Equal(42u, received.Value.TickNumber);
            Assert.Equal(acks[i], received.Value.LastProcessedInput);
            Assert.NotEmpty(received.Value.Components);
        }
    }

    [Fact]
    public void DisconnectedClientEntityDespawnReplicatesToRemainingClients()
    {
        const int count = 3;
        Fixture fixture = new(count);

        var entities = new int[count];
        for (var i = 0; i < count; i++)
        {
            entities[i] = fixture.Store.Alloc();
            fixture.Store.AddOrUpdate(entities[i], new WaywardBeyond.Shared.Networking.Components.NetworkComponent());
            fixture.Sessions.Register(fixture.Store, entities[i], fixture.ClientIds[i], new Session((uint)i));
        }

        Uuid dropped = fixture.ClientIds[0];
        int droppedEntity = entities[0];

        var replication = new NetworkReplicationSystem(
            fixture.Hub,
            fixture.Sessions,
            NullLogger<NetworkReplicationSystem>.Instance
        );

        //  Client drops: the server ends its session, disposes the body, captures the uuid, and frees the
        //  mirror (despawn uuid is captured before Free clears it).
        Assert.True(fixture.Hub.Remove(dropped));
        fixture.Sessions.EndSession(dropped);
        if (fixture.Store.TryGet(droppedEntity, out Swordfish.ECS.PhysicsComponent physics))
        {
            physics.Dispose();
        }

        Uuid droppedUuid = fixture.Store.GetUuid(droppedEntity);
        replication.RequestDespawn(droppedUuid.ToValue());
        fixture.Store.Free(droppedEntity);

        Assert.False(fixture.Sessions.TryGetEntity(dropped, out _));

        replication.PublishStage(0f, fixture.Store);

        //  Remaining clients observe the despawn; the dropped client is no longer served.
        Assert.False(fixture.Client(0).Receive<WorldSnapshot>().Success, "Dropped client should receive nothing.");
        for (var i = 1; i < count; i++)
        {
            Result<WorldSnapshot> received = fixture.Client(i).Receive<WorldSnapshot>();
            Assert.True(received.Success, $"Client {i} should receive a snapshot.");
            Assert.Contains(droppedUuid.ToValue(), received.Value.RemovedEntities);
        }
    }

    [Fact]
    public void SnapshotAddressedAtAnotherClientsEntityIsIgnored()
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);

        const int count = 2;
        Fixture fixture = new(count);

        var entities = new int[count];
        for (var i = 0; i < count; i++)
        {
            entities[i] = fixture.Store.Alloc();
            fixture.Store.AddOrUpdate(entities[i], new WaywardBeyond.Shared.Networking.Components.NetworkComponent());
            fixture.Sessions.Register(fixture.Store, entities[i], fixture.ClientIds[i], new Session((uint)i));
        }

        var replication = new NetworkReplicationSystem(
            fixture.Hub,
            fixture.Sessions,
            NullLogger<NetworkReplicationSystem>.Instance
        );

        Uuid victimUuid = fixture.Store.GetUuid(entities[0]);
        Uuid attackerUuid = fixture.Store.GetUuid(entities[1]);

        //  Client B addresses an input snapshot at client A's entity uuid.
        fixture.Client(1).Send(new WorldSnapshot
        {
            TickNumber = 1,
            LastProcessedInput = 0,
            Components = [new ComponentSnapshot(victimUuid.ToValue(), 1, SerializeInput(9u))],
            RemovedEntities = [],
        });

        replication.ApplyStage(0f, fixture.Store);

        //  The victim's mirror staged nothing: no input, no interactions, untouched ack state.
        Assert.True(fixture.Store.TryGet(entities[0], out WaywardBeyond.Shared.Networking.Components.NetworkComponent victim));
        Assert.Null(victim.StagedInputs);
        Assert.Null(victim.StagedInteractions);
        Assert.Equal(0u, victim.LastAckedInput);

        //  The same client writing to its own session entity still stages on its own mirror.
        fixture.Client(1).Send(new WorldSnapshot
        {
            TickNumber = 2,
            LastProcessedInput = 0,
            Components = [new ComponentSnapshot(attackerUuid.ToValue(), 1, SerializeInput(9u))],
            RemovedEntities = [],
        });

        replication.ApplyStage(0f, fixture.Store);

        Assert.True(fixture.Store.TryGet(entities[1], out WaywardBeyond.Shared.Networking.Components.NetworkComponent attacker));
        Assert.NotNull(attacker.StagedInputs);
        Assert.Equal(9u, attacker.LastAckedInput);
    }

    private static byte[] SerializeInput(uint serverTickAtSample)
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new InputComponent
        {
            MovementX = 1f,
            MovementY = 2f,
            MovementZ = 3f,
            SequenceNumber = 1,
            ServerTickAtSample = serverTickAtSample,
        });

        NetworkRegistry.TryGetInfo<InputComponent>(out NetworkComponentInfo info);
        return info.Codec.Serialize(store, entity);
    }

    [Fact]
    public void MalformedSnapshotFromOneClientDoesNotAffectOthers()
    {
        NetworkRegistry.Initialize([typeof(InputComponent).Assembly]);

        const int count = 2;
        Fixture fixture = new(count);

        var entities = new int[count];
        for (var i = 0; i < count; i++)
        {
            entities[i] = fixture.Store.Alloc();
            fixture.Store.AddOrUpdate(entities[i], new WaywardBeyond.Shared.Networking.Components.NetworkComponent());
            fixture.Sessions.Register(fixture.Store, entities[i], fixture.ClientIds[i], new Session((uint)i));
        }

        var replication = new NetworkReplicationSystem(
            fixture.Hub,
            fixture.Sessions,
            NullLogger<NetworkReplicationSystem>.Instance
        );

        Uuid attackerUuid = fixture.Store.GetUuid(entities[1]);

        //  Client B leads with a payload whose codec throws, then a valid input of its own. Client A
        //  sends a plain valid input in the same tick.
        fixture.Client(1).Send(new WorldSnapshot
        {
            Components =
            [
                new ComponentSnapshot(attackerUuid.ToValue(), ThrowingUuid, [0xDE, 0xAD]),
                new ComponentSnapshot(attackerUuid.ToValue(), 1, SerializeInput(3u)),
            ],
            RemovedEntities = [],
        });
        fixture.Client(0).Send(new WorldSnapshot
        {
            Components = [new ComponentSnapshot(fixture.Store.GetUuid(entities[0]).ToValue(), 1, SerializeInput(7u))],
            RemovedEntities = [],
        });

        //  The stage must not throw: the malformed client is skipped, its remaining components dropped,
        //  and the healthy client's input still stages.
        replication.ApplyStage(0f, fixture.Store);

        Assert.True(fixture.Store.TryGet(entities[0], out WaywardBeyond.Shared.Networking.Components.NetworkComponent victim));
        Assert.NotNull(victim.StagedInputs);
        Assert.Equal(7u, victim.LastAckedInput);

        Assert.True(fixture.Store.TryGet(entities[1], out WaywardBeyond.Shared.Networking.Components.NetworkComponent attacker));
        Assert.Null(attacker.StagedInputs);
    }

    [Fact]
    public void LeavingGameEndsSessionAndFreesMirror()
    {
        Fixture fixture = new(1);
        var system = new ServerJoinSystem(
            fixture.Hub,
            fixture.Sessions,
            new WorldSaveService(NullLogger<WorldSaveService>.Instance, () => throw new NotImplementedException(), TestBricks.Map),
            new NetworkReplicationSystem(fixture.Hub, fixture.Sessions, NullLogger<NetworkReplicationSystem>.Instance),
            TestInteractionSystem.Create(fixture.Hub),
            NullLogger<ServerJoinSystem>.Instance,
            TestBricks.Map
        );

        int entity = fixture.Store.Alloc();
        Uuid entityUuid = fixture.Store.GetUuid(entity);
        fixture.Store.AddOrUpdate(entity, new WaywardBeyond.Shared.Networking.Components.NetworkComponent());
        fixture.Sessions.Register(fixture.Store, entity, fixture.ClientIds[0], new Session(1u));

        fixture.Client(0).Send(new LeaveGameRequest { Dummy = 0 });
        system.Tick(0f, fixture.Store);

        Assert.False(fixture.Sessions.TryGetEntity(fixture.ClientIds[0], out _));
        Assert.False(fixture.Store.TryGet(entityUuid, out _));
    }
}