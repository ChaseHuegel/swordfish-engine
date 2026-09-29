using WaywardBeyond.Shared.Bricks;

namespace WaywardBeyond.Client.Core.Events;

internal readonly struct BreakEvent(BrickInfo brickInfo)
{
    public readonly BrickInfo BrickInfo = brickInfo;
}