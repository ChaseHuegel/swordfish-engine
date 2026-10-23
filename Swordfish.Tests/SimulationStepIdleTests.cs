#nullable enable
using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Physics;
using Xunit;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Networking;

namespace Swordfish.Tests;

/// <summary>
/// Pins the change-gated write in the shared simulation step. An idle command reproduces the current
/// physics state, so it must not mark the authoritative component dirty.
/// </summary>
public class SimulationStepIdleTests
{
    [Fact]
    public void IdleCommandDoesNotDirtyPhysics()
    {
        var store = new DataStore();
        int entity = CreateEntity(store);
        StageInput(store, entity, new InputComponent { ServerTickAtSample = 1 });

        using var step = new SimulationStep(store, new FakePhysics());
        store.ClearDirty<PhysicsComponent>(entity);
        step.Step();

        Assert.False(store.IsDirty<PhysicsComponent>(entity), "An idle command must not dirty the physics component.");
    }

    [Fact]
    public void MovementCommandDirtiesPhysics()
    {
        var store = new DataStore();
        int entity = CreateEntity(store);
        StageInput(store, entity, new InputComponent { MovementZ = 1f, ServerTickAtSample = 1 });

        using var step = new SimulationStep(store, new FakePhysics());
        store.ClearDirty<PhysicsComponent>(entity);
        step.Step();

        Assert.True(store.IsDirty<PhysicsComponent>(entity), "A movement command must dirty the physics component.");
        Assert.True(store.TryGet(entity, out PhysicsComponent physics) && physics.Velocity != Vector3.Zero);
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

    private sealed class FakePhysics : IPhysics
    {
        public float FixedTimestamp => 1f / 64f;

        public event EventHandler<EventArgs>? FixedUpdate
        {
            add { }
            remove { }
        }

        public RaycastResult Raycast(in Ray ray) => default;

        public void SetGravity(Vector3 gravity) { }
    }
}
