using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using WaywardBeyond.Shared.Networking.Sessions;

namespace WaywardBeyond.Shared.Networking.Transport;

public class TCPFrameServer(ILogger logger, SessionService sessionService) : FrameStreamService(sessionService)
{
    private readonly ILogger _logger = logger;

    public void Start(IPEndPoint endPoint)
    {
        Task.Run(() => ListenForConnectionsAsync(endPoint));
    }

    private async Task ListenForConnectionsAsync(IPEndPoint endPoint)
    {
        TcpListener tcpServer = new(endPoint.Address, endPoint.Port);
        tcpServer.Start();
        while (true)
        {
            try
            {
                TcpClient client = await tcpServer.AcceptTcpClientAsync();
                NetworkStream tcpStream = client.GetStream();
                var frameStream = new FrameStream(tcpStream);
                AcceptPeer(frameStream);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting a client.");
            }
        }
    }
}
