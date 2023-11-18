using Needlefish;

namespace Swordfish.Networking.Serialization;

public class NeedlefishSerializer<TModel> : ISerializer<TModel> where TModel : IDataBody
{
    public ArraySegment<byte> Serialize(TModel model)
    {
        byte[] buffer = NeedlefishFormatter.Serialize(model);
        return new ArraySegment<byte>(buffer);
    }

    public T Deserialize<T>(ArraySegment<byte> data) where T : TModel, new()
    {
        return NeedlefishFormatter.Deserialize<T>(data.Array);
    }
}