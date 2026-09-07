using System;
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
        store.AddOrUpdate(entity, new TransformComponent());
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

        for (uint tick = 1; tick <= 40; tick++)
        {
            var command = new InputComponent
            {
                MovementZ = (tick % 4 == 0) ? -1f : 0f,
                LookPitchDelta = 0.01f,
                LookYawDelta = (tick % 8 == 0) ? 0.02f : 0f,
                LookRollDelta = (tick % 16 == 0) ? 0.03f : 0f,
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
        for (uint tick = 1; tick <= 60; tick++)
        {
            var command = new InputComponent
            {
                LookYawDelta = 0.05f,
                LookPitchDelta = (tick % 10 == 0) ? 0.03f : 0f,
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
    }
}