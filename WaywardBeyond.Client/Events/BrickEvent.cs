using WaywardBeyond.Bricks;

namespace WaywardBeyond.Client.Events;

/// <summary>Raised when a brick event occurs in the world for presentation listeners.</summary>
internal readonly struct BrickEvent(ushort brickID, BrickEventKind kind)
{
    public readonly ushort BrickID = brickID;
    public readonly BrickEventKind Kind = kind;
}
