using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Networking;

/// <summary>One sequenced station action operation awaiting replication or server execution.</summary>
public readonly struct StagedStationAction(uint sequenceNumber, in StationActionOp op)
{
    /// <summary>The client sequence number used to dedupe and order commands.</summary>
    public readonly uint SequenceNumber = sequenceNumber;

    /// <summary>The station command payload.</summary>
    public readonly StationActionOp Op = op;
}
