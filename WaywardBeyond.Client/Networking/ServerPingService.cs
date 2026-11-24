using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using NsdRpc;
using NsdRpc.Tcp;
using WaywardBeyond.Config;
using WaywardBeyond.Data.Networking;
using WaywardBeyond.Networking.Discovery;
using WaywardBeyond.Networking.Rpc;

namespace WaywardBeyond.Client.Networking;

/// <summary>Result of probing a remote server with a TCP <see cref="ServerDiscoveryRequest"/>.</summary>
internal readonly record struct ServerProbeResult(
    bool Reachable,
    string ServerName,
    int PlayerCount,
    int PingMs
);

/// <summary>
/// Probes remote servers over TCP using <see cref="GameServerApiClient.DiscoverAsync"/> to measure
/// round-trip ping and fetch live server metadata (name and player count).
/// </summary>
internal sealed class ServerPingService
{
    private readonly NetworkingConfig _config;
    private readonly RpcRegistry _emptyRegistry;

    /// <summary>Creates the TCP discovery probe service.</summary>
    public ServerPingService(in NetworkingConfig config)
    {
        _config = config;
        _emptyRegistry = new RpcRegistry();
        _emptyRegistry.Freeze();
    }

    /// <summary>Connects to <paramref name="host"/>:<paramref name="port"/>, sends a discovery request, measures RTT, and closes.</summary>
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

            //  NsdRpc.Tcp starts the socket receive loop before AcceptAsync hands the connection
            //  to RpcServer, so a frame sent immediately after ConnectAsync can be dropped on fast
            //  local links. Retry once on an early timeout with a fresh RTT measurement.
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
                if (!response.IsSuccess || response.Value.ProtocolVersion != LanDiscovery.PROTOCOL_VERSION)
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
