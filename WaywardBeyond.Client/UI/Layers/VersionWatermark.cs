using System.Numerics;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;
using WaywardBeyond.Client.UI.Theme;

namespace WaywardBeyond.Client.UI.Layers;

internal class VersionWatermark : IUILayer
{
    public bool IsVisible()
    {
        return true;
    }

    public Result RenderUI(double delta, UIBuilder<Material> ui)
    {
        using (ui.Text($"{WaywardBeyond.Version.Name}"))
        {
            ui.FontSize = UiMetrics.SmallFont;
            ui.Color = UiTheme.TextMuted * new Vector4(1f, 1f, 1f, 0.5f);
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
            };
        }
        
        return Result.FromSuccess();
    }
}