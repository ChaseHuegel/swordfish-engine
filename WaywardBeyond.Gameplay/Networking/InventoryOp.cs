using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Networking;

/// <summary>One sequenced inventory transfer awaiting replication or application.</summary>
public readonly struct InventoryOp(uint sequenceNumber, in TransferOp transfer)
{
    /// <summary>The client sequence number used to dedupe and order ops.</summary>
    public readonly uint SequenceNumber = sequenceNumber;

    /// <summary>The keyed transfer payload.</summary>
    public readonly TransferOp Transfer = transfer;
}
