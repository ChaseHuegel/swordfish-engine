namespace WaywardBeyond.Shared.Networking.Transport;

/// <summary>
/// Why a peer connection ended, reported by the transport's <see cref="TcpTransport.OnDisconnected"/>.
/// </summary>
public enum DisconnectReason
{
    /// <summary>The peer closed the connection cleanly (EOF on the stream).</summary>
    PeerClosed,

    /// <summary>A read failed or violated the protocol (e.g. an oversized frame prefix).</summary>
    ReadError,

    /// <summary>A read timed out: the peer stopped delivering bytes within the socket timeout.</summary>
    ReadTimeout,

    /// <summary>A socket write failed.</summary>
    WriteError,

    /// <summary>A socket write timed out: the peer stopped reading.</summary>
    WriteTimeout,

    /// <summary>The reliable send backlog exceeded the disconnect threshold for the disconnect window (see #0004).</summary>
    BacklogLimit,
}