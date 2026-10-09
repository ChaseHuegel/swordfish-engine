using WaywardBeyond.Bricks;

namespace WaywardBeyond.Client.Events;

internal readonly struct BreakEvent(BrickInfo brickInfo)
{
    public readonly BrickInfo BrickInfo = brickInfo;
}