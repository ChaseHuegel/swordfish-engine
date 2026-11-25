#nullable enable
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Physics;
using Xunit;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Gameplay.Networking;

namespace WaywardBeyond.Tests;

/// <summary>
/// Pins the remote-body path in the shared simulation step. A client's remote player mirror carries a
/// <see cref="BodyViewComponent"/> and no <see cref="InputComponent"/>, so the step must damp it exactly
/// like the server's commandless mirror without applying any client input.
/// </summary>
public class SimulationStepRemoteBodyTests
{
    [Fact]
    public void RemoteBodyDampsLikeACommandlessServerBody()
    {
        var store = new DataStore();
        int serverMirror = CreateBody(store, withInput: true);
        int remoteMirror = CreateBody(store, withInput: false);
        var velocity = new Vector3(2f, 0f, 0f);
        var torque = new Vector3(0f, 3f, 0f);
        store.AddOrUpdate(serverMirror, PhysicsAt(velocity, torque));
        store.AddOrUpdate(remoteMirror, PhysicsAt(velocity, torque));

        using var step = new SimulationStep(store, new FakePhysics());
        step.AlignTo(0);
        step.Step();

        Assert.True(store.TryGet(serverMirror, out PhysicsComponent serverPhysics));
        Assert.True(store.TryGet(remoteMirror, out PhysicsComponent remotePhysics));
        Assert.Equal(serverPhysics.Velocity, remotePhysics.Velocity);
        Assert.Equal(serverPhysics.Torque, remotePhysics.Torque);
        Assert.True(remotePhysics.Velocity.X < velocity.X, "A remote body must damp between snapshots.");
        Assert.True(remotePhysics.Torque.Y < torque.Y, "A remote body must damp angular velocity between snapshots.");
    }

    [Fact]
    public void RemoteBodyStepIgnoresStagedInput()
    {
        var store = new DataStore();
        int remoteMirror = CreateBody(store, withInput: false);

        //  The staged command belongs to command-driven bodies. A mirror has no InputComponent, so the
        //  step must never resolve it and must not add movement.
        Assert.True(store.TryGet(remoteMirror, out NetworkComponent net));
        net.StagedInputs!.Add(new InputComponent { MovementZ = 1f, ServerTickAtSample = 0 });
        store.ClearDirty<PhysicsComponent>(remoteMirror);

        using var step = new SimulationStep(store, new FakePhysics());
        step.AlignTo(0);
        step.Step();

        Assert.True(store.TryGet(remoteMirror, out PhysicsComponent physics));
        Assert.Equal(Vector3.Zero, physics.Velocity);
        Assert.Equal(Vector3.Zero, physics.Torque);
        Assert.False(store.IsDirty<PhysicsComponent>(remoteMirror), "A resting remote body must not dirty its physics component.");
    }

    [Fact]
    public void RemoteBodySnapsToRestAtTheStopEpsilon()
    {
        var store = new DataStore();
        int remoteMirror = CreateBody(store, withInput: false);
        store.AddOrUpdate(remoteMirror, PhysicsAt(new Vector3(0.001f, 0f, 0f), Vector3.Zero));

        using var step = new SimulationStep(store, new FakePhysics());
        step.AlignTo(0);
        for (var tick = 0; tick < 64; tick++)
        {
            step.Step();
        }

        Assert.True(store.TryGet(remoteMirror, out PhysicsComponent physics));
        Assert.Equal(Vector3.Zero, physics.Velocity);
    }

    [Fact]
    public void NonCharacterBodyWithoutInputKeepsItsVelocity()
    {
        var store = new DataStore();
        int prop = store.Alloc();
        store.AddOrUpdate(prop, PhysicsAt(new Vector3(2f, 0f, 0f), new Vector3(0f, 3f, 0f)));
        store.AddOrUpdate(prop, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));

        using var step = new SimulationStep(store, new FakePhysics());
        step.AlignTo(0);
        step.Step();

        Assert.True(store.TryGet(prop, out PhysicsComponent physics));
        Assert.Equal(new Vector3(2f, 0f, 0f), physics.Velocity);
        Assert.Equal(new Vector3(0f, 3f, 0f), physics.Torque);
    }

    private static int CreateBody(DataStore store, bool withInput)
    {
        int entity = store.Alloc();
        if (withInput)
        {
            store.AddOrUpdate(entity, new InputComponent());
        }

        store.AddOrUpdate(entity, new BodyViewComponent { Body = "wb:human_m" });
        store.AddOrUpdate(entity, new PhysicsComponent(Layers.MOVING, BodyType.Dynamic, CollisionDetection.Continuous));
        store.AddOrUpdate(entity, new TransformComponent(Vector3.Zero, Quaternion.Identity, Vector3.One));
        store.AddOrUpdate(entity, NetworkComponent.Create());
        return entity;
    }

    private static PhysicsComponent PhysicsAt(in Vector3 velocity, in Vector3 torque)
    {
        return new PhysicsComponent(
            Layers.MOVING,
            BodyType.Dynamic,
            CollisionDetection.Continuous,
            velocity: velocity,
            torque: torque
        );
    }
}
