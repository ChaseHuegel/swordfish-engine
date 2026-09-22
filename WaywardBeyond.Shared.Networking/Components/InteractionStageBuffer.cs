namespace WaywardBeyond.Shared.Networking.Components;

/// <summary>
/// Server-side per-entity ring buffer of inbound interaction edges, keyed by the target sim tick each
/// <see cref="InteractionEvent.ServerTickAtSample"/> carries. Mirrors <see cref="InputStageBuffer"/> with
/// two additions: events targeting the same sim tick collapse newest-per-tick (highest sequence wins),
/// and a retransmitted packet carrying the same <see cref="InteractionEvent.SequenceNumber"/> overwrites
/// its slot instead of being staged twice.
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

            //  Collapse newest-per-tick: keep the highest-sequence event targeting the same sim tick.
            if (existing.ServerTickAtSample == interaction.ServerTickAtSample)
            {
                if (interaction.SequenceNumber > existing.SequenceNumber)
                {
                    _entries[index] = interaction;
                }
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
}