using System;
using System.Diagnostics;
using NsdRpc;
using NsdRpc.Tcp;

namespace WaywardBeyond.Config;

/// <summary>Maps <see cref="NetworkingConfig"/> onto the nsd-rpc options.</summary>
public static class NetworkingConfigExtensions
{
    /// <summary>The hard frame cap. The fragment budget must stay below it.</summary>
    private const int FRAME_BYTES = 65_570;

    /// <summary>Builds the peer options. <paramref name="logSink"/> receives the library drop and error events.</summary>
    public static RpcOptions ToRpcOptions(this NetworkingConfig config, Action<TraceEventType, string, Exception?>? logSink = null)
    {
        return new RpcOptions
        {
            Timeout = TimeSpan.FromMilliseconds(Math.Max(0, config.Protocol.CallTimeoutMs.Get())),
            MaxFrameBytes = FRAME_BYTES,
            MaxPendingCalls = Math.Max(1, config.Protocol.MaxPendingCalls.Get()),
            MaxBufferedChunks = Math.Max(1, config.Protocol.MaxBufferedChunks.Get()),
            LogSink = logSink,
        };
    }

    /// <summary>Builds the TCP options for one client or listener. A zero port binds an ephemeral port.</summary>
    public static TcpOptions ToTcpOptions(this NetworkingConfig config, string host, int port)
    {
        return new TcpOptions
        {
            Host = host,
            Port = port,
            ConnectTimeout = TimeSpan.FromMilliseconds(Math.Max(0, config.Transport.ConnectTimeoutMs.Get())),
            IdleTimeout = TimeSpan.FromMilliseconds(Math.Max(0, config.Transport.IdleTimeoutMs.Get())),
            SendQueueBytes = Math.Max(1, config.Transport.SendQueueBytes.Get()),
            SendBacklogDisconnectBytes = Math.Max(0, config.Transport.SendBacklogDisconnectBytes.Get()),
            SendBacklogDisconnectMs = Math.Max(0, config.Transport.SendBacklogDisconnectMs.Get()),
            SendBatchInterval = TimeSpan.FromMilliseconds(Math.Max(0, config.Transport.SendBatchIntervalMs.Get())),
            SendBatchMaxBytes = Math.Max(1, config.Transport.SendBatchMaxBytes.Get()),
        };
    }
}
