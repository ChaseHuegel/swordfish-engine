using System;
using System.Numerics;
using Swordfish.ECS;
using Swordfish.Library.Serialization;
using WaywardBeyond.Client.Core.Voxels.Models;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Serialization;

internal class CharacterEntityModelSerializer : ISerializer<CharacterEntityModel>
{
    public byte[] Serialize(CharacterEntityModel value)
    {
        var characterEntityData = new CharacterEntityData(
            value.Uuid.ToValue(),
            value.Position.X,
            value.Position.Y,
            value.Position.Z,
            value.Orientation.X,
            value.Orientation.Y,
            value.Orientation.Z,
            value.Orientation.W,
            value.Scale.X,
            value.Scale.Y,
            value.Scale.Z,
            value.GameMode
        );
        
        return characterEntityData.Serialize();
    }
    
    public CharacterEntityModel Deserialize(byte[] data)
    {
        CharacterEntityData characterEntityData = CharacterEntityData.Deserialize(data);

        var position = new Vector3((float)characterEntityData.X, (float)characterEntityData.Y, (float)characterEntityData.Z);
        var orientation = new Quaternion(characterEntityData.OrientationX, characterEntityData.OrientationY, characterEntityData.OrientationZ, characterEntityData.OrientationW);
        
        return new CharacterEntityModel(Uuid.FromValue(characterEntityData.Uuid), position, orientation, characterEntityData.GameMode);
    }
}