using WaywardBeyond.Bricks;

namespace WaywardBeyond.Client.Events;

internal readonly struct BreakEvent(Brick brickInfo)
{
    public readonly Brick Brick = brickInfo;
}