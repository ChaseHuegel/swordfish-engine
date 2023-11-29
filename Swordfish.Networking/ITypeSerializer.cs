namespace Swordfish.Networking;

public interface ITypeSerializer<TModelIn, TModelOut> : ISerializer<TModelOut> where TModelOut : new()
{
    ArraySegment<byte> Serialize(TModelIn target);
}

public interface ITypeSerializer<TModel> : ITypeSerializer<TModel, TModel> where TModel : new()
{
}
