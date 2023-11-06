namespace Swordfish.Networking;

public interface ISerializer<TModel>
{
    ArraySegment<byte> Serialize(TModel target);

    T Deserialize<T>(ArraySegment<byte> data) where T : TModel, new();
}