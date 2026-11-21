using System.Collections.Generic;
using Reef;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.Diagnostics;

namespace WaywardBeyond.Client.UI;

/// <summary>Shows running warning, error, and critical log totals in the F3 overlay.</summary>
internal sealed class LogCountersOverlay(in LogCounters logCounters) : IDebugOverlay
{
    private readonly StatLinesView _statLines = new();
    private readonly LogCounters _logCounters = logCounters;
    private readonly List<StatLine> _lines = [];

    /// <inheritdoc/>
    public bool IsVisible() => true;

    /// <inheritdoc/>
    public Result RenderDebugOverlay(double delta, UIBuilder<Material> ui)
    {
        LogCountersSnapshot counters = _logCounters.GetSnapshot();

        _lines.Clear();
        _lines.Add(new StatLine($"LOGS {counters.Total}"));
        _lines.Add(new StatLine($"   WARN  {counters.Warnings}", Warning: counters.Warnings > 0));
        _lines.Add(new StatLine($"   ERR   {counters.Errors}", Danger: counters.Errors > 0));
        _lines.Add(new StatLine($"   CRIT  {counters.Criticals}", Danger: counters.Criticals > 0));

        _statLines.Render(ui, new StatLinesState(_lines));

        return Result.FromSuccess();
    }
}
