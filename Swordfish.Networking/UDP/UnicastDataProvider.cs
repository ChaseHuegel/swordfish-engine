using System.Net;
using System.Net.Sockets;

namespace Swordfish.Networking.UDP;

public class UnicastDataProvider : IDataReader<DataEventArgs>, IDataWriter<IPEndPoint>
{
    protected UdpClient _udpClient;

    public event EventHandler<DataEventArgs>? Received;

    public UnicastDataProvider(AddressFamily addressFamily = AddressFamily.InterNetwork)
    {
        _udpClient = new UdpClient(addressFamily);
    }

    public UnicastDataProvider(int port, AddressFamily addressFamily = AddressFamily.InterNetwork)
    {
        _udpClient = new UdpClient(port, addressFamily);
    }

    public virtual void Start()
    {
        _udpClient.BeginReceive(OnReceived, null);
    }

    public void Dispose()
    {
        Received = null;
        _udpClient.Dispose();
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    public virtual void Send(ArraySegment<byte> buffer, IPEndPoint endPoint)
    {
        _udpClient.Send(buffer.Array, buffer.Count, endPoint);
    }

    public virtual async Task SendAsync(ArraySegment<byte> buffer, IPEndPoint endPoint)
    {
        await _udpClient.SendAsync(buffer.Array, buffer.Count, endPoint);
    }

    protected virtual void Dispose(bool disposing)
    {
        //  For inheritors to override.
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
            SafeInvokeReceived(new ArraySegment<byte>(buffer));
    }

    private void SafeInvokeReceived(ArraySegment<byte> data)
    {
        try {
            Received?.Invoke(this, new DataEventArgs(data));
        } catch {
            //  Swallow exceptions thrown by listeners.
        }
    }
}