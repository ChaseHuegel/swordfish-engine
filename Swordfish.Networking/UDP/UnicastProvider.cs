using System.Net;
using System.Net.Sockets;
using Swordfish.Networking.Messaging;

namespace Swordfish.Networking.UDP;

public class UnicastProvider : IReceiver<ArraySegment<byte>>, IWriter<ArraySegment<byte>, IPEndPoint>
{
    private volatile bool _startedListening;
    private MessageQueue<ArraySegment<byte>> _packetQueue;
    protected UdpClient _udpClient;

    public event EventHandler<ArraySegment<byte>>? Received;

    private UnicastProvider()
    {
        _packetQueue = new MessageQueue<ArraySegment<byte>>();
        _packetQueue.NewMessage += OnNewMessage;
        _udpClient = null!;
    }

    public UnicastProvider(AddressFamily addressFamily = AddressFamily.InterNetwork) : this()
    {
        _udpClient = new UdpClient(0, addressFamily);
    }

    public UnicastProvider(int port, AddressFamily addressFamily = AddressFamily.InterNetwork) : this()
    {
        _udpClient = new UdpClient(port, addressFamily);
    }

    public virtual void BeginListening()
    {
        if (_startedListening)
            return;

        _startedListening = true;
        _packetQueue.Start();
        _udpClient.BeginReceive(OnReceived, null);
    }

    public void Dispose()
    {
        Received = null;
        _udpClient.Dispose();
        _packetQueue.Dispose();
        GC.SuppressFinalize(this);
    }

    public void Send(ArraySegment<byte> buffer, IPEndPoint endPoint)
    {
        _udpClient.Send(buffer.Array, buffer.Count, endPoint);
    }

    public async Task SendAsync(ArraySegment<byte> buffer, IPEndPoint endPoint)
    {
        await _udpClient.SendAsync(buffer.Array, buffer.Count, endPoint);
    }

    private void OnReceived(IAsyncResult result)
    {
        IPEndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);

        byte[]? buffer = null;
        try
        {
            buffer = _udpClient.EndReceive(result, ref endPoint!);
        }
        catch (ObjectDisposedException)
        {
            //  The UDP client has been disposed, safely stop listening.
            return;
        }
        catch
        {
            //  If there was some other issue just move on and keep trying to listen.
        }

        _udpClient.BeginReceive(OnReceived, null);

        if (buffer != null)
            _packetQueue.Post(buffer);
    }

    private void OnNewMessage(object sender, ArraySegment<byte> e)
    {
        SafeInvokeReceived(e);
    }

    private void SafeInvokeReceived(ArraySegment<byte> data)
    {
        try {
            Received?.Invoke(this, data);
        } catch {
            //  Swallow exceptions thrown by listeners.
        }
    }
}