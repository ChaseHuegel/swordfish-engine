using Needlefish;

namespace Swordfish.Networking;

public class PacketHeader : IDataBody
{
    [DataField(0)]
    public int SessionID;

    [DataField(1)]
    public int PacketID;

    [DataField(2)]
    public uint Sequence;
}
