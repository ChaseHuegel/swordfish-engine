using Swordfish.Library.Serialization;
using WaywardBeyond.Shared.Data;

namespace WaywardBeyond.Client.Core.Serialization;

internal class LevelSerializer : ISerializer<Level>
{
    public byte[] Serialize(Level value)
    {
        return value.Serialize();
    }

    public Level Deserialize(byte[] data)
    {
        return Level.Deserialize(data);
    }
}