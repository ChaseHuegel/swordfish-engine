using System;
using System.Numerics;
using Microsoft.Extensions.Logging.Abstractions;
using Swordfish.ECS;
using Swordfish.Physics;
using Swordfish.Physics.Jolt;
using Swordfish.Settings;
using WaywardBeyond.Shared.Gameplay;
using WaywardBeyond.Shared.Networking.Components;
using Xunit;

namespace Swordfish.Tests;

public class SharedMotionStepTests
{
    private sealed class TestPhysics : IPhysics
    {
        public event EventHandler<EventArgs>? FixedUpdate;
        public RaycastResult Raycast(in Ray ray) => default;
        public void SetGravity(System.Numerics.Vector3 gravity) { }
    }

    private static int CreatePlayer(DataStore store, Uuid uuid)
    {
        int entity = store.Alloc(uuid);
        store.AddOrUpdate(entity, new InputComponent());
        store.AddOrUpdate(entity, new TransformComponent(PlayerBodyConfig.DEFAULT_SPAWN_POSITION, Quaternion.Identity, PlayerBodyConfig.PLAYER_SCALE));
        store.AddOrUpdate(entity, PlayerBodyConfig.CreatePhysics());
        store.AddOrUpdate(entity, PlayerBodyConfig.CreateCollider(PlayerBodyConfig.PLAYER_SCALE));
        return entity;
    }

    /// <summary>
    /// The shared step is deterministic: given the identical sim-tick command sequence it produces the
    /// identical torque (look) and velocity on two independent worlds (server authority vs client
    /// prediction). Position/orientation integration is owned by the physics solver, not the step, so
    /// torque, not orientation, is the deterministic output asserted here.
    /// </summary>
    [Fact]
    public void SharedStepProducesIdenticalStateForIdenticalInputSequence()
    {
        var uuid = Uuid.FromValue(0x1234);
        var serverStore = new DataStore();
        var clientStore = new DataStore();
        int serverEntity = CreatePlayer(serverStore, uuid);
        int clientEntity = CreatePlayer(clientStore, uuid);

        var serverInputs = new InputStageBuffer();
        var clientInputs = new InputStageBuffer();

        float pitch = 0f, yaw = 0f, roll = 0f;
        for (uint tick = 1; tick <= 40; tick++)
        {
            //  The wire carries running absolute look totals; the step applies per-sim-tick differences.
            pitch += 0.01f;
            if (tick % 8 == 0) yaw += 0.02f;
            if (tick % 16 == 0) roll += 0.03f;

            var command = new InputComponent
            {
                MovementZ = (tick % 4 == 0) ? -1f : 0f,
                LookPitch = pitch,
                LookYaw = yaw,
                LookRoll = roll,
                Jump = tick == 5,
                SequenceNumber = tick,
                ServerTickAtSample = tick,
            };
            serverInputs.Stage(command);
            clientInputs.Stage(command);
        }

        var serverPhysics = new TestPhysics();
        var clientPhysics = new TestPhysics();
        using var serverStep = new SharedPlayerMotionStep(serverStore, serverPhysics, (int _, uint simTick, out InputComponent c) => serverInputs.TryGet(simTick, out c));
        using var clientStep = new SharedPlayerMotionStep(clientStore, clientPhysics, (int _, uint simTick, out InputComponent c) => clientInputs.TryGet(simTick, out c));

        for (var i = 0; i < 40; i++)
        {
            serverStep.Step();
            clientStep.Step();
        }

        Assert.True(serverStore.TryGet<PhysicsComponent>(serverEntity, out PhysicsComponent serverPhysicsState));
        Assert.True(clientStore.TryGet<PhysicsComponent>(clientEntity, out PhysicsComponent clientPhysicsState));
        Assert.Equal(serverPhysicsState.Velocity, clientPhysicsState.Velocity);
        Assert.Equal(serverPhysicsState.Torque, clientPhysicsState.Torque);
    }

