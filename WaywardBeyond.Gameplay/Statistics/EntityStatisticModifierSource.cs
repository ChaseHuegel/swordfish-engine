using System;
using WaywardBeyond.Data.Components;
using WaywardBeyond.Data.Statistics;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// Normal-priority modifier source that reads numeric statistics from an entity's
/// <see cref="StatisticsComponent"/> (unscaled for integer attributes/flags, fixed-point otherwise,
/// skipping buff timer keys) and adds them to the base total.
/// </summary>
public sealed class EntityStatisticModifierSource : IStatModifierSource
{
    /// <inheritdoc/>
    public StatModifierPriority Priority => StatModifierPriority.Normal;

    /// <inheritdoc/>
    public string? StatId => null;

    /// <inheritdoc/>
    public void Apply(in StatQuery query, ref StatAccumulator accumulator)
    {
        if (query.Store == null || query.Entity < 0 || string.IsNullOrEmpty(query.StatId))
        {
            return;
        }

        ReadOnlySpan<char> statId = query.StatId.AsSpan();
        if (StatisticIds.IsBuffTimerKey(statId)
            || StatisticIds.IsCooldownKey(statId)
            || StatisticIds.IsDeathStat(statId))
        {
            return;
        }

        if (!query.Store.TryGet(query.Entity, out StatisticsComponent stats))
        {
            return;
        }

        float entityValue;
        if (StatisticIds.IsIntegerStat(statId))
        {
            if (!stats.TryGetValue(statId, out long rawValue))
            {
                return;
            }

            entityValue = rawValue;
        }
        else if (!stats.TryGetFixed(query.StatId, out entityValue))
        {
            return;
        }

        if (float.IsFinite(entityValue))
        {
            accumulator.Apply(StatModifier.AddBase(entityValue));
        }
    }
}
