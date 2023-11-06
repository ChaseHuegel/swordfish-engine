namespace Swordfish.Networking;

public interface IDataWriter : IDisposable
{
    void Send(ArraySegment<byte> buffer);

    Task SendAsync(ArraySegment<byte> buffer);
}

public interface IDataWriter<TMessage> : IDisposable
{
    void Send(ArraySegment<byte> buffer, TMessage message);

    Task SendAsync(ArraySegment<byte> buffer, TMessage message);
}