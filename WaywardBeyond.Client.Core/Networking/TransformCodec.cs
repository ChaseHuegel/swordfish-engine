using System;
using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Client.Core.Networking;

public sealed class TransformCodec : IPayloadCodec
{
    public Type ComponentType => typeof(TransformComponent);

    public byte[] Serialize(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out TransformComponent transform))
        {
            return Array.Empty<byte>();
        }

        var message = new TransformMessage(
            transform.Position.X, transform.Position.Y, transform.Position.Z,
            transform.Orientation.X, transform.Orientation.Y, transform.Orientation.Z, transform.Orientation.W,
            transform.Scale.X, transform.Scale.Y, transform.Scale.Z
        );
        return message.Serialize();
    }

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