namespace Swordfish.Networking;

public class PacketStream
{
    private byte _base;
    private Packet?[] _buffer = new Packet?[256];
    private AutoResetEvent ReadWaitHandle = new(false);

    public void Write(Packet packet)
    {
        if (Math.Abs(packet.Sequence - _base) > 20)
            new ManualResetEvent(false).WaitOne(1);

        _buffer[packet.Sequence] = packet;
        ReadWaitHandle.Set();
    }

    public Packet Read()
    {
        while (!HasData())
            ReadWaitHandle.WaitOne();

        Packet packet = _buffer[_base]!;
        _buffer[_base] = null;
        _base++;
        return packet;
    }

    public bool HasData() => _buffer[_base] != null;
}