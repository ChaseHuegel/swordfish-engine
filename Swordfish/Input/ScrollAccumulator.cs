namespace Swordfish.Input;

/// <summary>Accumulates wheel deltas between frames and latches them once per frame.</summary>
internal sealed class ScrollAccumulator
{
    private readonly object _lock = new();
    private float _pending;
    private float _latched;

    /// <summary>Gets the delta latched for the current frame.</summary>
    public float Value
    {
        get
        {
            lock (_lock)
            {
                return _latched;
            }
        }
    }

    /// <summary>Adds one wheel delta to the pending total.</summary>
    public void Add(float delta)
    {
        lock (_lock)
        {
            _pending += delta;
        }
    }

    /// <summary>Latches the pending total and clears it for the next frame.</summary>
    public void Latch()
    {
        lock (_lock)
        {
            _latched = _pending;
            _pending = 0f;
        }
    }
}
