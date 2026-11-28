#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DryIoc;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using NsdRpc;
using Swordfish.Library.Util;
using Swordfish.Tests;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Data.Statistics;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Systems;
using Xunit;
using GameState = WaywardBeyond.Client.GameState;
using WaywardBeyondClient = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>Verifies <see cref="StatisticsComponent"/> registration, <see cref="ReplicationAudience.OwnerOnly"/> isolation, and change-gated zero-delta idle ticks.</summary>
[Collection("ClientGameState")]
public sealed class StatisticsReplicationTests
{
    [Fact]
    public void NetworkingCompositionRegistersStatisticsComponentAsOwnerOnlyUuid20()
    {
        using var container = new Container();
        container.RegisterNetworking();
        NetworkRegistry registry = container.Resolve<NetworkRegistry>();

        Result<NetworkComponentInfo> lookup = registry.GetInfo<StatisticsComponent>();
        Assert.True(lookup.Success);
        Assert.Equal(Uuid.FromValue(20), lookup.Value.Uuid);
        Assert.Equal(NetworkDirection.ServerOwned, lookup.Value.Direction);
        Assert.Equal(ReplicationAudience.OwnerOnly, lookup.Value.Audience);
        Assert.IsType<NsdComponentCodec<StatisticsComponent>>(lookup.Value.Codec);
    }

    [Fact]
    public async Task StatisticsComponentReplicatesOnlyToOwnerAcrossFullSyncDeltaRemovalAndSkipsIdleWrites()
    {
        using LoopbackHarness harness = LoopbackHarness.Create();
        LoopbackClient clientA = await harness.AddPlayerClientAsync(Uuid.FromValue(1001));
        LoopbackClient clientB = await harness.AddPlayerClientAsync(Uuid.FromValue(1002));

        var statsA = new StatisticsComponent();
        statsA.SetFixed(StatisticIds.HEALTH, 100f);
        statsA.SetFixed(StatisticIds.HEALTH_MAX, 100f);
        statsA.SetValue(StatisticIds.STRENGTH, 5L);
        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, statsA);

        var statsB = new StatisticsComponent();
        statsB.SetFixed(StatisticIds.HEALTH, 80f);
        statsB.SetFixed(StatisticIds.HEALTH_MAX, 100f);
        statsB.SetValue(StatisticIds.STRENGTH, 2L);
        harness.ServerStore.AddOrUpdate(clientB.ServerEntity, statsB);
        harness.ClearAllDirty();

        //  1. Full sync for client A delivers A's StatisticsComponent and omits B's StatisticsComponent,
        //     while still delivering B's public BodyViewComponent.
        harness.Replication.RequestFullSync(clientA.ServerPeer.GameId);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot fullSyncA = await clientA.WaitForSnapshotAsync();
        Assert.Contains(
            fullSyncA.Components,
            c => c.Entity == clientA.EntityUuid.ToValue() && c.TypeUuid == harness.StatisticsTypeUuid);
        Assert.DoesNotContain(
            fullSyncA.Components,
            c => c.Entity == clientB.EntityUuid.ToValue() && c.TypeUuid == harness.StatisticsTypeUuid);
        Assert.Contains(
            fullSyncA.Components,
            c => c.Entity == clientB.EntityUuid.ToValue() && c.TypeUuid == harness.BodyViewTypeUuid);
        Assert.Empty(clientB.Inbound.Snapshots);

