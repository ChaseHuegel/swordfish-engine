namespace WaywardBeyond.Shared.Networking.Transport;

public interface INetworkTransport
{
    bool IsConnected { get; }
    bool IsLocal { get; }

    void Send<T>(in T message);

    bool TryReceive<T>(out T message);
}
