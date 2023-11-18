namespace Swordfish.Networking;

public class Packet
{
    public readonly byte Sequence;
    public readonly ushort ID;
    public readonly ArraySegment<byte> Payload;

    public Packet()
    {

    }

    public Packet(byte sequence, ushort id, ArraySegment<byte> payload)
    {
        Sequence = sequence;
        ID = id;
        Payload = payload;
    }
}