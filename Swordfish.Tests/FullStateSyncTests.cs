using System;
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
///     Late-joiner full-state sync (the multiplayer host visible as a remote player). The server's delta
///     stream only broadcasts dirty server-owned components and clears their flags at publish, so a
///     player who joined before another client connected (e.g. the host) is otherwise never sent to that
///     client. The join must hand late joiners a full-state snapshot instead.
/// </summary>
public class FullStateSyncTests
{
    private struct PresenceComponent : IDataComponent
    {
        public int Value;
    }

    private sealed class PresenceCodec : IPayloadCodec
    {
        public Type ComponentType => typeof(PresenceComponent);

        public byte[] Serialize(DataStore store, int entity)
        {
            store.TryGet(entity, out PresenceComponent value);
            return BitConverter.GetBytes(value.Value);
        }

        public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
        {
            store.AddOrUpdate(entity, new PresenceComponent { Value = BitConverter.ToInt32(payload) });
        }
    }

    //  ServerOwned marker so the full-state has replicable content. Unique uuid avoids colliding with
    //  other tests' ad hoc registrations in the shared NetworkRegistry.
    private const ulong PRESENCE_UUID = 0xE020;

    //  The real networked BodyView identity (matches its [NetworkComponent] attribute), used to prove a
    //  stale-dirty slot never publishes an empty BodyView snapshot.
    private const ulong BODY_VIEW_UUID = 10;

    public FullStateSyncTests()
    {
        NetworkRegistry.Register<PresenceComponent>(Uuid.FromValue(PRESENCE_UUID), NetworkDirection.ServerOwned, new PresenceCodec());
        NetworkRegistry.Register<BodyViewComponent>(Uuid.FromValue(BODY_VIEW_UUID), NetworkDirection.ServerOwned, new NsdComponentCodec<BodyViewComponent>());
    }

    private static INetworkSerializer[] Serializers => new INetworkSerializer[]
    {
        new NsdMessageSerializer<JoinRequest>(),
        new NsdMessageSerializer<JoinAccept>(),
        new NsdMessageSerializer<WorldStreamComplete>(),
        new NsdMessageSerializer<WorldSnapshot>(),
        new NsdMessageSerializer<LeaveGameRequest>(),
    };

    private static WorldSaveService FailingWorldService()
    {
        return new WorldSaveService(NullLogger<WorldSaveService>.Instance, () => throw new NotImplementedException());
    }

    [Fact]
    public void LateJoinerReceivesFullStateOfPreExistingPlayers()
    {
        var hub = new ServerConnectionHub();
        var sessions = new SessionManager();
        var store = new DataStore();
        var replication = new NetworkReplicationSystem(hub, sessions, NullLogger<NetworkReplicationSystem>.Instance);
        var join = new ServerJoinSystem(hub, sessions, FailingWorldService(), replication, NullLogger<ServerJoinSystem>.Instance);

        //  The host connects and joins while it is the only client, then its server-owned state is
        //  published and its dirty flag consumed.
        var hostConnection = new LocalConnection(Serializers);
        Uuid hostClientId = hub.Add(hostConnection.Server);

        hostConnection.Client.Send(new JoinRequest
        {
            CharacterId = 1,
            PublicView = new PublicView { CharacterId = 1, Name = "Host", Body = 1 },
        });
        join.Tick(0f, store);

        Assert.True(sessions.TryGetEntity(hostClientId, out int hostEntity));
        Uuid hostUuid = store.GetUuid(hostEntity);

        store.AddOrUpdate(hostEntity, new PresenceComponent { Value = 7 });
        replication.PublishStage(0f, store);
        Assert.True(hostConnection.Client.Receive<WorldSnapshot>().Success, "The host should receive its own dirty snapshot.");

        //  A late joiner connects and joins. The host's components are no longer dirty, so they can
        //  only arrive via the join-time full-state snapshot.
        var guestConnection = new LocalConnection(Serializers);
        hub.Add(guestConnection.Server);

        guestConnection.Client.Send(new JoinRequest
        {
            CharacterId = 2,
            PublicView = new PublicView { CharacterId = 2, Name = "Guest", Body = 0 },
        });
        join.Tick(0f, store);
        replication.PublishStage(0f, store);

        Assert.True(guestConnection.Client.Receive<JoinAccept>().Success);
        Assert.True(guestConnection.Client.Receive<WorldStreamComplete>().Success);

        Result<WorldSnapshot> received = guestConnection.Client.Receive<WorldSnapshot>();
        Assert.True(received.Success, "The late joiner should receive a full-state snapshot.");

        ComponentSnapshot hostState = Assert.Single(
            received.Value.Components,
            c => c.Entity == hostUuid.ToValue() && c.TypeUuid == PRESENCE_UUID
        );
        Assert.Equal(7, BitConverter.ToInt32(hostState.Payload));

        //  The full-state flag is one-shot; nothing further is sent until the delta stream finds dirt.
        Assert.False(guestConnection.Client.Receive<WorldSnapshot>().Success);
    }

    [Fact]
    public void FreedSlotReuseDoesNotPublishEmptyComponentSnapshots()
    {
        //  Freeing an entity re-arms DIRTY on its component slots (so QueryRemoved can read the last
        //  known value). A rebuilt entity reusing that slot therefore reports dirty for component kinds
        //  it no longer carries; publishing the empty payload would let clients materialize phantom
        //  state - the ghost BodyView billboard that once appeared on every voxel world entity.
        var hub = new ServerConnectionHub();
        var sessions = new SessionManager();
        var store = new DataStore();
        var replication = new NetworkReplicationSystem(hub, sessions, NullLogger<NetworkReplicationSystem>.Instance);

        var connection = new LocalConnection(Serializers);
        Uuid clientId = hub.Add(connection.Server);

        //  A player mirror that carried a BodyView.
        int mirror = store.Alloc();
        store.AddOrUpdate(mirror, new NetworkComponent());
        store.AddOrUpdate(mirror, new BodyViewComponent { Body = 3 });
        sessions.Register(store, mirror, clientId, new Session(1u));

        //  Publish once so the mirror's dirty BodyView is consumed and cleared.
        replication.PublishStage(0f, store);
        Assert.True(connection.Client.Receive<WorldSnapshot>().Success);

        //  An unload frees the mirror, leaving its slot with a stale BodyView DIRTY flag.
        store.Free(mirror);

        //  A rebuilt world entity reuses the same slot but carries no BodyView.
        int world = store.Alloc();
        Assert.Equal(mirror, world);
        store.AddOrUpdate(world, new NetworkComponent());
        store.AddOrUpdate(world, new PresenceComponent { Value = 5 });

        replication.PublishStage(0f, store);

        Result<WorldSnapshot> snapshot = connection.Client.Receive<WorldSnapshot>();
        Assert.True(snapshot.Success, "The rebuilt entity's own dirty marker should still publish.");

        Assert.All(snapshot.Value.Components, component => Assert.NotEmpty(component.Payload));
        Assert.DoesNotContain(snapshot.Value.Components, component => component.TypeUuid == BODY_VIEW_UUID);
    }
}
