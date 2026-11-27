using WaywardBeyond.Data.Components;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// Normal-priority modifier source that reads fixed-point numeric statistics from an entity's
/// <see cref="StatisticsComponent"/> and adds them to the base total.
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
            stats.TryGetFixed(query.StatId, out float entityValue) &&
            float.IsFinite(entityValue))
        {
            accumulator.Apply(StatModifier.AddBase(entityValue));
        }
    }
}
