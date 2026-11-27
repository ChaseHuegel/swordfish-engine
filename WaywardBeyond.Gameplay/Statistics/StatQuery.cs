using Krill;
using WaywardBeyond.Bricks;
using WaywardBeyond.Items;

namespace WaywardBeyond.Gameplay.Statistics;

/// <summary>
/// Context describing one statistic query evaluated through <see cref="IStatResolver"/>.
/// Callers set <see cref="Subject"/> to identify which base source owns the statistic and populate
/// any additional context fields relevant to modifier sources.
/// </summary>
public readonly struct StatQuery(
    string statId,
    float defaultBase = 0f,
    StatSubject subject = StatSubject.Any,
    DataStore? store = null,
    int entity = -1,
    Item? heldItem = null,
    string? heldItemId = null,
    Brick? targetBrick = null,
    ushort targetBrickDataId = 0
) {
    /// <summary>The identifier of the statistic being evaluated.</summary>
    public readonly string StatId = statId;

    /// <summary>The fallback base value when no base-setting modifier applies.</summary>
    public readonly float DefaultBase = defaultBase;

    /// <summary>The primary subject supplying the base value for this query.</summary>
    public readonly StatSubject Subject = subject;

    /// <summary>Optional ECS store containing the acting entity.</summary>
    public readonly DataStore? Store = store;

    /// <summary>Optional acting entity handle inside <see cref="Store"/>, or -1 when absent.</summary>
    public readonly int Entity = entity;

    /// <summary>Optional resolved held item.</summary>
    public readonly Item? HeldItem = heldItem;

    /// <summary>Optional held item id used when <see cref="HeldItem"/> is not supplied.</summary>
    public readonly string? HeldItemId = heldItemId;

    /// <summary>Optional resolved target brick.</summary>
    public readonly Brick? TargetBrick = targetBrick;

    /// <summary>Optional target brick data id for target-scoped modifiers, or 0 when absent.</summary>
    public readonly ushort TargetBrickDataId = targetBrickDataId;
}
