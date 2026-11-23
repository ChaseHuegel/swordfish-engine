using System;
using System.Numerics;
using DryIoc;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Types.Shapes;
using Swordfish.Library.Util;
using Xunit;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay;
using WaywardBeyond.Networking;
using WaywardBeyond.Networking.Codecs;
using WaywardBeyond.Networking.Registry;

namespace WaywardBeyond.Tests;

/// <summary>Pins collider replication for the character compound and its supported shape packing.</summary>
public class ColliderCodecTests
{
    [Fact]
    public void RegistrationLimitsColliderReplicationToCharacterBodies()
    {
        var container = new Container();
        container.RegisterNetworking();

        Result<NetworkComponentInfo> registration = container.Resolve<NetworkRegistry>().GetInfo<ColliderComponent>();
        Assert.True(registration.Success);
        Func<DataStore, int, bool> filter = registration.Value.ReplicationFilter!;
        Assert.NotNull(filter);

        var store = new DataStore();
        int character = store.Alloc();
        store.AddOrUpdate(character, new BodyViewComponent { Body = "wb:human_m" });
        int structure = store.Alloc();

        Assert.True(filter(store, character));
        Assert.False(filter(store, structure));
    }

    [Fact]
    public void CharacterCompoundColliderRoundTrips()
    {
        ColliderComponent original = CharacterDefaults.CreateCharacterColliderComponent(Vector3.One);
        var store = new DataStore();
        int source = store.Alloc();
        store.AddOrUpdate(source, original);

        var codec = new ColliderCodec();
        Result<byte[]> serialized = codec.Serialize(store, source);
        Assert.True(serialized.Success);

        var target = new DataStore();
        int destination = target.Alloc();
        Assert.True(codec.Apply(target, destination, serialized.Value).Success);

        Assert.True(target.TryGet(destination, out ColliderComponent copy));
        Assert.True(copy.CompoundShape.HasValue);

        CompoundShape expected = original.CompoundShape!.Value;
        CompoundShape actual = copy.CompoundShape!.Value;
        Assert.Equal(expected.Shapes.Length, actual.Shapes.Length);
        Assert.Equal(expected.Shapes[0].Type, actual.Shapes[0].Type);
        Assert.Equal(expected.Shapes[0].Box3.Extents, actual.Shapes[0].Box3.Extents);
        Assert.Equal(expected.Positions[0], actual.Positions[0]);
        Assert.Equal(expected.Orientations[0], actual.Orientations[0]);
    }

    [Fact]
    public void SingleShapeColliderMaterializesAsOnePartCompound()
    {
        var original = new ColliderComponent(new Box3(new Vector3(1f, 2f, 3f)));
        var store = new DataStore();
        int source = store.Alloc();
        store.AddOrUpdate(source, original);

        var codec = new ColliderCodec();
        Result<byte[]> serialized = codec.Serialize(store, source);
        Assert.True(serialized.Success);

        var target = new DataStore();
        int destination = target.Alloc();
        Assert.True(codec.Apply(target, destination, serialized.Value).Success);

        Assert.True(target.TryGet(destination, out ColliderComponent copy));
        Assert.True(copy.CompoundShape.HasValue);
        Assert.Single(copy.CompoundShape!.Value.Shapes);
        Assert.Equal(Vector3.Zero, copy.CompoundShape!.Value.Positions[0]);
        Assert.Equal(Quaternion.Identity, copy.CompoundShape!.Value.Orientations[0]);
    }

