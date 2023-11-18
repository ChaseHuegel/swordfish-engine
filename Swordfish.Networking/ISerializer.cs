namespace Swordfish.Networking;

public interface ISerializer<TModel> where TModel : new()
{
    ArraySegment<byte> Serialize(object target);

    TModel Deserialize(ArraySegment<byte> data);
}