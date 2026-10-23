using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>Replicates <see cref="TransformComponent"/> as <see cref="TransformMessage"/>.</summary>
public sealed class TransformCodec : IPayloadCodec<TransformComponent>
{
    /// <inheritdoc/>
    public Result<byte[]> Serialize(DataStore store, int entity)
    {
        return store.TryGet(entity, out TransformComponent transform)
            ? Result<byte[]>.FromSuccess(Serialize(in transform))
            : Result<byte[]>.FromFailure((string?)null);
    }

    /// <inheritdoc/>
    public Result Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        TransformMessage message = TransformMessage.Deserialize(payload);
        var position = new Vector3(message.PositionX, message.PositionY, message.PositionZ);
        var orientation = new Quaternion(message.OrientationX, message.OrientationY, message.OrientationZ, message.OrientationW);
        var scale = new Vector3(message.ScaleX, message.ScaleY, message.ScaleZ);

        if (store.TryGet(entity, out TransformComponent current)
            && current.Position == position
            && current.Orientation == orientation
            && current.Scale == scale)
        {
            return Result.FromSuccess();
        }

        store.AddOrUpdate(entity, new TransformComponent(position, orientation, scale));
        return Result.FromSuccess();
    }

    /// <inheritdoc/>
    public byte[] Serialize(in TransformComponent value)
    {
        var message = new TransformMessage(
            value.Position.X, value.Position.Y, value.Position.Z,
            value.Orientation.X, value.Orientation.Y, value.Orientation.Z, value.Orientation.W,
            value.Scale.X, value.Scale.Y, value.Scale.Z
        );

        return message.Serialize();
    }
}
