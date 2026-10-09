namespace WaywardBeyond.Client.Networking;

public sealed class SnapshotAckTracker
{
    public uint LastAppliedSnapshotTick { get; set; }
}
