#nullable enable
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Physics;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Networking;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Pins inertial dampener simulation behavior on <see cref="SimulationStep"/>.</summary>
public class InertialDampenersTests
{
    [Fact]
    public void LinearVelocityDecaysWhenDampenersEnabled()
    {
        var store = new DataStore();
        var initialVelocity = new Vector3(10f, -4f, 6f);
        int entity = CreatePlayer(store, initialVelocity, Vector3.Zero);
        StageInput(store, entity, new InputComponent { DampenersDisabled = false, ServerTickAtSample = 0 });

        using var step = new SimulationStep(store, new FakePhysics());
        step.AlignTo(0);
        step.Step();

        Assert.True(store.TryGet(entity, out PhysicsComponent physics));
        Assert.True(physics.Velocity.Length() < initialVelocity.Length());
        Assert.True(physics.Velocity.Length() > 0f);
    }

    [Fact]
    public void LinearVelocityPersistsAndDoesNotDirtyPhysicsWhenDampenersDisabled()
    {
        var store = new DataStore();
        var coastVelocity = new Vector3(10f, -4f, 6f);
        int entity = CreatePlayer(store, coastVelocity, Vector3.Zero);
        StageInput(store, entity, new InputComponent { DampenersDisabled = true, ServerTickAtSample = 0 });

        using var step = new SimulationStep(store, new FakePhysics());
        step.AlignTo(0);
        store.ClearDirty<PhysicsComponent>(entity);

        for (var i = 0; i < 30; i++)
        {
            step.Step();
        }

        Assert.True(store.TryGet(entity, out PhysicsComponent physics));
        Assert.Equal(coastVelocity, physics.Velocity);
        Assert.False(store.IsDirty<PhysicsComponent>(entity));
    }

    [Fact]
    public void AngularVelocityStillDampsWhileCoastingWithDampenersDisabled()
    {
        var store = new DataStore();
        var coastVelocity = new Vector3(8f, 0f, -3f);
        var initialTorque = new Vector3(0.5f, 3f, -1f);
        int entity = CreatePlayer(store, coastVelocity, initialTorque);
        StageInput(store, entity, new InputComponent { DampenersDisabled = true, ServerTickAtSample = 0 });

        using var step = new SimulationStep(store, new FakePhysics());
        step.AlignTo(0);
        step.Step();

        Assert.True(store.TryGet(entity, out PhysicsComponent physics));
        Assert.Equal(coastVelocity, physics.Velocity);
        Assert.True(physics.Torque.Length() < initialTorque.Length());
        Assert.True(physics.Torque.Length() > 0f);
    }

    [Fact]
    public void ThrustAndLookApplyInBothDampenerModes()
    {
        var store = new DataStore();
        var physicsProvider = new FakePhysics();
        var initialVelocity = new Vector3(4f, 0f, 0f);
        int dampedEntity = CreatePlayer(store, initialVelocity, Vector3.Zero);
        int coastEntity = CreatePlayer(store, initialVelocity, Vector3.Zero);

        StageInput(store, dampedEntity, new InputComponent
        {
            MovementX = 1f,
            LookPitch = 0f,
            DampenersDisabled = false,
            ServerTickAtSample = 0,
        });
        StageInput(store, coastEntity, new InputComponent
        {
            MovementX = 1f,
            LookPitch = 0f,
            DampenersDisabled = true,
            ServerTickAtSample = 0,
        });

        using var step = new SimulationStep(store, physicsProvider);
        step.AlignTo(0);
        step.Step();

        //  Tick 2 supplies a look delta so look history produces angular velocity in both modes.
        StageInput(store, dampedEntity, new InputComponent
        {
            MovementX = 1f,
            LookPitch = 0.25f,
            DampenersDisabled = false,
            ServerTickAtSample = 2,
        });
        StageInput(store, coastEntity, new InputComponent
        {
            MovementX = 1f,
            LookPitch = 0.25f,
            DampenersDisabled = true,
            ServerTickAtSample = 2,
        });
        step.Step();

        Assert.True(store.TryGet(dampedEntity, out PhysicsComponent dampedPhysics));
        Assert.True(store.TryGet(coastEntity, out PhysicsComponent coastPhysics));

        float dt = physicsProvider.FixedTimestamp;
        float expectedThrust = CharacterDefaults.BASE_SPEED * dt;
        float expectedCoastX = initialVelocity.X + 2f * expectedThrust;
        Assert.Equal(expectedCoastX, coastPhysics.Velocity.X, precision: 4);
        Assert.True(coastPhysics.Velocity.X > dampedPhysics.Velocity.X);
        Assert.True(dampedPhysics.Velocity.X > initialVelocity.X);
        Assert.Equal(dampedPhysics.Torque, coastPhysics.Torque);
        Assert.True(coastPhysics.Torque.X > 0f);
    }

    [Fact]
    public void CommandlessAndStaleStepsStillDampLinearVelocityEvenWhenLiveInputHasDampenersDisabled()
    {
        var store = new DataStore();
        var initialVelocity = new Vector3(5f, 0f, 0f);
        int missingCommandEntity = CreatePlayer(store, initialVelocity, Vector3.Zero);
        int staleCommandEntity = CreatePlayer(store, initialVelocity, Vector3.Zero);

        //  Seed live InputComponent with DampenersDisabled = true; SimulationStep must read
        //  DampenersDisabled only from the resolved command, never from the live component.
        store.AddOrUpdate(missingCommandEntity, new InputComponent { DampenersDisabled = true });
        store.AddOrUpdate(staleCommandEntity, new InputComponent { DampenersDisabled = true });
        StageInput(store, staleCommandEntity, new InputComponent { DampenersDisabled = true, ServerTickAtSample = 0 });

        using var step = new SimulationStep(store, new FakePhysics(), commandMaxAgeTicks: 1);
        step.AlignTo(0);

        //  Tick 1: missingCommandEntity has no command (damps); staleCommandEntity is fresh (coasts).
        step.Step();
        Assert.True(store.TryGet(missingCommandEntity, out PhysicsComponent missingAfterTick1));
        Assert.True(store.TryGet(staleCommandEntity, out PhysicsComponent staleAfterTick1));
        Assert.True(missingAfterTick1.Velocity.X < initialVelocity.X);
        Assert.Equal(initialVelocity.X, staleAfterTick1.Velocity.X);

        //  Tick 2: staleCommandEntity stays within maxAgeTicks (2 - 1 <= 1). Tick 3 exceeds it.
        step.Step();
        step.Step();
        Assert.True(store.TryGet(staleCommandEntity, out PhysicsComponent staleAfterTick3));
        Assert.True(staleAfterTick3.Velocity.X < initialVelocity.X);
    }

    private static int CreatePlayer(DataStore store, in Vector3 velocity, in Vector3 torque)
    {
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new InputComponent());
        store.AddOrUpdate(entity, new PhysicsComponent(
            Layers.MOVING,
            BodyType.Dynamic,
            CollisionDetection.Discrete,
            velocity,
            torque
        ));
        store.AddOrUpdate(entity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(entity, NetworkComponent.Create());
        return entity;
    }

    private static void StageInput(DataStore store, int entity, in InputComponent input)
    {
        Assert.True(store.TryGet(entity, out NetworkComponent net));
        net.StagedInputs!.Stage(in input, input.ServerTickAtSample);
    }
}
