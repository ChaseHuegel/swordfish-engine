using System;
using System.Collections.Generic;
using Krill;
using Reef;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.Particles;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Util;
using Swordfish.Settings;

namespace WaywardBeyond.Client.UI;

/// <summary>Displays frame timing, tick rates, draw calls, and active particle counts in the F3 overlay.</summary>
public class PerformanceStatsOverlay(
    in IWindowContext windowContext,
    in IECSContext ecs,
    in IRenderer renderer,
    in IParticleService particleService,
    in RenderSettings renderSettings
) : IDebugOverlay
{
    private readonly StatLinesView _statLines = new();

    private const int SAMPLE_LENGTH = 60 * 2;

    private readonly IWindowContext _windowContext = windowContext;
    private readonly IECSContext _ecs = ecs;
    private readonly IRenderer _renderer = renderer;
    private readonly IParticleService _particleService = particleService;
    private readonly RenderSettings _renderSettings = renderSettings;
    private readonly Sampler _fpsSampler = new(SAMPLE_LENGTH);
    private readonly Sampler _updateSampler = new(SAMPLE_LENGTH);
    private readonly Sampler _tickSampler = new(SAMPLE_LENGTH);
    private readonly List<StatLine> _lines = [];

    /// <inheritdoc/>
    public bool IsVisible() => true;

    /// <inheritdoc/>
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

        using (ui.Text($"Draw calls: {_renderer.DrawCalls.Get()}")) { }

        string particlesLine = _renderSettings.Particles.Enabled.Get()
            ? $"Particles: {_particleService.ActiveParticleCount} / {Math.Max(0, _renderSettings.Particles.MaxParticles.Get())}"
            : "Particles: disabled";
        using (ui.Text(particlesLine)) { }

        _lines.Clear();
        _lines.Add(Line("FPS", fpsSample, warningBelow: 45d));
        _lines.Add(Line("UPS", upsSample, warningBelow: 60d));
        _lines.Add(Line("TPS", tpsSample, warningBelow: 60d));

        _statLines.Render(ui, new StatLinesState(_lines));

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
