using System;
using WaywardBeyond.Data;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// Normal-priority modifier source that reads custom numeric statistics from an entity's
/// <see cref="StatisticsComponent"/> (or character save statistics array) and adds them to the base total.
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
        if (query.Store != null &&
            query.Entity >= 0 &&
            query.Store.TryGet(query.Entity, out StatisticsComponent stats) &&
            stats.TryGet(query.StatId, out float entityValue) &&
            float.IsFinite(entityValue))
        {
            accumulator.Apply(StatModifier.AddBase(entityValue));
            return;
        }

        Statistic[]? characterStatistics = query.CharacterStatistics;
        if (characterStatistics == null)
        {
            return;
        }

        for (var i = 0; i < characterStatistics.Length; i++)
        {
            if (string.Equals(characterStatistics[i].ID, query.StatId, StringComparison.Ordinal))
            {
                accumulator.Apply(StatModifier.AddBase(characterStatistics[i].Value));
                return;
            }
        }
    }
}
