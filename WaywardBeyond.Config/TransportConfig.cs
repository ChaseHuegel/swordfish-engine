using Swordfish.Library.Types;

namespace WaywardBeyond.Config;

/// <summary>Network transport configuration.</summary>
public sealed record TransportConfig
{
    /// <summary>The deadline for one TCP connect attempt, in milliseconds. Zero disables it.</summary>
    public DataBinding<int> ConnectTimeoutMs { get; private set; } = new(5_000);

    /// <summary>Closes a connection when no bytes arrive within this window, in milliseconds. Zero disables it.</summary>
    public DataBinding<int> IdleTimeoutMs { get; private set; } = new(5_000);

    /// <summary>Coalesces queued frames for this interval into one socket write, in milliseconds. Zero sends immediately.</summary>
    public DataBinding<int> SendBatchIntervalMs { get; private set; } = new(16);

    /// <summary>The largest bytes in one batched socket write.</summary>
    public DataBinding<int> SendBatchMaxBytes { get; private set; } = new(65_536);

    /// <summary>The hard cap on queued send bytes for one connection.</summary>
    public DataBinding<int> SendQueueBytes { get; private set; } = new(4_194_304);

    /// <summary>Queued send bytes that start the backlog disconnect window. Must not exceed <see cref="SendQueueBytes"/>.</summary>
    public DataBinding<int> SendBacklogDisconnectBytes { get; private set; } = new(1_048_576);

    /// <summary>How long the backlog may stay over its threshold before the peer is closed, in milliseconds.</summary>
    public DataBinding<int> SendBacklogDisconnectMs { get; private set; } = new(10_000);
}
