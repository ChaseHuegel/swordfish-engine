using System;
using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Shared.Gameplay;

/// <summary>
/// Maps the engine <see cref="PhysicsComponent"/> onto the canonical <see cref="PhysicsMessage"/> wire
/// shape. The engine component's <c>Torque</c> field holds angular velocity only at sync boundaries
/// (set from <c>body.GetAngularVelocity()</c> during the Jolt sync cycle); the codec therefore treats
/// it as angular velocity. The component's <c>Layer</c>/<c>BodyType</c>/<c>CollisionDetection</c> are
/// constructor-only and are never fabricated here - the server owns body construction at spawn.
/// </summary>
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
            physics.Write.Torque = new Vector3(message.AngularVelocityX, message.AngularVelocityY, message.AngularVelocityZ);
        });
    }
}