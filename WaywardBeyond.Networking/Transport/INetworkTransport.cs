using Swordfish.Library.Util;

namespace WaywardBeyond.Networking.Transport;

/// <summary>
/// One endpoint of a bidirectional message channel. Messages are typed and (de)serialized by the
/// registered <see cref="Serialization.INetworkSerializer"/> set; polling is per message type.
/// </summary>
public interface INetworkTransport
{
    /// <summary>True while the endpoint can send and receive messages.</summary>
    bool IsConnected { get; }

    /// <summary>True for the in-process loopback endpoint, false for a socket endpoint.</summary>
    bool IsLocal { get; }

    /// <summary>Serializes and sends a message; fails when the transport is down or unregistered.</summary>
    Result Send<T>(in T message);

    /// <summary>Dequeues the next received message of type <typeparamref name="T"/>, or fails when none is queued.</summary>
    Result<T> Receive<T>();
}