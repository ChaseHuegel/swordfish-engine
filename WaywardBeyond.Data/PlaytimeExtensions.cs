using System;

namespace WaywardBeyond.Data;

/// <summary>Advances a <see cref="Playtime"/> by the wall-clock elapsed since it was last stamped.</summary>
public static class PlaytimeExtensions
{
    /// <summary>
    ///     Returns a <see cref="Playtime"/> stamped at <paramref name="nowMs"/> with the elapsed session
    ///     folded into its age. A zero <c>LastPlayedMs</c> means the subject was never stamped, so no time
    ///     is owed and the epoch never leaks in.
    /// </summary>
    public static Playtime Advance(this in Playtime playtime, long nowMs)
    {
        long ageMs = playtime.LastPlayedMs <= 0
            ? playtime.AgeMs
            : playtime.AgeMs + Math.Max(0, nowMs - playtime.LastPlayedMs);

        return new Playtime(_LastPlayedMs: nowMs, _AgeMs: ageMs);
    }
}