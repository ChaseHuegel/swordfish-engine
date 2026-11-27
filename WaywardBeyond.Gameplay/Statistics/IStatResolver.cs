using System.Collections.Generic;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// Evaluates effective statistic values by running every registered <see cref="IStatModifierSource"/>
/// in priority order.
/// </summary>
public interface IStatResolver
{
    /// <summary>Computes the effective value of the statistic described by <paramref name="query"/>.</summary>
    float Evaluate(in StatQuery query);

    /// <summary>Returns all registered modifier sources in execution order.</summary>
    IReadOnlyList<IStatModifierSource> GetSources();
}
