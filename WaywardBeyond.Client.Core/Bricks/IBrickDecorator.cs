using Swordfish.Bricks;
using Swordfish.ECS;

namespace WaywardBeyond.Client.Core.Bricks;

internal interface IBrickDecorator
{
    void OnBrickAdded(DataStore store, int entity, BrickGrid grid, int x, int y, int z, Brick brick, BrickInfo info);

    void OnBrickRemoved(DataStore store, int entity, BrickGrid grid, int x, int y, int z, Brick brick, BrickInfo info);
}