using System;
using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Client.Core.Networking;

public sealed class PhysicsCodec : IPayloadCodec
{
    public Type ComponentType => typeof(PhysicsComponent);

    public byte[] Serialize(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out PhysicsComponent physics))
        {
            return Array.Empty<byte>();
        }

        var message = new PhysicsMessage(
            physics.Velocity.X, physics.Velocity.Y, physics.Velocity.Z,
            physics.Torque.X, physics.Torque.Y, physics.Torque.Z
        );
        return message.Serialize();
    }

    public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        PhysicsMessage message = PhysicsMessage.Deserialize(payload);

        store.QueryRef<PhysicsComponent>(entity, 0f, (float _, DataStore s, int e, ref Ref<PhysicsComponent> physics) =>
        {
            physics.Write.Velocity = new Vector3(message.VelocityX, message.VelocityY, message.VelocityZ);
            physics.Write.Torque = new Vector3(message.TorqueX, message.TorqueY, message.TorqueZ);
        });
    }
}