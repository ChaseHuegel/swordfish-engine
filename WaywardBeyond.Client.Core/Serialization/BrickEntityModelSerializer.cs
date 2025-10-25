using System;
using System.Collections.Generic;
using System.Numerics;
using Swordfish.Bricks;
using Swordfish.Library.Serialization;
using WaywardBeyond.Client.Core.Bricks;
using WaywardBeyond.Client.Core.Saves;

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
        RawBrick[] rawBricks = value.Grid.ToRawBricks();

        var brickEntityData = new BrickEntityData(
            value.Guid.ToString(),
            value.Position.X,
            value.Position.Y,
            value.Position.Z,
            value.Orientation.X,
            value.Orientation.Y,
            value.Orientation.Z,
            value.Orientation.W,
            rawBricks
        );
        
        return brickEntityData.Serialize();
    }

    public BrickEntityModel Deserialize(byte[] data)
    {
        BrickEntityData brickEntityData = BrickEntityData.Deserialize(data);

        Guid guid = Guid.TryParse(brickEntityData.Guid, out Guid parsedGuid) ? parsedGuid : Guid.NewGuid();
        var position = new Vector3((float)brickEntityData.X, (float)brickEntityData.Y, (float)brickEntityData.Z);
        var orientation = new Quaternion(brickEntityData.OrientationX, brickEntityData.OrientationY, brickEntityData.OrientationZ, brickEntityData.OrientationW);
        
        var brickGrid = new BrickGrid(dimensionSize: 16);
        for (var i = 0; i < brickEntityData.Bricks.Length; i++)
        {
            RawBrick rawBrick = brickEntityData.Bricks[i];
            var brickOrientation = new BrickOrientation(rawBrick.Orientation);
            
            ushort id = _brickRemapping.TryGetValue(rawBrick.ID, out ushort remappedID) ? remappedID : rawBrick.ID;
            brickGrid.Set(rawBrick.X, rawBrick.Y, rawBrick.Z, new Brick(id, rawBrick.Data, brickOrientation));
        }
        
        return new BrickEntityModel(guid, position, orientation, brickGrid);
    }
}