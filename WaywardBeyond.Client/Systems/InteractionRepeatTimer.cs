namespace WaywardBeyond.Client.Systems;

/// <summary>Times hold-to-repeat for one interaction button.</summary>
internal sealed class InteractionRepeatTimer(float interval)
{
    private readonly float _interval = interval;

    private bool _active;
    private float _elapsed;

    /// <summary>Starts the interval from a fresh press.</summary>
    public void Press()
    {
        _active = true;
        _elapsed = 0f;
    }

    /// <summary>Advances the interval and returns true when the interaction repeats. An ineligible tick ends the repeat.</summary>
    public bool Advance(float delta, bool eligible)
    {
        if (!_active)
        {
            return false;
        }

        if (!eligible)
        {
            Release();
            return false;
        }

        _elapsed += delta;
        if (_elapsed < _interval)
        {
            return false;
        }

        _elapsed -= _interval;
        return true;
    }

    private void Release()
    {
        _active = false;
        _elapsed = 0f;
    }
}
