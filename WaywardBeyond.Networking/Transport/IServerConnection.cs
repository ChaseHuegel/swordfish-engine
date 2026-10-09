using Swordfish.Library.Util;

namespace WaywardBeyond.Networking.Transport;

/// <summary>The server half of a connection; used to receive upstream and send downstream messages.</summary>
public interface IServerConnection : INetworkTransport
{
    /// <summary>
    /// Sends an already-framed wire frame ([body length][type-tag length][type tag][payload]) as
    /// per-tick traffic, bypassing message serialization. Used by the replication publish to fan a
    /// serialized-once snapshot out to every client. The transport queues the buffer as-is; the caller
    /// must not touch it afterward.
    /// </summary>
    Result SendRaw(in byte[] frame);
}