using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Networking;

/// <summary>One sequenced inventory transfer or legacy move awaiting replication or application.</summary>
public readonly struct InventoryOp
{
    /// <summary>The client sequence number used to dedupe and order ops.</summary>
    public readonly uint SequenceNumber;

    /// <summary>The unified keyed transfer payload when present.</summary>
    public readonly TransferOp? Transfer;

    /// <summary>The legacy positional move payload when present.</summary>
    public readonly SlotMoveOp? SlotMove;

    /// <summary>Constructs an inventory op carrying a unified transfer.</summary>
    public InventoryOp(uint sequenceNumber, in TransferOp transfer)
    {
        SequenceNumber = sequenceNumber;
        Transfer = transfer;
        SlotMove = null;
    }

    /// <summary>Constructs an inventory op carrying a legacy positional move.</summary>
    public InventoryOp(uint sequenceNumber, in SlotMoveOp slotMove)
    {
        SequenceNumber = sequenceNumber;
        Transfer = null;
        SlotMove = slotMove;
    }
}
