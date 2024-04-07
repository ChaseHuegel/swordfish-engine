namespace Swordfish.Networking;

public struct PacketReceivedArgs<TSource>
{
    public TSource Source;
    public Packet Packet;

    public PacketReceivedArgs(TSource source, Packet packet)
    {
        Source = source;
        Packet = packet;
    }
}