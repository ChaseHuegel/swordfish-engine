using Swordfish.Library.Util;
using WaywardBeyond.Items;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// High-priority modifier source that reads any declared statistic from the held item's
/// <see cref="Item.Statistics"/> table and applies it as the base value.
/// </summary>
public sealed class ItemStatisticModifierSource(IItemDatabase? itemDatabase = null) : IStatModifierSource
{
    /// <inheritdoc/>
    public StatModifierPriority Priority => StatModifierPriority.High;

    /// <inheritdoc/>
    public string? StatId => null;

    /// <inheritdoc/>
    public void Apply(in StatQuery query, ref StatAccumulator accumulator)
    {
        if (query.Subject is not (StatSubject.Any or StatSubject.Item))
        {
            return;
        }

        Item? item = ResolveItem(in query);
        if (item == null || !item.TryGetStatistic(query.StatId, out float value) || !float.IsFinite(value))
        {
            return;
        }

        accumulator.Apply(StatModifier.SetBase(value));
    }

    private Item? ResolveItem(in StatQuery query)
    {
        if (query.HeldItem != null)
        {
            return query.HeldItem;
        }

        if (itemDatabase == null || string.IsNullOrEmpty(query.HeldItemId))
        {
            return null;
        }

        Result<Item> result = itemDatabase.Get(query.HeldItemId);
        return result.Success ? result.Value : null;
    }
}
