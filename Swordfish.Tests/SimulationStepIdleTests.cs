#nullable enable
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Physics;
using Xunit;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Networking;

namespace Swordfish.Tests;

/// <summary>
/// Pins the change-gated write and the idle path in the shared simulation step. An idle command
/// reproduces the current physics state, so it must not mark the authoritative component dirty. A step
/// without a command is idle input, so it damps instead of leaving the body at its last velocity.
/// </summary>
public class SimulationStepIdleTests
{
    [Fact]
    public void IdleCommandDoesNotDirtyPhysics()
    {
        var store = new DataStore();
        int entity = CreateEntity(store);
        StageInput(store, entity, new InputComponent { ServerTickAtSample = 0 });

        using var step = new SimulationStep(store, new FakePhysics());
        step.AlignTo(0);
        store.ClearDirty<PhysicsComponent>(entity);
        step.Step();

        Assert.False(store.IsDirty<PhysicsComponent>(entity), "An idle command must not dirty the physics component.");
    }

    [Fact]
    public void MovementCommandDirtiesPhysics()
    {
        var store = new DataStore();
        int entity = CreateEntity(store);
        StageInput(store, entity, new InputComponent { MovementZ = 1f, ServerTickAtSample = 0 });

        using var step = new SimulationStep(store, new FakePhysics());
        step.AlignTo(0);
        store.ClearDirty<PhysicsComponent>(entity);
        step.Step();

        Assert.True(store.IsDirty<PhysicsComponent>(entity), "A movement command must dirty the physics component.");
        Assert.True(store.TryGet(entity, out PhysicsComponent physics) && physics.Velocity != Vector3.Zero);
    }

    [Fact]
    public void MissingCommandDampsVelocity()
    {
        var store = new DataStore();
        int entity = CreateEntity(store);
        store.AddOrUpdate(entity, new PhysicsComponent(
            Layers.MOVING,
            BodyType.Dynamic,
            CollisionDetection.Discrete,
            velocity: new Vector3(1f, 0f, 0f),
            torque: Vector3.Zero
        ));

        using var step = new SimulationStep(store, new FakePhysics());
        step.AlignTo(0);
        step.Step();

        Assert.True(store.TryGet(entity, out PhysicsComponent physics));
        Assert.True(physics.Velocity.X < 1f, "A step without a command must damp velocity.");
        Assert.True(physics.Velocity.X > 0f);
    }

    [Fact]
    public void MissingCommandDoesNotDirtyARestingBody()
    {
        var store = new DataStore();
        int entity = CreateEntity(store);

        using var step = new SimulationStep(store, new FakePhysics());
        step.AlignTo(0);
        store.ClearDirty<PhysicsComponent>(entity);
        step.Step();

        Assert.False(store.IsDirty<PhysicsComponent>(entity), "An input-less resting step must not dirty the physics component.");
    }

    private static int CreateEntity(DataStore store)
    {
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new InputComponent());
        store.AddOrUpdate(entity, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Discrete));
        store.AddOrUpdate(entity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(entity, NetworkComponent.Create());
        return entity;
    }

    private static void StageInput(DataStore store, int entity, in InputComponent input)
    {
        Assert.True(store.TryGet(entity, out NetworkComponent net));
        net.StagedInputs!.Add(input);
    }
}
