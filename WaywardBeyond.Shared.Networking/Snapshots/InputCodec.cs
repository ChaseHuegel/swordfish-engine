using System;
using System.Numerics;
using Swordfish.ECS;
using WaywardBeyond.Shared.Networking.Components;
using WaywardBeyond.Shared.Networking.Registry;

namespace WaywardBeyond.Shared.Networking.Snapshots;

public sealed class InputCodec : IPayloadCodec
{
    public Type ComponentType => typeof(InputComponent);

    public byte[] Serialize(DataStore store, int entity)
    {
        if (!store.TryGet(entity, out InputComponent input))
        {
            return Array.Empty<byte>();
        }

        var message = new InputMessage(
            input.Movement.X, input.Movement.Y, input.Movement.Z,
            input.LookDelta.X, input.LookDelta.Y,
            input.Jump,
            input.SequenceNumber,
            input.ServerTickAtSample
        );
        return message.Serialize();
    }

    public void Apply(DataStore store, int entity, ReadOnlySpan<byte> payload)
    {
        InputMessage message = InputMessage.Deserialize(payload);

        store.AddOrUpdate(entity, new InputComponent
        {
            Movement = new Vector3(message.MovementX, message.MovementY, message.MovementZ),
            LookDelta = new Vector2(message.LookDeltaX, message.LookDeltaY),
            Jump = message.Jump,
            SequenceNumber = message.SequenceNumber,
            ServerTickAtSample = message.ServerTickAtSample,
        });
    }
}