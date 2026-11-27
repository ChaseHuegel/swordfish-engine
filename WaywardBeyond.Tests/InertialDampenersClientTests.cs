#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Input;
using Swordfish.Library.IO;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Swordfish.Settings;
using Swordfish.Tests;
using WaywardBeyond.Client.Components;
using WaywardBeyond.Client.Configuration;
using WaywardBeyond.Client.Events;
using WaywardBeyond.Client.Networking;
using WaywardBeyond.Client.Player;
using WaywardBeyond.Client.Shortcuts;
using WaywardBeyond.Client.Systems;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Networking;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Registry;
using WaywardBeyond.Server.Systems;
using Xunit;
using ClientGameState = WaywardBeyond.Client.GameState;
using ClientHost = WaywardBeyond.Client.WaywardBeyond;

namespace WaywardBeyond.Tests;

/// <summary>
/// Pins the client dampener toggle shortcut, modal/text-input suppression, state exposure, and
/// client/server prediction parity across mid-flight dampener transitions.
/// </summary>
[Collection("ClientGameState")]
public class InertialDampenersClientTests
{
    [Fact]
    public void ShortcutTogglesDampenersEnabledAndRespectsRebindingAndSessionReset()
    {
        using var harness = new ShortcutHarness();
        ClientHost.GameState.Set(ClientGameState.Playing);

        Assert.True(harness.InputSystem.DampenersEnabled);
        harness.TickInput();
        Assert.True(harness.Store.TryGet(harness.Player, out InputComponent initial));
        Assert.False(initial.DampenersDisabled);
        harness.Store.ClearDirty<InputComponent>(harness.Player);

        //  Press Z to disable dampeners: immediate state flip, immediate InputComponent dirty + stage.
        harness.PressKey(Key.Z);
        Assert.False(harness.InputSystem.DampenersEnabled);
        harness.TickInput();
        Assert.True(harness.Store.IsDirty<InputComponent>(harness.Player));
        Assert.True(harness.Store.TryGet(harness.Player, out InputComponent disabled));
        Assert.True(disabled.DampenersDisabled);
        harness.Store.ClearDirty<InputComponent>(harness.Player);

        //  Rebind to X and confirm Z no longer toggles while X re-enables dampeners.
        Assert.True(harness.Shortcuts.Rebind(GameShortcuts.TOGGLE_DAMPENERS, new ShortcutBinding(Key.X)).Success);
        harness.PressKey(Key.Z);
        Assert.False(harness.InputSystem.DampenersEnabled);

        harness.PressKey(Key.X);
        Assert.True(harness.InputSystem.DampenersEnabled);
        harness.TickInput();
        Assert.True(harness.Store.IsDirty<InputComponent>(harness.Player));
        Assert.True(harness.Store.TryGet(harness.Player, out InputComponent reenabled));
        Assert.False(reenabled.DampenersDisabled);

        //  ResetSession restores dampeners ON for the next world session.
        harness.PressKey(Key.X);
        Assert.False(harness.InputSystem.DampenersEnabled);
        harness.InputSystem.ResetSession(harness.PlayerUuid);
        Assert.True(harness.InputSystem.DampenersEnabled);
    }

    [Fact]
    public void ShortcutIsSuppressedOutsidePlayingAndWhileModalsOrTextInputBlockInteraction()
    {
        using var harness = new ShortcutHarness();

        //  Suppressed in MainMenu and Paused states.
        ClientHost.GameState.Set(ClientGameState.MainMenu);
        harness.PressKey(Key.Z);
        Assert.True(harness.InputSystem.DampenersEnabled);

        ClientHost.GameState.Set(ClientGameState.Paused);
        harness.PressKey(Key.Z);
        Assert.True(harness.InputSystem.DampenersEnabled);

        //  Active while Playing with no modal blockers.
        ClientHost.GameState.Set(ClientGameState.Playing);
        harness.PressKey(Key.Z);
        Assert.False(harness.InputSystem.DampenersEnabled);

        //  Suppressed while a modal or text input holds an InteractionBlocker (e.g. chat/inventory/blueprint).
        using (InteractionState.InteractionBlocker modalBlocker = harness.InteractionState.BlockInteraction())
        {
            harness.PressKey(Key.Z);
            Assert.False(harness.InputSystem.DampenersEnabled);

            //  Sampling while a modal is open preserves the existing DampenersDisabled state without slamming brakes.
            harness.TickInput();
            Assert.True(harness.Store.TryGet(harness.Player, out InputComponent whileBlocked));
            Assert.True(whileBlocked.DampenersDisabled);
        }

        //  Restored after modal closes.
        harness.PressKey(Key.Z);
        Assert.True(harness.InputSystem.DampenersEnabled);
    }

