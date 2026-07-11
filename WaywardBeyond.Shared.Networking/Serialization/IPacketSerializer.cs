using Swordfish.Library.Serialization;
using WaywardBeyond.Shared.Networking;

namespace WaywardBeyond.Shared.Networking.Serialization;

public interface IPacketSerializer<T> : ISerializer<T>
{
    GamePacketType PacketType { get; }
}
