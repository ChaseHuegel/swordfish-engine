using System;
using System.Collections.Immutable;

namespace WaywardBeyond.Data.Statistics;

/// <summary>Well-known statistic identifiers, fixed-point scaling, and buff timing helpers shared across items, bricks, characters, and stat modifiers.</summary>
public static class StatisticIds
{
    /// <summary>Namespace prefix for built-in game statistics.</summary>
    public const string NAMESPACE_PREFIX = "wb:";

    /// <summary>Mining speed multiplier applied when breaking bricks (base 1.0).</summary>
    public const string MINING_SPEED = "wb:mining_speed";

    /// <summary>Default base value for <see cref="MINING_SPEED"/> when no item or modifier overrides it.</summary>
    public const float DEFAULT_MINING_SPEED = 1.0f;

    /// <summary>Base break hardness of a brick in seconds before mining speed modifiers apply (base 1.0).</summary>
    public const string HARDNESS = "wb:hardness";

    /// <summary>Default base value for <see cref="HARDNESS"/> when no brick or modifier overrides it.</summary>
    public const float DEFAULT_HARDNESS = 1.0f;

    /// <summary>Current health vital stat ID (fixed-point).</summary>
    public const string HEALTH = "wb:health";

    /// <summary>Maximum health vital stat ID (fixed-point).</summary>
    public const string HEALTH_MAX = "wb:health_max";

    /// <summary>Current oxygen vital stat ID (fixed-point).</summary>
    public const string OXYGEN = "wb:oxygen";

    /// <summary>Maximum oxygen vital stat ID (fixed-point).</summary>
    public const string OXYGEN_MAX = "wb:oxygen_max";

    /// <summary>Environmental pressurization flag stat ID (0 = vacuum, 1 = pressurized).</summary>
    public const string PRESSURIZED = "wb:pressurized";

    /// <summary>S.P.A.C.E.R. Strength attribute stat ID.</summary>
    public const string STRENGTH = "wb:strength";

    /// <summary>S.P.A.C.E.R. Precision attribute stat ID.</summary>
    public const string PRECISION = "wb:precision";

    /// <summary>S.P.A.C.E.R. Awareness attribute stat ID.</summary>
    public const string AWARENESS = "wb:awareness";

    /// <summary>S.P.A.C.E.R. Charisma attribute stat ID.</summary>
    public const string CHARISMA = "wb:charisma";

    /// <summary>S.P.A.C.E.R. Education attribute stat ID.</summary>
    public const string EDUCATION = "wb:education";

    /// <summary>S.P.A.C.E.R. Resolve attribute stat ID.</summary>
    public const string RESOLVE = "wb:resolve";

    /// <summary>Kinetic collision damage resistance modifier stat ID (fixed-point).</summary>
    public const string KINETIC_RESIST = "wb:kinetic_resist";

    /// <summary>Movement speed percentage modifier stat ID (fixed-point).</summary>
    public const string MOVE_SPEED_PCT = "wb:move_speed_pct";

    /// <summary>Prefix for active and persisted buff timing stat IDs.</summary>
    public const string BUFF_PREFIX = "wb:buff.";

    /// <summary>Suffix for active buff expiry tick stat IDs.</summary>
    public const string BUFF_EXPIRES_TICK_SUFFIX = ".expires_tick";

    /// <summary>Suffix for persisted buff remaining tick stat IDs.</summary>
    public const string BUFF_REMAINING_TICKS_SUFFIX = ".remaining_ticks";

    /// <summary>Prefix for per-item consumable cooldown stat IDs.</summary>
    public const string COOLDOWN_PREFIX = "wb:cooldown.";

    /// <summary>Fixed-point multiplier applied to fractional vital and modifier values stored as 64-bit integers.</summary>
    public const long FIXED_POINT_SCALE = 1000L;

    /// <summary>Default maximum health in user units.</summary>
    public const float DEFAULT_HEALTH_MAX = 100f;

    /// <summary>Default maximum oxygen in user units.</summary>
    public const float DEFAULT_OXYGEN_MAX = 100f;

    /// <summary>Default maximum health in fixed-point units.</summary>
    public const long DEFAULT_HEALTH_MAX_FIXED = 100L * FIXED_POINT_SCALE;

    /// <summary>Default maximum oxygen in fixed-point units.</summary>
    public const long DEFAULT_OXYGEN_MAX_FIXED = 100L * FIXED_POINT_SCALE;

    /// <summary>Minimum allowed value for a S.P.A.C.E.R. attribute.</summary>
    public const long MIN_ATTRIBUTE = 1L;

    /// <summary>Maximum allowed base value for a S.P.A.C.E.R. attribute seeded at join.</summary>
    public const long MAX_BASE_ATTRIBUTE = 8L;

