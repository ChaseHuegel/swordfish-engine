using System;
using Microsoft.Extensions.Logging;
using NsdRpc;
using NsdRpc.Tcp;
using WaywardBeyond.Config;

namespace WaywardBeyond.Networking.Rpc;

/// <summary>
/// Maps <c>network.toml</c> to the nsd-rpc options. The RPC peer and the TCP transport read the
/// options once at construction.
/// </summary>
public static class GameRpcOptions
{
    /// <summary>Builds the peer options. <paramref name="logger"/> receives the library drop and error events.</summary>
    public static RpcOptions CreateRpc(NetworkingConfig config, ILogger logger)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        return new RpcOptions
        {
            Timeout = TimeSpan.FromMilliseconds(Math.Max(0, config.Protocol.CallTimeoutMs.Get())),
            MaxPendingCalls = Math.Max(1, config.Protocol.MaxPendingCalls.Get()),
            MaxBufferedChunks = Math.Max(1, config.Protocol.MaxBufferedChunks.Get()),
            LogSink = RpcLogging.ToLogSink(logger),
        };
    }

    /// <summary>Builds the TCP options for one client or listener. A zero port binds an ephemeral port.</summary>
    public static TcpOptions CreateTcp(NetworkingConfig config, string host, int port)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        int sendQueueBytes = Math.Max(1, config.Transport.SendQueueBytes.Get());
        int backlogBytes = config.Transport.SendBacklogDisconnectBytes.Get();
        int backlogMs = config.Transport.SendBacklogDisconnectMs.Get();
        if (backlogBytes > sendQueueBytes)
        {
            throw new InvalidOperationException(
                $"Transport.SendBacklogDisconnectBytes ({backlogBytes}) must not exceed Transport.SendQueueBytes ({sendQueueBytes}).");
        }

        if ((backlogBytes > 0) != (backlogMs > 0))
        {
            throw new InvalidOperationException(
                "Transport.SendBacklogDisconnectBytes and Transport.SendBacklogDisconnectMs must both be zero or both be positive.");
        }

        return new TcpOptions
        {
            Host = host,
            Port = port,
            ConnectTimeout = TimeSpan.FromMilliseconds(Math.Max(0, config.Transport.ConnectTimeoutMs.Get())),
            IdleTimeout = TimeSpan.FromMilliseconds(Math.Max(0, config.Transport.IdleTimeoutMs.Get())),
            SendQueueBytes = sendQueueBytes,
            SendBacklogDisconnectBytes = Math.Max(0, backlogBytes),
            SendBacklogDisconnectMs = Math.Max(0, backlogMs),
            SendBatchInterval = TimeSpan.FromMilliseconds(Math.Max(0, config.Transport.SendBatchIntervalMs.Get())),
            SendBatchMaxBytes = Math.Max(1, config.Transport.SendBatchMaxBytes.Get()),
        };
    }
}
