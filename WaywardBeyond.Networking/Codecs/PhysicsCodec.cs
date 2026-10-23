using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Util;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>Replicates <see cref="PhysicsComponent"/> velocity as <see cref="PhysicsMessage"/>.</summary>
public sealed class PhysicsCodec : IPayloadCodec<PhysicsComponent>
{
    /// <inheritdoc/>
    public Result<byte[]> Serialize(DataStore store, int entity)
    {
        return store.TryGet(entity, out PhysicsComponent physics)
            ? Result<byte[]>.FromSuccess(Serialize(in physics))
            : Result<byte[]>.FromFailure((string?)null);
    }

    /// <inheritdoc/>
    public Result Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        if (!store.TryGet(entity, out PhysicsComponent physics))
        {
            return Result.FromFailure((string?)null);
        }

        PhysicsMessage message = PhysicsMessage.Deserialize(payload);
        var write = new PhysicsWrite(
            new Vector3(message.VelocityX, message.VelocityY, message.VelocityZ),
            new Vector3(message.AngularVelocityX, message.AngularVelocityY, message.AngularVelocityZ)
        );

        store.QueryRef<PhysicsComponent, PhysicsWrite>(entity, 0f, ref write);
        return Result.FromSuccess();
    }

    /// <inheritdoc/>
    public byte[] Serialize(in PhysicsComponent value)
    {
        var message = new PhysicsMessage(
            value.Velocity.X, value.Velocity.Y, value.Velocity.Z,
            value.Torque.X, value.Torque.Y, value.Torque.Z
        );

        return message.Serialize();
    }

    private struct PhysicsWrite : IForEachRef<PhysicsComponent>
    {
        private readonly Vector3 _velocity;
        private readonly Vector3 _torque;

        public PhysicsWrite(in Vector3 velocity, in Vector3 torque)
        {
            _velocity = velocity;
            _torque = torque;
        }

        public void Execute(float delta, DataStore store, int entity, ref Ref<PhysicsComponent> physics)
        {
            ref readonly PhysicsComponent current = ref physics.Read;
            if (current.Velocity == _velocity && current.Torque == _torque)
            {
                return;
            }

            ref PhysicsComponent value = ref physics.Write;
            value.Velocity = _velocity;
            value.Torque = _torque;
        }
    }
}
