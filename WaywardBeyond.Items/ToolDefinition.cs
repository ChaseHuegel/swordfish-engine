namespace WaywardBeyond.Items;

/// <summary>Defines tool behavior attached to an item.</summary>
public struct ToolDefinition()
{
    /// <summary>The default mining speed multiplier applied when omitted or invalid.</summary>
    public const float DEFAULT_MINING_SPEED = 1.0f;

    /// <summary>The tool behavior kind.</summary>
    public ToolType Type;

    /// <summary>The optional target pattern or mode string for the tool.</summary>
    public string Target = string.Empty;

    /// <summary>The optional mining speed multiplier for mining tools. Defaults to 1.0.</summary>
    public float? MiningSpeed;

    /// <summary>Returns a finite positive mining speed multiplier, falling back to <see cref="DEFAULT_MINING_SPEED"/>.</summary>
    public readonly float ResolveMiningSpeed()
    {
        return MiningSpeed.HasValue && float.IsFinite(MiningSpeed.Value) && MiningSpeed.Value > 0f
            ? MiningSpeed.Value
            : DEFAULT_MINING_SPEED;
    }
}
