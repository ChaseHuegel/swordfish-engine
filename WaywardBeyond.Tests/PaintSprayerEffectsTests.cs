#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using DryIoc;
using Krill;
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
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Settings;
using Swordfish.Tests;
using WaywardBeyond.Bricks;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Events;
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
using WaywardBeyond.Server.Particles;
using WaywardBeyond.Server.Systems;
using WaywardBeyond.Voxels;
using Xunit;
using GameState = WaywardBeyond.Client.GameState;
using WaywardBeyondGameState = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>Covers paint sprayer particle templates, tinting, nozzle geometry, local and generic replicated emitters, and face splatter bursts.</summary>
[Collection("ClientGameState")]
public sealed class PaintSprayerEffectsTests
{
    private const string CLIENT_ASSETS_ROOT = "TestFiles/Client/assets";
    private const string SPRAYER_ITEM_ID = "wb:sprayer";
    private const string LASER_ITEM_ID = "laser";
    private const ushort BRICK_ID = 1;
    private const ushort RED_PAINT_ID = 1;
    private const ushort CYAN_PAINT_ID = 2;
    private const ushort CAUTION_TEX_ID = 3;

    [Fact]
    public void ShippedSprayerParticleEffectsLoadWithAlphaBlendAndZeroEmissive()
    {
        var vfs = new VirtualFileSystem();
        vfs.Mount(new PathInfo(CLIENT_ASSETS_ROOT));
        var parseService = new VirtualFileParseService(
            [new TextureParser(), new TomlParser<ParticleEffectDefinition>()],
            vfs
        );
        var textureDb = new TextureDatabase(NullLogger<TextureDatabase>.Instance, parseService, vfs);
        var effectDb = new ParticleEffectDatabase(NullLogger<ParticleEffectDatabase>.Instance, parseService, vfs, textureDb);

        Result<ParticleEffect> streamResult = effectDb.Get("paint_sprayer_stream");
        Assert.True(streamResult.Success, streamResult.Message);
        ParticleEffect stream = streamResult.Value;
        Assert.Equal(ParticleBlendMode.Alpha, stream.BlendMode);
        Assert.Equal(0f, stream.EmissiveIntensity);
        Assert.True(stream.Rate > 0f);
        Assert.True(stream.SpreadDegrees <= 20f);
        Assert.True(stream.Gravity.Y < 0f);
        Assert.Equal(1f, stream.StartColor.X, precision: 4);
        Assert.Equal(1f, stream.StartColor.Y, precision: 4);
        Assert.Equal(1f, stream.StartColor.Z, precision: 4);
        Assert.True(stream.SecondaryStartColor.HasValue);

        Result<ParticleEffect> impactResult = effectDb.Get("paint_sprayer_impact");
        Assert.True(impactResult.Success, impactResult.Message);
        ParticleEffect impact = impactResult.Value;
        Assert.Equal(ParticleBlendMode.Alpha, impact.BlendMode);
        Assert.Equal(0f, impact.EmissiveIntensity);
        Assert.True(impact.Rate > 0f);
        Assert.True(impact.SpreadDegrees >= 60f);

        Result<ParticleEffect> coatResult = effectDb.Get("paint_sprayer_coat");
        Assert.True(coatResult.Success, coatResult.Message);
        ParticleEffect coat = coatResult.Value;
        Assert.Equal(ParticleBlendMode.Alpha, coat.BlendMode);
        Assert.Equal(0f, coat.EmissiveIntensity);
        Assert.Equal(0f, coat.Rate);
        Assert.True(coat.BurstCount > 0);
        Assert.Equal(0.90f, coat.StartColor.W, precision: 4);

        string[] solventIds =
        [
            "paint_sprayer_solvent_stream",
            "paint_sprayer_solvent_impact",
            "paint_sprayer_solvent_coat",
        ];

        for (var i = 0; i < solventIds.Length; i++)
        {
            Result<ParticleEffect> solventResult = effectDb.Get(solventIds[i]);
            Assert.True(solventResult.Success, solventResult.Message);
            ParticleEffect solvent = solventResult.Value;
            Assert.Equal(ParticleBlendMode.Alpha, solvent.BlendMode);
            Assert.Equal(0f, solvent.EmissiveIntensity);
            Assert.Equal(Tonemapper.SrgbToLinear(44f / 255f), solvent.EndColor.X, precision: 4);
            Assert.Equal(Tonemapper.SrgbToLinear(232f / 255f), solvent.EndColor.Y, precision: 4);
            Assert.Equal(Tonemapper.SrgbToLinear(245f / 255f), solvent.EndColor.Z, precision: 4);
        }
    }

    [Fact]
    public void PaintSprayerGeometryComputesNozzleSafeStartDirectionAndFaceNormals()
    {
        var viewModel = new ModelDefinition
        {
            Position = new ModelDefinition.Float3(0.5f, -0.5f, -0.3f),
            Rotation = new ModelDefinition.Float3(0f, 5f, 0f),
            Scale = new ModelDefinition.Float3(1f, 1f, 1f),
        };
        var scaledTransform = new TransformComponent(
            new Vector3(4f, 1f, -2f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.6f),
            new Vector3(2f, 0.5f, 1.5f)
        );

        Vector3 actualNozzle = PaintSprayerGeometry.ComputeFirstPersonNozzle(in scaledTransform, viewModel);
        Quaternion localRot = Quaternion.CreateFromYawPitchRoll(5f * MathS.DEGREES_TO_RADIANS, 0f, 0f);
        Vector3 childWorldOrigin = scaledTransform.Position + Vector3.Transform(new Vector3(0.5f, -0.5f, -0.3f), scaledTransform.Orientation);
        Quaternion childWorldRot = scaledTransform.Orientation * localRot;
        Vector3 childWorldScale = scaledTransform.Scale * Vector3.One;
        Vector3 expectedNozzle = childWorldOrigin + Vector3.Transform(new Vector3(0f, -0.006f, -0.85f) * childWorldScale, childWorldRot);
        Assert.Equal(expectedNozzle.X, actualNozzle.X, precision: 4);
        Assert.Equal(expectedNozzle.Y, actualNozzle.Y, precision: 4);
        Assert.Equal(expectedNozzle.Z, actualNozzle.Z, precision: 4);

        var originTransform = new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One);
        Vector3 originNozzle = PaintSprayerGeometry.ComputeFirstPersonNozzle(in originTransform, null);
        Assert.Equal(originNozzle, ToolAimGeometry.ComputeSafeStart(in originTransform, originNozzle, new Vector3(0f, 0f, -5f)));

