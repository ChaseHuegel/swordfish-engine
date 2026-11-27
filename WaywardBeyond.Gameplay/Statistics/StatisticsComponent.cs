using System.Collections.Generic;
using Krill;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// Runtime key-value numeric statistics attached to an entity (such as a character). Parallels
/// <see cref="Items.Item.Statistics"/> so characters, entities, and items can equally define custom statistics.
/// </summary>
public readonly struct StatisticsComponent(Dictionary<string, float> values) : IDataComponent
{
    /// <summary>The underlying statistic values keyed by statistic id, or null on a default instance.</summary>
    public IReadOnlyDictionary<string, float>? Values => values;

    /// <summary>Attempts to read the statistic value for <paramref name="statisticId"/>.</summary>
    public bool TryGet(string statisticId, out float value)
    {
        if (values == null || string.IsNullOrEmpty(statisticId))
        {
            value = 0f;
            return false;
        }

        return values.TryGetValue(statisticId, out value);
    }

    /// <summary>Gets the statistic value for <paramref name="statisticId"/>, or <paramref name="defaultValue"/>.</summary>
    public float Get(string statisticId, float defaultValue = 0f)
    {
        return TryGet(statisticId, out float value) ? value : defaultValue;
    }

    /// <summary>Sets the statistic value for <paramref name="statisticId"/>.</summary>
    public void Set(string statisticId, float value)
    {
        if (values == null || string.IsNullOrEmpty(statisticId) || !float.IsFinite(value))
        {
            return;
        }

        values[statisticId] = value;
    }

    /// <summary>Adds <paramref name="delta"/> to the statistic value for <paramref name="statisticId"/>.</summary>
    public void Add(string statisticId, float delta)
    {
        if (values == null || string.IsNullOrEmpty(statisticId) || !float.IsFinite(delta))
        {
            return;
        }

        float current = values.TryGetValue(statisticId, out float existing) ? existing : 0f;
        float updated = current + delta;
        if (float.IsFinite(updated))
        {
            values[statisticId] = updated;
        }
    }
}
