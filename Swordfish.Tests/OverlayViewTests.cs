using System.Collections.Generic;
using Swordfish.ECS;
using Swordfish.Graphics;
using Xunit;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Layers;

namespace Swordfish.Tests;

public class OverlayViewTests
{
    [Fact]
    public void OverlayViewsMatchSnapshots()
    {
        int[] widths = [800, 1280, 1920];
        int[] heights = [600, 720, 1080];
        for (var i = 0; i < widths.Length; i++)
        {
            int width = widths[i];
            int height = heights[i];

            UiSnapshotTests.Compare($"{width}x{height}-version-watermark", width, height, ui =>
            {
                new VersionWatermarkView().Render(ui, new VersionWatermarkState("Wayward Beyond 0.1.0"));
            });

            UiSnapshotTests.Compare($"{width}x{height}-nameplates", width, height, ui =>
            {
                var nameplates = new List<NameplateInfo>
                {
                    new(Uuid.FromValue(1), "Ada", 200, 120, 14),
                    new(Uuid.FromValue(2), "Chase", 320, 180, 18),
                };
                new NameplateView().Render(ui, new NameplateState(nameplates));
            });

            UiSnapshotTests.Compare($"{width}x{height}-stat-lines", width, height, ui =>
            {
                var lines = new List<StatLine>
                {
                    new("FPS M:60 / A:59 / L:48 / H:61"),
                    new("UPS M:42 / A:43 / L:38 / H:45", Warning: true),
                    new("TPS M:20 / A:22 / L:18 / H:24", Danger: true),
                };
                new StatLinesView().Render(ui, new StatLinesState(lines));
            });

            UiSnapshotTests.Compare($"{width}x{height}-crosshair", width, height, ui =>
            {
                new CrosshairView().Render(ui, new CrosshairState(new Material(null!)));
            });
        }
    }
}
