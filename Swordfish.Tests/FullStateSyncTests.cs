using System;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Serialization;
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

    public FullStateSyncTests()
    {
        NetworkRegistry.Register<PresenceComponent>(Uuid.FromValue(PRESENCE_UUID), NetworkDirection.ServerOwned, new PresenceCodec());
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
}