using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Sessions;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// Phase 3: multi-client sessions over the <see cref="ServerConnectionHub"/>. N synthetic clients
/// (each a <see cref="LocalConnection"/> pair) connect to one server hub; spawn requests are routed back
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

    public SessionRoutingTests()
    {
        NetworkRegistry.Register<MarkerComponent>(Uuid.FromValue(MarkerUuid), NetworkDirection.ServerOwned, new MarkerCodec());
    }

    private static INetworkSerializer[] Serializers => new INetworkSerializer[]
    {
        new NsdMessageSerializer<SpawnRequest>(),
        new NsdMessageSerializer<SpawnResponse>(),
        new NsdMessageSerializer<WorldSnapshot>(),
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
    public void ConcurrentSpawnsAreRoutedToTheirOwnClient()
    {
        const int count = 3;
        Fixture fixture = new(count);
        var system = new ServerSpawnSystem(
            fixture.Hub,
            fixture.Sessions,
            new ServerWorldService(NullLogger<ServerWorldService>.Instance, () => throw new NotImplementedException()),
            NullLogger<ServerSpawnSystem>.Instance
        );

        for (var i = 0; i < count; i++)
        {
            fixture.Client(i).Send(new SpawnRequest { CharacterId = (ulong)(100 + i) });
        }

        system.Tick(0f, fixture.Store);

        //  Each client receives exactly one response and it is the entity bound to its own session.
        var assigned = new List<(Uuid clientId, ulong entity)>();
        for (var i = 0; i < count; i++)
        {
            Result<SpawnResponse> response = fixture.Client(i).Receive<SpawnResponse>();
            Assert.True(response.Success, $"Client {i} should receive a spawn response.");
            Assert.True(response.Value.Accepted);
            assigned.Add((fixture.ClientIds[i], response.Value.Entity));
        }

        //  No client received another client's response.
        for (var i = 0; i < count; i++)
        {
            Assert.False(fixture.Client(i).Receive<SpawnResponse>().Success, $"Client {i} received a stray spawn response.");
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
}