namespace WaywardBeyond.Networking.Transport;

/// <summary>
/// Monotonic frame/byte counters for a transport, incremented on the actual send and receive paths.
/// Rates are computed by consumers (e.g. the F3 stats overlay); transports never sample.
/// </summary>
public interface IConnectionCounters
{
    long PacketsSent { get; }
    long PacketsReceived { get; }
    long BytesSent { get; }
    long BytesReceived { get; }
}