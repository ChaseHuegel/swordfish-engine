using WaywardBeyond.Shared.Networking.Components;

namespace WaywardBeyond.Shared.Networking.Snapshots;

public static class EntitySnapshotExtensions
{
    public static ClientInputMsg ToMessage(in this InputComponent input, uint serverTick)
    {
        return new ClientInputMsg
        {
            SequenceNumber = input.SequenceNumber,
            MovementX = input.Movement.X,
            MovementY = input.Movement.Y,
            MovementZ = input.Movement.Z,
            LookDeltaX = input.LookDelta.X,
            LookDeltaY = input.LookDelta.Y,
            Jump = input.Jump,
            ServerTickAtSample = serverTick,
        };
    }

    public static InputComponent ToComponent(in this ClientInputMsg msg)
    {
        return new InputComponent
        {
            Movement = new System.Numerics.Vector3(msg.MovementX, msg.MovementY, msg.MovementZ),
            LookDelta = new System.Numerics.Vector2(msg.LookDeltaX, msg.LookDeltaY),
            Jump = msg.Jump,
            SequenceNumber = msg.SequenceNumber,
            ServerTickAtSample = msg.ServerTickAtSample,
        };
    }
}