    /// <summary>Upper bound on persisted server-side vital maximums in fixed-point units.</summary>
    public const long MAX_VITAL_FIXED = 1_000_000L * FIXED_POINT_SCALE;

    /// <summary>Ordered S.P.A.C.E.R. attribute stat IDs matching Strength through Resolve.</summary>
    public static readonly ImmutableArray<string> BaseAttributeIds =
    [
        STRENGTH,
        PRECISION,
        AWARENESS,
        CHARISMA,
        EDUCATION,
        RESOLVE,
    ];

    /// <summary>Converts a finite floating-point stat value into fixed-point representation.</summary>
    public static bool TryToFixed(float value, out long fixedValue)
    {
        if (!float.IsFinite(value))
        {
            fixedValue = 0L;
            return false;
        }

        double scaled = Math.Round((double)value * FIXED_POINT_SCALE);
        if (scaled >= long.MaxValue)
        {
            fixedValue = long.MaxValue;
        }
        else if (scaled <= long.MinValue)
        {
            fixedValue = long.MinValue;
        }
        else
        {
            fixedValue = (long)scaled;
        }

        return true;
    }

    /// <summary>Converts a floating-point stat value into fixed-point representation, returning zero when non-finite.</summary>
    public static long ToFixed(float value)
    {
        return TryToFixed(value, out long fixedValue) ? fixedValue : 0L;
    }

    /// <summary>Converts a fixed-point stat value back into floating-point user units.</summary>
    public static float FromFixed(long fixedValue)
    {
        return fixedValue / (float)FIXED_POINT_SCALE;
    }

    /// <summary>Builds the canonical per-item consumable cooldown stat ID for <paramref name="itemId"/>.</summary>
    public static string Cooldown(ReadOnlySpan<char> itemId)
    {
        if (itemId.IsEmpty || itemId.IsWhiteSpace())
        {
            throw new ArgumentException("Item ID must not be empty.", nameof(itemId));
        }

        return string.Concat(COOLDOWN_PREFIX, itemId);
    }

    /// <summary>Returns whether <paramref name="statId"/> names a per-item consumable cooldown entry.</summary>
    public static bool IsCooldownKey(ReadOnlySpan<char> statId)
    {
        return statId.Length > COOLDOWN_PREFIX.Length
            && statId.StartsWith(COOLDOWN_PREFIX.AsSpan(), StringComparison.Ordinal)
            && !statId[COOLDOWN_PREFIX.Length..].IsWhiteSpace();
    }

    /// <summary>Builds the canonical expiry-tick stat ID for <paramref name="buffId"/>.</summary>
    public static string BuffExpiresTick(ReadOnlySpan<char> buffId)
    {
        if (buffId.IsEmpty || buffId.IsWhiteSpace())
        {
            throw new ArgumentException("Buff ID must not be empty.", nameof(buffId));
        }

        return string.Concat(BUFF_PREFIX, buffId, BUFF_EXPIRES_TICK_SUFFIX);
    }

    /// <summary>Builds the canonical persisted remaining-ticks stat ID for <paramref name="buffId"/>.</summary>
    public static string BuffRemainingTicks(ReadOnlySpan<char> buffId)
    {
        if (buffId.IsEmpty || buffId.IsWhiteSpace())
        {
            throw new ArgumentException("Buff ID must not be empty.", nameof(buffId));
        }

        return string.Concat(BUFF_PREFIX, buffId, BUFF_REMAINING_TICKS_SUFFIX);
    }

    /// <summary>Returns whether <paramref name="statId"/> has a valid <c>&lt;namespace&gt;:&lt;name&gt;</c> stat identifier shape.</summary>
    public static bool IsNamespacedStatId(ReadOnlySpan<char> statId)
    {
        if (statId.IsEmpty || char.IsWhiteSpace(statId[0]) || char.IsWhiteSpace(statId[^1]))
        {
            return false;
        }

        int colonIndex = statId.IndexOf(':');
        if (colonIndex <= 0 || colonIndex >= statId.Length - 1)
        {
            return false;
        }

        ReadOnlySpan<char> namespacePart = statId[..colonIndex];
        for (var i = 0; i < namespacePart.Length; i++)
        {
            char c = namespacePart[i];
            if (c == '.' || char.IsWhiteSpace(c))
            {
                return false;
            }
        }

        return !statId[(colonIndex + 1)..].IsWhiteSpace();
    }

