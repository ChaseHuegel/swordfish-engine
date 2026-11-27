using Swordfish.Library.Util;
using WaywardBeyond.Bricks;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// High-priority modifier source that reads any declared statistic from the targeted brick's
/// <see cref="Brick.Statistics"/> table and applies it as the base value.
/// </summary>
public sealed class BrickStatisticModifierSource(IBrickDatabase? brickDatabase = null) : IStatModifierSource
{
    /// <inheritdoc/>
    public StatModifierPriority Priority => StatModifierPriority.High;

    /// <inheritdoc/>
    public string? StatId => null;

    /// <inheritdoc/>
    public void Apply(in StatQuery query, ref StatAccumulator accumulator)
    {
        if (query.Subject is not (StatSubject.Any or StatSubject.Brick))
        {
            return;
        }

        Brick? brick = ResolveBrick(in query);
        if (brick == null || !brick.TryGetStatistic(query.StatId, out float value) || !float.IsFinite(value))
        {
            return;
        }

        accumulator.Apply(StatModifier.SetBase(value));
    }

    private Brick? ResolveBrick(in StatQuery query)
    {
        if (query.TargetBrick != null)
        {
            return query.TargetBrick;
        }

        if (brickDatabase == null || query.TargetBrickDataId == 0)
        {
            return null;
        }

        Result<Brick> result = brickDatabase.Get(query.TargetBrickDataId);
        return result.Success ? result.Value : null;
    }
}
