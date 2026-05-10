using System.Runtime.InteropServices;
using Torches.Networking.Models;
using Torches.Networking.Models.Entity;

namespace WaywardBeyond.Server.Core.Networking;

[StructLayout(LayoutKind.Explicit)]
public readonly struct Packet
{
    [FieldOffset(0)]
    public readonly PacketType Type;
    
    [FieldOffset(4)]
    public readonly EntityUpdatePacket EntityUpdate;

    public Packet(EntityUpdatePacket entityUpdatePacket)
    {
        Type = PacketType.EntityUpdate;
        EntityUpdate = entityUpdatePacket;
    }
}