        Vector3 closeStart = ToolAimGeometry.ComputeSafeStart(in originTransform, originNozzle, new Vector3(0f, 0f, -0.35f));
        Assert.NotEqual(originNozzle, closeStart);
        Assert.True(Vector3.Dot(new Vector3(0f, 0f, -0.35f) - closeStart, -Vector3.UnitZ) > 0.05f);

        Vector3 pointBlankStart = ToolAimGeometry.ComputeSafeStart(in originTransform, originNozzle, new Vector3(0f, 0f, -0.02f));
        Assert.NotEqual(closeStart, pointBlankStart);

        Vector3 dir = ToolAimGeometry.ComputeAimDirection(in originTransform, originNozzle, new Vector3(0f, 0f, -6f));
        Assert.Equal(1f, dir.Length(), precision: 4);
        Assert.True(dir.Z < -0.9f);
        Assert.Equal(-Vector3.UnitZ, ToolAimGeometry.ComputeAimDirection(in originTransform, originNozzle, originNozzle));

        Vector3 expectedRemoteOrigin = scaledTransform.Position + Vector3.Transform(new Vector3(0.25f, -0.25f, -0.35f) * scaledTransform.Scale, scaledTransform.Orientation);
        Vector3 actualRemoteOrigin = ToolAimGeometry.ComputeRemoteOrigin(in scaledTransform);
        Assert.Equal(expectedRemoteOrigin.X, actualRemoteOrigin.X, precision: 4);
        Assert.Equal(expectedRemoteOrigin.Y, actualRemoteOrigin.Y, precision: 4);
        Assert.Equal(expectedRemoteOrigin.Z, actualRemoteOrigin.Z, precision: 4);

        PaintSprayerGeometry.ComputeFaceWorldNormalAndOffset(
            BrickFaceMask.Top,
            Quaternion.Identity,
            Orientation.Identity,
            out Vector3 topNormal,
            out Vector3 topOffset
        );
        Assert.Equal(Vector3.UnitY, topNormal);
        Assert.Equal(new Vector3(0f, 0.5f, 0f), topOffset);