    [Fact]
    public void ConvexHullColliderRoundTrips()
    {
        var hull = new ConvexHull(
        [
            new Vector3(0f, 0f, 0f),
            new Vector3(1f, 0f, 0f),
            new Vector3(0f, 1f, 0f),
            new Vector3(0f, 0f, 1f),
        ]);
        var original = new ColliderComponent(new Shape(hull));
        var store = new DataStore();
        int source = store.Alloc();
        store.AddOrUpdate(source, original);

        var codec = new ColliderCodec();
        Result<byte[]> serialized = codec.Serialize(store, source);
        Assert.True(serialized.Success);

        var target = new DataStore();
        int destination = target.Alloc();
        Assert.True(codec.Apply(target, destination, serialized.Value).Success);

        Assert.True(target.TryGet(destination, out ColliderComponent copy));
        Shape shape = copy.CompoundShape!.Value.Shapes[0];
        Assert.Equal(ShapeType.ConvexHull, shape.Type);
        Assert.NotNull(shape.ConvexHull);
        Assert.Equal(4, shape.ConvexHull!.Count);
    }

    [Fact]
    public void SerializeRejectsAShapeThatIsNotACollider()
    {
        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, new ColliderComponent(new Shape(new Ellipse2(1f, 2f, 3f, 4f, 5f))));

        Result<byte[]> serialized = new ColliderCodec().Serialize(store, entity);

        Assert.False(serialized.Success);
        Assert.Contains("not a 3D collider", serialized.Message);
    }

    [Fact]
    public void ApplyRejectsMismatchedPartCounts()
    {
        var message = new ColliderMessage
        {
            Shapes = [new ColliderShapeMessage { Type = (byte)ShapeType.Box3, X = 1f, Y = 1f, Z = 1f }],
            Positions = [],
            Orientations = [],
        };

        var store = new DataStore();
        int entity = store.Alloc();

        Result applied = new ColliderCodec().Apply(store, entity, message.Serialize());

        Assert.False(applied.Success);
        Assert.Contains("counts differ", applied.Message);
    }

    [Fact]
    public void ApplyRejectsNonPositiveOrNonFiniteShapeDimensions()
    {
        var message = new ColliderMessage
        {
            Shapes = [new ColliderShapeMessage { Type = (byte)ShapeType.Box3, X = 1f, Y = 0f, Z = 1f, Points = [] }],
            Positions = [new Float3Message()],
            Orientations = [new Float4Message { W = 1f }],
        };

        var store = new DataStore();
        int entity = store.Alloc();

        Result applied = new ColliderCodec().Apply(store, entity, message.Serialize());

        Assert.False(applied.Success);
        Assert.Contains("box extents are invalid", applied.Message);
    }

    [Fact]
    public void ApplyRejectsInvalidShapeTransform()
    {
        var message = new ColliderMessage
        {
            Shapes = [new ColliderShapeMessage { Type = (byte)ShapeType.Box3, X = 1f, Y = 1f, Z = 1f, Points = [] }],
            Positions = [new Float3Message { X = float.NaN }],
            Orientations = [new Float4Message { W = 1f }],
        };

        var store = new DataStore();
        int entity = store.Alloc();

        Result applied = new ColliderCodec().Apply(store, entity, message.Serialize());

        Assert.False(applied.Success);
        Assert.Contains("position is non-finite", applied.Message);

        message.Positions = [new Float3Message()];
        message.Orientations = [new Float4Message()];

        Result zeroQuaternion = new ColliderCodec().Apply(store, entity, message.Serialize());

        Assert.False(zeroQuaternion.Success);
        Assert.Contains("orientation is invalid", zeroQuaternion.Message);
    }

    [Fact]
    public void ApplyRejectsNonFiniteConvexHullPoint()
    {
        var message = new ColliderMessage
        {
            Shapes =
            [
                new ColliderShapeMessage
                {
                    Type = (byte)ShapeType.ConvexHull,
                    Points =
                    [
                        new Float3Message(),
                        new Float3Message { X = 1f },
                        new Float3Message { Y = 1f },
                        new Float3Message { Z = float.PositiveInfinity },
                    ],
                },
            ],
            Positions = [new Float3Message()],
            Orientations = [new Float4Message { W = 1f }],
        };

        var store = new DataStore();
        int entity = store.Alloc();

        Result applied = new ColliderCodec().Apply(store, entity, message.Serialize());

        Assert.False(applied.Success);
        Assert.Contains("non-finite point", applied.Message);
    }
}
