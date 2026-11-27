namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// One extensible contributor to statistic evaluation. Both game modules and mods register implementations
/// through DI so new modifier sources participate without changing callers.
/// </summary>
public interface IStatModifierSource
{
    /// <summary>The ordering band in which this source runs.</summary>
    StatModifierPriority Priority { get; }

    /// <summary>Optional statistic id filter. Return null to inspect every statistic query.</summary>
    string? StatId { get; }

    /// <summary>Contributes zero or more modifiers to <paramref name="accumulator"/> for <paramref name="query"/>.</summary>
    void Apply(in StatQuery query, ref StatAccumulator accumulator);
}
