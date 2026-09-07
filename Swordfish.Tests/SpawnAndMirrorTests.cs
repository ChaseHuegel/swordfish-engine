using System;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

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
    public void ServerMaterializesUnknownClientOwnedEntity()
    {
        NetworkRegistry.Register<MirrorComponent>(Uuid.FromValue(0xE001), NetworkDirection.ClientOwned, new MirrorCodec());

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<WorldSnapshot>() });
        var hub = new ServerConnectionHub();
        hub.Add(connection.Server);
        var serverStore = new DataStore();
        var system = new WaywardBeyond.Server.Core.Systems.NetworkReplicationSystem(
            hub,
            new SessionManager(),
            NullLogger<WaywardBeyond.Server.Core.Systems.NetworkReplicationSystem>.Instance
        );

        var entityUuid = Uuid.FromValue(0xABC);
        var worldSnapshot = new WorldSnapshot
        {
            TickNumber = 1,
            LastProcessedInput = 0,
            Components = [new ComponentSnapshot(entityUuid.ToValue(), 0xE001, BitConverter.GetBytes(42))],
            RemovedEntities = [],
        };
        connection.Client.Send(worldSnapshot);

        system.ApplyStage(0f, serverStore);

        Assert.True(serverStore.TryGet(entityUuid, out int entity));
        Assert.True(serverStore.TryGet(entity, out MirrorComponent mirror));
        Assert.Equal(42, mirror.Value);
    }

    [Fact]
    public void ServerSpawnRepliesWithAuthoritativeUuidAndBody()
    {
        var connection = new LocalConnection(new INetworkSerializer[]
        {
            new NsdMessageSerializer<SpawnRequest>(),
            new NsdMessageSerializer<SpawnResponse>(),
        });
        var hub = new ServerConnectionHub();
        hub.Add(connection.Server);
        var serverStore = new DataStore();
        var system = new WaywardBeyond.Server.Core.Systems.ServerSpawnSystem(
            hub,
            new SessionManager(),
            new WaywardBeyond.Server.Core.Saves.WorldSaveService(
                NullLogger<WaywardBeyond.Server.Core.Saves.WorldSaveService>.Instance,
                () => throw new NotImplementedException()
            ),
            NullLogger<WaywardBeyond.Server.Core.Systems.ServerSpawnSystem>.Instance
        );

        var request = new SpawnRequest { CharacterId = 99 };
        connection.Client.Send(request);

        system.Tick(0f, serverStore);

        Result<SpawnResponse> response = connection.Client.Receive<SpawnResponse>();
        Assert.True(response.Success);
        Assert.True(response.Value.Accepted);

        Assert.True(serverStore.TryGet(Uuid.FromValue(response.Value.Entity), out int entity));
        Assert.True(serverStore.TryGet(entity, out NetworkComponent net));
        Assert.True(serverStore.TryGet<TransformComponent>(entity, out _));
        Assert.True(serverStore.TryGet<PhysicsComponent>(entity, out _));
        Assert.True(serverStore.TryGet<ColliderComponent>(entity, out _));
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
            NullLogger<WaywardBeyond.Server.Core.Systems.NetworkReplicationSystem>.Instance
        );
        system.ApplyStage(0f, serverStore);

        Assert.True(serverStore.TryGet(player, out TransformComponent transform));
        Assert.Equal(new Vector3(9f, 9f, 9f), transform.Position);
    }
}