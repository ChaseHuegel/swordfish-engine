using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Buffs;

/// <summary>Defines one stat bonus or penalty applied while a buff is active.</summary>
public readonly record struct BuffStatModifier(
    string StatId,
    float? FlatDelta = null,
    float? PercentDelta = null
)
{
    /// <summary>Validates the target stat ID and delta bounds.</summary>
    public Result<BuffStatModifier> Validate(string buffId)
    {
        if (string.IsNullOrWhiteSpace(StatId))
        {
            return Result<BuffStatModifier>.FromFailure($"Buff \"{buffId}\" has a stat modifier missing stat_id.");
        }

        if (FlatDelta.HasValue && !float.IsFinite(FlatDelta.Value))
        {
            return Result<BuffStatModifier>.FromFailure($"Buff \"{buffId}\" modifier for \"{StatId}\" has non-finite flat_delta.");
        }

        if (PercentDelta.HasValue && !float.IsFinite(PercentDelta.Value))
        {
            return Result<BuffStatModifier>.FromFailure($"Buff \"{buffId}\" modifier for \"{StatId}\" has non-finite percent_delta.");
        }

        if (!FlatDelta.HasValue && !PercentDelta.HasValue)
        {
            return Result<BuffStatModifier>.FromFailure($"Buff \"{buffId}\" modifier for \"{StatId}\" must specify flat_delta or percent_delta.");
        }

        if ((FlatDelta ?? 0f) == 0f && (PercentDelta ?? 0f) == 0f)
        {
            return Result<BuffStatModifier>.FromFailure($"Buff \"{buffId}\" modifier for \"{StatId}\" must have a non-zero delta.");
        }

        return Result<BuffStatModifier>.FromSuccess(this with { StatId = StatId.Trim() });
    }
}
