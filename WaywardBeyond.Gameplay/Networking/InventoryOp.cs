using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Networking;

/// <summary>One sequenced inventory move awaiting replication or application.</summary>
public readonly struct InventoryOp(uint sequenceNumber, in SlotMoveOp slotMove)
{
    /// <summary>The client sequence number used to dedupe and order ops.</summary>
    public readonly uint SequenceNumber = sequenceNumber;

    /// <summary>The move payload.</summary>
    public readonly SlotMoveOp SlotMove = slotMove;
}
