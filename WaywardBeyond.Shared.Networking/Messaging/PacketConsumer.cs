using WaywardBeyond.Shared.Networking;
using WaywardBeyond.Shared.Networking.Events;
using WaywardBeyond.Shared.Networking.Serialization;
using WaywardBeyond.Shared.Networking.Transport;

namespace WaywardBeyond.Shared.Networking.Messaging;

public class PacketConsumer<T>(
    IPacketSerializer<T> serializer,
    IDataProducer[] dataProducers
) : MessageConsumer<T>(serializer, dataProducers)
{
    private readonly IPacketSerializer<T> _packetSerializer = serializer;

    public PacketAwaiter<T> GetPacketAwaiter()
    {
        return new PacketAwaiter<T>(this);
    }

    protected override void OnDataReceived(object? sender, DataEventArgs e)
    {
        GamePacket gamePacket = GamePacket.Deserialize(e.Data, 0, e.Data.Length);
        if (gamePacket.Type != _packetSerializer.PacketType)
        {
            return;
        }
        base.OnDataReceived(sender, new DataEventArgs(gamePacket.Payload, e.Sender));
    }
}
