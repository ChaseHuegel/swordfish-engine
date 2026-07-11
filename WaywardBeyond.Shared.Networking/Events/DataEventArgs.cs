using WaywardBeyond.Shared.Networking.Sessions;

namespace WaywardBeyond.Shared.Networking.Events;

public readonly struct DataEventArgs(byte[] bytes, Session sender)
{
    public readonly byte[] Data = bytes;
    public readonly Session Sender = sender;
}
