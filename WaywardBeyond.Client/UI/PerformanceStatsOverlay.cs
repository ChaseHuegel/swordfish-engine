using System.Collections.Generic;
using Reef;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.UI;

public class PerformanceStatsOverlay(in IWindowContext windowContext, in IECSContext ecs) : IDebugOverlay
{
    private const int SAMPLE_LENGTH = 60 * 2;

    private readonly IWindowContext _windowContext = windowContext;
    private readonly IECSContext _ecs = ecs;
    private readonly Sampler _fpsSampler = new(SAMPLE_LENGTH);
    private readonly Sampler _updateSampler = new(SAMPLE_LENGTH);
    private readonly Sampler _tickSampler = new(SAMPLE_LENGTH);

    public bool IsVisible() => true;

    public Result RenderDebugOverlay(double delta, UIBuilder<Material> ui)
    {
        double fps = 1000d / (_windowContext.RenderDelta.Get() * 1000d);
        _fpsSampler.Record(fps);
        Sample fpsSample = _fpsSampler.GetSnapshot();

        double ups = 1000d / (_windowContext.UpdateDelta.Get() * 1000d);
        _updateSampler.Record(ups);
        Sample upsSample = _updateSampler.GetSnapshot();

        double tps = 1000d / (_ecs.Delta.Get() * 1000d);
        _tickSampler.Record(tps);
        Sample tpsSample = _tickSampler.GetSnapshot();

        var lines = new List<StatLine>
        {
            Line("FPS", fpsSample, warningBelow: 45d),
            Line("UPS", upsSample, warningBelow: 60d),
            Line("TPS", tpsSample, warningBelow: 60d),
        };

        new StatLinesView().Render(ui, new StatLinesState(lines));

        return Result.FromSuccess();
    }

    private static StatLine Line(string label, Sample sample, double warningBelow)
    {
        var danger = sample.Median < 30d;
        var warning = !danger && sample.Median < warningBelow;

        return new StatLine(
            $"{label} M:{sample.Median:F0} / A:{sample.Average:F0} / L:{sample.Lowest:F0} / H:{sample.Highest:F0}",
            danger,
            warning
        );
    }
}
