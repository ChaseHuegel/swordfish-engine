using System;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Components;
using WaywardBeyond.Server.Core.Saves;
using WaywardBeyond.Shared.Data;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

using WaywardBeyond.Shared.Config;

namespace Swordfish.Tests;

public class SpawnAndMirrorTests
{
    private struct MirrorComponent : IDataComponent
    {
        public int Value;
    }

    private sealed class MirrorCodec : IPayloadCodec
    {
        public Type ComponentType => typeof(MirrorComponent);

        public byte[] Serialize(DataStore store, int entity)
        {
            store.TryGet(entity, out MirrorComponent value);
            return BitConverter.GetBytes(value.Value);
        }

        public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
        {
            store.AddOrUpdate(entity, new MirrorComponent { Value = BitConverter.ToInt32(payload) });
        }
    }

    private sealed class PlaceTransformCodec : IPayloadCodec
    {
        public Type ComponentType => typeof(TransformComponent);

        public byte[] Serialize(DataStore store, int entity) => Array.Empty<byte>();

        public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
        {
            store.AddOrUpdate(entity, new TransformComponent(new Vector3(1f, 2f, 3f)));
        }
    }

    [Fact]
    public void ServerIgnoresClientOwnedSnapshotOutsideItsSessionEntity()
    {
        NetworkRegistry.Register<MirrorComponent>(Uuid.FromValue(0xE001), NetworkDirection.ClientOwned, new MirrorCodec());

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<WorldSnapshot>() });
        var hub = new ServerConnectionHub();
        Uuid clientId = hub.Add(connection.Server);
        var serverStore = new DataStore();
        var sessions = new SessionManager();

        //  The client's session owns a spawned player entity.
        int sessionEntity = serverStore.Alloc();
        Uuid sessionUuid = serverStore.GetUuid(sessionEntity);
        sessions.Register(serverStore, sessionEntity, clientId, new WaywardBeyond.Shared.Networking.Sessions.Session(1u));

        var system = new WaywardBeyond.Server.Core.Systems.NetworkReplicationSystem(
            hub,
            sessions,
            NullLogger<WaywardBeyond.Server.Core.Systems.NetworkReplicationSystem>.Instance, new NetworkingSettings()
        );

        //  A snapshot addressed at an unknown (never allocated) uuid must not materialize an entity.
        var forgedUuid = Uuid.FromValue(0xABC);
        var worldSnapshot = new WorldSnapshot
        {
            TickNumber = 1,
            LastProcessedInput = 0,
            Components = [new ComponentSnapshot(forgedUuid.ToValue(), 0xE001, BitConverter.GetBytes(42))],
            RemovedEntities = [],
        };
        connection.Client.Send(worldSnapshot);

        system.ApplyStage(0f, serverStore);

        Assert.False(serverStore.TryGet(forgedUuid, out _), "A forged uuid must never allocate an entity.");
        Assert.False(serverStore.TryGet(sessionEntity, out MirrorComponent _), "A misaddressed payload must not land on the session entity.");

        //  A snapshot addressed at the sender's own session entity still applies.
        var ownSnapshot = new WorldSnapshot
        {
            Components = [new ComponentSnapshot(sessionUuid.ToValue(), 0xE001, BitConverter.GetBytes(7))],
            RemovedEntities = [],
        };
        connection.Client.Send(ownSnapshot);

        system.ApplyStage(0f, serverStore);

        Assert.True(serverStore.TryGet(sessionEntity, out MirrorComponent mirror));
        Assert.Equal(7, mirror.Value);
    }

    [Fact]
    public void ServerJoinRepliesWithAuthoritativeUuidAndBody()
    {
        var connection = new LocalConnection(new INetworkSerializer[]
        {
            new NsdMessageSerializer<JoinRequest>(),
            new NsdMessageSerializer<JoinAccept>(),
            new NsdMessageSerializer<LevelStreamComplete>(),
        });
        var hub = new ServerConnectionHub();
        hub.Add(connection.Server);
        var serverStore = new DataStore();
        var sessions = new SessionManager();
        var system = new WaywardBeyond.Server.Core.Systems.ServerJoinSystem(
            hub,
            sessions,
            new WaywardBeyond.Server.Core.Saves.LevelSaveService(
                NullLogger<WaywardBeyond.Server.Core.Saves.LevelSaveService>.Instance,
                new StubLevelCatalog(),
                TestBricks.Map
            ),
            new WaywardBeyond.Server.Core.Systems.NetworkReplicationSystem(
                hub,
                sessions,
                NullLogger<WaywardBeyond.Server.Core.Systems.NetworkReplicationSystem>.Instance, new NetworkingSettings()
            ),
            TestInteractionSystem.Create(hub),
            NullLogger<WaywardBeyond.Server.Core.Systems.ServerJoinSystem>.Instance,
            TestBricks.Map
        );

        connection.Client.Send(new JoinRequest
        {
            CharacterId = 99,
            PublicView = new PublicView { CharacterId = 99, Name = "P", Body = "wb:m_human" },
        });

        system.Tick(0f, serverStore);

        Result<JoinAccept> accept = connection.Client.Receive<JoinAccept>();
        Assert.True(accept.Success);
        Assert.NotEqual((ulong)0, accept.Value.PlayerEntity);
        Assert.True(connection.Client.Receive<LevelStreamComplete>().Success);

        Assert.True(serverStore.TryGet(Uuid.FromValue(accept.Value.PlayerEntity), out int entity));
        Assert.True(serverStore.TryGet(entity, out NetworkComponent net));
        Assert.True(serverStore.TryGet<TransformComponent>(entity, out _));
        Assert.True(serverStore.TryGet<PhysicsComponent>(entity, out _));
        Assert.True(serverStore.TryGet<ColliderComponent>(entity, out _));
        Assert.True(serverStore.TryGet(entity, out OwnedCharacterComponent owned));
        Assert.Equal((ulong)99, owned.CharacterId);
    }

    [Fact]
    public void ServerIgnoresClientAuthoredServerOwnedState()
    {
        NetworkRegistry.Register<TransformComponent>(Uuid.FromValue(2), NetworkDirection.ServerOwned, new PlaceTransformCodec());

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<WorldSnapshot>() });
        var hub = new ServerConnectionHub();
        hub.Add(connection.Server);
        var serverStore = new DataStore();

        int player = serverStore.Alloc();
        Uuid playerUuid = serverStore.GetUuid(player);
        serverStore.AddOrUpdate(player, new NetworkComponent());
        serverStore.AddOrUpdate(player, new TransformComponent(new Vector3(9f, 9f, 9f)));

        var placement = new WorldSnapshot
        {
            Components =
            [
                new ComponentSnapshot(playerUuid.ToValue(), 2, new TransformMessage(1f, 2f, 3f, 0f, 0f, 0f, 1f, 1f, 1f, 1f).Serialize()),
            ],
            RemovedEntities = [],
        };
        connection.Client.Send(placement);

        var system = new WaywardBeyond.Server.Core.Systems.NetworkReplicationSystem(
            hub,
            new SessionManager(),
            NullLogger<WaywardBeyond.Server.Core.Systems.NetworkReplicationSystem>.Instance, new NetworkingSettings()
        );
        system.ApplyStage(0f, serverStore);

        Assert.True(serverStore.TryGet(player, out TransformComponent transform));
        Assert.Equal(new Vector3(9f, 9f, 9f), transform.Position);
    }
}