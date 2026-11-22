using System;
using System.Numerics;
using Krill;
using Swordfish.ECS;
using Swordfish.Library.Util;
using Swordfish.Physics;
using WaywardBeyond.Data.Networking;

namespace WaywardBeyond.Networking.Codecs;

/// <summary>Replicates <see cref="PhysicsComponent"/> velocity and body configuration as <see cref="PhysicsMessage"/>.</summary>
public sealed class PhysicsCodec : IPayloadCodec<PhysicsComponent>
{
    /// <inheritdoc/>
    public Result<byte[]> Serialize(DataStore store, int entity)
    {
        return store.TryGet(entity, out PhysicsComponent physics)
            ? Result<byte[]>.FromSuccess(Serialize(in physics))
            : Result<byte[]>.FromFailure($"entity {entity} has no {nameof(PhysicsComponent)} to serialize.");
    }

    /// <inheritdoc/>
    public Result Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        PhysicsMessage message = PhysicsMessage.Deserialize(payload);

        if (message.Layer is not (Layers.NON_MOVING or Layers.MOVING))
        {
            return Result.FromFailure($"entity {entity} has an invalid physics layer {message.Layer}.");
        }

        if ((BodyType)message.BodyType is not (BodyType.Static or BodyType.Kinematic or BodyType.Dynamic))
        {
            return Result.FromFailure($"entity {entity} has an invalid body type {message.BodyType}.");
        }

        if ((CollisionDetection)message.CollisionDetection is not (CollisionDetection.Discrete or CollisionDetection.Continuous))
        {
            return Result.FromFailure($"entity {entity} has an invalid collision detection {message.CollisionDetection}.");
        }

        var velocity = new Vector3(message.VelocityX, message.VelocityY, message.VelocityZ);
        var torque = new Vector3(message.AngularVelocityX, message.AngularVelocityY, message.AngularVelocityZ);
        if (!IsFinite(in velocity) || !IsFinite(in torque))
        {
            return Result.FromFailure($"entity {entity} has non-finite physics state.");
        }

        if (!store.TryGet(entity, out PhysicsComponent _))
        {
            //  The client builds its own bodies for the local player and the world. A body it did not
            //  build (a remote player) materializes from the authoritative snapshot instead.
            store.AddOrUpdate(entity, new PhysicsComponent(
                message.Layer,
                (BodyType)message.BodyType,
                (CollisionDetection)message.CollisionDetection,
                velocity,
                torque
            ));

            return Result.FromSuccess();
        }

        var write = new PhysicsWrite(in velocity, in torque);
        store.QueryRef<PhysicsComponent, PhysicsWrite>(entity, 0f, ref write);
        return Result.FromSuccess();
    }

    /// <inheritdoc/>
    public byte[] Serialize(in PhysicsComponent value)
    {
        var message = new PhysicsMessage
        {
            VelocityX = value.Velocity.X,
            VelocityY = value.Velocity.Y,
            VelocityZ = value.Velocity.Z,
            AngularVelocityX = value.Torque.X,
            AngularVelocityY = value.Torque.Y,
            AngularVelocityZ = value.Torque.Z,
            Layer = value.Layer,
            BodyType = (byte)value.BodyType,
            CollisionDetection = (byte)value.CollisionDetection,
        };

        return message.Serialize();
    }

    private static bool IsFinite(in Vector3 value)
    {
        return float.IsFinite(value.X) && float.IsFinite(value.Y) && float.IsFinite(value.Z);
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
