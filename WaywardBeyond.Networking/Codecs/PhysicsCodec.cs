using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Networking.Registry;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>
/// Maps the engine <see cref="PhysicsComponent"/> onto the canonical <see cref="PhysicsMessage"/> wire
/// shape. The engine component's <c>Torque</c> field holds angular velocity only at sync boundaries
/// (set from <c>body.GetAngularVelocity()</c> during the Jolt sync cycle); the codec therefore treats
/// it as angular velocity. The component's <c>Layer</c>/<c>BodyType</c>/<c>CollisionDetection</c> are
/// constructor-only and are never fabricated here - the server owns body construction at spawn.
/// </summary>
public sealed class PhysicsCodec : IPayloadCodec
{
    /// <inheritdoc/>
    public Type ComponentType => typeof(PhysicsComponent);

    /// <inheritdoc/>
    public Result<byte[]> Serialize(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out PhysicsComponent physics))
        {
            return Result<byte[]>.FromFailure($"Entity {entity} has no PhysicsComponent.");
        }

        var message = new PhysicsMessage(
            physics.Velocity.X, physics.Velocity.Y, physics.Velocity.Z,
            physics.Torque.X, physics.Torque.Y, physics.Torque.Z
        );
        return Result<byte[]>.FromSuccess(message.Serialize());
    }

    /// <inheritdoc/>
    public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        PhysicsMessage message = PhysicsMessage.Deserialize(payload);

        store.QueryRef(entity, 0f, (float _, DataStore _, int _, ref Ref<PhysicsComponent> physics) =>
        {
            physics.Write.Velocity = new Vector3(message.VelocityX, message.VelocityY, message.VelocityZ);
            physics.Write.Torque = new Vector3(message.AngularVelocityX, message.AngularVelocityY, message.AngularVelocityZ);
        });
    }
}