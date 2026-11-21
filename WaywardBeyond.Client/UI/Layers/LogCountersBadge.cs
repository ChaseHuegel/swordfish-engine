using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Diagnostics;

namespace WaywardBeyond.Client.UI.Layers;

/// <summary>Shows problem log totals in the bottom-right corner during debug builds.</summary>
internal sealed class LogCountersBadge(in LogCounters logCounters) : IUILayer
{
    private readonly LogCountersBadgeView _view = new();
    private readonly LogCounters _logCounters = logCounters;

    /// <inheritdoc/>
    public bool IsVisible() => _logCounters.GetSnapshot().HasProblems;

    /// <inheritdoc/>
    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        LogCountersSnapshot counters = _logCounters.GetSnapshot();
        if (!counters.HasProblems)
        {
            return Result.FromSuccess();
        }

        _view.Render(ui, new LogCountersBadgeState(counters.Warnings, counters.Errors, counters.Criticals));

        return Result.FromSuccess();
    }
}
