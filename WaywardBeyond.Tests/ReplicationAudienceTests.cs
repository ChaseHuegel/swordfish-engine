#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using DryIoc;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using NsdRpc;
using Swordfish.ECS;
using Swordfish.Library.Types.Shapes;
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
using WaywardBeyond.Gameplay;
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

/// <summary>Pins declarative replication audiences, entity exclusions, unconditional component removals, and client-derived remote character colliders.</summary>
[Collection("ClientGameState")]
public sealed class ReplicationAudienceTests
{
    [Fact]
    public void RegistryRejectsScopedAudienceOnClientOwnedComponent()
    {
        var registry = new NetworkRegistry();

        Result othersOnly = registry.Register<InputComponent>(
            Uuid.FromValue(1),
            NetworkDirection.ClientOwned,
            new NsdComponentCodec<InputComponent>(),
            ReplicationAudience.OthersOnly
        );
        Result ownerOnly = registry.Register<EquipmentComponent>(
            Uuid.FromValue(12),
            NetworkDirection.ClientOwned,
            new NsdComponentCodec<EquipmentComponent>(),
            ReplicationAudience.OwnerOnly
        );

        Assert.False(othersOnly.Success);
        Assert.Contains("ServerOwned", othersOnly.Message);
        Assert.False(ownerOnly.Success);
        Assert.Contains("ServerOwned", ownerOnly.Message);
    }

    [Fact]
    public void DefaultRegistryUsesOthersOnlyForLaserBeamAndDoesNotRegisterColliderComponent()
    {
        using var container = new Container();
        container.RegisterNetworking();
        NetworkRegistry registry = container.Resolve<NetworkRegistry>();

        Result<NetworkComponentInfo> laser = registry.GetInfo<LaserBeamComponent>();
        Assert.True(laser.Success);
        Assert.Equal(Uuid.FromValue(17), laser.Value.Uuid);
        Assert.Equal(ReplicationAudience.OthersOnly, laser.Value.Audience);

        Assert.False(registry.GetInfo<ColliderComponent>().Success);
    }

    [Fact]
    public async Task OthersOnlyAudienceOmitsOwnerAcrossDeltaFullSyncAndRemovalAndSkipsEmptyDeltas()
    {
        using LoopbackHarness harness = LoopbackHarness.CreateWithDefaultRegistry();
        LoopbackClient clientA = await harness.AddCharacterClientAsync(Uuid.FromValue(101));
        LoopbackClient clientB = await harness.AddCharacterClientAsync(Uuid.FromValue(102));
        harness.ClearAllDirty();

        //  1. Isolated delta on an OthersOnly component delivers to observer and skips empty delta for owner.
        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, new LaserBeamComponent { TargetX = 4f, Hit = true });
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot observerDelta = await clientB.WaitForSnapshotAsync();
        Assert.Single(observerDelta.Components);
        Assert.Equal(clientA.EntityUuid.ToValue(), observerDelta.Components[0].Entity);
        Assert.Equal(harness.LaserBeamTypeUuid, observerDelta.Components[0].TypeUuid);
        Assert.Empty(clientA.Inbound.Snapshots);

