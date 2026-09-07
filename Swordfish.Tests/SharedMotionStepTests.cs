using System;
using Swordfish.ECS;
using Swordfish.Physics;
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
    /// identical velocity and orientation on two independent worlds (server authority vs client
    /// prediction). Position integration is owned by the physics solver, not the step.
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
                LookYaw = tick * 0.01f,
                LookPitch = (tick % 8 == 0) ? 0.02f : 0f,
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

        Assert.True(serverStore.TryGet<TransformComponent>(serverEntity, out TransformComponent serverTransform));
        Assert.True(clientStore.TryGet<TransformComponent>(clientEntity, out TransformComponent clientTransform));
        Assert.Equal(serverTransform.Orientation, clientTransform.Orientation);
    }
}