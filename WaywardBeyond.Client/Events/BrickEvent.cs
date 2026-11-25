using System.Numerics;
using WaywardBeyond.Bricks;

namespace WaywardBeyond.Client.Events;

/// <summary>Raised when a brick event occurs in the world for presentation listeners.</summary>
internal readonly struct BrickEvent(
    ushort brickID,
    BrickEventKind kind,
    Vector3? worldPosition = null,
    Vector3? normal = null
) {
    public readonly ushort BrickID = brickID;
    public readonly BrickEventKind Kind = kind;
    public readonly Vector3? WorldPosition = worldPosition;
    public readonly Vector3? Normal = normal;
}
