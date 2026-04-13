using WaywardBeyond.Client.Core.Bricks;

namespace WaywardBeyond.Client.Core.Events;

internal readonly struct PlaceEvent(BrickInfo brickInfo)
{
    public readonly BrickInfo BrickInfo = brickInfo;
}