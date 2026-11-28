using Swordfish.Library.Util;

namespace WaywardBeyond.Data.Buffs;

/// <summary>Defines one per-second periodic stat regeneration or drain rate applied while a buff is active.</summary>
public readonly record struct BuffRegenRate(
    string StatId,
    float PerSecond
)
{
    /// <summary>Validates the target stat ID and per-second rate.</summary>
    public Result<BuffRegenRate> Validate(string buffId)
    {
        if (string.IsNullOrWhiteSpace(StatId))
        {
            return Result<BuffRegenRate>.FromFailure($"Buff \"{buffId}\" has a regen rate missing StatId.");
        }

        string statId = StatId.Trim();
        if (!float.IsFinite(PerSecond) || PerSecond == 0f)
        {
            return Result<BuffRegenRate>.FromFailure($"Buff \"{buffId}\" regen rate for \"{statId}\" must be finite and non-zero.");
        }

        return Result<BuffRegenRate>.FromSuccess(this with { StatId = statId });
    }
}
