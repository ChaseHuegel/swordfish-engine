using System.Numerics;
using WaywardBeyond.Bricks;

namespace WaywardBeyond.Client.Events;

/// <summary>Raised when a brick face paint override is applied or cleared in world space.</summary>
internal readonly struct PaintFaceEvent(
    PaintKind mode,
    PaintFaceAction action,
    ushort paintId,
    Vector3 worldPosition,
    Vector3 normal
) {
    public readonly PaintKind Mode = mode;
    public readonly PaintFaceAction Action = action;
    public readonly ushort PaintId = paintId;
    public readonly Vector3 WorldPosition = worldPosition;
    public readonly Vector3 Normal = normal;
}
