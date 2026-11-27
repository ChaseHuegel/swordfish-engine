namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>One numeric modification applied during statistic evaluation.</summary>
public readonly struct StatModifier(StatModifierOperation operation, float value)
{
    /// <summary>The combining operation.</summary>
    public readonly StatModifierOperation Operation = operation;

    /// <summary>The modifier magnitude.</summary>
    public readonly float Value = value;

    /// <summary>Creates a modifier that sets the base value.</summary>
    public static StatModifier SetBase(float value) => new(StatModifierOperation.SetBase, value);

    /// <summary>Creates a modifier that adds a flat offset to the base value.</summary>
    public static StatModifier AddBase(float value) => new(StatModifierOperation.AddBase, value);

    /// <summary>Creates a modifier that adds to the summed percentage multiplier.</summary>
    public static StatModifier AddPercent(float value) => new(StatModifierOperation.AddPercent, value);

    /// <summary>Creates a modifier that multiplies the post-percentage total.</summary>
    public static StatModifier Multiply(float value) => new(StatModifierOperation.Multiply, value);
}
