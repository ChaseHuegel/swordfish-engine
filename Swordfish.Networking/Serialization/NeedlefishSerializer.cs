using Needlefish;

namespace Swordfish.Networking.Serialization;

public class NeedlefishSerializer : ISerializer<IDataBody>
{
    public ArraySegment<byte> Serialize(IDataBody target)
    {
        byte[] buffer = NeedlefishFormatter.Serialize(target);
        return new ArraySegment<byte>(buffer);
    }

    public T Deserialize<T>(ArraySegment<byte> data) where T : IDataBody, new()
    {
        return NeedlefishFormatter.Deserialize<T>(data.Array);
    }
}