namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>How a <see cref="StatModifier"/> combines with a statistic's running value.</summary>
public enum StatModifierOperation : byte
{
    /// <summary>Replaces the fallback base value before additive and multiplicative modifiers apply.</summary>
    SetBase = 0,

    /// <summary>Adds a flat offset to the base value before percentage scaling.</summary>
    AddBase = 1,

    /// <summary>Adds to the summed percentage multiplier applied to the base total (for example, 0.02 for +2%).</summary>
    AddPercent = 2,

    /// <summary>Multiplies the post-percentage total independently.</summary>
    Multiply = 3,
}
