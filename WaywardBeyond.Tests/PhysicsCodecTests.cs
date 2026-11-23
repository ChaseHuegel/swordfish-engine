using System;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Physics;
using Xunit;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Codecs;

namespace WaywardBeyond.Tests;

/// <summary>Pins the physics codec's full-state replication and its diagnosable failures.</summary>
public class PhysicsCodecTests
{
    [Fact]
    public void ApplyCreatesTheComponentFromTheSnapshot()
    {
        var source = new PhysicsComponent(
            Layers.MOVING,
            BodyType.Kinematic,
            CollisionDetection.Continuous,
            new Vector3(1f, 2f, 3f),
            new Vector3(4f, 5f, 6f)
        );
        var codec = new PhysicsCodec();
        byte[] payload = codec.Serialize(in source);

        var store = new DataStore();
        int entity = store.Alloc();

        Assert.True(codec.Apply(store, entity, payload).Success);

        Assert.True(store.TryGet(entity, out PhysicsComponent result));
        Assert.Equal(Layers.MOVING, result.Layer);
        Assert.Equal(BodyType.Kinematic, result.BodyType);
        Assert.Equal(CollisionDetection.Continuous, result.CollisionDetection);
        Assert.Equal(source.Velocity, result.Velocity);
        Assert.Equal(source.Torque, result.Torque);
    }

    [Fact]
    public void ApplyUpdatesVelocityOnAnExistingComponentAndKeepsItsBodyConfiguration()
    {
        var source = new PhysicsComponent(
            Layers.MOVING,
            BodyType.Dynamic,
            CollisionDetection.Continuous,
            new Vector3(7f, 8f, 9f),
            new Vector3(10f, 11f, 12f)
        );
        var codec = new PhysicsCodec();
        byte[] payload = codec.Serialize(in source);

        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new PhysicsComponent(Layers.NON_MOVING, BodyType.Static, CollisionDetection.Discrete));

        Assert.True(codec.Apply(store, entity, payload).Success);

        Assert.True(store.TryGet(entity, out PhysicsComponent result));
        Assert.Equal(Layers.NON_MOVING, result.Layer);
        Assert.Equal(BodyType.Static, result.BodyType);
        Assert.Equal(CollisionDetection.Discrete, result.CollisionDetection);
        Assert.Equal(source.Velocity, result.Velocity);
        Assert.Equal(source.Torque, result.Torque);
    }

    [Fact]
    public void ApplyRejectsAnInvalidBodyType()
    {
        var message = new PhysicsMessage
        {
            Layer = Layers.MOVING,
            BodyType = byte.MaxValue,
            CollisionDetection = (byte)CollisionDetection.Discrete,
        };

        var store = new DataStore();
        int entity = store.Alloc();

        Result result = new PhysicsCodec().Apply(store, entity, message.Serialize());

        Assert.False(result.Success);
        Assert.Contains("body type", result.Message);
    }

    [Fact]
    public void ApplyRejectsAnInvalidLayer()
    {
        var message = new PhysicsMessage
        {
            Layer = byte.MaxValue,
            BodyType = (byte)BodyType.Dynamic,
            CollisionDetection = (byte)CollisionDetection.Discrete,
        };

        var store = new DataStore();
        int entity = store.Alloc();

        Result result = new PhysicsCodec().Apply(store, entity, message.Serialize());

        Assert.False(result.Success);
        Assert.Contains("physics layer", result.Message);
    }

    [Fact]
    public void ApplyRejectsNonFinitePhysicsState()
    {
        var message = new PhysicsMessage
        {
            Layer = Layers.MOVING,
            BodyType = (byte)BodyType.Dynamic,
            CollisionDetection = (byte)CollisionDetection.Discrete,
            VelocityX = float.NaN,
        };

        var store = new DataStore();
        int entity = store.Alloc();

        Result result = new PhysicsCodec().Apply(store, entity, message.Serialize());

        Assert.False(result.Success);
        Assert.Contains("non-finite", result.Message);
    }

    [Fact]
    public void SerializeFailsWithMessageWhenTheEntityHasNoComponent()
    {
        var store = new DataStore();
        int entity = store.Alloc();

        Result<byte[]> result = new PhysicsCodec().Serialize(store, entity);

        Assert.False(result.Success);
        Assert.Contains(nameof(PhysicsComponent), result.Message);
    }

    [Fact]
    public void ApplyRejectsAMalformedPayload()
    {
        var source = new PhysicsComponent(
            Layers.MOVING,
            BodyType.Dynamic,
            CollisionDetection.Discrete,
            new Vector3(1f, 2f, 3f),
            Vector3.Zero
        );
        byte[] valid = new PhysicsCodec().Serialize(in source);
        var store = new DataStore();
        int entity = store.Alloc();

        Result result = new PhysicsCodec().Apply(store, entity, valid.AsSpan(0, 2));

        Assert.False(result.Success);
        Assert.False(string.IsNullOrEmpty(result.Message));
    }
}
