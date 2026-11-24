using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NsdRpc;
using NsdRpc.Tcp;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Rpc;

namespace WaywardBeyond.Client.Networking;

/// <summary>Probes remote servers to measure round-trip latency and fetch live server metadata.</summary>
internal sealed class ServerPingService
{
    private readonly NetworkingConfig _config;
    private readonly RpcRegistry _emptyRegistry;

    /// <summary>Creates the server discovery probe service.</summary>
    public ServerPingService(in NetworkingConfig config)
    {
        _config = config;
        _emptyRegistry = new RpcRegistry();
        _emptyRegistry.Freeze();
    }

    /// <summary>Probes <paramref name="host"/>:<paramref name="port"/> and returns reachability, metadata, and latency.</summary>
    public async Task<ServerProbeResult> ProbeAsync(string host, int port, CancellationToken cancellationToken = default)
    {
        int timeoutMs = Math.Max(250, _config.Discovery.PingTimeoutMs.Get());
        TimeSpan timeout = TimeSpan.FromMilliseconds(timeoutMs);
        TcpOptions tcpOptions = _config.ToTcpOptions(host, port);
        tcpOptions.ConnectTimeout = timeout;
        tcpOptions.SendBatchInterval = TimeSpan.Zero;

        RpcOptions rpcOptions = _config.ToRpcOptions();
        rpcOptions.Timeout = timeout;

        var connection = new TcpClientConnection(rpcOptions, tcpOptions);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(timeoutMs);

        try
        {
            await connection.ConnectAsync().WaitAsync(cts.Token).ConfigureAwait(false);
            var client = new RpcClient(connection, _emptyRegistry, rpcOptions);
            var api = new GameServerApiClient(client);

            //  Retry once on an early timeout because a frame sent immediately after connect can
            //  race the server's accept loop on fast links.
            int attemptBudgetMs = Math.Min(250, timeoutMs);
            while (!cts.IsCancellationRequested && connection.IsConnected)
            {
                using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
                attemptCts.CancelAfter(attemptBudgetMs);

                long started = Stopwatch.GetTimestamp();
                RpcResult<ServerDiscovery> response;
                try
                {
                    response = await api
                        .DiscoverAsync(new ServerDiscoveryRequest(), attemptCts.Token)
                        .WaitAsync(attemptCts.Token)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (!cts.IsCancellationRequested && connection.IsConnected)
                {
                    attemptBudgetMs = timeoutMs;
                    continue;
                }

                double elapsedMs = Stopwatch.GetElapsedTime(started).TotalMilliseconds;
                if (!response.IsSuccess || response.Value.ProtocolVersion != GameRpc.PROTOCOL_VERSION)
                {
                    return new ServerProbeResult(false, string.Empty, 0, 0);
                }

                return new ServerProbeResult(
                    Reachable: true,
                    ServerName: response.Value.ServerName ?? string.Empty,
                    PlayerCount: response.Value.PlayerCount,
                    PingMs: (int)Math.Round(elapsedMs)
                );
            }

            return new ServerProbeResult(false, string.Empty, 0, 0);
        }
        catch
        {
            return new ServerProbeResult(false, string.Empty, 0, 0);
        }
        finally
        {
            connection.Close(RpcDisconnectReason.LocalClose);
        }
    }
}