    /// <summary>
    /// End-to-end torque-look determinism: the shared step drives two real Jolt worlds (client + server)
    /// with the identical look-delta commands; after serialized physics steps both bodies must end at the
    /// same orientation, proving the restored torque-based look integrates identically on both sides.
    /// </summary>
    [Fact]
    public void TorqueLookIntegratesIdenticallyAcrossTwoJoltWorlds()
    {
        var settings = new PhysicsSettings();
        var logger = NullLogger<JoltPhysicsSystem>.Instance;
        var clientPhysics = new JoltPhysicsSystem(logger, settings);
        var serverPhysics = new JoltPhysicsSystem(logger, settings);
        var clientStore = new DataStore();
        var serverStore = new DataStore();

        var uuid = Uuid.FromValue(0x5678);
        int clientEntity = CreatePlayer(clientStore, uuid);
        int serverEntity = CreatePlayer(serverStore, uuid);

        var clientInputs = new InputStageBuffer();
        var serverInputs = new InputStageBuffer();
        float yaw = 0f, pitch = 0f;
        for (uint tick = 1; tick <= 60; tick++)
        {
            //  Cumulative totals: the step applies the per-sim-tick difference (0.05 yaw / 0.03 pitch).
            yaw += 0.05f;
            if (tick % 10 == 0) pitch += 0.03f;

            var command = new InputComponent
            {
                LookPitch = pitch,
                LookYaw = yaw,
                SequenceNumber = tick,
                ServerTickAtSample = tick,
            };
            clientInputs.Stage(command);
            serverInputs.Stage(command);
        }

        using var clientStep = new SharedPlayerMotionStep(clientStore, clientPhysics, (int _, uint simTick, out InputComponent c) => clientInputs.TryGet(simTick, out c));
        using var serverStep = new SharedPlayerMotionStep(serverStore, serverPhysics, (int _, uint simTick, out InputComponent c) => serverInputs.TryGet(simTick, out c));

        for (var i = 0; i < 60; i++)
        {
            clientPhysics.Tick(0.016f, clientStore);
            serverPhysics.Tick(0.016f, serverStore);
        }

        Assert.True(clientStore.TryGet<TransformComponent>(clientEntity, out TransformComponent clientTransform));
        Assert.True(serverStore.TryGet<TransformComponent>(serverEntity, out TransformComponent serverTransform));
        Assert.Equal(clientTransform.Orientation, serverTransform.Orientation);

        Assert.True(clientStore.TryGet<PhysicsComponent>(clientEntity, out PhysicsComponent clientPhysicsState));
        Assert.True(serverStore.TryGet<PhysicsComponent>(serverEntity, out PhysicsComponent serverPhysicsState));
        Assert.Equal(clientPhysicsState.Velocity, serverPhysicsState.Velocity);

        //  The look deltas were applied and integrated (not dropped): the body rotated away from identity.
        Assert.NotEqual(System.Numerics.Quaternion.Identity, clientTransform.Orientation);
    }

    /// <summary>
    /// Movement keys must map to the intended view-relative world directions. At identity, forward =
    /// +Z, so W (MovementZ = -1) must yield velocity toward -Z, D (MovementX = +1) toward +right (X),
    /// and Space (MovementY = +1) toward +up (Y).
    /// </summary>
    [Theory]
    [InlineData(0f, 0f, -1f, 0f, 0f, -1f)]
    [InlineData(0f, 0f, 1f, 0f, 0f, 1f)]
    [InlineData(1f, 0f, 0f, 1f, 0f, 0f)]
    [InlineData(-1f, 0f, 0f, -1f, 0f, 0f)]
    [InlineData(0f, 1f, 0f, 0f, 1f, 0f)]
    [InlineData(0f, -1f, 0f, 0f, -1f, 0f)]
    public void MovementInputMapsToViewRelativeDirection(float mx, float my, float mz, float ex, float ey, float ez)
    {
        var uuid = Uuid.FromValue(0x9999);
        var store = new DataStore();
        int entity = CreatePlayer(store, uuid);
        var inputs = new InputStageBuffer();
        inputs.Stage(new InputComponent { MovementX = mx, MovementY = my, MovementZ = mz, SequenceNumber = 1, ServerTickAtSample = 1 });

        var physics = new TestPhysics();
        using var step = new SharedPlayerMotionStep(store, physics, (int _, uint simTick, out InputComponent c) => inputs.TryGet(simTick, out c));
        step.Step();

        Assert.True(store.TryGet<PhysicsComponent>(entity, out PhysicsComponent physicsState));
        var expected = new System.Numerics.Vector3(ex, ey, ez) * PlayerBodyConfig.BASE_SPEED * PlayerBodyConfig.PHYSICS_STEP;
        var tolerance = PlayerBodyConfig.BASE_SPEED * PlayerBodyConfig.PHYSICS_STEP * 0.001f;
        Assert.True((expected - physicsState.Velocity).Length() <= tolerance, $"Expected {expected}, got {physicsState.Velocity}");
    }
}