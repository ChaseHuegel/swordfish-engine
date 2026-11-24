namespace WaywardBeyond.Client.Networking;

/// <summary>Result of probing a remote server for reachability, metadata, and round-trip latency.</summary>
internal readonly record struct ServerProbeResult(
    bool Reachable,
    string ServerName,
    int PlayerCount,
    int PingMs
);
