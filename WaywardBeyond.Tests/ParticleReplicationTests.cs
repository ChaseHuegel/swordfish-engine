#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using DryIoc;
using Krill;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NsdRpc;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.Particles;
using Swordfish.Library.Collections;
using Swordfish.Library.Events;
using Swordfish.Library.IO;
using Swordfish.Library.Types.Shapes;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Settings;
using Swordfish.Tests;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Levels;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Permissions;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Particles;
using WaywardBeyond.Server.Saves;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Skills;
using WaywardBeyond.Voxels;
using Xunit;
using GameState = WaywardBeyond.Client.GameState;
using WaywardBeyondGameState = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>Covers generic emitter replication, parent attachment, owner muting, and batched particle burst RPCs.</summary>
[Collection("ClientGameState")]
public sealed class ParticleReplicationTests
{
    [Fact]
    public void ParticleEmitterCodecRoundTripsSkipsUnchangedAndRejectsHostilePayloads()
    {
        var codec = new ParticleEmitterCodec();
        var store = new DataStore();
        int entity = store.Alloc();

        //  Serializing absent component fails cleanly.
        Result<byte[]> absent = codec.Serialize(store, entity);
        Assert.False(absent.Success);

        //  Serializing resolves EffectId from runtime Effect fallback when EffectId is null.
        ParticleEffect effect = CreateEffect("spark_stream", rate: 60f);
        var fromAsset = new ParticleEmitterComponent(effect, playing: true, rateScale: 1.5f, tintRgba: 0xE43B44FFu)
        {
            Offset = new Vector3(1f, 2f, 3f),
            LocalDirection = Vector3.UnitZ,
        };
        store.AddOrUpdate(entity, fromAsset);

        Result<byte[]> serialized = codec.Serialize(store, entity);
        Assert.True(serialized.Success, serialized.Message);

        var clientStore = new DataStore();
        int clientEntity = clientStore.Alloc();
        Result applied = codec.Apply(clientStore, clientEntity, serialized.Value);
        Assert.True(applied.Success, applied.Message);
        Assert.True(clientStore.TryGet(clientEntity, out ParticleEmitterComponent decoded));
        Assert.Null(decoded.Effect);
        Assert.Equal("spark_stream", decoded.EffectId);
        Assert.True(decoded.Playing);
        Assert.Equal(1.5f, decoded.RateScale);
        Assert.Equal(0xE43B44FFu, decoded.TintRgba);
        Assert.Equal(new Vector3(1f, 2f, 3f), decoded.Offset);
        Assert.Equal(Vector3.UnitZ, decoded.LocalDirection);

        //  Applying identical wire values leaves the component clean (no spurious dirty marking).
        clientStore.ClearDirty<ParticleEmitterComponent>(clientEntity);
        Result unchanged = codec.Apply(clientStore, clientEntity, serialized.Value);
        Assert.True(unchanged.Success);
        Assert.False(clientStore.IsDirty<ParticleEmitterComponent>(clientEntity));

        //  Hostile or invalid wire payloads are rejected without mutating client state.
        byte[][] invalidPayloads =
        [
            [],
            [0xFF, 0x01],
            new ParticleEmitterMessage { EffectId = "", Playing = true, RateScale = 1f }.Serialize(),
            new ParticleEmitterMessage { EffectId = "   ", Playing = true, RateScale = 1f }.Serialize(),
            new ParticleEmitterMessage { EffectId = new string('a', ParticleEmitterCodec.MAX_EFFECT_ID_LENGTH + 1), Playing = true, RateScale = 1f }.Serialize(),
            new ParticleEmitterMessage { EffectId = "valid", Playing = true, RateScale = -0.1f }.Serialize(),
            new ParticleEmitterMessage { EffectId = "valid", Playing = true, RateScale = float.NaN }.Serialize(),
            new ParticleEmitterMessage { EffectId = "valid", Playing = true, RateScale = float.PositiveInfinity }.Serialize(),
            new ParticleEmitterMessage { EffectId = "valid", Playing = true, RateScale = 1f, OffsetX = float.NaN }.Serialize(),
            new ParticleEmitterMessage { EffectId = "valid", Playing = true, RateScale = 1f, LocalDirectionZ = float.NegativeInfinity }.Serialize(),
        ];

        for (var i = 0; i < invalidPayloads.Length; i++)
        {
            Result rejected = codec.Apply(clientStore, clientEntity, invalidPayloads[i]);
            Assert.False(rejected.Success);
        }

        Assert.True(clientStore.TryGet(clientEntity, out ParticleEmitterComponent untouched));
        Assert.Equal("spark_stream", untouched.EffectId);
        Assert.False(clientStore.IsDirty<ParticleEmitterComponent>(clientEntity));

        //  Invalid server component is rejected at serialize time.
        store.AddOrUpdate(entity, new ParticleEmitterComponent("", playing: true));
        Assert.False(codec.Serialize(store, entity).Success);
    }