        var voxelYaw90 = new Orientation(pitch: 0, yaw: 1, roll: 0);
        Quaternion structureYaw90 = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);
        Vector3 expectedFrontWorld = Vector3.Transform(
            Vector3.Transform(Vector3.UnitZ, voxelYaw90.ToQuaternion()),
            structureYaw90
        );
        PaintSprayerGeometry.ComputeFaceWorldNormalAndOffset(
            BrickFaceMask.Front,
            structureYaw90,
            voxelYaw90,
            out Vector3 rotatedNormal,
            out Vector3 rotatedOffset
        );
        Assert.Equal(expectedFrontWorld.X, rotatedNormal.X, precision: 4);
        Assert.Equal(expectedFrontWorld.Y, rotatedNormal.Y, precision: 4);
        Assert.Equal(expectedFrontWorld.Z, rotatedNormal.Z, precision: 4);
        Assert.Equal(rotatedNormal * 0.5f, rotatedOffset);

        PaintSprayerGeometry.ComputeFaceWorldNormalAndOffset(
            BrickFaceMask.Front | BrickFaceMask.Top,
            Quaternion.Identity,
            Orientation.Identity,
            out Vector3 slopeNormal,
            out Vector3 slopeOffset
        );
        Vector3 expectedSlope = Vector3.Normalize(new Vector3(0f, 1f, 1f));
        Assert.Equal(expectedSlope.X, slopeNormal.X, precision: 4);
        Assert.Equal(expectedSlope.Y, slopeNormal.Y, precision: 4);
        Assert.Equal(expectedSlope.Z, slopeNormal.Z, precision: 4);
        Assert.Equal(expectedSlope * 0.35f, slopeOffset);

        Quaternion pitch45 = Quaternion.CreateFromAxisAngle(Vector3.UnitX, MathF.PI * 0.25f);
        PaintSprayerGeometry.ComputeFaceWorldNormalAndOffset(
            BrickFaceMask.All,
            pitch45,
            Orientation.Identity,
            out Vector3 allNormal,
            out Vector3 allOffset
        );
        Vector3 expectedAllNormal = Vector3.Normalize(Vector3.Transform(Vector3.UnitY, pitch45));
        Assert.Equal(expectedAllNormal.X, allNormal.X, precision: 4);
        Assert.Equal(expectedAllNormal.Y, allNormal.Y, precision: 4);
        Assert.Equal(expectedAllNormal.Z, allNormal.Z, precision: 4);
        Assert.Equal(Vector3.Zero, allOffset);

        Assert.True(ToolAimGeometry.IsFinite(new Vector3(1f, -2f, 3f)));
        Assert.False(ToolAimGeometry.IsFinite(new Vector3(float.NaN, 0f, 0f)));
        Assert.False(ToolAimGeometry.IsFinite(new Vector3(0f, float.PositiveInfinity, 0f)));
    }

    [Fact]
    public void PaintSprayerEffectSystemDrivesColorAndSolventEmittersAndReleasesCleanly()
    {
        SprayerFixture fixture = CreateSprayerFixture();
        int player = fixture.Store.Alloc();
        fixture.Store.AddOrUpdate(player, new PlayerComponent());
        fixture.Store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        fixture.Store.AddOrUpdate(player, new EquipmentComponent(0));
        var inventory = new InventoryComponent(2);
        inventory.Contents![0] = new ItemData(SPRAYER_ITEM_ID, 1, 1);
        inventory.Contents[1] = new ItemData(LASER_ITEM_ID, 1, 1);
        fixture.Store.AddOrUpdate(player, inventory);

        uint redTint = ParticleEffect.PackTint(fixture.Paints.Get(RED_PAINT_ID).Value.Color);
        uint cyanTint = ParticleEffect.PackTint(fixture.Paints.Get(CYAN_PAINT_ID).Value.Color);

        GameState previous = WaywardBeyondGameState.GameState.Get();
        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            //  No button held -> no emitters spawned.
            fixture.System.Tick(0.016f, fixture.Store);
            Assert.Equal(0, CountComponents<ParticleEmitterComponent>(fixture.Store));

            //  Holding non-sprayer tool -> no emitters spawned.
            fixture.Store.AddOrUpdate(player, new EquipmentComponent(1));
            fixture.Input.HeldButtons.Add(MouseButton.Left);
            fixture.System.Tick(0.016f, fixture.Store);
            Assert.Equal(0, CountComponents<ParticleEmitterComponent>(fixture.Store));

            //  LMB hit with sprayer -> nozzle stream + surface impact emitters tinted to active red swatch.
            fixture.Store.AddOrUpdate(player, new EquipmentComponent(0));
            fixture.Physics.NextHit = new RaycastResult(true, default(Entity), new Vector3(0f, 0f, -4f), Vector3.UnitZ);
            fixture.System.Tick(0.016f, fixture.Store);

            Assert.Equal(player, fixture.Physics.LastIgnoredEntity);
            List<ParticleEmitterComponent> redEmitters = CollectComponents<ParticleEmitterComponent>(fixture.Store);
            Assert.Equal(2, redEmitters.Count);
            Assert.All(redEmitters, e =>
            {
                Assert.True(e.Playing);
                Assert.Equal(redTint, e.TintRgba);
            });
            Assert.Contains(redEmitters, e => e.EffectId == "paint_sprayer_stream");
            Assert.Contains(redEmitters, e => e.EffectId == "paint_sprayer_impact");

            //  Changing selected swatch mid-spray updates TintRgba immediately on the next tick.
            fixture.InteractionState.SelectColorPaint("wb:cyan");
            fixture.System.Tick(0.016f, fixture.Store);
            List<ParticleEmitterComponent> cyanEmitters = CollectComponents<ParticleEmitterComponent>(fixture.Store);
            Assert.Equal(2, cyanEmitters.Count);
            Assert.All(cyanEmitters, e => Assert.Equal(cyanTint, e.TintRgba));

            //  Aiming into empty air keeps nozzle stream playing and pauses surface impact mist.
            fixture.Physics.NextHit = default;
            fixture.System.Tick(0.016f, fixture.Store);
            List<ParticleEmitterComponent> missEmitters = CollectComponents<ParticleEmitterComponent>(fixture.Store);
            Assert.Equal(2, missEmitters.Count);
            ParticleEmitterComponent streamMiss = Assert.Single(missEmitters, e => e.EffectId == "paint_sprayer_stream");
            ParticleEmitterComponent impactMiss = Assert.Single(missEmitters, e => e.EffectId == "paint_sprayer_impact");
            Assert.True(streamMiss.Playing);
            Assert.False(impactMiss.Playing);

            //  Switching to RMB hit activates untinted solvent stream + surface mist.
            fixture.Input.HeldButtons.Remove(MouseButton.Left);
            fixture.Input.HeldButtons.Add(MouseButton.Right);
            fixture.Physics.NextHit = new RaycastResult(true, default(Entity), new Vector3(0f, 0f, -3f), Vector3.UnitY);
            fixture.System.Tick(0.016f, fixture.Store);
            List<ParticleEmitterComponent> solventEmitters = CollectComponents<ParticleEmitterComponent>(fixture.Store);
            Assert.Equal(2, solventEmitters.Count);
            Assert.All(solventEmitters, e =>
            {
                Assert.True(e.Playing);
                Assert.Equal(0u, e.TintRgba);
            });
            Assert.Contains(solventEmitters, e => e.EffectId == "paint_sprayer_solvent_stream");
            Assert.Contains(solventEmitters, e => e.EffectId == "paint_sprayer_solvent_impact");

            //  Opening an interaction blocker releases both emitters.
            using (fixture.InteractionState.BlockInteraction())
            {
                fixture.System.Tick(0.016f, fixture.Store);
                Assert.Equal(0, CountComponents<ParticleEmitterComponent>(fixture.Store));
            }

            //  Resuming unblocked RMB re-spawns solvent emitters; non-finite transform releases them.
            fixture.System.Tick(0.016f, fixture.Store);
            Assert.Equal(2, CountComponents<ParticleEmitterComponent>(fixture.Store));

            fixture.Store.AddOrUpdate(player, new TransformComponent(new Vector3(float.NaN, 0f, 0f), Quaternion.Identity, Vector3.One));
            fixture.System.Tick(0.016f, fixture.Store);
            Assert.Equal(0, CountComponents<ParticleEmitterComponent>(fixture.Store));

            //  Restore finite transform and active spray, then verify button release and game-state exit cleanup.
            fixture.Store.AddOrUpdate(player, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            fixture.System.Tick(0.016f, fixture.Store);
            Assert.Equal(2, CountComponents<ParticleEmitterComponent>(fixture.Store));

            fixture.Input.HeldButtons.Clear();
            fixture.System.Tick(0.016f, fixture.Store);
            Assert.Equal(0, CountComponents<ParticleEmitterComponent>(fixture.Store));

            fixture.Input.HeldButtons.Add(MouseButton.Right);
            fixture.System.Tick(0.016f, fixture.Store);
            Assert.Equal(2, CountComponents<ParticleEmitterComponent>(fixture.Store));

            WaywardBeyondGameState.GameState.Set(GameState.MainMenu);
            fixture.System.Tick(0.016f, fixture.Store);
            Assert.Equal(0, CountComponents<ParticleEmitterComponent>(fixture.Store));
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(previous);
        }
    }

    [Fact]
    public async Task ServerPaintSprayerSystemReplicatesGenericEmittersHandlesLocalMuteRemoteRenderAndDisconnects()
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
        itemDb.Add(ToolItem(SPRAYER_ITEM_ID, ToolType.Sprayer));
        itemDb.Add(ToolItem(LASER_ITEM_ID, ToolType.Mining));
        StubPaintDatabase paints = CreatePaintDatabase();
        uint redTint = ParticleEffect.PackTint(paints.Get(RED_PAINT_ID).Value.Color);
        uint cyanTint = ParticleEffect.PackTint(paints.Get(CYAN_PAINT_ID).Value.Color);

        var serverStore = new DataStore();
        using var simStep = new SimulationStep(serverStore, physics);
        var peers = new ServerPeers();
        var sessions = new SessionManager();
        var replication = new NetworkReplicationSystem(peers, sessions, NullLogger<NetworkReplicationSystem>.Instance, new NetworkingConfig(), registry);
        var serverParticles = new ServerParticleService(peers, sessions, replication, NullLogger<ServerParticleService>.Instance);
        var sprayerSystem = new ServerPaintSprayerSystem(physics, itemDb, simStep, serverParticles, paints);

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

            Uuid shooterUuid = Uuid.FromValue(9101);
            int serverEntity = serverStore.Alloc(shooterUuid);
            serverStore.AddOrUpdate(serverEntity, NetworkComponent.Create());
            serverStore.AddOrUpdate(serverEntity, new OwnedCharacterComponent());
            serverStore.AddOrUpdate(serverEntity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            serverStore.AddOrUpdate(serverEntity, new EquipmentComponent(0));
            var inventory = new InventoryComponent(2);
            inventory.Contents![0] = new ItemData(SPRAYER_ITEM_ID, 1, 1);
            inventory.Contents[1] = new ItemData(LASER_ITEM_ID, 1, 1);
            serverStore.AddOrUpdate(serverEntity, inventory);
            serverStore.AddOrUpdate(serverEntity, new InputComponent { PrimaryHeld = false, SecondaryHeld = false });
            sessions.Register(serverStore, serverEntity, serverPeer.GameId, new Session(1));

            //  Remote observer client setup (local PlayerComponent has a different UUID from shooterUuid).
            var remoteObserverStore = new DataStore();
            int remoteMirrorOnObserver = remoteObserverStore.Alloc(shooterUuid);
            remoteObserverStore.AddOrUpdate(remoteMirrorOnObserver, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            remoteObserverStore.AddOrUpdate(remoteMirrorOnObserver, new BodyViewComponent { Body = "wb:human_m" });
            int observerPlayer = remoteObserverStore.Alloc(Uuid.FromValue(9999));
            remoteObserverStore.AddOrUpdate(observerPlayer, new PlayerComponent());

            StubAssetDatabase<ParticleEffect> templateDb = CreateTemplateAssetDatabase();
            var observerParticleSystem = new ParticleSystem(templateDb, new RenderSettings(), new Random(13));
            var observerChildSystem = new ChildSystem();
            var observerParticleSync = new ClientParticleReplicationSystem(inbound, observerParticleSystem, NullLogger<ClientParticleReplicationSystem>.Instance);
            ClientReconcileSystem observerReconcile = CreateClientReconcile(inbound, registry);

            //  1. Non-firing sprayer or non-sprayer tool spawns zero generic emitters.
            sprayerSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.Equal(0, CountComponents<ParticleEmitterComponent>(serverStore));

            serverStore.AddOrUpdate(serverEntity, new EquipmentComponent(1));
            serverStore.AddOrUpdate(serverEntity, new InputComponent { PrimaryHeld = true, SecondaryHeld = true, PaintId = "wb:cyan" });
            sprayerSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.Equal(0, CountComponents<ParticleEmitterComponent>(serverStore));

            //  2. Firing with sprayer spawns nozzle (ParentEntity + ExcludeEntity) and impact (ExcludeEntity) emitters.
            serverStore.AddOrUpdate(serverEntity, new EquipmentComponent(0));
            sprayerSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.Equal(serverEntity, physics.LastIgnoredEntity);
            Assert.Equal(2, CountComponents<ParticleEmitterComponent>(serverStore));

            (int nozzleEntity, ParticleEmitterComponent serverNozzle) = FindEmitterEntity(serverStore, "paint_sprayer_stream");
            (int impactEntity, ParticleEmitterComponent serverImpact) = FindEmitterEntity(serverStore, "paint_sprayer_impact");
            Assert.Equal(cyanTint, serverNozzle.TintRgba);
            Assert.Equal(cyanTint, serverImpact.TintRgba);
            Assert.True(serverNozzle.Playing);
            Assert.True(serverImpact.Playing);

            Assert.True(serverStore.TryGet(nozzleEntity, out ParticleEmitterBindingComponent serverNozzleBinding));
            Assert.Equal(shooterUuid.ToValue(), serverNozzleBinding.ParentEntity);
            Assert.Equal(shooterUuid.ToValue(), serverNozzleBinding.ExcludeEntity);

            Assert.True(serverStore.TryGet(impactEntity, out ParticleEmitterBindingComponent serverImpactBinding));
            Assert.Equal(0UL, serverImpactBinding.ParentEntity);
            Assert.Equal(shooterUuid.ToValue(), serverImpactBinding.ExcludeEntity);

            //  Publish to remote observer over loopback and verify generic emitter rendering + parent tracking.
            replication.PublishStage(1f, serverStore);
            await WaitForSnapshotsAsync(inbound, expectedCount: 1);
            TickReconcileWhilePlaying(observerReconcile, remoteObserverStore);

            Assert.Equal(2, CountComponents<ParticleEmitterComponent>(remoteObserverStore));

            //  3. Steady aim leaves emitter components and transforms clean (0 re-dirtied snapshots).
            Assert.False(serverStore.IsDirty<ParticleEmitterComponent>(nozzleEntity));
            Assert.False(serverStore.IsDirty<ParticleEmitterComponent>(impactEntity));
            Assert.False(serverStore.IsDirty<TransformComponent>(nozzleEntity));
            Assert.False(serverStore.IsDirty<TransformComponent>(impactEntity));
            sprayerSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.False(serverStore.IsDirty<ParticleEmitterComponent>(nozzleEntity));
            Assert.False(serverStore.IsDirty<TransformComponent>(nozzleEntity));

            //  4. Moving the player while maintaining relative aim updates without dirtying nozzle TransformComponent.
            serverStore.AddOrUpdate(serverEntity, new TransformComponent(new Vector3(3f, 0f, 0f), Quaternion.Identity, Vector3.One));
            physics.NextHit = new RaycastResult(hit: true, default(Entity), new Vector3(3f, 0f, -4f), hitNormal);
            sprayerSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.False(serverStore.IsDirty<TransformComponent>(nozzleEntity));

            //  5. Untrusted/texture PaintId normalizes to red swatch tint.
            serverStore.AddOrUpdate(serverEntity, new InputComponent { PrimaryHeld = true, PaintId = "malicious:id" });
            sprayerSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.True(serverStore.TryGet(nozzleEntity, out ParticleEmitterComponent redNozzle));
            Assert.Equal(redTint, redNozzle.TintRgba);

            //  6. Secondary fire switches to untinted solvent effects and miss pauses impact without dirtying impact TransformComponent.
            physics.NextHit = default;
            serverStore.AddOrUpdate(serverEntity, new InputComponent { PrimaryHeld = false, SecondaryHeld = true, PaintId = "wb:cyan" });
            serverStore.ClearDirty<TransformComponent>(impactEntity);
            sprayerSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.True(serverStore.TryGet(nozzleEntity, out ParticleEmitterComponent solventNozzle));
            Assert.True(serverStore.TryGet(impactEntity, out ParticleEmitterComponent solventImpact));
            Assert.Equal("paint_sprayer_solvent_stream", solventNozzle.EffectId);
            Assert.Equal("paint_sprayer_solvent_impact", solventImpact.EffectId);
            Assert.Equal(0u, solventNozzle.TintRgba);
            Assert.Equal(0u, solventImpact.TintRgba);
            Assert.False(solventImpact.Playing);
            Assert.False(serverStore.IsDirty<TransformComponent>(impactEntity));

            //  Replicate solvent state to observer and verify observer emits solvent particles, while shooter store mutes them.
            replication.PublishStage(1f, serverStore);
            await WaitForSnapshotsAsync(inbound, expectedCount: 1);
            TickReconcileWhilePlaying(observerReconcile, remoteObserverStore);

            GameState previous = WaywardBeyondGameState.GameState.Get();
            WaywardBeyondGameState.GameState.Set(GameState.Playing);
            try
            {
                observerParticleSync.Tick(0.016f, remoteObserverStore);
                observerChildSystem.Tick(0.05f, remoteObserverStore);
                observerParticleSystem.Tick(0.05f, remoteObserverStore);
                Assert.True(observerParticleSystem.ActiveParticleCount > 0);
                Assert.False(remoteObserverStore.Has(typeof(ParticleEmitterMutedComponent), FindEntity(remoteObserverStore, "paint_sprayer_solvent_stream")));

                //  Shooter's own client store mutes both replicated emitters via ExcludeEntity == shooterUuid.
                var shooterClientStore = new DataStore();
                int shooterMirror = shooterClientStore.Alloc(shooterUuid);
                shooterClientStore.AddOrUpdate(shooterMirror, new PlayerComponent());
                shooterClientStore.AddOrUpdate(shooterMirror, new TransformComponent(new Vector3(3f, 0f, 0f), Quaternion.Identity, Vector3.One));
                int clientNozzle = shooterClientStore.Alloc(serverStore.GetUuid(nozzleEntity));
                shooterClientStore.AddOrUpdate(clientNozzle, new TransformComponent(Vector3.Zero));
                shooterClientStore.AddOrUpdate(clientNozzle, solventNozzle);
                shooterClientStore.AddOrUpdate(clientNozzle, serverNozzleBinding);
                int clientImpact = shooterClientStore.Alloc(serverStore.GetUuid(impactEntity));
                shooterClientStore.AddOrUpdate(clientImpact, new TransformComponent(Vector3.Zero));
                shooterClientStore.AddOrUpdate(clientImpact, solventImpact);
                shooterClientStore.AddOrUpdate(clientImpact, serverImpactBinding);

                var shooterParticles = new ParticleSystem(templateDb, new RenderSettings(), new Random(13));
                var shooterSync = new ClientParticleReplicationSystem(new ClientInbound(), shooterParticles, NullLogger<ClientParticleReplicationSystem>.Instance);
                shooterSync.Tick(0.016f, shooterClientStore);
                shooterParticles.Tick(0.05f, shooterClientStore);
                Assert.True(shooterClientStore.Has(typeof(ParticleEmitterMutedComponent), clientNozzle));
                Assert.True(shooterClientStore.Has(typeof(ParticleEmitterMutedComponent), clientImpact));
                Assert.Equal(0, shooterParticles.ActiveParticleCount);
            }
            finally
            {
                WaywardBeyondGameState.GameState.Set(previous);
            }

            //  7. Releasing fire despawns both emitters on server and replicates removal to client.
            serverStore.AddOrUpdate(serverEntity, new InputComponent { PrimaryHeld = false, SecondaryHeld = false });
            sprayerSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.Equal(0, CountComponents<ParticleEmitterComponent>(serverStore));

            replication.PublishStage(1f, serverStore);
            await WaitForSnapshotsAsync(inbound, expectedCount: 1);
            TickReconcileWhilePlaying(observerReconcile, remoteObserverStore);
            Assert.Equal(0, CountComponents<ParticleEmitterComponent>(remoteObserverStore));

            //  8. Player disconnect while firing sweeps stale emitter pair cleanly.
            physics.NextHit = new RaycastResult(hit: true, default(Entity), new Vector3(3f, 0f, -4f), hitNormal);
            serverStore.AddOrUpdate(serverEntity, new InputComponent { PrimaryHeld = true, PaintId = "wb:red" });
            sprayerSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.Equal(2, CountComponents<ParticleEmitterComponent>(serverStore));

            serverStore.Free(serverEntity);
            sprayerSystem.Tick(serverStore, simStep.CurrentSimTick);
            Assert.Equal(0, CountComponents<ParticleEmitterComponent>(serverStore));
        }
        finally
        {
            connection.Close();
            rpcServer.Close();
            transport.Close();
        }
    }

    [Fact]
    public void PaintFaceParticleListenerEmitsApplyAndClearBurstsAndIgnoresInvalidEvents()
    {
        StubPaintDatabase paints = CreatePaintDatabase();
        var particles = new RecordingParticleService();
        var listener = new PaintFaceParticleListener(particles, paints);
        var invoker = new EventInvoker<PaintFaceEvent>([listener]);

        uint cyanTint = ParticleEffect.PackTint(paints.Get(CYAN_PAINT_ID).Value.Color);
        uint redTint = ParticleEffect.PackTint(paints.Get(RED_PAINT_ID).Value.Color);
        var worldPos = new Vector3(2f, 3f, -4f);
        var normal = new Vector3(0f, 0f, 2f);

        //  Apply emits 1 color-tinted splatter burst offset by 0.04 along normalized surface normal.
        invoker.Invoke(new PaintFaceEvent(PaintKind.Color, PaintFaceAction.Apply, CYAN_PAINT_ID, worldPos, normal));
        RecordedBurst applyBurst = Assert.Single(particles.Bursts);
        Assert.Equal("paint_sprayer_coat", applyBurst.EffectId);
        Assert.Equal(cyanTint, applyBurst.TintRgba);
        Assert.Equal(new Vector3(2f, 3f, -3.96f), applyBurst.Position);
        Assert.Equal(Vector3.UnitZ, applyBurst.Direction);
        Assert.Null(applyBurst.Count);
        particles.Bursts.Clear();

        //  Clear with known stripped color emits both solvent (count 10, tint 0) and stripped color flecks (count 10, tinted).
        invoker.Invoke(new PaintFaceEvent(PaintKind.Color, PaintFaceAction.Clear, RED_PAINT_ID, worldPos, Vector3.UnitY));
        Assert.Equal(2, particles.Bursts.Count);
        Assert.Equal("paint_sprayer_solvent_coat", particles.Bursts[0].EffectId);
        Assert.Equal(0u, particles.Bursts[0].TintRgba);
        Assert.Equal(10, particles.Bursts[0].Count);
        Assert.Equal("paint_sprayer_coat", particles.Bursts[1].EffectId);
        Assert.Equal(redTint, particles.Bursts[1].TintRgba);
        Assert.Equal(10, particles.Bursts[1].Count);
        particles.Bursts.Clear();

        //  Clear with paintId == 0 emits only solvent burst.
        invoker.Invoke(new PaintFaceEvent(PaintKind.Color, PaintFaceAction.Clear, paintId: 0, worldPos, Vector3.UnitY));
        RecordedBurst solventOnly = Assert.Single(particles.Bursts);
        Assert.Equal("paint_sprayer_solvent_coat", solventOnly.EffectId);
        Assert.Equal(0u, solventOnly.TintRgba);
        Assert.Equal(10, solventOnly.Count);
        particles.Bursts.Clear();

        //  Texture events and non-finite positions/normals are ignored.
        invoker.Invoke(new PaintFaceEvent(PaintKind.Texture, PaintFaceAction.Apply, CAUTION_TEX_ID, worldPos, Vector3.UnitY));
        invoker.Invoke(new PaintFaceEvent(PaintKind.Color, PaintFaceAction.Apply, RED_PAINT_ID, new Vector3(float.NaN, 0f, 0f), Vector3.UnitY));
        invoker.Invoke(new PaintFaceEvent(PaintKind.Color, PaintFaceAction.Apply, RED_PAINT_ID, worldPos, new Vector3(0f, float.PositiveInfinity, 0f)));
        Assert.Empty(particles.Bursts);
    }

    [Fact]
    public void LocalAndRemoteFacePaintHooksRaiseEventsAndSuppressPredictedEcho()
    {
        StubPaintDatabase paints = CreatePaintDatabase();
        var bricks = new StubBrickRegistryDatabase();
        var particles = new RecordingParticleService();
        var listener = new PaintFaceParticleListener(particles, paints);
        var paintFaceInvoker = new EventInvoker<PaintFaceEvent>([listener]);

        uint cyanTint = ParticleEffect.PackTint(paints.Get(CYAN_PAINT_ID).Value.Color);
        uint redTint = ParticleEffect.PackTint(paints.Get(RED_PAINT_ID).Value.Color);

        var originalOverrides = new FaceOverride[6];
        originalOverrides[(int)BrickFace.Top] = new FaceOverride(texturePaintId: 0, colorPaintId: RED_PAINT_ID);
        var hitPos = new Vector3(1.5f, 0.5f, -2.5f);

        PlayerInteractionService.FirePaintFaceEvent(
            InteractionKind.PrimaryPressed,
            new PaintResolution(BrickFaceMask.Top, PaintKind.Color, CYAN_PAINT_ID, "wb:cyan"),
            originalOverrides,
            hitPos,
            Vector3.UnitY,
            paintFaceInvoker
        );
        RecordedBurst localApply = Assert.Single(particles.Bursts);
        Assert.Equal("paint_sprayer_coat", localApply.EffectId);
        Assert.Equal(cyanTint, localApply.TintRgba);
        particles.Bursts.Clear();

        PlayerInteractionService.FirePaintFaceEvent(
            InteractionKind.SecondaryPressed,
            new PaintResolution(BrickFaceMask.Top, PaintKind.Color, paintId: 0, canonicalPaintId: null),
            originalOverrides,
            hitPos,
            Vector3.UnitY,
            paintFaceInvoker
        );
        Assert.Equal(2, particles.Bursts.Count);
        Assert.Equal("paint_sprayer_solvent_coat", particles.Bursts[0].EffectId);
        Assert.Equal(0u, particles.Bursts[0].TintRgba);
        Assert.Equal("paint_sprayer_coat", particles.Bursts[1].EffectId);
        Assert.Equal(redTint, particles.Bursts[1].TintRgba);
        particles.Bursts.Clear();

        PlayerInteractionService.FirePaintFaceEvent(
            InteractionKind.PrimaryPressed,
            new PaintResolution(BrickFaceMask.Top, PaintKind.Texture, CAUTION_TEX_ID, "wb:caution"),
            originalOverrides,
            hitPos,
            Vector3.UnitY,
            paintFaceInvoker
        );
        Assert.Empty(particles.Bursts);

        var store = new DataStore();
        var voxelObject = new VoxelObject(chunkSize: 16);
        voxelObject.Set(1, 2, 3, new Voxel(BRICK_ID, new ShapeLight(BrickShape.Block, 0), 0));

        var structureOrigin = new Vector3(10f, 0f, -4f);
        Quaternion structureRot = Quaternion.CreateFromAxisAngle(Vector3.UnitY, MathF.PI * 0.5f);
        int structure = store.Alloc();
        Uuid structureUuid = store.GetUuid(structure);
        store.AddOrUpdate(structure, new TransformComponent(structureOrigin, structureRot, Vector3.One));
        store.AddOrUpdate(structure, new VoxelComponent(voxelObject));

        var queue = new PendingInteractionQueue();
        int localPlayer = store.Alloc();
        store.AddOrUpdate(localPlayer, new PlayerComponent());
        store.AddOrUpdate(localPlayer, new PendingInteractionComponent(queue));

        var inbound = new ClientInbound();
        var lighting = new VoxelLightingService(bricks);
        var reconcile = new ClientVoxelReconcileSystem(
            inbound,
            new SnapshotAckTracker(),
            bricks,
            lighting,
            new EventInvoker<BrickEvent>([]),
            paints,
            paintFaceInvoker
        );

        GameState previous = WaywardBeyondGameState.GameState.Get();
        WaywardBeyondGameState.GameState.Set(GameState.Playing);
        try
        {
            queue.RegisterFaceEdit(new PendingFaceEdit(
                structure,
                new Int3(1, 2, 3),
                BrickFaceMask.Front,
                PaintKind.Color,
                new FaceOverride[6],
                predictedPaintId: "wb:red",
                sequence: 42,
                serverTickAtSample: 0
            ));
            inbound.VoxelFaceEdits.Enqueue(new VoxelFaceEditMessage
            {
                EntityUuid = structureUuid.ToValue(),
                X = 1,
                Y = 2,
                Z = 3,
                FaceMask = (byte)BrickFaceMask.Front,
                Mode = (byte)PaintKind.Color,
                PaintId = "wb:red",
                Sequence = 42,
            });
            reconcile.Tick(0.016f, store);
            Assert.Empty(particles.Bursts);

            inbound.VoxelFaceEdits.Enqueue(new VoxelFaceEditMessage
            {
                EntityUuid = structureUuid.ToValue(),
                X = 1,
                Y = 2,
                Z = 3,
                FaceMask = (byte)BrickFaceMask.Front,
                Mode = (byte)PaintKind.Color,
                PaintId = "wb:red",
                Sequence = 0,
            });
            reconcile.Tick(0.016f, store);

            RecordedBurst remoteApply = Assert.Single(particles.Bursts);
            Assert.Equal("paint_sprayer_coat", remoteApply.EffectId);
            Assert.Equal(redTint, remoteApply.TintRgba);
            Vector3 rotatedFront = Vector3.Normalize(Vector3.Transform(Vector3.UnitZ, structureRot));
            Vector3 expectedWorldPos = InteractionResolver.BrickToWorldSpace(new Int3(1, 2, 3), structureOrigin, structureRot)
                + rotatedFront * 0.54f;
            Assert.Equal(expectedWorldPos.X, remoteApply.Position.X, precision: 3);
            Assert.Equal(expectedWorldPos.Y, remoteApply.Position.Y, precision: 3);
            Assert.Equal(expectedWorldPos.Z, remoteApply.Position.Z, precision: 3);
            Assert.Equal(rotatedFront.X, remoteApply.Direction.X, precision: 3);
            Assert.Equal(rotatedFront.Z, remoteApply.Direction.Z, precision: 3);
            particles.Bursts.Clear();

            inbound.VoxelFaceEdits.Enqueue(new VoxelFaceEditMessage
            {
                EntityUuid = structureUuid.ToValue(),
                X = 1,
                Y = 2,
                Z = 3,
                FaceMask = (byte)BrickFaceMask.Front,
                Mode = (byte)PaintKind.Color,
                PaintId = null,
                Sequence = 0,
            });
            reconcile.Tick(0.016f, store);

            Assert.Equal(2, particles.Bursts.Count);
            Assert.Equal("paint_sprayer_solvent_coat", particles.Bursts[0].EffectId);
            Assert.Equal(0u, particles.Bursts[0].TintRgba);
            Assert.Equal("paint_sprayer_coat", particles.Bursts[1].EffectId);
            Assert.Equal(redTint, particles.Bursts[1].TintRgba);
            particles.Bursts.Clear();

            inbound.VoxelFaceEdits.Enqueue(new VoxelFaceEditMessage
            {
                EntityUuid = structureUuid.ToValue(),
                X = 1,
                Y = 2,
                Z = 3,
                FaceMask = (byte)BrickFaceMask.Top,
                Mode = (byte)PaintKind.Texture,
                PaintId = "wb:caution",
                Sequence = 0,
            });
            reconcile.Tick(0.016f, store);
            Assert.Empty(particles.Bursts);
        }
        finally
        {
            WaywardBeyondGameState.GameState.Set(previous);
        }
    }

    private static SprayerFixture CreateSprayerFixture()
    {
        var itemDb = new StubItemDatabase();
        itemDb.Add(ToolItem(SPRAYER_ITEM_ID, ToolType.Sprayer));
        itemDb.Add(ToolItem(LASER_ITEM_ID, ToolType.Mining));

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
        StubPaintDatabase paints = CreatePaintDatabase();
        var system = new PaintSprayerEffectSystem(
            in playerData,
            in interactionState,
            in input,
            in physics,
            paints
        );

        return new SprayerFixture(new DataStore(), system, fakeInput, stubPhysics, interactionState, paints);
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

    private static StubAssetDatabase<ParticleEffect> CreateTemplateAssetDatabase()
    {
        return new StubAssetDatabase<ParticleEffect>
        {
            Assets =
            {
                ["paint_sprayer_stream"] = CreateTemplate("paint_sprayer_stream", rate: 75f, burstCount: 8, spread: 13f, startAlpha: 0.72f),
                ["paint_sprayer_impact"] = CreateTemplate("paint_sprayer_impact", rate: 48f, burstCount: 6, spread: 72f, startAlpha: 0.65f),
                ["paint_sprayer_coat"] = CreateTemplate("paint_sprayer_coat", rate: 0f, burstCount: 18, spread: 78f, startAlpha: 0.90f),
                ["paint_sprayer_solvent_stream"] = CreateTemplate("paint_sprayer_solvent_stream", rate: 75f, burstCount: 8, spread: 13f, startAlpha: 0.72f),
                ["paint_sprayer_solvent_impact"] = CreateTemplate("paint_sprayer_solvent_impact", rate: 48f, burstCount: 6, spread: 72f, startAlpha: 0.65f),
                ["paint_sprayer_solvent_coat"] = CreateTemplate("paint_sprayer_solvent_coat", rate: 0f, burstCount: 18, spread: 78f, startAlpha: 0.90f),
            },
        };
    }

    private static ParticleEffect CreateTemplate(string id, float rate, int burstCount, float spread, float startAlpha)
    {
        return new ParticleEffect(
            id,
            texture: null,
            ParticleBlendMode.Alpha,
            rate,
            burstCount,
            maxParticles: 160,
            new ParticleRange(0.16f, 0.34f),
            new ParticleRange(2f, 6f),
            spread,
            spawnRadius: 0.04f,
            new ParticleRange(0.03f, 0.06f),
            new ParticleRange(0.06f, 0.12f),
            new Vector4(1f, 1f, 1f, startAlpha),
            new Vector4(0.5f, 0.55f, 0.65f, startAlpha),
            new Vector4(1f, 1f, 1f, 0f),
            emissiveIntensity: 0f,
            new Vector3(0f, -0.5f, 0f),
            drag: 4f,
            new ParticleRange(0f, 360f),
            new ParticleRange(-90f, 90f)
        );
    }

    private static StubPaintDatabase CreatePaintDatabase()
    {
        var paints = new StubPaintDatabase();
        paints.Add(RED_PAINT_ID, Paint.CreateColor("wb:red", "red", new Vector3(228f / 255f, 59f / 255f, 68f / 255f)));
        paints.Add(CYAN_PAINT_ID, Paint.CreateColor("wb:cyan", "cyan", new Vector3(44f / 255f, 232f / 255f, 245f / 255f)));
        paints.Add(CAUTION_TEX_ID, Paint.CreateTexture("wb:caution", "caution", BrickAlpha.Opaque, false, ["caution"]));
        return paints;
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

    private static (int Entity, ParticleEmitterComponent Component) FindEmitterEntity(DataStore store, string effectId)
    {
        var foundEntity = Entity.Null;
        ParticleEmitterComponent foundComponent = default;
        store.Query<ParticleEmitterComponent>(0f, (float _, DataStore _, int entity, in ParticleEmitterComponent emitter) =>
        {
            if (emitter.EffectId == effectId)
            {
                foundEntity = entity;
                foundComponent = emitter;
            }
        });

        Assert.NotEqual(Entity.Null, foundEntity);
        return (foundEntity, foundComponent);
    }

    private static int FindEntity(DataStore store, string effectId) => FindEmitterEntity(store, effectId).Entity;

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

    private sealed class SprayerFixture(
        DataStore store,
        PaintSprayerEffectSystem system,
        FakeInputService input,
        StubClientPhysics physics,
        InteractionState interactionState,
        StubPaintDatabase paints
    ) {
        public readonly DataStore Store = store;
        public readonly PaintSprayerEffectSystem System = system;
        public readonly FakeInputService Input = input;
        public readonly StubClientPhysics Physics = physics;
        public readonly InteractionState InteractionState = interactionState;
        public readonly StubPaintDatabase Paints = paints;
    }

    private readonly record struct RecordedBurst(
        string EffectId,
        Vector3 Position,
        Vector3 Direction,
        int? Count,
        float SpeedScale,
        float SizeScale,
        uint TintRgba
    );

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
                : Result<T>.FromFailure($"Unknown asset \"{id}\"");
        }
    }

    private sealed class RecordingParticleService : IParticleService
    {
        public readonly List<RecordedBurst> Bursts = [];

        public int ActiveParticleCount => 0;

        public void EmitBurst(in ParticleBurstRequest request) { }

        public Result EmitBurst(string effectId, Vector3 position, Vector3 direction, int? count = null)
        {
            return EmitBurst(effectId, position, direction, count, speedScale: 1f, sizeScale: 1f, tintRgba: 0u);
        }

        public Result EmitBurst(
            string effectId,
            Vector3 position,
            Vector3 direction,
            int? count,
            float speedScale,
            float sizeScale,
            uint tintRgba = 0u
        ) {
            Bursts.Add(new RecordedBurst(effectId, position, direction, count, speedScale, sizeScale, tintRgba));
            return Result.FromSuccess();
        }

        public void Clear() { }
    }

    private sealed class StubPaintDatabase : IPaintDatabase, IPaintRegistry
    {
        private readonly Dictionary<ushort, Paint> _byId = [];
        private readonly Dictionary<string, ushort> _idByName = new(StringComparer.Ordinal);

        public void Add(ushort id, Paint paint)
        {
            paint.BindDataID(id);
            _byId[id] = paint;
            _idByName[paint.ID] = id;
        }

        public IReadOnlyList<Paint> TexturePaints => [.. _byId.Values.Where(p => p.Kind == PaintKind.Texture)];
        public IReadOnlyList<Paint> ColorPaints => [.. _byId.Values.Where(p => p.Kind == PaintKind.Color)];
        public ushort Id(string name) => _idByName.TryGetValue(name, out ushort id) ? id : (ushort)0;
        public string? Name(ushort id) => _byId.TryGetValue(id, out Paint? p) ? p.ID : null;

        public Result<Paint> Get(string id)
        {
            return _idByName.TryGetValue(id, out ushort num)
                ? Result<Paint>.FromSuccess(_byId[num])
                : Result<Paint>.FromFailure((string?)null);
        }

        public Result<Paint> Get(ushort id)
        {
            return _byId.TryGetValue(id, out Paint? p)
                ? Result<Paint>.FromSuccess(p)
                : Result<Paint>.FromFailure((string?)null);
        }

        public List<Paint> Get(Func<Paint, bool> predicate) => [.. _byId.Values.Where(predicate)];
    }

    private sealed class StubBrickRegistryDatabase : IBrickDatabase, IBrickRegistry
    {
        private readonly Brick _panel = new("wb:panel", BrickAlpha.Opaque, passable: false, meshID: null, BrickShape.Block, default, sounds: null, paintable: true);

        public StubBrickRegistryDatabase()
        {
            _panel.BindDataID(BRICK_ID);
        }

        public Result<Brick> Get(string id) => id == "wb:panel" ? Result<Brick>.FromSuccess(_panel) : Result<Brick>.FromFailure((string?)null);
        public Result<Brick> Get(ushort id) => id == BRICK_ID ? Result<Brick>.FromSuccess(_panel) : Result<Brick>.FromFailure((string?)null);
        public List<Brick> Get(Func<Brick, bool> predicate) => [];
        public bool IsCuller(in Voxel voxel, BrickShape shape) => voxel.ID != 0 && shape == BrickShape.Block;
        public ushort Id(string name) => name == "wb:panel" ? BRICK_ID : (ushort)0;
        public ushort Resolve(string name) => Id(name);
        public string? Name(ushort id) => id == BRICK_ID ? "wb:panel" : null;
    }

    private sealed class StubRpcHost : IRpcHost
    {
        public PeerSet Peers => default;
    }
}
