using Swordfish.Bricks;

namespace WaywardBeyond.Client.Core.Bricks;

internal struct BrickGridItem
{
    public readonly Brick Brick;
    public readonly int X;
    public readonly int Y;
    public readonly int Z;

    public BrickGridItem(Brick brick, int x, int y, int z)
    {
        Brick = brick;
        X = x;
        Y = y;
        Z = z;
    }
}