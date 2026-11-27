namespace WaywardBeyond.Data.Statistics;

/// <summary>Well-known statistic identifiers shared across items, characters, and stat modifiers.</summary>
public static class StatisticIds
{
    /// <summary>Mining speed multiplier applied when breaking bricks (base 1.0).</summary>
    public const string MINING_SPEED = "wb:mining_speed";

    /// <summary>Default base value for <see cref="MINING_SPEED"/> when no item or modifier overrides it.</summary>
    public const float DEFAULT_MINING_SPEED = 1.0f;
}
