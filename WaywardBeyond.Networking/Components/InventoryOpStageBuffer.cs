using Swordfish.ECS;

namespace WaywardBeyond.Networking.Components;

/// <summary>
/// Client-side per-entity ring of outbound inventory operations awaiting replication, mirroring
/// <see cref="InteractionStageBuffer"/>: each op carries a monotonic <see cref="InventoryEvent.SequenceNumber"/>,
/// a retransmitted copy of the same sequence overwrites its slot (idempotent re-send), and every distinct
/// op is kept so rapid moves between sends are not dropped.
/// </summary>
public sealed class InventoryOpStageBuffer
{
    private const int CAPACITY = 64;

    private readonly InventoryOp[] _entries = new InventoryOp[CAPACITY];
    private uint _tail;
    private uint _head;

    public void Stage(uint sequenceNumber, in SlotMoveOp op)
    {
        for (uint i = _head; i > _tail; i--)
        {
            uint index = (i - 1) % CAPACITY;
            if (_entries[index].SequenceNumber == sequenceNumber)
            {
                _entries[index] = new InventoryOp(sequenceNumber, op);
                return;
            }
        }

        _entries[_head % CAPACITY] = new InventoryOp(sequenceNumber, op);
        _head++;

        if (_head - _tail > CAPACITY)
        {
            _tail = _head - CAPACITY;
        }
    }

    /// <summary>
    /// Returns the oldest staged op whose sequence is newer than <paramref name="lastSequenceNumber"/> -
    /// an op the server has not consumed yet. The caller tracks the last consumed sequence per entity and
    /// calls this in a loop until it returns false.
    /// </summary>
    public bool TryConsume(uint lastSequenceNumber, out InventoryOp op)
    {
        uint? bestIndex = null;
        uint bestSequence = uint.MaxValue;

        for (uint i = _head; i > _tail; i--)
        {
            uint index = (i - 1) % CAPACITY;
            uint sequence = _entries[index].SequenceNumber;
            if (sequence <= lastSequenceNumber)
            {
                continue;
            }

            if (sequence < bestSequence)
            {
                bestIndex = index;
                bestSequence = sequence;
            }
        }

        if (bestIndex == null)
        {
            op = default;
            return false;
        }

        op = _entries[bestIndex.Value];
        return true;
    }

    /// <summary>Returns a copy of every staged op in staged order, oldest first.</summary>
    public InventoryOp[] Snapshot()
    {
        int count = (int)(_head - _tail);
        if (count <= 0)
        {
            return [];
        }

        var result = new InventoryOp[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = _entries[(_tail + i) % CAPACITY];
        }

        return result;
    }

    /// <summary>Discards every staged op.</summary>
    public void Clear()
    {
        _head = 0;
        _tail = 0;
    }

    public readonly struct InventoryOp
    {
        public readonly uint SequenceNumber;
        public readonly SlotMoveOp SlotMove;

        public InventoryOp(uint sequenceNumber, in SlotMoveOp slotMove)
        {
            SequenceNumber = sequenceNumber;
            SlotMove = slotMove;
        }
    }
}