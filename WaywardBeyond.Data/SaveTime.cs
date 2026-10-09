using System;

namespace WaywardBeyond.Data;

/// <summary>
/// Shared rule for accumulating playtime from wall-clock stamps. A zero <paramref name="lastPlayedMs"/>
/// means the subject has never been stamped, so no time is owed; the epoch must never leak in.
/// </summary>
public static class SaveTime
{
    public static long Accumulate(long ageMs, long lastPlayedMs, long nowMs)
    {
        if (lastPlayedMs <= 0)
        {
            return ageMs;
        }

        long deltaMs = nowMs - lastPlayedMs;
        return ageMs + Math.Max(0, deltaMs);
    }
}