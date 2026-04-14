using WaywardBeyond.Client.Core.Bricks;

namespace WaywardBeyond.Client.Core.Events;

internal readonly struct BreakEvent(BrickInfo brickInfo)
{
    public readonly BrickInfo BrickInfo = brickInfo;
}