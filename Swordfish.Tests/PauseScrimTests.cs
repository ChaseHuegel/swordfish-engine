using Reef;
using Reef.UI;
using Swordfish.Graphics;
using WaywardBeyond.Client.UI.Layers.Menus.Pause;
using WaywardBeyond.Client.UI.Theme;
using Xunit;

namespace Swordfish.Tests;

public class PauseScrimTests
{
    [Fact]
    public void PauseScrimMatchesLegacy()
    {
        int[] widths = [800, 1280, 1920];
        int[] heights = [600, 720, 1080];
        for (var i = 0; i < widths.Length; i++)
        {
            int width = widths[i];
            int height = heights[i];
            UiSnapshotTests.Compare($"{width}x{height}-pause-scrim", width, height, ui =>
            {
                using (ui.Element())
                {
                    ui.Color = UiTheme.ScrimSoft;
                    ui.Constraints = new Constraints
                    {
                        Width = new Relative(1f),
                        Height = new Relative(1f),
                    };
                }
            }, folder: "Legacy/Pages", allowUpdate: false);

            UiSnapshotTests.AssertPageMatchesLegacy($"{width}x{height}-pause-scrim", width, height, ui =>
            {
                new PauseScrimView().Render(ui);
            });
        }
    }
}
