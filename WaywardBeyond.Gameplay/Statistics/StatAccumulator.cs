namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// Zero-allocation running accumulator for one statistic evaluation:
/// <c>(base + sum(AddBase)) * (1 + sum(AddPercent)) * product(Multiply)</c>.
/// </summary>
public struct StatAccumulator(float defaultBase)
{
    /// <summary>The caller-supplied fallback base value used when no <see cref="StatModifierOperation.SetBase"/> applies.</summary>
    public float DefaultBase = float.IsFinite(defaultBase) ? defaultBase : 0f;

    /// <summary>Whether a <see cref="StatModifierOperation.SetBase"/> modifier has overridden <see cref="DefaultBase"/>.</summary>
    public bool HasBaseOverride = false;

    /// <summary>The overridden base value when <see cref="HasBaseOverride"/> is true.</summary>
    public float BaseOverride = 0f;

    /// <summary>Sum of all <see cref="StatModifierOperation.AddBase"/> modifiers.</summary>
    public float BaseAddition = 0f;

    /// <summary>Sum of all <see cref="StatModifierOperation.AddPercent"/> modifiers.</summary>
    public float PercentAddition = 0f;

    /// <summary>Product of all <see cref="StatModifierOperation.Multiply"/> modifiers.</summary>
    public float Multiplier = 1f;

    /// <summary>Applies <paramref name="modifier"/> when its value is finite.</summary>
    public void Apply(in StatModifier modifier)
    {
        if (!float.IsFinite(modifier.Value))
        {
            return;
        }

        switch (modifier.Operation)
        {
            case StatModifierOperation.SetBase:
                HasBaseOverride = true;
                BaseOverride = modifier.Value;
                break;

            case StatModifierOperation.AddBase:
                BaseAddition += modifier.Value;
                break;

            case StatModifierOperation.AddPercent:
                PercentAddition += modifier.Value;
                break;

            case StatModifierOperation.Multiply:
                Multiplier *= modifier.Value;
                break;
        }
    }

    /// <summary>Computes the final effective value, falling back to <see cref="DefaultBase"/> if non-finite.</summary>
    public readonly float Resolve()
    {
        float effectiveBase = (HasBaseOverride ? BaseOverride : DefaultBase) + BaseAddition;
        float percentScale = 1f + PercentAddition;
        float result = effectiveBase * percentScale * Multiplier;
        return float.IsFinite(result) ? result : DefaultBase;
    }
}
