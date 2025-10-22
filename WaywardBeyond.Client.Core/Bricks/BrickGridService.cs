using Swordfish.Bricks;
using Swordfish.ECS;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.Bricks;

internal sealed class BrickGridService(in IBrickDecorator[] brickDecorators, in BrickDatabase brickDatabase)
{
    private readonly IBrickDecorator[] _brickDecorators = brickDecorators;
    private readonly BrickDatabase _brickDatabase = brickDatabase;

    public void SetBrick(DataStore store, int entity, BrickGrid grid, int x, int y, int z, Brick brick)
    {
        Brick oldBrick = grid.Get(x, y, z);
        if (!grid.Set(x, y, z, brick))
        {
            return;
        }
        
        Result<BrickInfo> brickInfoResult = _brickDatabase.Get(oldBrick.ID);
        if (brickInfoResult)
        {
            for (var i = 0; i < _brickDecorators.Length; i++)
            {
                _brickDecorators[i].OnBrickRemoved(store, entity, grid, x, y, z, oldBrick, brickInfoResult.Value);
            }
        }
        
        brickInfoResult = _brickDatabase.Get(brick.ID);
        if (brickInfoResult)
        {
            for (var i = 0; i < _brickDecorators.Length; i++)
            {
                _brickDecorators[i].OnBrickAdded(store, entity, grid, x, y, z, brick, brickInfoResult.Value);
            }
        }
    }
}