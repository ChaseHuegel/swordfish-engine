#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
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
using Swordfish.IO;
using Swordfish.Library.Collections;
using Swordfish.Library.Events;
using Swordfish.Library.IO;
using Swordfish.Library.Serialization.Toml;
using Swordfish.Library.Types;
using Swordfish.Library.Types.Shapes;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using Swordfish.Tests;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Graphics;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Services;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Client.Voxels.Processing;
using WaywardBeyond.Config;
using WaywardBeyond.Data;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Interactions;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Items;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Networking.Rpc;
using WaywardBeyond.Networking.Transport;
using WaywardBeyond.Server;
using WaywardBeyond.Server.Components;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Voxels;
using Xunit;
using GameState = WaywardBeyond.Client.GameState;
using WaywardBeyondGameState = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>Covers mining laser visual assets, geometry, server replication, client effects, and break bursts.</summary>
[Collection("ClientGameState")]
public sealed class MiningLaserEffectsTests
{
    private const string CLIENT_ASSETS_ROOT = "TestFiles/Client/assets";
    private const string LASER_ITEM_ID = "laser";
    private const string PICKAXE_ITEM_ID = "test:wrench";
    private const ushort ORE_BRICK_ID = 7;

    [Fact]
    public void ShippedParticleEffectsLoadWithAdditiveBloomAndLinearColors()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(CLIENT_ASSETS_ROOT));
        var parseService = new VirtualFileParseService(
            [new TextureParser(), new TomlParser<ParticleEffectDefinition>()],
            vfs
        );
        var textureDb = new TextureDatabase(NullLogger<TextureDatabase>.Instance, parseService, vfs);
        var effectDb = new ParticleEffectDatabase(NullLogger<ParticleEffectDatabase>.Instance, parseService, vfs, textureDb);

        Result<ParticleEffect> impactResult = effectDb.Get("mining_laser_impact");
        Assert.True(impactResult.Success, impactResult.Message);
        ParticleEffect impact = impactResult.Value;
        Assert.Equal(ParticleBlendMode.Additive, impact.BlendMode);
        Assert.True(impact.Rate > 0f);
        Assert.True(impact.EmissiveIntensity > 1f);
        Assert.Equal(Tonemapper.SrgbToLinear(254f / 255f), impact.StartColor.X, precision: 4);
        Assert.Equal(Tonemapper.SrgbToLinear(231f / 255f), impact.StartColor.Y, precision: 4);
        Assert.Equal(Tonemapper.SrgbToLinear(97f / 255f), impact.StartColor.Z, precision: 4);
        Assert.NotNull(impact.SecondaryStartColor);

        Result<ParticleEffect> breakResult = effectDb.Get("mining_laser_break");
        Assert.True(breakResult.Success, breakResult.Message);
        ParticleEffect breakEffect = breakResult.Value;
        Assert.Equal(ParticleBlendMode.Additive, breakEffect.BlendMode);
        Assert.True(breakEffect.BurstCount > 0);
        Assert.True(breakEffect.EmissiveIntensity > 1f);
        Assert.Equal(Tonemapper.SrgbToLinear(254f / 255f), breakEffect.StartColor.X, precision: 4);
        Assert.Equal(Tonemapper.SrgbToLinear(174f / 255f), breakEffect.StartColor.Y, precision: 4);
        Assert.Equal(Tonemapper.SrgbToLinear(52f / 255f), breakEffect.StartColor.Z, precision: 4);
    }

    [Fact]
    public void ShippedLaserBeamShaderAndMaterialParseCleanly()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(CLIENT_ASSETS_ROOT));
        ILogger logger = NullLogger.Instance;
        var glslParser = new GlslParser(in logger, in vfs);

        Assert.True(vfs.TryGetFile(AssetPaths.Shaders.At("laser_beam.glsl"), out PathInfo shaderPath));
        Shader shader = glslParser.Parse(shaderPath);

        Assert.Equal(2, shader.Sources.Length);
        ShaderSource vertex = Assert.Single(shader.Sources, s => s.Type == ShaderType.Vertex);
        ShaderSource fragment = Assert.Single(shader.Sources, s => s.Type == ShaderType.Fragment);
        Assert.Contains("vWorldPos", vertex.Source);
        Assert.Contains("vBeamUV", vertex.Source);
        Assert.Contains("uTime", fragment.Source);
        Assert.Contains("BrightColor", fragment.Source);
        Assert.Contains("BrightColor = vec4(coreColor * 0.85, alpha);", fragment.Source);
        Assert.Contains("BrightColor = vec4(sheathColor * 0.95, sheathAlpha);", fragment.Source);
        Assert.DoesNotContain("* alpha, alpha)", fragment.Source);
        Assert.DoesNotContain("* sheathAlpha, sheathAlpha)", fragment.Source);

        var materialParser = new TomlParser<MaterialDefinition>();
        Assert.True(vfs.TryGetFile(AssetPaths.Materials.At("effects/laser_beam.toml"), out PathInfo materialPath));
        MaterialDefinition material = materialParser.Parse(materialPath);
        Assert.Equal("laser_beam", material.Shader);
        Assert.Equal(AlphaMode.Blend, material.AlphaMode);
        Assert.Empty(material.Textures);
    }

    [Fact]
    public void LaserBeamMeshIsSharedBoundedOrderedSheathFirstAndOutwardWound()
    {
        using var mesh = new LaserBeamMesh();

        Assert.True(mesh.Shared);
        Vector3 min = mesh.Bounds.Center - mesh.Bounds.Extents;
        Vector3 max = mesh.Bounds.Center + mesh.Bounds.Extents;
        Assert.True(mesh.Bounds.SphereRadius > 0.5f);
        Assert.True(min.X <= -0.055f);
        Assert.True(max.X >= 0.055f);
        Assert.True(min.Y <= -0.055f);
        Assert.True(max.Y >= 0.055f);
        Assert.True(min.Z <= -0.99f);
        Assert.True(max.Z >= -0.001f);

        Assert.NotEmpty(mesh.Triangles);
        Assert.Equal(0, mesh.Triangles.Length % 3);

        var sawCore = false;
        for (var i = 0; i < mesh.Triangles.Length; i += 3)
        {
            uint i0 = mesh.Triangles[i];
            uint i1 = mesh.Triangles[i + 1];
            uint i2 = mesh.Triangles[i + 2];

            float layer0 = mesh.Uv[i0].Z;
            float layer1 = mesh.Uv[i1].Z;
            float layer2 = mesh.Uv[i2].Z;
            Assert.Equal(layer0, layer1);
            Assert.Equal(layer0, layer2);

            if (layer0 < 0.5f)
            {
                sawCore = true;
            }
            else
            {
                Assert.False(sawCore, "Every outer sheath triangle must precede the inner core triangles.");
            }

            Vector3 a = mesh.Vertices[i0];
            Vector3 b = mesh.Vertices[i1];
            Vector3 c = mesh.Vertices[i2];
            Vector3 faceNormal = Vector3.Cross(b - a, c - a);
            Vector3 centroid = (a + b + c) / 3f;
            var radial = new Vector3(centroid.X, centroid.Y, 0f);
            Assert.True(Vector3.Dot(faceNormal, radial) > 0f, $"Triangle {i / 3} wound inward.");
        }

        Assert.True(sawCore);
    }

    [Fact]
    public void LaserBeamGeometryMapsBeamAxesMuzzleAndCloseRangeSafely()
    {
        Vector3[] directions =
        [
            Vector3.UnitX,
            -Vector3.UnitX,
            Vector3.UnitY,
            -Vector3.UnitY,
            Vector3.UnitZ,
            -Vector3.UnitZ,
            Vector3.Normalize(new Vector3(1f, 2f, -3f)),
            Vector3.Normalize(new Vector3(-2f, -1f, 1.5f)),
        ];

        foreach (Vector3 dir in directions)
        {
            Quaternion orientation = LaserBeamGeometry.CreateBeamOrientation(dir);
            Vector3 beamForward = Vector3.Transform(-Vector3.UnitZ, orientation);
            Assert.True(Vector3.Dot(beamForward, Vector3.Normalize(dir)) > 0.9999f, $"Direction {dir} mismatch: {beamForward}");
        }

        var viewModel = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.5f, -0.4f, 0.1f),
            Rotation = new ModelDefinition.Float3(0f, 5f, 0f),
            Scale = new ModelDefinition.Float3(0.25f, 0.25f, 0.25f),
        };
        var playerTransform = new TransformComponent(
            new Vector3(10f, 2f, -4f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.4f),
            Vector3.One
        );

        Vector3 muzzle = LaserBeamGeometry.ComputeFirstPersonMuzzle(in playerTransform, viewModel);
        Quaternion vmRot = Quaternion.CreateFromYawPitchRoll(
            5f * MathS.DEGREES_TO_RADIANS,
            0f,
            0f
        );
        Vector3 expectedLocal = new Vector3(0.5f, -0.4f, 0.1f)
            + Vector3.Transform(new Vector3(0f, 0f, -1.125f) * new Vector3(0.25f, 0.25f, 0.25f), vmRot);
        Vector3 expectedWorld = playerTransform.Position + Vector3.Transform(expectedLocal, playerTransform.Orientation);
        Assert.Equal(expectedWorld.X, muzzle.X, precision: 4);
        Assert.Equal(expectedWorld.Y, muzzle.Y, precision: 4);
        Assert.Equal(expectedWorld.Z, muzzle.Z, precision: 4);

        var scaledTransform = new TransformComponent(
            new Vector3(4f, -1f, 2f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.7f),
            new Vector3(2f, 0.5f, 1.5f)
        );
        Vector3 scaledMuzzle = LaserBeamGeometry.ComputeFirstPersonMuzzle(in scaledTransform, viewModel);
        Vector3 childWorldOrigin = scaledTransform.Position + Vector3.Transform(new Vector3(0.5f, -0.4f, 0.1f), scaledTransform.Orientation);
        Quaternion childWorldOrientation = scaledTransform.Orientation * vmRot;
        Vector3 childWorldScale = scaledTransform.Scale * new Vector3(0.25f, 0.25f, 0.25f);
        Vector3 expectedScaledMuzzle = childWorldOrigin + Vector3.Transform(new Vector3(0f, 0f, -1.125f) * childWorldScale, childWorldOrientation);
        Assert.Equal(expectedScaledMuzzle.X, scaledMuzzle.X, precision: 4);
        Assert.Equal(expectedScaledMuzzle.Y, scaledMuzzle.Y, precision: 4);
        Assert.Equal(expectedScaledMuzzle.Z, scaledMuzzle.Z, precision: 4);

        var originTransform = new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One);
        Vector3 originMuzzle = LaserBeamGeometry.ComputeFirstPersonMuzzle(in originTransform, null);
        Vector3 farTarget = new(0f, 0f, -6f);
        Assert.Equal(originMuzzle, ToolAimGeometry.ComputeSafeStart(in originTransform, originMuzzle, farTarget));

        Vector3 closeTarget = new(0f, 0f, -0.25f);
        Vector3 closeStart = ToolAimGeometry.ComputeSafeStart(in originTransform, originMuzzle, closeTarget);
        Assert.NotEqual(originMuzzle, closeStart);
        Assert.True(Vector3.Dot(closeTarget - closeStart, -Vector3.UnitZ) > 0.05f);

        Vector3 pointBlankTarget = new(0f, 0f, -0.02f);
        Vector3 pointBlankStart = ToolAimGeometry.ComputeSafeStart(in originTransform, originMuzzle, pointBlankTarget);
        Assert.NotEqual(closeStart, pointBlankStart);

        Vector3 remoteOrigin = ToolAimGeometry.ComputeRemoteOrigin(in scaledTransform);
        Vector3 expectedRemoteOrigin = scaledTransform.Position + Vector3.Transform(new Vector3(0.25f, -0.25f, -0.35f) * scaledTransform.Scale, scaledTransform.Orientation);
        Assert.Equal(expectedRemoteOrigin.X, remoteOrigin.X, precision: 4);
        Assert.Equal(expectedRemoteOrigin.Y, remoteOrigin.Y, precision: 4);
        Assert.Equal(expectedRemoteOrigin.Z, remoteOrigin.Z, precision: 4);

        Assert.True(ToolAimGeometry.IsFinite(new Vector3(1f, -2f, 3f)));
        Assert.False(ToolAimGeometry.IsFinite(new Vector3(float.NaN, 0f, 0f)));
        Assert.False(ToolAimGeometry.IsFinite(new Vector3(0f, float.PositiveInfinity, 0f)));
    }

    [Fact]
    public void AimRayHitsFloorDirectlyBelowPlayerCollider()
    {
        var physics = new JoltPhysicsSystem(NullLogger.Instance, new PhysicsSettings());
        physics.SetGravity(Vector3.Zero);
        var store = new DataStore();

        Quaternion lookingStraightDown = Quaternion.CreateFromAxisAngle(Vector3.UnitX, -MathF.PI * 0.5f);
        var playerTransform = new TransformComponent(new Vector3(0f, 1f, 0f), lookingStraightDown, CharacterDefaults.PlayerScale);
        int player = store.Alloc();
        store.AddOrUpdate(player, playerTransform);
        store.AddOrUpdate(player, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        store.AddOrUpdate(player, CharacterDefaults.CreateCharacterColliderComponent(CharacterDefaults.PlayerScale));

        int floor = store.Alloc();
        store.AddOrUpdate(floor, new TransformComponent(new Vector3(0f, -1f, 0f), Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(floor, new PhysicsComponent(Layers.NON_MOVING, BodyType.Static, CollisionDetection.Discrete));
        store.AddOrUpdate(floor, new ColliderComponent(new Box3(new Vector3(4f, 1f, 4f))));

        physics.Tick(physics.FixedTimestamp * 1.25f, store);

        bool hit = AimRay.TryTrace(
            physics,
            player,
            in playerTransform,
            CharacterDefaults.DEFAULT_REACH,
            out Vector3 endpoint,
            out Vector3 normal,
            out Entity hitEntity
        );

        Assert.True(hit);
        Assert.Equal(floor, hitEntity.Ptr);
        Assert.Equal(-0.5f, endpoint.Y, precision: 3);
        Assert.Equal(Vector3.UnitY, normal);

        var nonFiniteTransform = new TransformComponent(new Vector3(float.NaN, 0f, 0f), Quaternion.Identity, Vector3.One);
        Assert.False(AimRay.TryTrace(physics, player, in nonFiniteTransform, CharacterDefaults.DEFAULT_REACH, out _, out _, out _));
    }

    [Fact]
    public async Task ServerLaserBeamSystemChangeGatesAndReplicatesAddUpdateAndRemoval()
    {
        using var container = new Container();
        container.RegisterNetworking();
        var registry = container.Resolve<NetworkRegistry>();

        var hitPoint = new Vector3(0f, 0f, -4f);
        var hitNormal = new Vector3(0f, 1f, 0f);
        var physics = new StubServerPhysics
        {
            NextHit = new RaycastResult(hit: true, default(Entity), hitPoint, hitNormal),
        };
        var itemDb = new StubItemDatabase();
        itemDb.Add(ToolItem(LASER_ITEM_ID, ToolType.Mining));
        itemDb.Add(ToolItem(PICKAXE_ITEM_ID, ToolType.Drill));

        var serverStore = new DataStore();
        using var simStep = new SimulationStep(serverStore, physics);
        var peers = new ServerPeers();
        var sessions = new SessionManager();
        var replication = new NetworkReplicationSystem(peers, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingConfig(), registry);
        var beamSystem = new ServerLaserBeamSystem(physics, itemDb, simStep);

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

            Uuid remoteUuid = Uuid.FromValue(9001);
            int serverEntity = serverStore.Alloc(remoteUuid);
            NetworkComponent serverNet = NetworkComponent.Create();
            serverStore.AddOrUpdate(serverEntity, serverNet);
            serverStore.AddOrUpdate(serverEntity, new OwnedCharacterComponent());
            serverStore.AddOrUpdate(serverEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            serverStore.AddOrUpdate(serverEntity, new EquipmentComponent(0));
            var inventory = new InventoryComponent(2);
            inventory.Contents![0] = new ItemData(LASER_ITEM_ID, 1, 1);
            inventory.Contents[1] = new ItemData(PICKAXE_ITEM_ID, 1, 1);
            serverStore.AddOrUpdate(serverEntity, inventory);
            serverStore.AddOrUpdate(serverEntity, new InputComponent { PrimaryHeld = false });
            sessions.Register(serverStore, serverEntity, serverPeer.GameId, new Session(1));

            var clientStore = new DataStore();
            int clientEntity = clientStore.Alloc(remoteUuid);
            clientStore.AddOrUpdate(clientEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            clientStore.AddOrUpdate(clientEntity, new BodyViewComponent { Body = "wb:human_m" });
            var motionProcessor = new ClientPlayerMotionProcessor(
                new FakeInputService(),
                new FakeWindowContext(),
                new FakePhysics(),
                new EventInvoker<PlayerMovedEvent>([])
            );
            var clientReconcile = new ClientReconcileSystem(
                inbound,
                new SnapshotAckTracker(),
                motionProcessor,
                new LocalPlayerReconciler(NullLogger<LocalPlayerReconciler>.Instance),
                registry,
                NullLogger<ClientReconcileSystem>.Instance
            );

            //  Non-firing mining laser does not attach LaserBeamComponent.
            beamSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.False(serverStore.Has(typeof(LaserBeamComponent), serverEntity));

            //  Switching to a non-mining tool while holding primary never attaches LaserBeamComponent.
            serverStore.AddOrUpdate(serverEntity, new EquipmentComponent(1));
            serverStore.AddOrUpdate(serverEntity, new InputComponent { PrimaryHeld = true });
            beamSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.False(serverStore.Has(typeof(LaserBeamComponent), serverEntity));

            //  Firing with mining laser attaches LaserBeamComponent and marks it dirty.
            serverStore.AddOrUpdate(serverEntity, new EquipmentComponent(0));
            beamSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.Equal(serverEntity, physics.LastIgnoredEntity);
            Assert.True(serverStore.TryGet(serverEntity, out LaserBeamComponent serverBeam));
            Assert.True(serverBeam.Hit);
            Assert.Equal(hitPoint, serverBeam.Target);
            Assert.Equal(hitNormal, serverBeam.Normal);
            Assert.True(serverStore.IsDirty<LaserBeamComponent>(serverEntity));

            //  Publish to client mirror via loopback replication.
            replication.PublishStage(1f, serverStore);
            await WaitForSnapshotsAsync(inbound, expectedCount: 1);
            TickReconcileWhilePlaying(clientReconcile, clientStore);

            Assert.True(clientStore.TryGet(clientEntity, out LaserBeamComponent replicatedBeam));
            Assert.True(replicatedBeam.Hit);
            Assert.Equal(hitPoint, replicatedBeam.Target);
            Assert.Equal(hitNormal, replicatedBeam.Normal);

            //  Steady aim within deadband must not re-dirty LaserBeamComponent.
            Assert.False(serverStore.IsDirty<LaserBeamComponent>(serverEntity));
            beamSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.False(serverStore.IsDirty<LaserBeamComponent>(serverEntity));

            //  Missing target updates component with Hit = false and endpoint at reach.
            physics.NextHit = default;
            beamSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.True(serverStore.TryGet(serverEntity, out LaserBeamComponent missBeam));
            Assert.False(missBeam.Hit);
            Assert.Equal(new Vector3(0f, 0f, -CharacterDefaults.DEFAULT_REACH), missBeam.Target);
            Assert.True(serverStore.IsDirty<LaserBeamComponent>(serverEntity));

            //  Releasing primary removes LaserBeamComponent and replicates ComponentRemoval.
            serverStore.AddOrUpdate(serverEntity, new InputComponent { PrimaryHeld = false });
            beamSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.False(serverStore.Has(typeof(LaserBeamComponent), serverEntity));

            replication.PublishStage(1f, serverStore);
            await WaitForSnapshotsAsync(inbound, expectedCount: 1);
            TickReconcileWhilePlaying(clientReconcile, clientStore);

            Assert.False(clientStore.Has(typeof(LaserBeamComponent), clientEntity));
        }
        finally
        {
            connection.Close();
            rpcServer.Close();
            transport.Close();
        }
    }

    [Fact]
    public void MiningLaserEffectSystemManagesLocalRemoteHitMissAndShutdownCleanup()
    {
        EffectSystemFixture fixture = CreateEffectSystemFixture();
        using MiningLaserEffectSystem system = fixture.System;

        int localPlayer = fixture.Store.Alloc();
        fixture.Store.AddOrUpdate(localPlayer, new PlayerComponent());
        fixture.Store.AddOrUpdate(localPlayer, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        fixture.Store.AddOrUpdate(localPlayer, new EquipmentComponent(0));
        var inventory = new InventoryComponent(2);
        inventory.Contents![0] = new ItemData(LASER_ITEM_ID, 1, 1);
        inventory.Contents[1] = new ItemData(PICKAXE_ITEM_ID, 1, 1);
        fixture.Store.AddOrUpdate(localPlayer, inventory);
        //  Presence of BodyViewComponent + replicated LaserBeamComponent on local player must never spawn a remote beam.
        fixture.Store.AddOrUpdate(localPlayer, new BodyViewComponent { Body = "wb:human_m" });
        fixture.Store.AddOrUpdate(localPlayer, new LaserBeamComponent { TargetX = 2f, TargetY = 2f, TargetZ = -2f, Hit = true });

        int remotePlayer = fixture.Store.Alloc();
        fixture.Store.AddOrUpdate(remotePlayer, new TransformComponent(new Vector3(5f, 0f, 0f), Quaternion.Identity, Vector3.One));
        fixture.Store.AddOrUpdate(remotePlayer, new BodyViewComponent { Body = "wb:human_m" });

        GameState previous = WaywardBeyondGameState.GameState.Get();
        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            //  Local hit: creates beam renderer + impact light + playing emitter.
            fixture.Physics.NextHit = new RaycastResult(true, default(Entity), new Vector3(0f, 0f, -5f), Vector3.UnitY);
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            system.Tick(0.016f, fixture.Store);

            Assert.Equal(localPlayer, fixture.Physics.LastIgnoredEntity);
            Assert.Equal(1, CountComponents<MeshRendererComponent>(fixture.Store));
            Assert.Equal(1, CountComponents<LightComponent>(fixture.Store));
            Assert.Equal(1, CountComponents<ParticleEmitterComponent>(fixture.Store));
            ParticleEmitterComponent hitEmitter = Assert.Single(CollectComponents<ParticleEmitterComponent>(fixture.Store));
            Assert.True(hitEmitter.Playing);
            MeshRendererComponent beamRenderer = Assert.Single(CollectComponents<MeshRendererComponent>(fixture.Store));
            Assert.True(beamRenderer.MeshRenderer!.Mesh.Shared);

            //  Local miss: beam stays alive, light is removed, impact emitter pauses.
            fixture.Physics.NextHit = default;
            system.Tick(0.016f, fixture.Store);

            Assert.Equal(1, CountComponents<MeshRendererComponent>(fixture.Store));
            Assert.Equal(0, CountComponents<LightComponent>(fixture.Store));
            ParticleEmitterComponent missEmitter = Assert.Single(CollectComponents<ParticleEmitterComponent>(fixture.Store));
            Assert.False(missEmitter.Playing);

            //  Remote player with LaserBeamComponent spawns a second independent beam; non-finite remote target is ignored.
            int badRemote = fixture.Store.Alloc();
            fixture.Store.AddOrUpdate(badRemote, new TransformComponent(new Vector3(-5f, 0f, 0f), Quaternion.Identity, Vector3.One));
            fixture.Store.AddOrUpdate(badRemote, new BodyViewComponent { Body = "wb:human_m" });
            fixture.Store.AddOrUpdate(badRemote, new LaserBeamComponent
            {
                TargetX = float.NaN,
                TargetY = 0f,
                TargetZ = -2f,
                Hit = true,
            });
            fixture.Store.AddOrUpdate(remotePlayer, new LaserBeamComponent
            {
                TargetX = 5f,
                TargetY = 1f,
                TargetZ = -3f,
                NormalX = 0f,
                NormalY = 0f,
                NormalZ = 1f,
                Hit = true,
            });
            system.Tick(0.016f, fixture.Store);

            Assert.Equal(2, CountComponents<MeshRendererComponent>(fixture.Store));
            Assert.Equal(1, CountComponents<LightComponent>(fixture.Store));
            Assert.Equal(2, CountComponents<ParticleEmitterComponent>(fixture.Store));

            //  Removing remote LaserBeamComponent and releasing local LMB frees all effect entities.
            fixture.Input.HeldButtons.Remove(MouseButton.Left);
            fixture.Store.Remove(typeof(LaserBeamComponent), remotePlayer);
            system.Tick(0.016f, fixture.Store);

            Assert.Equal(0, CountComponents<MeshRendererComponent>(fixture.Store));
            Assert.Equal(0, CountComponents<LightComponent>(fixture.Store));
            Assert.Equal(0, CountComponents<ParticleEmitterComponent>(fixture.Store));

            //  Re-activate then transition out of Playing -> ReleaseAll cleans up every entity.
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            fixture.Physics.NextHit = new RaycastResult(true, default(Entity), new Vector3(0f, 0f, -4f), Vector3.UnitZ);
            system.Tick(0.016f, fixture.Store);
            Assert.Equal(1, CountComponents<MeshRendererComponent>(fixture.Store));

            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            system.Tick(0.016f, fixture.Store);
            Assert.Equal(0, CountComponents<MeshRendererComponent>(fixture.Store));
            Assert.Equal(0, CountComponents<LightComponent>(fixture.Store));
            Assert.Equal(0, CountComponents<ParticleEmitterComponent>(fixture.Store));
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(previous);
        }
    }

    [Fact]
    public void MiningLaserEffectSystemIgnoresNonFiniteTransformsAndReleasesExistingBeam()
    {
        EffectSystemFixture fixture = CreateEffectSystemFixture();
        using MiningLaserEffectSystem system = fixture.System;

        int remotePlayer = fixture.Store.Alloc();
        fixture.Store.AddOrUpdate(remotePlayer, new TransformComponent(new Vector3(float.NaN, 0f, 0f), Quaternion.Identity, Vector3.One));
        fixture.Store.AddOrUpdate(remotePlayer, new BodyViewComponent { Body = "wb:human_m" });
        fixture.Store.AddOrUpdate(remotePlayer, new LaserBeamComponent
        {
            TargetX = 0f,
            TargetY = 0f,
            TargetZ = -3f,
            NormalX = 0f,
            NormalY = 1f,
            NormalZ = 0f,
            Hit = true,
        });

        int localPlayer = fixture.Store.Alloc();
        fixture.Store.AddOrUpdate(localPlayer, new PlayerComponent());
        fixture.Store.AddOrUpdate(localPlayer, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        fixture.Store.AddOrUpdate(localPlayer, new EquipmentComponent(0));
        var inventory = new InventoryComponent(1);
        inventory.Contents![0] = new ItemData(LASER_ITEM_ID, 1, 1);
        fixture.Store.AddOrUpdate(localPlayer, inventory);

        GameState previous = WaywardBeyondGameState.GameState.Get();
        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            fixture.Physics.NextHit = new RaycastResult(true, default(Entity), new Vector3(0f, 0f, -4f), Vector3.UnitY);
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            system.Tick(0.016f, fixture.Store);

            //  Only the finite local player spawns entities; non-finite remote player spawns nothing.
            Assert.Equal(1, CountComponents<MeshRendererComponent>(fixture.Store));
            Assert.Equal(1, CountComponents<LightComponent>(fixture.Store));
            Assert.Equal(1, CountComponents<ParticleEmitterComponent>(fixture.Store));

            //  Non-finite local transform skips beam update and releases the prior local beam entities.
            fixture.Store.AddOrUpdate(localPlayer, new TransformComponent(new Vector3(float.PositiveInfinity, 0f, 0f), Quaternion.Identity, Vector3.One));
            system.Tick(0.016f, fixture.Store);

            Assert.Equal(0, CountComponents<MeshRendererComponent>(fixture.Store));
            Assert.Equal(0, CountComponents<LightComponent>(fixture.Store));
            Assert.Equal(0, CountComponents<ParticleEmitterComponent>(fixture.Store));
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(previous);
        }
    }

    [Fact]
    public void BrickBreakParticleListenerAndVoxelReconcileAndCleanupEmitAndClearBursts()
    {
        var particles = new RecordingParticleService();
        IParticleService particleService = particles;
        var listener = new BrickBreakParticleListener(in particleService);
        var brickEvent = new EventInvoker<BrickEvent>([listener]);

        //  Local predicted break with explicit world position and normal emits burst.
        var breakPos = new Vector3(3.5f, 1.5f, -2.5f);
        var breakNormal = new Vector3(0f, 0f, 1f);
        brickEvent.Invoke(new BrickEvent(ORE_BRICK_ID, BrickEventKind.Break, breakPos, breakNormal));
        Assert.Single(particles.Bursts);
        Assert.Equal(("mining_laser_break", breakPos, breakNormal, (int?)null), particles.Bursts[0]);

        //  Missing normal defaults to UnitY; Place, zero BrickID, and missing WorldPosition are ignored.
        brickEvent.Invoke(new BrickEvent(ORE_BRICK_ID, BrickEventKind.Break, new Vector3(1f, 2f, 3f)));
        brickEvent.Invoke(new BrickEvent(ORE_BRICK_ID, BrickEventKind.Place, breakPos, breakNormal));
        brickEvent.Invoke(new BrickEvent(0, BrickEventKind.Break, breakPos, breakNormal));
        brickEvent.Invoke(new BrickEvent(ORE_BRICK_ID, BrickEventKind.Break));

        Assert.Equal(2, particles.Bursts.Count);
        Assert.Equal(Vector3.UnitY, particles.Bursts[1].Direction);
        particles.Bursts.Clear();

        //  Remote unpredicted brick break in ClientVoxelReconcileSystem raises BrickEvent with BrickToWorldSpace position.
        var store = new DataStore();
        var voxelObject = new VoxelObject(chunkSize: 16);
        voxelObject.Set(1, 2, 3, new Voxel(ORE_BRICK_ID, new ShapeLight(BrickShape.Block, 0), 0));
        var structureOrigin = new Vector3(10f, 0f, -4f);
        int structure = store.Alloc();
        Uuid structureUuid = store.GetUuid(structure);
        store.AddOrUpdate(structure, new TransformComponent(structureOrigin, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(structure, new VoxelComponent(voxelObject));

        var inbound = new ClientInbound();
        var bricks = new StubBrickRegistryDatabase();
        var lighting = new VoxelLightingService(bricks);
        var reconcile = new ClientVoxelReconcileSystem(inbound, new SnapshotAckTracker(), bricks, lighting, brickEvent);

        inbound.VoxelEdits.Enqueue(new VoxelEditMessage
        {
            EntityUuid = structureUuid.ToValue(),
            X = 1,
            Y = 2,
            Z = 3,
            Voxel = new Voxel(),
            Sequence = 0,
            BrickId = null,
        });

        GameState previous = WaywardBeyondGameState.GameState.Get();
        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            reconcile.Tick(0.016f, store);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(previous);
        }

        Vector3 expectedWorldPos = InteractionResolver.BrickToWorldSpace(new Int3(1, 2, 3), structureOrigin, Quaternion.Identity);
        Assert.Single(particles.Bursts);
        Assert.Equal("mining_laser_break", particles.Bursts[0].EffectId);
        Assert.Equal(expectedWorldPos, particles.Bursts[0].Position);
        Assert.Equal(Vector3.UnitY, particles.Bursts[0].Direction);

        //  ClientCleanupSystem clears particle simulation on world teardown.
        var cleanup = new ClientCleanupSystem(NullLogger<ClientCleanupSystem>.Instance, particles);
        cleanup.RequestCleanup();
        cleanup.Tick(0.016f, store);
        Assert.Equal(1, particles.ClearCount);
    }

    private static EffectSystemFixture CreateEffectSystemFixture()
    {
        var itemDb = new StubItemDatabase();
        itemDb.Add(ToolItem(LASER_ITEM_ID, ToolType.Mining));
        itemDb.Add(ToolItem(PICKAXE_ITEM_ID, ToolType.Drill));

        IInputService input = new FakeInputService();
        var fakeInput = (FakeInputService)input;
        IPhysics physics = new StubClientPhysics();
        var stubPhysics = (StubClientPhysics)physics;
        var playerData = new PlayerData(itemDb);
        var motionProcessor = new ClientPlayerMotionProcessor(
            input,
            new FakeWindowContext(),
            physics,
            new EventInvoker<PlayerMovedEvent>([])
        );
        var interactionState = new InteractionState(in motionProcessor);
        IAssetDatabase<Material> materialDb = new StubAssetDatabase<Material>
        {
            Assets =
            {
                ["effects/laser_beam"] = new Material(new Shader("laser_beam"), [])
                {
                    AlphaMode = AlphaMode.Blend,
                },
            },
        };
        IAssetDatabase<ParticleEffect> effectDb = new StubAssetDatabase<ParticleEffect>
        {
            Assets =
            {
                ["mining_laser_impact"] = CreateDummyParticleEffect("mining_laser_impact"),
            },
        };

        var system = new MiningLaserEffectSystem(
            in playerData,
            in interactionState,
            in input,
            in physics,
            in materialDb,
            in effectDb
        );

        return new EffectSystemFixture(new DataStore(), system, fakeInput, stubPhysics);
    }

    private static ParticleEffect CreateDummyParticleEffect(string id)
    {
        return new ParticleEffect(
            id,
            texture: null,
            ParticleBlendMode.Additive,
            rate: 55f,
            burstCount: 8,
            maxParticles: 128,
            new ParticleRange(0.12f, 0.28f),
            new ParticleRange(2.5f, 6.0f),
            spreadDegrees: 62f,
            spawnRadius: 0.05f,
            new ParticleRange(0.04f, 0.075f),
            new ParticleRange(0.01f, 0.02f),
            Vector4.One,
            secondaryStartColor: null,
            endColor: Vector4.One,
            emissiveIntensity: 3.2f,
            gravity: Vector3.Zero,
            drag: 3.2f,
            new ParticleRange(0f, 0f),
            new ParticleRange(0f, 0f)
        );
    }

    private static Item ToolItem(string id, ToolType type)
    {
        return new Item(
            id,
            name: id,
            iconID: null,
            maxStack: 1,
            placeable: null,
            tool: new ToolDefinition { Type = type },
            viewModel: null,
            worldModel: null
        );
    }

    private static int CountComponents<T>(DataStore store) where T : struct, IDataComponent
    {
        var count = 0;
        store.Query<T>(0f, (float _, DataStore _, int _, in T _) => count++);
        return count;
    }

    private static List<T> CollectComponents<T>(DataStore store) where T : struct, IDataComponent
    {
        var items = new List<T>();
        store.Query<T>(0f, (float _, DataStore _, int _, in T component) => items.Add(component));
        return items;
    }

    private static async Task WaitForSnapshotsAsync(ClientInbound inbound, int expectedCount)
    {
        long deadline = Environment.TickCount64 + 5000;
        while (inbound.Snapshots.Count < expectedCount && Environment.TickCount64 < deadline)
        {
            await Task.Delay(5);
        }

        Assert.True(inbound.Snapshots.Count >= expectedCount, $"Timed out waiting for {expectedCount} snapshot(s).");
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

    private sealed class EffectSystemFixture(
        DataStore store,
        MiningLaserEffectSystem system,
        FakeInputService input,
        StubClientPhysics physics
    ) {
        public readonly DataStore Store = store;
        public readonly MiningLaserEffectSystem System = system;
        public readonly FakeInputService Input = input;
        public readonly StubClientPhysics Physics = physics;
    }

    private sealed class StubServerPhysics : IServerWorldPhysics
    {
        public RaycastResult NextHit { get; set; }
        public int LastIgnoredEntity { get; private set; } = -1;

        public float FixedTimestamp => 1f / 64f;

        public event EventHandler<EventArgs>? FixedUpdate;

        public RaycastResult Raycast(in Ray ray) => Raycast(in ray, ignoreEntity: -1);

        public RaycastResult Raycast(in Ray ray, int ignoreEntity)
        {
            LastIgnoredEntity = ignoreEntity;
            return NextHit;
        }

        public bool Overlaps(in ShapeOverlapQuery query) => false;

        public void SetGravity(Vector3 gravity) { }

        public void Tick(float delta, DataStore store)
        {
            FixedUpdate?.Invoke(this, EventArgs.Empty);
        }
    }

    private sealed class StubClientPhysics : IPhysics
    {
        public RaycastResult NextHit { get; set; }
        public int LastIgnoredEntity { get; private set; } = -1;

        public float FixedTimestamp => 1f / 64f;

        public event EventHandler<EventArgs>? FixedUpdate
        {
            add { }
            remove { }
        }

        public RaycastResult Raycast(in Ray ray) => Raycast(in ray, ignoreEntity: -1);

        public RaycastResult Raycast(in Ray ray, int ignoreEntity)
        {
            LastIgnoredEntity = ignoreEntity;
            return NextHit;
        }

        public bool Overlaps(in ShapeOverlapQuery query) => false;

        public void SetGravity(Vector3 gravity) { }
    }

    private sealed class StubItemDatabase : IItemDatabase
    {
        private readonly Dictionary<string, Item> _items = new(StringComparer.Ordinal);

        public void Add(Item item) => _items[item.ID] = item;

        public Result<Item> Get(string id)
        {
            return _items.TryGetValue(id, out Item? item)
                ? Result<Item>.FromSuccess(item)
                : Result<Item>.FromFailure((string?)null);
        }
    }

    private sealed class StubAssetDatabase<T> : IAssetDatabase<T>
    {
        public readonly Dictionary<string, T> Assets = new(StringComparer.Ordinal);

        public Result<T> Get(string id)
        {
            return Assets.TryGetValue(id, out T? asset)
                ? Result<T>.FromSuccess(asset)
                : Result<T>.FromFailure((string?)null);
        }
    }

    private sealed class RecordingParticleService : IParticleService
    {
        public readonly List<(string EffectId, Vector3 Position, Vector3 Direction, int? Count)> Bursts = [];
        public int ClearCount { get; private set; }

        public int ActiveParticleCount => 0;

        public void EmitBurst(in ParticleBurstRequest request) { }

        public Result EmitBurst(string effectId, Vector3 position, Vector3 direction, int? count = null)
        {
            Bursts.Add((effectId, position, direction, count));
            return Result.FromSuccess();
        }

        public void Clear()
        {
            ClearCount++;
        }
    }

    private sealed class StubBrickRegistryDatabase : IBrickDatabase, IBrickRegistry
    {
        private readonly Brick _ore = new("wb:ore", BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, default, sounds: null);

        public StubBrickRegistryDatabase()
        {
            _ore.BindDataID(ORE_BRICK_ID);
        }

        public Result<Brick> Get(string id) => id == "wb:ore" ? Result<Brick>.FromSuccess(_ore) : Result<Brick>.FromFailure((string?)null);

        public Result<Brick> Get(ushort id) => id == ORE_BRICK_ID ? Result<Brick>.FromSuccess(_ore) : Result<Brick>.FromFailure((string?)null);

        public List<Brick> Get(Func<Brick, bool> predicate) => [];

        public bool IsCuller(in Voxel voxel, BrickShape shape) => voxel.ID != 0 && shape == BrickShape.Block;

        public ushort Id(string name) => name == "wb:ore" ? ORE_BRICK_ID : (ushort)0;

        public ushort Resolve(string name) => Id(name);

        public string? Name(ushort id) => id == ORE_BRICK_ID ? "wb:ore" : null;
    }

    private sealed class StubRpcHost : IRpcHost
    {
        public PeerSet Peers => default;
    }
}
