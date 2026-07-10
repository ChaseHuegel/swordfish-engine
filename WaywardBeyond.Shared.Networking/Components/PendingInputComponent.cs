using Swordfish.ECS;

namespace WaywardBeyond.Shared.Networking.Components;

public struct PendingInputComponent : IDataComponent
{
    private const int BUFFER_CAPACITY = 256;

    private InputComponent[] _history;
    private uint _head;
    private uint _tail;

    public PendingInputComponent(int capacity = BUFFER_CAPACITY)
    {
        _history = new InputComponent[capacity];
        _head = 0;
        _tail = 0;
    }

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

    public void AckUpTo(uint sequenceNumber)
    {
        while (_tail < _head)
        {
            if (_history == null)
            {
                break;
            }
            
            uint index = _tail % (uint)_history.Length;
            if (_history[index].SequenceNumber > sequenceNumber)
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
}
