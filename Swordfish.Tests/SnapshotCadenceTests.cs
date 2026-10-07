using System;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Server.Core;
using WaywardBeyond.Server.Core.Systems;
using WaywardBeyond.Shared.Config;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Sessions;
using WaywardBeyond.Shared.Networking.Transport;
using Xunit;

namespace Swordfish.Tests;

/// <summary>
/// The publish stage honors <see cref="NetworkingSettings.SnapshotHz"/> measured in wall-clock time,
/// instead of publishing on every server tick.
/// </summary>
public class SnapshotCadenceTests
{
    private struct CadenceComponent : IDataComponent
    {
        public int Value;
    }

    private sealed class CadenceCodec : IPayloadCodec
    {
        public Type ComponentType => typeof(CadenceComponent);

        public byte[] Serialize(DataStore store, int entity)
        {
            store.TryGet(entity, out CadenceComponent value);
            return BitConverter.GetBytes(value.Value);
        }

        public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
        {
            store.AddOrUpdate(entity, new CadenceComponent { Value = 1 });
        }
    }

    private const ulong CadenceUuid = 0xE0C1;

    public SnapshotCadenceTests()
    {
        NetworkRegistry.Register<CadenceComponent>(Uuid.FromValue(CadenceUuid), NetworkDirection.ServerOwned, new CadenceCodec());
    }

    [Fact]
    public void PublishesOncePerSnapshotIntervalNotEveryTick()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new NetworkComponent());
        store.AddOrUpdate(entity, new CadenceComponent { Value = 7 });

        var connection = new LocalConnection(new INetworkSerializer[] { new NsdMessageSerializer<WorldSnapshot>() });
        var hub = new ServerConnectionHub();
        Uuid clientId = hub.Add(connection.Server);
        var sessions = new SessionManager();
        sessions.Register(store, entity, clientId, new Session(1u));

        var settings = new NetworkingSettings();
        settings.SnapshotHz.Set(30);

        var replication = new NetworkReplicationSystem(hub, sessions, NullLogger<NetworkReplicationSystem>.Instance, settings);
        replication.SimTick = 1;

        //  At 60 tps the interval (1/30 s) is reached on the second tick: the first publishes nothing.
        float tick = 1f / 60f;
        replication.PublishStage(tick, store);
        Assert.False(connection.Client.Receive<WorldSnapshot>().Success, "Nothing publishes before the interval elapses.");

        replication.PublishStage(tick, store);
        Assert.True(connection.Client.Receive<WorldSnapshot>().Success, "The interval crossing publishes one snapshot.");
    }
}
