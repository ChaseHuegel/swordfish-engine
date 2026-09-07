namespace WaywardBeyond.Shared.Networking.Components;

/// <summary>
/// Server-side per-entity ring buffer of inbound commands, keyed by the target sim tick each
/// <see cref="InputComponent.ServerTickAtSample"/> carries. Multiple samples for the same sim tick
/// collapse to the newest; the shared step consumes exactly one command per sim tick.
/// </summary>
public sealed class InputStageBuffer
{
    private const int CAPACITY = 256;

    private readonly InputComponent[] _entries = new InputComponent[CAPACITY];
    private readonly uint[] _ticks = new uint[CAPACITY];
    private uint _tail;
    private uint _head;

    public void Stage(in InputComponent input)
    {
        _entries[_head % CAPACITY] = input;
        _ticks[_head % CAPACITY] = input.ServerTickAtSample;
        _head++;

        if (_head - _tail > CAPACITY)
        {
            _tail = _head - CAPACITY;
        }
    }

    /// <summary>
    /// Returns the newest staged command whose target sim tick is at or below the given sim tick, so a
    /// command tagged slightly in the past (network late) is still consumed rather than skipped.
    /// </summary>
    public bool TryGet(uint simTick, out InputComponent command)
    {
        for (uint i = _head; i > _tail; i--)
        {
            uint index = (i - 1) % CAPACITY;
            if (_ticks[index] <= simTick)
            {
                command = _entries[index];
                return true;
            }
        }

        command = default;
        return false;
    }
}