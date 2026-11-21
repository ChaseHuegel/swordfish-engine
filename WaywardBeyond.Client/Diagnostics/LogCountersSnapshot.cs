namespace WaywardBeyond.Client.Diagnostics;

/// <summary>A point-in-time copy of the log counters.</summary>
internal readonly record struct LogCountersSnapshot(long Warnings, long Errors, long Criticals, long Others)
{
    /// <summary>Gets the total number of counted entries.</summary>
    public long Total => Warnings + Errors + Criticals + Others;

    /// <summary>Gets whether any warning, error, or critical entry was counted.</summary>
    public bool HasProblems => Total > 0;
}