using Swordfish.ECS;

namespace WaywardBeyond.Networking.Components;

/// <summary>
/// The client-side ring of outbound input samples awaiting server acknowledgment, replayed for
/// prediction each sim tick. Mirrors the server's <see cref="InputStageBuffer"/> ordering so the client
/// replays the same command-per-sim-tick sequence the server staged.
/// </summary>
public struct PendingInputComponent(int capacity = PendingInputComponent.BUFFER_CAPACITY) : IDataComponent
{
    private const int BUFFER_CAPACITY = 256;

    private InputComponent[] _history = new InputComponent[capacity];
    private uint _head = 0;
    private uint _tail = 0;

    public void Push(in InputComponent input)
    {
        _history ??= new InputComponent[BUFFER_CAPACITY];
        _history[_head % _history.Length] = input;
        _head++;
        if (_head - _tail > _history.Length)
        {
            _tail = _head - (uint)_history.Length;
        }
    }

    /// <summary>
    /// Trims inputs whose target sim tick (<see cref="InputComponent.ServerTickAtSample"/>) is at or
    /// below the given acked sim tick.
    /// </summary>
    public void AckUpTo(uint simTick)
    {
        while (_tail < _head)
        {
            if (_history == null)
            {
                break;
            }
            
            uint index = _tail % (uint)_history.Length;
            if (_history[index].ServerTickAtSample > simTick)
            {
                break;
            }

            _tail++;
        }
    }

    public readonly int PendingCount => (int)(_head - _tail);

    public readonly InputComponent GetPending(int index)
    {
        return _history?[(_tail + (uint)index) % (uint)_history.Length] ?? default;
    }

    /// <summary>
    /// Replay lookup: returns the newest pending input whose target sim tick
    /// (<see cref="InputComponent.ServerTickAtSample"/>) is at or below the given sim tick, mirroring the
    /// server's <see cref="InputStageBuffer.TryGet"/> exactly (newest-per-sim-tick collapse included), so
    /// the client replays the same command-per-sim-tick sequence the server staged.
    /// </summary>
    public readonly bool TryGetNewestAtOrBefore(uint simTick, out InputComponent command)
    {
        for (var i = (int)(_head - _tail) - 1; i >= 0; i--)
        {
            InputComponent candidate = GetPending(i);
            if (candidate.ServerTickAtSample <= simTick)
            {
                command = candidate;
                return true;
            }
        }

        command = default;
        return false;
    }
}
