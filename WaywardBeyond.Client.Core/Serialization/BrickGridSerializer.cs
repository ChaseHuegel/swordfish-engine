using Swordfish.Bricks;
using Swordfish.Library.Serialization;

namespace WaywardBeyond.Client.Core.Serialization;

internal class BrickGridSerializer : ISerializer<BrickGrid>
{
    public byte[] Serialize(BrickGrid value)
    {
        var rawBrickGrid = value.ToRawBrickGrid();
        return rawBrickGrid.Serialize();
    }

    public BrickGrid Deserialize(byte[] data)
    {
        RawBrickGrid rawBrickGrid = RawBrickGrid.Deserialize(data);

        var brickGrid = new BrickGrid(dimensionSize: 16);
        for (var i = 0; i < rawBrickGrid.Bricks.Length; i++)
        {
            RawBrick rawBrick = rawBrickGrid.Bricks[i];
            var orientation = new BrickOrientation(rawBrick.Orientation);
            brickGrid.Set(rawBrick.X, rawBrick.Y, rawBrick.Z, new Brick(rawBrick.ID, rawBrick.Data, orientation));
        }

        return brickGrid;
    }
}