using System.Collections.Generic;
using Swordfish.ECS;
using Swordfish.Graphics;
using Xunit;
using WaywardBeyond.Client.UI;
using WaywardBeyond.Client.UI.Components;
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

            UiSnapshotTests.Compare($"{width}x{height}-notification", width, height, ui =>
            {
                new NotificationView().Render(ui, new NotificationViewState("Saved", 0.8f, HasBackground: true));
            });

            UiSnapshotTests.Compare($"{width}x{height}-notification-plain", width, height, ui =>
            {
                new NotificationView().Render(ui, new NotificationViewState("Shape: Block", 1f, HasBackground: false));
            });

            UiSnapshotTests.Compare($"{width}x{height}-notification-bars", width, height, ui =>
            {
                var bars = new List<NotificationBarState>
                {
                    new("Mining", "Level 4", true, 0.45f, [162, 325], 1f, null),
                    new("Farming", string.Empty, false, 0.8f, [], 0.6f, new System.Numerics.Vector4(0.43f, 0.81f, 0.59f, 1f)),
                };
                new BarsView().Render(ui, new BarsState(bars));
            });

            UiSnapshotTests.Compare($"{width}x{height}-hotbar", width, height, ui =>
            {
                var icon = new Material(null!);
                var slots = new List<ItemSlotState>
                {
                    new(icon, "1", "12", Selected: true),
                    new(icon, "2"),
                    new(null!, "3"),
                    new(icon, "4", "3"),
                };
                new HotbarView().Render(ui, new HotbarState(slots));
            });

            UiSnapshotTests.Compare($"{width}x{height}-control-hints", width, height, ui =>
            {
                var hints = new List<HintViewState>
                {
                    new("TAB", "Inventory"),
                    new("R", "Change shape"),
                    new(string.Empty, "Send feedback"),
                };
                new ControlHintsView().Render(ui, new ControlHintsState(hints));
            });
        }
    }
}
