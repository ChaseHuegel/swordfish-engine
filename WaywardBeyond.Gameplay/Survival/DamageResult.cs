using WaywardBeyond.Data;

namespace WaywardBeyond.Gameplay.Survival;

/// <summary>The outcome of one <see cref="DamageService"/> damage application.</summary>
public readonly struct DamageResult
{
    /// <summary>Creates a damage result.</summary>
    public DamageResult(float applied, bool fatal, DamageSource source)
    {
        Applied = applied;
        Fatal = fatal;
        Source = source;
    }

    /// <summary>The health actually removed.</summary>
    public readonly float Applied;

    /// <summary>Whether this damage reduced health to zero.</summary>
    public readonly bool Fatal;

    /// <summary>The damage source that was applied.</summary>
    public readonly DamageSource Source;
}