    [Fact]
    public void ClientAndServerMaintainIdenticalTrajectoriesAcrossMidFlightToggleTransitions()
    {
        try
        {
            ClientHost.GameState.Set(ClientGameState.Playing);

            var clientPhysics = new FakePhysics();
            var physicsSettings = new PhysicsSettings();
            physicsSettings.Gravity.Set(Vector3.Zero);
            physicsSettings.FixedTimestamp.Set(clientPhysics.FixedTimestamp);
            var serverPhysics = new ServerPhysicsSystem(NullLoggerFactory.Instance, physicsSettings);
            var serverStore = new DataStore();
            var protocol = new ProtocolConfig();
            uint maxAge = (uint)protocol.CommandMaxAgeTicks.Get();
            using var serverStep = new SimulationStep(serverStore, serverPhysics, commandMaxAgeTicks: maxAge);

            Uuid playerUuid = Uuid.FromValue(9001);
            int serverPlayer = serverStore.Alloc(playerUuid);
            serverStore.AddOrUpdate(serverPlayer, NetworkComponent.Create());
            serverStore.AddOrUpdate(serverPlayer, new InputComponent());
            serverStore.AddOrUpdate(serverPlayer, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            serverStore.AddOrUpdate(serverPlayer, CharacterDefaults.CreateCharacterPhysicsComponent());
            serverStore.AddOrUpdate(serverPlayer, CharacterDefaults.CreateCharacterColliderComponent(CharacterDefaults.PlayerScale));

            var clientInputService = new FakeInputService();
            var clientShortcuts = new ShortcutService(clientInputService, NullLogger.Instance);
            var motionProcessor = new ClientPlayerMotionProcessor(
                clientInputService,
                new FakeWindowContext(),
                clientPhysics,
                new EventInvoker<PlayerMovedEvent>([]),
                protocol
            );
            var interactionState = new InteractionState(motionProcessor);
            motionProcessor.SetInputEnabled(true);

            var clientStore = new DataStore();
            int clientPlayer = clientStore.Alloc(playerUuid);
            clientStore.AddOrUpdate(clientPlayer, new PlayerComponent());
            clientStore.AddOrUpdate(clientPlayer, new InputComponent());
            clientStore.AddOrUpdate(clientPlayer, PendingInputComponent.Create());
            clientStore.AddOrUpdate(clientPlayer, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
            clientStore.AddOrUpdate(clientPlayer, CharacterDefaults.CreateCharacterPhysicsComponent());

            var snapshotAck = new SnapshotAckTracker();
            var clientInput = new ClientInputSystem(motionProcessor, clientShortcuts, new ControlSettings(), snapshotAck, interactionState);
            var inbound = new ClientInbound();
            var registry = CreateRegistry();
            var reconciler = new LocalPlayerReconciler(NullLogger<LocalPlayerReconciler>.Instance);
            var clientReconcile = new ClientReconcileSystem(
                inbound,
                snapshotAck,
                motionProcessor,
                reconciler,
                registry,
                NullLogger<ClientReconcileSystem>.Instance
            );

            //  Initialize client SimulationStep lazily before stepping.
            motionProcessor.Tick(0.016f, clientStore);

            float dt = clientPhysics.FixedTimestamp;
            var inputCodec = new NsdComponentCodec<InputComponent>();
            var transformCodec = new TransformCodec();
            var physicsCodec = new PhysicsCodec();
            uint serverLastAcked = 0;

            void RunJointTick(bool deliverSnapshot)
            {
                //  1. Client samples input and uploads dirty InputComponent to server.
                clientInput.Tick(dt, clientStore);
                if (clientStore.IsDirty<InputComponent>(clientPlayer))
                {
                    Result<byte[]> wire = inputCodec.Serialize(clientStore, clientPlayer);
                    Assert.True(wire.Success);
                    clientStore.ClearDirty<InputComponent>(clientPlayer);

                    var tempStore = new DataStore();
                    int tempEntity = tempStore.Alloc(playerUuid);
                    Assert.True(inputCodec.Apply(tempStore, tempEntity, wire.Value).Success);
                    Assert.True(tempStore.TryGet(tempEntity, out InputComponent decoded));
                    Assert.True(serverStore.TryGet(serverPlayer, out NetworkComponent net));
                    net.StagedInputs!.Stage(in decoded, serverStep.CurrentSimTick);
                    serverLastAcked = Math.Max(serverLastAcked, decoded.ServerTickAtSample);
                }

                //  2. Both worlds advance one fixed sim step at matching ticks.
                motionProcessor.Step!.Step();
                serverStep.Step();

                //  Integrate position deterministically from the stepped velocity for parity check.
                IntegratePosition(clientStore, clientPlayer, dt);
                IntegratePosition(serverStore, serverPlayer, dt);

                Assert.True(clientStore.TryGet(clientPlayer, out PhysicsComponent clientPhysBefore));
                Assert.True(serverStore.TryGet(serverPlayer, out PhysicsComponent serverPhysBefore));
                Assert.Equal(serverPhysBefore.Velocity, clientPhysBefore.Velocity);
                Assert.Equal(serverPhysBefore.Torque, clientPhysBefore.Torque);

                //  3. Periodically publish authoritative server snapshot and reconcile on client.
                if (deliverSnapshot)
                {
                    Assert.True(serverStore.TryGet(serverPlayer, out TransformComponent srvTransform));
                    Assert.True(serverStore.TryGet(serverPlayer, out PhysicsComponent srvPhysics));
                    byte[] tBytes = transformCodec.Serialize(in srvTransform);
                    byte[] pBytes = physicsCodec.Serialize(in srvPhysics);

                    var snapshot = new WorldSnapshot
                    {
                        TickNumber = serverStep.CurrentSimTick,
                        LastProcessedInput = serverLastAcked,
                        Components =
                        [
                            new ComponentSnapshot(playerUuid.ToValue(), Uuid.FromValue(2).ToValue(), tBytes),
                            new ComponentSnapshot(playerUuid.ToValue(), Uuid.FromValue(3).ToValue(), pBytes),
                        ],
                        RemovedEntities = [],
                    };
                    inbound.Snapshots.Enqueue(new ClientInbound.InboundSnapshot(snapshot));

                    clientStore.ClearDirty<PhysicsComponent>(clientPlayer);
                    clientReconcile.Tick(dt, clientStore);

                    //  Zero rubberband: authoritative snapshot matches predicted velocity cleanly.
                    Assert.False(clientStore.IsDirty<PhysicsComponent>(clientPlayer));
                    Assert.True(clientStore.TryGet(clientPlayer, out TransformComponent cliTransform));
                    Assert.Equal(srvTransform.Position, cliTransform.Position);
                }
            }

            //  Phase 1: Accelerate forward with dampeners ON for 18 ticks (toggling mid-flight between snapshots).
            clientInputService.HeldKeys.Add(Key.W);
            for (var tick = 0; tick < 18; tick++)
            {
                RunJointTick(deliverSnapshot: tick % 4 == 3);
            }

            Assert.True(serverStore.TryGet(serverPlayer, out PhysicsComponent afterThrust));
            Assert.True(afterThrust.Velocity.Z < -1f);

            //  Phase 2: Release W and toggle dampeners OFF mid-flight; coast across 24 ticks with snapshots.
            clientInputService.HeldKeys.Remove(Key.W);
            clientInputService.HeldKeys.Add(Key.Z);
            clientInputService.KeyPressed?.Invoke(clientInputService, new KeyEventArgs(Key.Z));
            clientInputService.HeldKeys.Remove(Key.Z);
            clientInputService.KeyReleased?.Invoke(clientInputService, new KeyEventArgs(Key.Z));
            Assert.False(clientInput.DampenersEnabled);

            Vector3 coastStartVelocity = afterThrust.Velocity;
            for (var tick = 0; tick < 24; tick++)
            {
                RunJointTick(deliverSnapshot: tick % 4 == 3);
            }

            Assert.True(clientStore.TryGet(clientPlayer, out PhysicsComponent cliCoast));
            Assert.True(serverStore.TryGet(serverPlayer, out PhysicsComponent srvCoast));
            Assert.Equal(coastStartVelocity, srvCoast.Velocity);
            Assert.Equal(srvCoast.Velocity, cliCoast.Velocity);

            //  Phase 3: Toggle dampeners back ON mid-flight; both decelerate identically across snapshots.
            clientInputService.HeldKeys.Add(Key.Z);
            clientInputService.KeyPressed?.Invoke(clientInputService, new KeyEventArgs(Key.Z));
            clientInputService.HeldKeys.Remove(Key.Z);
            clientInputService.KeyReleased?.Invoke(clientInputService, new KeyEventArgs(Key.Z));
            Assert.True(clientInput.DampenersEnabled);

            for (var tick = 0; tick < 24; tick++)
            {
                RunJointTick(deliverSnapshot: tick % 4 == 3);
            }

            Assert.True(clientStore.TryGet(clientPlayer, out PhysicsComponent cliBrake));
            Assert.True(serverStore.TryGet(serverPlayer, out PhysicsComponent srvBrake));
            Assert.Equal(srvBrake.Velocity, cliBrake.Velocity);
            Assert.True(srvBrake.Velocity.Length() < coastStartVelocity.Length());
        }
        finally
        {
            ClientHost.GameState.Set(ClientGameState.MainMenu);
        }
    }

    private static void IntegratePosition(DataStore store, int entity, float dt)
    {
        store.QueryRef(entity, dt, static (float stepDt, DataStore _, int _, ref Ref<TransformComponent> transform, ref Ref<PhysicsComponent> physics) =>
        {
            Vector3 nextPosition = transform.Read.Position + physics.Read.Velocity * stepDt;
            if (nextPosition != transform.Read.Position)
            {
                transform.Write.Position = nextPosition;
            }
        });
    }

    private static NetworkRegistry CreateRegistry()
    {
        var registry = new NetworkRegistry();
        Assert.True(registry.Register<TransformComponent>(Uuid.FromValue(2), NetworkDirection.ServerOwned, new TransformCodec()).Success);
        Assert.True(registry.Register<PhysicsComponent>(Uuid.FromValue(3), NetworkDirection.ServerOwned, new PhysicsCodec()).Success);
        registry.Freeze();
        return registry;
    }

    private sealed class ShortcutHarness : IDisposable
    {
        public DataStore Store { get; } = new();
        public int Player { get; }
        public Uuid PlayerUuid { get; }
        public FakeInputService InputService { get; } = new();
        public ShortcutService Shortcuts { get; }
        public ClientPlayerMotionProcessor MotionProcessor { get; }
        public InteractionState InteractionState { get; }
        public ClientInputSystem InputSystem { get; }

        public ShortcutHarness()
        {
            Shortcuts = new ShortcutService(InputService, NullLogger.Instance);
            MotionProcessor = new ClientPlayerMotionProcessor(
                InputService,
                new FakeWindowContext(),
                new FakePhysics(),
                new EventInvoker<PlayerMovedEvent>([])
            );
            InteractionState = new InteractionState(MotionProcessor);
            MotionProcessor.SetInputEnabled(true);
            InputSystem = new ClientInputSystem(MotionProcessor, Shortcuts, new ControlSettings(), new SnapshotAckTracker(), InteractionState);

            PlayerUuid = Uuid.FromValue(77);
            Player = Store.Alloc(PlayerUuid);
            Store.AddOrUpdate(Player, new PlayerComponent());
            Store.AddOrUpdate(Player, new InputComponent());
            Store.AddOrUpdate(Player, PendingInputComponent.Create());
        }

        public void PressKey(Key key)
        {
            InputService.HeldKeys.Add(key);
            InputService.KeyPressed?.Invoke(InputService, new KeyEventArgs(key));
            InputService.HeldKeys.Remove(key);
            InputService.KeyReleased?.Invoke(InputService, new KeyEventArgs(key));
        }

        public void TickInput()
        {
            InputSystem.Tick(1f / 64f, Store);
        }

        public void Dispose()
        {
            ClientHost.GameState.Set(ClientGameState.MainMenu);
        }
    }
}
