using System.Collections.Concurrent;
using System.Numerics;

namespace Swordfish.Physics.Jolt;

/// <summary>Buffers physics contact events written across solver worker threads for lock-free single-thread drain.</summary>
internal sealed class JoltContactBuffer
{
    private const int INITIAL_CAPACITY = 256;

    private PhysicsContactEvent[] _activeBuffer = new PhysicsContactEvent[INITIAL_CAPACITY];
    private PhysicsContactEvent[] _dispatchBuffer = new PhysicsContactEvent[INITIAL_CAPACITY];
    private readonly ConcurrentQueue<PhysicsContactEvent> _overflow = new();
    private int _writeIndex;

    /// <summary>Appends a contact event from any Jolt worker thread without blocking.</summary>
    public void Add(in PhysicsContactEvent contactEvent)
    {
        PhysicsContactEvent[] buffer = Volatile.Read(ref _activeBuffer);
        int slot = Interlocked.Increment(ref _writeIndex) - 1;
        if ((uint)slot < (uint)buffer.Length)
        {
            buffer[slot] = contactEvent;
            return;
        }

        _overflow.Enqueue(contactEvent);
    }

    /// <summary>Drains all buffered contact events on the tick thread and invokes <paramref name="handler"/> outside any lock.</summary>
    public void Drain(Action<PhysicsContactEvent>? handler)
    {
        int written = Interlocked.Exchange(ref _writeIndex, 0);
        if (written == 0 && _overflow.IsEmpty)
        {
            return;
        }

        PhysicsContactEvent[] current = _activeBuffer;
        int directCount = written < current.Length ? written : current.Length;

        //  Swap buffers before dispatching so re-entrant writes never touch the array being read.
        _activeBuffer = _dispatchBuffer;
        _dispatchBuffer = current;

        if (written > _activeBuffer.Length)
        {
            uint targetCapacity = BitOperations.RoundUpToPowerOf2((uint)Math.Max(written, INITIAL_CAPACITY));
            int nextCapacity = (int)Math.Min(targetCapacity, (uint)Array.MaxLength);
            _activeBuffer = new PhysicsContactEvent[nextCapacity];
        }

        try
        {
            if (handler != null)
            {
                for (var i = 0; i < directCount; i++)
                {
                    handler(current[i]);
                }

                while (_overflow.TryDequeue(out PhysicsContactEvent overflowEvent))
                {
                    handler(overflowEvent);
                }
            }
        }
        finally
        {
            while (_overflow.TryDequeue(out _))
            {
            }

            if (_dispatchBuffer.Length < _activeBuffer.Length)
            {
                _dispatchBuffer = new PhysicsContactEvent[_activeBuffer.Length];
            }
        }
    }
}
