namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Display data for the log counter badge.</summary>
internal readonly record struct LogCountersBadgeState(long Warnings, long Errors, long Criticals)
{
    /// <summary>Gets whether the badge shows warnings.</summary>
    public bool HasWarnings => Warnings > 0;

    /// <summary>Gets whether the badge shows errors.</summary>
    public bool HasErrors => Errors > 0;

    /// <summary>Gets whether the badge shows critical entries.</summary>
    public bool HasCriticals => Criticals > 0;
}

/// <summary>Draws running problem log totals in the bottom-right corner.</summary>
internal partial class LogCountersBadgeView
{
}
