using WaywardBeyond.Bricks;

namespace WaywardBeyond.Client.Events;

internal readonly struct PlaceEvent(BrickInfo brickInfo)
{
    public readonly BrickInfo BrickInfo = brickInfo;
}