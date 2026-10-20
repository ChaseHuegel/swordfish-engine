using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Registry;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>
/// Maps the engine <see cref="TransformComponent"/> onto the canonical <see cref="TransformMessage"/> wire
/// shape so the authoritative pose replicates through the snapshot path.
/// </summary>
public sealed class TransformCodec : IPayloadCodec
{
    /// <inheritdoc/>
    public Type ComponentType => typeof(TransformComponent);

    /// <inheritdoc/>
    public Result<byte[]> Serialize(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out TransformComponent transform))
        {
            return Result<byte[]>.FromFailure($"Entity {entity} has no TransformComponent.");
        }

        var message = new TransformMessage(
            transform.Position.X, transform.Position.Y, transform.Position.Z,
            transform.Orientation.X, transform.Orientation.Y, transform.Orientation.Z, transform.Orientation.W,
            transform.Scale.X, transform.Scale.Y, transform.Scale.Z
        );
        return Result<byte[]>.FromSuccess(message.Serialize());
    }

    /// <inheritdoc/>
    public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        TransformMessage message = TransformMessage.Deserialize(payload);

        store.AddOrUpdate(entity, new TransformComponent(
            new Vector3(message.PositionX, message.PositionY, message.PositionZ),
            new Quaternion(message.OrientationX, message.OrientationY, message.OrientationZ, message.OrientationW),
            new Vector3(message.ScaleX, message.ScaleY, message.ScaleZ)
        ));
    }
}