namespace Swordfish.Networking;

public interface IDataReader<TData> : IDisposable
{
    event EventHandler<TData>? Received;
}