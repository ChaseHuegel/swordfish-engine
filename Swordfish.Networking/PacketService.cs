using DryIoc.ImTools;
using Needlefish;
using Swordfish.Library.Threading;

namespace Swordfish.Networking;

public class PacketService<TEndPoint> : IDisposable
{
    private readonly IReceiver<DataReceivedArgs<TEndPoint>> _receiver;
    private readonly IWriter<ArraySegment<byte>, TEndPoint> _writer;
    private readonly ITypeSerializer<IPacketDefinition, Packet> _serializer;
    private readonly IFilter<PacketReceivedArgs<TEndPoint>> _filter;

    public event EventHandler<Packet>? Received;

    public PacketService(IReceiver<DataReceivedArgs<TEndPoint>> receiver, IWriter<ArraySegment<byte>, TEndPoint> writer, ITypeSerializer<IPacketDefinition, Packet> serializer, IFilter<PacketReceivedArgs<TEndPoint>> filter)
    {
        _receiver = receiver;
        _writer = writer;
        _serializer = serializer;
        _filter = filter;

        _receiver.Received += OnDataReceived;
    }

    public void Start()
    {
        _receiver.BeginListening();
    }

    public void Dispose()
    {
        _receiver.Received -= OnDataReceived;
        _receiver.Dispose();
        _writer.Dispose();
    }

    public void Send(IPacketDefinition packetDefinition, TEndPoint destination)
    {
        ArraySegment<byte> data = _serializer.Serialize(packetDefinition);
        _writer.Send(data, destination);
    }

    public Task SendAsync(IPacketDefinition packetDefinition, TEndPoint destination)
    {
        ArraySegment<byte> data = _serializer.Serialize(packetDefinition);
        return _writer.SendAsync(data, destination);
    }

    private void OnDataReceived(object sender, DataReceivedArgs<TEndPoint> args)
    {
        Packet packet = _serializer.Deserialize(args.Data);

        if (_filter.Check(new PacketReceivedArgs<TEndPoint>(args.Source, packet)))
            SafeInvokeReceived(packet);
    }

    private void SafeInvokeReceived(Packet packet)
    {
        try
        {
            Received?.Invoke(this, packet);
        }
        catch
        {
            //  Swallow exceptions thrown by listeners.
        }
    }
}