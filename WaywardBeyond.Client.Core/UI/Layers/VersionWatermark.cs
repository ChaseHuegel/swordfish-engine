using System.Numerics;
using Reef;
using Reef.UI;
using Swordfish.Graphics;
using Swordfish.Library.Util;

namespace WaywardBeyond.Client.Core.UI.Layers;

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
            ui.FontSize = 16;
            ui.Color = new Vector4(0.5f);
            ui.Constraints = new Constraints
            {
                Anchors = Anchors.Center | Anchors.Bottom,
            };
        }
        
        return Result.FromSuccess();
    }
}