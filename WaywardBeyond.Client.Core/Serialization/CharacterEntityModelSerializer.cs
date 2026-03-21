using System;
using System.Numerics;
using Swordfish.Library.Serialization;
using WaywardBeyond.Client.Core.Characters;
using WaywardBeyond.Client.Core.Saves;
using WaywardBeyond.Client.Core.Voxels;
using WaywardBeyond.Client.Core.Voxels.Models;

namespace WaywardBeyond.Client.Core.Serialization;

internal class CharacterEntityModelSerializer : ISerializer<CharacterEntityModel>
{
    public byte[] Serialize(CharacterEntityModel value)
    {
        var characterEntityData = new CharacterEntityData(
            value.Guid.ToString(),
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

        Guid guid = Guid.TryParse(characterEntityData.Guid, out Guid parsedGuid) ? parsedGuid : Guid.NewGuid();
        var position = new Vector3((float)characterEntityData.X, (float)characterEntityData.Y, (float)characterEntityData.Z);
        var orientation = new Quaternion(characterEntityData.OrientationX, characterEntityData.OrientationY, characterEntityData.OrientationZ, characterEntityData.OrientationW);
        
        return new CharacterEntityModel(guid, position, orientation, characterEntityData.GameMode);
    }
}