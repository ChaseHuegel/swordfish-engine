using System;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.Library.Util;
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
/// Component removal replicates as a ComponentRemoval on the live entity: the publish rule emits an
/// update for dirty-and-exists, a removal for dirty-and-removed, clears dirty in both, and never both
/// in one tick. Entity despawns (RemovedEntities) are unchanged.
/// </summary>
public class ComponentRemovalReplicationTests
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
            store.AddOrUpdate(entity, new MarkerComponent { Value = 1 });
        }
    }

    private const ulong MarkerUuid = 0xE030;

    public ComponentRemovalReplicationTests()
    {
        NetworkRegistry.Register<MarkerComponent>(Uuid.FromValue(MarkerUuid), NetworkDirection.ServerOwned, new MarkerCodec());
    }

    [Fact]
    public void RemovalPublishesOnlyTheRemovalAndClearsDirty()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new NetworkComponent());
        store.AddOrUpdate(entity, new MarkerComponent { Value = 7 });
        store.ClearDirty<MarkerComponent>(entity);

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<WorldSnapshot>() });
        var hub = new ServerConnectionHub();
        Uuid clientId = hub.Add(connection.Server);
        var sessions = new SessionManager();
        sessions.Register(store, entity, clientId, new Session(1u));

        var replication = new NetworkReplicationSystem(hub, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingSettings());
        replication.SimTick = 1;

        //  The component is removed server-side; the next publish must emit only a removal.
        Assert.True(store.Remove<MarkerComponent>(entity));
        replication.ApplyStage(0f, store);
        replication.PublishStage(1f, store);

        Result<WorldSnapshot> removalResult = connection.Client.Receive<WorldSnapshot>();
        Assert.True(removalResult.Success);
        WorldSnapshot removal = removalResult.Value;
        Assert.Empty(removal.Components);
        Assert.Equal(1, removal.RemovedComponents.Length);
        Assert.Equal(store.GetUuid(entity).ToValue(), removal.RemovedComponents[0].Entity);
        Assert.Equal(MarkerUuid, removal.RemovedComponents[0].TypeUuid);
        Assert.False(store.IsDirty<MarkerComponent>(entity), "The removal dirty flag must clear.");

        //  The entity survives removal; a later update publishes a delta, not another removal.
        store.AddOrUpdate(entity, new MarkerComponent { Value = 9 });
        replication.SimTick = 2;
        replication.PublishStage(1f, store);

        Result<WorldSnapshot> update = connection.Client.Receive<WorldSnapshot>();
        Assert.True(update.Success);
        Assert.Empty(update.Value.RemovedComponents);
    }
}