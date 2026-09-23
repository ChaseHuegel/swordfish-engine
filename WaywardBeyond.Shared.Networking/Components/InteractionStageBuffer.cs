namespace WaywardBeyond.Shared.Networking.Components;

/// <summary>
/// Server-side per-entity ring buffer of inbound interaction edges, keyed by the target sim tick each
/// <see cref="InteractionEvent.ServerTickAtSample"/> carries. Mirrors <see cref="InputStageBuffer"/>: a
/// retransmitted packet carrying the same <see cref="InteractionEvent.SequenceNumber"/> overwrites its
/// slot instead of being staged twice, but every distinct interaction edge is kept. Unlike continuous
/// input heads, a click is a discrete edit - collapsing same-tick events to the newest would silently
/// drop valid placements that share a snapshot tick.
/// </summary>
public sealed class InteractionStageBuffer
{
    private const int CAPACITY = 256;

    private readonly InteractionEvent[] _entries = new InteractionEvent[CAPACITY];
    private uint _tail;
    private uint _head;

    public void Stage(in InteractionEvent interaction)
    {
        for (uint i = _head; i > _tail; i--)
        {
            uint index = (i - 1) % CAPACITY;
            InteractionEvent existing = _entries[index];

            //  A retransmitted packet carries the same sequence; overwrite in place, never double-count.
            if (existing.SequenceNumber == interaction.SequenceNumber)
            {
                _entries[index] = interaction;
                return;
            }
        }

        _entries[_head % CAPACITY] = interaction;
        _head++;

        if (_head - _tail > CAPACITY)
        {
            _tail = _head - CAPACITY;
        }
    }

    /// <summary>
    /// Returns the newest staged interaction whose target sim tick is at or below the given sim tick, so
    /// an edge tagged slightly in the past (network late) is still consumed rather than skipped.
    /// </summary>
    public bool TryGet(uint simTick, out InteractionEvent interaction)
    {
        for (uint i = _head; i > _tail; i--)
        {
            uint index = (i - 1) % CAPACITY;
            if (_entries[index].ServerTickAtSample <= simTick)
            {
                interaction = _entries[index];
                return true;
            }
        }

        interaction = default;
        return false;
    }

    /// <summary>
    /// Drains the oldest staged interaction whose target sim tick is at or below <paramref name="simTick"/>
    /// and whose sequence is newer than <paramref name="lastSequenceNumber"/> - an edge the caller has not
    /// consumed yet. Unlike <see cref="TryGet"/>, distinct events for different ticks are each returned once
    /// (in tick order), so discrete interaction edges are never skipped by newer arrivals. The caller tracks
    /// the last consumed sequence per entity and calls this in a loop until it returns false.
    /// </summary>
    public bool TryConsume(uint simTick, uint lastSequenceNumber, out InteractionEvent interaction)
    {
        uint? bestIndex = null;
        uint bestTick = uint.MaxValue;
        uint bestSequence = uint.MaxValue;

        for (uint i = _head; i > _tail; i--)
        {
            uint index = (i - 1) % CAPACITY;
            InteractionEvent entry = _entries[index];
            if (entry.ServerTickAtSample > simTick)
            {
                continue;
            }

            if (entry.SequenceNumber <= lastSequenceNumber)
            {
                continue;
            }

            //  Prefer the earliest unconsumed event so overlapping edges preserve their order.
            if (entry.ServerTickAtSample < bestTick ||
                (entry.ServerTickAtSample == bestTick && entry.SequenceNumber < bestSequence))
            {
                bestIndex = index;
                bestTick = entry.ServerTickAtSample;
                bestSequence = entry.SequenceNumber;
            }
        }

        if (bestIndex == null)
        {
            interaction = default;
            return false;
        }

        interaction = _entries[bestIndex.Value];
        return true;
    }

    /// <summary>Returns a copy of every staged interaction in staged order, oldest first.</summary>
    public InteractionEvent[] Snapshot()
    {
        int count = (int)(_head - _tail);
        if (count <= 0)
        {
            return [];
        }

        var result = new InteractionEvent[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = _entries[(_tail + i) % CAPACITY];
        }

        return result;
    }

    /// <summary>Discards every staged interaction.</summary>
    public void Clear()
    {
        _head = 0;
        _tail = 0;
    }
}