    [Fact]
    public async Task LoopbackReplicatesEmitterLifecycleUpdatesRemovalsFullSyncAndDespawns()
    {
        using var container = new Container();
        container.RegisterNetworking();
        var registry = container.Resolve<NetworkRegistry>();

        var serverStore = new DataStore();
        var peers = new ServerPeers();
        var sessions = new SessionManager();
        var replication = new NetworkReplicationSystem(peers, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingConfig(), registry);
        var particleService = new ServerParticleService(peers, sessions, replication, NullLogger<ServerParticleService>.Instance);

        var transport = new LoopbackRpcServerTransport();
        var serverRpcRegistry = new RpcRegistry();
        serverRpcRegistry.Freeze();
        var rpcServer = new RpcServer(transport, serverRpcRegistry);
        var host = new StubRpcHost();
        var connected = new TaskCompletionSource<RpcPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
        rpcServer.ClientConnected += peer => connected.TrySetResult(peer);
        rpcServer.Start();

        var connection = new LoopbackRpcClientConnection(transport);
        try
        {
            await connection.ConnectAsync();

            var inbound = new ClientInbound();
            var clientRpcRegistry = new RpcRegistry();
            clientRpcRegistry.AddService(new ClientHandlers(inbound));
            clientRpcRegistry.Freeze();
            _ = new RpcClient(connection, clientRpcRegistry);

            RpcPeer rpcPeer = await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
            ServerPeer serverPeer = peers.GetOrAdd(host, rpcPeer, isLocal: true);

            int serverPlayer = serverStore.Alloc(Uuid.FromValue(7001));
            sessions.Register(serverStore, serverPlayer, serverPeer.GameId, new Session(1));

            var clientStore = new DataStore();
            ClientReconcileSystem clientReconcile = CreateClientReconcile(inbound, registry);

            //  Invalid emitter requests are rejected safely.
            Assert.False(particleService.SpawnEmitter(serverStore, new ParticleEmitterComponent("", playing: true), Vector3.Zero).Success);
            Assert.False(particleService.SpawnEmitter(serverStore, new ParticleEmitterComponent("ok", playing: true), new Vector3(float.NaN, 0f, 0f)).Success);

            Result<int> spawned = particleService.SpawnEmitter(
                serverStore,
                new ParticleEmitterComponent("beacon", playing: true, rateScale: 1.25f, tintRgba: 0x00FF00FFu)
                {
                    Offset = new Vector3(0f, 0.5f, 0f),
                    LocalDirection = Vector3.UnitZ,
                },
                new Vector3(4f, 5f, -6f),
                parentEntity: Uuid.FromValue(7001),
                excludeEntity: Uuid.FromValue(7001)
            );
            Assert.True(spawned.Success, spawned.Message);
            int serverEmitter = spawned.Value;
            Uuid emitterUuid = serverStore.GetUuid(serverEmitter);

            //  Delta publish replicates emitter + transform + binding to client.
            replication.PublishStage(1f, serverStore);
            await WaitForSnapshotsAsync(inbound, expectedCount: 1);
            TickReconcileWhilePlaying(clientReconcile, clientStore);

            Assert.True(clientStore.TryGet(emitterUuid, out int clientEmitter));
            Assert.True(clientStore.TryGet(clientEmitter, out ParticleEmitterComponent replicated));
            Assert.Equal("beacon", replicated.EffectId);
            Assert.True(replicated.Playing);
            Assert.Equal(1.25f, replicated.RateScale);
            Assert.Equal(0x00FF00FFu, replicated.TintRgba);
            Assert.True(clientStore.TryGet(clientEmitter, out ParticleEmitterBindingComponent binding));
            Assert.Equal(7001UL, binding.ParentEntity);
            Assert.Equal(7001UL, binding.ExcludeEntity);

            //  Full-sync snapshot still carries the un-dirty emitter for late joiners.
            replication.RequestFullSync(serverPeer.GameId);
            clientStore.Free(clientEmitter);
            replication.PublishStage(1f, serverStore);
            await WaitForSnapshotsAsync(inbound, expectedCount: 1);
            TickReconcileWhilePlaying(clientReconcile, clientStore);
            Assert.True(clientStore.TryGet(emitterUuid, out clientEmitter));
            Assert.True(clientStore.TryGet(clientEmitter, out ParticleEmitterComponent fullSynced));
            Assert.Equal("beacon", fullSynced.EffectId);

            //  Removing ParticleEmitterBindingComponent replicates ComponentRemoval cleanly.
            serverStore.Remove(typeof(ParticleEmitterBindingComponent), serverEmitter);
            replication.PublishStage(1f, serverStore);
            await WaitForSnapshotsAsync(inbound, expectedCount: 1);
            TickReconcileWhilePlaying(clientReconcile, clientStore);
            Assert.False(clientStore.Has(typeof(ParticleEmitterBindingComponent), clientEmitter));
            Assert.True(clientStore.Has(typeof(ParticleEmitterComponent), clientEmitter));

            //  Despawn guard ignores wrong expectedUuid and frees cleanly when matching.
            particleService.DespawnEmitter(serverStore, serverEmitter, Uuid.FromValue(999999));
            Assert.Equal(emitterUuid, serverStore.GetUuid(serverEmitter));
            particleService.DespawnEmitter(serverStore, serverEmitter, emitterUuid);
            Assert.False(serverStore.TryGet(emitterUuid, out _));

            replication.PublishStage(1f, serverStore);
            await WaitForSnapshotsAsync(inbound, expectedCount: 1);
            TickReconcileWhilePlaying(clientReconcile, clientStore);
            Assert.False(clientStore.TryGet(emitterUuid, out _));
        }
        finally
        {
            connection.Close();
            rpcServer.Close();
            transport.Close();
        }
    }

    [Fact]
    public void ClientParticleReplicationSystemFollowsParentAndMutesWhileParentMissingOrInvalid()
    {
        var db = new StubAssetDatabase<ParticleEffect>();
        db.Assets["thruster"] = CreateEffect("thruster", rate: 100f, lifetimeMin: 2f, lifetimeMax: 2f);
        var renderSettings = new RenderSettings();
        var particleSystem = new ParticleSystem(db, renderSettings, new Random(3), NullLogger<ParticleSystem>.Instance);
        var childSystem = new ChildSystem();
        var inbound = new ClientInbound();
        var clientParticles = new ClientParticleReplicationSystem(inbound, particleSystem, NullLogger<ClientParticleReplicationSystem>.Instance);
        var store = new DataStore();

        Uuid parentUuid = Uuid.FromValue(500);
        Uuid localObserverUuid = Uuid.FromValue(501);
        Uuid remoteOwnerUuid = Uuid.FromValue(502);
        int localPlayer = store.Alloc(localObserverUuid);
        store.AddOrUpdate(localPlayer, new PlayerComponent());

        Uuid emitterUuid = Uuid.FromValue(600);
        int emitter = store.Alloc(emitterUuid);
        store.AddOrUpdate(emitter, new TransformComponent(Vector3.Zero));
        store.AddOrUpdate(emitter, new ParticleEmitterComponent("thruster", playing: true)
        {
            Offset = new Vector3(0f, 0f, 1f),
            LocalDirection = Vector3.UnitZ,
        });
        store.AddOrUpdate(emitter, new ParticleEmitterBindingComponent
        {
            ParentEntity = parentUuid.ToValue(),
            ExcludeEntity = remoteOwnerUuid.ToValue(),
        });

        GameState previous = WaywardBeyondGameState.GameState.Get();
        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            //  1. Missing parent mutes emitter without spawning particles even though Playing == true.
            clientParticles.Tick(0.016f, store);
            Assert.True(store.Has(typeof(ParticleEmitterMutedComponent), emitter));
            particleSystem.Tick(0.05f, store);
            Assert.Equal(0, particleSystem.ActiveParticleCount);

            //  2. Spawning parent with non-finite transform keeps emitter muted; valid transform unmutes and attaches ChildComponent.
            int parent = store.Alloc(parentUuid);
            store.AddOrUpdate(parent, new TransformComponent(new Vector3(float.NaN, 0f, 0f), Quaternion.Identity, Vector3.One));
            clientParticles.Tick(0.016f, store);
            Assert.True(store.Has(typeof(ParticleEmitterMutedComponent), emitter));

            Quaternion rotYaw90 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);
            store.AddOrUpdate(parent, new TransformComponent(new Vector3(10f, 0f, 0f), rotYaw90, Vector3.One));
            clientParticles.Tick(0.016f, store);

