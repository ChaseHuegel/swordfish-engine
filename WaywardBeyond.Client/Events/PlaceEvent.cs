using WaywardBeyond.Bricks;

namespace WaywardBeyond.Client.Events;

internal readonly struct PlaceEvent(Brick brick)
{
    public readonly Brick Brick = brick;
}