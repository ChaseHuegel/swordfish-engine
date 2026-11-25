using System;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Xunit;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Codecs;

namespace WaywardBeyond.Tests;

/// <summary>Pins the transform codec's replication and its rejection of values a body cannot use.</summary>
public class TransformCodecTests
{
    [Fact]
    public void ApplyWritesTheSnapshot()
    {
        var source = new TransformComponent(
            new Vector3(1f, 2f, 3f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 1.5f),
            new Vector3(2f, 2f, 2f)
        );
        var codec = new TransformCodec();
        byte[] payload = codec.Serialize(in source);

        var store = new DataStore();
        int entity = store.Alloc();

        Assert.True(codec.Apply(store, entity, payload).Success);

        Assert.True(store.TryGet(entity, out TransformComponent result));
        Assert.Equal(source.Position, result.Position);
        Assert.Equal(source.Orientation, result.Orientation);
        Assert.Equal(source.Scale, result.Scale);
    }

    [Fact]
    public void ApplyRejectsANonFiniteTransform()
    {
        Result result = ApplyMessage(new TransformMessage
        {
            PositionX = float.NaN,
            OrientationW = 1f,
            ScaleX = 1f,
            ScaleY = 1f,
            ScaleZ = 1f,
        });

        Assert.False(result.Success);
        Assert.Contains("non-finite", result.Message);
    }

    [Fact]
    public void ApplyRejectsANonFiniteOrientation()
    {
        Result result = ApplyMessage(new TransformMessage
        {
            OrientationW = float.PositiveInfinity,
            ScaleX = 1f,
            ScaleY = 1f,
            ScaleZ = 1f,
        });

        Assert.False(result.Success);
        Assert.Contains("non-finite", result.Message);
    }

    [Fact]
    public void ApplyRejectsAZeroOrientation()
    {
        Result result = ApplyMessage(new TransformMessage
        {
            ScaleX = 1f,
            ScaleY = 1f,
            ScaleZ = 1f,
        });

        Assert.False(result.Success);
        Assert.Contains("degenerate orientation", result.Message);
    }

    [Fact]
    public void ApplyKeepsTheCurrentTransformWhenTheSnapshotIsRejected()
    {
        var current = new TransformComponent(
            new Vector3(1f, 2f, 3f),
            Quaternion.CreateFromAxisAngle(Vector3.UnitY, 0.5f),
            Vector3.One
        );
        var message = new TransformMessage
        {
            PositionX = float.NaN,
            OrientationW = 1f,
            ScaleX = 1f,
            ScaleY = 1f,
            ScaleZ = 1f,
        };

        var store = new DataStore();
        int entity = store.Alloc();
        store.AddOrUpdate(entity, current);

        Assert.False(new TransformCodec().Apply(store, entity, message.Serialize()).Success);

        Assert.True(store.TryGet(entity, out TransformComponent result));
        Assert.Equal(current.Position, result.Position);
        Assert.Equal(current.Orientation, result.Orientation);
        Assert.Equal(current.Scale, result.Scale);
    }

    [Fact]
    public void SerializeFailsWithMessageWhenTheEntityHasNoComponent()
    {
        var store = new DataStore();
        int entity = store.Alloc();

        Result<byte[]> result = new TransformCodec().Serialize(store, entity);

        Assert.False(result.Success);
        Assert.Contains(nameof(TransformComponent), result.Message);
    }

    private static Result ApplyMessage(TransformMessage message)
    {
        var store = new DataStore();
        int entity = store.Alloc();
        return new TransformCodec().Apply(store, entity, message.Serialize());
    }
}
