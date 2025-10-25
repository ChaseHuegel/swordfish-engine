using Swordfish.Library.Serialization;
using WaywardBeyond.Client.Core.Saves;

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