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
using Swordfish.Physics;
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
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Systems;
using Xunit;
using GameState = WaywardBeyond.Client.GameState;
using WaywardBeyondClient = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>Pins per-recipient replication filtering across deltas, full syncs, removals, and local player colliders.</summary>
[Collection("ClientGameState")]
public sealed class RecipientReplicationFilterTests
{
    [Fact]
    public void RegistryRejectsRecipientFilterOnClientOwnedComponent()
    {
        var registry = new NetworkRegistry();

        Result registration = registry.Register<InputComponent>(
            Uuid.FromValue(1),
            NetworkDirection.ClientOwned,
            new NsdComponentCodec<InputComponent>(),
            recipientFilter: static (_, _, _) => true
        );

        Assert.False(registration.Success);
        Assert.Contains("ServerOwned", registration.Message);
    }

    [Fact]
    public async Task DeltaPublishEvaluatesPerRecipientFilterAndSkipsEmptyDeltas()
    {
        using LoopbackHarness harness = LoopbackHarness.CreateWithDefaultRegistry();
        LoopbackClient clientA = await harness.AddCharacterClientAsync(Uuid.FromValue(101));
        LoopbackClient clientB = await harness.AddCharacterClientAsync(Uuid.FromValue(102));

        //  Clear initial spawn dirty flags before testing isolated deltas.
        harness.ClearAllDirty();

        //  Dirty only client A's collider: client B receives it, while client A receives no empty delta.
        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, CharacterDefaults.CreateCharacterColliderComponent(Vector3.One));
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot snapshotForB = await clientB.WaitForSnapshotAsync();
        Assert.Single(snapshotForB.Components);
        Assert.Equal(clientA.EntityUuid.ToValue(), snapshotForB.Components[0].Entity);
        Assert.Equal(harness.ColliderTypeUuid, snapshotForB.Components[0].TypeUuid);
        Assert.Empty(clientA.Inbound.Snapshots);