    /// <summary>Returns whether <paramref name="statId"/> stores an unscaled integer (attribute or flag) rather than fixed-point units.</summary>
    public static bool IsIntegerStat(ReadOnlySpan<char> statId)
    {
        if (statId.Equals(PRESSURIZED.AsSpan(), StringComparison.Ordinal))
        {
            return true;
        }

        if (IsCooldownKey(statId))
        {
            return true;
        }

        for (var i = 0; i < BaseAttributeIds.Length; i++)
        {
            if (statId.Equals(BaseAttributeIds[i].AsSpan(), StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Returns whether <paramref name="statId"/> names any buff timer entry (active expiry or persisted remaining ticks).</summary>
    public static bool IsBuffTimerKey(ReadOnlySpan<char> statId)
    {
        return IsBuffExpiresTickKey(statId) || IsBuffRemainingTicksKey(statId);
    }

    /// <summary>Returns whether <paramref name="statId"/> names an active buff expiry tick entry.</summary>
    public static bool IsBuffExpiresTickKey(ReadOnlySpan<char> statId)
    {
        return TryParseBuffId(statId, out _);
    }

    /// <summary>Returns whether <paramref name="statId"/> names a persisted buff remaining-ticks entry.</summary>
    public static bool IsBuffRemainingTicksKey(ReadOnlySpan<char> statId)
    {
        return TryParseBuffRemainingTicksId(statId, out _);
    }

    /// <summary>Extracts the buff ID slice from a canonical expiry-tick stat ID without allocating.</summary>
    public static bool TryParseBuffId(ReadOnlySpan<char> statId, out ReadOnlySpan<char> buffId)
    {
        return TryParseBuffKey(statId, BUFF_EXPIRES_TICK_SUFFIX.AsSpan(), out buffId);
    }

    /// <summary>Extracts the buff ID slice from a persisted remaining-ticks stat ID without allocating.</summary>
    public static bool TryParseBuffRemainingTicksId(ReadOnlySpan<char> statId, out ReadOnlySpan<char> buffId)
    {
        return TryParseBuffKey(statId, BUFF_REMAINING_TICKS_SUFFIX.AsSpan(), out buffId);
    }

    /// <summary>Returns whether <paramref name="statId"/> equals <c>wb:buff.&lt;buffId&gt;.expires_tick</c> without allocating.</summary>
    public static bool MatchesBuffExpiresTick(ReadOnlySpan<char> statId, ReadOnlySpan<char> buffId)
    {
        if (buffId.IsEmpty || buffId.IsWhiteSpace())
        {
            return false;
        }

        int expectedLength = BUFF_PREFIX.Length + buffId.Length + BUFF_EXPIRES_TICK_SUFFIX.Length;
        if (statId.Length != expectedLength)
        {
            return false;
        }

        return statId.StartsWith(BUFF_PREFIX.AsSpan(), StringComparison.Ordinal)
            && statId.Slice(BUFF_PREFIX.Length, buffId.Length).Equals(buffId, StringComparison.Ordinal)
            && statId.EndsWith(BUFF_EXPIRES_TICK_SUFFIX.AsSpan(), StringComparison.Ordinal);
    }

    /// <summary>Computes remaining buff duration in seconds from an authoritative expiry tick and fixed timestep.</summary>
    public static float ComputeRemainingSeconds(long expiresTick, long currentSimTick, float fixedDt)
    {
        if (expiresTick <= currentSimTick || fixedDt <= 0f || !float.IsFinite(fixedDt))
        {
            return 0f;
        }

        double remaining = ((double)expiresTick - currentSimTick) * fixedDt;
        return remaining >= float.MaxValue ? float.MaxValue : (float)remaining;
    }

    /// <summary>Computes the authoritative sim tick at which a buff with <paramref name="durationSeconds"/> expires.</summary>
    public static long ComputeExpiresTick(long currentSimTick, float durationSeconds, float fixedDt)
    {
        if (durationSeconds <= 0f || !float.IsFinite(durationSeconds) || fixedDt <= 0f || !float.IsFinite(fixedDt))
        {
            return currentSimTick;
        }

        double rawTicks = Math.Ceiling((double)durationSeconds / fixedDt);
        if (rawTicks >= long.MaxValue)
        {
            return long.MaxValue;
        }

        long ticks = Math.Max(1L, (long)rawTicks);
        return currentSimTick > long.MaxValue - ticks ? long.MaxValue : currentSimTick + ticks;
    }

    private static bool TryParseBuffKey(ReadOnlySpan<char> statId, ReadOnlySpan<char> suffix, out ReadOnlySpan<char> buffId)
    {
        int minLength = BUFF_PREFIX.Length + suffix.Length + 1;
        if (statId.Length < minLength
            || !statId.StartsWith(BUFF_PREFIX.AsSpan(), StringComparison.Ordinal)
            || !statId.EndsWith(suffix, StringComparison.Ordinal))
        {
            buffId = default;
            return false;
        }

        ReadOnlySpan<char> inner = statId.Slice(
            BUFF_PREFIX.Length,
            statId.Length - BUFF_PREFIX.Length - suffix.Length);
        if (inner.IsWhiteSpace())
        {
            buffId = default;
            return false;
        }

        buffId = inner;
        return true;
    }
}
