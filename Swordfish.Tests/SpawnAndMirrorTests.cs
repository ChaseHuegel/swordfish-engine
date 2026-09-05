using System;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Shared.Networking;
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

    [Fact]
    public void ServerMaterializesMirrorForUnknownClientOwnedEntity()
    {
        NetworkRegistry.Register<MirrorComponent>(Uuid.FromValue(0xE001), NetworkDirection.ClientOwned, new MirrorCodec());

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<WorldSnapshot>() });
        var serverStore = new DataStore();
        var system = new WaywardBeyond.Server.Core.Systems.NetworkReplicationSystem(
            connection.Server,
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
        var connection = new LocalConnection(new INetworkSerializer[]
        {
            new NsdMessageSerializer<SpawnRequest>(),
            new NsdMessageSerializer<SpawnResponse>(),
        });
        var serverStore = new DataStore();
        var system = new WaywardBeyond.Server.Core.Systems.ServerSpawnSystem(
            connection.Server,
            NullLogger<WaywardBeyond.Server.Core.Systems.ServerSpawnSystem>.Instance
        );

        var request = new SpawnRequest
        {
            CharacterId = 99,
            PositionX = 1f,
            PositionY = 2f,
            PositionZ = 3f,
            OrientationX = 0f,
            OrientationY = 0f,
            OrientationZ = 0f,
            OrientationW = 1f,
        };
        connection.Client.Send(request);

        system.Tick(0f, serverStore);

        Result<SpawnResponse> response = connection.Client.Receive<SpawnResponse>();
        Assert.True(response.Success);
        Assert.True(response.Value.Accepted);

        Assert.True(serverStore.TryGet(Uuid.FromValue(response.Value.Entity), out int entity));
        Assert.True(serverStore.TryGet(entity, out WaywardBeyond.Shared.Networking.Components.NetworkComponent net));
    }
}