using Krill;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Gameplay.Networking;

namespace WaywardBeyond.Client.Components;

/// <summary>
/// Client-side outbound buffer for discrete, sequence-numbered <see cref="StationActionOp"/> commands
/// awaiting upstream replication. Commands are never client-predicted; the server remains the sole authority.
/// </summary>
public struct PendingStationActionComponent() : IDataComponent
{
    private const int CAPACITY = 64;

    /// <summary>Station commands awaiting replication to the server.</summary>
    public readonly StageRing<StagedStationAction> Outbound = new(CAPACITY);

    /// <summary>Monotonic command sequence counter stamped onto <see cref="StationActionEvent.SequenceNumber"/>.</summary>
    public uint NextSequence = 0;
}
