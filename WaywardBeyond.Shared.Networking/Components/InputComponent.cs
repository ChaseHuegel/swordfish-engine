using System.Numerics;
using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Components;

public struct InputComponent : IDataComponent
{
    public Vector3 Movement;
    public Vector2 LookDelta;
    public bool Jump;
    public uint SequenceNumber;
    public uint ServerTickAtSample;
}
