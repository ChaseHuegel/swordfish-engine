namespace WaywardBeyond.Shared.Networking.Transport;

/// <summary>The server half of a connection; used to receive upstream and send downstream messages.</summary>
public interface IServerConnection : INetworkTransport { }