        //  2. Reconcile applies client A's StatisticsComponent onto A's local player entity.
        var clientAStore = new DataStore();
        int localPlayerA = clientAStore.Alloc(clientA.EntityUuid);
        clientAStore.AddOrUpdate(localPlayerA, new PlayerComponent());
        ClientReconcileSystem reconcileA = CreateClientReconcile(clientA.Inbound, harness.Registry);
        clientA.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(fullSyncA));
        TickReconcileWhilePlaying(reconcileA, clientAStore);

        Assert.True(clientAStore.TryGet(localPlayerA, out StatisticsComponent clientStatsAfterJoin));
        Assert.Equal(100f, clientStatsAfterJoin.GetFixed(StatisticIds.HEALTH));
        Assert.Equal(5L, clientStatsAfterJoin.GetValue(StatisticIds.STRENGTH));

        //  3. Delta mutation on client A replicates only to client A, never to client B.
        Assert.True(harness.ServerStore.SetStatisticFixed(clientA.ServerEntity, StatisticIds.HEALTH, 72.5f));
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot deltaA = await clientA.WaitForSnapshotAsync();
        Assert.Single(deltaA.Components);
        Assert.Equal(clientA.EntityUuid.ToValue(), deltaA.Components[0].Entity);
        Assert.Equal(harness.StatisticsTypeUuid, deltaA.Components[0].TypeUuid);
        Assert.Empty(clientB.Inbound.Snapshots);

        clientA.Inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(deltaA));
        TickReconcileWhilePlaying(reconcileA, clientAStore);
        Assert.True(clientAStore.TryGet(localPlayerA, out StatisticsComponent clientStatsAfterDelta));
        Assert.Equal(72.5f, clientStatsAfterDelta.GetFixed(StatisticIds.HEALTH));

        //  4. Change-gated idle write produces zero dirty flag and zero wire snapshots.
        Assert.False(harness.ServerStore.SetStatisticFixed(clientA.ServerEntity, StatisticIds.HEALTH, 72.5f));
        Assert.False(harness.ServerStore.IsDirty<StatisticsComponent>(clientA.ServerEntity));
        harness.Replication.PublishStage(1f, harness.ServerStore);
        Assert.Empty(clientA.Inbound.Snapshots);
        Assert.Empty(clientB.Inbound.Snapshots);

        //  5. Removal of client A's StatisticsComponent notifies only client A.
        harness.ServerStore.Remove(typeof(StatisticsComponent), clientA.ServerEntity);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot removalA = await clientA.WaitForSnapshotAsync();
        Assert.Single(removalA.RemovedComponents);
        Assert.Equal(clientA.EntityUuid.ToValue(), removalA.RemovedComponents[0].Entity);
        Assert.Equal(harness.StatisticsTypeUuid, removalA.RemovedComponents[0].TypeUuid);
        Assert.Empty(clientB.Inbound.Snapshots);
    }

    private static ClientReconcileSystem CreateClientReconcile(ClientInbound inbound, NetworkRegistry registry)
    {
        var motionProcessor = new ClientPlayerMotionProcessor(
            new FakeInputService(),
            new FakeWindowContext(),
            new FakePhysics(),
            new EventInvoker<PlayerMovedEvent>([])
        );

        return new ClientReconcileSystem(
            inbound,
            new SnapshotAckTracker(),
            motionProcessor,
            new LocalPlayerReconciler(NullLogger<LocalPlayerReconciler>.Instance),
            registry,
            NullLogger<ClientReconcileSystem>.Instance
        );
    }

    private static void TickReconcileWhilePlaying(ClientReconcileSystem reconcile, DataStore store)
    {
        GameState previous = WaywardBeyondClient.GameState.Get();
        WaywardBeyondClient.GameState.Set(GameState.Playing);
        try
        {
            reconcile.Tick(1f / 128f, store);
        }
        finally
        {
            WaywardBeyondClient.GameState.Set(previous);
        }
    }

    private sealed class LoopbackHarness : IDisposable
    {
        private readonly LoopbackRpcServerTransport _transport;
        private readonly RpcServer _rpcServer;
        private readonly StubRpcHost _host = new();
        private readonly ServerPeers _peers = new();
        private readonly SessionManager _sessions = new();
        private readonly List<LoopbackClient> _clients = [];
        private readonly Lock _connectLock = new();
        private TaskCompletionSource<RpcPeer>? _pendingConnect;
        private uint _nextSessionId = 1;

        public DataStore ServerStore { get; } = new();
        public NetworkRegistry Registry { get; }
        public NetworkReplicationSystem Replication { get; }
        public ulong StatisticsTypeUuid { get; }
        public ulong BodyViewTypeUuid { get; }

        private LoopbackHarness(NetworkRegistry registry)
        {
            Registry = registry;
            Replication = new NetworkReplicationSystem(
                _peers,
                _sessions,
                NullLogger<NetworkReplicationSystem>.Instance,
                new NetworkingConfig(),
                registry
            );

            StatisticsTypeUuid = registry.GetInfo<StatisticsComponent>().Value.Uuid.ToValue();
            BodyViewTypeUuid = registry.GetInfo<BodyViewComponent>().Value.Uuid.ToValue();

            _transport = new LoopbackRpcServerTransport();
            var serverRpcRegistry = new RpcRegistry();
            serverRpcRegistry.Freeze();
            _rpcServer = new RpcServer(_transport, serverRpcRegistry);
            _rpcServer.ClientConnected += OnClientConnected;
            _rpcServer.Start();
        }

        public static LoopbackHarness Create()
        {
            using var container = new Container();
            container.RegisterNetworking();
            return new LoopbackHarness(container.Resolve<NetworkRegistry>());
        }

        public async Task<LoopbackClient> AddPlayerClientAsync(Uuid entityUuid)
        {
            var connected = new TaskCompletionSource<RpcPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_connectLock)
            {
                _pendingConnect = connected;
            }

            var connection = new LoopbackRpcClientConnection(_transport);
            await connection.ConnectAsync();

            var inbound = new ClientInbound();
            var clientRpcRegistry = new RpcRegistry();
            clientRpcRegistry.AddService(new ClientHandlers(inbound));
            clientRpcRegistry.Freeze();
            _ = new RpcClient(connection, clientRpcRegistry);

            RpcPeer rpcPeer = await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
            ServerPeer serverPeer = _peers.GetOrAdd(_host, rpcPeer, isLocal: true);

            int entity = ServerStore.Alloc(entityUuid);
            ServerStore.AddOrUpdate(entity, NetworkComponent.Create());
            ServerStore.AddOrUpdate(entity, new BodyViewComponent { Body = "wb:human_m" });
            _sessions.Register(ServerStore, entity, serverPeer.GameId, new Session(_nextSessionId++));

            var client = new LoopbackClient(connection, inbound, serverPeer, entity, entityUuid);
            _clients.Add(client);
            return client;
        }

        public void ClearAllDirty()
        {
            NetworkComponentInfo[] serverOwned = Registry.GetComponents(NetworkDirection.ServerOwned);
            for (var i = 0; i < _clients.Count; i++)
            {
                int entity = _clients[i].ServerEntity;
                for (var c = 0; c < serverOwned.Length; c++)
                {
                    ServerStore.ClearDirty(serverOwned[c].Type, entity);
                }
            }
        }

        public void Dispose()
        {
            for (var i = 0; i < _clients.Count; i++)
            {
                _clients[i].Connection.Close();
            }

            _rpcServer.Close();
            _transport.Close();
        }

        private void OnClientConnected(RpcPeer peer)
        {
            lock (_connectLock)
            {
                _pendingConnect?.TrySetResult(peer);
            }
        }
    }

    private sealed class LoopbackClient(
        LoopbackRpcClientConnection connection,
        ClientInbound inbound,
        ServerPeer serverPeer,
        int serverEntity,
        Uuid entityUuid
    )
    {
        public LoopbackRpcClientConnection Connection { get; } = connection;
        public ClientInbound Inbound { get; } = inbound;
        public ServerPeer ServerPeer { get; } = serverPeer;
        public int ServerEntity { get; } = serverEntity;
        public Uuid EntityUuid { get; } = entityUuid;

        public async Task<WorldSnapshot> WaitForSnapshotAsync()
        {
            long deadline = Environment.TickCount64 + 5_000;
            while (Inbound.Snapshots.IsEmpty && Environment.TickCount64 < deadline)
            {
                await Task.Delay(5);
            }

            Assert.False(Inbound.Snapshots.IsEmpty, "Timed out waiting for a replicated snapshot.");
            Assert.True(Inbound.Snapshots.TryDequeue(out ClientInbound.InboundSnapshot message));
            Assert.False(message.IsBatch);
            return message.Full;
        }
    }

    private sealed class StubRpcHost : IRpcHost
    {
        public PeerSet Peers => default;
    }
}
