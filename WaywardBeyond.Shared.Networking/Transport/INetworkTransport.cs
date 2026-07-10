using Swordfish.Library.Util;

namespace WaywardBeyond.Shared.Networking.Transport;

public interface INetworkTransport
{
    bool IsConnected { get; }
    bool IsLocal { get; }

    Result Send<T>(in T message);

    Result<T> Receive<T>();
}
