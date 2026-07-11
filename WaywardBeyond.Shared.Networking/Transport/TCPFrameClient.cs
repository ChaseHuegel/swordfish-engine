using System;
using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using WaywardBeyond.Shared.Networking.Sessions;

namespace WaywardBeyond.Shared.Networking.Transport;

public class TCPFrameClient(ILogger logger, SessionService sessionService) : FrameStreamService(sessionService)
{
    private readonly ILogger _logger = logger;

    public void Connect(IPEndPoint endPoint)
    {
        try
        {
            TcpClient tcpClient = new(AddressFamily.InterNetwork);
            tcpClient.Connect(endPoint);
            NetworkStream stream = tcpClient.GetStream();
            var frameStream = new FrameStream(stream);
            AcceptPeer(frameStream);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error connecting to a server at {EndPoint}.", endPoint);
        }
    }
}
