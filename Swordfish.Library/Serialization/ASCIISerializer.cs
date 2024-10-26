using System.Text;

namespace Swordfish.Library.Serialization;

public class ASCIISerializer : ISerializer<string>
{
    public byte[] Serialize(string value)
    {
        return Encoding.ASCII.GetBytes(value);
    }

    public string Deserialize(byte[] data)
    {
        return Encoding.ASCII.GetString(data);
    }
}