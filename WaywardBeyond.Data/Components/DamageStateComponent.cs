using Krill;

namespace WaywardBeyond.Data.Components;

/// <summary>Server-side record of the most recent damage event and the fatal source, if any.</summary>
public partial struct DamageStateComponent : IDataComponent
{
    /// <summary>The source of the most recent damage event.</summary>
    public DamageSource LastSource;

    /// <summary>The health actually removed by the most recent damage event.</summary>
    public float LastAmount;

    /// <summary>The sim tick of the most recent damage event.</summary>
    public long LastTick;

    /// <summary>The source that reduced health to zero, or <see cref="DamageSource.None"/> while alive.</summary>
    public DamageSource DeathCause;

    /// <summary>The sim tick health reached zero, or zero while alive.</summary>
    public long DeathTick;
}