        //  2. Mixed delta sends All components to owner while omitting OthersOnly component.
        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, new TransformComponent(new Vector3(5f, 0f, 0f), Quaternion.Identity, Vector3.One));
        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, new LaserBeamComponent { TargetX = 8f, Hit = true });
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot ownerMixed = await clientA.WaitForSnapshotAsync();
        WorldSnapshot observerMixed = await clientB.WaitForSnapshotAsync();

        Assert.Contains(ownerMixed.Components, c => c.Entity == clientA.EntityUuid.ToValue() && c.TypeUuid == harness.TransformTypeUuid);
        Assert.DoesNotContain(ownerMixed.Components, c => c.Entity == clientA.EntityUuid.ToValue() && c.TypeUuid == harness.LaserBeamTypeUuid);

        Assert.Contains(observerMixed.Components, c => c.Entity == clientA.EntityUuid.ToValue() && c.TypeUuid == harness.TransformTypeUuid);
        Assert.Contains(observerMixed.Components, c => c.Entity == clientA.EntityUuid.ToValue() && c.TypeUuid == harness.LaserBeamTypeUuid);

        //  3. Full sync omits client A's own LaserBeamComponent while including client B's.
        harness.ServerStore.AddOrUpdate(clientB.ServerEntity, new LaserBeamComponent { TargetZ = 3f, Hit = false });
        harness.ClearAllDirty();

        harness.Replication.RequestFullSync(clientA.ServerPeer.GameId);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot fullSyncForA = await clientA.WaitForSnapshotAsync();
        Assert.Contains(fullSyncForA.Components, c => c.Entity == clientA.EntityUuid.ToValue() && c.TypeUuid == harness.TransformTypeUuid);
        Assert.DoesNotContain(fullSyncForA.Components, c => c.Entity == clientA.EntityUuid.ToValue() && c.TypeUuid == harness.LaserBeamTypeUuid);
        Assert.Contains(fullSyncForA.Components, c => c.Entity == clientB.EntityUuid.ToValue() && c.TypeUuid == harness.LaserBeamTypeUuid);
        Assert.Empty(clientB.Inbound.Snapshots);

        //  4. Simultaneous removal of LaserBeamComponent and BodyViewComponent publishes both removals
        //     to observer and omits the OthersOnly removal for owner (no removal suppression).
        harness.ServerStore.Remove(typeof(BodyViewComponent), clientA.ServerEntity);
        harness.ServerStore.Remove(typeof(LaserBeamComponent), clientA.ServerEntity);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot ownerRemoval = await clientA.WaitForSnapshotAsync();
        WorldSnapshot observerRemoval = await clientB.WaitForSnapshotAsync();

        Assert.Contains(ownerRemoval.RemovedComponents, r => r.Entity == clientA.EntityUuid.ToValue() && r.TypeUuid == harness.BodyViewTypeUuid);
        Assert.DoesNotContain(ownerRemoval.RemovedComponents, r => r.Entity == clientA.EntityUuid.ToValue() && r.TypeUuid == harness.LaserBeamTypeUuid);

        Assert.Contains(observerRemoval.RemovedComponents, r => r.Entity == clientA.EntityUuid.ToValue() && r.TypeUuid == harness.BodyViewTypeUuid);
        Assert.Contains(observerRemoval.RemovedComponents, r => r.Entity == clientA.EntityUuid.ToValue() && r.TypeUuid == harness.LaserBeamTypeUuid);
        Assert.False(harness.ServerStore.IsDirty<LaserBeamComponent>(clientA.ServerEntity));
    }

    [Fact]
    public async Task OwnerOnlyAudienceDeliversOnlyToOwnerAndSerializesOncePerDirtyEntity()
    {
        var codec = new CountingBodyViewCodec();
        var registry = new NetworkRegistry();
        Uuid bodyViewType = Uuid.FromValue(10);

        Assert.True(registry.Register<BodyViewComponent>(
            bodyViewType,
            NetworkDirection.ServerOwned,
            codec,
            ReplicationAudience.OwnerOnly
        ).Success);
        registry.Freeze();

        using LoopbackHarness harness = LoopbackHarness.Create(registry);
        LoopbackClient clientA = await harness.AddBareClientAsync(Uuid.FromValue(201));
        LoopbackClient clientB = await harness.AddBareClientAsync(Uuid.FromValue(202));

        //  1. Delta delivers only to owner and serializes once despite two recipients.
        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, new BodyViewComponent { Body = "wb:human_m" });
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot ownerDelta = await clientA.WaitForSnapshotAsync();
        Assert.Single(ownerDelta.Components);
        Assert.Equal(clientA.EntityUuid.ToValue(), ownerDelta.Components[0].Entity);
        Assert.Empty(clientB.Inbound.Snapshots);
        Assert.Equal(1, codec.SerializeCount);

        //  2. Full sync delivers only the recipient's own OwnerOnly component.
        harness.ServerStore.AddOrUpdate(clientB.ServerEntity, new BodyViewComponent { Body = "wb:human_f" });
        harness.ClearAllDirty();

        harness.Replication.RequestFullSync(clientA.ServerPeer.GameId);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot ownerFullSync = await clientA.WaitForSnapshotAsync();
        Assert.Single(ownerFullSync.Components);
        Assert.Equal(clientA.EntityUuid.ToValue(), ownerFullSync.Components[0].Entity);
        Assert.Empty(clientB.Inbound.Snapshots);

        //  3. Removal delivers only to owner and sends no empty delta to observer.
        harness.ServerStore.Remove(typeof(BodyViewComponent), clientA.ServerEntity);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot ownerRemoval = await clientA.WaitForSnapshotAsync();
        Assert.Single(ownerRemoval.RemovedComponents);
        Assert.Equal(clientA.EntityUuid.ToValue(), ownerRemoval.RemovedComponents[0].Entity);
        Assert.Equal(bodyViewType.ToValue(), ownerRemoval.RemovedComponents[0].TypeUuid);
        Assert.Empty(clientB.Inbound.Snapshots);
    }

    [Fact]
    public async Task LocalPlayerKeepsLocallyBuiltColliderAndRemotePlayerDerivesColliderFromBodyView()
    {
        using LoopbackHarness harness = LoopbackHarness.CreateWithDefaultRegistry();
        LoopbackClient clientA = await harness.AddCharacterClientAsync(Uuid.FromValue(501));
        LoopbackClient clientB = await harness.AddCharacterClientAsync(Uuid.FromValue(502));

        //  Also create a server voxel structure with a local ColliderComponent.
        Uuid structureUuid = Uuid.FromValue(550);
        int serverStructure = harness.ServerStore.Alloc(structureUuid);
        harness.ServerStore.AddOrUpdate(serverStructure, NetworkComponent.Create());
        harness.ServerStore.AddOrUpdate(serverStructure, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        harness.ServerStore.AddOrUpdate(serverStructure, new ColliderComponent(new Box3(Vector3.One)));

        //  Build client A's local player and local structure with their own locally built colliders.
        var clientStore = new DataStore();
        int localPlayer = clientStore.Alloc(clientA.EntityUuid);
        clientStore.AddOrUpdate(localPlayer, new PlayerComponent());
        clientStore.AddOrUpdate(localPlayer, new TransformComponent(Vector3.Zero, Quaternion.Identity, CharacterDefaults.PlayerScale));
        clientStore.AddOrUpdate(localPlayer, CharacterDefaults.CreateCharacterPhysicsComponent());
        ColliderComponent localCollider = CharacterDefaults.CreateCharacterColliderComponent(CharacterDefaults.PlayerScale);
        Shape[] originalPlayerShapes = localCollider.CompoundShape!.Value.Shapes;
        clientStore.AddOrUpdate(localPlayer, localCollider);
        clientStore.AddOrUpdate(localPlayer, new InputComponent());
        clientStore.AddOrUpdate(localPlayer, PendingInputComponent.Create());
        clientStore.ClearDirty<ColliderComponent>(localPlayer);

        int clientStructure = clientStore.Alloc(structureUuid);
        var structureCollider = new ColliderComponent(new Box3(new Vector3(2f, 2f, 2f)));
        clientStore.AddOrUpdate(clientStructure, structureCollider);
        clientStore.ClearDirty<ColliderComponent>(clientStructure);

        ClientReconcileSystem reconcile = CreateClientReconcile(clientA.Inbound, harness.Registry);

        //  1. Full-sync join derives remote player collider from BodyViewComponent while leaving
        //     local player and structure colliders untouched.
        harness.ClearAllDirty();
        harness.Replication.RequestFullSync(clientA.ServerPeer.GameId);
        harness.Replication.PublishStage(1f, harness.ServerStore);
        await clientA.WaitUntilSnapshotQueuedAsync();
        TickReconcileWhilePlaying(reconcile, clientStore);

        Assert.True(clientStore.TryGet(localPlayer, out ColliderComponent afterFullSync));
        Assert.True(afterFullSync.CompoundShape.HasValue);
        Assert.Same(originalPlayerShapes, afterFullSync.CompoundShape!.Value.Shapes);
        Assert.False(clientStore.IsDirty<ColliderComponent>(localPlayer));

        Assert.True(clientStore.TryGet(clientB.EntityUuid, out int remotePlayer));
        Assert.True(clientStore.TryGet(remotePlayer, out ColliderComponent remoteCollider));
        Assert.True(remoteCollider.CompoundShape.HasValue);
        Shape[] derivedRemoteShapes = remoteCollider.CompoundShape!.Value.Shapes;

        Assert.True(clientStore.TryGet(clientStructure, out ColliderComponent structureAfterFullSync));
        Assert.Equal(structureCollider.Shape, structureAfterFullSync.Shape);

        //  2. Subsequent deltas and server-side ColliderComponent removals leave local player,
        //     derived remote player, and structure colliders intact without shape resets.
        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, new TransformComponent(new Vector3(1f, 0f, 0f), Quaternion.Identity, CharacterDefaults.PlayerScale));
        harness.ServerStore.Remove(typeof(ColliderComponent), clientA.ServerEntity);
        harness.ServerStore.Remove(typeof(ColliderComponent), serverStructure);
        harness.Replication.PublishStage(1f, harness.ServerStore);
        await clientA.WaitUntilSnapshotQueuedAsync();
        TickReconcileWhilePlaying(reconcile, clientStore);

        Assert.True(clientStore.TryGet(localPlayer, out ColliderComponent afterDelta));
        Assert.Same(originalPlayerShapes, afterDelta.CompoundShape!.Value.Shapes);
        Assert.False(clientStore.IsDirty<ColliderComponent>(localPlayer));

        Assert.True(clientStore.TryGet(remotePlayer, out ColliderComponent remoteAfterDelta));
        Assert.Same(derivedRemoteShapes, remoteAfterDelta.CompoundShape!.Value.Shapes);

        Assert.True(clientStore.TryGet(clientStructure, out ColliderComponent structureAfterRemoval));
        Assert.Equal(structureCollider.Shape, structureAfterRemoval.Shape);
    }

    [Fact]
    public async Task ExcludedRecipientEntityOmitsAllComponentsRemovalsFullSyncAndDespawnForOwner()
    {
        using LoopbackHarness harness = LoopbackHarness.CreateWithDefaultRegistry();
        LoopbackClient owner = await harness.AddCharacterClientAsync(Uuid.FromValue(601));
        LoopbackClient observer = await harness.AddCharacterClientAsync(Uuid.FromValue(602));
        harness.ClearAllDirty();

        Uuid excludedUuid = Uuid.FromValue(650);
        int excludedEntity = harness.ServerStore.Alloc(excludedUuid);
        NetworkComponent excludedNet = NetworkComponent.Create();
        excludedNet.ExcludedRecipientEntity = owner.EntityUuid;
        harness.ServerStore.AddOrUpdate(excludedEntity, excludedNet);
        harness.ServerStore.AddOrUpdate(excludedEntity, new TransformComponent(new Vector3(3f, 4f, 5f), Quaternion.Identity, Vector3.One));
        harness.ServerStore.AddOrUpdate(excludedEntity, new IdentifierComponent(name: null, tag: "game"));

        //  1. Delta omits every component of the excluded entity for the owner while sending all to observer.
        harness.Replication.PublishStage(1f, harness.ServerStore);
        WorldSnapshot observerDelta = await observer.WaitForSnapshotAsync();
        Assert.Contains(observerDelta.Components, c => c.Entity == excludedUuid.ToValue() && c.TypeUuid == harness.TransformTypeUuid);
        Assert.Empty(owner.Inbound.Snapshots);

        //  2. Full sync omits the excluded entity for the owner while sending it to observer.
        harness.Replication.RequestFullSync(owner.ServerPeer.GameId);
        harness.Replication.RequestFullSync(observer.ServerPeer.GameId);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot ownerFullSync = await owner.WaitForSnapshotAsync();
        WorldSnapshot observerFullSync = await observer.WaitForSnapshotAsync();
        Assert.DoesNotContain(ownerFullSync.Components, c => c.Entity == excludedUuid.ToValue());
        Assert.Contains(observerFullSync.Components, c => c.Entity == excludedUuid.ToValue() && c.TypeUuid == harness.TransformTypeUuid);

        //  3. Component removal on a live excluded entity omits the excluded owner while notifying observer.
        harness.ServerStore.Remove(typeof(IdentifierComponent), excludedEntity);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot observerRemoval = await observer.WaitForSnapshotAsync();
        Assert.Contains(observerRemoval.RemovedComponents, r => r.Entity == excludedUuid.ToValue());
        Assert.Empty(owner.Inbound.Snapshots);

        //  4. Entity despawn omits the excluded owner while notifying observer.
        harness.Replication.RequestDespawn(excludedUuid.ToValue(), owner.EntityUuid);
        harness.ServerStore.Free(excludedEntity);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot observerDespawn = await observer.WaitForSnapshotAsync();
        Assert.Contains(observerDespawn.RemovedEntities, id => id == excludedUuid.ToValue());
        Assert.Empty(owner.Inbound.Snapshots);
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

    private sealed class CountingBodyViewCodec : IPayloadCodec<BodyViewComponent>
    {
        private readonly NsdComponentCodec<BodyViewComponent> _inner = new();

        public int SerializeCount { get; private set; }

        /// <inheritdoc/>
        public Result<byte[]> Serialize(DataStore store, int entity)
        {
            SerializeCount++;
            return _inner.Serialize(store, entity);
        }

        /// <inheritdoc/>
        public byte[] Serialize(in BodyViewComponent value)
        {
            return _inner.Serialize(in value);
        }

        /// <inheritdoc/>
        public Result Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
        {
            return _inner.Apply(store, entity, payload);
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
        public ulong TransformTypeUuid { get; }
        public ulong BodyViewTypeUuid { get; }
        public ulong LaserBeamTypeUuid { get; }

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

            Result<NetworkComponentInfo> transformInfo = registry.GetInfo<TransformComponent>();
            TransformTypeUuid = transformInfo.Success ? transformInfo.Value.Uuid.ToValue() : 0UL;
            Result<NetworkComponentInfo> bodyViewInfo = registry.GetInfo<BodyViewComponent>();
            BodyViewTypeUuid = bodyViewInfo.Success ? bodyViewInfo.Value.Uuid.ToValue() : 0UL;
            Result<NetworkComponentInfo> laserBeamInfo = registry.GetInfo<LaserBeamComponent>();
            LaserBeamTypeUuid = laserBeamInfo.Success ? laserBeamInfo.Value.Uuid.ToValue() : 0UL;

            _transport = new LoopbackRpcServerTransport();
            var serverRpcRegistry = new RpcRegistry();
            serverRpcRegistry.Freeze();
            _rpcServer = new RpcServer(_transport, serverRpcRegistry);
            _rpcServer.ClientConnected += OnClientConnected;
            _rpcServer.Start();
        }

        public static LoopbackHarness CreateWithDefaultRegistry()
        {
            using var container = new Container();
            container.RegisterNetworking();
            return Create(container.Resolve<NetworkRegistry>());
        }

        public static LoopbackHarness Create(NetworkRegistry registry)
        {
            return new LoopbackHarness(registry);
        }

        public async Task<LoopbackClient> AddCharacterClientAsync(Uuid entityUuid)
        {
            LoopbackClient client = await AddBareClientAsync(entityUuid);
            ServerStore.AddOrUpdate(client.ServerEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, CharacterDefaults.PlayerScale));
            ServerStore.AddOrUpdate(client.ServerEntity, CharacterDefaults.CreateCharacterPhysicsComponent());
            ServerStore.AddOrUpdate(client.ServerEntity, CharacterDefaults.CreateCharacterColliderComponent(CharacterDefaults.PlayerScale));
            ServerStore.AddOrUpdate(client.ServerEntity, new BodyViewComponent { Body = "wb:human_m" });
            return client;
        }

        public async Task<LoopbackClient> AddBareClientAsync(Uuid entityUuid)
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

        public async Task WaitUntilSnapshotQueuedAsync()
        {
            long deadline = Environment.TickCount64 + 5_000;
            while (Inbound.Snapshots.IsEmpty && Environment.TickCount64 < deadline)
            {
                await Task.Delay(5);
            }

            Assert.False(Inbound.Snapshots.IsEmpty, "Timed out waiting for a replicated snapshot.");
        }

        public async Task<WorldSnapshot> WaitForSnapshotAsync()
        {
            await WaitUntilSnapshotQueuedAsync();
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
