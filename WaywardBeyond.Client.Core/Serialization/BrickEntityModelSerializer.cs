using System.Collections.Generic;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.Library.Serialization;
using WaywardBeyond.Client.Core.Bricks;

namespace WaywardBeyond.Client.Core.Serialization;

internal class BrickEntityModelSerializer : ISerializer<BrickEntityModel>
{
    private readonly Dictionary<ushort, ushort> _brickRemapping = [];

    public BrickEntityModelSerializer(in BrickDatabase brickDatabase)
    {
        _brickRemapping[1] = brickDatabase.Get("caution_panel").Value.DataID;
        _brickRemapping[2] = brickDatabase.Get("display_console").Value.DataID;
        _brickRemapping[3] = brickDatabase.Get("display_control").Value.DataID;
        _brickRemapping[4] = brickDatabase.Get("display_monitor").Value.DataID;
        _brickRemapping[5] = brickDatabase.Get("glass").Value.DataID;
        _brickRemapping[6] = brickDatabase.Get("ice").Value.DataID;
        _brickRemapping[7] = brickDatabase.Get("panel").Value.DataID;
        _brickRemapping[8] = brickDatabase.Get("rock").Value.DataID;
        _brickRemapping[9] = brickDatabase.Get("ship_core").Value.DataID;
        _brickRemapping[10] = brickDatabase.Get("storage").Value.DataID;
        _brickRemapping[11] = brickDatabase.Get("thruster").Value.DataID;
        _brickRemapping[12] = brickDatabase.Get("truss").Value.DataID;
    }

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
            
            ushort id = _brickRemapping.TryGetValue(rawBrick.ID, out ushort remappedID) ? remappedID : rawBrick.ID;
            brickGrid.Set(rawBrick.X, rawBrick.Y, rawBrick.Z, new Brick(id, rawBrick.Data, brickOrientation));
        }

        var position = new Vector3(rawBrickGrid.X ?? 0, rawBrickGrid.Y ?? 0, rawBrickGrid.Z ?? 0);
        var orientation = new Quaternion(rawBrickGrid.OrientationX ?? 0, rawBrickGrid.OrientationY ?? 0, rawBrickGrid.OrientationZ ?? 0, rawBrickGrid.OrientationW ?? 1);
        return new BrickEntityModel(position, orientation, brickGrid);
    }
}