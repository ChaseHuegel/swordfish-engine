namespace WaywardBeyond.Client.Core.Networking;

public sealed class SnapshotAckTracker
{
    public uint LastAppliedSnapshotTick { get; set; }
}
