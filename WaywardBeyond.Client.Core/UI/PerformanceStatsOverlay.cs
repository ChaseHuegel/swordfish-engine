using System.Numerics;
using Reef;
using Reef.UI;
using Swordfish.ECS;
using Swordfish.Graphics;
using Swordfish.Library.Diagnostics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI;

public class PerformanceStatsOverlay(in IWindowContext windowContext, in IECSContext ecs) : IDebugOverlay
{
    private readonly IWindowContext _windowContext = windowContext;
    private readonly IECSContext _ecs = ecs;
    private readonly Sampler _fpsSampler = new(length: 15);
    private readonly Sampler _updateSampler = new(length: 15);
    private readonly Sampler _tickSampler = new(length: 15);

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

        using (ui.Element())
        {
            ui.LayoutDirection = LayoutDirection.Horizontal;
            ui.Spacing = 20;

            using (ui.Text($"FPS: {fpsSample.Median:F0}"))
            {
                if (fpsSample.Median < 30)
                {
                    ui.Color = new Vector4(1f, 0f, 0f, 1f);
                }
                else if (fpsSample.Median < 45)
                {
                    ui.Color = new Vector4(1f, 1f, 0f, 1f);
                }
            }
            
            using (ui.Text($"UPS: {upsSample.Median:F0}"))
            {
                if (upsSample.Median < 30)
                {
                    ui.Color = new Vector4(1f, 0f, 0f, 1f);
                }
                else if (upsSample.Median < 45)
                {
                    ui.Color = new Vector4(1f, 1f, 0f, 1f);
                }
            }

            using (ui.Text($"TPS: {tpsSample.Median:F0}"))
            {
                if (tpsSample.Median < 30)
                {
                    ui.Color = new Vector4(1f, 0f, 0f, 1f);
                }
                else if (tpsSample.Median < 45)
                {
                    ui.Color = new Vector4(1f, 1f, 0f, 1f);
                }
            }

        }

        return Result.FromSuccess();
    }
}