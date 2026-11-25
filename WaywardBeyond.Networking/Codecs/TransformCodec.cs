using System;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>Replicates <see cref="TransformComponent"/> as <see cref="TransformMessage"/>.</summary>
public sealed class TransformCodec : IPayloadCodec<TransformComponent>
{
    //  A quaternion at or below this squared length cannot describe a rotation.
    private const float MIN_ORIENTATION_LENGTH_SQ = 1e-12f;

    /// <inheritdoc/>
    public Result<byte[]> Serialize(DataStore store, int entity)
    {
        return store.TryGet(entity, out TransformComponent transform)
            ? Result<byte[]>.FromSuccess(Serialize(in transform))
            : Result<byte[]>.FromFailure($"entity {entity} has no {nameof(TransformComponent)} to serialize.");
    }

    /// <inheritdoc/>
    public Result Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        TransformMessage message = TransformMessage.Deserialize(payload);
        var position = new Vector3(message.PositionX, message.PositionY, message.PositionZ);
        var orientation = new Quaternion(message.OrientationX, message.OrientationY, message.OrientationZ, message.OrientationW);
        var scale = new Vector3(message.ScaleX, message.ScaleY, message.ScaleZ);

        if (!IsValid(in position, in orientation, in scale))
        {
            return Result.FromFailure($"entity {entity} has a non-finite transform or a degenerate orientation.");
        }

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

    /// <summary>Whether a replicated transform is finite and carries a usable orientation.</summary>
    public static bool IsValid(in Vector3 position, in Quaternion orientation, in Vector3 scale)
    {
        return float.IsFinite(position.X) && float.IsFinite(position.Y) && float.IsFinite(position.Z)
            && float.IsFinite(scale.X) && float.IsFinite(scale.Y) && float.IsFinite(scale.Z)
            && float.IsFinite(orientation.X) && float.IsFinite(orientation.Y)
            && float.IsFinite(orientation.Z) && float.IsFinite(orientation.W)
            && orientation.LengthSquared() > MIN_ORIENTATION_LENGTH_SQ;
    }
}
