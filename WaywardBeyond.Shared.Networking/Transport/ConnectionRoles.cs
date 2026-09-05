namespace WaywardBeyond.Shared.Networking.Transport;

/// <summary>The client half of a connection; used to send upstream and receive downstream messages.</summary>
public interface IClientConnection : INetworkTransport { }

/// <summary>The server half of a connection; used to receive upstream and send downstream messages.</summary>
public interface IServerConnection : INetworkTransport { }