        //  Dirty client A's transform and collider plus a non-character structure's collider.
        int structure = harness.ServerStore.Alloc(Uuid.FromValue(500));
        harness.ServerStore.AddOrUpdate(structure, NetworkComponent.Create());
        harness.ServerStore.AddOrUpdate(structure, new ColliderComponent(new Box3(Vector3.One)));

        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, new TransformComponent(new Vector3(5f, 0f, 0f), Quaternion.Identity, Vector3.One));
        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, CharacterDefaults.CreateCharacterColliderComponent(Vector3.One));
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot ownerSnapshot = await clientA.WaitForSnapshotAsync();
        WorldSnapshot observerSnapshot = await clientB.WaitForSnapshotAsync();

        Assert.Contains(ownerSnapshot.Components, c => c.Entity == clientA.EntityUuid.ToValue() && c.TypeUuid == harness.TransformTypeUuid);
        Assert.DoesNotContain(ownerSnapshot.Components, c => c.Entity == clientA.EntityUuid.ToValue() && c.TypeUuid == harness.ColliderTypeUuid);
        Assert.DoesNotContain(ownerSnapshot.Components, c => c.Entity == 500UL && c.TypeUuid == harness.ColliderTypeUuid);

        Assert.Contains(observerSnapshot.Components, c => c.Entity == clientA.EntityUuid.ToValue() && c.TypeUuid == harness.TransformTypeUuid);
        Assert.Contains(observerSnapshot.Components, c => c.Entity == clientA.EntityUuid.ToValue() && c.TypeUuid == harness.ColliderTypeUuid);
        Assert.DoesNotContain(observerSnapshot.Components, c => c.Entity == 500UL && c.TypeUuid == harness.ColliderTypeUuid);
    }

    [Fact]
    public async Task FullSyncEvaluatesPerRecipientFilter()
    {
        using LoopbackHarness harness = LoopbackHarness.CreateWithDefaultRegistry();
        LoopbackClient clientA = await harness.AddCharacterClientAsync(Uuid.FromValue(201));
        LoopbackClient clientB = await harness.AddCharacterClientAsync(Uuid.FromValue(202));

        //  Clear dirty flags so only the full-sync snapshot carries component state.
        harness.ClearAllDirty();

        harness.Replication.RequestFullSync(clientA.ServerPeer.GameId);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot fullSync = await clientA.WaitForSnapshotAsync();
        Assert.Contains(fullSync.Components, c => c.Entity == clientA.EntityUuid.ToValue() && c.TypeUuid == harness.TransformTypeUuid);
        Assert.DoesNotContain(fullSync.Components, c => c.Entity == clientA.EntityUuid.ToValue() && c.TypeUuid == harness.ColliderTypeUuid);
        Assert.Contains(fullSync.Components, c => c.Entity == clientB.EntityUuid.ToValue() && c.TypeUuid == harness.ColliderTypeUuid);
        Assert.Empty(clientB.Inbound.Snapshots);
    }

    [Fact]
    public async Task ComponentRemovalEvaluatesPerRecipientFilter()
    {
        using LoopbackHarness harness = LoopbackHarness.CreateWithDefaultRegistry();
        LoopbackClient clientA = await harness.AddCharacterClientAsync(Uuid.FromValue(301));
        LoopbackClient clientB = await harness.AddCharacterClientAsync(Uuid.FromValue(302));

        harness.ClearAllDirty();

        //  1. Isolated removal of client A's collider publishes ComponentRemoval to client B, never to client A.
        harness.ServerStore.Remove(typeof(ColliderComponent), clientA.ServerEntity);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot observerSnapshot = await clientB.WaitForSnapshotAsync();
        Assert.Single(observerSnapshot.RemovedComponents);
        Assert.Equal(clientA.EntityUuid.ToValue(), observerSnapshot.RemovedComponents[0].Entity);
        Assert.Equal(harness.ColliderTypeUuid, observerSnapshot.RemovedComponents[0].TypeUuid);

        //  2. Re-add, clear dirty, then publish a mixed tick with transform delta + collider removal.
        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, CharacterDefaults.CreateCharacterColliderComponent(Vector3.One));
        harness.ClearAllDirty();

        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, new TransformComponent(new Vector3(2f, 0f, 0f), Quaternion.Identity, Vector3.One));
        harness.ServerStore.Remove(typeof(ColliderComponent), clientA.ServerEntity);
        harness.Replication.PublishStage(1f, harness.ServerStore);

        //  Client A's first snapshot arrives here, proving step 1 sent nothing and step 2 omits the removal.
        WorldSnapshot ownerMixed = await clientA.WaitForSnapshotAsync();
        WorldSnapshot observerMixed = await clientB.WaitForSnapshotAsync();

        Assert.Single(ownerMixed.Components);
        Assert.Equal(harness.TransformTypeUuid, ownerMixed.Components[0].TypeUuid);
        Assert.Empty(ownerMixed.RemovedComponents);

        Assert.Single(observerMixed.Components);
        Assert.Single(observerMixed.RemovedComponents);
        Assert.Equal(clientA.EntityUuid.ToValue(), observerMixed.RemovedComponents[0].Entity);
        Assert.Equal(harness.ColliderTypeUuid, observerMixed.RemovedComponents[0].TypeUuid);
    }

    [Fact]
    public async Task RecipientFilterReceivesRecipientMetadataAndSerializesOncePerDirtyEntity()
    {
        var codec = new CountingBodyViewCodec();
        var observedRecipients = new List<ReplicationRecipient>();
        var registry = new NetworkRegistry();
        Uuid bodyViewType = Uuid.FromValue(10);

        Assert.True(registry.Register<BodyViewComponent>(
            bodyViewType,
            NetworkDirection.ServerOwned,
            codec,
            replicationFilter: static (store, entity) =>
                store.TryGet(entity, out BodyViewComponent view) && !string.IsNullOrEmpty(view.Body),
            recipientFilter: (store, entity, recipient) =>
            {
                observedRecipients.Add(recipient);
                return store.GetUuid(entity) != recipient.EntityUuid;
            }
        ).Success);
        registry.Freeze();

        using LoopbackHarness harness = LoopbackHarness.Create(registry);
        LoopbackClient clientA = await harness.AddBareClientAsync(Uuid.FromValue(401));
        LoopbackClient clientB = await harness.AddBareClientAsync(Uuid.FromValue(402));

        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, new BodyViewComponent { Body = "wb:human_m" });
        harness.ServerStore.AddOrUpdate(clientB.ServerEntity, new BodyViewComponent { Body = "" });
        harness.Replication.PublishStage(1f, harness.ServerStore);

        WorldSnapshot snapshotForB = await clientB.WaitForSnapshotAsync();
        Assert.Single(snapshotForB.Components);
        Assert.Equal(clientA.EntityUuid.ToValue(), snapshotForB.Components[0].Entity);
        Assert.Empty(clientA.Inbound.Snapshots);

        //  Entity B failed the per-entity filter; entity A serialized once despite two recipients.
        Assert.Equal(1, codec.SerializeCount);
        Assert.Equal(2, observedRecipients.Count);
        Assert.Contains(observedRecipients, r =>
            r.ClientId == clientA.ServerPeer.GameId
            && r.Entity == clientA.ServerEntity
            && r.EntityUuid == clientA.EntityUuid);
        Assert.Contains(observedRecipients, r =>
            r.ClientId == clientB.ServerPeer.GameId
            && r.Entity == clientB.ServerEntity
            && r.EntityUuid == clientB.EntityUuid);
    }

    [Fact]
    public async Task LocalPlayerKeepsLocallyBuiltColliderAcrossFullSyncAndDeltaReconcile()
    {
        using LoopbackHarness harness = LoopbackHarness.CreateWithDefaultRegistry();
        LoopbackClient clientA = await harness.AddCharacterClientAsync(Uuid.FromValue(501));
        LoopbackClient clientB = await harness.AddCharacterClientAsync(Uuid.FromValue(502));

        //  Build client A's local player with its own locally constructed collider and clear dirty.
        var clientStore = new DataStore();
        int localPlayer = clientStore.Alloc(clientA.EntityUuid);
        clientStore.AddOrUpdate(localPlayer, new PlayerComponent());
        clientStore.AddOrUpdate(localPlayer, new TransformComponent(Vector3.Zero, Quaternion.Identity, CharacterDefaults.PlayerScale));
        clientStore.AddOrUpdate(localPlayer, CharacterDefaults.CreateCharacterPhysicsComponent());
        ColliderComponent localCollider = CharacterDefaults.CreateCharacterColliderComponent(CharacterDefaults.PlayerScale);
        Shape[] originalShapes = localCollider.CompoundShape!.Value.Shapes;
        clientStore.AddOrUpdate(localPlayer, localCollider);
        clientStore.AddOrUpdate(localPlayer, new InputComponent());
        clientStore.AddOrUpdate(localPlayer, PendingInputComponent.Create());
        clientStore.ClearDirty<ColliderComponent>(localPlayer);

        ClientReconcileSystem reconcile = CreateClientReconcile(clientA.Inbound, harness.Registry);

        //  1. Full-sync join snapshot replicates client B's collider while leaving client A's untouched.
        harness.ClearAllDirty();
        harness.Replication.RequestFullSync(clientA.ServerPeer.GameId);
        harness.Replication.PublishStage(1f, harness.ServerStore);
        await clientA.WaitUntilSnapshotQueuedAsync();
        TickReconcileWhilePlaying(reconcile, clientStore);

        Assert.True(clientStore.TryGet(localPlayer, out ColliderComponent afterFullSync));
        Assert.True(afterFullSync.CompoundShape.HasValue);
        Assert.Same(originalShapes, afterFullSync.CompoundShape!.Value.Shapes);
        Assert.False(clientStore.IsDirty<ColliderComponent>(localPlayer));

        Assert.True(clientStore.TryGet(clientB.EntityUuid, out int remotePlayer));
        Assert.True(clientStore.TryGet(remotePlayer, out ColliderComponent remoteCollider));
        Assert.True(remoteCollider.CompoundShape.HasValue);

        //  2. Delta snapshot with updated colliders on both players still leaves local player's untouched.
        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, new TransformComponent(new Vector3(1f, 0f, 0f), Quaternion.Identity, CharacterDefaults.PlayerScale));
        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, CharacterDefaults.CreateCharacterColliderComponent(new Vector3(2f, 2f, 2f)));
        harness.ServerStore.AddOrUpdate(clientB.ServerEntity, CharacterDefaults.CreateCharacterColliderComponent(new Vector3(2f, 2f, 2f)));
        harness.Replication.PublishStage(1f, harness.ServerStore);
        await clientA.WaitUntilSnapshotQueuedAsync();
        TickReconcileWhilePlaying(reconcile, clientStore);

        Assert.True(clientStore.TryGet(localPlayer, out ColliderComponent afterDelta));
        Assert.True(afterDelta.CompoundShape.HasValue);
        Assert.Same(originalShapes, afterDelta.CompoundShape!.Value.Shapes);
        Assert.False(clientStore.IsDirty<ColliderComponent>(localPlayer));

        Assert.True(clientStore.TryGet(remotePlayer, out ColliderComponent updatedRemoteCollider));
        Assert.NotSame(remoteCollider.CompoundShape!.Value.Shapes, updatedRemoteCollider.CompoundShape!.Value.Shapes);

        //  3. Server-side removal of local player's collider still leaves the local player's collider intact.
        harness.ServerStore.AddOrUpdate(clientA.ServerEntity, new TransformComponent(new Vector3(2f, 0f, 0f), Quaternion.Identity, CharacterDefaults.PlayerScale));
        harness.ServerStore.Remove(typeof(ColliderComponent), clientA.ServerEntity);
        harness.Replication.PublishStage(1f, harness.ServerStore);
        await clientA.WaitUntilSnapshotQueuedAsync();
        TickReconcileWhilePlaying(reconcile, clientStore);

        Assert.True(clientStore.TryGet(localPlayer, out ColliderComponent afterRemoval));
        Assert.True(afterRemoval.CompoundShape.HasValue);
        Assert.Same(originalShapes, afterRemoval.CompoundShape!.Value.Shapes);
        Assert.False(clientStore.IsDirty<ColliderComponent>(localPlayer));
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
        public ulong ColliderTypeUuid { get; }

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
            Result<NetworkComponentInfo> colliderInfo = registry.GetInfo<ColliderComponent>();
            ColliderTypeUuid = colliderInfo.Success ? colliderInfo.Value.Uuid.ToValue() : 0UL;

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