            Assert.False(store.Has(typeof(ParticleEmitterMutedComponent), emitter));
            Assert.True(store.TryGet(emitter, out ChildComponent child));
            Assert.Equal(parentUuid, child.Parent);

            //  ChildSystem + ParticleSystem emit from rotated parent offset (10,0,0) + Yaw90*(0,0,1) = (11,0,0).
            childSystem.Tick(0.05f, store);
            particleSystem.Tick(0.05f, store);
            Assert.Equal(5, particleSystem.ActiveParticleCount);
            ParticleRenderSnapshot snap = particleSystem.AcquireRenderSnapshot();
            Assert.Equal(11f, snap.Instances[0].Position.X, precision: 2);

            //  3. Moving parent updates ChildSystem -> emitter without touching emitter on server.
            store.AddOrUpdate(parent, new TransformComponent(new Vector3(20f, 0f, 0f), rotYaw90, Vector3.One));
            childSystem.Tick(0f, store);
            particleSystem.Clear();
            particleSystem.Tick(0.05f, store);
            ParticleRenderSnapshot movedSnap = particleSystem.AcquireRenderSnapshot();
            Assert.Equal(5, movedSnap.InstanceCount);
            for (var i = 0; i < movedSnap.InstanceCount; i++)
            {
                Assert.Equal(21f, movedSnap.Instances[i].Position.X, precision: 2);
            }
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(previous);
        }
    }

    [Fact]
    public void ClientParticleReplicationSystemSuppressesLocalExcludeDetachesClearedParentAndSweepsRemovedBindings()
    {
        var db = new StubAssetDatabase<ParticleEffect>();
        db.Assets["thruster"] = CreateEffect("thruster", rate: 100f, lifetimeMin: 2f, lifetimeMax: 2f);
        var particleSystem = new ParticleSystem(db, new RenderSettings(), new Random(5));
        var childSystem = new ChildSystem();
        var inbound = new ClientInbound();
        var clientParticles = new ClientParticleReplicationSystem(inbound, particleSystem, NullLogger<ClientParticleReplicationSystem>.Instance);
        var store = new DataStore();

        Uuid localUuid = Uuid.FromValue(8001);
        Uuid remoteUuid = Uuid.FromValue(8002);
        int localPlayer = store.Alloc(localUuid);
        store.AddOrUpdate(localPlayer, new PlayerComponent());
        store.AddOrUpdate(localPlayer, new TransformComponent(new Vector3(5f, 0f, 0f)));

        int remotePlayer = store.Alloc(remoteUuid);
        store.AddOrUpdate(remotePlayer, new TransformComponent(new Vector3(15f, 0f, 0f)));

        int excludedEmitter = store.Alloc(Uuid.FromValue(8101));
        store.AddOrUpdate(excludedEmitter, new TransformComponent(Vector3.Zero));
        store.AddOrUpdate(excludedEmitter, new ParticleEmitterComponent("thruster", playing: true));
        store.AddOrUpdate(excludedEmitter, new ParticleEmitterBindingComponent
        {
            ParentEntity = localUuid.ToValue(),
            ExcludeEntity = localUuid.ToValue(),
        });

        int visibleEmitter = store.Alloc(Uuid.FromValue(8102));
        store.AddOrUpdate(visibleEmitter, new TransformComponent(Vector3.Zero));
        store.AddOrUpdate(visibleEmitter, new ParticleEmitterComponent("thruster", playing: true));
        store.AddOrUpdate(visibleEmitter, new ParticleEmitterBindingComponent
        {
            ParentEntity = remoteUuid.ToValue(),
            ExcludeEntity = remoteUuid.ToValue(),
        });

        //  Self-parented emitter is also muted and never adds ChildComponent pointing to itself.
        Uuid selfUuid = Uuid.FromValue(8103);
        int selfParentEmitter = store.Alloc(selfUuid);
        store.AddOrUpdate(selfParentEmitter, new TransformComponent(Vector3.Zero));
        store.AddOrUpdate(selfParentEmitter, new ParticleEmitterComponent("thruster", playing: true));
        store.AddOrUpdate(selfParentEmitter, new ParticleEmitterBindingComponent
        {
            ParentEntity = selfUuid.ToValue(),
        });

        GameState previous = WaywardBeyondGameState.GameState.Get();
        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            clientParticles.Tick(0.016f, store);
            Assert.True(store.Has(typeof(ParticleEmitterMutedComponent), excludedEmitter));
            Assert.True(store.TryGet(excludedEmitter, out ParticleEmitterComponent stillPlaying) && stillPlaying.Playing);

            Assert.False(store.Has(typeof(ParticleEmitterMutedComponent), visibleEmitter));
            Assert.True(store.TryGet(visibleEmitter, out ChildComponent remoteChild));
            Assert.Equal(remoteUuid, remoteChild.Parent);

            Assert.True(store.Has(typeof(ParticleEmitterMutedComponent), selfParentEmitter));
            Assert.False(store.Has(typeof(ChildComponent), selfParentEmitter));

            childSystem.Tick(0.05f, store);
            particleSystem.Tick(0.05f, store);
            Assert.Equal(5, particleSystem.ActiveParticleCount);
            ParticleRenderSnapshot snap = particleSystem.AcquireRenderSnapshot();
            for (var i = 0; i < snap.InstanceCount; i++)
            {
                Assert.Equal(15f, snap.Instances[i].Position.X, precision: 2);
            }

            //  Clearing ParentEntity detaches ChildComponent; removing ParticleEmitterBindingComponent unmutes excludedEmitter.
            store.AddOrUpdate(visibleEmitter, new ParticleEmitterBindingComponent
            {
                ParentEntity = 0UL,
                ExcludeEntity = remoteUuid.ToValue(),
            });
            store.Remove(typeof(ParticleEmitterBindingComponent), excludedEmitter);
            clientParticles.Tick(0.016f, store);

            Assert.False(store.Has(typeof(ChildComponent), visibleEmitter));
            Assert.False(store.Has(typeof(ParticleEmitterMutedComponent), excludedEmitter));
            Assert.False(store.Has(typeof(ChildComponent), excludedEmitter));
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(previous);
        }
    }

    [Fact]
    public async Task LoopbackBatchesParticleBurstsCapsPerTickFiltersExcludeAndPropagatesLinearTint()
    {
        var db = new StubAssetDatabase<ParticleEffect>();
        db.Assets["spark"] = CreateEffect(
            id: "spark",
            rate: 0f,
            burstCount: 4,
            maxParticles: 64,
            lifetimeMin: 2f,
            lifetimeMax: 2f,
            speedMin: 0f,
            speedMax: 0f,
            startColor: Vector4.One
        );

        var serverStore = new DataStore();
        var peers = new ServerPeers();
        var sessions = new SessionManager();
        var replication = new NetworkReplicationSystem(peers, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingConfig(), new NetworkRegistry());
        var particleService = new ServerParticleService(peers, sessions, replication, NullLogger<ServerParticleService>.Instance);
        var flushSystem = new ServerParticleFlushSystem(particleService);

        var transport = new LoopbackRpcServerTransport();
        var serverRpcRegistry = new RpcRegistry();
        serverRpcRegistry.Freeze();
        var rpcServer = new RpcServer(transport, serverRpcRegistry);
        var host = new StubRpcHost();
        var connectedPeers = new List<RpcPeer>();
        var peerLock = new object();
        rpcServer.ClientConnected += peer =>
        {
            lock (peerLock)
            {
                connectedPeers.Add(peer);
            }
        };
        rpcServer.Start();

        var connA = new LoopbackRpcClientConnection(transport);
        var connB = new LoopbackRpcClientConnection(transport);
        try
        {
            await connA.ConnectAsync();
            await connB.ConnectAsync();

            var inboundA = new ClientInbound();
            var regA = new RpcRegistry();
            regA.AddService(new ClientHandlers(inboundA));
            regA.Freeze();
            _ = new RpcClient(connA, regA);

            var inboundB = new ClientInbound();
            var regB = new RpcRegistry();
            regB.AddService(new ClientHandlers(inboundB));
            regB.Freeze();
            _ = new RpcClient(connB, regB);

            await WaitForAsync(() =>
            {
                lock (peerLock)
                {
                    return connectedPeers.Count == 2;
                }
            });

            RpcPeer rpcPeerA;
            RpcPeer rpcPeerB;
            lock (peerLock)
            {
                rpcPeerA = connectedPeers[0];
                rpcPeerB = connectedPeers[1];
            }

            ServerPeer peerA = peers.GetOrAdd(host, rpcPeerA, isLocal: true);
            ServerPeer peerB = peers.GetOrAdd(host, rpcPeerB, isLocal: false);

            int entityA = serverStore.Alloc(Uuid.FromValue(1));
            int entityB = serverStore.Alloc(Uuid.FromValue(2));
            sessions.Register(serverStore, entityA, peerA.GameId, new Session(1));
            sessions.Register(serverStore, entityB, peerB.GameId, new Session(2));

            //  Validate rejected inputs on EmitBurst.
            Assert.False(particleService.EmitBurst("", Vector3.Zero, Vector3.UnitY).Success);
            Assert.False(particleService.EmitBurst(new string('x', ParticleEmitterCodec.MAX_EFFECT_ID_LENGTH + 1), Vector3.Zero, Vector3.UnitY).Success);
            Assert.False(particleService.EmitBurst("spark", new Vector3(float.NaN, 0f, 0f), Vector3.UnitY).Success);
            Assert.False(particleService.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, speedScale: -1f).Success);
            Assert.False(particleService.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, sizeScale: 0f).Success);
            Assert.False(particleService.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, count: 0).Success);

            //  1. Multiple bursts coalesce into one batch RPC; streaming peerA is skipped while peerB receives it.
            replication.BeginStream(peerA.GameId);
            uint magentaTint = ParticleEffect.PackTint(new Vector3(1f, 0f, 0.5f), alpha: 1f);
            Assert.True(particleService.EmitBurst("spark", new Vector3(1f, 2f, 3f), Vector3.UnitY, count: 2, tintRgba: magentaTint).Success);
            Assert.True(particleService.EmitBurst("unknown_mod_burst", new Vector3(4f, 5f, 6f), Vector3.UnitY, count: 1).Success);
            flushSystem.Tick(0.016f, serverStore);

            Assert.True(await WaitForAsync(() => inboundB.ParticleBursts.Count >= 1));
            replication.CancelStream(peerA.GameId);
            Assert.True(particleService.EmitBurst("sentinel_stream_end", Vector3.Zero, Vector3.UnitY, count: 1, excludeClient: peerB.GameId).Success);
            flushSystem.Tick(0.016f, serverStore);
            Assert.True(await WaitForAsync(() => inboundA.ParticleBursts.Count >= 1));
            ParticleBurstBatchMessage sentinelStreamBatch = Assert.Single(inboundA.ParticleBursts);
            Assert.Equal("sentinel_stream_end", Assert.Single(sentinelStreamBatch.Bursts).EffectId);
            inboundA.ParticleBursts.Clear();

            ParticleBurstBatchMessage batchB1 = Assert.Single(inboundB.ParticleBursts);
            Assert.Equal(2, batchB1.Bursts.Length);

            //  Check client drops bursts when GameState < Playing.
            var clientParticlesA = new ParticleSystem(db, new RenderSettings(), new Random(1));
            var clientRepA = new ClientParticleReplicationSystem(inboundA, clientParticlesA, NullLogger<ClientParticleReplicationSystem>.Instance);
            var clientParticlesB = new ParticleSystem(db, new RenderSettings(), new Random(1));
            var clientRepB = new ClientParticleReplicationSystem(inboundB, clientParticlesB, NullLogger<ClientParticleReplicationSystem>.Instance);
            var clientStoreB = new DataStore();

            GameState previous = WaywardBeyondGameState.GameState.Get();
            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            try
            {
                //  Pre-play tick drops inbound bursts without emitting particles.
                inboundA.ParticleBursts.Enqueue(batchB1);
                clientRepA.Tick(0.016f, new DataStore());
                clientParticlesA.Tick(0.016f, new DataStore());
                Assert.Equal(0, clientParticlesA.ActiveParticleCount);

                WaywardBeyondGameState.GameState.Set(GameState.Playing);
                clientRepB.Tick(0.016f, clientStoreB);
                clientParticlesB.Tick(0.016f, clientStoreB);
                Assert.Equal(2, clientParticlesB.ActiveParticleCount);

                ParticleRenderSnapshot tintSnap = clientParticlesB.AcquireRenderSnapshot();
                Vector4 expectedLinear = ParticleEffect.UnpackTintLinear(magentaTint);
                Assert.Equal(expectedLinear.X, tintSnap.Instances[0].Color.X, precision: 3);
                Assert.Equal(expectedLinear.Y, tintSnap.Instances[0].Color.Y, precision: 3);
                Assert.Equal(expectedLinear.Z, tintSnap.Instances[0].Color.Z, precision: 3);

                //  Hostile wire burst entries are skipped cleanly without crashing or emitting.
                clientParticlesB.Clear();
                clientParticlesB.Tick(0.016f, clientStoreB);
                inboundB.ParticleBursts.Enqueue(new ParticleBurstBatchMessage
                {
                    Bursts =
                    [
                        new ParticleBurstEntry { EffectId = "", PositionX = 0f, DirectionY = 1f, SpeedScale = 1f, SizeScale = 1f },
                        new ParticleBurstEntry { EffectId = "spark", PositionX = float.NaN, DirectionY = 1f, SpeedScale = 1f, SizeScale = 1f },
                        new ParticleBurstEntry { EffectId = "spark", DirectionY = 1f, SpeedScale = -2f, SizeScale = 1f },
                        new ParticleBurstEntry { EffectId = "spark", DirectionY = 1f, SpeedScale = 1f, SizeScale = 0f },
                        new ParticleBurstEntry { EffectId = "spark", DirectionY = 1f, Count = 0, SpeedScale = 1f, SizeScale = 1f },
                    ],
                });
                clientRepB.Tick(0.016f, clientStoreB);
                clientParticlesB.Tick(0.016f, clientStoreB);
                Assert.Equal(0, clientParticlesB.ActiveParticleCount);

                //  2. ExcludeClient skips peerA when all bursts exclude peerA while delivering to peerB.
                inboundA.ParticleBursts.Clear();
                inboundB.ParticleBursts.Clear();
                Assert.True(particleService.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, count: 1, excludeClient: peerA.GameId).Success);
                flushSystem.Tick(0.016f, serverStore);

                await WaitForAsync(() => inboundB.ParticleBursts.Count >= 1);
                Assert.True(particleService.EmitBurst("sentinel_after_exclude", Vector3.Zero, Vector3.UnitY, count: 1, excludeClient: peerB.GameId).Success);
                flushSystem.Tick(0.016f, serverStore);
                Assert.True(await WaitForAsync(() => inboundA.ParticleBursts.Count >= 1));
                ParticleBurstBatchMessage sentinelExcludeBatch = Assert.Single(inboundA.ParticleBursts);
                Assert.Equal("sentinel_after_exclude", Assert.Single(sentinelExcludeBatch.Bursts).EffectId);
                Assert.Single(inboundB.ParticleBursts);
                inboundA.ParticleBursts.Clear();
                inboundB.ParticleBursts.Clear();

                //  Exercise reusing the filtered batch buffer across varying filtered entry counts.
                Assert.True(particleService.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, count: 1, excludeClient: peerA.GameId).Success);
                Assert.True(particleService.EmitBurst("spark", Vector3.One, Vector3.UnitY, count: 2, excludeClient: peerA.GameId).Success);
                flushSystem.Tick(0.016f, serverStore);
                Assert.True(await WaitForAsync(() => inboundB.ParticleBursts.Count >= 1));
                ParticleBurstBatchMessage multiFilteredBatch = Assert.Single(inboundB.ParticleBursts);
                Assert.Equal(2, multiFilteredBatch.Bursts.Length);
                inboundB.ParticleBursts.Clear();

                //  3. MAX_BURSTS_PER_TICK cap drops excess cleanly and resets on next tick.
                for (var i = 0; i < ServerParticleService.MAX_BURSTS_PER_TICK; i++)
                {
                    Assert.True(particleService.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, count: 1).Success);
                }

                Assert.False(particleService.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, count: 1).Success);
                flushSystem.Tick(0.016f, serverStore);

                await WaitForAsync(() => inboundA.ParticleBursts.Count >= 1 && inboundB.ParticleBursts.Count >= 1);
                ParticleBurstBatchMessage cappedBatch = Assert.Single(inboundA.ParticleBursts);
                Assert.Equal(ServerParticleService.MAX_BURSTS_PER_TICK, cappedBatch.Bursts.Length);

                //  Next tick accepts bursts again.
                Assert.True(particleService.EmitBurst("spark", Vector3.Zero, Vector3.UnitY, count: 1).Success);
                flushSystem.Tick(0.016f, serverStore);
            }
            finally
            {
                WaywardBeyondGameState.GameState.Set(previous);
            }
        }
        finally
        {
            connA.Close();
            connB.Close();
            rpcServer.Close();
            transport.Close();
        }
    }

    [Fact]
    public void ServerCompositionPinsDistinctIServerParticleServicePerServerWorld()
    {
        using var rootContainer = new Container();
        rootContainer.RegisterInstance<IContainer>(rootContainer);
        rootContainer.RegisterInstance<ILoggerFactory>(NullLoggerFactory.Instance);
        rootContainer.RegisterInstance<ILogger>(NullLogger.Instance);
        rootContainer.Register(typeof(ILogger<>), typeof(NullLogger<>), Reuse.Singleton);
        rootContainer.RegisterInstance(new NetworkingConfig());
        rootContainer.RegisterInstance(new GameplayConfig());
        rootContainer.RegisterInstance(new LevelGeneratorConfig());
        rootContainer.RegisterInstance(new PhysicsSettings());
        rootContainer.RegisterInstance(new StorageConfig());
        rootContainer.RegisterInstance(AppPaths.ForWorkingDirectory());
        rootContainer.RegisterDelegate<ILevelCatalog>(_ => new FakeLevelCatalog(), Reuse.Singleton);
        rootContainer.RegisterDelegate<IBrickRegistry>(_ => BrickRegistry.FromNames(["test"]), Reuse.Singleton);
        rootContainer.RegisterDelegate<ISkillDatabase>(_ => new FakeSkillDatabase(), Reuse.Singleton);
        rootContainer.RegisterDelegate<IItemDatabase>(_ => new FakeItemDatabase(), Reuse.Singleton);
        rootContainer.RegisterDelegate<IBrickDatabase>(_ => new FakeBrickDatabase(), Reuse.Singleton);
        rootContainer.RegisterDelegate<PermissionPolicy>(_ => PermissionPolicy.Create([]), Reuse.Singleton);
        new ServerModule().Inject(rootContainer);

        using IContainer childA = rootContainer.CreateChild(RegistrySharing.CloneAndDropCache, null, null, null, withDisposables: false);
        using IContainer childB = rootContainer.CreateChild(RegistrySharing.CloneAndDropCache, null, null, null, withDisposables: false);
        using var worldA = new ServerWorld(childA);
        using var worldB = new ServerWorld(childB);

        Assert.NotNull(worldA.Particles);
        Assert.NotNull(worldB.Particles);
        Assert.Same(worldA.Particles, childA.Resolve<IServerParticleService>());
        Assert.Same(worldA.Particles, childA.Resolve<ServerParticleService>());
        Assert.Same(worldB.Particles, childB.Resolve<IServerParticleService>());
        Assert.NotSame(worldA.Particles, worldB.Particles);
    }

    [Fact]
    public void ClientParticleReplicationSystemContinuesDuringPauseAndCleansUpBelowPlaying()
    {
        var db = new StubAssetDatabase<ParticleEffect>();
        db.Assets["thruster"] = CreateEffect("thruster", rate: 100f, lifetimeMin: 2f, lifetimeMax: 2f);
        db.Assets["spark"] = CreateEffect("spark", rate: 0f, burstCount: 3, lifetimeMin: 2f, lifetimeMax: 2f);
        var particleSystem = new ParticleSystem(db, new RenderSettings(), new Random(11));
        var childSystem = new ChildSystem();
        var inbound = new ClientInbound();
        var clientParticles = new ClientParticleReplicationSystem(inbound, particleSystem, NullLogger<ClientParticleReplicationSystem>.Instance);
        var store = new DataStore();

        Uuid localUuid = Uuid.FromValue(9001);
        Uuid remoteUuid = Uuid.FromValue(9002);
        int localPlayer = store.Alloc(localUuid);
        store.AddOrUpdate(localPlayer, new PlayerComponent());
        store.AddOrUpdate(localPlayer, new TransformComponent(Vector3.Zero));

        int remotePlayer = store.Alloc(remoteUuid);
        store.AddOrUpdate(remotePlayer, new TransformComponent(new Vector3(10f, 0f, 0f)));

        int visibleEmitter = store.Alloc(Uuid.FromValue(9011));
        store.AddOrUpdate(visibleEmitter, new TransformComponent(Vector3.Zero));
        store.AddOrUpdate(visibleEmitter, new ParticleEmitterComponent("thruster", playing: true));
        var visibleBinding = new ParticleEmitterBindingComponent
        {
            ParentEntity = remoteUuid.ToValue(),
            ExcludeEntity = remoteUuid.ToValue(),
        };
        store.AddOrUpdate(visibleEmitter, visibleBinding);

        int excludedEmitter = store.Alloc(Uuid.FromValue(9012));
        store.AddOrUpdate(excludedEmitter, new TransformComponent(Vector3.Zero));
        store.AddOrUpdate(excludedEmitter, new ParticleEmitterComponent("thruster", playing: true));
        var excludedBinding = new ParticleEmitterBindingComponent
        {
            ParentEntity = localUuid.ToValue(),
            ExcludeEntity = localUuid.ToValue(),
        };
        store.AddOrUpdate(excludedEmitter, excludedBinding);

        GameState previous = WaywardBeyondGameState.GameState.Get();
        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            clientParticles.Tick(0.016f, store);
            Assert.True(store.TryGet(visibleEmitter, out ChildComponent _));
            Assert.True(store.Has(typeof(ParticleEmitterMutedComponent), excludedEmitter));

            //  Switching to Paused must continue draining bursts and syncing binding transitions.
            WaywardBeyondGameState.GameState.Set(GameState.Paused);
            store.AddOrUpdate(remotePlayer, new TransformComponent(new Vector3(30f, 0f, 0f)));
            inbound.ParticleBursts.Enqueue(new ParticleBurstBatchMessage
            {
                Bursts =
                [
                    new ParticleBurstEntry
                    {
                        EffectId = "spark",
                        PositionX = 2f,
                        PositionY = 3f,
                        PositionZ = 4f,
                        DirectionY = 1f,
                        Count = 3,
                        SpeedScale = 0f,
                        SizeScale = 1f,
                    },
                ],
            });

            clientParticles.Tick(0.016f, store);
            childSystem.Tick(0.05f, store);
            particleSystem.Tick(0.05f, store);

            ParticleRenderSnapshot pauseSnap = particleSystem.AcquireRenderSnapshot();
            Assert.Contains(pauseSnap.Instances[..pauseSnap.InstanceCount], p => MathF.Abs(p.Position.X - 2f) < 0.01f);
            Assert.Contains(pauseSnap.Instances[..pauseSnap.InstanceCount], p => MathF.Abs(p.Position.X - 30f) < 0.01f);

            //  Clearing ParentEntity and removing a binding during Paused cleanly detaches/unmutes.
            store.AddOrUpdate(visibleEmitter, new ParticleEmitterBindingComponent
            {
                ParentEntity = 0UL,
                ExcludeEntity = remoteUuid.ToValue(),
            });
            store.Remove(typeof(ParticleEmitterBindingComponent), excludedEmitter);
            clientParticles.Tick(0.016f, store);
            Assert.False(store.Has(typeof(ChildComponent), visibleEmitter));
            Assert.False(store.Has(typeof(ParticleEmitterMutedComponent), excludedEmitter));
            Assert.False(store.Has(typeof(ChildComponent), excludedEmitter));

            //  Re-attach, then leave play (< Playing): managed components must not leak.
            store.AddOrUpdate(visibleEmitter, visibleBinding);
            store.AddOrUpdate(excludedEmitter, excludedBinding);
            clientParticles.Tick(0.016f, store);
            Assert.True(store.Has(typeof(ChildComponent), visibleEmitter));
            Assert.True(store.Has(typeof(ParticleEmitterMutedComponent), excludedEmitter));

            WaywardBeyondGameState.GameState.Set(GameState.Loading);
            clientParticles.Tick(0.016f, store);
            Assert.False(store.Has(typeof(ChildComponent), visibleEmitter));
            Assert.False(store.Has(typeof(ChildComponent), excludedEmitter));
            Assert.False(store.Has(typeof(ParticleEmitterMutedComponent), excludedEmitter));
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(previous);
        }
    }

    [Fact]
    public void ClientReconcileSystemEnteringPlayCoalescePreservesStationaryEmitterTransform()
    {
        using var container = new Container();
        container.RegisterNetworking();
        var registry = container.Resolve<NetworkRegistry>();

        var inbound = new ClientInbound();
        ClientReconcileSystem reconcile = CreateClientReconcile(inbound, registry);
        var store = new DataStore();

        Uuid playerUuid = Uuid.FromValue(9101);
        int localPlayer = store.Alloc(playerUuid);
        var seededPlayerPos = new Vector3(1f, 2f, 3f);
        store.AddOrUpdate(localPlayer, new PlayerComponent());
        store.AddOrUpdate(localPlayer, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Discrete));
        store.AddOrUpdate(localPlayer, new TransformComponent(seededPlayerPos, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(localPlayer, new InputComponent());
        store.AddOrUpdate(localPlayer, PendingInputComponent.Create());

        Uuid emitterUuid = Uuid.FromValue(9102);
        var emitterPos = new Vector3(12f, 4f, -8f);
        Uuid remoteUuid = Uuid.FromValue(9103);

        var tempStore = new DataStore();
        int tempEmitter = tempStore.Alloc();
        tempStore.AddOrUpdate(tempEmitter, new ParticleEmitterComponent("beacon", playing: true));
        Result<byte[]> emitterPayload = new ParticleEmitterCodec().Serialize(tempStore, tempEmitter);
        Assert.True(emitterPayload.Success, emitterPayload.Message);

        int tempBody = tempStore.Alloc();
        tempStore.AddOrUpdate(tempBody, new BodyViewComponent { Body = "wb:human_m" });
        Result<byte[]> bodyPayload = new NsdComponentCodec<BodyViewComponent>().Serialize(tempStore, tempBody);
        Assert.True(bodyPayload.Success, bodyPayload.Message);

        var transformCodec = new TransformCodec();
        byte[] emitterTransformPayload = transformCodec.Serialize(new TransformComponent(emitterPos, Quaternion.Identity, Vector3.One));
        byte[] stalePlayerTransformPayload = transformCodec.Serialize(new TransformComponent(new Vector3(99f, 99f, 99f), Quaternion.Identity, Vector3.One));
        byte[] remoteStartPayload = transformCodec.Serialize(new TransformComponent(new Vector3(1f, 0f, 0f), Quaternion.Identity, Vector3.One));
        byte[] remoteNextPayload = transformCodec.Serialize(new TransformComponent(new Vector3(5f, 0f, 0f), Quaternion.Identity, Vector3.One));

        ulong transformTypeId = Uuid.FromValue(2).ToValue();
        ulong bodyTypeId = Uuid.FromValue(10).ToValue();
        ulong emitterTypeId = Uuid.FromValue(19).ToValue();

        GameState previous = WaywardBeyondGameState.GameState.Get();
        try
        {
            WaywardBeyondGameState.GameState.Set(GameState.Loading);
            reconcile.Tick(1f / 128f, store);

            //  Full-sync snapshot carries stationary world-space emitter, then a delta omits it.
            inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(new WorldSnapshot
            {
                TickNumber = 1,
                LastProcessedInput = 0,
                Components =
                [
                    new ComponentSnapshot(emitterUuid.ToValue(), transformTypeId, emitterTransformPayload),
                    new ComponentSnapshot(emitterUuid.ToValue(), emitterTypeId, emitterPayload.Value),
                    new ComponentSnapshot(playerUuid.ToValue(), transformTypeId, stalePlayerTransformPayload),
                    new ComponentSnapshot(remoteUuid.ToValue(), transformTypeId, remoteStartPayload),
                    new ComponentSnapshot(remoteUuid.ToValue(), bodyTypeId, bodyPayload.Value),
                ],
                RemovedEntities = [],
            }));
            inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(new WorldSnapshot
            {
                TickNumber = 2,
                LastProcessedInput = 0,
                Components =
                [
                    new ComponentSnapshot(remoteUuid.ToValue(), transformTypeId, remoteNextPayload),
                ],
                RemovedEntities = [],
            }));

            WaywardBeyondGameState.GameState.Set(GameState.Playing);
            reconcile.Tick(1f / 128f, store);

            Assert.True(store.TryGet(emitterUuid, out int clientEmitter));
            Assert.True(store.TryGet(clientEmitter, out ParticleEmitterComponent replicatedEmitter));
            Assert.Equal("beacon", replicatedEmitter.EffectId);
            Assert.True(store.TryGet(clientEmitter, out TransformComponent emitterTransform));
            Assert.Equal(emitterPos, emitterTransform.Position);

            //  Verify ParticleSystem emits from the preserved world-space transform.
            var db = new StubAssetDatabase<ParticleEffect>();
            db.Assets["beacon"] = CreateEffect("beacon", rate: 100f, lifetimeMin: 2f, lifetimeMax: 2f);
            var particleSystem = new ParticleSystem(db, new RenderSettings(), new Random(13));
            particleSystem.Tick(0.05f, store);
            Assert.Equal(5, particleSystem.ActiveParticleCount);
            ParticleRenderSnapshot snap = particleSystem.AcquireRenderSnapshot();
            Assert.Equal(emitterPos, snap.Instances[0].Position);

            //  Newest delta wins without blending remote start position; local player keeps seeded spawn.
            Assert.True(store.TryGet(remoteUuid, out int remoteEntity));
            Assert.True(store.TryGet(remoteEntity, out TransformComponent remoteTransform));
            Assert.Equal(new Vector3(5f, 0f, 0f), remoteTransform.Position);
            Assert.True(store.TryGet(localPlayer, out TransformComponent playerTransform));
            Assert.Equal(seededPlayerPos, playerTransform.Position);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(previous);
        }
    }

    [Fact]
    public async Task NetworkReplicationSystemClearsOversizedInboundInputPaintId()
    {
        using var container = new Container();
        container.RegisterNetworking();
        var registry = container.Resolve<NetworkRegistry>();

        var serverStore = new DataStore();
        var peers = new ServerPeers();
        var sessions = new SessionManager();
        var replication = new NetworkReplicationSystem(peers, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingConfig(), registry);

        var transport = new LoopbackRpcServerTransport();
        var serverRpcRegistry = new RpcRegistry();
        serverRpcRegistry.Freeze();
        var rpcServer = new RpcServer(transport, serverRpcRegistry);
        var host = new StubRpcHost();
        var connected = new TaskCompletionSource<RpcPeer>(TaskCreationOptions.RunContinuationsAsynchronously);
        rpcServer.ClientConnected += peer => connected.TrySetResult(peer);
        rpcServer.Start();

        var connection = new LoopbackRpcClientConnection(transport);
        try
        {
            await connection.ConnectAsync();
            var clientRpcRegistry = new RpcRegistry();
            clientRpcRegistry.Freeze();
            _ = new RpcClient(connection, clientRpcRegistry);

            RpcPeer rpcPeer = await connected.Task.WaitAsync(TimeSpan.FromSeconds(5));
            ServerPeer serverPeer = peers.GetOrAdd(host, rpcPeer, isLocal: false);

            Uuid playerUuid = Uuid.FromValue(9201);
            int serverPlayer = serverStore.Alloc(playerUuid);
            serverStore.AddOrUpdate(serverPlayer, NetworkComponent.Create());
            sessions.Register(serverStore, serverPlayer, serverPeer.GameId, new Session(1));

            var codec = new NsdComponentCodec<InputComponent>();
            var tempStore = new DataStore();
            int tempPlayer = tempStore.Alloc();

            tempStore.AddOrUpdate(tempPlayer, new InputComponent
            {
                MovementX = 0.5f,
                SequenceNumber = 1,
                ServerTickAtSample = 4,
                PrimaryHeld = true,
                PaintId = new string('p', 129),
            });
            Result<byte[]> oversizedPayload = codec.Serialize(tempStore, tempPlayer);
            Assert.True(oversizedPayload.Success);

            serverPeer.Snapshots.Enqueue(new WorldSnapshot
            {
                TickNumber = 4,
                Components = [new ComponentSnapshot(playerUuid.ToValue(), Uuid.FromValue(1).ToValue(), oversizedPayload.Value)],
            });

            replication.ApplyStage(1f / 64f, serverStore);

            Assert.True(serverStore.TryGet(serverPlayer, out InputComponent clearedInput));
            Assert.Null(clearedInput.PaintId);
            Assert.True(clearedInput.PrimaryHeld);
            Assert.Equal(0.5f, clearedInput.MovementX);
            Assert.True(serverStore.TryGet(serverPlayer, out NetworkComponent net));
            Result<InputComponent> staged = net.StagedInputs!.GetCommand(4);
            Assert.True(staged.Success);
            Assert.Null(staged.Value.PaintId);
            Assert.True(staged.Value.PrimaryHeld);

            //  Within-limit PaintId passes through intact.
            tempStore.AddOrUpdate(tempPlayer, new InputComponent
            {
                SequenceNumber = 2,
                ServerTickAtSample = 5,
                PrimaryHeld = true,
                PaintId = "wb:red",
            });
            Result<byte[]> validPayload = codec.Serialize(tempStore, tempPlayer);
            Assert.True(validPayload.Success);
            serverPeer.Snapshots.Enqueue(new WorldSnapshot
            {
                TickNumber = 5,
                Components = [new ComponentSnapshot(playerUuid.ToValue(), Uuid.FromValue(1).ToValue(), validPayload.Value)],
            });

            replication.ApplyStage(1f / 64f, serverStore);

            Assert.True(serverStore.TryGet(serverPlayer, out InputComponent validInput));
            Assert.Equal("wb:red", validInput.PaintId);
            Result<InputComponent> validStaged = net.StagedInputs!.GetCommand(5);
            Assert.True(validStaged.Success);
            Assert.Equal("wb:red", validStaged.Value.PaintId);
        }
        finally
        {
            connection.Close();
            rpcServer.Close();
            transport.Close();
        }
    }

    private static ParticleEffect CreateEffect(
        string id,
        float rate,
        int burstCount = 4,
        int maxParticles = 128,
        float lifetimeMin = 1f,
        float lifetimeMax = 1f,
        float speedMin = 0f,
        float speedMax = 0f,
        Vector4? startColor = null
    ) {
        return new ParticleEffect(
            id,
            texture: null,
            ParticleBlendMode.Alpha,
            rate,
            burstCount,
            maxParticles,
            new ParticleRange(lifetimeMin, lifetimeMax),
            new ParticleRange(speedMin, speedMax),
            spreadDegrees: 0f,
            spawnRadius: 0f,
            new ParticleRange(0.1f, 0.1f),
            new ParticleRange(0.1f, 0.1f),
            startColor ?? Vector4.One,
            secondaryStartColor: null,
            new Vector4(1f, 1f, 1f, 0f),
            emissiveIntensity: 0f,
            Vector3.Zero,
            drag: 0f,
            ParticleRange.Constant(0f),
            ParticleRange.Constant(0f)
        );
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

    private static void TickReconcileWhilePlaying(ClientReconcileSystem system, DataStore store)
    {
        GameState previous = WaywardBeyondGameState.GameState.Get();
        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            system.Tick(1f / 128f, store);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(previous);
        }
    }

    private static async Task WaitForSnapshotsAsync(ClientInbound inbound, int expectedCount)
    {
        bool ok = await WaitForAsync(() => inbound.Snapshots.Count >= expectedCount);
        Assert.True(ok, $"Timed out waiting for {expectedCount} snapshot(s).");
    }

    private static async Task<bool> WaitForAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        long deadline = Environment.TickCount64 + timeoutMs;
        while (!condition() && Environment.TickCount64 < deadline)
        {
            await Task.Delay(5);
        }

        return condition();
    }

    private sealed class StubRpcHost : IRpcHost
    {
        public PeerSet Peers => default;
    }

    private sealed class StubAssetDatabase<T> : IAssetDatabase<T>
    {
        public readonly Dictionary<string, T> Assets = new(StringComparer.Ordinal);

        public Result<T> Get(string id)
        {
            return Assets.TryGetValue(id, out T? asset)
                ? Result<T>.FromSuccess(asset)
                : Result<T>.FromFailure($"Unknown asset \"{id}\"");
        }
    }

    private sealed class FakeSkillDatabase : ISkillDatabase
    {
        public Result<Skill> Get(string id) => Result<Skill>.FromFailure((string?)null);
        public IReadOnlyList<Skill> Get(XPSource xpSource) => [];
    }

    private sealed class FakeItemDatabase : IItemDatabase
    {
        public Result<Item> Get(string id) => Result<Item>.FromFailure((string?)null);
    }

    private sealed class FakeBrickDatabase : IBrickDatabase
    {
        public Result<Brick> Get(string id) => Result<Brick>.FromFailure((string?)null);
        public Result<Brick> Get(ushort id) => Result<Brick>.FromFailure((string?)null);
        public List<Brick> Get(Func<Brick, bool> predicate) => [];
        public bool IsCuller(in Voxel voxel, BrickShape shape) => false;
    }

    private sealed class FakeLevelCatalog : ILevelCatalog
    {
        public bool Create(string name, string seed, GameMode gameMode, out string levelGuid)
        {
            levelGuid = string.Empty;
            return true;
        }

        public Level[] ListLevels() => [];
        public bool Delete(string levelGuid) => true;
        public bool Exists(string levelGuid) => false;
        public ILevelStorage? Open(string levelGuid) => null;
    }
}
