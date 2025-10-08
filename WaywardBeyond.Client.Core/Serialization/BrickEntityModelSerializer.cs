using System.Numerics;
using Swordfish.Bricks;
using Swordfish.Library.Serialization;
using WaywardBeyond.Client.Core.Bricks;

namespace WaywardBeyond.Client.Core.Serialization;

internal class BrickEntityModelSerializer : ISerializer<BrickEntityModel>
{
    public byte[] Serialize(BrickEntityModel value)
    {
        var rawBrickGrid = value.Grid.ToRawBrickGrid();
        
        rawBrickGrid.X = value.Position.X;
        rawBrickGrid.Y = value.Position.Y;
        rawBrickGrid.Z = value.Position.Z;
        
        rawBrickGrid.OrientationX = value.Orientation.X;
        rawBrickGrid.OrientationY = value.Orientation.Y;
        rawBrickGrid.OrientationZ = value.Orientation.Z;
        rawBrickGrid.OrientationW = value.Orientation.W;
        
        return rawBrickGrid.Serialize();
    }

    public BrickEntityModel Deserialize(byte[] data)
    {
        RawBrickGrid rawBrickGrid = RawBrickGrid.Deserialize(data);

        var brickGrid = new BrickGrid(dimensionSize: 16);
        for (var i = 0; i < rawBrickGrid.Bricks.Length; i++)
        {
            RawBrick rawBrick = rawBrickGrid.Bricks[i];
            var brickOrientation = new BrickOrientation(rawBrick.Orientation);
            brickGrid.Set(rawBrick.X, rawBrick.Y, rawBrick.Z, new Brick(rawBrick.ID, rawBrick.Data, brickOrientation));
        }

        var position = new Vector3(rawBrickGrid.X ?? 0, rawBrickGrid.Y ?? 0, rawBrickGrid.Z ?? 0);
        var orientation = new Quaternion(rawBrickGrid.OrientationX ?? 0, rawBrickGrid.OrientationY ?? 0, rawBrickGrid.OrientationZ ?? 0, rawBrickGrid.OrientationW ?? 1);
        return new BrickEntityModel(position, orientation, brickGrid);
    }
}