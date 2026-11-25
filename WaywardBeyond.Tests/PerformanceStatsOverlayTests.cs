#nullable enable
using System;
using System.Collections.Generic;
using System.Numerics;
using Krill;
using Reef;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Graphics.Particles;
using Swordfish.Library.Types;
using Swordfish.Library.Util;
using Swordfish.Settings;
using Swordfish.Tests;
using WaywardBeyond.Client.UI;
using Xunit;

namespace WaywardBeyond.Tests;

/// <summary>Covers the F3 <see cref="PerformanceStatsOverlay"/> particle count and state display.</summary>
public sealed class PerformanceStatsOverlayTests
{
    [Fact]
    public void RenderDebugOverlayShowsActiveParticleCountAndDisabledState()
    {
        var windowContext = new FakeWindowContext();
        windowContext.RenderDelta.Set(1d / 60d);
        windowContext.UpdateDelta.Set(1d / 60d);

        var world = new World();
        var ecs = new StubEcsContext(world);
        ecs.Delta.Set(1d / 60d);

        var renderer = new StubRenderer();
        renderer.DrawCalls.Set(12);

        var particles = new StubParticleService { ActiveParticleCount = 37 };
        var renderSettings = new RenderSettings();
        var overlay = new PerformanceStatsOverlay(windowContext, ecs, renderer, particles, renderSettings);

        var ui = new UIBuilder<Material>(1280, 720, new StubTextEngine(), new UIController());

        Result activeResult = overlay.RenderDebugOverlay(0.016d, ui);
        Assert.True(activeResult.Success);
        List<RenderCommand<Material>> activeCommands = ui.Build(0d);
        Assert.Contains(activeCommands, cmd => cmd.Text == "Draw calls: 12");
        Assert.Contains(activeCommands, cmd => cmd.Text == "Particles: 37 / 4096");
        Assert.Contains(activeCommands, cmd => cmd.Text != null && cmd.Text.StartsWith("FPS ", StringComparison.Ordinal));
        Assert.Contains(activeCommands, cmd => cmd.Text != null && cmd.Text.StartsWith("UPS ", StringComparison.Ordinal));
        Assert.Contains(activeCommands, cmd => cmd.Text != null && cmd.Text.StartsWith("TPS ", StringComparison.Ordinal));

        particles.ActiveParticleCount = 0;
        renderSettings.Particles.MaxParticles.Set(-5);
        Result clampedResult = overlay.RenderDebugOverlay(0.016d, ui);
        Assert.True(clampedResult.Success);
        List<RenderCommand<Material>> clampedCommands = ui.Build(0.016d);
        Assert.Contains(clampedCommands, cmd => cmd.Text == "Particles: 0 / 0");

        renderSettings.Particles.Enabled.Set(false);
        Result disabledResult = overlay.RenderDebugOverlay(0.016d, ui);
        Assert.True(disabledResult.Success);
        List<RenderCommand<Material>> disabledCommands = ui.Build(0.032d);
        Assert.Contains(disabledCommands, cmd => cmd.Text == "Particles: disabled");
    }

    private sealed class StubEcsContext(World world) : IECSContext
    {
        public DataBinding<double> Delta { get; } = new(0d);
        public World World { get; } = world;
    }

    private sealed class StubRenderer : IRenderer
    {
        public DataBinding<int> DrawCalls { get; } = new(0);

        public Texture Screenshot() => throw new NotSupportedException();
    }

    private sealed class StubParticleService : IParticleService
    {
        public int ActiveParticleCount { get; set; }

        public void EmitBurst(in ParticleBurstRequest request) { }

        public Result EmitBurst(string effectId, Vector3 position, Vector3 direction, int? count = null)
        {
            return Result.FromSuccess();
        }

        public void Clear() { }
    }
}
