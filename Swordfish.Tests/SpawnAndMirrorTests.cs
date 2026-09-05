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

    private sealed class NoopCodec<T> : IPayloadCodec where T : struct, IDataComponent
    {
        public Type ComponentType => typeof(T);
        public byte[] Serialize(DataStore store, int entity) => [];
        public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload) { }
    }

    [Fact]
    public void ServerMaterializesMirrorForUnknownClientOwnedEntity()
    {
        NetworkRegistry.Register<MirrorComponent>(Uuid.FromValue(0xE001), NetworkDirection.ClientOwned, new MirrorCodec());

        var connection = new LocalConnection(new object[] { new NsdMessageSerializer<WorldSnapshot>() });
        var serverStore = new DataStore();
        var system = new WaywardBeyond.Server.Core.Systems.NetworkReplicationSystem(
            connection.Server,
            new ServerPlayerOwnership(),
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

        system.Tick(0f, serverStore);

        Assert.True(serverStore.TryGet(entityUuid, out int entity));
        Assert.True(serverStore.TryGet(entity, out MirrorComponent mirror));
        Assert.Equal(42, mirror.Value);

        // Mirrors carry no NetworkComponent, so nothing is replicated downstream.
        Assert.False(connection.Client.Receive<WorldSnapshot>().Success);
    }

    [Fact]
    public void ServerSpawnRepliesWithAuthoritativeUuid()
    {
        var connection = new LocalConnection(new object[]
        {
            new NsdMessageSerializer<SpawnRequest>(),
            new NsdMessageSerializer<SpawnResponse>(),
        });
        var serverStore = new DataStore();
        var system = new WaywardBeyond.Server.Core.Systems.ServerSpawnSystem(
            connection.Server,
            new ServerPlayerOwnership(),
            NullLogger<WaywardBeyond.Server.Core.Systems.ServerSpawnSystem>.Instance
        );

        var request = new SpawnRequest
        {
            CharacterId = 99,
        };
        connection.Client.Send(request);

        system.Tick(0f, serverStore);

        Result<SpawnResponse> response = connection.Client.Receive<SpawnResponse>();
        Assert.True(response.Success);
        Assert.True(response.Value.Accepted);

        Assert.True(serverStore.TryGet(Uuid.FromValue(response.Value.Entity), out int entity));
        Assert.True(serverStore.TryGet(entity, out WaywardBeyond.Shared.Networking.Components.NetworkComponent net));
    }

    [Fact]
    public void ServerAcceptsInitialTransformPlacementButDoesNotEchoToOwner()
    {
        NetworkRegistry.Register<TransformComponent>(Uuid.FromValue(2), NetworkDirection.ServerOwned, new NoopCodec<TransformComponent>());

        var ownership = new ServerPlayerOwnership();
        var connection = new LocalConnection(new object[] { new NsdMessageSerializer<WorldSnapshot>() });
        var serverStore = new DataStore();

        int player = serverStore.Alloc();
        Uuid playerUuid = serverStore.GetUuid(player);
        serverStore.AddOrUpdate(player, new NetworkComponent());
        ownership.SetOwnedPlayer(playerUuid);

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
            connection.Server,
            ownership,
            NullLogger<WaywardBeyond.Server.Core.Systems.NetworkReplicationSystem>.Instance
        );
        system.Tick(0f, serverStore);

        Assert.True(serverStore.TryGet(playerUuid, out int placedEntity));
        Assert.True(serverStore.TryGet(placedEntity, out TransformComponent transform));
        Assert.Equal(new Vector3(1f, 2f, 3f), transform.Position);

        // The transform is not echoed back to the owning client.
        Assert.False(connection.Client.Receive<WorldSnapshot>().Success);
    }
}